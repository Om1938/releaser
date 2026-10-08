# Operations

## Topology

| Profile | Containers | When |
|---|---|---|
| Default (`docker-compose.yml`) | app, postgres | One node is plenty for most publishers; the feed is a cheap, cached lookup. |
| Scale-out (`+ docker-compose.scale.yml`) | 2× app, Caddy, redis, postgres | High availability or very large fleets. Consistency is validated by the `CacheFreshnessTests` suite and the demo run through the load balancer. |

The application is stateless apart from PostgreSQL. It stores configuration, audit entries, Identity users and ASP.NET Core data-protection keys there. You can add or replace app nodes freely.

## TLS and reverse proxy

Terminate TLS in front of Releaser and forward to port 8080. Set `RELEASER_PUBLIC_BASE_URL` to the public `https://` URL. If the proxy sets `X-Forwarded-For`, also set `RELEASER_TRUST_FORWARDED_HEADERS=true` so rate limiting and logs see client IPs. Admin cookies are always marked `Secure` outside Development, which is why the dashboard requires HTTPS, or `localhost` for local trials.

## Health and logs

- `GET /health/live` reports that the process is up.
- `GET /health/ready` adds a PostgreSQL connectivity check. Docker `HEALTHCHECK` and load balancers use it.
- Logs are structured JSON on stdout (Serilog compact format). Feed decisions are logged at `Debug`. Set `Serilog__MinimumLevel__Override__Releaser.Server=Debug` to see them.
- OpenTelemetry traces and metrics are optional. Set `OTEL_EXPORTER_OTLP_ENDPOINT`.

## Backup and restore

All state lives in PostgreSQL.

```bash
# Backup (consistent, online)
docker compose exec -T postgres pg_dump -U releaser -d releaser --format=custom > releaser-$(date +%F).dump

# Restore into a fresh installation
docker compose up -d postgres
docker compose exec -T postgres pg_restore -U releaser -d releaser --clean --if-exists < releaser-2026-10-08.dump
docker compose up -d app
```

Back up before every upgrade. The audit log, release-note revisions and data-protection keys are all included. Keys stored in the database are not encrypted separately (see [security](security.md)), so protect dumps like the database itself.

## Upgrades

1. Back up the database.
2. Pull or build the new image: `docker compose pull` or `docker compose build`.
3. Run `docker compose up -d`. Pending EF Core migrations are applied at startup under a PostgreSQL advisory lock, so several nodes starting together migrate one at a time.

Prefer explicit migration steps? Set `Database__MigrateOnStartup=false` and run `docker compose run --rm app migrate` before starting the new version. Migrations are additive where feasible. Release notes call out any that are not reversible.

## Scaling out

```bash
docker compose -f docker-compose.yml -f docker-compose.scale.yml up -d --build --wait
```

Every node serves both the dashboard and the feed. Redis is an L2 cache only. If it goes down, nodes fall back to PostgreSQL and decisions don't change. The pause/withdraw freshness bound is the same with or without Redis ([why](caching-and-freshness.md)).

## Capacity notes

Each feed request costs:
- at most one indexed primary-key lookup per application every `FreshnessSeconds`, shared by all concurrent requests,
- a cached snapshot read,
- an in-memory resolution.

The client rate limit (default 600 per minute per IP) protects against abusive clients. Raise it when many installations share an egress IP.
