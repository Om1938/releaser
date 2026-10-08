# ADR 0001 — Modular monolith with vertical slices

**Status:** Accepted

## Decision
The backend is one deployable ASP.NET Core process, split into three projects:

- `Releaser.Domain`: pure business rules (value objects, lifecycles, audience matching, policies, the update resolver and cohort selection). It has no references to ASP.NET, EF Core or Electron.
- `Releaser.Electron`: the electron-updater adapter. It parses, validates and rewrites manifests and renders feed metadata. It depends only on `Releaser.Domain`.
- `Releaser.Server`: the host. `Features/<Module>/` holds one file per vertical slice (endpoint mapping, request/response records, validator, handler). `Infrastructure/` holds persistence, caching, manifest fetching and authentication.

Slice handlers use `ReleaserDbContext` directly. We don't add repository or mediator layers because EF Core already provides the unit of work, and an extra layer would be speculative generality.

## Consequences
- The domain rules are unit-tested without a database.
- Electron specifics stay out of the core targeting domain (PRD §7 Extensibility). A future updater ecosystem would be a sibling adapter project.
