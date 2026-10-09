import { test } from "node:test";
import assert from "node:assert/strict";
import { createServer } from "node:http";
import { execFile } from "node:child_process";
import { promisify } from "node:util";
import { mkdtemp, mkdir, readFile, writeFile, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const run = promisify(execFile);
for (const enabled of [false, true]) {
  test(`generated index and article previews preserve indexing policy (${enabled})`, async () => {
    const directory = await mkdtemp(join(tmpdir(), "biogarden-seo-"));
    const server = createServer((_request, response) => {
      response.setHeader("Content-Type", "application/json");
      response.end(JSON.stringify({ items: [{ id: 42, title: "Public article", article: "<p>Test description.</p>", image: "/test.jpg", draft: false }] }));
    });
    try {
      await new Promise(resolve => server.listen(0, "127.0.0.1", resolve));
      await mkdir(join(directory, "dist"));
      await writeFile(join(directory, "dist/index.html"), '<html><head><title>Shop</title><meta name="robots" content="noindex, nofollow"></head><body></body></html>');
      await run(process.execPath, [fileURLToPath(new URL("./generate-seo-files.mjs", import.meta.url))], {
        cwd: directory,
        env: { ...process.env, VITE_INDEXING_ENABLED: String(enabled), VITE_SITE_URL: "https://shop.example",
          VITE_API_BASE_URL: `http://127.0.0.1:${server.address().port}`, VITE_REQUIRE_SEO_POSTS: "true" },
      });
      for (const path of ["index.html", "tudastar/42/index.html"]) {
        const html = await readFile(join(directory, "dist", path), "utf8");
        assert.equal((html.match(/name="robots"/g) ?? []).length, 1);
        assert.ok(html.includes(`name="robots" content="${enabled ? "index, follow" : "noindex, nofollow"}"`));
      }
    } finally {
      await new Promise(resolve => server.close(resolve));
      assert.equal(dirname(directory), tmpdir());
      await rm(
        directory,
        { recursive: true, force: true }
      );
    }
  });
}
