# ADR 0007 — Deterministic rollout cohorts

**Status:** Accepted

- `bucket = first 8 bytes of SHA-256(deploymentSalt + ":" + installationId)`, read as an unsigned big-endian integer, modulo 10 000.
- An installation is in the cohort when `bucket < percentage × 100`. Percentages allow two decimals, from 0.00 to 100.00.
- `deploymentSalt` is a random value fixed when the deployment is created. Different deployments therefore sample independent cohorts, and the same early adopters don't land in every rollout.
- Raising the percentage only **adds** installations, and lowering it only removes them, so repeated checks are stable.
- A deployment at 100% includes everyone, including requests with no installation ID.
- **Eligibility ≠ adoption:** the percentage is the share of installations *eligible to be offered* the update. Whether and when they install depends on the updater and the user. The platform does not track adoption.
