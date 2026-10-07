import { test, expect } from "@playwright/test";
import { mockPublicShopApi } from "./fixtures";

test("skip link and route focus reach one main landmark", async ({ page }) => {
  await mockPublicShopApi(page);
  await page.goto("/termekek/1");
  await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
  await expect(page.getByRole("main")).toHaveCount(1);
  await page.keyboard.press("Tab");
  await expect(page.getByRole("link", { name: "Ugrás a tartalomhoz" })).toBeFocused();
  await page.keyboard.press("Enter");
  await expect(page.getByRole("main")).toBeFocused();
  await page.getByRole("link", { name: /^Kosár/ }).first().click();
  await expect(page.getByRole("main")).toBeFocused();
  await expect(page.getByRole("main")).toHaveCount(1);
});

test("reduced motion disables smooth scrolling", async ({ page }) => {
  await mockPublicShopApi(page);
  await page.emulateMedia({ reducedMotion: "reduce" });
  await page.goto("/");
  expect(await page.evaluate(() => getComputedStyle(document.documentElement).scrollBehavior)).toBe("auto");
});
