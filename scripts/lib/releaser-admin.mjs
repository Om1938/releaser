// Minimal admin API client for scripts (demo seeding, contract tests). Uses only Node built-ins.
import { generateKeyPairSync, sign } from "node:crypto";

export class ReleaserAdmin {
  /** @param {string} baseUrl */
  constructor(baseUrl) {
    this.baseUrl = baseUrl.replace(/\/$/, "");
    this.cookie = "";
  }

  async login(email, password) {
    const response = await this.#send("POST", "/api/admin/v1/auth/login", { email, password });
    this.cookie = (response.headers.getSetCookie?.() ?? []).map((c) => c.split(";")[0]).join("; ");
    return response.json();
  }

  get(path) { return this.json("GET", path); }
  post(path, body) { return this.json("POST", path, body); }
  put(path, body) { return this.json("PUT", path, body); }

  async json(method, path, body) {
    const response = await this.#send(method, path, body);
    return response.status === 204 ? null : response.json();
  }

  async #send(method, path, body) {
    const response = await fetch(this.baseUrl + path, {
      method,
      headers: {
        "content-type": "application/json",
        "x-releaser-csrf": "1",
        ...(this.cookie ? { cookie: this.cookie } : {}),
      },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
    if (!response.ok) {
      throw new Error(`${method} ${path} -> ${response.status}: ${await response.text()}`);
    }
    return response;
  }
}

const PLATFORM_FILES = {
  Windows: "latest.yml",
  MacOS: "latest-mac.yml",
  LinuxX64: "latest-linux.yml",
  LinuxArm64: "latest-linux-arm64.yml",
};

/**
 * Seeds the PRD §6 scenario: releases 1.0.0/1.1.0/1.2.0 referencing an external artifact host,
 * audiences Customer A, Customer B, Internal QA and Everyone, and the four deployments.
 */
export async function seedPrdScenario(admin, { appKey, artifactBaseUrl, publicKeyPem }) {
  const app = await admin.post("/api/admin/v1/applications", {
    key: appKey, name: "Sample App", description: "Electron sample app (PRD §6 scenario)", defaultChannelKey: "stable", defaultChannelName: "Stable",
    supportedPlatforms: Object.keys(PLATFORM_FILES),
  });
  const base = `/api/admin/v1/applications/${app.id}`;
  const releases = {};
  for (const version of ["1.0.0", "1.1.0", "1.2.0"]) {
    releases[version] = await admin.post(`${base}/releases`, {
      version, title: `Sample App ${version}`, channels: ["stable"],
      manifests: Object.entries(PLATFORM_FILES).map(([platform, file]) => ({ platform, url: `${artifactBaseUrl}/${version}/${file}` })),
    });
  }
  const audience = (name, ...includes) => admin.post(`${base}/audiences`, { name, description: null, includes, excludes: [] });
  const everyone = await audience("Everyone", { kind: "everyone" });
  const customerA = await audience("Customer A", { kind: "customer", customerIds: ["customer-a"] });
  const customerB = await audience("Customer B", { kind: "customer", customerIds: ["customer-b"] });
  const qa = await audience("Internal QA", { kind: "group", groups: ["internal-qa"] });

  const deploy = async (name, version, audienceId, percentage) => {
    const created = await admin.post(`${base}/deployments`, { name, releaseId: releases[version].id, channel: "stable", audienceId, percentage, priority: 0 });
    return admin.post(`${base}/deployments/${created.id}/activate`);
  };
  const deployments = {
    default: await deploy("Default", "1.0.0", everyone.id, 100),
    customerA: await deploy("Customer A", "1.1.0", customerA.id, 100),
    customerB: await deploy("Customer B", "1.2.0", customerB.id, 20),
    qa: await deploy("Internal QA", "1.2.0", qa.id, 100),
  };

  await admin.put(`${base}/releases/${releases["1.1.0"].id}/notes`, {
    title: "Faster sync", summary: "Sync is now twice as fast.", bodyMarkdown: "Sync runs in the background.",
    changes: [{ category: "Added", text: "Background sync" }, { category: "Fixed", text: "Crash when offline" }],
  });
  await admin.post(`${base}/releases/${releases["1.1.0"].id}/notes/publish`);
  if (publicKeyPem) {
    await admin.post(`${base}/context-keys`, { name: "Publisher backend", publicKeyPem });
  }
  return { app, releases, deployments };
}

/** Plays the publisher backend: an ES256 key pair that signs identity-context tokens. */
export function createContextSigner() {
  const { privateKey, publicKey } = generateKeyPairSync("ec", { namedCurve: "P-256" });
  const encode = (value) => Buffer.from(JSON.stringify(value)).toString("base64url");
  return {
    publicKeyPem: publicKey.export({ type: "spki", format: "pem" }).toString(),
    /** @param {{aud: string, tid?: string, sub?: string, grp?: string[], iid?: string}} claims */
    sign(claims, lifetimeSeconds = 900) {
      const now = Math.floor(Date.now() / 1000);
      const unsigned = `${encode({ alg: "ES256", typ: "JWT" })}.${encode({ ...claims, iat: now, nbf: now, exp: now + lifetimeSeconds })}`;
      const signature = sign("sha256", Buffer.from(unsigned), { key: privateKey, dsaEncoding: "ieee-p1363" });
      return `${unsigned}.${signature.toString("base64url")}`;
    },
  };
}
