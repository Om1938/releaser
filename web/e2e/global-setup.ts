import { readFile, writeFile } from "node:fs/promises";
import { createServer } from "node:http";
import { dirname, join, normalize } from "node:path";
import { fileURLToPath } from "node:url";
// @ts-expect-error -- plain ESM helper shared with the demo and contract tests
import { ReleaserAdmin, seedPrdScenario } from "../../scripts/lib/releaser-admin.mjs";

const here = dirname(fileURLToPath(import.meta.url));
const publicDir = join(here, "../../samples/artifact-host/public");
export const statePath = join(here, ".state.json");

/** Starts a local "external CDN" and seeds the PRD scenario through the admin API. */
export default async function globalSetup() {
  const server = createServer(async (request, response) => {
    try {
      const path = normalize(decodeURIComponent(new URL(request.url ?? "/", "http://x").pathname)).replace(/^(\.\.[/\\])+/, "");
      response.end(await readFile(join(publicDir, path)));
    } catch {
      response.statusCode = 404;
      response.end();
    }
  });
  await new Promise<void>((resolve) => server.listen(0, "127.0.0.1", resolve));
  const address = server.address();
  const artifactBaseUrl = `http://127.0.0.1:${typeof address === "object" && address ? address.port : 0}`;

  const admin = new ReleaserAdmin(process.env.RELEASER_API ?? "http://localhost:5080");
  await admin.login(process.env.RELEASER_ADMIN_EMAIL ?? "admin@example.com", process.env.RELEASER_ADMIN_PASSWORD ?? "ChangeMe-Dev-Only-1");
  const appKey = `e2e-${Date.now().toString(36)}`;
  const { app } = await seedPrdScenario(admin, { appKey, artifactBaseUrl });
  await writeFile(statePath, JSON.stringify({ appId: app.id, appKey }));
  return async () => {
    server.close();
  };
}
