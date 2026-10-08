// Generates the sample "external CDN" content: placeholder artifacts plus electron-builder style
// update manifests (relative file paths, real sha512/size). Run: node generate.mjs
// The files are NOT real installers; they exist so downloads and checksums can be verified.
import { createHash } from "node:crypto";
import { mkdirSync, writeFileSync, rmSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "public");
const versions = ["1.0.0", "1.1.0", "1.2.0"];
const releaseDate = "2026-09-30T10:00:00.000Z";

const targets = {
  "latest.yml": (v) => [`Sample-App-Setup-${v}.exe`],
  "latest-mac.yml": (v) => [`Sample-App-${v}-arm64-mac.zip`, `Sample-App-${v}-mac.zip`],
  "latest-linux.yml": (v) => [`Sample-App-${v}.AppImage`],
  "latest-linux-arm64.yml": (v) => [`Sample-App-${v}-arm64.AppImage`],
};

rmSync(root, { recursive: true, force: true });
for (const version of versions) {
  const dir = join(root, version);
  mkdirSync(dir, { recursive: true });
  for (const [manifestName, filesFor] of Object.entries(targets)) {
    const files = filesFor(version).map((name) => {
      const bytes = Buffer.from(`Placeholder for ${name}. Not a real installer.\n`);
      writeFileSync(join(dir, name), bytes);
      return { url: name, sha512: createHash("sha512").update(bytes).digest("base64"), size: bytes.length };
    });
    const lines = [`version: ${version}`, "files:"];
    for (const f of files) {
      lines.push(`  - url: ${f.url}`, `    sha512: ${f.sha512}`, `    size: ${f.size}`);
    }
    lines.push(`path: ${files[0].url}`, `sha512: ${files[0].sha512}`, `releaseDate: '${releaseDate}'`, "");
    writeFileSync(join(dir, manifestName), lines.join("\n"));
  }
}
console.log(`Generated sample artifacts in ${root}`);
