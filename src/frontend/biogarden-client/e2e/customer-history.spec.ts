import { expect, test } from "./test";
import { apiBaseUrl, mockAccountOrdersApi, signInAsCustomer, successfulOrder } from "./fixtures";

test("customer history requests pages and filters while preserving detail access", async ({ page }) => {
  await signInAsCustomer(page);
  await mockAccountOrdersApi(page);
  const queries: URLSearchParams[] = [];
  await page.route(url => url.origin === apiBaseUrl && url.pathname === "/api/orders", route => {
    const query = new URL(route.request().url()).searchParams;
    queries.push(query);
    const history = query.get("view") === "history";
    const current = Number(query.get("page"));
    return route.fulfill({ json: {
      items: [{ ...successfulOrder, orderNumber: history ? `Historical ${current}` : "Recent order" }],
      page: current, pageSize: 10, totalCount: history ? 21 : 1, totalPages: history ? 3 : 1,
    } });
  });
  await page.goto("/fiokom/rendeleseim");
  await expect(page.getByRole("heading", { name: "Recent order" })).toBeVisible();
  await page.getByRole("button", { name: "Korábbi rendelések", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Historical 1" })).toBeVisible();
  await page.getByRole("button", { name: "Következő", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Historical 2" })).toBeVisible();
  await page.getByLabel("Rendelésszám", { exact: true }).fill("BK-2026");
  await expect.poll(() => queries.at(-1)?.get("search")).toBe("BK-2026");
  expect(queries.at(-1)?.get("page")).toBe("1");
  await page.getByLabel("Rendelés éve").fill("2026");
  await page.getByLabel("Rendelés éve").press("Tab");
  await expect.poll(() => queries.at(-1)?.get("year")).toBe("2026");
  await page.getByLabel("Rendelés éve").fill("20");
  await page.getByLabel("Rendelés éve").press("Tab");
  await expect(page.getByRole("alert")).toContainText("négyjegyű évet");
  expect(queries.at(-1)?.get("year")).toBe("2026");
  await page.getByLabel("Rendelés éve").fill("2025");
  await page.getByLabel("Rendelés éve").press("Enter");
  await expect.poll(() => queries.at(-1)?.get("year")).toBe("2025");
  await page.goBack();
  await expect(page.getByLabel("Rendelés éve")).toHaveValue("2026");
  await expect.poll(() => queries.at(-1)?.get("year")).toBe("2026");
  const orderCard = page.getByRole("article").filter({ has: page.getByRole("heading", { name: "Historical 1" }) });
  await orderCard.click({ position: { x: 10, y: 10 } });
  await expect(page.getByRole("heading", { name: successfulOrder.orderNumber })).toBeVisible();
  await expect(page.getByLabel("Kérés leírása", { exact: true })).toBeEnabled();
  await page.goBack();
  const detailsLink = page.getByRole("link", { name: "Részletek – Historical 1", exact: true });
  await detailsLink.focus();
  await expect(detailsLink).toBeFocused();
  await detailsLink.press("Enter");
  await expect(page.getByRole("heading", { name: successfulOrder.orderNumber })).toBeVisible();
});
