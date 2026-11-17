-- Fixes found while reviewing the initial schema.
--
-- Apply after 002_enrollment.sql:
--   psql ... -f control-plane/migrations/003_schema_fixes.sql

-- 1. Configuration versions are scoped by environment. The same YAML may be
--    valid in stage and in prod, so the hash must be unique per environment,
--    not globally: a global constraint rejects the second environment.
ALTER TABLE config_versions
    DROP CONSTRAINT IF EXISTS config_versions_sha256_unique;

ALTER TABLE config_versions
    DROP CONSTRAINT IF EXISTS config_versions_environment_sha256_unique;

ALTER TABLE config_versions
    ADD CONSTRAINT config_versions_environment_sha256_unique
    UNIQUE (environment, sha256);

-- 2. A wave selector is either the bare token '*' or a JSON object. JSONB
--    cannot store the bare '*' token, so the column is plain TEXT and the
--    deployment service parses it. Existing rows are converted: a JSON string
--    "*" becomes the token '*', any other JSON keeps its text form.
ALTER TABLE deployment_waves
    ALTER COLUMN selector TYPE TEXT
    USING (CASE WHEN selector::text = '"*"' THEN '*' ELSE selector::text END);

ALTER TABLE deployment_waves
    ALTER COLUMN selector SET DEFAULT '*';
