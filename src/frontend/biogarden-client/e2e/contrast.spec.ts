import { test, expect } from "./test";
import { mockPublicShopApi } from "./fixtures";

test("small text theme combinations retain AA contrast", async ({ page }) => {
  await mockPublicShopApi(page);
  await page.goto("/termekek");
  const ratios = await page.evaluate(() => {
    const styles = getComputedStyle(document.documentElement);
    const luminance = (hex: string) => {
      const rgb = hex.trim().replace("#", "").match(/../g)!.map(x => parseInt(x, 16) / 255)
        .map(x => x <= 0.04045 ? x / 12.92 : ((x + 0.055) / 1.055) ** 2.4);
      return rgb[0] * 0.2126 + rgb[1] * 0.7152 + rgb[2] * 0.0722;
    };
    const pairs = [["#ffffff", styles.getPropertyValue("--color-site-green")],
      ["#ffffff", styles.getPropertyValue("--color-site-green-hover")],
      [styles.getPropertyValue("--color-site-muted"), "#ffffff"],
      [styles.getPropertyValue("--color-site-muted"), styles.getPropertyValue("--color-site-surface-soft")]];
    return pairs.map(([a, b]) => (Math.max(luminance(a), luminance(b)) + 0.05) / (Math.min(luminance(a), luminance(b)) + 0.05));
  });
  for (const ratio of ratios) expect(ratio).toBeGreaterThanOrEqual(4.5);
});
