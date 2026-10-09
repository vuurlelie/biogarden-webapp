import { expect, test, type Page } from "./test";
import { apiBaseUrl, checkoutQuote, mockCheckoutApi, mockSumUpReturnStatusApi, successfulOrder } from "./fixtures";

async function paymentStep(page: Page) {
  await page.goto("/penztar");
  await page.getByLabel(/^Név/).fill("Árazás Teszt");
  await page.getByLabel(/^Email cím/).fill("pricing@example.com");
  await page.getByLabel(/^Telefonszám/).fill("+36301234567");
  await page.getByRole(
    "button",
    { name: "Tovább a pénztárhoz" }
  ).click();
}
async function accept(page: Page) {
  await page.getByLabel(/Általános Szerződési Feltételek/).check();
  await page.getByLabel(/Adatkezelési Tájékoztatót/).check();
}

test("review, submission and success use the server quote rather than cart arithmetic", async ({ page }) => {
  await mockCheckoutApi(page);
  const quote = { ...checkoutQuote({ fulfillmentMethod: 0, paymentMethod: 1 }), subtotal: 2317, totalPrice: 2317, version: "B".repeat(64),
    items: [{ ...successfulOrder.items[0], unitPrice: 1417, lineTotal: 1417 },
      { ...successfulOrder.items[0], productId: 2, productName: "Másik lapon hozzáadott termék", unitPrice: 900, lineTotal: 900 }] };
  await page.route(
    `${apiBaseUrl}/api/orders/quote`,
    route => route.fulfill({ json: quote })
  );
  let submitted: Record<string, unknown> | undefined;
  await page.route(
    `${apiBaseUrl}/api/orders`,
    route => {
      submitted = route.request().postDataJSON();
      return route.fulfill({ status: 201, json: { ...successfulOrder, ...quote, id: successfulOrder.id } });
    }
  );
  await paymentStep(page);
  await page.getByLabel("Fizetés átvételkor").check();
  await page.getByRole(
    "button",
    { name: "Rendelés áttekintése" }
  ).click();
  const dialog = page.getByRole("dialog");
  await expect(dialog).toContainText("Másik lapon hozzáadott termék");
  await expect(dialog.getByRole("definition").last()).toHaveText(/2317/);
  await accept(page);
  await page.getByRole(
    "button",
    { name: "Megrendelés elküldése" }
  ).click();
  await expect(page.getByRole(
    "region",
    { name: "Mentett rendelési összesítés" }
  )).toContainText(/2317/);
  expect(submitted?.quoteVersion).toBe(quote.version);
  expect(submitted?.paymentMethod).toBe(1);
  expect(submitted).not.toHaveProperty("totalPrice");
});

test("stale submission requires a new review and fresh consent before retry", async ({ page }) => {
  await mockCheckoutApi(page);
  let changed = false;
  await page.route(
    `${apiBaseUrl}/api/orders/quote`,
    route => route.fulfill({ json: {
      ...checkoutQuote(route.request().postDataJSON()), subtotal: changed ? 2400 : 1800, totalPrice: changed ? 2400 : 1800, version: (changed ? "B" : "A").repeat(64),
    } })
  );
  await page.route(
    `${apiBaseUrl}/api/orders`,
    route => {
      changed = true;
      return route.fulfill({ status: 409, json: { detail: "Az árak megváltoztak. Tekintsd át újra." } });
    }
  );
  await paymentStep(page);
  await page.getByLabel("Fizetés átvételkor").check();
  await page.getByRole(
    "button",
    { name: "Rendelés áttekintése" }
  ).click();
  await accept(page);
  await page.getByRole(
    "button",
    { name: "Megrendelés elküldése" }
  ).click();
  await expect(page.getByRole("dialog")).toHaveCount(0);
  await page.getByRole(
    "button",
    { name: "Rendelés áttekintése" }
  ).click();
  await expect(page.getByRole("dialog")).toContainText(/2400/);
  await expect(page.getByLabel(/Általános Szerződési Feltételek/)).not.toBeChecked();
  await expect(page.getByLabel(/Adatkezelési Tájékoztatót/)).not.toBeChecked();
});

