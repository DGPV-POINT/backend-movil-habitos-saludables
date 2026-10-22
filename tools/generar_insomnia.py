"""Genera insomnia/HealthyMind.insomnia.json. Ejecutar desde Backend: python tools/generar_insomnia.py"""
import json
import pathlib

WS = "wrk_healthymind"
resources = []
counter = 0

TODAY = "{% now 'custom', 'YYYY-MM-DD' %}"
NOW = "{% now 'iso-8601', '' %}"


def save(*pairs):
    """Script post-respuesta que guarda valores del JSON de respuesta en el entorno."""
    lines = "const body = insomnia.response.json();\n"
    for var, expr in pairs:
        lines += f"if (body && {expr}) insomnia.environment.set('{var}', {expr});\n"
    return lines


def folder(name, desc=""):
    global counter
    counter += 1
    fid = f"fld_{counter:02d}"
    resources.append({"_id": fid, "_type": "request_group", "parentId": WS, "name": name,
                      "description": desc, "metaSortKey": counter})
    return fid


def req(parent, name, method, path, body=None, auth="user", desc="", script=None):
    global counter
    counter += 1
    r = {"_id": f"req_{counter:03d}", "_type": "request", "parentId": parent, "name": name, "method": method,
         "url": "{{ _.baseUrl }}" + path, "description": desc, "metaSortKey": counter,
         "headers": [], "parameters": [], "authentication": {}, "body": {}}
    if body is not None:
        r["body"] = {"mimeType": "application/json", "text": json.dumps(body, ensure_ascii=False, indent=2)}
        r["headers"].append({"name": "Content-Type", "value": "application/json"})
    if auth == "user":
        r["authentication"] = {"type": "bearer", "token": "{{ _.accessToken }}"}
    elif auth == "admin":
        r["authentication"] = {"type": "bearer", "token": "{{ _.adminToken }}"}
    if script:
        r["afterResponseScript"] = script
    resources.append(r)


auth_save = save(("accessToken", "body.accessToken"), ("refreshToken", "body.refreshToken"),
                 ("userId", "body.user && body.user.id"))

f = folder("0. Salud", "Ping sin token: el móvil lo usa para saber si hay conexión real con el servidor.")
req(f, "Health check", "GET", "/health", auth=None)

f = folder("1. Autenticación", "Ejecutar Registro o Login primero: guardan accessToken y refreshToken en el entorno.")
req(f, "Registro", "POST", "/api/auth/register", {
    "username": "{{ _.username }}", "email": "{{ _.email }}", "firstName": "Ana", "lastName": "Pérez",
    "password": "{{ _.password }}", "deviceId": "{{ _.deviceId }}", "deviceName": "Insomnia",
    "timeZoneId": "America/Guayaquil"},
    auth=None, script=auth_save, desc="Crea la cuenta (rol User) y deja la sesión abierta en el dispositivo. 201.")
req(f, "Login (contraseña, una sola vez)", "POST", "/api/auth/login", {
    "usernameOrEmail": "{{ _.username }}", "password": "{{ _.password }}", "deviceId": "{{ _.deviceId }}",
    "deviceName": "Insomnia"},
    auth=None, script=auth_save, desc="5 fallos seguidos bloquean la cuenta 15 minutos.")
req(f, "Refresh (lo que hace la huella)", "POST", "/api/auth/refresh", {
    "refreshToken": "{{ _.refreshToken }}", "deviceId": "{{ _.deviceId }}"},
    auth=None, script=auth_save,
    desc="Rota el refresh token. Si reenvías uno ya usado, se cierra la sesión del dispositivo (401).")
req(f, "Logout", "POST", "/api/auth/logout", {"refreshToken": "{{ _.refreshToken }}"}, auth=None)

f = folder("2. Perfil y sesiones")
req(f, "Mi perfil", "GET", "/api/profile")
req(f, "Actualizar perfil", "PUT", "/api/profile", {
    "firstName": "Ana María", "lastName": "Pérez", "email": "{{ _.email }}", "timeZoneId": "America/Guayaquil"})
req(f, "Cambiar contraseña", "PUT", "/api/profile/password", {
    "currentPassword": "{{ _.password }}", "newPassword": "Clave98765", "deviceId": "{{ _.deviceId }}"},
    desc="Cierra las sesiones de los demás dispositivos. Luego actualiza la variable password del entorno.")
req(f, "Dispositivos con sesión", "GET", "/api/auth/sessions", script=save(("sessionId", "body[0] && body[0].id")))
req(f, "Cerrar sesión de un dispositivo", "DELETE", "/api/auth/sessions/{{ _.sessionId }}")
req(f, "Cerrar sesión en todos los dispositivos", "DELETE", "/api/auth/sessions")

f = folder("3. Hábitos (RF02)")
req(f, "Crear hábito", "POST", "/api/habits", {
    "name": "Tomar agua", "category": "Hidratacion", "goal": 8, "unit": "vasos", "scheduledDays": [],
    "trackingMode": "Manual"},
    script=save(("habitId", "body.id")),
    desc="Categorías: Salud, Ejercicio, Nutricion, Lectura, Productividad, Bienestar, Hidratacion. "
         "scheduledDays vacío = todos los días.")
