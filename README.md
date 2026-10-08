# Releaser

**Build anywhere. Host anywhere. Release selectively.**

Releaser is a small, open-source, self-hosted **release control plane for Electron apps** built with `electron-builder` and updated with `electron-updater`. You keep building, signing and hosting installers exactly as you do today. Releaser decides **which installation is offered which version**, then serves the updater metadata that points at your own CDN.

- Register externally hosted releases by referencing their `latest*.yml` manifests. Releaser never uploads, stores, proxies or serves binaries.
- Target **customers/tenants, users, groups, installations**, platform, current version and custom attributes.
- **Deployments** have deterministic **percentage rollouts** with pause, resume, complete and cancel. **Pins** hold an audience on a version, and **exclusions** keep a bad version away.
- Withdrawals and pauses take effect on every node within a **bounded freshness window** (5 s by default).
- **Release notes** go through draft → published with full revision history, and apps fetch them directly.
- **Explainable decisions**: ask "what would this installation get, and why?" without a device inventory.
- **Audit trail** for every administrative change. Admin roles are Admin, ReleaseManager and Viewer.
- **One instance = one publisher.** Your customers are targeting identities. They never get accounts.

The default installation is two containers, the app and PostgreSQL. Redis is optional and only used to scale out.

## Quick start (demo)

```bash
docker compose --env-file .env.demo -f docker-compose.yml -f docker-compose.demo.yml up -d --build --wait
docker compose --env-file .env.demo -f docker-compose.yml -f docker-compose.demo.yml run --rm demo
```

The demo:
1. Starts Releaser, PostgreSQL and an nginx container acting as your "external CDN".
2. Registers versions 1.0.0, 1.1.0 and 1.2.0.
3. Targets Customer A, Customer B (20% rollout), Internal QA and everyone else.
4. Pauses Customer B, publishes release notes and verifies all of it, printing 16 PASS/FAIL checks.

Open <http://localhost:8080> and sign in as `admin@example.com` / `ChangeMe-Demo-Only-1`.

For a real installation, follow [docs/getting-started.md](docs/getting-started.md).

## Connect an Electron app

```ts
import { autoUpdater } from "electron-updater";

autoUpdater.setFeedURL({
  provider: "generic",
  url: `https://releaser.example.com/u/my-app/${installationId}/${app.getVersion()}/`,
});
await autoUpdater.checkForUpdatesAndNotify();
```

See [docs/integration-electron.md](docs/integration-electron.md) for installation IDs, identity-context tokens (customer, user and group targeting), channels and release notes. The supported updater versions and platforms are in [docs/supported-matrix.md](docs/supported-matrix.md).

## Documentation

| Topic | |
|---|---|
| Install & first release | [getting-started](docs/getting-started.md) |
| Configuration reference | [configuration](docs/configuration.md) |
| Operations: backup, upgrade, scaling, health | [operations](docs/operations.md) |
| Electron integration | [integration-electron](docs/integration-electron.md) |
| How decisions are made | [precedence-and-rollouts](docs/precedence-and-rollouts.md) |
| Caching & pause/withdraw freshness | [caching-and-freshness](docs/caching-and-freshness.md) |
| Security model | [security](docs/security.md) |
| HTTP API & typed client | [api](docs/api.md) |
| Supported versions/platforms | [supported-matrix](docs/supported-matrix.md) |
| Known limitations | [known-limitations](docs/known-limitations.md) |
| Development & contributing | [development](docs/development.md), [CONTRIBUTING](CONTRIBUTING.md), [Code of conduct](CODE_OF_CONDUCT.md) |
| Architecture decisions | [docs/decisions](docs/decisions/README.md) |

## Stack

C#/.NET 10, ASP.NET Core Minimal APIs (modular monolith, vertical slices), EF Core 10 + PostgreSQL 18, HybridCache with optional Redis, ASP.NET Core Identity. The dashboard uses React 19, strict TypeScript, Vite, Tailwind CSS v4, shadcn/ui (Radix), TanStack Router and Query, React Hook Form and Zod, with a client generated from OpenAPI.

## Non-goals

Building, signing, storing or serving installers. CI/CD. Multi-publisher SaaS. Device management. Automatic health-based rollouts or downgrades. Feature flags. Non-Electron updaters, for now.

## License

[Apache-2.0](LICENSE)
