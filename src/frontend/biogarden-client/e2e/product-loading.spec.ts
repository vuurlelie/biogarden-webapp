import { expect, test, type Route } from "@playwright/test";
import { apiBaseUrl, mockPublicShopApi, product } from "./fixtures";

test("slow recommendations do not delay availability or purchasing", async ({ page }) => {
  await mockPublicShopApi(page);
  let recommendations: Route | undefined;
  let availabilityReads = 0;
  await page.route(url => url.origin === apiBaseUrl && url.pathname === "/api/products", route => { recommendations = route; });
  await page.route(`${apiBaseUrl}/api/cart/products/1/availability`, route => {
    availabilityReads++; return route.fulfill({ json: { availableQuantity: 8 } });
  });
  await page.goto("/termekek/1");
  await expect.poll(() => recommendations).toBeTruthy();
  await expect(page.getByRole("button", { name: "Kosárba teszem", exact: true })).toBeEnabled();
  expect(availabilityReads).toBe(1);
  await page.getByRole("button", { name: "Kosárba teszem", exact: true }).click();
  await expect(page.getByRole("status")).toContainText("bekerült a kosárba");
  await recommendations!.fulfill({ json: {
    items: [product, { ...product, id: 2, name: "Kapcsolódó teszttermék" }], page: 1, pageSize: 60, totalCount: 2, totalPages: 1,
  } });
  await expect(page.getByRole("heading", { name: "Kapcsolódó teszttermék", exact: true })).toBeVisible();
});

test("failed recommendations leave the product available for purchase", async ({ page }) => {
  await mockPublicShopApi(page);
  await page.route(url => url.origin === apiBaseUrl && url.pathname === "/api/products",
    route => route.fulfill({ status: 503, json: { detail: "Átmeneti hiba." } }));
  await page.goto("/termekek/1");
  await expect(page.getByRole("heading", { name: product.name, exact: true })).toBeVisible();
  await page.getByRole("button", { name: "Kosárba teszem", exact: true }).click();
  await expect(page.getByRole("status")).toContainText("bekerült a kosárba");
  await expect(page.getByRole("heading", { name: "Kapcsolódó termékek", exact: true })).toHaveCount(0);
});

test("navigating to a related product clears the previous product's stock refresh failure", async ({ page }) => {
  await mockPublicShopApi(page);
  let reads = 0;
  await page.route(`${apiBaseUrl}/api/cart/products/1/availability`, route => ++reads === 1
    ? route.fulfill({ json: { availableQuantity: 8 } })
    : route.fulfill({ status: 503, json: { detail: "Átmeneti hiba." } }));
  await page.route(`${apiBaseUrl}/api/products/2`, route => route.fulfill({ json: {
    ...product, id: 2, name: "Teszt kakukkfu tea", description: "Másik termék", stockQuantity: 8,
  } }));
  await page.route(`${apiBaseUrl}/api/cart/products/2/availability`, route => route.fulfill({ json: { availableQuantity: 8 } }));
  await page.goto("/termekek/1");
  await page.getByRole("button", { name: "Kosárba teszem", exact: true }).click();
  await expect(page.getByRole("alert")).toContainText("a fennmaradó készletet nem sikerült frissíteni");
  await page.getByRole("heading", { name: "Teszt kakukkfu tea", exact: true }).getByRole("link").click();
  await expect(page).toHaveURL(/\/termekek\/2$/);
  await expect(page.getByRole("button", { name: "Kosárba teszem", exact: true })).toBeEnabled();
  await expect(page.getByRole("alert")).toHaveCount(0);
  await expect(page.getByRole("status")).toHaveCount(0);
});
