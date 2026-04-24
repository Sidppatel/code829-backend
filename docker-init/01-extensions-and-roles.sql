-- Install pgcrypto extension at the database level (not via application migrations).
-- This runs once when the container is first initialized with an empty data volume.
-- PostgreSQL init scripts in /docker-entrypoint-initdb.d/ execute in alphabetical order.

CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- Least-privilege roles for production readiness.
-- ep_dev owns the schema (used for migrations).
-- ep_app is the runtime application role (read/write, no DDL).
-- ep_readonly is for analytics/reporting queries.
--
-- Passwords are sourced from environment variables EP_APP_PASSWORD and
-- EP_READONLY_PASSWORD (passed to the Postgres container via docker-compose).
-- psql 16+ meta-commands \getenv + %L-quoted dynamic SQL inject the values
-- safely without hardcoding secrets in the image.

\getenv ep_app_password EP_APP_PASSWORD
\getenv ep_readonly_password EP_READONLY_PASSWORD

-- Fail fast if the env vars are missing — a silently-created role with an
-- empty password is a worse outcome than a broken boot.
SELECT CASE
    WHEN :'ep_app_password' = '' THEN
        (SELECT 1 / 0) -- forces a divide-by-zero to abort init
    ELSE 1
END;
SELECT CASE
    WHEN :'ep_readonly_password' = '' THEN
        (SELECT 1 / 0)
    ELSE 1
END;

-- Application runtime role
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'ep_app') THEN
        EXECUTE format('CREATE ROLE ep_app LOGIN PASSWORD %L', :'ep_app_password');
    END IF;
END
$$;

GRANT CONNECT ON DATABASE event_platform TO ep_app;
GRANT USAGE ON SCHEMA public TO ep_app;
ALTER DEFAULT PRIVILEGES FOR ROLE ep_dev IN SCHEMA public
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO ep_app;
ALTER DEFAULT PRIVILEGES FOR ROLE ep_dev IN SCHEMA public
    GRANT USAGE, SELECT ON SEQUENCES TO ep_app;

-- Read-only reporting role
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'ep_readonly') THEN
        EXECUTE format('CREATE ROLE ep_readonly LOGIN PASSWORD %L', :'ep_readonly_password');
    END IF;
END
$$;

GRANT CONNECT ON DATABASE event_platform TO ep_readonly;
GRANT USAGE ON SCHEMA public TO ep_readonly;
ALTER DEFAULT PRIVILEGES FOR ROLE ep_dev IN SCHEMA public
    GRANT SELECT ON TABLES TO ep_readonly;
