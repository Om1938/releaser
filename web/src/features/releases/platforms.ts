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
