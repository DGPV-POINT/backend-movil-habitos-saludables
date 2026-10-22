# HealthyMind API (.NET 10)

Backend de **HealthyMindIA**: un solo servicio que reemplaza los microservicios Java del repo de referencia
(auth, user, habit y statistics + Keycloak). Registro y login propios, PostgreSQL en Docker, recomendaciones
automáticas con IA vía Groq y soporte para que la app MAUI trabaje sin conexión (SQLite) y sincronice después.

## Arquitectura hexagonal

```
src/
  HealthyMind.Domain          Entidades y reglas (sin dependencias): User, RefreshToken, Habit, HabitRecord,
                              AiRecommendation, ComplianceCalculator, StreakCalculator
  HealthyMind.Application     Casos de uso + puertos
     Ports/In                 Lo que la API puede pedir (IAuthUseCases, IHabitUseCases, ISyncUseCases, ...)
     Ports/Out                Lo que la aplicación necesita del mundo (repositorios, hash, JWT, IA)
     Features/*               Implementación de cada caso de uso, DTOs y validaciones (FluentValidation)
  HealthyMind.Infrastructure  Adaptadores de salida: EF Core + PostgreSQL, PBKDF2, JWT, cliente Groq,
                              y el proceso en segundo plano que genera recomendaciones
  HealthyMind.Api             Adaptador de entrada HTTP: controladores delgados, JWT, ProblemDetails, rate limit
tests/
  HealthyMind.UnitTests         Reglas de dominio y cálculos (sin base de datos)
  HealthyMind.IntegrationTests  API real contra PostgreSQL desechable (Testcontainers, requiere Docker)
```

Las dependencias apuntan hacia adentro: Domain no conoce a nadie; Application solo a Domain;
Infrastructure implementa los puertos de Application; Api compone todo.

## Para el equipo (primera vez)

1. Instalar [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0) y Docker Desktop (abrirlo antes de seguir).
2. Clonar y entrar a la carpeta:
   ```bash
   git clone https://github.com/mguerron22/backend-mobil-habitos-saludables.git
   ```
3. Cada uno pone **su propia** key de Groq (gratis en https://console.groq.com/keys). Queda guardada en tu PC,
   fuera del repositorio; nunca la pongas en un archivo del proyecto ni la subas:
   ```bash
   dotnet user-secrets set "Groq:ApiKey" "TU_KEY_DE_GROQ" --project src/HealthyMind.Api
   ```
   Sin key todo funciona igual, solo no se generan recomendaciones.
4. Levantar base y API (pasos de la sección siguiente) e importar `insomnia/HealthyMind.insomnia.json` en Insomnia.

Si solo una persona corre el backend y los demás conectan la app desde sus teléfonos a la IP de esa PC,
la key solo la necesita esa PC (y su firewall debe permitir el puerto 5080).

## Levantar en local

Requisitos: .NET SDK 10 y Docker Desktop.

```bash
docker compose up -d
```

```bash
dotnet user-secrets set "Groq:ApiKey" "TU_KEY_DE_GROQ" --project src/HealthyMind.Api
```

```bash
dotnet run --project src/HealthyMind.Api
```

- API: http://localhost:5080 (escucha en todas las interfaces: el emulador Android entra por `http://10.0.2.2:5080`
  y un teléfono físico por la IP de tu PC).
- Documentación interactiva (Scalar): http://localhost:5080/scalar
- En desarrollo las migraciones se aplican solas y se crea el admin `admin` / `Admin12345`
  (ver `appsettings.Development.json`; solo para desarrollo).
- pgAdmin opcional en el navegador: `docker compose --profile tools up -d` → http://localhost:5050
  (servidor: host `postgres`, puerto `5432`, usuario `healthymind`, clave `healthymind_dev`).
  Desde tu PC la base está en `localhost:5433`.

Sin la key de Groq todo funciona igual; solo no se generan recomendaciones (queda un aviso en el log).

### Pruebas

```bash
dotnet test
```

### Insomnia

Importar `insomnia/HealthyMind.insomnia.json` (Insomnia → Import). Trae 49 peticiones en carpetas por requerimiento
y entornos *Local* y *Emulador Android*. Ejecutar **1. Autenticación → Registro** (o Login): los scripts guardan
el token y los ids en el entorno, y las demás peticiones los usan solos. Para regenerarlo: `python tools/generar_insomnia.py`.

## Cómo encaja con la app móvil

### Login una vez, después huella

1. La app genera un `deviceId` estable (un GUID guardado en `SecureStorage`) y hace `POST /api/auth/login`
   (o `register`) con usuario y contraseña. Es la única vez que se pide contraseña.
2. Guarda el `refreshToken` en `SecureStorage` y ofrece activar la huella.
3. Al abrir la app: valida la huella (plugin de biometría), lee el `refreshToken` y llama a
   `POST /api/auth/refresh` con el mismo `deviceId`. Recibe tokens nuevos y **reemplaza** el refresh guardado.
4. El `accessToken` dura 15 minutos; el `refreshToken`, 60 días desde el último uso (`Auth:RefreshTokenLifetimeDays`).

Reglas que la app debe respetar:
- **No llamar a `refresh` dos veces en paralelo**: un refresh token usado dos veces se toma como robo y cierra la
  sesión del dispositivo (vuelve a pedir contraseña).
