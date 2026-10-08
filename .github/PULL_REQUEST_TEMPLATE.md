## What and why

<!-- What does this change do, and which problem or issue does it address? Link issues with "Closes #123". -->

## How it was verified

<!-- Commands you ran and their results. Delete what does not apply. -->

- [ ] `dotnet build Releaser.slnx` (warnings are errors) and `dotnet format Releaser.slnx --verify-no-changes`
- [ ] `dotnet test --project tests/Releaser.Domain.Tests` / `Releaser.Electron.Tests` / `Releaser.Server.Tests`
- [ ] `cd web && pnpm lint && pnpm typecheck && pnpm test`
- [ ] `cd web && pnpm test:e2e` and/or `cd tests/updater-contract && npm test` (needs a dev server on :5080)
- [ ] Docker demo: `docker compose --env-file .env.demo -f docker-compose.yml -f docker-compose.demo.yml run --rm demo`

## Checklist

- [ ] Domain behaviour changes start with a failing test (TDD), and rules stay in `Releaser.Domain`
- [ ] Every new mutation writes an audit entry; application-scoped entities implement `IApplicationScoped`
- [ ] Endpoint changes: regenerated `web/openapi/openapi.json` and `web/src/api/schema.d.ts`
- [ ] Schema changes: added an EF Core migration (no automatic schema sync)
- [ ] Docs updated; non-obvious decisions recorded as an ADR in `docs/decisions/`
- [ ] Stays within scope: no binary storage/serving, CI/CD orchestration, multi-tenancy or non-Electron updaters ([non-goals](../PRD.md#8-explicit-non-goals))

## Screenshots (UI changes)

<!-- Before/after, desktop and narrow width. -->
