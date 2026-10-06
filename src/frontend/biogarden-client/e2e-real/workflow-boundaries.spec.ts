import { expect, test } from "@playwright/test";

test("expired guest cart and private content stay unavailable through the real API", async ({ page, context }) => {
  const api = process.env.E2E_API_URL!;
  await context.route("**/*", route => ["localhost", "127.0.0.1"].includes(new URL(route.request().url()).hostname) ? route.continue() : route.abort());
  await context.addCookies([{ name: "biogarden_cart_id", value: process.env.E2E_EXPIRED_SESSION!, url: api,
    httpOnly: true, secure: true, sameSite: "None" }]);
  const cartResponse = page.waitForResponse(response => response.url() === `${api}/api/cart` && response.ok());
  await page.goto("/kosar");
  const cart = await (await cartResponse).json();
  expect(cart.id).not.toBe(process.env.E2E_EXPIRED_CART_ID);
  expect(cart.items).toEqual([]);
  await expect(page.getByText("A kosarad még üres")).toBeVisible();
  await page.goto("/penztar");
  await expect(page.getByRole("heading", { name: "A kosár üres" })).toBeVisible();
  await expect(page.getByRole("button", { name: /Megrendelés elküldése/ })).toHaveCount(0);
  // Availability is enforced at the real mutation endpoint even with a known product ID.
  const unavailable = await page.evaluate(async ({ api, id }) => {
    const response = await fetch(`${api}/api/cart/items`, { method: "POST", credentials: "include",
      headers: { "Content-Type": "application/json", "X-Checkout-Request": "1" }, body: JSON.stringify({ productId: Number(id), quantity: 1 }) });
    return response.status;
  }, { api, id: process.env.E2E_UNAVAILABLE_ID! });
  expect(unavailable).toBe(409);
  await page.goto(`/tudastar/${process.env.E2E_PUBLISHED_ID}`);
  await expect(page.getByRole("heading", { name: "Browser public article" })).toBeVisible();
  for (const id of [process.env.E2E_DRAFT_ID, process.env.E2E_DELETED_ID]) {
    const response = page.waitForResponse(value => value.url() === `${api}/api/posts/${id}`);
    await page.goto(`/tudastar/${id}`);
    expect((await response).status()).toBe(404);
    await expect(page.getByText(/A keresett azonosítójú bejegyzés sajnos nem található/)).toBeVisible();
    await expect(page.getByText(/Browser private draft|Browser deleted article/)).toHaveCount(0);
  }
});
