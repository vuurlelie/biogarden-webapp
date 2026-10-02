import { defineConfig, devices } from "@playwright/test";

const api = process.env.E2E_API_URL;
if (!api || !["localhost", "127.0.0.1", "[::1]"].includes(new URL(api).hostname)) {
  throw new Error("Run this suite through RealBrowserJourneyTests with its disposable loopback API.");
}
export default defineConfig({
  testDir: "./e2e-real", workers: 1, retries: 0, timeout: 60_000,
  expect: { timeout: 15_000 }, reporter: "list",
  use: { baseURL: "http://localhost:4174", trace: "retain-on-failure" },
  webServer: {
    command: `${process.platform === "win32" ? "npm.cmd" : "npm"} run dev -- --host localhost --port 4174 --strictPort`,
    url: "http://localhost:4174", reuseExistingServer: false, timeout: 120_000,
    env: { VITE_API_BASE_URL: api },
  },
  projects: [{ name: "chromium-real", use: { ...devices["Desktop Chrome"] } }],
});