req(f, "Crear hábito de pasos (RF06)", "POST", "/api/habits", {
    "name": "Caminar", "category": "Ejercicio", "goal": 6000, "unit": "pasos",
    "scheduledDays": ["Monday", "Wednesday", "Friday"], "trackingMode": "StepCounter"},
    script=save(("stepsHabitId", "body.id")))
req(f, "Listar mis hábitos", "GET", "/api/habits")
req(f, "Filtrar por categoría / búsqueda / estado", "GET", "/api/habits?category=Hidratacion&search=agua&isActive=true")
req(f, "Ver hábito", "GET", "/api/habits/{{ _.habitId }}")
req(f, "Editar hábito", "PUT", "/api/habits/{{ _.habitId }}", {
    "name": "Tomar agua", "category": "Hidratacion", "goal": 10, "unit": "vasos", "scheduledDays": [],
    "trackingMode": "Manual"})
req(f, "Pausar hábito", "PATCH", "/api/habits/{{ _.habitId }}/status", {"isActive": False})
req(f, "Reactivar hábito", "PATCH", "/api/habits/{{ _.habitId }}/status", {"isActive": True})

f = folder("4. Registros de cumplimiento (RF03)")
req(f, "Registrar el día de hoy (con ubicación)", "POST", "/api/habits/{{ _.habitId }}/records", {
    "recordDate": TODAY, "completedValue": 8, "notes": "Antes del almuerzo", "latitude": -0.1807,
    "longitude": -78.4678},
    script=save(("recordId", "body.id")),
    desc="409 si ya existe ese día; 422 si la fecha es futura o el hábito está pausado.")
req(f, "Pasos del día (crea o actualiza)", "PUT", "/api/habits/{{ _.stepsHabitId }}/records/" + TODAY, {
    "completedValue": 7200, "notes": None, "latitude": None, "longitude": None},
    desc="Idempotente: el móvil lo llama varias veces al día con el conteo del sensor.")
req(f, "Historial del hábito", "GET", "/api/habits/{{ _.habitId }}/records")
req(f, "Historial por rango", "GET", "/api/habits/{{ _.habitId }}/records?from=2026-10-01&to=" + TODAY)
req(f, "Ver registro", "GET", "/api/records/{{ _.recordId }}")
req(f, "Corregir registro", "PUT", "/api/records/{{ _.recordId }}", {
    "completedValue": 6, "notes": "Corregido", "latitude": None, "longitude": None})
req(f, "Eliminar registro", "DELETE", "/api/records/{{ _.recordId }}")
req(f, "Eliminar hábito (al final: borra también sus registros)", "DELETE", "/api/habits/{{ _.habitId }}",
    desc="Baja lógica del hábito y sus registros; se propaga a los dispositivos en el siguiente pull.")

f = folder("5. Estadísticas (RF04)")
req(f, "Dashboard (inicio)", "GET", "/api/statistics/dashboard")
req(f, "Diario (hoy)", "GET", "/api/statistics/daily")
req(f, "Diario (fecha)", "GET", "/api/statistics/daily?date=" + TODAY)
req(f, "Semanal (lunes a domingo)", "GET", "/api/statistics/weekly")
req(f, "Mensual (actual)", "GET", "/api/statistics/monthly")
req(f, "Mensual (año y mes)", "GET", "/api/statistics/monthly?year=2026&month=10")
req(f, "Rango (máx. 366 días)", "GET", "/api/statistics/range?from=2026-10-01&to=" + TODAY)

f = folder("6. Recomendaciones IA (RF05)",
           "Se generan solas cada cierto tiempo con Groq (modelo en Groq:Model). Para forzar una, usar la carpeta Administración.")
req(f, "Última recomendación", "GET", "/api/recommendations/latest", desc="204 si todavía no hay ninguna.")
req(f, "Historial", "GET", "/api/recommendations?page=1&pageSize=20",
    script=save(("recommendationId", "body.items && body.items[0] && body.items[0].id")))
req(f, "Ver recomendación", "GET", "/api/recommendations/{{ _.recommendationId }}")

f = folder("7. Sincronización offline (RNF05)",
           "Al recuperar conexión: primero push, luego pull con el serverTime guardado.")
req(f, "Pull completo (primera vez)", "GET", "/api/sync/pull", script=save(("serverTime", "body.serverTime")))
req(f, "Push de cambios hechos sin conexión", "POST", "/api/sync/push", {
    "deviceId": "{{ _.deviceId }}",
    "habits": [{"id": "{{ _.offlineHabitId }}", "name": "Leer", "category": "Lectura", "goal": 20,
                "unit": "páginas", "scheduledDays": [], "trackingMode": "Manual", "isActive": True,
                "isDeleted": False, "updatedAt": NOW}],
    "records": [{"id": "{{ _.offlineRecordId }}", "habitId": "{{ _.offlineHabitId }}", "recordDate": TODAY,
                 "completedValue": 25, "notes": "Leído en el bus, sin señal", "latitude": None, "longitude": None,
                 "isDeleted": False, "updatedAt": NOW}]},
    desc="Cada elemento vuelve Applied, Stale (el servidor tiene algo más nuevo) o Rejected (regla de negocio). "
         "Si trae serverId, el móvil reemplaza su id local por ese.")
