import { expect, test } from "@playwright/test";
import {
  apiBaseUrl,
  expectNoUnhandledApiCall,
  mockCheckoutApi,
  mockEmptyCartApi,
  signInAsAdmin,
} from "./fixtures";

test.beforeEach(async ({ page }) => {
  await page.route("http://localhost:5097/api/**", expectNoUnhandledApiCall);
});

test("checkout review modal traps focus and restores it on close", async ({ page }) => {
  await mockCheckoutApi(page);

  await page.goto("/penztar");
  await page.getByLabel(/Nev|Név/).fill("Teszt Elek");
  await page.getByLabel(/Email cim|Email cím/).fill("teszt@example.com");
  await page.getByLabel(/Telefonszam|Telefonszám/).fill("+36301234567");

  await page.getByRole("button", { name: /Tovabb a penztarhoz|Tovább a pénztárhoz/ }).click();
  await page.getByLabel(/Fizetes atvetelkor|Fizetés átvételkor/).check();

  const reviewButton = page.getByRole("button", { name: /Rendeles attekintese|Rendelés áttekintése/ });
  await reviewButton.focus();
  await page.keyboard.press("Enter");

  const dialog = page.getByRole("dialog", { name: /Megrendeles attekintese|Megrendelés áttekintése/ });
  await expect(dialog).toBeVisible();

  const closeButton = page.getByRole("button", { name: /Bezaras|Bezárás/ });
  await expect(closeButton).toBeFocused();

  await page.keyboard.press("Shift+Tab");
  await expect(page.getByRole("button", { name: /Megrendeles elkuldese|Megrendelés elküldése/ })).toBeFocused();

  await page.keyboard.press("Escape");
  await expect(dialog).toBeHidden();
  await expect(reviewButton).toBeFocused();
});

test("admin delete confirmation traps focus and closes with Escape", async ({ page }) => {
  await signInAsAdmin(page);
  await mockEmptyCartApi(page);
  await page.route(`${apiBaseUrl}/uploads/**`, async (route) => {
    await route.fulfill({ status: 204 });
  });
  await page.route(
    (url) => url.origin === apiBaseUrl && url.pathname === "/api/admin/products",
    async (route) => {
      await route.fulfill({
        json: {
          items: [
            {
              id: 1,
              name: "Teszt levendula tea",
              description: "Admin teszt termek.",
              price: 1800,
              category: 2,
              image: "uploads/products/defaultimg.png",
              unit: 1,
              unitAmount: 50,
              unitAmountMeasure: 4,
              stockQuantity: 8,
              available: true,
              deliveryAvailable: true,
            },
          ],
          page: 1,
          pageSize: 12,
          totalCount: 1,
          totalPages: 1,
        },
      });
    },
  );

  await page.goto("/admin/termekek");

  const deleteButton = page.getByRole("button", { name: /Torles|Törlés/ }).first();
  await deleteButton.focus();
  await page.keyboard.press("Enter");

  const dialog = page.getByRole("dialog", { name: /Termek torlese|Termék törlése/ });
  await expect(dialog).toBeVisible();
  await expect(page.getByRole("button", { name: /Megse|Mégse/ })).toBeFocused();

  await page.keyboard.press("Shift+Tab");
  await expect(dialog.getByRole("button", { name: /Torles|Törlés/ })).toBeFocused();

  await page.keyboard.press("Escape");
  await expect(dialog).toBeHidden();
  await expect(deleteButton).toBeFocused();
});
