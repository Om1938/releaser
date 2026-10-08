// Contract: Releaser feed responses are consumed correctly by the real electron-updater 6.8.9 code.
// Requires a running Releaser (RELEASER_URL) that may fetch from the artifact host (Development settings allow http/localhost).
import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { createServer } from "node:http";
import { readFile } from "node:fs/promises";
import { join, dirname, normalize } from "node:path";
import { fileURLToPath } from "node:url";
import { ReleaserAdmin, seedPrdScenario, createContextSigner } from "../../scripts/lib/releaser-admin.mjs";
import { checkForUpdates } from "./updater-harness.mjs";

const RELEASER_URL = (process.env.RELEASER_URL ?? "http://localhost:5080").replace(/\/$/, "");
const ADMIN_EMAIL = process.env.RELEASER_ADMIN_EMAIL ?? "admin@example.com";
const ADMIN_PASSWORD = process.env.RELEASER_ADMIN_PASSWORD ?? "ChangeMe-Dev-Only-1";
const FRESHNESS_MS = Number(process.env.RELEASER_FRESHNESS_SECONDS ?? 5) * 1000 + 500;
const publicDir = join(dirname(fileURLToPath(import.meta.url)), "../../samples/artifact-host/public");

let artifactServer;
let artifactBaseUrl = process.env.ARTIFACT_BASE_URL;
let scenario;
const appKey = `contract-${Date.now().toString(36)}`;
const signer = createContextSigner();
const tokenFor = (claims) => signer.sign({ aud: appKey, ...claims });
const feedUrl = (installation, version) => `${RELEASER_URL}/u/${appKey}/${installation}/${version}/`;

before(async () => {
  if (!artifactBaseUrl) {
    artifactServer = createServer(async (request, response) => {
      try {
        const path = normalize(decodeURIComponent(new URL(request.url, "http://x").pathname)).replace(/^(\.\.[/\\])+/, "");
        response.end(await readFile(join(publicDir, path)));
      } catch {
        response.statusCode = 404;
        response.end();
      }
    });
    await new Promise((resolve) => artifactServer.listen(0, "127.0.0.1", resolve));
    artifactBaseUrl = `http://127.0.0.1:${artifactServer.address().port}`;
  }
  const admin = new ReleaserAdmin(RELEASER_URL);
  await admin.login(ADMIN_EMAIL, ADMIN_PASSWORD);
  scenario = { admin, ...(await seedPrdScenario(admin, { appKey, artifactBaseUrl, publicKeyPem: signer.publicKeyPem })) };
});

after(() => artifactServer?.close());

async function assertDownloadsVerify(files) {
  assert.ok(files.length > 0, "an offered update lists files");
  for (const file of files) {
    assert.ok(file.url.startsWith(artifactBaseUrl + "/"), `download ${file.url} must come from the external artifact host`);
    assert.ok(!file.url.startsWith(RELEASER_URL), "the platform must never serve binaries");
    const bytes = Buffer.from(await (await fetch(file.url)).arrayBuffer());
    assert.equal(createHash("sha512").update(bytes).digest("base64"), file.sha512, `sha512 of ${file.url} must match the manifest`);
  }
}

for (const platform of ["Windows", "MacOS", "LinuxX64", "LinuxArm64"]) {
  test(`${platform}: Customer A on 1.0.0 is offered 1.1.0 with verifiable external downloads`, async () => {
    const result = await checkForUpdates({ feedUrl: feedUrl("contract-a-0001", "1.0.0"), currentVersion: "1.0.0", platform, token: tokenFor({ tid: "customer-a" }) });
    assert.equal(result.available, true);
    assert.equal(result.version, "1.1.0");
    await assertDownloadsVerify(result.files);
  });
}

test("Internal QA is offered 1.2.0", async () => {
  const result = await checkForUpdates({ feedUrl: feedUrl("contract-qa-001", "1.0.0"), currentVersion: "1.0.0", platform: "MacOS", token: tokenFor({ grp: ["internal-qa"] }) });
  assert.equal(result.version, "1.2.0");
  assert.equal(result.available, true);
});

test("an anonymous installation on 1.0.0 gets no update; one on 0.9.0 gets 1.0.0", async () => {
  const current = await checkForUpdates({ feedUrl: feedUrl("contract-anon-01", "1.0.0"), currentVersion: "1.0.0", platform: "Windows" });
  assert.equal(current.available, false);
  const older = await checkForUpdates({ feedUrl: feedUrl("contract-anon-02", "0.9.0"), currentVersion: "0.9.0", platform: "Windows" });
  assert.equal(older.available, true);
  assert.equal(older.version, "1.0.0");
});

test("a no-update answer is not a downgrade even when allowDowngrade is on (custom channel)", async () => {
  const result = await checkForUpdates({ feedUrl: feedUrl("contract-down-01", "1.2.0"), currentVersion: "1.2.0", platform: "Windows", token: tokenFor({ tid: "customer-a" }), channel: "latest" });
  assert.equal(result.available, false);
  assert.equal(result.version, "1.2.0");
});

test("published release notes reach electron-updater and the notes API", async () => {
  const result = await checkForUpdates({ feedUrl: feedUrl("contract-a-0002", "1.0.0"), currentVersion: "1.0.0", platform: "Windows", token: tokenFor({ tid: "customer-a" }) });
  assert.match(String(result.releaseNotes), /Crash when offline/);
  const notes = await (await fetch(`${RELEASER_URL}/api/client/v1/apps/${appKey}/notes?since=1.0.0`)).json();
  assert.deepEqual(notes.map((n) => n.version), ["1.1.0"]);
});

test("Customer B: ~20% cohort is stable, and pausing stops offers within the freshness bound", async () => {
  const token = tokenFor({ tid: "customer-b" });
  const ids = Array.from({ length: 200 }, (_, i) => `contract-b-${String(i).padStart(4, "0")}`);
  const offered = async () => {
    const results = await Promise.all(ids.map((id) => checkForUpdates({ feedUrl: feedUrl(id, "1.0.0"), currentVersion: "1.0.0", platform: "LinuxX64", token })));
    return ids.filter((_, i) => results[i].available && results[i].version === "1.2.0");
  };
  const first = await offered();
  assert.ok(first.length >= 20 && first.length <= 65, `expected ~20% of 200, got ${first.length}`);
  assert.deepEqual(await offered(), first, "repeated checks keep the same cohort");

  const { app, deployments } = scenario;
  await scenario.admin.post(`/api/admin/v1/applications/${app.id}/deployments/${deployments.customerB.id}/pause`);
  await new Promise((resolve) => setTimeout(resolve, FRESHNESS_MS));
  assert.deepEqual(await offered(), [], "a paused deployment offers nothing");

  const qa = await checkForUpdates({ feedUrl: feedUrl("contract-qa-002", "1.0.0"), currentVersion: "1.0.0", platform: "LinuxX64", token: tokenFor({ grp: ["internal-qa"] }) });
  assert.equal(qa.version, "1.2.0", "unrelated deployments are unchanged");
});

test("when the platform rejects a request, electron-updater reports an error instead of updating", async () => {
  await assert.rejects(() => checkForUpdates({ feedUrl: `${RELEASER_URL}/u/${appKey}/bad/1.0.0/`, currentVersion: "1.0.0", platform: "Windows" }));
});
