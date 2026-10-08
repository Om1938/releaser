import { z } from "zod";

const slugPattern = /^[a-z0-9](?:[a-z0-9-]{0,62}[a-z0-9])?$/;
const reservedChannelSuffixes = ["-mac", "-linux", "-linux-arm64", "-linux-armv7l"];

export const slugSchema = z
  .string()
  .trim()
  .regex(slugPattern, "Use 1-64 lowercase letters, digits or dashes, not starting or ending with a dash.");

/** Mirrors the server rule: channel keys must not collide with electron-updater file names. */
export const channelKeySchema = slugSchema
  .refine((key) => key !== "latest", "'latest' is reserved by electron-updater.")
  .refine((key) => !reservedChannelSuffixes.some((suffix) => key.endsWith(suffix)), "Channel keys cannot end with -mac or -linux suffixes.");

export const semverSchema = z
  .string()
  .trim()
  .regex(/^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$/, "Use a semantic version such as 1.2.0.");

/** Splits a textarea of identifiers (newlines or commas) into a clean list. */
export function splitList(value: string): string[] {
  return [...new Set(value.split(/[\n,]/).map((item) => item.trim()).filter(Boolean))];
}
