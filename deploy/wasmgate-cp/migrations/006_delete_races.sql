-- Close two deletion races:
--
-- 1. A deleted agent must not reappear when a reconnect races with deletion.
--    deleted_agents is a tombstone checked by UpsertAsync under an advisory lock.
-- 2. Environment-scoped rows must never reference a missing environment.
--    Foreign keys make a concurrent insert fail instead of creating dangling data.

CREATE TABLE IF NOT EXISTS deleted_agents (
    id          UUID        PRIMARY KEY,
    deleted_at  TIMESTAMPTZ NOT NULL
);

-- Re-runnable: drop and recreate the constraints.
ALTER TABLE agents DROP CONSTRAINT IF EXISTS agents_environment_fk;
ALTER TABLE agents
    ADD CONSTRAINT agents_environment_fk
    FOREIGN KEY (environment) REFERENCES environments (name);

ALTER TABLE enrollment_tokens DROP CONSTRAINT IF EXISTS enrollment_tokens_environment_fk;
ALTER TABLE enrollment_tokens
    ADD CONSTRAINT enrollment_tokens_environment_fk
    FOREIGN KEY (environment) REFERENCES environments (name);

ALTER TABLE config_versions DROP CONSTRAINT IF EXISTS config_versions_environment_fk;
ALTER TABLE config_versions
    ADD CONSTRAINT config_versions_environment_fk
    FOREIGN KEY (environment) REFERENCES environments (name);

ALTER TABLE releases DROP CONSTRAINT IF EXISTS releases_environment_fk;
ALTER TABLE releases
    ADD CONSTRAINT releases_environment_fk
    FOREIGN KEY (environment) REFERENCES environments (name);

ALTER TABLE deployments DROP CONSTRAINT IF EXISTS deployments_environment_fk;
ALTER TABLE deployments
    ADD CONSTRAINT deployments_environment_fk
    FOREIGN KEY (environment) REFERENCES environments (name);
