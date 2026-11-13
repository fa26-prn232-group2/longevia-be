#!/bin/sh
# Back up every service database to $BACKUP_PATH/<env>/<timestamp>/.
#
#   sh deployments/scripts/backup-db.sh production
set -eu

ENV_NAME="${1:-production}"
APP_PATH="${APP_PATH:-/opt/longevia-production}"
COMPOSE_FILE="${COMPOSE_FILE:-deployments/docker-compose.production.yml}"
ENV_FILE="${ENV_FILE:-.env.production}"
KEEP_DAYS="${KEEP_DAYS:-7}"

cd "$APP_PATH"

get_env() {
    grep -E "^$1=" "$ENV_FILE" | head -n1 | cut -d= -f2-
}

POSTGRES_USER_VALUE="$(get_env POSTGRES_USER)"
BACKUP_ROOT="${BACKUP_PATH:-$(get_env BACKUP_PATH)}"
BACKUP_ROOT="${BACKUP_ROOT:-/opt/longevia-backups}"

[ -n "$POSTGRES_USER_VALUE" ] || { echo "POSTGRES_USER is required" >&2; exit 1; }

compose() {
    docker compose --env-file "$ENV_FILE" --env-file .env.production.deploy -f "$COMPOSE_FILE" "$@"
}

STAMP="$(date -u +%Y%m%dT%H%M%SZ)"
DEST="$BACKUP_ROOT/$ENV_NAME/$STAMP"
mkdir -p "$DEST"
umask 077

backup_one() {
    service="$1"
    db="$(get_env "$2")"
    [ -n "$db" ] || { echo "Missing database name for $service" >&2; exit 1; }
    compose exec -T postgres pg_dump -U "$POSTGRES_USER_VALUE" -d "$db" > "$DEST/$service.sql"
    gzip -f "$DEST/$service.sql"
}

backup_one identity IDENTITY_DB
backup_one diet DIET_DB
backup_one progress PROGRESS_DB
backup_one recommendation KNOWLEDGE_DB

(cd "$DEST" && sha256sum ./*.sql.gz > SHA256SUMS)

find "$BACKUP_ROOT/$ENV_NAME" -mindepth 1 -maxdepth 1 -type d -mtime +"$KEEP_DAYS" -exec rm -rf {} +

echo "Backup written to $DEST"
