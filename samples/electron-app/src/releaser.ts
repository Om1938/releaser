import { randomUUID } from "node:crypto";
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { app } from "electron";
import { autoUpdater } from "electron-updater";

/** Where this Releaser instance lives and which application key the app is registered under. */
export interface ReleaserConfig {
  baseUrl: string;
  appKey: string;
  /**
   * Optional: returns a short-lived ES256 identity-context token from YOUR backend (signed with your private key),
   * carrying tid (customer), sub (user), grp (groups) and aud = appKey. Omit for installation/default targeting only.
   */
  getContextToken?: () => Promise<string | undefined>;
}

/** A stable, anonymous id for this installation; stored in userData and never derived from hardware or personal data. */
export function installationId(): string {
  const file = join(app.getPath("userData"), "releaser-installation-id");
  if (existsSync(file)) {
    return readFileSync(file, "utf8").trim();
  }
  const id = randomUUID();
  mkdirSync(dirname(file), { recursive: true });
  writeFileSync(file, id, "utf8");
  return id;
}

/**
 * Points electron-updater's generic provider at Releaser. Installation id and current version travel in the URL path
 * (never as headers to your artifact host). No query string: electron-updater would copy it onto every download URL.
 */
export async function configureUpdates(config: ReleaserConfig): Promise<void> {
  const feedUrl = `${config.baseUrl.replace(/\/$/, "")}/u/${encodeURIComponent(config.appKey)}/${installationId()}/${app.getVersion()}/`;
  autoUpdater.setFeedURL({ provider: "generic", url: feedUrl });
  autoUpdater.allowDowngrade = false;
  const token = await config.getContextToken?.();
  // Note: electron-updater also sends requestHeaders when downloading from your artifact host. Keep tokens short-lived.
  autoUpdater.requestHeaders = token ? { Authorization: `Bearer ${token}` } : null;
}

export interface PublishedReleaseNote {
  version: string;
  title: string;
  summary: string | null;
  bodyMarkdown: string;
  changes: { category: string; text: string }[];
  publishedAt: string | null;
}

/** Published notes newer than the running version, for the app to render itself. */
export async function fetchReleaseNotes(config: ReleaserConfig): Promise<PublishedReleaseNote[]> {
  const url = `${config.baseUrl.replace(/\/$/, "")}/api/client/v1/apps/${encodeURIComponent(config.appKey)}/notes?since=${app.getVersion()}`;
  const response = await fetch(url);
  return response.ok ? ((await response.json()) as PublishedReleaseNote[]) : [];
}
