#!/usr/bin/env bash
# Comandos de HealthyMind en el servidor. Todo queda limitado al proyecto "healthymind":
# nunca toca otros contenedores, imágenes, redes ni volúmenes.
#   ./hm.sh up | down | restart | logs | status | backup | load <archivo.tar.gz>
set -euo pipefail
cd /srv/healthymind
DC=(docker compose -p healthymind -f docker-compose.prod.yml --env-file .env)

case "${1:-}" in
  up)      "${DC[@]}" up -d ;;
  down)    "${DC[@]}" down ;;            # sin -v: los datos en /srv se conservan
  restart) "${DC[@]}" restart hm-api ;;
  logs)    "${DC[@]}" logs -f --tail 200 hm-api ;;
  status)  "${DC[@]}" ps; echo; curl -fsS http://localhost:5080/health && echo ;;
  backup)
    mkdir -p backups
    f="backups/healthymind_$(date +%Y%m%d_%H%M).sql.gz"
    docker exec hm-postgres pg_dump -U healthymind healthymind | gzip > "$f"
    find backups -name '*.sql.gz' -mtime +14 -delete    # conserva 14 días
    echo "Copia: $f" ;;
  load)
    gunzip -c "${2:?Indica el archivo .tar.gz}" | docker load ;;
  *) echo "Uso: $0 up|down|restart|logs|status|backup|load <archivo>"; exit 1 ;;
esac
