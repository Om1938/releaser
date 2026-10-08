import { expect, expectAccessible, seeded, test } from "./fixtures";

test("unauthenticated visitors are sent to sign in", async ({ page }) => {
  await page.goto("/audit");
  await expect(page).toHaveURL(/\/login/);
  await expectAccessible(page);
});

test("create an application from the dashboard", async ({ signedIn: page }) => {
  const key = `ui-${Date.now().toString(36)}`;
  await page.getByRole("button", { name: "New application" }).first().click();
  await page.getByLabel("Name", { exact: true }).fill("UI Created App");
  await page.getByLabel("Key", { exact: true }).fill(key);
  await page.getByRole("button", { name: "Create application" }).click();
  await expect(page.getByRole("heading", { name: "UI Created App" })).toBeVisible();
  await expect(page.getByText(`/u/${key}/`).first()).toBeVisible();
});

test("PRD scenario: releases, deployments, pause and explainer", async ({ signedIn: page }) => {
  const { appId } = seeded();
  await page.goto(`/apps/${appId}/releases`);
  for (const version of ["1.0.0", "1.1.0", "1.2.0"]) {
    await expect(page.getByRole("link", { name: version, exact: true })).toBeVisible();
  }
  await expectAccessible(page);

  await page.goto(`/apps/${appId}/deployments`);
  const customerB = page.getByRole("row", { name: /Customer B/ });
  await expect(customerB.getByText("20%", { exact: true }).filter({ visible: true })).toBeVisible();
  await customerB.getByRole("button", { name: "Pause" }).click();
  await expect(customerB.getByText("Paused").filter({ visible: true })).toBeVisible();
  await expect(page.getByText(/eligibility/i).first()).toBeVisible();
  await expectAccessible(page);

  await page.goto(`/apps/${appId}/explain`);
  await page.getByLabel("Customer id").fill("customer-a");
  await page.getByRole("button", { name: "Explain decision" }).click();
  await expect(page.getByText("Offered 1.1.0")).toBeVisible();
  await expectAccessible(page);

  await page.goto("/audit");
  await expect(page.getByText("deployment.pause").first()).toBeVisible();
});

test("release detail shows external manifests and published notes", async ({ signedIn: page }) => {
  const { appId } = seeded();
  await page.goto(`/apps/${appId}/releases`);
  await page.getByRole("link", { name: "1.1.0", exact: true }).click();
  await expect(page.getByText("Published", { exact: true }).first()).toBeVisible();
  await expect(page.getByText("latest-mac.yml").first()).toBeVisible();
  await expectAccessible(page);
});

for (const path of ["", "/audiences", "/policies", "/settings"]) {
  test(`application page ${path || "/"} is accessible`, async ({ signedIn: page }) => {
    await page.goto(`/apps/${seeded().appId}${path}`);
    await expect(page.getByRole("navigation", { name: "Application sections" })).toBeVisible();
    await expectAccessible(page);
  });
}

test("navigation works on a phone-sized screen @responsive", async ({ signedIn: page }) => {
  await page.goto(`/apps/${seeded().appId}/deployments`);
  await expect(page.getByRole("heading", { name: "Deployments" })).toBeVisible();
  await page.getByRole("button", { name: "Toggle Sidebar" }).click();
  await expect(page.getByRole("link", { name: "Audit log" })).toBeVisible();
});
