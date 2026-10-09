import { expect, test, type Page } from "./test";
import { apiBaseUrl, mockCheckoutApi } from "./fixtures";

const widgetUrl = "https://map.gls-hungary.com/widget/gls-dpm.js";
const pointsUrl = `${apiBaseUrl}/api/shipping/gls/points/**`;
const draftUrl = `${apiBaseUrl}/api/checkout-drafts/33333333-3333-3333-3333-333333333333`;
const point = { id: "HU-LOCKER", type: 2, name: "Verified locker", countryCode: "HU", postalCode: "8200", city: "Veszprém", address: "GLS utca 1.", acceptsCash: false, acceptsCard: true };
// Emulate only the documented custom-element boundary, including its shadow DOM.
const widget = `customElements.define('gls-dpm', class extends HTMLElement {
  connectedCallback() {
    const root = this.attachShadow({mode:'open'});
    const input = document.createElement('input'); input.setAttribute('aria-label','GLS keresés'); root.append(input);
    const button = document.createElement('button'); button.textContent = 'Teszt pont kiválasztása';
    button.onclick = () => this.dispatchEvent(new CustomEvent('change', {detail: {id: this.getAttribute('filter-type') === 'parcel-shop' ? 'HU-SHOP' : 'HU-LOCKER', name: 'Untrusted widget name'}})); root.append(button);
  }
});`;

async function setup(page: Page) {
  await mockCheckoutApi(page);
  await page.route(
    widgetUrl,
    route => route.fulfill({ contentType: "text/javascript", body: widget })
  );
  await page.route(
    pointsUrl,
    route => {
      const type = Number(new URL(route.request().url()).searchParams.get("type"));
      return route.fulfill({ json: type === 3 ? { ...point, id: "HU-SHOP", type, name: "Verified shop" } : point });
    }
  );
}
async function details(page: Page) {
  await page.getByLabel(/^Név/).fill("GLS Teszt");
  await page.getByLabel(/^Email cím/).fill("gls@example.com");
  await page.getByLabel(/^Telefonszám/).fill("+36301234567");
}
async function select(page: Page, shop = false) {
  await page.getByRole(
    "radio",
    { name: shop ? "GLS CsomagPont" : "GLS Csomagautomata", exact: true }
  ).check();
  await page.getByRole(
    "button",
    { name: "GLS térkép megnyitása" }
  ).click();
  await expect(page.locator("gls-dpm")).toHaveAttribute("filter-type", shop ? "parcel-shop" : "parcel-locker");
  await expect(page.locator("gls-dpm")).toHaveAttribute("filter-services", "delivery");
  await page.getByRole(
    "button",
    { name: "Teszt pont kiválasztása" }
  ).click();
  await expect(page.getByRole("dialog")).toHaveCount(0);
  await expect(page.getByText(
    shop ? "Verified shop" : "Verified locker",
    { exact: true }
  )).toBeVisible();
}

test("lazy picker verifies provider selection, restores its draft and sends only the point ID", async ({ page }) => {
  await setup(page);
  let loaded = 0;
  page.on(
    "request",
    request => {
      if (request.url() === widgetUrl)
        loaded++;
    }
  );
  await page.goto("/penztar");
  await details(page);
  await page.getByRole(
    "radio",
    { name: "GLS Csomagautomata", exact: true }
  ).check();
  expect(loaded).toBe(0);
  await page.getByRole(
    "button",
    { name: "Tovább a pénztárhoz" }
  ).click();
  await expect(page.getByRole("alert")).toContainText("Válassz egy GLS");
  const saved = page.waitForResponse(response => response.url() === draftUrl && response.request().method() === "PUT" && response.request().postDataJSON().fields.glsDeliveryPointId === point.id);
  await select(page);
  expect(loaded).toBe(1);
  await expect(page.getByText("Untrusted widget name")).toHaveCount(0);
  await expect(page.locator("#checkout-delivery-street")).toHaveCount(0);
  await saved;
  await page.reload();
  await expect(page.getByRole(
    "radio",
    { name: "GLS Csomagautomata", exact: true }
  )).toBeChecked();
  await expect(page.getByText(
    "Verified locker",
    { exact: true }
  )).toBeVisible();
  expect(loaded).toBe(1);
  await page.getByRole(
    "button",
    { name: "Tovább a pénztárhoz" }
  ).click();
  await page.getByLabel(/Online bankkártyás/).check();
  await page.getByRole(
    "button",
    { name: "Rendelés áttekintése" }
  ).click();
  await expect(page.getByRole("dialog")).toContainText("Verified locker");
  await page.getByLabel(/Általános Szerződési Feltételek/).check();
  await page.getByLabel(/Adatkezelési Tájékoztatót/).check();
  const sent = page.waitForRequest(request => request.url() === `${apiBaseUrl}/api/orders` && request.method() === "POST");
  await page.getByRole(
    "button",
    { name: "Tovább a fizetéshez" }
  ).click();
  const payload = (await sent).postDataJSON();
  expect(payload.glsDeliveryPointId).toBe(point.id);
  expect(payload.fulfillmentMethod).toBe(point.type);
  expect(payload.deliveryStreet).toBeUndefined();
});

