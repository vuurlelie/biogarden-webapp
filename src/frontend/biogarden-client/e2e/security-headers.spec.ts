import { expect, test } from "./test";
import { readFileSync } from "node:fs";

test("frontend HTML responses deny framing and retain the resource CSP", async ({ page }) => {
  const response = await page.goto("/");
  const headers = response!.headers();
  expect(headers["content-security-policy"]).toContain("frame-ancestors 'none'");
  expect(headers["x-frame-options"]).toBe("DENY");
  expect(headers["x-content-type-options"]).toBe("nosniff");
  expect(headers["referrer-policy"]).toBe("strict-origin-when-cross-origin");
  await expect(page.locator('meta[http-equiv="Content-Security-Policy"]')).toHaveAttribute("content", /object-src 'none'/);
  // IIS must apply the same headers to SPA rewrites and generated article HTML.
  const config = readFileSync("public/web.config", "utf8");
  for (const name of ["Content-Security-Policy", "X-Frame-Options", "X-Content-Type-Options", "Referrer-Policy", "Permissions-Policy"]) {
    expect(config).toContain(`name="${name}" value="${headers[name.toLowerCase()]}"`);
  }
});
