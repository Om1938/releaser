import { expect, expectAccessible, test } from "./fixtures";

// Issue #10: change the application's supported platforms from Settings.
test("drop macOS from an application's supported platforms", async ({ signedIn: page }) => {
  const key = `sp-${Date.now().toString(36)}`;
  await page.getByRole("button", { name: "New application" }).first().click();
  const create = page.getByRole("dialog");
  await create.getByLabel("Name", { exact: true }).fill("Supported Platforms App");
  await create.getByLabel("Key", { exact: true }).fill(key);
  await create.getByLabel("Windows", { exact: true }).check();
  await create.getByLabel("macOS", { exact: true }).check();
  await create.getByRole("button", { name: "Create application" }).click();
  await expect(page.getByText("Windows · macOS")).toBeVisible();

  await page.getByRole("navigation", { name: "Application sections" }).getByRole("link", { name: "Settings" }).click();
  const card = page.locator("[data-slot=card]", { has: page.getByText("Supported platforms", { exact: true }) });
  await card.getByLabel("macOS", { exact: true }).uncheck();
  await expectAccessible(page);
  await card.getByRole("button", { name: "Save platforms" }).click();

  await expect(page.getByText("Supported platforms updated")).toBeVisible();
  await expect(page.getByText("Windows · macOS")).toHaveCount(0);
  await expect(card.getByLabel("macOS", { exact: true })).not.toBeChecked();
});
