# ADR 0014 — Obliterating releases

**Status:** Accepted (issue #12)

## Context
Release identities are immutable (ADR 0005), which protects installations from a version silently meaning different files. But a registration mistake (wrong folder, wrong manifest, wrong version) used to burn the version number forever.

## Decision
- An explicit **obliterate** operation permanently deletes a release together with its manifest snapshots, its release note and revisions, all its deployments (any state), and the pins and exclusions that reference it. All of this happens in one transaction, which bumps the configuration version (ADR 0008). The version can then be registered again.
- **The audit log is kept.** A `release.obliterated` entry records the version, the state, the platforms, every manifest source URL with its SHA-256, and what was removed.
- **Guard rails:**
  - Only the **Admin** role can obliterate (`CanObliterateReleases`).
  - The request must carry `confirmVersion` equal to the release version, checked in the domain (`Release.ConfirmObliteration`).
  - The dashboard first shows an impact preview (`GET …/obliteration-impact`) that highlights live deployments, and the button only works once the exact version is typed.
- `DELETE /api/admin/v1/applications/{appId}/releases/{releaseId}?confirmVersion=…` returns 204.
- If something starts referencing the release while it's being deleted (a race), the foreign-key violation is returned as 409 `still_referenced`, never 500.

## Consequences
- Installations that already received the version keep it. If the version is re-registered with different files, they won't be offered it, because electron-updater sees the same version. The dialog and docs say so, and fixing forward with a new version is still preferred once anything has shipped.
- Obliteration is allowed in every state. A withdrawn or fully completed release can be removed too: the operator sees exactly what is affected before confirming.