- Un `401` en `refresh` → pedir contraseña. Un `403` → cuenta suspendida.
- Sin internet la huella solo desbloquea los datos locales de SQLite; el refresh se hace al volver la conexión.

### Sin conexión (SQLite) y sincronización

- La app crea hábitos y registros con **ids GUID propios**; la API los acepta tal cual (campo `id` opcional en los
  POST y obligatorio en el push), así no hay que remapear nada.
- Para avisar "sin internet", combinar `Connectivity` de MAUI con `GET /health` (anónimo): hay wifi no siempre
  significa que el servidor responda.
- Al recuperar conexión: **primero** `POST /api/sync/push` con lo pendiente y **después**
  `GET /api/sync/pull?since={serverTime}` con el `serverTime` del pull anterior (sin `since` = descarga completa).
- Cada elemento del push vuelve con:
  - `Applied`: marcarlo sincronizado. Si trae `serverId` distinto, reemplazar el id local por ese
    (pasa si otro teléfono ya registró ese mismo día).
  - `Stale`: el servidor tiene algo más nuevo; quedarse con lo que llegue en el pull.
  - `Rejected`: nunca se aplicará (p. ej. fecha futura, hábito eliminado); descartarlo y avisar.
- Conflictos: gana el cambio con `updatedAt` más reciente; las bajas son definitivas y viajan como
  `isDeleted: true` en el pull.
- El pull puede repetir elementos que ya tenías (margen de seguridad de 30 s): aplicarlos como *upsert*.

### Contador de pasos (RF06)

Crear el hábito con `trackingMode: "StepCounter"` y reportar el conteo del sensor con
`PUT /api/habits/{habitId}/records/{yyyy-MM-dd}` (crea o actualiza, se puede llamar varias veces al día).

## Recomendaciones con IA (RF05)

> **Modelo:** la propuesta pide Llama 3.3, pero Groq lo retiró (en octubre de 2026 responde `model_not_found`).
> Por defecto se usa `openai/gpt-oss-120b` con `Groq:ReasoningEffort = low`. También funciona `qwen/qwen3.8-27b`
> (en ese caso dejar `ReasoningEffort` vacío). Se cambia solo con configuración, sin tocar código.

Las genera solo el `RecommendationBackgroundService` (cada `RecommendationJob:IntervalMinutes`) para cada usuario
con actividad nueva desde su última recomendación, como máximo una cada `Recommendations:MinHoursBetween` horas.
Analiza los últimos 30 días: cumplimiento por hábito y por día de la semana, rachas y valores promedio.
A Groq solo se envían cifras y nombres de hábitos, nunca nombre, correo ni ubicación del usuario.
La app las lee con `GET /api/recommendations/latest` o le llegan en el pull.

Para probar la key sin esperar: `POST /api/admin/recommendations/users/{userId}` (rol Admin).

## Configuración

| Clave | Para qué | Dónde poner el valor real |
|---|---|---|
| `ConnectionStrings:HealthyMind` | PostgreSQL | variable de entorno en producción |
| `Jwt:SigningKey` | firma de tokens (≥ 32 caracteres) | variable de entorno / user-secrets |
| `Groq:ApiKey` | key de Groq | user-secrets o `Groq__ApiKey` |
| `BootstrapAdmin:*` | crea el primer admin si no existe | solo desarrollo o primer despliegue |
| `Auth:RefreshTokenLifetimeDays` | días sin abrir la app antes de pedir contraseña | appsettings |
| `RecommendationJob:*`, `Recommendations:*` | frecuencia y alcance de la IA | appsettings |

## Endpoints

| Área | Método y ruta |
|---|---|
| Salud | `GET /health` |
| Auth | `POST /api/auth/register` · `login` · `refresh` · `logout` |
| Sesiones | `GET /api/auth/sessions` · `DELETE /api/auth/sessions/{id}` · `DELETE /api/auth/sessions` |
| Perfil | `GET/PUT /api/profile` · `PUT /api/profile/password` |
| Hábitos | `GET/POST /api/habits` · `GET/PUT/DELETE /api/habits/{id}` · `PATCH /api/habits/{id}/status` |
| Registros | `GET/POST /api/habits/{id}/records` · `PUT /api/habits/{id}/records/{fecha}` · `GET/PUT/DELETE /api/records/{id}` |
| Estadísticas | `GET /api/statistics/dashboard` · `daily` · `weekly` · `monthly` · `range` |
| Recomendaciones | `GET /api/recommendations/latest` · `GET /api/recommendations` · `GET /api/recommendations/{id}` |
| Sincronización | `POST /api/sync/push` · `GET /api/sync/pull` |
| Admin | `GET/POST /api/admin/users` · `GET /api/admin/users/{id}` · `PATCH .../suspend` · `PATCH .../reactivate` · `POST /api/admin/recommendations/run` · `POST /api/admin/recommendations/users/{id}` |

Los errores siguen ProblemDetails (RFC 9457) con mensajes en español: 400 validación (con `errors` por campo),
401 sin sesión, 403 sin permiso o cuenta suspendida, 404, 409 duplicado, 422 regla de negocio,
429 demasiados intentos de login, 503 IA no disponible.

## Pendiente (fuera de este alcance)

- Recuperación de contraseña por correo: el repo Java la delegaba en Keycloak; aquí hace falta un servidor SMTP.
