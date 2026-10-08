# Caching and freshness

PostgreSQL is the only source of truth. Caches only ever shortcut to identical data (ADR [0008](decisions/0008-cache-freshness.md)).

## How it works

- Every application has a `config_version`. Any change that can affect decisions (releases, channels, deployments, audiences, policies, notes, context keys, application settings) increments it **in the same transaction**.
- The **resolution snapshot** is everything needed to decide updates for one application. It is cached in HybridCache under `snapshot:{app}:{config_version}`, in local memory and optionally Redis. The key includes the version, so a cached snapshot can't be stale. A newer version simply has a different key.
- Each node remembers the **current version stamp** for at most `Resolution:FreshnessSeconds` (default 5 s). Concurrent requests share one lookup. The node that made a change drops its stamp immediately.

## Guarantee

> After a pause, withdrawal, rollout change or any other configuration change commits, **no node makes a decision based on the old configuration for longer than `FreshnessSeconds`**, with or without Redis, on one node or many.

Freshness only covers *new decisions*. An updater that already received metadata may still download and install it, and Releaser can't undo installs.

## Failure behaviour

| Failure | Effect |
|---|---|
| Redis down or unreachable | Snapshots are rebuilt from PostgreSQL. Decisions are unchanged. |
| PostgreSQL down, stamp still fresh | Decisions continue from the cached snapshot for the rest of the freshness window. |
| PostgreSQL down, stamp expired | Feed returns **503**. Updaters report an error and don't update. Nothing is offered from stale data. |

These guarantees are covered by `tests/Releaser.Server.Tests/CacheFreshnessTests.cs`: a pause on node A observed by node B within the bound (with and without Redis), fail-closed on database loss, and parity when Redis is unreachable.
