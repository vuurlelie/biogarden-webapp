import { expect, test, type Page, type Route } from "@playwright/test";
import { apiBaseUrl, mockPublicShopApi } from "./fixtures";

const cart = (count: number) => ({
  id: "cart-session-test", items: [], totalItemCount: count, totalPrice: count * 900,
  expiresAtUtc: "2099-01-01T00:00:00Z", isCheckedOut: false,
});

async function harness(page: Page) {
  // Retain Vite's React refresh preamble while replacing only the application entry.
  await page.route("**/cart-test", async route => {
    const response = await route.fetch();
    const html = (await response.text()).replace('/src/main.tsx', '/e2e/cart-harness.tsx');
    await route.fulfill({ response, body: html });
  });
  await page.goto("/cart-test");
  await expect(page.getByRole("button", { name: "Add first" })).toBeVisible();
}

test("the real product page waits for the first guest cart before adding", async ({ page }) => {
  await mockPublicShopApi(page);
  let initial: Route | undefined;
  let gets = 0;
  const writes: Route[] = [];
  await page.route(`${apiBaseUrl}/api/cart`, route => { gets++; initial = route; });
  await page.route(`${apiBaseUrl}/api/cart/items`, route => { writes.push(route); });
  await page.goto("/termekek/1");
  await expect.poll(() => initial).toBeTruthy();
  await page.getByRole("button", { name: /Kosárba teszem/ }).click();
  await expect(page.getByRole("button", { name: "Kosárba helyezés...", exact: true })).toBeDisabled();
  expect(writes).toHaveLength(0);
  expect(gets).toBe(1);
  await initial!.fulfill({ json: cart(0) });
  await expect.poll(() => writes.length).toBe(1);
  await writes[0].fulfill({ json: cart(1) });
  await expect(page.getByRole("status")).toContainText(/bekerült a kosárba/);
});

test("queued additions run in order and a refresh cannot overwrite their result", async ({ page }) => {
  let initial: Route | undefined;
  const order: string[] = [];
  const writes: Route[] = [];
  await page.route(`${apiBaseUrl}/api/cart`, async route => {
    order.push("read");
    if (!initial) initial = route;
    else await route.fulfill({ json: cart(2) });
  });
  await page.route(`${apiBaseUrl}/api/cart/items`, route => {
    order.push(`add-${route.request().postDataJSON().productId}`);
    writes.push(route);
  });
  await harness(page);
  await expect.poll(() => initial).toBeTruthy();
  await page.getByRole("button", { name: "Add first" }).click();
  await page.getByRole("button", { name: "Add second" }).click();
  await page.getByRole("button", { name: "Refresh" }).click();
  expect(order).toEqual(["read"]);
  await initial!.fulfill({ json: cart(0) });
  await expect.poll(() => writes.length).toBe(1);
  await writes[0].fulfill({ json: cart(1) });
  await expect.poll(() => writes.length).toBe(2);
  await writes[1].fulfill({ json: cart(2) });
  await expect(page.getByTestId("count")).toHaveText("2");
  await expect.poll(() => order).toEqual(["read", "add-1", "add-2", "read"]);
});

test("an older refresh finishes before a newer addition is sent", async ({ page }) => {
  let reads = 0;
  let refresh: Route | undefined;
  let writes = 0;
  await page.route(`${apiBaseUrl}/api/cart`, route => {
    if (++reads === 1) return route.fulfill({ json: cart(1) });
    refresh = route;
  });
  await page.route(`${apiBaseUrl}/api/cart/items`, route => { writes++; return route.fulfill({ json: cart(2) }); });
  await harness(page);
  await expect(page.getByTestId("count")).toHaveText("1");
  await page.getByRole("button", { name: "Refresh" }).click();
  await expect.poll(() => refresh).toBeTruthy();
  await page.getByRole("button", { name: "Add first" }).click();
  expect(writes).toBe(0);
  await refresh!.fulfill({ json: cart(1) });
  await expect(page.getByTestId("count")).toHaveText("2");
  expect(writes).toBe(1);
});