req(f, "Push: eliminar lo creado offline", "POST", "/api/sync/push", {
    "deviceId": "{{ _.deviceId }}",
    "habits": [{"id": "{{ _.offlineHabitId }}", "name": "Leer", "category": "Lectura", "goal": 20,
                "unit": "páginas", "scheduledDays": [], "trackingMode": "Manual", "isActive": True,
                "isDeleted": True, "updatedAt": NOW}],
    "records": []})
req(f, "Pull incremental (desde serverTime)", "GET", "/api/sync/pull?since={{ _.serverTime }}",
    script=save(("serverTime", "body.serverTime")))

f = folder("8. Administración (RF01)",
           "Ejecutar primero Login administrador. El admin inicial de desarrollo está en appsettings.Development.json.")
req(f, "Login administrador", "POST", "/api/auth/login", {
    "usernameOrEmail": "{{ _.adminUsername }}", "password": "{{ _.adminPassword }}", "deviceId": "insomnia-admin",
    "deviceName": "Insomnia"},
    auth=None, script=save(("adminToken", "body.accessToken")))
req(f, "Buscar usuarios", "GET", "/api/admin/users?search={{ _.username }}&page=1&pageSize=20", auth="admin",
    script=save(("targetUserId", "body.items && body.items[0] && body.items[0].id")),
    desc="Filtros opcionales: search, status (Active | Suspended).")
req(f, "Ver usuario", "GET", "/api/admin/users/{{ _.targetUserId }}", auth="admin")
req(f, "Crear usuario", "POST", "/api/admin/users", {
    "username": "coach.demo", "email": "coach.demo@healthymind.local", "firstName": "Coach", "lastName": "Demo",
    "password": "Clave1234", "role": "User", "timeZoneId": "America/Guayaquil"}, auth="admin",
    desc="role: User o Admin.")
req(f, "Suspender cuenta", "PATCH", "/api/admin/users/{{ _.targetUserId }}/suspend", auth="admin",
    desc="Corta al instante: el access token deja de servir y se cierran sus sesiones.")
req(f, "Reactivar cuenta", "PATCH", "/api/admin/users/{{ _.targetUserId }}/reactivate", auth="admin")
req(f, "IA: generar ya para un usuario", "POST", "/api/admin/recommendations/users/{{ _.targetUserId }}",
    auth="admin", desc="Prueba la API key de Groq sin esperar al proceso automático. 204 si el usuario no tiene hábitos; 503 si Groq no está configurado o falla.")
req(f, "IA: correr la generación automática ahora", "POST", "/api/admin/recommendations/run", auth="admin")

env = {
    "baseUrl": "http://localhost:5080", "deviceId": "insomnia-device-01", "username": "demo.user",
    "email": "demo.user@healthymind.local", "password": "Clave1234",
    "adminUsername": "admin", "adminPassword": "Admin12345",
    "accessToken": "", "refreshToken": "", "adminToken": "", "userId": "", "targetUserId": "", "sessionId": "",
    "habitId": "", "stepsHabitId": "", "recordId": "", "recommendationId": "", "serverTime": "",
    "offlineHabitId": "3f7c2a10-5b1e-4c8e-9d2a-6a1b2c3d4e5f",
    "offlineRecordId": "8a9b0c1d-2e3f-4a5b-8c6d-7e8f9a0b1c2d",
}

export = {
    "_type": "export", "__export_format": 4, "__export_source": "healthymind.generator",
    "resources": [
        {"_id": WS, "_type": "workspace", "parentId": None, "name": "HealthyMind API", "scope": "collection",
         "description": "Backend .NET 10 de HealthyMindIA. Empezar por 1. Autenticación: el resto usa el token guardado."},
        {"_id": "env_base", "_type": "environment", "parentId": WS, "name": "Base Environment", "data": env,
         "dataPropertyOrder": {"&": list(env)}},
        {"_id": "env_local", "_type": "environment", "parentId": "env_base", "name": "Local",
         "data": {"baseUrl": "http://localhost:5080"}},
        {"_id": "env_emulador", "_type": "environment", "parentId": "env_base", "name": "Emulador Android",
         "data": {"baseUrl": "http://10.0.2.2:5080"}},
        *resources,
    ],
}

out = pathlib.Path(__file__).resolve().parent.parent / "insomnia" / "HealthyMind.insomnia.json"
out.parent.mkdir(exist_ok=True)
out.write_text(json.dumps(export, ensure_ascii=False, indent=2), encoding="utf-8")
print(f"{out} -> {sum(1 for r in resources if r['_type'] == 'request')} peticiones")
