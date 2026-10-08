# ADR 0008 — Caching and bounded freshness

**Status:** Accepted

- PostgreSQL is the source of truth. Each application row has a `ConfigVersion` (bigint). Any change to the application's releases, channels, deployments, audiences, policies, notes or keys increments it **in the same transaction**.
- **Resolution snapshot:** the immutable resolution input for one application at one `ConfigVersion`. It is cached in `HybridCache` under the key `snapshot:{appId}:{configVersion}`.
  - L1 is in-memory. L2 is Redis, used only when `Redis:ConnectionString` is set.
  - The key includes the version, so a cached entry can never be stale. Redis is only ever a shortcut to identical data.
- **Version stamp:** each node keeps the current `ConfigVersion` per application in local memory for at most `Resolution:FreshnessSeconds` (default **5 s**). The node that performs a write evicts its own entry immediately.
- **Guarantee:** a pause, withdrawal or any other change stops affecting decisions on **every node within `FreshnessSeconds`** after commit, with or without Redis. No pub/sub is needed.
- **Failure behaviour:**
  - Redis unavailable: HybridCache falls back to building the snapshot from PostgreSQL.
  - PostgreSQL unavailable once the stamp has expired: the feed returns `503`. It never serves a snapshot older than the freshness bound, so a cache outage can't broaden eligibility.
- Final per-user decisions are not cached.
