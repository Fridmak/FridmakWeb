-- Enrollment tokens and issued agent certificates.
--
-- Apply after 001_init.sql:
--   psql ... -f control-plane/migrations/001_init.sql
--   psql ... -f control-plane/migrations/002_enrollment.sql

CREATE TABLE IF NOT EXISTS enrollment_tokens (
    id              UUID PRIMARY KEY,
    token_hash      TEXT        NOT NULL,
    agent_id        UUID        REFERENCES agents (id) ON DELETE CASCADE,
    name_prefix     TEXT,
    max_uses        INTEGER     NOT NULL CHECK (max_uses > 0),
    used_count      INTEGER     NOT NULL DEFAULT 0 CHECK (used_count >= 0),
    expires_at      TIMESTAMPTZ NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL,
    CONSTRAINT enrollment_tokens_hash_unique UNIQUE (token_hash)
);

CREATE INDEX IF NOT EXISTS enrollment_tokens_expires_idx
    ON enrollment_tokens (expires_at);

CREATE TABLE IF NOT EXISTS agent_certificates (
    agent_id        UUID        NOT NULL REFERENCES agents (id) ON DELETE CASCADE,
    serial_number   TEXT        NOT NULL,
    thumbprint      TEXT        NOT NULL,
    pem             TEXT        NOT NULL,
    not_before      TIMESTAMPTZ NOT NULL,
    not_after       TIMESTAMPTZ NOT NULL,
    issued_at       TIMESTAMPTZ NOT NULL,
    revoked_at      TIMESTAMPTZ,
    PRIMARY KEY (agent_id, serial_number)
);

CREATE INDEX IF NOT EXISTS agent_certificates_thumbprint_idx
    ON agent_certificates (thumbprint);
