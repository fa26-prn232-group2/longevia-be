#!/bin/sh
# Restore one service database from a backup. Destructive: --yes is mandatory.
#
#   sh deployments/scripts/restore-db.sh production diet /path/diet.sql.gz --yes
set -eu

ENV_NAME="${1:-production}"
SERVICE="${2:?service is required}"
BACKUP_FILE="${3:?backup file is required}"
shift 3

CONFIRMED=""
for argument in "$@"; do
    [ "$argument" = "--yes" ] && CONFIRMED="--yes"
done
[ "$CONFIRMED" = "--yes" ] || { echo "Refusing to restore without --yes" >&2; exit 1; }
[ -f "$BACKUP_FILE" ] || { echo "Backup not found: $BACKUP_FILE" >&2; exit 1; }

APP_PATH="${APP_PATH:-/opt/longevia-production}"
COMPOSE_FILE="${COMPOSE_FILE:-deployments/docker-compose.production.yml}"
ENV_FILE="${ENV_FILE:-.env.production}"

cd "$APP_PATH"

get_env() {
    grep -E "^$1=" "$ENV_FILE" | head -n1 | cut -d= -f2-
}

POSTGRES_USER_VALUE="$(get_env POSTGRES_USER)"
case "$SERVICE" in
    identity) DB="$(get_env IDENTITY_DB)" ;;
    diet) DB="$(get_env DIET_DB)" ;;
    progress) DB="$(get_env PROGRESS_DB)" ;;
    recommendation) DB="$(get_env KNOWLEDGE_DB)" ;;
    *) echo "Unknown service: $SERVICE" >&2; exit 1 ;;
esac
[ -n "$DB" ] || { echo "Database name for $SERVICE is empty" >&2; exit 1; }

compose() {
    docker compose --env-file "$ENV_FILE" --env-file .env.production.deploy -f "$COMPOSE_FILE" "$@"
}

# Always keep a fresh backup of the current state before overwriting it.
sh deployments/scripts/backup-db.sh "$ENV_NAME"

tmp_file="$(mktemp)"
trap 'rm -f "$tmp_file"' EXIT
gunzip -c "$BACKUP_FILE" > "$tmp_file"

compose exec -T postgres psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER_VALUE" -d "$DB" < "$tmp_file"

TABLES="$(compose exec -T postgres psql -tAc \
    "SELECT count(*) FROM information_schema.tables WHERE table_schema='public'" \
    -U "$POSTGRES_USER_VALUE" -d "$DB" | tr -d '[:space:]')"

echo "Restore complete: service=$SERVICE database=$DB tables=$TABLES"