test("switching locker and shop clears selection and permits a separately verified shop", async ({ page }) => {
  await setup(page);
  await page.goto("/penztar");
  await details(page);
  await select(page);
  await page.getByRole(
    "radio",
    { name: "GLS CsomagPont", exact: true }
  ).check();
  await expect(page.getByText(
    "Verified locker",
    { exact: true }
  )).toHaveCount(0);
  await page.getByRole(
    "button",
    { name: "Tovább a pénztárhoz" }
  ).click();
  await expect(page.getByRole("alert")).toContainText("Válassz egy GLS");
  await select(page, true);
  await page.getByRole(
    "radio",
    { name: "Házhoz szállítás", exact: true }
  ).check();
  await expect(page.getByRole("button", { name: /GLS térkép/ })).toHaveCount(0);
  await expect(page.getByRole(
    "group",
    { name: "Szállítási cím" }
  )).toBeVisible();
});

test(
  "failed script can retry and the mobile dialog handles shadow focus and Escape",
  async ({ page }) => {
    await setup(page);
    await page.setViewportSize({ width: 390, height: 844 });
    let fail = true;
    await page.route(widgetUrl, route => fail ? route.abort() : route.fallback());
    await page.goto("/penztar");
    await page.getByRole(
      "radio",
      { name: "GLS Csomagautomata", exact: true }
    ).check();
    const trigger = page.getByRole(
      "button",
      { name: "GLS térkép megnyitása" }
    );
    await trigger.click();
    const dialog = page.getByRole("dialog");
    await expect(dialog.getByRole("alert")).toContainText("nem tölthető be");
    fail = false;
    await page.getByRole(
      "button",
      { name: "Térkép újratöltése" }
    ).click();
    await page.getByLabel("GLS keresés").focus();
    await page.keyboard.press("Tab");
    await expect(page.getByRole(
      "button",
      { name: "Teszt pont kiválasztása" }
    )).toBeFocused();
    expect(await dialog.evaluate(element => element.scrollWidth <= element.clientWidth)).toBe(true);
    await page.keyboard.press("Escape");
    await expect(dialog).toHaveCount(0);
    await expect(trigger).toBeFocused();
  }
);

test(
  "directory outage and invalid point keep the dialog open and allow retry",
  async ({ page }) => {
    await setup(page);
    let status = 503;
    await page.route(
      pointsUrl,
      route => status === 200 ? route.fallback() : route.fulfill({ status, json: { detail: "Válassz új pontot vagy próbáld újra." } })
    );
    await page.goto("/penztar");
    await page.getByRole(
      "radio",
      { name: "GLS Csomagautomata", exact: true }
    ).check();
    await page.getByRole(
      "button",
      { name: "GLS térkép megnyitása" }
    ).click();
    for (const failure of [503, 409]) {
      status = failure;
      await page.getByRole(
        "button",
        { name: "Teszt pont kiválasztása" }
      ).click();
      await expect(page.getByRole("dialog").getByRole("alert")).toContainText("Válassz új pontot");
    }
    status = 200;
    await page.getByRole(
      "button",
      { name: "Teszt pont kiválasztása" }
    ).click();
    await expect(page.getByRole("dialog")).toHaveCount(0);
    await expect(page.getByText(
      "Verified locker",
      { exact: true }
    )).toBeVisible();
  }
);

test("obsolete restored selection keeps customer input but blocks continuing", async ({ page }) => {
  await setup(page);
  await page.goto("/penztar");
  await details(page);
  const saved = page.waitForResponse(response => response.url() === draftUrl && response.request().method() === "PUT" && response.request().postDataJSON().fields.glsDeliveryPointId === point.id);
  await select(page);
  await saved;
  let failed = true;
  await page.route(
    pointsUrl,
    route => failed ? route.fulfill({ status: 409, json: { detail: "Ez a pont már nem elérhető." } }) : route.fallback()
  );
  await page.reload();
  await expect(page.getByRole("alert")).toContainText("már nem elérhető");
  await expect(page.getByLabel(/^Név/)).toHaveValue("GLS Teszt");
  await page.getByRole(
    "button",
    { name: "Tovább a pénztárhoz" }
  ).click();
  await expect(page.getByRole(
    "button",
    { name: "Tovább a pénztárhoz" }
  )).toBeVisible();
  failed = false;
  await page.getByRole(
    "button",
    { name: "Átvételi pont újraellenőrzése" }
  ).click();
  await expect(page.getByText(
    "Verified locker",
    { exact: true }
  )).toBeVisible();
});

test(
  "closing a pending selection prevents its late response from replacing another delivery mode",
  async ({ page }) => {
    await setup(page);
    let release!: () => void;
    const gate = new Promise<void>(resolve => { release = resolve; });
    await page.route(
      pointsUrl,
      async route => { await gate; await route.fallback(); }
    );
    await page.goto("/penztar");
    await page.getByRole(
      "radio",
      { name: "GLS Csomagautomata", exact: true }
    ).check();
    await page.getByRole(
      "button",
      { name: "GLS térkép megnyitása" }
    ).click();
    await page.getByRole(
      "button",
      { name: "Teszt pont kiválasztása" }
    ).click();
    await expect(page.getByRole("dialog").getByRole("status")).toContainText("ellenőrzése");
    await page.keyboard.press("Escape");
    await page.getByRole(
      "radio",
      { name: "Személyes átvétel", exact: true }
    ).check();
    release();
    await expect(page.getByText(
      "Verified locker",
      { exact: true }
    )).toHaveCount(0);
    await expect(page.getByRole(
      "radio",
      { name: "Személyes átvétel", exact: true }
    )).toBeChecked();
  }
);
