# ADR 0003 — Trust, manifest integrity and SSRF guarding

**Status:** Accepted

- `latest*.yml` is unsigned, and electron-updater trusts it through TLS. Binary trust comes from `sha512` (checked by electron-updater) and from OS code signatures: Windows `publisherName` comes from the app's own embedded `app-update.yml`, and macOS uses Squirrel.Mac code-signature validation. The platform **never generates or alters** checksums or signature-related fields.
- Registration rejects manifests where any file lacks `sha512`, `version` doesn't match the release version, or `files` is empty.
- External manifest URLs must use `https`. `Manifests:AllowInsecureHttp=true` exists for local development and the sample only.
- Fetch errors name the platform. A 403 or 404 hints that object stores such as S3 answer 403 for missing files when listing isn't allowed.
- Fetch guard:
  - Only absolute http(s) URLs are accepted.
  - The target host must not resolve to a loopback, link-local, private or unspecified address unless `Manifests:AllowPrivateNetworks=true`.
  - Responses are capped at 1 MiB with a 10 s timeout, and redirects aren't followed.
- A registered release's manifests are immutable. Missing platforms can be added later (ADR 0005), but correcting a bad manifest means withdrawing the release (or, for registration mistakes, obliterating it — ADR 0014) and registering a new version.
- The platform serves the feed over the operator's HTTPS. Deployment docs require TLS termination in front of it.
