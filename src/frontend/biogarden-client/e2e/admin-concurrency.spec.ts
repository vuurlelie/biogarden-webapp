import { expect, test } from "./test";
import { apiBaseUrl, mockEmptyCartApi, product, signInAsAdmin, successfulOrder } from "./fixtures";

test.beforeEach(async ({ page }) => {
  await signInAsAdmin(page);
  await mockEmptyCartApi(page);
  await page.route(`${apiBaseUrl}/uploads/**`, route => route.fulfill({ status: 204 }));
});

test("product conflict preserves the draft and reloads current stock before reapplying edits", async ({ page }) => {
  await page.route(url => url.pathname === "/api/admin/products", route => route.fulfill({ json: {
    items: [{ ...product, description: "Original description", stockQuantity: 8 }], page: 1, pageSize: 12, totalCount: 1,
  } }));
  await page.route(`${apiBaseUrl}/api/products/1`, route => route.fulfill({ json: { ...product, version: "new", description: "Current description", stockQuantity: 7 } }));
  await page.route(`${apiBaseUrl}/api/admin/products/1`, route => route.fulfill({ status: 409, json: { detail: "A termék megváltozott." } }));
  await page.goto("/admin/termekek");
  await page.getByRole("button", { name: "Szerkesztés", exact: true }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel("Név", { exact: false }).fill("My unsaved name");
  await dialog.getByRole("button", { name: "Mentés", exact: true }).click();
  await expect(dialog.getByLabel("Név", { exact: false })).toHaveValue("My unsaved name");
  await dialog.getByRole("button", { name: "Aktuális adatok betöltése" }).click();
  await dialog.getByText("Korábbi bevitel – ellenőrizd, majd alkalmazd újra a szükséges módosításokat").click();
  await expect(dialog.getByText(/My unsaved name —/)).toBeVisible();
  await expect(dialog.getByLabel("Név", { exact: false })).toHaveValue(product.name);
});

test("stale order edit preserves input and reloads the verified payment before another save", async ({ page }) => {

  const writes: Record<string, unknown>[] = [];
  await page.route((url) => url.origin === apiBaseUrl && url.pathname === "/api/admin/orders", route => route.fulfill({ json: { items: [{ ...successfulOrder,
    version: writes.length === 0 ? "old" : "new", paymentStatus: writes.length === 0 ? 0 : 2, status: writes.length > 1 ? 2 : 1 }], page: 1, pageSize: 12, totalCount: 1, totalPages: 1 } }));
  await page.route(`${apiBaseUrl}/api/admin/orders/${successfulOrder.id}`, async route => {
    if (route.request().method() === "GET") { await route.fulfill({ json: { ...successfulOrder, version: "new", paymentStatus: 2, status: 1 } }); return; }
    writes.push(route.request().postDataJSON());
    await route.fulfill(writes.length === 1 ? { status: 409, json: { detail: "Az adatok időközben megváltoztak." } }
      : { json: { ...successfulOrder, version: "saved", status: 2, paymentStatus: 2 } });
  });
  await page.goto("/admin/rendelesek");
  await page.getByRole("button", { name: "Részletek", exact: true }).click();
  await page.getByRole("button", { name: "Állapot módosítása" }).click();
  await page.getByLabel("Rendelés állapota").selectOption("2");
  await page.getByRole("button", { name: "Mentés", exact: true }).click();
  await expect(page.getByLabel("Rendelés állapota")).toHaveValue("2");
  await expect(page.getByRole("button", { name: "Mentés", exact: true })).toBeDisabled();
  await page.getByRole("button", { name: "Aktuális adatok betöltése" }).click();
  await expect(page.getByLabel("Fizetés állapota")).toHaveValue("2");
  await expect(page.getByText(/Korábbi bevitel:/)).toBeVisible();
  await page.getByLabel("Rendelés állapota").selectOption("2");
  await page.getByRole("button", { name: "Mentés", exact: true }).click();
  await expect.poll(() => writes.length).toBe(2);
  expect(writes[0].version).toBe("old");
  expect(writes[1]).toMatchObject({ version: "new", paymentStatus: 2 });
});

test("inventory conflict reloads sold stock and retains the previous input for comparison", async ({ page }) => {

  let submitted: Record<string, unknown> | undefined;
  await page.route((url) => url.origin === apiBaseUrl && url.pathname === "/api/admin/inventory", route => route.fulfill({ json: { items: [{ ...product, productId: 1,
    version: submitted ? "new" : "old", stockQuantity: submitted ? 7 : 8, reservedQuantity: 0, availableQuantity: 7 }], page: 1, pageSize: 12, totalCount: 1, totalPages: 1 } }));
  await page.route(`${apiBaseUrl}/api/admin/inventory/1`, async route => {
    if (route.request().method() === "GET") { await route.fulfill({ json: { ...product, productId: 1, version: "new", stockQuantity: 7, reservedQuantity: 0, availableQuantity: 7 } }); return; }
    submitted = route.request().postDataJSON();
    await route.fulfill({ status: 409, json: { detail: "Készlet megváltozott." } });
  });
  await page.goto("/admin/raktarkeszlet");
  await page.getByRole("button", { name: "Készlet módosítása" }).click();
  await page.getByLabel("Új teljes készlet").fill("9");
  await page.getByRole("button", { name: "Mentés", exact: true }).click();
  await page.getByRole("button", { name: "Aktuális adatok betöltése" }).click();
  await expect(page.getByLabel("Új teljes készlet")).toHaveValue("7");
  await expect(page.getByText(/Korábbi bevitel: 9 db/)).toBeVisible();
  expect(submitted?.version).toBe("old");
});

test("completed order permits audited refund recording but cannot become unpaid", async ({ page }) => {
  let submitted: Record<string, unknown> | undefined;
  await page.route((url) => url.origin === apiBaseUrl && url.pathname === "/api/admin/orders", route => route.fulfill({ json: { items: [{ ...successfulOrder, status: 3, paymentStatus: submitted ? 5 : 4 }], page: 1, pageSize: 12, totalCount: 1, totalPages: 1 } }));
  await page.route(`${apiBaseUrl}/api/admin/orders/${successfulOrder.id}`, async route => {
    submitted = route.request().postDataJSON();
    await route.fulfill({ json: { ...successfulOrder, status: 3, paymentStatus: 5, version: "refunded" } });
  });
  await page.goto("/admin/rendelesek");
  await page.getByRole("button", { name: "Részletek", exact: true }).click();
  await page.getByRole("button", { name: "Állapot módosítása" }).click();
  await expect(page.getByLabel("Rendelés állapota")).toBeDisabled();
  await expect(page.getByLabel("Fizetés állapota").locator('option[value="0"]')).toHaveCount(0);
  await page.getByLabel("Fizetés állapota").selectOption("5");
  await page.getByRole("button", { name: "Mentés", exact: true }).click();
  await expect(page.getByRole("alert")).toContainText("indoklása kötelező");
  expect(submitted).toBeUndefined();
  await page.getByLabel("Fizetési módosítás indoklása").fill("Visszatérítés ellenőrizve: REF-123");
  await page.getByRole("button", { name: "Mentés", exact: true }).click();
  await expect.poll(() => submitted?.paymentStatus).toBe(5);
  expect(submitted?.paymentCorrectionReason).toBe("Visszatérítés ellenőrizve: REF-123");
  await page.getByRole("button", { name: "Állapot módosítása" }).click();
  await expect(page.getByLabel("Fizetés állapota")).toHaveValue("5");
});
