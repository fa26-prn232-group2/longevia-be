#!/bin/sh
# Deploy one immutable tag to the production VPS.
#
#   TAG=production-<sha> sh deployments/scripts/deploy-release.sh production
#
# The production env file is managed by the developer on the VPS and is never
# created or overwritten by this script.
set -eu

ENV_NAME="${1:-production}"
APP_PATH="${APP_PATH:-/opt/longevia-production}"
COMPOSE_FILE="${COMPOSE_FILE:-deployments/docker-compose.production.yml}"
ENV_FILE="${ENV_FILE:-.env.production}"
DEPLOY_FILE="${DEPLOY_FILE:-.env.production.deploy}"
IMAGE_PREFIX="${IMAGE_PREFIX:-ghcr.io/fa26-prn232-group2/longevia}"
SERVICES="${SERVICES:-identity diet progress recommendation worker}"
TAG="${TAG:?TAG is required (for example production-<sha>)}"

cd "$APP_PATH"

[ -f "$ENV_FILE" ] || { echo "Missing $APP_PATH/$ENV_FILE" >&2; exit 1; }
sh deployments/scripts/check-env.sh "$ENV_FILE"

get_env() {
    grep -E "^$1=" "$ENV_FILE" | head -n1 | cut -d= -f2-
}

DOMAIN_VALUE="$(get_env DOMAIN)"
TLS_DIR_VALUE="$(get_env TLS_DIR)"
HTTPS_PORT_VALUE="$(get_env GATEWAY_HTTPS_PORT)"
[ -n "$DOMAIN_VALUE" ] && [ -n "$TLS_DIR_VALUE" ] && [ -n "$HTTPS_PORT_VALUE" ] || {
    echo "DOMAIN, TLS_DIR and GATEWAY_HTTPS_PORT are required in $ENV_FILE" >&2
    exit 1
}

if [ ! -f "$TLS_DIR_VALUE/$DOMAIN_VALUE/fullchain.pem" ] || [ ! -f "$TLS_DIR_VALUE/$DOMAIN_VALUE/privkey.pem" ]; then
    echo "TLS certificate missing at $TLS_DIR_VALUE/$DOMAIN_VALUE/. Run issue-certs.sh first." >&2
    exit 1
fi

# Optional: log in to GHCR when the packages are still private.
GHCR_USERNAME_VALUE="$(get_env GHCR_USERNAME)"
GHCR_READ_TOKEN_VALUE="$(get_env GHCR_READ_TOKEN)"
if [ -n "$GHCR_USERNAME_VALUE" ] && [ -n "$GHCR_READ_TOKEN_VALUE" ]; then
    echo "$GHCR_READ_TOKEN_VALUE" | docker login ghcr.io -u "$GHCR_USERNAME_VALUE" --password-stdin >/dev/null
fi

# Resolve immutable digests for this release and pin them in the deploy file.
tmp_deploy="${DEPLOY_FILE}.new"
: > "$tmp_deploy"
for service in $SERVICES; do
    image="$IMAGE_PREFIX-$service:$TAG"
    docker pull "$image" >/dev/null
    digest="$(docker inspect --format '{{index .RepoDigests 0}}' "$image" | sed 's/.*@//')"
    if [ -z "$digest" ] || [ "$digest" = "<no value>" ]; then
        echo "Could not resolve digest for $image" >&2
        exit 1
    fi
    upper="$(echo "$service" | tr '[:lower:]' '[:upper:]')"
    echo "${upper}_IMAGE=$IMAGE_PREFIX-$service@$digest" >> "$tmp_deploy"
done

if [ -f "$DEPLOY_FILE" ]; then
    cp "$DEPLOY_FILE" "${DEPLOY_FILE}.prev"
fi
mv "$tmp_deploy" "$DEPLOY_FILE"
chmod 600 "$DEPLOY_FILE"

compose() {
    docker compose --env-file "$ENV_FILE" --env-file "$DEPLOY_FILE" -f "$COMPOSE_FILE" "$@"
}

compose config --quiet

compose pull postgres rabbitmq redis nginx
compose up -d --wait postgres rabbitmq redis

compose --profile init run --rm db-init

for service in identity diet progress recommendation; do
    compose --profile migrate run --rm "migrate-$service"
done

compose --profile bootstrap run --rm kb-bootstrap

compose up -d --wait --no-deps identity diet progress recommendation worker
compose up -d --wait --no-deps nginx

# Monitoring is opt-in through the `monitoring` profile, so a plain deploy leaves
# Prometheus and Grafana stopped. Start (or keep) them on every release, but never
# gate the release on them: neither service has a healthcheck and their images are
# not digest-pinned in .env.production.deploy.
if [ "${MONITORING_ENABLED:-true}" = "true" ]; then
    compose --profile monitoring up -d prometheus grafana
fi

for path in /health /api/identity/ready /api/diet/ready /api/progress/ready; do
    curl -fsS --resolve "$DOMAIN_VALUE:$HTTPS_PORT_VALUE:127.0.0.1" \
        "https://$DOMAIN_VALUE:$HTTPS_PORT_VALUE$path" >/dev/null
done

cors_headers="$(curl -sS -D - -o /dev/null -X OPTIONS \
    --resolve "$DOMAIN_VALUE:$HTTPS_PORT_VALUE:127.0.0.1" \
    -H "Origin: https://longevia.hycat.online" \
    -H "Access-Control-Request-Method: GET" \
    "https://$DOMAIN_VALUE:$HTTPS_PORT_VALUE/health")"
echo "$cors_headers" | grep -qi '^access-control-allow-origin: https://longevia.hycat.online' || {
    echo "CORS preflight did not return the expected allow-origin header." >&2
    exit 1
}

printf '%s env=%s tag=%s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$ENV_NAME" "$TAG" >> .deploy-history
echo "Deploy $ENV_NAME complete: tag=$TAG"
