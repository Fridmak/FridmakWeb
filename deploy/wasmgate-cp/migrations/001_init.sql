-- WasmGate control plane: initial PostgreSQL schema.
--
-- PostgreSQL is the only supported store. There is deliberately no SQLite
-- variant: the control plane is a multi-agent service and needs real
-- transactions, JSONB and concurrent writers.

CREATE TABLE IF NOT EXISTS config_versions (
    id              UUID PRIMARY KEY,
    environment     TEXT        NOT NULL,
    sha256          TEXT        NOT NULL,
    yaml            TEXT        NOT NULL,
    message         TEXT,
    created_by      TEXT        NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL,
    CONSTRAINT config_versions_sha256_unique UNIQUE (sha256)
);

CREATE INDEX IF NOT EXISTS config_versions_env_created_idx
    ON config_versions (environment, created_at DESC);

CREATE TABLE IF NOT EXISTS artifacts (
    id              TEXT PRIMARY KEY,
    kind            TEXT        NOT NULL,
    size            BIGINT      NOT NULL CHECK (size >= 0),
    media_type      TEXT,
    signature       TEXT,
    created_by      TEXT,
    created_at      TIMESTAMPTZ NOT NULL
);

CREATE TABLE IF NOT EXISTS releases (
    id                  UUID PRIMARY KEY,
    environment         TEXT        NOT NULL,
    config_version_id   UUID        NOT NULL REFERENCES config_versions (id),
    config_sha256       TEXT        NOT NULL,
    signature           TEXT,
    status              TEXT        NOT NULL,
    created_by          TEXT        NOT NULL,
    created_at          TIMESTAMPTZ NOT NULL
);

CREATE INDEX IF NOT EXISTS releases_env_created_idx
    ON releases (environment, created_at DESC);

CREATE TABLE IF NOT EXISTS release_artifacts (
    release_id      UUID NOT NULL REFERENCES releases (id) ON DELETE CASCADE,
    artifact_id     TEXT NOT NULL REFERENCES artifacts (id),
    name            TEXT NOT NULL,
    PRIMARY KEY (release_id, name)
);

CREATE TABLE IF NOT EXISTS agents (
    id                      UUID PRIMARY KEY,
    name                    TEXT        NOT NULL,
    version                 TEXT        NOT NULL,
    abi_versions            TEXT[]      NOT NULL DEFAULT '{}',
    labels                  JSONB       NOT NULL DEFAULT '{}'::jsonb,
    status                  TEXT        NOT NULL,
    registered_at           TIMESTAMPTZ NOT NULL,
    last_seen_at            TIMESTAMPTZ NOT NULL,
    active_release_id       TEXT,
    active_config_sha256    TEXT,
    CONSTRAINT agents_name_unique UNIQUE (name)
);

CREATE INDEX IF NOT EXISTS agents_status_last_seen_idx
    ON agents (status, last_seen_at DESC);

CREATE TABLE IF NOT EXISTS deployments (
    id              UUID PRIMARY KEY,
    environment     TEXT        NOT NULL,
    release_id      UUID        NOT NULL REFERENCES releases (id),
    status          TEXT        NOT NULL,
    created_by      TEXT        NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL,
    completed_at    TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS deployments_env_status_idx
    ON deployments (environment, status, created_at DESC);

CREATE TABLE IF NOT EXISTS deployment_waves (
    deployment_id   UUID    NOT NULL REFERENCES deployments (id) ON DELETE CASCADE,
    index           INTEGER NOT NULL CHECK (index >= 0),
    selector        JSONB   NOT NULL DEFAULT '{}'::jsonb,
    status          TEXT    NOT NULL,
    started_at      TIMESTAMPTZ,
    completed_at    TIMESTAMPTZ,
    PRIMARY KEY (deployment_id, index)
);

CREATE TABLE IF NOT EXISTS audit_events (
    id              BIGSERIAL PRIMARY KEY,
    at              TIMESTAMPTZ NOT NULL,
    actor           TEXT        NOT NULL,
    action          TEXT        NOT NULL,
    target          TEXT        NOT NULL,
    details         JSONB,
    request_id      TEXT
);

CREATE INDEX IF NOT EXISTS audit_events_at_idx
    ON audit_events (at DESC);
