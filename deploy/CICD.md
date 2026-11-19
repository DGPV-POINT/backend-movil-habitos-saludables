# CI/CD con GitHub Actions

Cada push a `main` ejecuta [.github/workflows/deploy.yml](../.github/workflows/deploy.yml):

1. **test** (servidores de GitHub): compila y corre todas las pruebas.
2. **deploy** (runner en 192.168.1.47): construye la imagen, escribe `/srv/healthymind/.env` desde los secrets,
   levanta `docker compose -p healthymind` y comprueba `/health`. Si falla, muestra los logs de la API.

El servidor está en red privada: el runner se conecta **hacia** GitHub, no hay que abrir puertos.

## 1. Secrets y variables (GitHub → repo → Settings → Secrets and variables → Actions)

| Tipo | Nombre | Valor |
|---|---|---|
| Secret | `POSTGRES_PASSWORD` | **la misma** que ya está en `/srv/healthymind/.env` (la base ya se creó con ella) |
| Secret | `JWT_SIGNING_KEY` | la misma del `.env` (si cambia, todos deben volver a iniciar sesión) |
| Secret | `GROQ_API_KEY` | key de Groq |
| Secret | `ADMIN_EMAIL` | correo del admin |
| Secret | `ADMIN_PASSWORD` | dejar **vacío** (el admin ya existe) |
| Variable | `ADMIN_USERNAME` | `admin` |
| Variable | `ALLOW_ON_DEMAND` | `false` (`true` solo el día de la demo) |

Para ver los valores actuales en el servidor: `cat /srv/healthymind/.env`.

Opcional: Settings → Environments → `production` → *Required reviewers* para aprobar cada despliegue a mano.

## 2. Instalar el runner en el servidor (una vez)

GitHub → repo → Settings → Actions → Runners → **New self-hosted runner** → Linux x64. Copiar de ahí la URL de
descarga y el token, y ejecutar como `admin` (todo queda dentro de `/srv/healthymind/runner`):

```bash
mkdir -p /srv/healthymind/runner && cd /srv/healthymind/runner
# curl -o actions-runner.tar.gz -L <URL que muestra GitHub>
tar xzf actions-runner.tar.gz
./config.sh --url https://github.com/DGPV-POINT/backend-movil-habitos-saludables \
            --token <TOKEN que muestra GitHub> --name debdesarrollo --labels healthymind --unattended
sudo ./svc.sh install admin     # servicio systemd: arranca solo al reiniciar el servidor
sudo ./svc.sh start
```

El runner aparece como *Idle* en Settings → Actions → Runners.

## Seguridad

- El runner corre como `admin`, que está en el grupo `docker` (equivale a root en el servidor).
  **Mantener el repositorio privado**: en uno público, cualquiera podría proponer código que se ejecute ahí.
- El workflow solo se dispara con push a `main` (no con pull requests).
- Nunca `prune` global: la limpieza borra solo imágenes `healthymind-api` (deja la actual y la anterior).

## Volver a una versión anterior

```bash
docker image ls healthymind-api          # ver tags disponibles
sed -i 's/^API_TAG=.*/API_TAG=<tag anterior>/' /srv/healthymind/.env && /srv/healthymind/hm.sh up
```

## Quitar todo al terminar el proyecto

```bash
cd /srv/healthymind/runner && sudo ./svc.sh uninstall && ./config.sh remove --token <token de GitHub>
docker compose -p healthymind -f /srv/healthymind/docker-compose.prod.yml down
docker image ls healthymind-api -q | xargs -r docker image rm
sudo rm -rf /srv/healthymind
```
