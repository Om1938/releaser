import { expect, expectAccessible, seeded, test } from "./fixtures";

// Issue #8: register Windows now, add the macOS manifest to the same release later.
test("register a Windows-only release, then add macOS from the release page", async ({ signedIn: page }) => {
  const { artifactBaseUrl } = seeded();
  const key = `plat-${Date.now().toString(36)}`;
  await page.getByRole("button", { name: "New application" }).first().click();
  await page.getByLabel("Name", { exact: true }).fill("Platform Later App");
  await page.getByLabel("Key", { exact: true }).fill(key);
  await page.getByRole("button", { name: "Create application" }).click();
  await expect(page.getByRole("heading", { name: "Platform Later App" })).toBeVisible();

  await page.getByRole("navigation", { name: "Application sections" }).getByRole("link", { name: "Releases" }).click();
  await page.getByRole("button", { name: "Register release" }).first().click();
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel("Version").fill("1.0.0");
  await expect(dialog.getByLabel("Windows", { exact: true })).toBeChecked();
  await expect(dialog.getByLabel("macOS", { exact: true })).not.toBeChecked();
  await dialog.getByLabel("Release folder URL").fill(`${artifactBaseUrl}/1.0.0`);
  await dialog.getByRole("button", { name: "Fill selected platforms" }).click();
  await expect(dialog.getByLabel(/macOS — latest-mac\.yml/)).toHaveCount(0);
  await expectAccessible(page);
  await dialog.getByRole("button", { name: /register/i }).click();
  await expect(dialog).toBeHidden();

  await page.getByRole("link", { name: "1.0.0", exact: true }).click();
  await expect(page.getByText(/Not registered yet: macOS/)).toBeVisible();
  await page.getByRole("button", { name: "Add platform manifest" }).click();
  const add = page.getByRole("dialog");
  await expect(add.getByLabel(/Manifest URL/)).toHaveValue(`${artifactBaseUrl}/1.0.0/latest-mac.yml`);
  await expectAccessible(page);
  await add.getByRole("button", { name: /add/i }).click();
  await expect(add).toBeHidden();
  await expect(page.getByRole("region", { name: "macOS manifest" })).toBeVisible();
  await expect(page.getByRole("region", { name: "Windows manifest" })).toBeVisible();
});
