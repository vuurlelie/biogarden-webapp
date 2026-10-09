import { expect, test } from "./test";
import {
  apiBaseUrl,
  expectNoUnhandledApiCall,
  mockPublicShopApi,
} from "./fixtures";

test.beforeEach(async ({ page }) => {
  await page.route(`${apiBaseUrl}/api/**`, expectNoUnhandledApiCall);
});

test("public pages expose route-specific metadata", async ({ page }) => {
  await mockPublicShopApi(page);

  await page.goto("/");
  const organizationStructuredData = JSON.parse(
    (await page.locator("#biogarden-structured-data").textContent()) ?? "{}",
  );
  expect(organizationStructuredData["@type"]).toBe("Organization");
  await expect(page).toHaveTitle("Bakony Kincse Biokertészet");
  await expect(page.locator('meta[name="description"]')).toHaveAttribute(
    "content",
    /természetes termékek/i,
  );

  await page.goto("/termekek");
  await expect(page).toHaveTitle("Termékek | Bakony Kincse Biokertészet");
  await expect(page.locator('link[rel="canonical"]')).toHaveAttribute(
    "href",
    /\/termekek$/,
  );

  await page.goto("/termekek/1");
  const productStructuredData = JSON.parse(
    (await page.locator("#biogarden-structured-data").textContent()) ?? "{}",
  );
  expect(productStructuredData["@type"]).toBe("Product");
  expect(productStructuredData.offers.priceCurrency).toBe("HUF");
  await expect(page).toHaveTitle("Teszt levendula tea | Bakony Kincse Biokertészet");
  await expect(page.locator('meta[property="og:type"]')).toHaveAttribute("content", "product");
});

test("private routes clear product metadata during client navigation", async ({ page }) => {
  await mockPublicShopApi(page);
  await page.goto("/termekek/1");
  await expect(page.locator('meta[name="robots"]')).toHaveAttribute("content", "index, follow");
  await page.getByRole("link", { name: /^Kosár/ }).first().click();
  await expect(page).toHaveTitle("Kosár | Bakony Kincse Biokertészet");
  await expect(page.locator('meta[name="robots"]')).toHaveAttribute("content", "noindex, nofollow");
  await expect(page.locator("#biogarden-structured-data")).toHaveCount(0);
  await expect(page.locator('meta[property="og:type"]')).toHaveAttribute("content", "website");
  await page.goto("/does-not-exist");
  await expect(page).toHaveTitle("Az oldal nem található | Bakony Kincse Biokertészet");
  await expect(page.locator('meta[name="robots"]')).toHaveAttribute("content", "noindex, nofollow");
});
