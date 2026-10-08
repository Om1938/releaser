# ADR 0013 — Per-application supported platforms

**Status:** Accepted (issue #10)

## Context
Applications ship to different platform sets. Some are Windows and macOS only. The dashboard used to offer all five platform targets everywhere, which invited registering the wrong platforms and gave no way to stop supporting one.

## Decision
- `Application.SupportedPlatforms` is a non-empty, ordered set. It's required when creating an application (nothing pre-selected), editable in Settings, and audited. Changing it bumps the configuration version (ADR 0008).
- It changes only through its own endpoint, `PUT /applications/{id}/supported-platforms` (audit `application.platforms_changed`, details `old -> new`). The general application update doesn't carry it. That way a rename can't re-enable a dropped platform with a stale list, and saving platforms can't overwrite someone else's rename (PR #11 review).
- **Enforced on registration:** registering a release or adding a manifest (ADR 0005) for an unsupported platform returns 409 `release.platform_not_supported`.
- **Enforced on resolution:** a feed request for an unsupported platform gets *no update* (`PlatformNotSupported`) before pins and deployments are considered (ADR 0006). Dropping a platform stops new offers within the freshness bound. Installed versions stay as they are, and releases keep their manifests in case the platform is re-enabled.
- **Migration:** existing applications get the platforms their releases already use. Applications without releases get all platforms.
- The resolution snapshot gained a field, so its cache key prefix moved to `snapshot:v2:`. Entries an older build cached in Redis are never deserialized into the new shape.
