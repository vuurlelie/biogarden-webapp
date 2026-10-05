import { expect, test, type Route } from "@playwright/test";
import { apiBaseUrl, mockPublicShopApi } from "./fixtures";

const cart = (count: number) => ({
  id: "cart-feedback", items: [], totalItemCount: count, totalPrice: count * 1800,
  expiresAtUtc: "2099-01-01T00:00:00Z", isCheckedOut: false,
});

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
