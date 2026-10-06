import { test, expect } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";
import { mockPublicShopApi, mockCheckoutApi, mockCatalogPaginationApi } from "./fixtures";

for (const path of ["/", "/rolunk", "/tudastar", "/termekek", "/termekek/1", "/kosar", "/penztar", "/bejelentkezes", "/does-not-exist"]) {
  test(`public journey accessibility: ${path}`, async ({ page }) => {
    if (path === "/penztar") await mockCheckoutApi(page);
    else if (path === "/tudastar") await mockCatalogPaginationApi(page);
    else await mockPublicShopApi(page);
    await page.goto(path);
    await expect(page.getByRole("heading", { level: 1 })).toHaveCount(1);
    await expect(page.getByRole("main")).toHaveCount(1);
    const result = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa", "wcag21aa"]).analyze();
    expect(result.violations).toEqual([]);
  });
}

for (const width of [320, 375, 768]) {
  test(`checkout and catalogue fit ${width}px`, async ({ page }) => {
    await page.setViewportSize({ width, height: 900 });
    await mockCheckoutApi(page);
    await page.goto("/penztar");
    await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    await mockPublicShopApi(page);
    await page.goto("/termekek");
    await expect(page.getByRole("button", { name: /Kosárba:/ }).first()).toBeVisible();
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  });
}

test("catalogue announces successful cart additions", async ({ page }) => {
  await mockPublicShopApi(page);
  await page.goto("/termekek");
  await page.getByRole("button", { name: /Kosárba:/ }).first().click();
  await expect(page.locator('[aria-live="polite"]')).toContainText("bekerült a kosárba");
  await expect(page.getByRole("link", { name: /Kosár, [1-9]/ }).first()).toBeVisible();
});

test("carousel can be paused and respects reduced motion", async ({ page }) => {
  await page.clock.install();
  await mockPublicShopApi(page);
  await page.goto("/");
  const carousel = page.getByRole("region", { name: "Kertészeti képek" });
  const track = carousel.locator("div[style]").first();
  await carousel.getByRole("button", { name: "Automatikus lejátszás szüneteltetése" }).click();
  await page.evaluate(() => (document.activeElement as HTMLElement).blur());
  const initial = await track.getAttribute("style");
  await page.clock.fastForward(6000);
  await expect(track).toHaveAttribute("style", initial!);
  await carousel.getByRole("button", { name: "Automatikus lejátszás folytatása" }).click();
  await page.evaluate(() => (document.activeElement as HTMLElement).blur());
  await page.mouse.move(0, 0);
  await page.clock.fastForward(6000);
  await expect(track).not.toHaveAttribute("style", initial!);
  await page.emulateMedia({ reducedMotion: "reduce" });
  const stopped = await track.getAttribute("style");
  await page.clock.fastForward(6000);
  await expect(track).toHaveAttribute("style", stopped!);
});
