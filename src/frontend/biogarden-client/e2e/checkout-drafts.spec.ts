import { expect, test, type Page } from "@playwright/test";
import { apiBaseUrl, mockCheckoutApi, signInAsCustomer, successfulOrder } from "./fixtures";

const draftUrl = `${apiBaseUrl}/api/checkout-drafts/33333333-3333-3333-3333-333333333333`;

async function details(page: Page) {
  await page.getByLabel(/^Név/).fill("Piszkozat Teszt");
  await page.getByLabel(/^Email cím/).fill("draft@example.com");
  await page.getByLabel(/^Telefonszám/).fill("+36301234567");
}

async function submitOrder(page: Page, online = false) {
  await details(page);
  await page.getByRole("button", { name: "Tovább a pénztárhoz" }).click();
  await page.getByLabel(online ? /Online.*bankkárty|Bankkártyás.*online/ : /Fizetés átvételkor/).check();
  await page.getByRole("button", { name: "Rendelés áttekintése" }).click();
  await page.getByLabel(/Általános Szerződési Feltételek/).check();
  await page.getByLabel(/Adatkezelési Tájékoztatót/).check();
  await page.getByRole("button", { name: online ? "Tovább a fizetéshez" : "Megrendelés elküldése" }).click();
}

test("refresh restores server draft without saving personal fields in browser storage", async ({ page }) => {
  await mockCheckoutApi(page);
  await page.addInitScript(() => sessionStorage.setItem("biogarden_checkout_draft", JSON.stringify({ customerName: "Legacy private data" })));
  await page.goto("/penztar");
  const saved = page.waitForResponse(response => response.url() === draftUrl && response.request().method() === "PUT");
  await details(page);
  expect((await saved).ok()).toBe(true);
  await page.reload();
  await expect(page.getByLabel(/^Név/)).toHaveValue("Piszkozat Teszt");
  await expect(page.getByLabel(/^Email cím/)).toHaveValue("draft@example.com");
  const storage = await page.evaluate(() => JSON.stringify({ ...localStorage, ...sessionStorage }));
  expect(storage).not.toContain("draft@example.com");
  expect(storage).not.toContain("Legacy private data");
  expect(storage).not.toContain("biogarden_checkout_draft");
});

test("discard removes the server draft and refresh does not restore it", async ({ page }) => {
  await mockCheckoutApi(page);
  await page.goto("/penztar");
  const saved = page.waitForResponse(response => response.url() === draftUrl && response.request().method() === "PUT");
  await details(page);
  await saved;
  await page.getByRole("button", { name: "Piszkozat törlése" }).click();
  await expect(page.getByLabel(/^Név/)).toHaveValue("");
  await page.reload();
  await expect(page.getByLabel(/^Név/)).toHaveValue("");
});

test("restored customer edits are not presented as current profile values", async ({ page }) => {
  await signInAsCustomer(page);
  await mockCheckoutApi(page);
  await page.route(`${apiBaseUrl}/api/profile`, route => route.fulfill({ json: {
    firstName: "Elek", lastName: "Fiók", email: "profile@example.com", phoneNumber: "+36301234567", customerType: 0, printedInvoiceRequested: false,
  } }));
  await page.route(`${apiBaseUrl}/api/profile/addresses`, route => route.fulfill({ json: { billingAddress: null, shippingAddress: null } }));
  await page.goto("/penztar");
  await expect(page.getByLabel(/^Név/)).toHaveValue("Fiók Elek");
  const saved = page.waitForResponse(response => response.url() === draftUrl && response.request().method() === "PUT"
    && response.request().postDataJSON().fields.customerName === "Piszkozat Teszt");
  await details(page);
  await saved;
  await page.reload();
  await expect(page.getByLabel(/^Név/)).toHaveValue("Piszkozat Teszt");
  await expect(page.getByLabel("Fiókban mentett személyes adatok betöltése")).not.toBeChecked();
  await page.getByRole("button", { name: "Piszkozat törlése" }).click();
  await expect(page.getByLabel(/^Név/)).toHaveValue("");
  await expect(page.getByLabel("Fiókban mentett személyes adatok betöltése")).not.toBeChecked();
});

