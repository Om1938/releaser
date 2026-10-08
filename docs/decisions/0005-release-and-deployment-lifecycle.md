# ADR 0005 — Release and deployment lifecycles

**Status:** Accepted

## Release
`Available → Deprecated → Withdrawn`. The only allowed moves are `Available → Deprecated`, `Available → Withdrawn` and `Deprecated → Withdrawn`. Withdrawal is one-way.
- Only `Available` releases are offered as update targets.
- `Deprecated` means superseded. It is informational, the release is no longer offered, and its notes stay readable.
- `Withdrawn` means problematic and blocked everywhere, including active deployments.
- Release identity (application, version) and manifests are immutable once registered. Title, channel assignment and notes can change.
- Channel assignment is many-to-many. Promoting a release means assigning it to another channel; nothing is rebuilt.

## Deployment
`Draft → Active ⇄ Paused → Completed | Cancelled`, and `Draft → Cancelled`.
- `Active`: offers its release to in-cohort members of its audience.
- `Paused`: **still claims** its in-cohort audience members but offers nothing. Those members get "no update (deployment paused)" and do **not** fall through to other deployments. Out-of-cohort members are unaffected. This way a pause never changes what unrelated audiences receive.
- `Completed`: requires 100%. It is locked and keeps offering, as the steady state.
- `Cancelled`: terminal. It neither offers nor claims.
- The rollout percentage can change while the deployment is Draft, Active or Paused.
- Every transition bumps the application's configuration version in the same transaction and writes an audit entry.
