import { test } from "node:test";
import assert from "node:assert/strict";
import { execFile } from "node:child_process";
import { promisify } from "node:util";
import { mkdtemp, writeFile, readFile, readdir, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { fileURLToPath } from "node:url";

const run = promisify(execFile);
const vite = fileURLToPath(new URL("../node_modules/vite/bin/vite.js", import.meta.url));
const config = fileURLToPath(new URL("../vite.config.ts", import.meta.url));
const generator = fileURLToPath(new URL("./generate-seo-files.mjs", import.meta.url));

for (const scenario of ["dotenv", "shell", "default", "invalid"]) {
  test(`production build and SEO origins: ${scenario}`, async () => {
    const directory = await mkdtemp(join(tmpdir(), "biogarden-origins-"));
    const environment = Object.fromEntries(Object.entries(process.env).filter(([key]) => !key.startsWith("VITE_") && key !== "MODE"));
    // Closed loopback port avoids external article requests during SEO checks.
    environment.VITE_API_BASE_URL = "http://127.0.0.1:1";
    try {
      await writeFile(join(directory, "index.html"), '<html><head><title>Fixture</title></head><body><script type="module" src="/main.js"></script></body></html>');
      await writeFile(join(directory, "main.js"), 'document.body.textContent = JSON.stringify([import.meta.env.VITE_API_BASE_URL, import.meta.env.VITE_SITE_URL]);');
      if (scenario !== "default")
        await writeFile(join(directory, ".env.production.local"), "VITE_API_BASE_URL=http://127.0.0.1:2/\nVITE_SITE_URL=https://dotenv.example/\n");
      if (scenario === "dotenv") delete environment.VITE_API_BASE_URL;
      if (scenario === "shell") environment.VITE_SITE_URL = "https://shell.example/";
      if (scenario === "invalid") environment.VITE_API_BASE_URL = "https://api.example/api";
      const build = () => run(process.execPath, [vite, "build", "--config", config], { cwd: directory, env: environment });
      if (scenario === "invalid") {
        await assert.rejects(build, error => error.stderr.includes("VITE_API_BASE_URL"));
        return;
      }
      const scripts = async () => {
        const names = await readdir(join(directory, "dist/assets"));
        return (await Promise.all(names.filter(name => name.endsWith(".js")).map(name => readFile(join(directory, "dist/assets", name), "utf8")))).join("\n");
      };
      await build();
      const expectedApi = scenario === "dotenv" ? "http://127.0.0.1:2" : "http://127.0.0.1:1";
      const expectedSite = scenario === "dotenv" ? "https://dotenv.example" : scenario === "shell" ? "https://shell.example" : "https://raczkrisztina.dev";
      const script = await scripts();
      assert.ok(script.includes(expectedApi));
      assert.ok(script.includes(expectedSite));
      assert.ok(!script.includes("jtempurl.com"));
      await run(process.execPath, [generator], { cwd: directory, env: environment });
      const sitemap = await readFile(join(directory, "dist/sitemap.xml"), "utf8");
      assert.ok(sitemap.includes(`<loc>${expectedSite}/</loc>`));
      assert.ok((await readFile(join(directory, "dist/index.html"), "utf8")).includes("noindex, nofollow"));
      if (scenario === "default") {
        delete environment.VITE_API_BASE_URL;
        await build();
        assert.ok((await scripts()).includes("https://api.raczkrisztina.dev"));
      }
    } finally { await rm(directory, { recursive: true, force: true }); }
  });
}
