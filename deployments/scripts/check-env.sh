#!/bin/sh
# Validate that .env.production contains every key the production stack needs.
# Prints key names only, never values.
set -eu

ENV_FILE="${1:-.env.production}"

if [ ! -f "$ENV_FILE" ]; then
    echo "Environment file not found: $ENV_FILE" >&2
    exit 1
fi

REQUIRED_KEYS="
ASPNETCORE_ENVIRONMENT
APP_TIMEZONE
Database__AutoMigrate
POSTGRES_USER
POSTGRES_PASSWORD
POSTGRES_APP_PASSWORD
IDENTITY_DB
DIET_DB
PROGRESS_DB
KNOWLEDGE_DB
RABBITMQ_USER
RABBITMQ_PASSWORD
JWT__ISSUER
JWT__AUDIENCE
JWT__KEY_ID
JWT__SIGNING_PRIVATE_KEY
JWT__SIGNING_PUBLIC_KEY
SERVICE__TOKEN
GOOGLE_AI__API_KEY
GOOGLE_AI__CHAT_MODEL
SMTP__HOST
SMTP__PORT
SMTP__USE_SSL
SMTP__USER
SMTP__PASSWORD
SMTP__SENDER_EMAIL
FRONTEND_BASE_URL
GF_SECURITY_ADMIN_USER
GF_SECURITY_ADMIN_PASSWORD
DOMAIN
TLS_DIR
GATEWAY_HTTP_PORT
GATEWAY_HTTPS_PORT
"

missing=""
for key in $REQUIRED_KEYS; do
    if ! grep -Eq "^${key}=.+" "$ENV_FILE"; then
        missing="$missing $key"
    fi
done

if [ -n "$missing" ]; then
    echo "Missing or empty keys in $ENV_FILE:$missing" >&2
    exit 1
fi

echo "Environment file $ENV_FILE contains all required keys."
