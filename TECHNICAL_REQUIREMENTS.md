# Technical Requirements and Selected Stack

**Version:** 0.1  
**Product:** Open-Source Release Management Platform  
**Status:** Selected baseline — no implementation design or code specified

## 1. Architecture principles

- **C# / .NET 10 LTS** is the backend platform.
- Use a **modular monolith**, organized by the established domains and vertical product capabilities.
- Apply **SOLID** and **DRY** pragmatically, prioritizing clear responsibilities and maintainability over unnecessary abstraction.
- Maintain clean boundaries between domain rules, application operations, persistence, administration, and updater-specific compatibility.
- Host the administrative UI and APIs together where practical; avoid microservices for v0.1.
- **PostgreSQL is authoritative** for releases, deployments, policies, and administration records.
- Electron installers and binaries always remain with external publishers' hosting providers.

## 2. Selected stack

| Area | Selection | Requirement |
|---|---|---|
| Language/runtime | **C# / .NET 10 LTS** | Nullable reference types; strict build-time analysis |
| HTTP API | **ASP.NET Core 10 Minimal APIs** | Documented administrative and update-facing interfaces |
| Architecture | **Modular monolith + vertical slices** | Clear domain isolation and explicit boundaries |
| Domain design | **Clean Architecture principles, SOLID, DRY** | Business rules independent of transport and storage concerns |
| Database | **PostgreSQL 18** | Durable source of truth |
| Data access | **Entity Framework Core 10 + Npgsql** | Explicit migrations; no automatic schema synchronization |
| Caching | **Microsoft.Extensions.Caching.Hybrid (HybridCache)** | L1 in-memory cache; optional distributed L2 |
| Distributed cache | **Redis (optional)** | Used only when configured; not mandatory for single-instance deployment |
| Frontend | **React + strict TypeScript + Vite** | Client-side admin dashboard |
| UI components | **shadcn/ui + Radix UI** | Accessible composable UI; use attached shadcn MCP during implementation |
| Styling | **Tailwind CSS v4** | Consistent responsive styling |
| Router | **TanStack Router** | Typed navigation |
| Server state | **TanStack Query** | Query and mutation state |
| Forms | **React Hook Form + Zod** | Form handling and validation |
| API contracts | **OpenAPI, generated TypeScript client** | Consistent API and dashboard contracts |
| Backend validation | **FluentValidation** | Boundary-level input validation |
| Authentication | **ASP.NET Core Identity initially** | Local publisher administrator identities |
| Authorization | **ASP.NET Core authorization policies** | Publisher administration permissions |
| Logging | **Serilog structured logging** | Troubleshooting and auditing support |
| Observability | **OpenTelemetry** | Optional instrumentation/export; no required external collector |
| Backend tests | **xUnit + Testcontainers** | Domain and PostgreSQL integration coverage |
| Frontend tests | **Vitest + React Testing Library** | UI behavior and contracts |
| End-to-end tests | **Playwright** | Admin workflows and updater compatibility scenarios |
| Distribution | **Docker + Docker Compose** | Repeatable self-hosting |
| CI | **GitHub Actions** | Build, test, quality and image publishing |

Version numbers identify agreed technology families; use compatible supported stable patches and lock dependency versions reproducibly.

## 3. Required logical capabilities

- Application and channel registry.
- Release registration using **external feed/manifest URLs** and compatibility metadata.
- Audience targeting for customer/tenant, user, group, installation, and limited attributes.
- Deployment state management and manually controlled rollout percentages.
- Deterministic policy evaluation and documented conflict precedence.
- Update resolution with an explainable eligible release or no-update outcome.
- Electron-specific metadata/feed compatibility integration separate from core policy decisions.
- Markdown release-note publication and retrieval.
- Publisher-only admin dashboard and audit history.

**Artifact handling is reference-only.** Do not build storage abstractions for binaries, artifact upload pipelines, download proxies, or CDN management.

## 4. Caching requirements

