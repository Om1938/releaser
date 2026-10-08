# ADR 0005 — Release and deployment lifecycles

**Status:** Accepted

## Release
`Available → Deprecated → Withdrawn`. The only allowed moves are `Available → Deprecated`, `Available → Withdrawn` and `Deprecated → Withdrawn`. Withdrawal is one-way.
- Only `Available` releases are offered as update targets.
- `Deprecated` means superseded. It is informational, the release is no longer offered, and its notes stay readable.
- `Withdrawn` means problematic and blocked everywhere, including active deployments.
- Release identity (application, version) and registered manifests are immutable. Title, channel assignment and notes can change. The only way to reuse a version is an admin **obliteration** (ADR 0014), which deletes the release completely.
- **Platforms are append-only** (issue #8). A release can be registered for some platforms and gain others later, e.g. macOS after Windows, through `POST …/releases/{id}/manifests`. Rules:
  - The added manifest must declare the release's version.
  - A platform the release already has can't be replaced, and nothing can be removed.
  - Withdrawn releases can't gain platforms.
  - Adding a platform is audited (`release.manifest_added`) and bumps the configuration version, so it reaches the feed within the freshness bound.
- Channel assignment is many-to-many. Promoting a release means assigning it to another channel; nothing is rebuilt.

## Deployment
`Draft → Active ⇄ Paused → Completed | Cancelled`, and `Draft → Cancelled`.
- `Active`: offers its release to in-cohort members of its audience.
- `Paused`: **still claims** its in-cohort audience members but offers nothing. Those members get "no update (deployment paused)" and do **not** fall through to other deployments. Out-of-cohort members are unaffected. This way a pause never changes what unrelated audiences receive.
- `Completed`: requires 100%. It is locked and keeps offering, as the steady state.
- `Cancelled`: terminal. It neither offers nor claims.
- The rollout percentage can change while the deployment is Draft, Active or Paused.
- Every transition bumps the application's configuration version in the same transaction and writes an audit entry.
