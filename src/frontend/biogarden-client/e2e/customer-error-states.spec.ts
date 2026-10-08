import { expect, test } from "./test";
import {
  apiBaseUrl,
  expectNoUnhandledApiCall,
  mockCheckoutValidationIssueApi,
  mockEmptyCartApi,
} from "./fixtures";

test.beforeEach(async ({ page }) => {
  await page.route(`${apiBaseUrl}/api/**`, expectNoUnhandledApiCall);
});

test("empty cart shows a clear recovery path", async ({ page }) => {
  await mockEmptyCartApi(page);

  await page.goto("/kosar");

  await expect(page.getByRole("heading", { name: /A kosarad.*(?:ures|üres)/i })).toBeVisible();
  await expect(page.getByRole("link", { name: /Termekek megtekintese|Termékek megtekintése/ })).toBeVisible();
});

test("checkout keeps the customer on the form when cart validation fails", async ({ page }) => {
  const issue = "A teszt termék közben elfogyott.";
  await mockCheckoutValidationIssueApi(page, issue);

  await page.goto("/penztar");
  await page.getByLabel(/Nev|Név/).fill("Teszt Elek");
  await page.getByLabel(/Email cim|Email cím/).fill("teszt@example.com");
  await page.getByLabel(/Telefonszam|Telefonszám/).fill("+36301234567");

  await page.getByRole("button", { name: /Tovabb a penztarhoz|Tovább a pénztárhoz/ }).click();

  await expect(page).toHaveURL(/\/penztar$/);
  await expect(page.getByRole("alert")).toContainText(issue);
  await expect(page.getByRole("heading", { name: /Penztar|Pénztár/ })).toBeVisible();
});

test("password recovery screens expose invalid and failed request states", async ({ page }) => {
  await mockEmptyCartApi(page);
  await page.route(`${apiBaseUrl}/api/auth/forgot-password`, async (route) => {
    await route.fulfill({
      status: 503,
      json: { detail: "A levélküldés átmenetileg nem elérhető." },
    });
  });

  await page.goto("/jelszo-visszaallitas");
  await expect(page.getByRole("alert")).toContainText(/link.*(?:ervenytelen|érvénytelen)/i);

  await page.goto("/elfelejtett-jelszo");
  await page.getByLabel(/Email cim|Email cím/).fill("teszt@example.com");
  await page.getByRole("button", { name: /Jelszo-visszaallito link kuldese|Jelszó-visszaállító link küldése/ }).click();

  await expect(page.getByRole("alert")).toContainText("A levélküldés átmenetileg nem elérhető.");
});

test("public order request rejects a nonexistent order without showing success", async ({ page }) => {
  await mockEmptyCartApi(page);
  await page.route(`${apiBaseUrl}/api/orders/withdrawal-requests`, async (route) => {
    await route.fulfill({
      status: 404,
      json: { detail: "Nem találtunk ilyen rendelést ezzel az email címmel." },
    });
  });
  await page.goto("/rendelessel-kapcsolatos-keres");
  const orderNumber = page.getByLabel("Rendelés azonosítója", { exact: false });
  const reason = page.getByLabel("Kérés leírása", { exact: false });
  await orderNumber.fill("BK-19000101-00000000");
  await page.getByLabel(/email cím/i).fill("teszt@example.com");
  await reason.fill("Szeretnék elállási kérelmet rögzíteni.");
  await page.getByRole("button", { name: "Kérés elküldése" }).click();
  await expect(orderNumber).toBeFocused();
  await expect(orderNumber).toHaveAttribute("aria-invalid", "true");
  await expect(page.getByRole("alert").first()).toContainText("Nem találtunk ilyen rendelést");
  await expect(page.getByRole("status")).toHaveCount(0);
  await expect(reason).toHaveValue("Szeretnék elállási kérelmet rögzíteni.");
});

test("public order request form confirms successful submission", async ({ page }) => {
  await mockEmptyCartApi(page);
  await page.route(`${apiBaseUrl}/api/orders/withdrawal-requests`, async (route) => {
    expect(route.request().method()).toBe("POST");
    expect(await route.request().postDataJSON()).toMatchObject({
      orderNumber: "BK-20260722-A1B2C3D4",
      customerEmail: "teszt@example.com",
      reason: "Szeretnék elállási kérelmet rögzíteni.",
    });

    await route.fulfill({
      json: {
        id: 1,
        orderId: "11111111-1111-1111-1111-111111111111",
        customerName: "Teszt Elek",
        customerEmail: "teszt@example.com",
        reason: "Szeretnék elállási kérelmet rögzíteni.",
        status: 1,
        requestedAtUtc: "2026-07-22T10:00:00Z",
        handledAtUtc: null,
      },
    });
  });

  await page.goto("/rendelessel-kapcsolatos-keres");
  await page.getByLabel(/Rendeles azonositója|Rendelés azonosítója/).fill("BK-20260722-A1B2C3D4");
  await page.getByLabel(/email cim|email cím/i).fill("teszt@example.com");
  await page.getByLabel(/Keres leirasa|Kérés leírása/).fill("Szeretnék elállási kérelmet rögzíteni.");
  await page.getByRole("button", { name: /Keres elkuldese|Kérés elküldése/ }).click();

  await expect(page.getByRole("status")).toContainText("A kérésedet rögzítettük");
  await page.getByLabel("Rendelés azonosítója", { exact: false }).fill("BK-20260730-C4D1Afg2");
  await expect(page.getByRole("status")).toHaveCount(0);
  await page.getByRole("button", { name: "Kérés elküldése" }).click();
  await expect(page.getByRole("alert")).toContainText("Ellenőrizd a rendelés azonosítóját");
});
