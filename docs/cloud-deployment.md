# Развёртывание FridmakWeb в облаке

Это инструкция для небольшого VPS (2–4 ГБ RAM) и друзей, а не для
high-load production. Стек уже собран в Docker Compose.

## 1. Что нужно

- VPS с Ubuntu 22.04/24.04 и Docker + Docker Compose.
- Домен, например `chat.example.com`, A-запись на IP сервера.
- Открытые порты `80` и `443`; остальные закрыты firewall.
- 2–4 ГБ RAM. Если Grafana/Prometheus не нужны на сервере — их можно
  не поднимать, но для демо они полезны.

## 2. Первый запуск

```bash
git clone <repo-url> fridmakweb
cd fridmakweb
cp .env.example .env
```

В `.env` обязательно поменяй:

```dotenv
POSTGRES_PASSWORD=...
ADMIN_PASSWORD=...
WASMGATE_ADMIN_TOKEN=...
WASMGATE_CP_ADMIN_TOKEN=...
METRICS_TOKEN=...
GRAFANA_ADMIN_PASSWORD=...

# Cloud / HTTPS
AUTH_COOKIE_SECURE=true
WASMGATE_BIND=127.0.0.1
```

`WASMGATE_BIND=127.0.0.1` важен: приложение должно быть доступно только
через TLS-прокси, а не напрямую по `http://IP:8081`.

```bash
docker compose up -d --build
docker compose ps
```

## 3. HTTPS через Caddy

Самый простой вариант — Caddy на хосте. Он сам получит Let's Encrypt
сертификат и будет проксировать запросы в WasmGate.

Установи Caddy и создай `/etc/caddy/Caddyfile` по примеру
[`deploy/caddy/Caddyfile.example`](../deploy/caddy/Caddyfile.example):

```caddyfile
{$APP_DOMAIN} {
    encode gzip
    reverse_proxy 127.0.0.1:8081
    header {
        Strict-Transport-Security "max-age=31536000; includeSubDomains"
        X-Content-Type-Options "nosniff"
        Referrer-Policy "strict-origin-when-cross-origin"
    }
}
```

```bash
sudo APP_DOMAIN=chat.example.com caddy run --config /etc/caddy/Caddyfile
```

После этого приложение доступно по `https://chat.example.com`.

> Важно: WasmGate — L4-прокси. Если Caddy стоит **перед** ним, WasmGate видит
> IP Caddy, а не клиента. Для маленькой группы друзей это нормально, но
> per-IP rate limit на edge становится общим. Варианты:
> - отключить `rate_limit` в `deploy/wasmgate/wasmgate.yaml` и ограничивать
>   запросы на уровне Caddy/облачного балансировщика;
> - либо терминировать TLS на WasmGate с сертификатами и сохранять реальный IP.

## 4. Что не публиковать наружу

В compose уже привязаны к loopback:

- `wasmgate-cp` — `127.0.0.1:19097`;
- `wasmgate` admin — `127.0.0.1:19099`;
- `prometheus` — `127.0.0.1:9091`;
- `grafana` — `127.0.0.1:3000`;
- PostgreSQL — `127.0.0.1:5434`;
- приложение напрямую — `127.0.0.1:8082`.

Для доступа к CP/Grafana используй SSH-туннель:

```bash
ssh -L 19097:127.0.0.1:19097 \
    -L 3000:127.0.0.1:3000 \
    -L 9091:127.0.0.1:9091 \
    user@chat.example.com
```

Потом открывай `http://localhost:19097`, `http://localhost:3000`,
`http://localhost:9091` на своей машине.

## 5. Первый админ

Админ создаётся из `.env` при первом запуске. Если пользователь уже
зарегистрирован, повысь его:

```bash
docker compose exec db psql -U fridmak -d fridmakweb
```

```sql
UPDATE "Users" SET "Role" = 'Admin' WHERE "Username" = 'твой_логин';
```

Затем выйди и войди заново.

## 6. Бэкапы

База:

```bash
docker compose exec -T db pg_dump -U fridmak fridmakweb > fridmakweb-$(date +%F).sql
```

Восстановление:

```bash
cat fridmakweb-2026-11-17.sql | docker compose exec -T db psql -U fridmak -d fridmakweb
```

Также сохраняй volume’ы:

- `fridmakweb_postgres-data` — пользователи, друзья, сообщения;
- `fridmakweb_wasmgate-cp-db-data` — Control Plane;
- `fridmakweb_wasmgate-certs` — сертификаты агентов;
- `fridmakweb_wasmgate-state` — enrollment state edge-ноды.

## 7. Обновление

```bash
git pull
docker compose up -d --build
```

Миграции применяются автоматически при старте приложения
(`Database__MigrateOnStartup=true`).

## 8. Ограничения текущей версии

- Личные сообщения работают через SignalR с fallback-поллингом. Для нескольких
  реплик приложения нужен Redis backplane.
- Нет подтверждения email и восстановления пароля.
- Нет push-уведомлений и мобильного клиента.
- Регистрация открыта. Если нужен закрытый круг — добавь инвайты или
  отключи регистрацию после создания аккаунтов друзей.

## 9. Чек-лист перед публикацией

- [ ] Домен указывает на сервер.
- [ ] `AUTH_COOKIE_SECURE=true`.
- [ ] `WASMGATE_BIND=127.0.0.1`.
- [ ] Порты `19097`, `19099`, `9091`, `3000`, `5434`, `8082` закрыты firewall.
- [ ] Пароли и токены не равны значениям из `.env.example`.
- [ ] Настроен бэкап PostgreSQL.
- [ ] Проверен вход, регистрация, друзья и личные сообщения под HTTPS.