test("a late quote for another payment method cannot replace the current quote", async ({ page }) => {
  await mockCheckoutApi(page);
  let release!: () => void;
  const gate = new Promise<void>(resolve => { release = resolve; });
  await page.route(
    `${apiBaseUrl}/api/orders/quote`,
    async route => {
      const request = route.request().postDataJSON();
      if (request.paymentMethod === 0)
        await gate;
      await route.fulfill({ json: { ...checkoutQuote(request), totalPrice: request.paymentMethod === 0 ? 9999 : 1800 } });
    }
  );
  await paymentStep(page);
  const pending = page.waitForRequest(request => request.url().endsWith("/quote"));
  await page.getByLabel("Online bankkártyás fizetés").check();
  await pending;
  await page.getByLabel("Fizetés átvételkor").check();
  await expect(page.getByRole(
    "button",
    { name: "Rendelés áttekintése" }
  )).toBeEnabled();
  release();
  await page.getByRole(
    "button",
    { name: "Rendelés áttekintése" }
  ).click();
  await expect(page.getByRole("dialog")).not.toContainText("9999");
  await expect(page.getByRole("dialog").getByRole("definition").last()).toHaveText(/1800/);
});

test(
  "quote failure blocks review and retry preserves form details",
  async ({ page }) => {
    await mockCheckoutApi(page);
    let failed = true;
    await page.route(
      `${apiBaseUrl}/api/orders/quote`,
      route => failed
        ? route.fulfill({ status: 503, json: { detail: "Árazás átmenetileg nem elérhető." } }) : route.fallback()
    );
    await paymentStep(page);
    await page.getByLabel("Fizetés átvételkor").check();
    await expect(page.getByRole("alert")).toContainText("Árazás átmenetileg");
    await expect(page.getByRole(
      "button",
      { name: "Rendelés áttekintése" }
    )).toBeDisabled();
    failed = false;
    await page.getByRole(
      "button",
      { name: "Árak újraellenőrzése" }
    ).click();
    await page.getByRole(
      "button",
      { name: "Rendelés áttekintése" }
    ).click();
    await expect(page.getByRole("dialog")).toContainText("Árazás Teszt");
  }
);

test(
  "only server-enabled delivery and payment methods are offered",
  async ({ page }) => {
    await mockCheckoutApi(page);
    await page.route(
      `${apiBaseUrl}/api/orders/checkout-methods`,
      route => route.fulfill({ json: [{ fulfillmentMethod: 0, deliveryFee: 0, paymentMethods: [1] }] })
    );
    await paymentStep(page);
    await expect(page.getByRole(
      "radio",
      { name: "GLS CsomagPont" }
    )).toHaveCount(0);
    await expect(page.getByLabel("Online bankkártyás fizetés")).toHaveCount(0);
    await expect(page.getByLabel("Fizetés átvételkor")).toBeVisible();
  }
);

test("payment return shows owned saved totals and destination, and handles legacy fields honestly", async ({ page }) => {
  await mockSumUpReturnStatusApi(page, 2);
  let legacy = false;
  await page.route(
    `${apiBaseUrl}/api/orders/${successfulOrder.id}/receipt`,
    route => route.fulfill({ json: {
      ...successfulOrder, totalPrice: 7990, subtotal: legacy ? null : 5000, deliveryFee: legacy ? null : 2390, paymentFee: legacy ? null : 600,
      paymentMethod: legacy ? null : 0, fulfillmentMethod: 3, glsDeliveryPointId: "HU-SHOP", glsDeliveryPointName: "Saved GLS shop", glsDeliveryPointAddress: "Saved street 8",
    } })
  );
  const url = `/rendeles-sikeres?sumup_checkout_reference=${successfulOrder.id.replaceAll("-", "")}&orderId=${successfulOrder.id}`;
  await page.goto(url);
  const summary = page.getByRole(
    "region",
    { name: "Mentett rendelési összesítés" }
  );
  await expect(summary).toContainText("Saved GLS shop");
  await expect(summary).toContainText("Saved street 8");
  await expect(summary).toContainText(/7990/);
  await expect(summary).toContainText(/2390/);
  legacy = true;
  await page.reload();
  await expect(summary).toContainText("díjbontása nem áll rendelkezésre");
  await expect(summary).toContainText("nincs rögzítve");
  await expect(summary).not.toContainText(/2390/);
});
