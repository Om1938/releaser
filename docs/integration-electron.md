# Electron integration

Releaser works with the stock `electron-updater` **generic provider**. Your build output and hosting don't change. The app tells electron-updater to ask Releaser instead of your CDN for the channel file (`latest*.yml`). Releaser answers with the manifest for the release this installation should get, and **every download URL in it points at your CDN**.

```
App ──GET /u/acme/<installationId>/1.0.0/latest-mac.yml──▶ Releaser   (decides, serves YAML only)
App ──GET https://cdn.example.com/acme/1.1.0/Acme-1.1.0-mac.zip──▶ your CDN   (binary, sha512-verified)
```

## 1. Feed URL

```ts
import { app } from "electron";
import { autoUpdater } from "electron-updater";

autoUpdater.setFeedURL({
  provider: "generic",
  url: `https://releaser.example.com/u/${appKey}/${installationId}/${app.getVersion()}/`,
});
autoUpdater.allowDowngrade = false;
await autoUpdater.checkForUpdatesAndNotify();
```

- **`installationId`** is an opaque, stable ID you generate once per installation, e.g. a UUID stored in `app.getPath("userData")`. It must be 8–128 characters from `A–Z a–z 0–9 . _ -`. Partial rollouts and installation-level targeting use it. Don't derive it from hardware or personal data.
- **Current version** must be `app.getVersion()`. Releaser uses it to avoid ever offering a downgrade. When nothing newer is eligible, it replies with a manifest announcing your current version, which electron-updater treats as "no update".
- **No query string.** electron-updater copies the feed URL's query string onto every download URL, which would break signed CDN URLs. Releaser's design keeps everything in the path.
- The `publish` configuration baked into `app-update.yml` by electron-builder is overridden by `setFeedURL`. Keep it pointing at your CDN anyway: it documents where electron-builder's artifacts live.

A complete helper is in [`samples/electron-app/src/releaser.ts`](../samples/electron-app/src/releaser.ts).

## 2. Channels

electron-updater requests `<channel><suffix>.yml`. The suffix depends on the platform: `""` on Windows, `-mac`, `-linux` and `-linux-arm64`. Releaser maps the channel part as follows:

| Requested | Releaser channel |
|---|---|
| `latest*.yml` (default) | the application's **default channel** (e.g. `stable`) |
| `beta*.yml` (`autoUpdater.channel = "beta"`) | channel `beta` |

> Setting `autoUpdater.channel` makes electron-updater set `allowDowngrade = true`. Releaser never serves a lower version, but set `allowDowngrade = false` again after changing the channel anyway.

## 3. Customer, user and group targeting (identity-context tokens)

The feed request carries no trustworthy identity, and anything the client claims could be forged. Customer, user, group and attribute rules therefore only match claims from a token **signed by your backend**:

1. Generate an ES256 (P-256) key pair. Register the **public** key under *Application → Settings → Identity context keys*.
2. Your backend issues a short-lived JWT for the signed-in user:

   | Claim | Meaning |
   |---|---|
   | `aud` | **required**, the application key |
   | `exp`, `iat` | **required**; lifetime ≤ `ContextTokens:MaxLifetimeHours` (24 h) |
   | `tid` | customer/tenant ID |
   | `sub` | user ID |
   | `grp` | array of group keys |
   | `iid` | optional; binds the token to one installation ID |
   | `attrs` | optional object of string attributes |

3. The app sends it with `autoUpdater.requestHeaders = { Authorization: "Bearer <token>" }`.

```js
// Node.js backend example — sign with your private key; never ship it in the app.
import { sign } from "node:crypto";
const b64 = (v) => Buffer.from(JSON.stringify(v)).toString("base64url");
export function updateContextToken({ appKey, customerId, userId, groups }, privateKey) {
  const now = Math.floor(Date.now() / 1000);
  const unsigned = `${b64({ alg: "ES256", typ: "JWT" })}.${b64({ aud: appKey, tid: customerId, sub: userId, grp: groups, iat: now, exp: now + 3600 })}`;
  return `${unsigned}.${sign("sha256", Buffer.from(unsigned), { key: privateKey, dsaEncoding: "ieee-p1363" }).toString("base64url")}`;
}
```

Requests with a missing, invalid, expired or wrongly scoped token are **not rejected**. They are evaluated without identity, so only installation, platform, version and *Everyone* rules apply. The client never learns why. Use the *Decision explainer* to debug.

> electron-updater also sends `requestHeaders` when it downloads artifacts, so your CDN will see the token. Keep lifetimes short, bind tokens with `iid` where you can, and remember that `aud` prevents replay against other applications.

## 4. Release notes

Published notes are available without authentication:

```
GET /api/client/v1/apps/{appKey}/notes?since=1.0.0     # releases newer than the installed one
GET /api/client/v1/apps/{appKey}/notes?version=1.1.0   # one release
```

Each item has `version`, `title`, `summary`, `bodyMarkdown`, `changes[] {category, text}` and `publishedAt`. Published notes also appear as `releaseNotes` (Markdown) in the update info electron-updater emits.

## 5. Failure behaviour

| Situation | Feed response | electron-updater |
|---|---|---|
| Nothing eligible / up to date / paused deployment / unknown channel | 200, `version: <current>` | `update-not-available` |
| Unknown application, or a channel name that isn't a valid key | 404 | `error` event, no update |
| Malformed path (bad version or installation ID) | 400 | `error` event, no update |
| Database unreachable after the freshness bound | 503 | `error` event, no update |
| Rate limited | 429 | `error` event, retry at the next check |

In every case the installed app keeps running its current version. Releaser can't uninstall or downgrade anything.

## 6. What Releaser preserves

At registration Releaser fetches each manifest once and stores an immutable snapshot. Only *relative* `files[].url`, `path` and `packages.*.path` values are resolved, against the manifest's own URL, exactly as electron-updater would. `sha512`, `size`, `blockMapSize`, `releaseDate` and unknown fields are kept byte-for-byte. Absolute URLs are untouched. `stagingPercentage` is dropped from served metadata because Releaser's server-side rollout replaces it. Code-signature verification (Windows `publisherName`, macOS Squirrel) is unchanged.
