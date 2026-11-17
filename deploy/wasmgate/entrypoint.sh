#!/bin/sh
set -eu

CONFIG="${WASMGATE_CONFIG:-/data/config/wasmgate.yaml}"
STATE_DIR="${WASMGATE_CP_STATE:-/data/agent/state}"
mkdir -p "$STATE_DIR" /data/config

if [ ! -f "$CONFIG" ]; then
    cp /etc/wasmgate/wasmgate.yaml "$CONFIG"
fi

if [ -n "${WASMGATE_CP_URL:-}" ] && [ ! -f "$STATE_DIR/agent.id" ]; then
    ADMIN_URL="${WASMGATE_CP_ADMIN_URL:-http://wasmgate-cp:9097}"
    ADMIN_TOKEN="${WASMGATE_CP_ADMIN_TOKEN:-}"

    if [ -z "$ADMIN_TOKEN" ]; then
        echo "WASMGATE_CP_ADMIN_TOKEN is required for first enrollment" >&2
        exit 1
    fi

    echo "waiting for WasmGate control plane at $ADMIN_URL"
    i=0
    while [ "$i" -lt 60 ]; do
        if curl -fsS "$ADMIN_URL/readyz" >/dev/null 2>&1; then
            break
        fi
        i=$((i + 1))
        sleep 2
    done
    if [ "$i" -ge 60 ]; then
        echo "control plane did not become ready" >&2
        exit 1
    fi

    NAME="${WASMGATE_AGENT_NAME:-edge-01}"
    PREFIX="${WASMGATE_AGENT_PREFIX:-edge-}"
    ENVIRONMENT="${WASMGATE_AGENT_ENV:-prod}"

    RESPONSE=$(curl -fsS -X POST "$ADMIN_URL/api/v1/registration-tokens" \
        -H "Authorization: Bearer $ADMIN_TOKEN" \
        -H "Content-Type: application/json" \
        -d "{\"namePrefix\":\"$PREFIX\",\"maxUses\":10,\"ttlSeconds\":86400,\"environment\":\"$ENVIRONMENT\"}")

    ENROLL_TOKEN=$(printf '%s' "$RESPONSE" | jq -r '.token')
    if [ -z "$ENROLL_TOKEN" ] || [ "$ENROLL_TOKEN" = "null" ]; then
        echo "failed to create registration token: $RESPONSE" >&2
        exit 1
    fi

    exec wasmgate -config "$CONFIG" \
        -cp "$WASMGATE_CP_URL" \
        -cp-server-ca /certs/server.crt \
        -cp-release-key /certs/release-signing.key.pub \
        -cp-state "$STATE_DIR" \
        -cp-name "$NAME" \
        -cp-token "$ENROLL_TOKEN"
fi

if [ -n "${WASMGATE_CP_URL:-}" ]; then
    exec wasmgate -config "$CONFIG" \
        -cp "$WASMGATE_CP_URL" \
        -cp-server-ca /certs/server.crt \
        -cp-release-key /certs/release-signing.key.pub \
        -cp-state "$STATE_DIR" \
        -cp-name "${WASMGATE_AGENT_NAME:-edge-01}"
fi

exec wasmgate -config "$CONFIG"
