-- Human-readable deployment outcome.
--
-- Before this migration a failed deployment only had a status and wave states,
-- so the admin UI could not explain why nothing happened. The control plane now
-- records the reason: no agents in the environment, selectors matched nobody,
-- no live session, an agent-reported apply error, or a reaper timeout.

ALTER TABLE deployments
    ADD COLUMN IF NOT EXISTS message TEXT;
