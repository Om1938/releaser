# Product Requirements Document

**Product:** Open-Source Release Management Platform  
**Version:** 0.1  
**Status:** Approved scope baseline; requirements subject to validation  
**Ownership:** One software publisher per self-hosted instance  
**Initial integration:** electron-builder and electron-updater

## 1. Product vision

A lightweight, open-source, self-hosted control plane for selectively releasing **externally built and hosted installable software**. Publishers continue to build, sign, host, and install software using existing tools. The platform registers release references, defines audiences, resolves update eligibility, and controls rollouts.

**Principle:** Build anywhere. Host anywhere. Release selectively.

## 2. Users and ownership

- **Platform operator:** The software publisher deploying the platform; owns applications, release policies, and administration.
- **Publisher administrators:** Authorized staff managing releases and deployments.
- **Release targets:** The publisher's customers/tenants, users, groups, and installations. They are targeting identities, **not tenants or administrators of this platform**.
- End customers do not need platform accounts, dashboard access, or awareness of the platform.
- A single instance can manage multiple applications, each distributed to many external customers.

## 3. Initial product scope

1. Installable software, starting exclusively with **electron-builder** artifacts and **electron-updater** consumption.
2. Externally managed builds, code signing, artifact storage, and download infrastructure.
3. Register releases by version and externally hosted update manifest/feed references, including supported platform and architecture information.
4. Manage applications, channels, releases, audiences, deployments, policies, release notes, and update decisions.
5. Target customers, users, groups, and individual installations; support platform, architecture, current-version, and limited custom targeting attributes.
6. Manual percentage-based progressive rollout, activation, pause/resume, withdrawal, and basic version pinning.
7. Retrieve release notes through direct application integration; publication is independent of deployment.
8. Provide a publisher-only administrative dashboard and integration interfaces.
9. Self-host with Docker; open-source and usable without vendor services.

## 4. Core domains

| Domain | Responsibility |
|---|---|
| Application | Independent managed application and updater configuration |
| Release | Named application version, state, and external update references |
| Channel | Configurable logical release stream (stable, beta, etc.) |
| Audience | Matching customers, users, groups, and installations |
| Deployment | Makes one release eligible for a defined audience and rollout percentage |
| Policy | Determines eligibility, exclusions, precedence, and version restrictions |
| Release Notes | Draft and published human-readable changes per release |
| Update Decision | Computes whether and which compatible version should be offered, and why |

Artifacts are **metadata/references associated with a release**, not separately hosted or managed product assets. An installation can be identified for targeting without requiring a device-management system. Update decisions need not be permanently stored individually.

## 5. Functional requirements

### 5.1 Applications and releases
- Register multiple applications with stable identifiers and independent release histories.
- Register versioned releases with one or more external updater-compatible feed/manifest URLs and platform/architecture applicability.
- Preserve version history and prevent accidental mutation of published release identities and externally referenced artifact identities.
- Track release states, including available, deprecated, and withdrawn.
- Register a release without deploying it.
- Permit channel assignment and promotion without requiring a rebuild when updater compatibility permits.

### 5.2 Audiences and policies
- Represent application customers/tenants, users, groups, and installations as release targets.
- Support both platform-maintained targeting records and trusted externally supplied identity context.
- Support inclusion, exclusion, grouping, and limited attribute-based matching.
- Resolve overlapping deployments and pins using documented, predictable precedence.
- Use deterministic cohort selection for percentage rollouts so repeated checks do not randomly reassign installations.
- Clearly distinguish percentage **eligibility** from actual update adoption.
- Explain why a given installation is eligible or ineligible for a release.
- A user- or tenant-targeted policy does not guarantee a distinct installed binary when software is shared at machine level; actual installation scope remains updater-specific.

### 5.3 Deployment management
- Create, activate, pause, resume, cancel, and complete deployments with an explicit lifecycle.
- Assign releases to audiences and manually adjust rollout percentages.
- Allow concurrent distinct deployments while resolving conflicts deterministically.
- Support version pinning and exclusion of problematic versions.
- Pausing or withdrawing a release prevents **new eligible update decisions** under the platform's defined freshness guarantees; it cannot undo completed or in-progress installations.
- Favor forward-fix recovery; automatic downgrades are not a v0.1 requirement.

