// Seeds the PRD §6 scenario against a running Releaser and verifies the acceptance criteria.
// Uses only Node built-ins; runs in the demo compose profile or locally:
//   RELEASER_URL=http://localhost:5080 ARTIFACT_BASE_URL=http://localhost:8081 node scripts/demo/run-demo.mjs
import { createHash } from "node:crypto";
import { ReleaserAdmin, createContextSigner, seedPrdScenario } from "../lib/releaser-admin.mjs";

const RELEASER_URL = (process.env.RELEASER_URL ?? "http://localhost:8080").replace(/\/$/, "");
const ARTIFACT_BASE_URL = (process.env.ARTIFACT_BASE_URL ?? "http://localhost:8081").replace(/\/$/, "");
const FRESHNESS_MS = Number(process.env.RELEASER_FRESHNESS_SECONDS ?? 5) * 1000 + 500;
const appKey = process.env.DEMO_APP_KEY ?? `sample-app-${Date.now().toString(36)}`;

const results = [];
function check(name, ok, detail = "") {
  results.push({ name, ok });
  console.log(`${ok ? "PASS" : "FAIL"}  ${name}${detail ? ` — ${detail}` : ""}`);
}

/** Requests the feed exactly as electron-updater's generic provider would. */
async function feed(installationId, version, { token, file = "latest.yml" } = {}) {
  const response = await fetch(`${RELEASER_URL}/u/${appKey}/${installationId}/${version}/${file}`, {
    headers: { "user-agent": "electron-builder", ...(token ? { authorization: `Bearer ${token}` } : {}) },
  });
  const body = await response.text();
  const offered = body.match(/^version: (\S+)$/m)?.[1];
  const files = [...body.matchAll(/^- url: (\S+)\n {2}sha512: (\S+)$/gm)].map((m) => ({ url: m[1], sha512: m[2] }));
  return { status: response.status, offered: files.length > 0 ? offered : null, files, contentType: response.headers.get("content-type") };
}

const admin = new ReleaserAdmin(RELEASER_URL);
await admin.login(process.env.RELEASER_ADMIN_EMAIL ?? "admin@example.com", process.env.RELEASER_ADMIN_PASSWORD ?? "ChangeMe-Demo-Only-1");
const signer = createContextSigner();
const token = (claims) => signer.sign({ aud: appKey, ...claims });

console.log(`Seeding application '${appKey}' with releases hosted at ${ARTIFACT_BASE_URL}\n`);
const { app, deployments } = await seedPrdScenario(admin, { appKey, artifactBaseUrl: ARTIFACT_BASE_URL, publicKeyPem: signer.publicKeyPem });

// 1. Targeted offers
check("Customer A on 1.0.0 is offered 1.1.0", (await feed("demo-a-0001", "1.0.0", { token: token({ tid: "customer-a" }) })).offered === "1.1.0");
check("Internal QA is offered 1.2.0", (await feed("demo-qa-001", "1.0.0", { token: token({ grp: ["internal-qa"] }) })).offered === "1.2.0");
check("A Customer A member in QA gets the more specific QA deployment (1.2.0)",
  (await feed("demo-aq-001", "1.0.0", { token: token({ tid: "customer-a", grp: ["internal-qa"] }) })).offered === "1.2.0");
check("Default audience on 0.9.0 is offered 1.0.0", (await feed("demo-d-0001", "0.9.0")).offered === "1.0.0");
check("Default audience on 1.0.0 gets no update", (await feed("demo-d-0002", "1.0.0")).offered === null);
check("No downgrade: Customer A on 1.2.0 gets no update", (await feed("demo-a-0002", "1.2.0", { token: token({ tid: "customer-a" }) })).offered === null);