- Use HybridCache with local memory caching in the default deployment.
- Allow Redis as an **optional** L2 distributed cache for operators using multiple application instances.
- Prefer caching reusable application configuration, active deployment snapshots, release metadata and policies instead of long-lived final user-specific decisions.
- Maintain deterministic release resolution regardless of whether Redis is enabled.
- Version or invalidate cached policy inputs when deployments, pins, or releases change.
- Define and test a bounded freshness guarantee for pause and withdrawal operations, including multi-instance setups.
- A distributed cache does **not**, by itself, guarantee synchronous invalidation of every node's local cache; the consistency strategy must account for that.
- Cache outages must not silently broaden eligibility or bypass exclusions. PostgreSQL remains the source of truth.
- Avoid Redis-based locking, queues, or other features without a demonstrated requirement.

## 5. Electron integration requirements

- Support a documented compatible subset of **electron-builder** and **electron-updater**, explicitly testing selected versions and platform packaging targets.
- Work with updater-compatible external feeds/manifests and preserve checksum, signature, and URL semantics.
- Handle platform and architecture variations as supported by the selected updater versions.
- Allow the platform to serve lightweight generated update metadata while external hosts serve all binaries.
- Support an enhanced direct integration path when customer/user/group identity context cannot be safely represented through an ordinary update-feed request.
- Treat client-supplied identity attributes as untrusted unless authenticated or attested through a trusted mechanism.
- Avoid promising automatic downgrade behavior or enforcement that the updater cannot provide.
- Validate Electron compatibility early using an end-to-end sample application and external test artifact host.

## 6. Data, safety and security requirements

- Schema and application changes must be migrated explicitly and reversibly where feasible.
- Published release identities and artifact references must have defined immutability guarantees.
- Deployment state transitions and policy activation must be atomic and auditable.
- Administrative roles and integration credentials must be scoped appropriately.
- Update-facing interfaces must not expose confidential audience membership or policy internals.
- Ensure rate limiting, safe error handling, input validation, secrets management and secure HTTP deployment guidance.
- Preserve updater trust checks rather than rebuilding unsigned or untrusted metadata unsafely.
- An unavailable control plane should result in a safe updater failure or no-update behavior, not an uncontrolled global fallback.

## 7. Operational and deployment requirements

**Default self-hosted profile:** two containers — application (API plus static React dashboard) and PostgreSQL; local memory cache.

**Optional scale-out profile:** multiple application instances, PostgreSQL and Redis; distributed consistency behavior must be validated separately.

- Publish reproducible multi-architecture container images where feasible.
- Provide documented Docker Compose deployment, environment configuration, persistence, database backup/restore, and upgrades.
- Expose health/readiness information and structured logs.
- Avoid mandatory brokers, search clusters, Kubernetes, dedicated worker services, or external observability infrastructure.
- Provide sensible defaults for a small publisher while supporting documented scaling later.

## 8. Quality gates and verification

- Automated domain tests for eligibility, precedence, exclusions, deterministic rollout selection and version pinning.
- PostgreSQL integration tests for transactions, migrations, and release/deployment persistence.
- Cache consistency tests, including deployment pause after an entry has been cached.
- Authentication, authorization and credential-scoping tests.
- End-to-end packaged Electron update tests against externally hosted artifacts.
- Frontend accessibility and basic browser workflow tests.
- Static analysis, formatting, dependency scanning, and reproducible builds in CI.
- Document the actual supported updater versions, platforms, runtime prerequisites and operational limits.

## 9. Excluded technology and complexity for v0.1

- No Redis requirement for the default installation.
- No Kafka, RabbitMQ, Temporal, dedicated job orchestrator, or microservices.
- No binary artifact storage, S3 integration, upload processing, or download streaming.
- No custom updater implementation replacing electron-updater.
- No customer-facing tenant dashboard, multi-publisher tenancy, or enterprise approval workflows.
- No automated health-triggered releases or fleet-scale analytics system.

## 10. Technical success criteria

A self-hosted Docker installation can register externally hosted Electron releases, resolve different versions for different authenticated target contexts, support stable staged rollouts and pause semantics, return Electron-compatible metadata without serving installers, and expose administrative workflows through a shadcn-based React dashboard.
