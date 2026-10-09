import { setMockSession, getMockSession } from "./test";
import { expect, test, type Page, type Route } from "./test";
import { apiBaseUrl, mockAccountOrdersApi, signInAsCustomer, successfulOrder } from "./fixtures";

const authKey = "biogarden_auth";
const emptyCart = {
  id: "session-cart", items: [], totalItemCount: 0, totalPrice: 0,
  expiresAtUtc: "2099-01-01T00:00:00Z", isCheckedOut: false,
};

async function setup(page: Page) {
  await mockAccountOrdersApi(page);
  await page.route(`${apiBaseUrl}/api/cart/merge`, route => route.fulfill({ json: emptyCart }));
}

test("logout in another tab removes private screens and checkout drafts", async ({ page, context }) => {
  await signInAsCustomer(page);
  await setup(page);
  await page.goto("/fiokom/rendeleseim");
  await expect(page.getByText(successfulOrder.orderNumber).first()).toBeVisible();
  const other = await context.newPage();
  await setup(other);
  await other.goto("/fiokom/rendeleseim");
  await expect(other.getByText(successfulOrder.orderNumber).first()).toBeVisible();
  await other.evaluate(() => {
    sessionStorage.setItem("biogarden_checkout_draft", "private draft");
    sessionStorage.setItem("biogarden_checkout_idempotency_key", "old key");
  });
  await page.getByRole("button", { name: "Kijelentkezés", exact: true }).first().click();
  await expect(other).toHaveURL(/\/bejelentkezes$/);
  await expect(other.getByText(successfulOrder.orderNumber)).toHaveCount(0);
  expect(await other.evaluate(() => [
    sessionStorage.getItem("biogarden_checkout_draft"),
    sessionStorage.getItem("biogarden_checkout_idempotency_key"),
  ])).toEqual([null, null]);
});

test("expiry removes private screens without waiting for another API call", async ({ page }) => {
  await page.clock.install();
  await signInAsCustomer(page);
  await setup(page);
  await page.goto("/fiokom/rendeleseim");
  await expect(page.getByText(successfulOrder.orderNumber).first()).toBeVisible();
  setMockSession(page.context(), null);
  await page.clock.fastForward(3_600_001);
  await expect(page).toHaveURL(/\/bejelentkezes$/);
  expect(await page.evaluate(key => localStorage.getItem(key), authKey)).toBeNull();
});

test("a current session's 401 logs out but 403 preserves the session", async ({ page }) => {
  await signInAsCustomer(page);
  await setup(page);
  await page.route(url => url.origin === apiBaseUrl && url.pathname === "/api/orders", route => route.fulfill({ status: 403 }));
  await page.goto("/fiokom/rendeleseim");
  await expect(page.getByRole("alert")).toHaveText("A művelet nem sikerült. Kérjük, próbáld újra.");
  await expect(page).toHaveURL(/\/fiokom\/rendeleseim$/);
  expect(await page.evaluate(key => localStorage.getItem(key), authKey)).toBeNull();
  await page.route(url => url.origin === apiBaseUrl && url.pathname === "/api/orders", route => route.fulfill({ status: 401 }));
  await page.reload();
  await expect(page).toHaveURL(/\/bejelentkezes$/);
  expect(await page.evaluate(key => localStorage.getItem(key), authKey)).toBeNull();
});

test("a delayed 401 from an old login cannot clear the replacement session", async ({ page }) => {
  await signInAsCustomer(page);
  await setup(page);
  const oldRequests: Route[] = [];
  let acceptNewSession = false;
  await page.route(url => url.origin === apiBaseUrl && url.pathname === "/api/orders", async route => {
    if (!acceptNewSession) {
      oldRequests.push(route);
      return;
    }
    await route.fulfill({ json: { items: [{ ...successfulOrder, orderNumber: "NEW-SESSION-ORDER" }], page: 1, pageSize: 10, totalCount: 1, totalPages: 1 } });
  });
  await page.goto("/fiokom/rendeleseim");
  await expect.poll(() => oldRequests.length).toBeGreaterThan(0);
  acceptNewSession = true;
  setMockSession(page.context(), { ...getMockSession(page.context())!, sessionId: "replacement-session", userId: "55555555-5555-5555-5555-555555555555" });
  await page.evaluate(() => { const channel = new BroadcastChannel("biogarden-session"); channel.postMessage("changed"); channel.close(); });
  await expect(page.getByText("NEW-SESSION-ORDER").first()).toBeVisible();
  const responses = Promise.all(oldRequests.map(route => page.waitForResponse(response => response.request() === route.request())));
  await Promise.all(oldRequests.map(route => route.fulfill({ status: 401 })));
  await responses;
  expect(await page.evaluate(key => localStorage.getItem(key), authKey)).toBeNull();
  await expect(page).toHaveURL(/\/fiokom\/rendeleseim$/);
  await expect(page.getByText("NEW-SESSION-ORDER").first()).toBeVisible();
});

