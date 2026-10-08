# ADR 0006 — Update resolution precedence

**Status:** Accepted. See also `docs/precedence-and-rollouts.md`.

Input:
- **Context:** channel, platform target, installation ID, current version, and verified identity (customer, user, groups, attributes).
- **Snapshot:** releases, deployments, audiences, pins and exclusions.

An **audience** matches when at least one *include* rule matches and no *exclude* rule matches. Its specificity for a given installation is the specificity of the most specific matching include rule:
`installation (5) > user (4) > group (3) > customer (2) > attribute/platform/current-version (1) > everyone (0)`.

1. **Unknown channel**: no update.
1. **Unsupported platform** (issue #10): if the application doesn't list the requested platform among its supported platforms, the result is no update (`PlatformNotSupported`). This comes before pins and deployments.
2. **Pin**: if any pin's audience matches, the most specific pin wins. Ties go to the higher `priority`, then the older pin, then the ID. The pinned release becomes the target and **deployments are not considered**. Pins are how a publisher holds or moves a group to an exact version.
3. **Deployment selection**: candidates are deployments on the channel that claim their audience (Active, Paused, Completed), whose audience matches, and whose rollout cohort contains the installation. Order them by audience specificity, then `priority` (higher first), then release version (higher first), then ID. The first one wins.
   - No winner: **no update**.
   - Winner paused: **no update** ("paused"). Out-of-cohort or non-matching installations never reach this deployment, so a pause can't change what other deployments offer. Matched in-cohort installations **do not fall through**.
4. **Target evaluation** (for the pinned or deployed release), in order:
   - Not `Available` or not on the channel: no update.
   - No manifest for the platform: no update.
   - An **exclusion** policy whose audience matches blocks this release: no update.
   - Version ≤ current: no update. **Downgrades are never offered.**
   - Otherwise the release is **offered**.

Every decision carries a trace of human-readable steps. The admin diagnostic endpoint returns it. The client feed never does, so audience membership and policy internals stay private.
