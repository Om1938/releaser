# Development

## Repository layout

```
src/Releaser.Domain      Pure business rules: value objects, lifecycles, audiences, resolver, cohorts (no I/O)
src/Releaser.Electron    electron-updater adapter: feed file names, manifest parse/validate/rewrite/render
src/Releaser.Server      ASP.NET Core host: Features/<Module>/<Slice>.cs + Infrastructure (EF Core, caching, auth, fetching)
tests/Releaser.*.Tests   xUnit v3: domain, adapter, and HTTP+PostgreSQL integration (Testcontainers)
tests/updater-contract   Real electron-updater code against a running server (Node)
web/                     React dashboard (Vite, TanStack Router/Query, shadcn/ui, Tailwind v4) + Playwright e2e
samples/                 Electron client sample and a sample "external CDN"
scripts/                 Demo seeding/verification and a tiny admin API client
docs/decisions/          Architecture decision records — read before changing an area
```

Architecture: a modular monolith with vertical slices (ADR [0001](decisions/0001-modular-monolith.md)). Business rules live in `Releaser.Domain` and are unit-tested without infrastructure. Slices talk to `ReleaserDbContext` directly. Electron specifics stay in `Releaser.Electron`.

## Prerequisites

.NET SDK 10, Node 24 with pnpm 11 (`corepack enable`), and Docker for Testcontainers and Compose.

## Run locally

```bash
docker run -d --name releaser-dev-pg -e POSTGRES_USER=releaser -e POSTGRES_PASSWORD=releaser -e POSTGRES_DB=releaser -p 55432:5432 postgres:18.6-alpine
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5080 \
  ConnectionStrings__Releaser="Host=localhost;Port=55432;Database=releaser;Username=releaser;Password=releaser" \
  dotnet run --project src/Releaser.Server --no-launch-profile
cd web && pnpm install && pnpm dev    # http://localhost:5173 (proxies /api and /u to :5080)
```

The Development environment bootstraps `admin@example.com` / `ChangeMe-Dev-Only-1`.

## Tests

| Command | What |
|---|---|
| `dotnet test --project tests/Releaser.Domain.Tests` | precedence, rollout stability, lifecycles, pins, exclusions |
| `dotnet test --project tests/Releaser.Electron.Tests` | manifest shapes, URL rewriting, validation |
| `dotnet test --project tests/Releaser.Server.Tests` | API + PostgreSQL 18: acceptance scenario, feed protocol, cache freshness (± Redis), permissions, tokens, migrations (~1 min, needs Docker) |
| `cd web && pnpm test` | Vitest + Testing Library |
| `cd web && pnpm test:e2e` | Playwright admin workflows + axe accessibility (needs the dev server on :5080) |
| `cd tests/updater-contract && npm test` | electron-updater contract (needs the dev server on :5080) |
| `docker compose --env-file .env.demo -f docker-compose.yml -f docker-compose.demo.yml run --rm demo` | full PRD acceptance against the container image |

Wrap long runs in `timeout`. The Testcontainers suites start their own PostgreSQL and Redis.

## Conventions

- **TDD for rules.** A behaviour change in `Releaser.Domain` starts with a failing test.
- **Value objects** for domain concepts (`SemanticVersion`, `ApplicationKey`, `ChannelKey`, `InstallationId`, `RolloutPercentage`, typed IDs).
- **One slice per feature folder.** Each slice holds endpoint mapping, request/response records, a FluentValidator and the handler. No repository or mediator layers.
- **Every mutation** writes an audit entry with `IAuditLog.Record(...)` before `SaveChangesAsync`. Application-scoped entities implement `IApplicationScoped`, so the configuration version bumps automatically in the same transaction.
- **Build hygiene.** Nullable is on, warnings are errors, and analyzers run at `latest-recommended`. The frontend uses strict TypeScript and ESLint.
- **Decisions** go in `docs/decisions` as an ADR when they aren't obvious from code.

## Database migrations

```bash
dotnet ef migrations add <Name> -p src/Releaser.Server -o Infrastructure/Persistence/Migrations
```

Migrations are explicit and committed. `ReleaseManagementTests.migrations_match_the_model` fails if the model and migrations drift.

## API client

After changing endpoints: `dotnet build src/Releaser.Server && (cd web && pnpm api:generate)`. Commit both regenerated files.

## Dependencies

- .NET: Central Package Management (`Directory.Packages.props`) with lock files; restore with `dotnet restore --locked-mode` to verify them (the Docker build does).
- Web: exact versions plus `pnpm-lock.yaml`. `web/pnpm-workspace.yaml` enforces `minimumReleaseAge: 1440`, so packages younger than one day are refused. Pick the previous release rather than relaxing the policy.