test("stale save preserves local edits until the customer loads the saved version", async ({ page }) => {
  await mockCheckoutApi(page);
  let writes = 0;
  await page.route(draftUrl, async route => {
    if (route.request().method() !== "PUT") return route.fallback();
    writes++;
    await route.fulfill({ status: 409, json: { detail: "Másik lap módosította a piszkozatot." } });
  });
  await page.goto("/penztar");
  await details(page);
  await expect(page.getByRole("alert")).toContainText("Másik lap módosította");
  await expect(page.getByLabel(/^Név/)).toHaveValue("Piszkozat Teszt");
  await page.getByLabel(/^Név/).fill("Megőrzött módosítás");
  await expect(page.getByLabel(/^Név/)).toHaveValue("Megőrzött módosítás");
  expect(writes).toBe(1);
  await page.getByRole("button", { name: /Mentett változat betöltése/ }).click();
  await expect(page.getByLabel(/^Név/)).toHaveValue("");
});

test("a failed save can be retried without reentering personal details", async ({ page }) => {
  await mockCheckoutApi(page);
  let fail = true;
  await page.route(draftUrl, async route => {
    if (route.request().method() === "PUT" && fail) {
      await route.fulfill({ status: 503, json: { detail: "Mentés átmenetileg nem elérhető." } });
    } else await route.fallback();
  });
  await page.goto("/penztar");
  await details(page);
  await expect(page.getByRole("alert")).toContainText("Mentés átmenetileg");
  fail = false;
  await page.getByRole("button", { name: "Mentés újrapróbálása" }).click();
  await expect(page.getByRole("alert")).toHaveCount(0);
  await page.reload();
  await expect(page.getByLabel(/^Név/)).toHaveValue("Piszkozat Teszt");
});

test("malformed restored fields are rejected and loading can be retried", async ({ page }) => {
  await mockCheckoutApi(page);
  let malformed = true;
  await page.route(draftUrl, route => malformed
    ? route.fulfill({ json: { version: "bad", fields: { customerName: 123 } } })
    : route.fallback());
  await page.goto("/penztar");
  await expect(page.getByRole("alert")).toContainText("sérültek");
  malformed = false;
  await page.getByRole("button", { name: "Piszkozat betöltése újra" }).click();
  await expect(page.getByLabel(/^Név/)).toHaveValue("");
});

test("payment startup failure and refresh retry the existing order", async ({ page }) => {
  let orders = 0;
  let payments = 0;
  await mockCheckoutApi(page, true, () => orders++);
  await page.route(`${apiBaseUrl}/api/orders/${successfulOrder.id}/sumup-checkout`, route => {
    payments++;
    return route.fulfill({ status: 503, json: { detail: "Payment temporarily unavailable" } });
  });
  await page.goto("/penztar");
  await submitOrder(page, true);
  await expect(page.getByRole("heading", { name: "A fizetés megszakadt" })).toBeVisible();
  await expect(page).toHaveURL(new RegExp(`orderId=${successfulOrder.id}`));
  expect(orders).toBe(1);
  await page.reload();
  await page.getByRole("button", { name: "Fizetés újrapróbálása" }).click();
  await expect.poll(() => payments).toBe(2);
  expect(orders).toBe(1);
});

test("cart refresh failure cannot turn a created pickup order into a checkout error", async ({ page }) => {
  let ordered = false;
  await mockCheckoutApi(page, false, () => { ordered = true; });
  await page.route(`${apiBaseUrl}/api/cart`, route => ordered
    ? route.fulfill({ status: 503 }) : route.fallback());
  await page.goto("/penztar");
  await submitOrder(page);
  await expect(page).toHaveURL(/\/rendeles-sikeres$/);
  await expect(page.getByText(successfulOrder.orderNumber)).toBeVisible();
});
