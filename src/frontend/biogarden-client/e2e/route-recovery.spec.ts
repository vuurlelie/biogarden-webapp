import { expect, test } from "./test";
import { mockPublicShopApi } from "./fixtures";

test("failed lazy page has a reload action that recovers without a reload loop", async ({ page }) => {
  await mockPublicShopApi(page);
  let blocked = true;
  await page.route("**/pages/public/LoginPage.tsx*", route =>
    blocked ? route.abort("failed") : route.continue());
  await page.goto("/bejelentkezes");
  await expect(page.getByRole("heading", { name: "Az oldal betöltése nem sikerült" })).toBeVisible();
  await expect(page.getByText("Unexpected Application Error!")).toHaveCount(0);
  // A persistent network failure must remain recoverable, without automatic navigation.
  await page.getByRole("button", { name: "Oldal újratöltése" }).click();
  await expect(page.getByRole("heading", { name: "Az oldal betöltése nem sikerült" })).toBeVisible();
  blocked = false;
  await page.getByRole("button", { name: "Oldal újratöltése" }).click();
  await expect(page.getByRole("heading", { name: "Bejelentkezés", exact: true })).toBeVisible();
});

test("reserved stock does not claim immediate availability", async ({ page }) => {
  await mockPublicShopApi(page);
  await page.route("http://localhost:5097/api/cart/products/1/availability", route =>
    route.fulfill({ json: { availableQuantity: 0 } }));
  await page.goto("/termekek/1");
  await expect(page.getByText("Jelenleg nincs szabad készlet", { exact: true })).toBeVisible();
  await expect(page.getByRole("button", { name: "Kosárba teszem", exact: true })).toBeDisabled();
});
