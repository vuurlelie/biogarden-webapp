import { expect, test } from "@playwright/test";
import {
  apiBaseUrl,
  expectNoUnhandledApiCall,
  mockEmptyCartApi,
  signInAsAdmin,
} from "./fixtures";

test.beforeEach(async ({ page }) => {
  await page.route(`${apiBaseUrl}/api/**`, expectNoUnhandledApiCall);
  await signInAsAdmin(page);
  await mockEmptyCartApi(page);
});

test("admin orders page shows load failures", async ({ page }) => {
  await page.route(`${apiBaseUrl}/api/admin/orders`, async (route) => {
    await route.fulfill({
      status: 503,
      json: { detail: "A rendelések most nem tölthetők be." },
    });
  });

  await page.goto("/admin/rendelesek");

  await expect(page.getByRole("heading", { name: /Rendelesek|Rendelések/ })).toBeVisible();
  await expect(page.getByRole("alert")).toContainText("A rendelések most nem tölthetők be.");
});

test("admin products page can retry after a load failure", async ({ page }) => {
  let attempts = 0;
  await page.route(`${apiBaseUrl}/api/admin/products`, async (route) => {
    attempts += 1;
    if (attempts <= 2) {
      await route.fulfill({
        status: 503,
        json: { detail: "A terméklista átmenetileg nem elérhető." },
      });
      return;
    }

    await route.fulfill({ json: [] });
  });

  await page.goto("/admin/termekek");

  await expect(page.getByRole("alert")).toContainText("A terméklista átmenetileg nem elérhető.");
  await page.getByRole("button", { name: /Ujraprobalas|Újrapróbálás/ }).click();
  await expect(page.getByRole("heading", { name: /Nincs talalat|Nincs találat/ })).toBeVisible();
});

test("admin accounts page shows the empty account state", async ({ page }) => {
  await page.route(`${apiBaseUrl}/api/admin/users`, async (route) => {
    await route.fulfill({ json: [] });
  });

  await page.goto("/admin/fiok");

  await expect(page.getByRole("heading", { name: /Felhasznalok|Felhasználók/ })).toBeVisible();
  await expect(page.getByText(/Nincs talalat|Nincs találat/)).toBeVisible();
});
