import { defineConfig, devices } from "@playwright/test";

const api = process.env.E2E_API_URL;
if (!api || !["localhost", "127.0.0.1", "[::1]"].includes(new URL(api).hostname)) {
  throw new Error("Run this suite through RealBrowserJourneyTests with its disposable loopback API.");
}
export default defineConfig({
  outputDir: `test-results/real-${process.env.E2E_BROWSER_PROJECT}`,
  testDir: "./e2e-real", workers: 1, retries: 0, timeout: 60_000,
  expect: { timeout: 15_000 }, reporter: "list",
  use: { baseURL: "https://localhost:4174", trace: "retain-on-failure", ignoreHTTPSErrors: true, actionTimeout: 15_000 },
  webServer: {
    command: `${process.platform === "win32" ? "npm.cmd" : "npm"} run dev -- --host localhost --port 4174 --strictPort --config vite.real.config.ts`,
    url: "https://localhost:4174", ignoreHTTPSErrors: true, reuseExistingServer: false, timeout: 120_000,
    env: { VITE_API_BASE_URL: api },
  },
  projects: [
    { name: "chromium-real", use: { ...devices["Desktop Chrome"] } },
    { name: "firefox-real", use: { ...devices["Desktop Firefox"] } },
    { name: "mobile-real", use: { ...devices["Pixel 7"] } },
  ],
});
