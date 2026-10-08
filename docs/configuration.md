# Configuration

Releaser uses standard ASP.NET Core configuration. Every key can be set as an environment variable by replacing `:` with `__` (for example `Resolution__FreshnessSeconds=5`). The Docker Compose files map the friendlier `RELEASER_*` variables from `.env` onto these keys.

| Key | Compose variable | Default | Description |
|---|---|---|---|
| `ConnectionStrings:Releaser` | (built from `POSTGRES_*`) | — | Npgsql connection string. **Required.** |
| `Bootstrap:AdminEmail` / `Bootstrap:AdminPassword` | `RELEASER_ADMIN_EMAIL` / `RELEASER_ADMIN_PASSWORD` | — | Creates the first Admin while no administrators exist. Ignored afterwards. Minimum 12 characters. |
| `Platform:PublicBaseUrl` | `RELEASER_PUBLIC_BASE_URL` | `http://localhost:8080` | Shown in the dashboard's integration snippet. |
| `Platform:TrustForwardedHeaders` | `RELEASER_TRUST_FORWARDED_HEADERS` | `false` | Honour `X-Forwarded-For`/`-Proto`. Enable only when your reverse proxy is the only way in. |
| `Resolution:FreshnessSeconds` | `RELEASER_FRESHNESS_SECONDS` | `5` | Upper bound for how long any node may keep using superseded configuration, such as before a pause or withdrawal ([details](caching-and-freshness.md)). 1–300. |
| `Resolution:SnapshotLocalMinutes` | — | `10` | L1 lifetime of immutable snapshots. |
| `Redis:ConnectionString` | (scale profile) | — | Enables Redis as HybridCache L2. Optional. |
| `Manifests:AllowInsecureHttp` | `RELEASER_ALLOW_INSECURE_MANIFESTS` | `false` | Allow `http://` manifest URLs. **Development/demo only.** |
| `Manifests:AllowPrivateNetworks` | `RELEASER_ALLOW_PRIVATE_MANIFEST_HOSTS` | `false` | Allow manifest hosts that resolve to private, loopback or link-local addresses. **Development/demo only.** |
| `Manifests:TimeoutSeconds` | — | `10` | Fetch timeout per manifest. |
| `ContextTokens:MaxLifetimeHours` | — | `24` | Longest accepted identity-token lifetime (`exp - iat`). |
| `ContextTokens:ClockSkewSeconds` | — | `60` | Allowed clock skew for token validation. |
| `RateLimits:ClientPerMinute` | `RELEASER_CLIENT_RATE_LIMIT_PER_MINUTE` | `600` | Feed and notes requests per client IP per minute. Raise this if many installations share a NAT. |
| `RateLimits:LoginPerMinute` | — | `10` | Login attempts per IP per minute. Accounts also lock for 15 minutes after 5 failures. |
| `Database:MigrateOnStartup` | — | `true` | Apply pending migrations at startup, under a PostgreSQL advisory lock. Set `false` to run `dotnet Releaser.Server.dll migrate` explicitly. |
| `Telemetry:OtlpEndpoint` | `OTEL_EXPORTER_OTLP_ENDPOINT` | — | Enables OpenTelemetry traces and metrics export over OTLP. Off by default. |
| `Serilog:*` | — | JSON console | Logging levels and sinks ([Serilog settings](https://github.com/serilog/serilog-settings-configuration)). |

The `Development` environment (`ASPNETCORE_ENVIRONMENT=Development`) relaxes the manifest rules, raises the client rate limit and bootstraps `admin@example.com` / `ChangeMe-Dev-Only-1`. Never run it in production.
