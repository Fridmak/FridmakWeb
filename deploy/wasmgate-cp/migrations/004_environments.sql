-- Environments become first-class deployment scopes.
--
-- Every agent belongs to exactly one environment, and a deployment targets only
-- agents of its own environment. Environments are created explicitly from the
-- admin UI/API, or implicitly when a configuration, release or agent first uses
-- the name. 'prod' and 'staging' always exist so a fresh installation has a
-- usable picker.
--
-- Existing rows are preserved: agents that predate this migration join 'prod',
-- which keeps single-environment installations working unchanged.

CREATE TABLE IF NOT EXISTS environments (
    name        TEXT        PRIMARY KEY,
    created_by  TEXT        NOT NULL,
    created_at  TIMESTAMPTZ NOT NULL
);

INSERT INTO environments (name, created_by, created_at)
VALUES ('prod', 'system', now()),
       ('staging', 'system', now())
ON CONFLICT (name) DO NOTHING;

ALTER TABLE agents
    ADD COLUMN IF NOT EXISTS environment TEXT NOT NULL DEFAULT 'prod';

CREATE INDEX IF NOT EXISTS agents_environment_idx
    ON agents (environment);

ALTER TABLE enrollment_tokens
    ADD COLUMN IF NOT EXISTS environment TEXT;

-- Adopt names already referenced by existing data, so an upgraded installation
-- sees its real environments instead of only the defaults.
INSERT INTO environments (name, created_by, created_at)
SELECT name, 'system', now()
FROM (
    SELECT environment AS name FROM config_versions
    UNION
    SELECT environment AS name FROM releases
    UNION
    SELECT environment AS name FROM deployments
    UNION
    SELECT environment AS name FROM agents
) AS referenced
ON CONFLICT (name) DO NOTHING;
