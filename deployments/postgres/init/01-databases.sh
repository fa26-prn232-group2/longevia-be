#!/bin/sh
# Runs once, on first initialization of the PostgreSQL data volume.
#
# Creates one database and one login role per owning service, so each service
# can only reach its own database. Recommendation is stateless and has no
# database; it is intentionally absent here.

set -e

create_service_db() {
    db="$1"
    role="$2"

    psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname postgres <<-EOSQL
        CREATE DATABASE "$db";
        CREATE ROLE "$role" LOGIN PASSWORD '$POSTGRES_APP_PASSWORD';
        GRANT ALL PRIVILEGES ON DATABASE "$db" TO "$role";
EOSQL

    psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$db" <<-EOSQL
        GRANT ALL ON SCHEMA public TO "$role";
        ALTER SCHEMA public OWNER TO "$role";
EOSQL
}

create_service_db "${IDENTITY_DB:-identity_db}" identity_user
create_service_db "${DIET_DB:-diet_db}"         diet_user
create_service_db "${PROGRESS_DB:-progress_db}" progress_user
