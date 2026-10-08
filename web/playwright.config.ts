import { defineConfig, devices } from "@playwright/test";

/**
 * Admin workflow + accessibility tests. Expects the API on RELEASER_API (default http://localhost:5080, Development settings)
 * and serves the dashboard through Vite's proxy unless E2E_BASE_URL points at an already running instance.
 */
const baseURL = process.env.E2E_BASE_URL ?? "http://localhost:5173";

export default defineConfig({
  testDir: "./e2e",
  timeout: 60_000,
  fullyParallel: false,
  retries: process.env.CI ? 1 : 0,
  reporter: [["list"], ["html", { open: "never" }]],
  globalSetup: "./e2e/global-setup.ts",
  use: { baseURL, trace: "retain-on-failure", screenshot: "only-on-failure" },
  projects: [
    { name: "desktop", use: { ...devices["Desktop Chrome"] }, grepInvert: /@responsive/ },
    { name: "mobile", use: { ...devices["Pixel 7"] }, grep: /@responsive/ },
  ],
  webServer: process.env.E2E_BASE_URL
    ? undefined
    : { command: "pnpm dev --strictPort", url: baseURL, reuseExistingServer: true, timeout: 60_000 },
});