### 5.4 Electron update compatibility
- Work with the existing electron-builder and electron-updater release workflow.
- Accept references to externally hosted update feeds/manifests, rather than demanding uploaded binaries.
- Provide updater-compatible eligibility responses/metadata for supported Electron platforms and packaging targets.
- Preserve artifact URLs, checksums, signatures, metadata and supported updater behavior; correctly handle relative download paths.
- Where per-user/tenant targeting needs trusted context beyond a standard updater request, provide a documented direct-integration mechanism.
- The platform must never silently weaken updater authenticity verification.
- Outbound artifact hosting and client download remain the publisher's responsibility.

### 5.5 Release notes
- Author per-release notes with Markdown, title, summary, and categorized changes.
- Support draft versus published state and independent visibility/publication.
- Expose notes via direct application integration for applications to render themselves.
- Preserve revisions and release-note history at an appropriate administrative level.
- Hosted changelog portals, emails, notifications, translations, and automatic generation are not required in v0.1.

### 5.6 Administration and audit
- Provide an administrative interface for applications, releases, channels, audiences, deployments, notes, and settings.
- Authenticate publisher administrators and enforce permission boundaries.
- Record consequential administration changes including actor, time, entity, and action.
- Offer an update-decision explanation or diagnostic view without requiring a full device inventory.

## 6. User journeys and acceptance criteria

A publisher builds and signs Electron versions **1.0.0, 1.1.0, 1.2.0** and hosts their installers and manifests on an external CDN. The publisher registers external references in the platform, defines Customer A, Customer B, and an internal QA group, and publishes deployments:

| Target | Eligible release | Rollout |
|---|---|---|
| Customer A | 1.1.0 | 100% |
| Customer B | 1.2.0 | 20% |
| Internal QA | 1.2.0 | 100% |
| Default audience | 1.0.0 | 100% |

The platform must demonstrate that:
- Compatible Electron installations receive an appropriate update or no-update result.
- Targeting and override precedence produce expected results for overlapping audiences.
- Customer B's 20% cohort remains stable across repeated checks.
- Pausing Customer B's deployment prevents further offers from that deployment, within the documented consistency boundary, without changing unrelated deployments.
- The publisher can modify rollout assignments without rebuilding or rehosting binaries.
- Applications can fetch published release notes.
- Administrative changes are recorded.
- Clients download binaries directly from their externally configured artifact host.

## 7. Quality requirements

- **Correctness:** Deterministic and explainable decisions; compatibility checks before offering a release.
- **Safety:** Fail safely when update resolution is unavailable; never compromise installed applications.
- **Security:** Separate administrative access from updater consumption; protect targeting information and release metadata.
- **Reliability:** Define clear policy freshness and pause/withdrawal semantics.
- **Operability:** Straightforward Docker deployment, upgrades, backup/restore, logs and health checks.
- **Portability:** No vendor-hosted dependencies or mandatory cloud services.
- **Extensibility:** Core targeting and release domains independent of Electron-specific adapter behavior.
- **Privacy:** Collect only identity attributes essential for targeting.

## 8. Explicit non-goals

- Building, signing, uploading, storing, mirroring, proxying, or serving installers/binaries.
- CI/CD pipeline orchestration or application compilation.
- Replacing electron-updater or installing updates directly.
- Customer/tenant self-service, subscriptions, billing, or a multi-publisher SaaS control plane.
- Device management, remote execution, or built-in crash analytics.
- Automatic health-based rollout promotion or pausing.
- Automated rollback/downgrade orchestration.
- General-purpose feature flagging.
- Native support for non-Electron updater ecosystems in the first version.

Serving lightweight updater-compatible **metadata** is in scope; serving binary artifacts is not.

## 9. Future opportunities (not commitments)

Additional generic updater adapters; richer targeting; scheduled deployments; approval workflows; automated health gates; advanced fleet telemetry; explicit safe downgrades where supported; cumulative changelogs; localization; external announcement integrations; public release-note portals; enterprise SSO.

## 10. Success definition

A publisher can self-host the platform, connect an existing externally hosted Electron release feed, register multiple versions, and manage targeted, deterministic, manually staged updates for distinct customer audiences **without moving binaries or rebuilding the updater**.
