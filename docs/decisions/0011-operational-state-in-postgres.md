# ADR 0011 — Operational state in PostgreSQL

**Status:** Accepted (found while validating the Compose scale-out profile)

## Decisions
- **Data Protection keys** (which protect admin cookies) are persisted in PostgreSQL via `PersistKeysToDbContext`. Without this, every restart signs all administrators out, and in the scale-out profile a cookie issued by one node is rejected by the other. PostgreSQL is already required, so this adds no infrastructure.
- **Startup migrations** run under a PostgreSQL session advisory lock (`pg_advisory_lock`). Nodes starting together otherwise race on `CREATE TABLE`.
- **Synchronous `SaveChanges`** is allowed only when no application-scoped entity changed. The Data Protection EF repository saves synchronously. Configuration-version bumps (ADR 0008) still require `SaveChangesAsync`.
- The Alpine runtime image includes `krb5-libs`. Npgsql probes GSSAPI during connection setup, and the process crashed without it.

## Consequences
- Keys are stored unencrypted in the database (the "No XML encryptor configured" warning). Database backups must be treated as secrets (docs/security.md).
