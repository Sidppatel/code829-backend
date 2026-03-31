-- Install pgcrypto extension at the database level (not via application migrations).
-- This runs once when the container is first initialized with an empty data volume.
-- PostgreSQL init scripts in /docker-entrypoint-initdb.d/ execute in alphabetical order.

CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- Least-privilege roles for production readiness.
-- ep_dev owns the schema (used for migrations).
-- ep_app is the runtime application role (read/write, no DDL).
-- ep_readonly is for analytics/reporting queries.

-- Application runtime role
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'ep_app') THEN
        CREATE ROLE ep_app LOGIN PASSWORD 'ep_app_password';
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
        CREATE ROLE ep_readonly LOGIN PASSWORD 'ep_readonly_password';
    END IF;
END
$$;

GRANT CONNECT ON DATABASE event_platform TO ep_readonly;
GRANT USAGE ON SCHEMA public TO ep_readonly;
ALTER DEFAULT PRIVILEGES FOR ROLE ep_dev IN SCHEMA public
    GRANT SELECT ON TABLES TO ep_readonly;