test("failed initialization blocks writes and can be retried", async ({ page }) => {
  let available = false;
  let writes = 0;
  await page.route(`${apiBaseUrl}/api/cart`, route => route.fulfill(available
    ? { json: cart(0) }
    : { status: 503, json: { detail: "Cart initialization unavailable" } }));
  await page.route(`${apiBaseUrl}/api/cart/items`, route => { writes++; return route.fulfill({ json: cart(1) }); });
  await harness(page);
  await expect(page.getByTestId("error")).toContainText("Cart initialization unavailable");
  await page.getByRole("button", { name: "Add first" }).click();
  await expect(page.getByTestId("loading")).toHaveText("false");
  await expect(page.getByTestId("error")).toContainText("Cart initialization unavailable");
  expect(writes).toBe(0);
  available = true;
  await page.getByRole("button", { name: "Add first" }).click();
  await expect(page.getByTestId("count")).toHaveText("1");
  expect(writes).toBe(1);
});

test("a failed mutation does not stop the next queued addition", async ({ page }) => {
  await page.route(`${apiBaseUrl}/api/cart`, route => route.fulfill({ json: cart(0) }));
  const writes: Route[] = [];
  await page.route(`${apiBaseUrl}/api/cart/items`, route => { writes.push(route); });
  await harness(page);
  await expect(page.getByTestId("count")).toHaveText("0");
  await page.getByRole("button", { name: "Add first" }).click();
  await page.getByRole("button", { name: "Add second" }).click();
  await expect.poll(() => writes.length).toBe(1);
  await writes[0].fulfill({ status: 409, json: { detail: "Stock changed" } });
  await expect.poll(() => writes.length).toBe(2);
  await writes[1].fulfill({ json: cart(1) });
  await expect(page.getByTestId("count")).toHaveText("1");
  await expect(page.getByTestId("error")).toBeEmpty();
});

test("login waits for an active guest write and cancels obsolete queued writes", async ({ page }) => {
  await page.route(`${apiBaseUrl}/api/cart`, route => route.fulfill({ json: cart(0) }));
  const writes: Route[] = [];
  let merge: Route | undefined;
  await page.route(`${apiBaseUrl}/api/cart/items`, route => { writes.push(route); });
  await page.route(`${apiBaseUrl}/api/cart/merge`, route => { merge = route; });
  await harness(page);
  await expect(page.getByTestId("count")).toHaveText("0");
  await page.getByRole("button", { name: "Add first" }).click();
  await expect.poll(() => writes.length).toBe(1);
  await page.getByRole("button", { name: "Add second" }).click();
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByTestId("count")).toHaveText("none");
  expect(merge).toBeUndefined();
  await writes[0].fulfill({ json: cart(99) });
  await expect.poll(() => merge).toBeTruthy();
  expect(writes).toHaveLength(1);
  await expect(page.getByTestId("count")).toHaveText("none");
  await merge!.fulfill({ json: cart(1) });
  await expect(page.getByTestId("count")).toHaveText("1");
});

test("failed login merge is retried before allowing a user cart mutation", async ({ page }) => {
  await page.route(`${apiBaseUrl}/api/cart`, route => route.fulfill({ json: cart(0) }));
  let merges = 0;
  let writes = 0;
  await page.route(`${apiBaseUrl}/api/cart/merge`, route => {
    merges++;
    return route.fulfill(merges === 1
      ? { status: 409, json: { detail: "Merge conflict" } }
      : { json: cart(1) });
  });
  await page.route(`${apiBaseUrl}/api/cart/items`, route => { writes++; return route.fulfill({ json: cart(2) }); });
  await harness(page);
  await expect(page.getByTestId("count")).toHaveText("0");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page.getByTestId("error")).toContainText("Merge conflict");
  expect(writes).toBe(0);
  await page.getByRole("button", { name: "Add first" }).click();
  await expect(page.getByTestId("count")).toHaveText("2");
  expect(merges).toBe(2);
  expect(writes).toBe(1);
});
