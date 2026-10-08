# Sample Electron integration

A minimal app showing the **client side** of Releaser. Your build, signing and hosting stay exactly as they are:

1. `electron-builder` builds, signs and writes `latest*.yml` + installers as usual (`build.publish` is a plain `generic`
   provider pointing at **your** CDN folder for that version).
2. You upload those files to your CDN/object storage as you do today.
3. You register the version in Releaser with the URLs of the `latest*.yml` files (dashboard → Releases → Register release).
4. At runtime the app calls `configureUpdates()` (see `src/releaser.ts`), which points electron-updater at Releaser.
   Releaser answers with the right manifest for this installation; downloads still come straight from your CDN.

```ts
await configureUpdates({ baseUrl: "https://releaser.example.com", appKey: "sample-app" });
await autoUpdater.checkForUpdatesAndNotify();
```

Customer/user/group targeting needs a token from your backend — see `docs/integration-electron.md`.

This sample is not built in CI (packaged-installer tests are out of scope for v0.1); compatibility is verified by
`tests/updater-contract`, which runs electron-updater's own update-check code against a live Releaser.
The files in `samples/artifact-host/public` are what such a CDN folder looks like (with placeholder binaries).
