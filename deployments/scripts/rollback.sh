#!/bin/sh
# Roll the stack back to the previous pinned digests.
#
#   sh deployments/scripts/rollback.sh production
set -eu

ENV_NAME="${1:-production}"
APP_PATH="${APP_PATH:-/opt/longevia-production}"
COMPOSE_FILE="${COMPOSE_FILE:-deployments/docker-compose.production.yml}"
ENV_FILE="${ENV_FILE:-.env.production}"
DEPLOY_FILE="${DEPLOY_FILE:-.env.production.deploy}"

cd "$APP_PATH"

[ -f "${DEPLOY_FILE}.prev" ] || { echo "No previous deploy file: ${DEPLOY_FILE}.prev" >&2; exit 1; }

cp "$DEPLOY_FILE" "${DEPLOY_FILE}.failed"
cp "${DEPLOY_FILE}.prev" "$DEPLOY_FILE"
chmod 600 "$DEPLOY_FILE"

compose() {
    docker compose --env-file "$ENV_FILE" --env-file "$DEPLOY_FILE" -f "$COMPOSE_FILE" "$@"
}

compose config --quiet
compose up -d --wait --no-deps identity diet progress recommendation worker
compose up -d --wait --no-deps nginx

DOMAIN="$(grep -E '^DOMAIN=' "$ENV_FILE" | head -n1 | cut -d= -f2-)"
HTTPS_PORT="$(grep -E '^GATEWAY_HTTPS_PORT=' "$ENV_FILE" | head -n1 | cut -d= -f2-)"
curl -fsS --resolve "$DOMAIN:$HTTPS_PORT:127.0.0.1" "https://$DOMAIN:$HTTPS_PORT/health" >/dev/null

echo "Rollback complete for $ENV_NAME"
