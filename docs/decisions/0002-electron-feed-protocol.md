# ADR 0002 — Electron feed protocol

**Status:** Accepted (validated against electron-updater 6.8.9 / builder-util-runtime 9.7.0 source)

## Facts established from electron-updater source
1. `GenericProvider` requests `<feedUrl>/<channel><suffix>.yml`. The suffix is `""` on Windows, `-mac` on macOS, `-linux` on Linux x64 and `-linux-<arch>` on other Linux architectures. The channel defaults to `latest`.
2. Relative `files[].url`, the legacy top-level `path` and `packages.<arch>.path` are resolved with `new URL(path, feedUrl)`, so they resolve against the **feed URL**, not against any redirect target.
3. `newUrlFromBase` **copies the feed URL's query string onto every resolved file URL**, absolute ones included. A query string on the feed URL would therefore break signed or queried artifact URLs.
4. `isUpdateAvailable` returns `false` when the served `version` equals the running version. That check happens before files are resolved, so a manifest containing only `version` is a valid "no update" answer.
5. Setting `autoUpdater.channel` silently sets `allowDowngrade = true`. Serving a lower version could then **downgrade** a client.
6. `requestHeaders` are also sent when downloading artifacts from the external host.
7. If the manifest contains `stagingPercentage`, electron-updater applies its own client-side random staging on top of whatever we serve.

## Decision
- **Feed URL:** `https://<platform>/u/<appKey>/<installationId>/<currentVersion>/`. The application sets it at runtime with `autoUpdater.setFeedURL({ provider: "generic", url })`. The installation ID and current version travel in the **path**, which means:
  - they never leak to the artifact host (unlike headers),
  - there's no query string to propagate,
  - stock electron-updater keeps working.
- The file name picks the platform target and channel. `latest` maps to the application's default channel, and any other prefix is looked up as a channel key.
- **Snapshotting:** at registration, the platform fetches each external `latest*.yml` once and stores it **immutably**. It records the source URL, raw content and SHA-256. Before storing, it rewrites only *relative* `files[].url`, `path` and `packages.*.path` into absolute URLs against the manifest's own location. Every other field stays as published, including `sha512`, `size` and `blockMapSize`.
- **Served metadata:**
  - The resolved release's stored manifest, without `stagingPercentage`, because the server-side cohort is authoritative.
  - Published release notes are injected as `releaseNotes` when they exist.
- **Never a downgrade:** when no newer eligible release exists, the platform answers with `version: <currentVersion>`, which electron-updater treats as "no update". It never serves a version lower than the client's current version.
- **Fail safe:** if the control plane can't decide (database unavailable and the freshness window expired), it returns `503`. electron-updater reports an error and does not update.
- Responses carry `Cache-Control: no-store`.

## Consequences
- Downloads go straight from the client to the publisher's artifact host. Blockmap URLs derive from the absolute file URL, so differential downloads also hit the external host.
- An app must call `setFeedURL` at runtime. The `publish` config baked into `app-update.yml` is not enough for targeting. See `docs/integration-electron.md`.
