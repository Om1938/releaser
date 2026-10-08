# Security

## Trust boundaries

| Surface | Who | Authentication | Exposes |
|---|---|---|---|
| `/api/admin/v1/*` + dashboard | Publisher staff | ASP.NET Core Identity cookie (HttpOnly, `SameSite=Strict`, `Secure`), roles | Everything about your releases and audiences |
| `/u/{app}/…/*.yml` | Installed apps | none, plus an optional publisher-signed ES256 token | Only the manifest for the decided release, or "no update" |
| `/api/client/v1/apps/{app}/notes` | Installed apps | none | Published notes of non-withdrawn releases |

The update-facing surface never returns audience membership, policy names, deployment names or decision traces. Rejected identity tokens produce an ordinary context-less answer, never an error explaining why.

## Administrators

- Local accounts only (ASP.NET Core Identity). Passwords have at least 12 characters. Accounts lock for 15 minutes after 5 failures, and login is rate-limited per IP.
- Roles:
  - **Viewer** can read everything.
  - **ReleaseManager** can also change applications, releases, audiences, deployments, policies, notes and keys.
  - **Admin** can also manage administrator accounts. At least one Admin always remains.
- CSRF: state-changing requests must carry `X-Releaser-Csrf: 1`, in addition to the `SameSite=Strict` cookie.
- Every consequential change is written to the audit log in the same transaction as the change. Audit details never include audience member lists.
- Cookie-protection keys (ASP.NET Core Data Protection) are stored in PostgreSQL so all nodes share them. They aren't encrypted separately at rest, so treat database backups as secrets.

## Identity-context tokens

- ES256 only. The platform stores **public** keys, and refuses PEMs that contain a private key.
- `aud` must equal the application key. `exp` and `iat` are required, and lifetime is capped (24 h by default). An `iid` claim binds a token to one installation.
- Revoking a key stops trust within the freshness bound.
- Known limitation: electron-updater sends `requestHeaders` to your artifact host too. Use short lifetimes.

## Manifest fetching (SSRF)

Releaser fetches manifests from URLs that administrators enter, so the fetcher is constrained:
- `https://` only (unless `Manifests:AllowInsecureHttp`).
- Each connection resolves DNS and refuses loopback, private, link-local, CGNAT, multicast and unspecified addresses (unless `Manifests:AllowPrivateNetworks`). The check happens at connect time, which defeats DNS rebinding.
- No redirects, a 1 MiB limit and a 10 s timeout. URLs with embedded credentials are rejected.

## Update integrity

Releaser never alters `sha512`, sizes or signature-related data, and rejects manifests whose files lack `sha512`. Binary trust still rests on electron-updater's checksum verification and OS code-signing checks. Serve Releaser over HTTPS: `latest.yml` is unsigned, so TLS protects it in transit.

## Privacy

Releaser stores only what targeting needs:
- installation IDs, user IDs, customer IDs and group keys that *you* put into audience rules,
- public keys.

Feed requests are not persisted individually. Request logs contain the path, which includes the installation ID and version. Configure log retention to suit your policy.

## Reporting vulnerabilities

See [SECURITY.md](../SECURITY.md).
