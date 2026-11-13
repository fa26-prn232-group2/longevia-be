#!/bin/sh
# Idempotent database/role bootstrap for Longevia production.
# Runs inside a one-shot container with PGHOST/PGUSER/PGPASSWORD set.
set -eu

create_service_db() {
    db="$1"
    role="$2"

    # Create the login role when missing and always align its password with the
    # deployed environment file. psql variables keep the password out of SQL text.
    psql -v ON_ERROR_STOP=1 --dbname postgres \
        -v role="$role" -v pw="$POSTGRES_APP_PASSWORD" <<'SQL'
SELECT format('CREATE ROLE %I LOGIN PASSWORD %L', :'role', :'pw')
WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = :'role') \gexec
SELECT format('ALTER ROLE %I WITH LOGIN PASSWORD %L', :'role', :'pw') \gexec
SQL

    # CREATE DATABASE cannot run inside a transaction; \gexec runs it standalone.
    psql -v ON_ERROR_STOP=1 --dbname postgres -v db="$db" -v role="$role" <<'SQL'
SELECT format('CREATE DATABASE %I OWNER %I', :'db', :'role')
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = :'db') \gexec
SQL

    psql -v ON_ERROR_STOP=1 --dbname "$db" -v role="$role" <<'SQL'
CREATE EXTENSION IF NOT EXISTS vector;
SELECT format('GRANT ALL ON SCHEMA public TO %I', :'role') \gexec
SELECT format('ALTER SCHEMA public OWNER TO %I', :'role') \gexec
SQL
}

create_service_db "${IDENTITY_DB:-identity_db}" identity_user
create_service_db "${DIET_DB:-diet_db}" diet_user
create_service_db "${PROGRESS_DB:-progress_db}" progress_user
create_service_db "${KNOWLEDGE_DB:-recommendation_db}" recommendation_user

echo "Database and role bootstrap completed."
