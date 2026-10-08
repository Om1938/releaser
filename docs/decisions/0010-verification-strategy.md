# ADR 0010 — Electron compatibility verification strategy

**Status:** Accepted (scope agreed with the maintainer)

v0.1 doesn't build or install packaged Electron apps. Compatibility is verified at the API level:
1. **.NET tests** cover manifest parsing and rewriting for every supported `latest*.yml` shape (Windows NSIS, macOS zip/dmg, Linux AppImage x64/arm64), plus feed resolution through the HTTP API with PostgreSQL in Testcontainers.
2. **Updater contract tests** (`tests/updater-contract`) run electron-updater's own `GenericProvider` URL logic, `parseUpdateInfo`, `resolveFiles` and version comparison against live API responses. They show that the files resolve to the external artifact host with their checksums intact, and that "no update" answers are treated as no update.

The supported matrix in `docs/supported-matrix.md` is therefore marked **metadata-verified**, not install-verified.
