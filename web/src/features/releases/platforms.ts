import { z } from "zod";
import type { Schemas } from "@/api/client";

export type Platform = Schemas["PlatformTarget"];

export const allPlatforms = ["Windows", "MacOS", "LinuxX64", "LinuxArm64", "LinuxArmv7l"] as const satisfies readonly Platform[];

/** File names electron-builder publishes on the default "latest" channel. */
export const feedFiles: Record<Platform, string> = {
  Windows: "latest.yml",
  MacOS: "latest-mac.yml",
  LinuxX64: "latest-linux.yml",
  LinuxArm64: "latest-linux-arm64.yml",
  LinuxArmv7l: "latest-linux-armv7l.yml",
};

export const manifestUrlSchema = z.url({ protocol: /^https?$/, error: "Use an absolute http(s) URL." });

/** Standard manifest URL for a platform inside a release folder, e.g. https://cdn/app/1.2.0 + latest-mac.yml. */
export function manifestUrlIn(folderUrl: string, platform: Platform): string {
  return `${folderUrl.replace(/\/+$/, "")}/${feedFiles[platform]}`;
}

/**
 * Suggests where another platform's manifest lives, next to an already registered one. Parses the URL so a
 * query string (e.g. a shared access token) is kept and slashes inside it are not mistaken for path separators.
 */
export function siblingManifestUrl(sourceUrl: string, platform: Platform): string {
  const url = new URL(sourceUrl);
  url.pathname = url.pathname.replace(/[^/]*$/, feedFiles[platform]);
  url.hash = "";
  return url.toString();
}

/** Platforms a release does not ship yet, in the standard order. */
export function missingPlatforms(release: Pick<Schemas["ReleaseResponse"], "platforms">): Platform[] {
  return allPlatforms.filter((platform) => !release.platforms.includes(platform));
}

/**
 * Platforms the application ships, from every release that is not withdrawn. Used as the default selection when
 * registering, so a one-off single-platform hotfix does not narrow the next release.
 */
export function shippedPlatforms(releases: readonly Pick<Schemas["ReleaseResponse"], "platforms" | "state">[]): Platform[] {
  const shipped = new Set(releases.filter((release) => release.state !== "Withdrawn").flatMap((release) => release.platforms));
  return allPlatforms.filter((platform) => shipped.has(platform));
}
