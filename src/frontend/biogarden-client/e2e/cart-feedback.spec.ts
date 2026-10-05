import { expect, test, type Route } from "@playwright/test";
import { apiBaseUrl, mockPublicShopApi } from "./fixtures";

const cart = (count: number) => ({
  id: "cart-feedback", items: [], totalItemCount: count, totalPrice: count * 1800,
  expiresAtUtc: "2099-01-01T00:00:00Z", isCheckedOut: false,
});

for (const firstFails of [false, true]) {
  test(`each card stays disabled until its own queued addition settles (first fails: ${firstFails})`, async ({ page }) => {
    await mockPublicShopApi(page);
    const writes: Route[] = [];
    await page.route(`${apiBaseUrl}/api/cart/items`, route => { writes.push(route); });
    await page.goto("/termekek");
    const first = page.getByRole("article").filter({ has: page.getByRole("heading", { name: "Teszt levendula tea", exact: true }) });
    const second = page.getByRole("article").filter({ has: page.getByRole("heading", { name: "Teszt kakukkfu tea", exact: true }) });
    await first.getByRole("button").click();
    await second.getByRole("button").click();
    await expect(first.getByRole("button")).toBeDisabled();
    await expect(second.getByRole("button")).toBeDisabled();
    await expect.poll(() => writes.length).toBe(1);
    await first.getByRole("button").evaluate((button: HTMLButtonElement) => button.click());
    await writes[0].fulfill(firstFails
      ? { status: 409, json: { detail: "Nincs elegendő készlet." } }
      : { json: cart(1) });
    await expect.poll(() => writes.length).toBe(2);
    await expect(first.getByRole("button")).toBeEnabled();
    await expect(second.getByRole("button")).toBeDisabled();
    expect(writes.map(route => route.request().postDataJSON().productId)).toEqual([1, 2]);
    await writes[1].fulfill({ json: cart(firstFails ? 1 : 2) });
    await expect(second.getByRole("button")).toBeEnabled();
    await expect(first.getByRole("alert")).toHaveCount(firstFails ? 1 : 0);
    await expect(second.getByRole("alert")).toHaveCount(0);
    expect(writes).toHaveLength(2);
  });
}

test("a product card reports a failed addition in place and clears the error on retry", async ({ page }) => {
  await mockPublicShopApi(page);
  const writes: Route[] = [];
  await page.route(`${apiBaseUrl}/api/cart/items`, route => { writes.push(route); });
  await page.goto("/termekek");
  const card = page.getByRole("article").filter({ has: page.getByRole("heading", { name: "Teszt levendula tea", exact: true }) });
  await card.getByRole("button", { name: "Kosárba", exact: true }).click();
  await expect.poll(() => writes.length).toBe(1);
  await writes[0].fulfill({ status: 409, json: { detail: "Nincs elegendő készlet." } });
  await expect(card.getByRole("alert")).toHaveText("Nincs elegendő készlet.");
  await card.getByRole("button", { name: "Kosárba", exact: true }).click();
  await expect(card.getByRole("alert")).toHaveCount(0);
  await expect(card.getByRole("button", { name: "Hozzáadás...", exact: true })).toBeDisabled();
  await expect.poll(() => writes.length).toBe(2);
  await writes[1].fulfill({ json: cart(1) });
  await expect(card.getByRole("button", { name: "Kosárba", exact: true })).toBeEnabled();
  await expect(card.getByRole("alert")).toHaveCount(0);
});

test("a successful addition survives stock refresh failure and retry does not add again", async ({ page }) => {
  await mockPublicShopApi(page);
  let writes = 0;
  let reads = 0;
  let retry: Route | undefined;
  await page.route(`${apiBaseUrl}/api/cart/items`, route => { writes++; return route.fulfill({ json: cart(1) }); });
  await page.route(`${apiBaseUrl}/api/cart/products/1/availability`, route => {
    reads++;
    if (reads === 1) return route.fulfill({ json: { availableQuantity: 8 } });
    if (reads === 2) return route.fulfill({ status: 503, json: { detail: "Átmeneti hiba." } });
    retry = route;
  });
  await page.goto("/termekek/1");
  const add = page.getByRole("button", { name: "Kosárba teszem", exact: true });
  await add.click();
  await expect(page.getByRole("status")).toContainText("bekerült a kosárba");
  await expect(page.getByRole("alert")).toContainText("a fennmaradó készletet nem sikerült frissíteni");
  await expect(add).toBeDisabled();
  await page.getByRole("button", { name: "Készlet újrapróbálása" }).click();
  await expect.poll(() => retry).toBeTruthy();
  await expect(page.getByRole("button", { name: "Készlet frissítése..." })).toBeDisabled();
  expect(writes).toBe(1);
  await retry!.fulfill({ json: { availableQuantity: 7 } });
  await expect(add).toBeEnabled();
  await expect(page.getByRole("alert")).toHaveCount(0);
  await expect(page.getByRole("status")).toContainText("bekerült a kosárba");
  expect(writes).toBe(1);
});

test("a rejected addition does not claim success or refresh remaining stock", async ({ page }) => {
  await mockPublicShopApi(page);
  let reads = 0;
  await page.route(`${apiBaseUrl}/api/cart/products/1/availability`, route => {
    reads++; return route.fulfill({ json: { availableQuantity: 8 } });
  });
  await page.route(`${apiBaseUrl}/api/cart/items`, route => route.fulfill({ status: 409, json: { detail: "Nincs elegendő készlet." } }));
  await page.goto("/termekek/1");
  const add = page.getByRole("button", { name: "Kosárba teszem", exact: true });
  await add.click();
  await expect(page.getByRole("status")).toHaveText("Nincs elegendő készlet.");
  await expect(add).toBeEnabled();
  expect(reads).toBe(1);
});
