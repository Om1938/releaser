import AxeBuilder from "@axe-core/playwright";
import { expect, test as base, type Page } from "@playwright/test";
import { readFileSync } from "node:fs";
import { statePath } from "./global-setup";

export const credentials = {
  email: process.env.RELEASER_ADMIN_EMAIL ?? "admin@example.com",
  password: process.env.RELEASER_ADMIN_PASSWORD ?? "ChangeMe-Dev-Only-1",
};

export function seeded(): { appId: string; appKey: string; artifactBaseUrl: string } {
  return JSON.parse(readFileSync(statePath, "utf8")) as { appId: string; appKey: string; artifactBaseUrl: string };
}

export async function signIn(page: Page) {
  await page.goto("/login");
  await page.getByLabel("Email").fill(credentials.email);
  await page.getByLabel("Password").fill(credentials.password);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByRole("heading", { name: "Applications" })).toBeVisible();
}

/** Fails on serious or critical WCAG 2.1 A/AA violations. */
export async function expectAccessible(page: Page) {
  const results = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"]).analyze();
  const blocking = results.violations.filter((v) => v.impact === "serious" || v.impact === "critical");
  expect(blocking.map((v) => `${v.id}: ${v.nodes.map((n) => n.target.join(" ")).join(", ")}`)).toEqual([]);
}

export const test = base.extend<{ signedIn: Page }>({
  signedIn: async ({ page }, use) => {
    await signIn(page);
    await use(page);
  },
});
export { expect };