// 2. Stable 20% rollout for Customer B
const tokenB = token({ tid: "customer-b" });
const ids = Array.from({ length: 1000 }, (_, i) => `demo-b-${String(i).padStart(4, "0")}`);
/** Runs checks with bounded concurrency, like a fleet of installations polling independently. */
async function mapLimited(items, limit, fn) {
  const results = new Array(items.length);
  let next = 0;
  await Promise.all(Array.from({ length: limit }, async () => {
    while (next < items.length) {
      const index = next++;
      results[index] = await fn(items[index]);
    }
  }));
  return results;
}
const cohort = async () =>
  (await mapLimited(ids, 25, async (id) => ((await feed(id, "1.0.0", { token: tokenB })).offered === "1.2.0" ? id : null))).filter(Boolean);
const first = await cohort();
const second = await cohort();
check("Customer B rollout offers 1.2.0 to about 20% of 1000 installations", first.length >= 160 && first.length <= 240, `${first.length}/1000 eligible`);
check("Customer B cohort is identical across repeated checks", JSON.stringify(first) === JSON.stringify(second));

// 3. Binaries stay external, checksums intact
const offer = await feed("demo-qa-002", "1.0.0", { token: token({ grp: ["internal-qa"] }) });
check("Download URLs point only at the external artifact host", offer.files.length > 0 && offer.files.every((f) => f.url.startsWith(ARTIFACT_BASE_URL + "/")),
  offer.files.map((f) => f.url).join(", "));
let checksumsOk = true;
for (const file of offer.files) {
  const bytes = Buffer.from(await (await fetch(file.url)).arrayBuffer());
  checksumsOk &&= createHash("sha512").update(bytes).digest("base64") === file.sha512;
}
check("Artifacts downloaded from the external host match the served sha512", checksumsOk);
check("The platform serves only YAML metadata", (offer.contentType ?? "").startsWith("text/yaml"));

// 4. Pause Customer B
await admin.post(`/api/admin/v1/applications/${app.id}/deployments/${deployments.customerB.id}/pause`);
console.log(`\nPaused 'Customer B'; waiting ${FRESHNESS_MS} ms (freshness bound)…`);
await new Promise((resolve) => setTimeout(resolve, FRESHNESS_MS));
const afterPause = await cohort();
check("After pause, no Customer B installation is offered 1.2.0", afterPause.length === 0, `${afterPause.length} still offered`);
check("Unrelated deployments are unchanged after the pause",
  (await feed("demo-a-0003", "1.0.0", { token: token({ tid: "customer-a" }) })).offered === "1.1.0" &&
  (await feed("demo-qa-003", "1.0.0", { token: token({ grp: ["internal-qa"] }) })).offered === "1.2.0");

// 5. Rollout changes without rebuilding
await admin.post(`/api/admin/v1/applications/${app.id}/deployments/${deployments.customerB.id}/resume`);
await admin.put(`/api/admin/v1/applications/${app.id}/deployments/${deployments.customerB.id}/rollout`, { percentage: 50, priority: 0 });
await new Promise((resolve) => setTimeout(resolve, FRESHNESS_MS));
const widened = await cohort();
check("Raising Customer B to 50% keeps every earlier member and adds more (no rebuild)", first.every((id) => widened.includes(id)) && widened.length > first.length,
  `${widened.length}/1000 eligible`);

// 6. Release notes
const notes = await (await fetch(`${RELEASER_URL}/api/client/v1/apps/${appKey}/notes?since=1.0.0`)).json();
check("Applications can fetch published release notes", notes.length === 1 && notes[0].version === "1.1.0" && notes[0].title === "Faster sync");

// 7. Audit trail
const audit = await admin.get(`/api/admin/v1/audit?appId=${app.id}&limit=200`);
const actions = new Set(audit.entries.map((e) => e.action));
check("Administrative changes are audited", ["application.created", "release.registered", "audience.created", "deployment.activate", "deployment.pause", "deployment.rollout_changed", "release_note.published"].every((a) => actions.has(a)),
  `${audit.entries.length} entries`);

const failed = results.filter((r) => !r.ok).length;
console.log(`\n${results.length - failed}/${results.length} acceptance checks passed. Dashboard: ${process.env.RELEASER_PUBLIC_BASE_URL ?? "http://localhost:8080"} (application '${appKey}').`);
process.exit(failed === 0 ? 0 : 1);
