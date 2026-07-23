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

test("login validation focuses the first invalid field", async ({ page }) => {
  await mockEmptyCartApi(page);

  await page.goto("/bejelentkezes");
  await page.getByRole("button", { name: /^Bejelentkezes$|^Bejelentkezés$/ }).click();

  const emailInput = page.getByLabel(/Email cim|Email cím/);
  await expect(page.getByRole("alert")).toContainText(/ervenyes email|érvényes email/);
  await expect(emailInput).toBeFocused();
  await expect(emailInput).toHaveAttribute("aria-invalid", "true");
});

test("public request validation focuses the field that needs more detail", async ({ page }) => {
  await mockEmptyCartApi(page);

  await page.goto("/rendelessel-kapcsolatos-keres");
  await page.getByLabel(/Rendeles azonositoja|Rendelés azonosítója/).fill("BK-2026-0001");
  await page.getByLabel(/email cim|email cím/i).fill("teszt@example.com");
  await page.getByLabel(/Keres leirasa|Kérés leírása/).fill("rovid");
  await page.getByRole("button", { name: /Keres elkuldese|Kérés elküldése/ }).click();

  const reasonInput = page.getByLabel(/Keres leirasa|Kérés leírása/);
  await expect(page.getByRole("alert")).toContainText("10 karakter");
  await expect(reasonInput).toBeFocused();
  await expect(reasonInput).toHaveAttribute("aria-invalid", "true");
});
