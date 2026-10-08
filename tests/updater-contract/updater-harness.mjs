// Runs electron-updater's real AppUpdater/GenericProvider logic in plain Node (no Electron, no packaging).
// Only the Electron-specific seams are replaced: the app adapter and the HTTP executor.
import { createRequire } from "node:module";
import http from "node:http";
import https from "node:https";
import { mkdtempSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";

const require = createRequire(import.meta.url);
const { AppUpdater } = require("electron-updater/out/AppUpdater");
const { HttpExecutor } = require("builder-util-runtime");

class NodeHttpExecutor extends HttpExecutor {
  createRequest(options, callback) {
    return (options.protocol === "https:" ? https : http).request(options, callback);
  }
}

class HeadlessUpdater extends AppUpdater {
  doDownloadUpdate() {
    throw new Error("Downloads are verified separately by fetching the resolved URLs.");
  }
}

function fakeApp(version) {
  const dir = mkdtempSync(join(tmpdir(), "releaser-contract-"));
  return {
    version,
    name: "sample-app",
    isPackaged: true,
    appUpdateConfigPath: join(dir, "app-update.yml"),
    userDataPath: dir,
    baseCachePath: dir,
    whenReady: () => Promise.resolve(),
    quit() {},
    relaunch() {},
    onQuit() {},
  };
}

const PLATFORMS = {
  Windows: { platform: "win32" },
  MacOS: { platform: "darwin" },
  LinuxX64: { platform: "linux", arch: "x64" },
  LinuxArm64: { platform: "linux", arch: "arm64" },
};

/**
 * Checks for updates exactly as an installed app would, then resolves download URLs with electron-updater's provider.
 * @returns {Promise<{available: boolean, version: string, files: {url: string, sha512: string}[], releaseNotes: unknown}>}
 */
export async function checkForUpdates({ feedUrl, currentVersion, platform, token, channel }) {
  const target = PLATFORMS[platform];
  const previousArch = process.env.TEST_UPDATER_ARCH;
  if (target.arch) process.env.TEST_UPDATER_ARCH = target.arch;
  try {
    const updater = new HeadlessUpdater(null, fakeApp(currentVersion));
    updater.logger = null;
    updater.autoDownload = false;
    updater.httpExecutor = new NodeHttpExecutor();
    updater._testOnlyOptions = { platform: target.platform, isUseDifferentialDownload: false };
    if (channel) updater.channel = channel;
    updater.setFeedURL({ provider: "generic", url: feedUrl });
    if (token) updater.requestHeaders = { authorization: `Bearer ${token}` };

    const result = await updater.checkForUpdates();
    const available = result?.isUpdateAvailable === true;
    const files = available
      ? updater.updateInfoAndProvider.provider.resolveFiles(result.updateInfo).map((f) => ({ url: f.url.href, sha512: f.info.sha512 }))
      : [];
    return { available, version: result?.updateInfo.version, files, releaseNotes: result?.updateInfo.releaseNotes };
  } finally {
    if (previousArch === undefined) delete process.env.TEST_UPDATER_ARCH;
    else process.env.TEST_UPDATER_ARCH = previousArch;
  }
}
