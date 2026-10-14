import { expect, test } from "./test";
import { mockPublicShopApi } from "./fixtures";

test.use({ viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true });

for (const path of ["/bejelentkezes", "/regisztracio", "/elfelejtett-jelszo", "/termekek/1"]) {
  test(`mobile fields remain readable and stable across focus: ${path}`, async ({ page }) => {
    await mockPublicShopApi(page);
    await page.goto(path);
    const fields = page.locator('input:not([type="checkbox"], [type="radio"], [type="hidden"]), textarea, select');
    await expect(fields.first()).toBeVisible();
    for (const field of await fields.all()) {
      if (!await field.isVisible()) continue;
      expect(await field.evaluate(el => parseFloat(getComputedStyle(el).fontSize))).toBeGreaterThanOrEqual(16);
    }
    const field = fields.first();
    const before = await field.boundingBox();
    await field.focus();
    await page.getByRole('heading', { level: 1 }).click();
    await expect(field).not.toBeFocused();
    const after = await field.boundingBox();
    expect(after?.width).toBe(before?.width);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
    const viewport = await page.locator('meta[name="viewport"]').getAttribute('content');
    expect(viewport).not.toMatch(/user-scalable\s*=\s*no|maximum-scale\s*=\s*1/);
  });
}
