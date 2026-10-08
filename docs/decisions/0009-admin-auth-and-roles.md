# ADR 0009 — Administrator authentication and roles

**Status:** Accepted

- ASP.NET Core Identity with local accounts and an HttpOnly, `SameSite=Strict`, `Secure` (outside Development) cookie.
- Mutating admin requests must carry `X-Releaser-Csrf: 1`. Cross-site forms can't set custom headers, and SameSite=Strict blocks cross-site cookies, which gives defence in depth.
- Roles:
  - `Viewer`: read everything in the admin API.
  - `ReleaseManager`: Viewer plus manage applications, channels, releases, audiences, deployments, policies, notes and keys.
  - `Admin`: ReleaseManager plus manage administrator accounts.
- The first Admin is created at startup from `Bootstrap:AdminEmail` / `Bootstrap:AdminPassword` when no users exist.
- The client endpoints (`/u/...` and `/api/client/...`) form a separate route group with no cookie authentication. Their only trust input is the optional ES256 context token (ADR 0004). They are rate-limited per remote IP.
- Login is rate-limited, and Identity lockout is enabled.
