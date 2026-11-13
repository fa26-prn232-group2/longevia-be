#!/bin/sh
# Issue or renew the production certificate with lego DNS-01 (Vercel).
#
#   sh deployments/scripts/issue-certs.sh issue
#   sh deployments/scripts/issue-certs.sh renew
#
# Reads DOMAIN, ACME_EMAIL, DNS_PROVIDER, TLS_DIR from .env.production.
# The Vercel token must be at $SECRET_DIR/dns.token (mode 600).
set -eu

ACTION="${1:-issue}"
ENV_FILE="${ENV_FILE:-.env.production}"
SECRET_DIR="${SECRET_DIR:-/opt/longevia-secrets/production}"
LEGO_IMAGE="${LEGO_IMAGE:-goacme/lego:v5.5.2}"

get_env() {
    if [ -f "$ENV_FILE" ]; then
        grep -E "^$1=" "$ENV_FILE" | head -n1 | cut -d= -f2-
    fi
}

DOMAIN="${DOMAIN:-$(get_env DOMAIN)}"
ACME_EMAIL="${ACME_EMAIL:-$(get_env ACME_EMAIL)}"
DNS_PROVIDER="${DNS_PROVIDER:-$(get_env DNS_PROVIDER)}"
TLS_DIR="${TLS_DIR:-$(get_env TLS_DIR)}"
TLS_DIR="${TLS_DIR:-/opt/longevia-tls}"
DNS_PROVIDER="${DNS_PROVIDER:-vercel}"

[ -n "$DOMAIN" ] || { echo "DOMAIN is required" >&2; exit 1; }
[ -n "$ACME_EMAIL" ] || { echo "ACME_EMAIL is required" >&2; exit 1; }
[ -f "$SECRET_DIR/dns.token" ] || { echo "Missing $SECRET_DIR/dns.token" >&2; exit 1; }

mkdir -p "$TLS_DIR"

TEAM_ARGS=""
if [ -n "${VERCEL_TEAM_ID:-}" ]; then
    TEAM_ARGS="-e VERCEL_TEAM_ID=$VERCEL_TEAM_ID"
fi

run_lego() {
    # Run as the deploy user so the certificate files stay writable on the host.
    docker run --rm \
        --user "$(id -u):$(id -g)" \
        -e HOME=/tmp \
        -v "$TLS_DIR:/lego" \
        -v "$SECRET_DIR:/secrets:ro" \
        -e VERCEL_API_TOKEN_FILE=/secrets/dns.token \
        $TEAM_ARGS \
        "$LEGO_IMAGE" \
        --path /lego \
        --accept-tos \
        --email "$ACME_EMAIL" \
        --dns "$DNS_PROVIDER" \
        "$@"
}

if [ "$ACTION" = "renew" ]; then
    run_lego --domains "$DOMAIN" renew --days 30
else
    run_lego --domains "$DOMAIN" run
fi

mkdir -p "$TLS_DIR/$DOMAIN"
cp "$TLS_DIR/certificates/$DOMAIN.crt" "$TLS_DIR/$DOMAIN/fullchain.pem"
cp "$TLS_DIR/certificates/$DOMAIN.key" "$TLS_DIR/$DOMAIN/privkey.pem"
chmod 644 "$TLS_DIR/$DOMAIN/fullchain.pem"
chmod 600 "$TLS_DIR/$DOMAIN/privkey.pem"

echo "Certificate ready: $TLS_DIR/$DOMAIN"
openssl x509 -enddate -noout -in "$TLS_DIR/$DOMAIN/fullchain.pem"
