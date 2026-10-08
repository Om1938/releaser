# Precedence and rollouts

Every update check runs the same deterministic steps (ADR [0006](decisions/0006-resolution-precedence.md)). The *Decision explainer* shows them for any hypothetical installation.

## Audiences and specificity

An audience matches when **any include rule** matches and **no exclude rule** matches. When several things match, the most specific rule wins:

`installation (5) > user (4) > group (3) > customer (2) > attribute / platform / current-version (1) > everyone (0)`

## Steps

1. **Pins.** If a pin's audience matches, the most specific pin decides the target release, and deployments are ignored. Ties go to higher priority, then the older pin.
2. **Deployments.** Otherwise, candidates are deployments on the requested channel that are *Active*, *Paused* or *Completed*, whose audience matches, and whose rollout cohort contains the installation. The winner is chosen by specificity, then `priority` (higher first), then the higher release version.
   - The winner is **paused** → **no update**, and the installation does *not* fall through to a less specific deployment. Pausing never changes what other audiences receive.
3. **The target release must be offerable.** It must be *Available*, still on the channel, have a manifest for the platform, and not be blocked by an **exclusion** whose audience matches.
4. **Never downgrade.** If the target isn't newer than the current version, the answer is **no update**.

## The PRD example

| Audience | Deployment |
|---|---|
| Customer A | 1.1.0, 100% |
| Customer B | 1.2.0, 20% |
| Internal QA (group) | 1.2.0, 100% |
| Everyone | 1.0.0, 100% |

- A Customer A user on 1.0.0 → **1.1.0**.
- A Customer A user who is also in Internal QA → **1.2.0** (group beats customer).
- A Customer B installation inside the 20% cohort → **1.2.0**. Outside the cohort → the *Everyone* deployment (1.0.0), so no update if already on 1.0.0.
- Customer B paused → in-cohort installations get **no update** and don't fall back. Everyone else is unchanged.

## Rollout cohorts

`bucket = SHA-256(deploymentSalt + ":" + installationId) mod 10000`. The installation is in the cohort when `bucket < percentage × 100`.

- **Stable:** the same installation always gets the same answer.
- **Monotonic:** raising 20% → 50% keeps every installation that was already eligible.
- **Independent:** each deployment has its own random salt, so the same early adopters aren't first in every rollout.
- A request without an installation ID only matches 100% deployments.

**Eligibility is not adoption.** 20% means 20% of the audience is *offered* the update. Whether and when they install depends on the user and electron-updater. Releaser collects no install telemetry.

## Lifecycles

- **Release:** `Available → Deprecated → Withdrawn` (withdrawal is permanent). Only *Available* releases are offered. Fix forward by registering a new version.
- **Deployment:** `Draft → Active ⇄ Paused → Completed | Cancelled`. *Completed* requires 100%, stays live, and is locked. *Cancelled* stops claiming its audience.
