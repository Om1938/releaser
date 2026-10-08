# ADR 0004 — Trusted identity context

**Status:** Accepted

Customer, user and group identity supplied by a client is **untrusted**. Only these are used without verification:
- the installation ID and current version from the feed path,
- the platform/arch, which the file name implies.

## Decision
- Each application can register one or more **ES256 public keys** (PEM). The publisher's own backend signs a short-lived JWT using the matching private key. The platform only ever stores public keys.
- Claims:
  - `sub`: user ID.
  - `tid`: customer/tenant ID.
  - `grp`: array of group keys.
  - `iid` (optional): when present, it must equal the path installation ID.
  - `attrs` (optional): an object of string attributes.
  - `aud`: must equal the application key.
  - `exp`: required; lifetime at most 24 h (configurable).
- The app sends the token as `Authorization: Bearer <jwt>` via `autoUpdater.requestHeaders`.
- An invalid or expired token yields a **context-less** evaluation, which falls back to the default and installation audiences. It never yields an error that might expose policy internals. The diagnostic view records the reason.

## Consequences / known limitations
- electron-updater also sends `requestHeaders` to the artifact host. Keep token lifetimes short. The `aud` claim stops replay against other applications.
- Applications without a publisher backend can still use installation-level and default targeting.
