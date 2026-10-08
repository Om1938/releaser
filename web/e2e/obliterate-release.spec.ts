import type { Page } from "@playwright/test";
import { expect, expectAccessible, seeded, test } from "./fixtures";

async function registerWindowsRelease(page: Page, artifactBaseUrl: string, version: string) {
  await page.getByRole("button", { name: "Register release" }).first().click();
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel("Version").fill(version);
  await dialog.getByLabel("Release folder URL").fill(`${artifactBaseUrl}/${version}`);
  await dialog.getByRole("button", { name: "Fill selected platforms" }).click();
  await dialog.getByRole("button", { name: /register/i }).click();
  await expect(dialog).toBeHidden();
}

// Issue #12: recover from a registration mistake by obliterating the release and registering the version again.
test("obliterate a release and register the same version again", async ({ signedIn: page }) => {
  const { artifactBaseUrl } = seeded();
  await page.getByRole("button", { name: "New application" }).first().click();
  const create = page.getByRole("dialog");
  await create.getByLabel("Name", { exact: true }).fill("Obliterate App");
  await create.getByLabel("Key", { exact: true }).fill(`obl-${Date.now().toString(36)}`);
  await create.getByLabel("Windows", { exact: true }).check();
  await create.getByRole("button", { name: "Create application" }).click();
  await page.getByRole("navigation", { name: "Application sections" }).getByRole("link", { name: "Releases" }).click();

  await registerWindowsRelease(page, artifactBaseUrl, "1.0.0");
  await page.getByRole("link", { name: "1.0.0", exact: true }).click();
  await page.getByRole("button", { name: "Obliterate release…" }).click();

  const dialog = page.getByRole("dialog");
  await expect(dialog.getByText("This permanently deletes:")).toBeVisible();
  const confirm = dialog.getByRole("button", { name: "Obliterate 1.0.0" });
  await expect(confirm).toBeDisabled();
  await dialog.getByLabel("Type 1.0.0 to confirm").fill("1.0");
  await expect(confirm).toBeDisabled();
  await expectAccessible(page);
  await dialog.getByLabel("Type 1.0.0 to confirm").fill("1.0.0");
  await confirm.click();

  await expect(page.getByText("Release 1.0.0 was obliterated")).toBeVisible();
  await expect(page.getByText("No releases registered")).toBeVisible();

  await registerWindowsRelease(page, artifactBaseUrl, "1.0.0");
  await expect(page.getByRole("link", { name: "1.0.0", exact: true })).toBeVisible();
});
