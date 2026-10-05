import { expect, test, type Route } from "@playwright/test";
import { apiBaseUrl, mockEmptyCartApi, product, signInAsAdmin, successfulOrder } from "./fixtures";

const user = {
  id: "55555555-5555-5555-5555-555555555555", email: "paging@example.com", firstName: "", lastName: "",
  phoneNumber: null, companyName: null, taxNumber: null, roles: [], isAdmin: false,
  emailConfirmed: true, isDeactivated: false, isAnonymized: false, createdAt: "2026-01-01T00:00:00Z",
  orderCount: 0, activeOrderCount: 0, pendingRequest: null,
};
const withdrawal = {
  id: 1, orderId: successfulOrder.id, orderNumber: "", orderStatus: 3, orderTotalPrice: 1800,
  customerName: "Test Customer", customerEmail: "test@example.com", reason: "Test request", status: 1,
  requestedAtUtc: "2026-01-01T00:00:00Z", handledAtUtc: null,
};
const lists = [
  { path: "orders", page: "/admin/rendelesek", row: (label: string) => ({ ...successfulOrder, orderNumber: label }) },
  { path: "users", page: "/admin/fiok", row: (label: string) => ({ ...user, lastName: label }) },
  { path: "inventory", page: "/admin/raktarkeszlet", row: (label: string) => ({ ...product, productId: 1, name: label, stockQuantity: 8, reservedQuantity: 0, availableQuantity: 8 }) },
  { path: "orders/withdrawal-requests", page: "/admin/elallasi-keresek", row: (label: string) => ({ ...withdrawal, orderNumber: label }) },
];

test.beforeEach(async ({ page }) => {
  await signInAsAdmin(page);
  await mockEmptyCartApi(page);
  await page.route(`${apiBaseUrl}/uploads/**`, route => route.fulfill({ status: 204 }));
});

for (const list of lists) {
  test(`${list.path} requests server pages and resets page when searching`, async ({ page }) => {
    const queries: URLSearchParams[] = [];
    await page.route(url => url.origin === apiBaseUrl && url.pathname === `/api/admin/${list.path}`, route => {
      const query = new URL(route.request().url()).searchParams;
      queries.push(query);
      const current = Number(query.get("page"));
      const searched = query.get("search") === "needle";
      return route.fulfill({ json: { items: [list.row(searched ? "Server result" : `Page ${current}`)],
        page: current, pageSize: 12, totalCount: searched ? 1 : 24, totalPages: searched ? 1 : 2 } });
    });
    await page.goto(list.page);
    await expect(page.getByRole("heading", { name: "Page 1", exact: true })).toBeVisible();
    await page.getByRole("button", { name: "Következő", exact: true }).click();
    await expect(page.getByRole("heading", { name: "Page 2", exact: true })).toBeVisible();
    expect(queries.at(-1)?.get("pageSize")).toBe("12");
    expect(queries.at(-1)?.get("page")).toBe("2");
    await page.getByLabel("Keresés", { exact: true }).fill("needle");
    await expect(page.getByRole("heading", { name: "Server result", exact: true })).toBeVisible();
    expect(queries.at(-1)?.get("page")).toBe("1");
    expect(queries.at(-1)?.get("search")).toBe("needle");
    await expect(page.getByRole("navigation", { name: "Lapozás" })).toHaveCount(0);
  });
}

test("a late previous page cannot overwrite a newer search", async ({ page }) => {
  let oldPage: Route | undefined;
  await page.route(url => url.pathname === "/api/admin/orders", route => {
    const query = new URL(route.request().url()).searchParams;
    if (query.get("page") === "2" && !query.get("search")) { oldPage = route; return; }
    return route.fulfill({ json: { items: [{ ...successfulOrder, orderNumber: query.get("search") ? "Current result" : "First result" }],
      page: 1, pageSize: 12, totalCount: 24, totalPages: 2 } });
  });
  await page.goto("/admin/rendelesek");
  await page.getByRole("button", { name: "Következő", exact: true }).click();
  await expect.poll(() => oldPage).toBeTruthy();
  await page.getByLabel("Keresés", { exact: true }).fill("current");
  await expect(page.getByRole("heading", { name: "Current result", exact: true })).toBeVisible();
  await oldPage!.fulfill({ json: { items: [{ ...successfulOrder, orderNumber: "Obsolete result" }], page: 2, pageSize: 12, totalCount: 24, totalPages: 2 } });
  await expect(page.getByRole("heading", { name: "Current result", exact: true })).toBeVisible();
  await expect(page.getByRole("heading", { name: "Obsolete result", exact: true })).toHaveCount(0);
});

test("handling the last request on a page uses the server's adjusted page", async ({ page }) => {
  let handled = false;
  await page.route(url => url.pathname === "/api/admin/orders/withdrawal-requests", route => {
    const current = handled ? 1 : Number(new URL(route.request().url()).searchParams.get("page"));
    return route.fulfill({ json: { items: [{ ...withdrawal, id: current, orderNumber: `Request ${current}` }],
      page: current, pageSize: 12, totalCount: handled ? 12 : 13, totalPages: handled ? 1 : 2 } });
  });
  await page.route(`${apiBaseUrl}/api/admin/orders/withdrawal-requests/2/handled`, route => {
    handled = true;
    return route.fulfill({ json: { ...withdrawal, id: 2, status: 2, orderNumber: "Request 2" } });
  });
  await page.goto("/admin/elallasi-keresek");
  await page.getByRole("button", { name: "Következő", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Request 2", exact: true })).toBeVisible();
  await page.getByRole("button", { name: "Feldolgozva", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Request 1", exact: true })).toBeVisible();
  await expect(page.getByRole("heading", { name: "Request 2", exact: true })).toHaveCount(0);
  await expect(page.getByRole("navigation", { name: "Lapozás" })).toHaveCount(0);
});
