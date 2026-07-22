import { expect, test } from "@playwright/test";
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
  await expect(page.locator('meta[property="og:type"]')).toHaveAttribute(
    "content",
    "product",
  );
});
