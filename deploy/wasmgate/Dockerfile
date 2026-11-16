# syntax=docker/dockerfile:1
#
# WasmGate data plane from the upstream metrics branch.
# The default revision is pinned; override with:
#   WASMGATE_REF=metrics docker compose build wasmgate
FROM golang:1.27-alpine AS build

ARG WASMGATE_REF=8aa95f56f9a3a2c67cf079f23abcc5ed695d3238
ARG VERSION=metrics

RUN apk add --no-cache ca-certificates curl

WORKDIR /src
RUN curl -fsSL "https://github.com/Fridmak/WasmGate/archive/${WASMGATE_REF}.tar.gz" -o /tmp/wasmgate.tar.gz \
    && tar -xzf /tmp/wasmgate.tar.gz --strip-components=1 -C /src \
    && rm /tmp/wasmgate.tar.gz

RUN go mod download
RUN CGO_ENABLED=0 go build -trimpath \
    -ldflags="-s -w -X main.version=${VERSION}" \
    -o /out/wasmgate ./cmd/wasmgate

FROM alpine:3.22

RUN apk add --no-cache ca-certificates curl \
    && adduser -D -u 10001 wasmgate

COPY --from=build /out/wasmgate /usr/local/bin/wasmgate
COPY deploy/wasmgate/wasmgate.yaml /etc/wasmgate/wasmgate.yaml

USER wasmgate
EXPOSE 8080 9099
ENTRYPOINT ["/usr/local/bin/wasmgate", "-config", "/etc/wasmgate/wasmgate.yaml"]
