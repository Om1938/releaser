# Supported updater versions and platforms

| Component | Version tested | Notes |
|---|---|---|
| electron-updater | **6.8.9** (builder-util-runtime 9.7.0) | Generic provider via `setFeedURL` |
| electron-builder | 26.x manifest format (`latest*.yml` with `files[]`, legacy `path`/`sha512`, `packages` for nsis-web) | Version in the sample: 26.15.3 |
| Electron | Any version supported by electron-updater 6.8.x | Sample uses 43.7.8 |

| Platform | Channel file | Packaging targets | Status |
|---|---|---|---|
| Windows x64 / arm64 | `latest.yml` | NSIS, NSIS web (`packages`) | metadata-verified |
| macOS x64 / arm64 / universal | `latest-mac.yml` | zip (updates), dmg listed | metadata-verified |
| Linux x64 | `latest-linux.yml` | AppImage | metadata-verified |
| Linux arm64 | `latest-linux-arm64.yml` | AppImage | metadata-verified |
| Linux armv7l | `latest-linux-armv7l.yml` | AppImage | parsed and served; not in contract tests |

**metadata-verified** means:
1. .NET tests parse and rewrite every manifest shape above.
2. API integration tests (PostgreSQL via Testcontainers) resolve the PRD scenario through the real feed endpoint.
3. `tests/updater-contract` runs electron-updater 6.8.9's own `AppUpdater.checkForUpdates()`, `GenericProvider` URL logic and `resolveFiles()` headlessly against a live Releaser. It simulates win32, darwin, linux x64 and linux arm64, then downloads every resolved file from the external host and checks its sha512.

**Not covered in v0.1:**
- No packaged-installer runs: actually installing NSIS, Squirrel.Mac or AppImage updates on real machines.
- Differential (blockmap) downloads are expected to work, because blockmap URLs derive from the absolute external file URL, but they aren't exercised.
- deb, rpm and pacman updaters, and providers other than `generic`.