test("a late guest cart cannot replace the cart after login", async ({ page }) => {
  await setup(page);
  const guestRequests: Route[] = [];
  await page.route(`${apiBaseUrl}/api/cart`, async route => {
    if (route.request().headers()["x-browser-session"] === "guest") {
      guestRequests.push(route);
      return;
    }
    await route.fulfill({ json: emptyCart });
  });
  await page.goto("/kosar");
  await expect.poll(() => guestRequests.length).toBeGreaterThan(0);
  await signInAsCustomer(page);
  await page.evaluate(() => { const channel = new BroadcastChannel("biogarden-session"); channel.postMessage("changed"); channel.close(); });
  await expect.poll(() => getMockSession(page.context())?.sessionId).toBe("customer-session");
  await page.waitForResponse(response => response.url().endsWith("/api/auth/session"));
  const responses = Promise.all(guestRequests.map(route => page.waitForResponse(response => response.request() === route.request())));
  await Promise.all(guestRequests.map(route => route.fulfill({ json: { ...emptyCart, totalItemCount: 99 } })));
  await responses;
  await expect(page.getByText("99", { exact: true })).toHaveCount(0);
  await expect(page.getByRole("heading", { name: "A kosarad még üres", exact: true })).toBeVisible();
});


test("foreground activity renews a memory-only session without reloading the cart", async ({ page }) => {
  await page.clock.install();
  await signInAsCustomer(page);
  await setup(page);
  const renewals: string[] = [];
  let merges = 0;
  page.on("request", request => {
    if (request.url().endsWith("/api/auth/session") && request.headers()["x-session-activity"] === "1") renewals.push(request.url());
    if (request.url().endsWith("/api/cart/merge")) merges++;
  });
  await page.goto("/fiokom/rendeleseim");
  await expect(page.getByText(successfulOrder.orderNumber).first()).toBeVisible();
  const initialRenewals = renewals.length;
  await page.clock.fastForward(31 * 60_000);
  expect(renewals).toHaveLength(initialRenewals);
  await page.keyboard.press("Tab");
  await expect.poll(() => renewals.length).toBe(initialRenewals + 1);
  expect(merges).toBe(0);
  expect(await page.evaluate(() => localStorage.getItem("biogarden_auth"))).toBeNull();
  await page.reload();
  await expect(page.getByText(successfulOrder.orderNumber).first()).toBeVisible();
});


test("a stalled startup session check times out and can be retried", async ({ page }) => {
  await page.clock.install();
  await setup(page);
  let checks = 0;
  await page.route(`${apiBaseUrl}/api/auth/session`, () => { checks++; });
  await page.goto("/bejelentkezes");
  await expect(page.getByRole("status")).toHaveText("A webhely betöltése…");
  await expect.poll(() => checks).toBe(1);
  await page.clock.fastForward(10_001);
  await expect(page.getByText("A webhely betöltése nem sikerült.")).toBeVisible();
  await page.unroute(`${apiBaseUrl}/api/auth/session`);
  await page.getByRole("button", { name: "Újrapróbálás" }).click();
  await expect(page.getByText("A webhely betöltése nem sikerült.")).toHaveCount(0);
  await expect(page.getByRole("button", { name: "Bejelentkezés", exact: true })).toBeVisible();
});

test("failed expiry checks hide private data and retry only a bounded number of times", async ({ page }) => {
  await page.clock.install();
  await signInAsCustomer(page);
  await setup(page);
  await page.goto("/fiokom/rendeleseim");
  await expect(page.getByText(successfulOrder.orderNumber).first()).toBeVisible();
  let checks = 0;
  await page.route(`${apiBaseUrl}/api/auth/session`, route => { checks++; return route.abort(); });
  await page.clock.fastForward(3_600_001);
  await expect(page.getByText(successfulOrder.orderNumber)).toHaveCount(0);
  await expect(page).toHaveURL(/\/bejelentkezes$/);
  await expect.poll(() => checks).toBe(1);
  for (const [index, delay] of [5_000, 15_000, 30_000].entries()) {
    await page.clock.fastForward(delay);
    await expect.poll(() => checks).toBe(index + 2);
  }
  await page.clock.fastForward(120_000);
  expect(checks).toBe(4);
  // A network-restored event verifies passively, even after automatic retries are exhausted.
  setMockSession(page.context(), null);
  await page.unroute(`${apiBaseUrl}/api/auth/session`);
  const response = page.waitForResponse(`${apiBaseUrl}/api/auth/session`);
  await page.evaluate(() => window.dispatchEvent(new Event("online")));
  expect((await response).request().headers()["x-session-activity"]).toBeUndefined();
  await expect(page.getByText(successfulOrder.orderNumber)).toHaveCount(0);
});

test("session verification recovers after a transient expiry failure without renewing idle time", async ({ page }) => {
  await page.clock.install();
  await signInAsCustomer(page);
  await setup(page);
  await page.goto("/fiokom/rendeleseim");
  await expect(page.getByText(successfulOrder.orderNumber).first()).toBeVisible();
  let checks = 0;
  await page.route(`${apiBaseUrl}/api/auth/session`, route => {
    checks++;
    return checks === 1 ? route.abort() : route.fallback();
  });
  // Another tab has renewed the actual server session, but this tab cannot initially verify it.
  setMockSession(page.context(), { ...getMockSession(page.context())!, expiresAtUtc: new Date(Date.now() + 7_200_000).toISOString() });
  await page.clock.fastForward(3_600_001);
  await expect(page.getByText(successfulOrder.orderNumber)).toHaveCount(0);
  const response = page.waitForResponse(`${apiBaseUrl}/api/auth/session`);
  await page.clock.fastForward(5_000);
  expect((await response).request().headers()["x-session-activity"]).toBeUndefined();
  await expect(page.getByRole("button", { name: "Kijelentkezés", exact: true }).first()).toBeVisible();
  await page.goto("/fiokom/rendeleseim");
  await expect(page.getByText(successfulOrder.orderNumber).first()).toBeVisible();
});
