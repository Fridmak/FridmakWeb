# syntax=docker/dockerfile:1
#
# Builds the WasmGate control plane from the same pinned revision as the data
# plane and packages the control-plane binary plus the data-plane validator.

FROM alpine:3.22 AS source

ARG WASMGATE_REF=2f18328fbe8bce96d93516e2fdbbb63c25c6c152

RUN apk add --no-cache ca-certificates curl
WORKDIR /src
RUN curl -fsSL "https://github.com/Fridmak/WasmGate/archive/${WASMGATE_REF}.tar.gz" -o /tmp/wasmgate.tar.gz \
    && tar -xzf /tmp/wasmgate.tar.gz --strip-components=1 -C /src \
    && rm /tmp/wasmgate.tar.gz

FROM golang:1.27-alpine AS dataplane

COPY --from=source /src /src
WORKDIR /src
RUN go mod download
RUN CGO_ENABLED=0 go build -trimpath -ldflags="-s -w" -o /out/wasmgate ./cmd/wasmgate

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

RUN apt-get update \
    && apt-get install -y --no-install-recommends clang zlib1g-dev \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /src
COPY --from=source /src/control-plane ./control-plane
RUN dotnet publish control-plane/src/WasmGate.Control.Api \
    -c Release -r linux-x64 --self-contained -o /app

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app ./
COPY --from=dataplane /out/wasmgate /app/wasmgate

RUN mkdir -p /data && chown -R 1654:1654 /data
VOLUME ["/data"]

ENV WASMGATE_CP_GRPC_LISTEN="http://0.0.0.0:9096" \
    WASMGATE_CP_HTTP_LISTEN="http://0.0.0.0:9097" \
    WASMGATE_CP_ARTIFACT_ROOT="/data/artifacts" \
    WASMGATE_CP_AGENT_CA_CERT="/data/ca/agents-ca.crt" \
    WASMGATE_CP_GRPC_CLIENT_CA="/data/ca/agents-ca.crt" \
    WASMGATE_CP_WASMGATE_BIN="/app/wasmgate"

USER 1654
EXPOSE 9096 9097
ENTRYPOINT ["/app/wasmgate-control"]
