# Known limitations (v0.1)

- **Electron only.** The core targeting domain is updater-agnostic, but only the electron-updater adapter exists.
- **Runtime feed URL required.** Apps must call `autoUpdater.setFeedURL()`. The `app-update.yml` baked in at build time can't carry per-installation path segments.
- **Context tokens reach the artifact host.** electron-updater sends `requestHeaders` with downloads too. Use short-lived, `aud`- and `iid`-bound tokens.
- **Architecture targeting.** Windows and macOS updaters request one manifest for all architectures, so audiences can't target x64 vs arm64 there. Linux architectures are distinct.
- **Machine-wide installs.** A user- or customer-targeted decision applies to the installation that asked. Per-machine installs shared by several users get whichever user's token they send.
- **No downgrades or recalls.** Withdrawal stops *new* offers. It can't remove installed versions. Ship a forward fix.
- **Eligibility only.** Rollout percentages describe who is offered an update. There is no adoption or crash telemetry, and no automatic promotion or pause.
- **Manifests are snapshots.** If you overwrite a published `latest*.yml` on your CDN, Releaser keeps serving the registered snapshot. Register a new version instead.
- **Local accounts only.** No SSO or approval workflows yet.
- **Not install-verified.** See [supported-matrix](supported-matrix.md).
