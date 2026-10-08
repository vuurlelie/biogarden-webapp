import { expect, test, type Response } from "@playwright/test";

test("isolated guests, login merge, lost checkout response and cancellation use the real API", async ({ page, browser }) => {
  const api = process.env.E2E_API_URL!;
  const other = await browser.newContext({ ignoreHTTPSErrors: true });
  try {
    const localOnly = (url: URL) => ["localhost", "127.0.0.1", "[::1]"].includes(url.hostname);
    await page.context().route("**/*", route => localOnly(new URL(route.request().url())) ? route.continue() : route.abort());
    await other.route("**/*", route => localOnly(new URL(route.request().url())) ? route.continue() : route.abort());
    const visitor = await other.newPage();
    await visitor.goto("https://localhost:4174/kosar");
    await expect(visitor.getByText(/A kosarad még üres/)).toBeVisible();
    await page.goto(`/termekek/${process.env.E2E_PRODUCT_ID}`);
    await page.getByRole("spinbutton").fill("1");
    await page.getByRole("button", { name: /Kosárba teszem/ }).click();
    await expect(page.getByRole("status")).toContainText(/bekerült a kosárba/);
    const cookie = (await page.context().cookies(api)).find(value => value.name === "biogarden_cart_id");
    expect(cookie).toMatchObject({ httpOnly: true, secure: true, sameSite: "None" });
    await visitor.reload();
    await expect(visitor.getByText(/A kosarad még üres/)).toBeVisible();
    await page.goto("/bejelentkezes");
    await page.getByLabel("Email cím").fill(process.env.E2E_CUSTOMER_EMAIL!);
    await page.locator("#login-password").fill("Password123!");
    const merged = page.waitForResponse(response => response.url().includes("/api/cart/merge") && response.ok());
    await page.getByRole("button", { name: "Bejelentkezés", exact: true }).click();
    await merged;
    const loginCookie = (await page.context().cookies(api)).find(value => value.name === "biogarden_session");
    expect(loginCookie).toMatchObject({ httpOnly: true, secure: true, sameSite: "Lax" });
    expect(await page.evaluate(() => localStorage.getItem("biogarden_auth"))).toBeNull();
    await page.goto("/penztar");
    await page.getByLabel(/Név/).fill("Test Customer");
    await page.getByLabel(/Email cím/).fill(process.env.E2E_CUSTOMER_EMAIL!);
    await page.getByLabel(/Telefonszám/).fill("+36301234567");
    await page.getByRole("button", { name: /Tovább a pénztárhoz/ }).click();
    await page.getByLabel(/Fizetés átvételkor/).check();
    await expect(page.getByLabel(/Fizetés átvételkor/)).toBeChecked();
    await expect(page.getByRole("definition").last()).toHaveText(/1\s*800/);
    const reviewButton = page.getByRole("button", { name: /Rendelés áttekintése/ });
    await expect(reviewButton).toBeEnabled();
    const dialog = page.getByRole("dialog", { name: /Megrendelés áttekintése/ });
    const validationResponses: { status: number; body: unknown }[] = [];
    const recordValidation = async (response: Response) => {
      if (response.url() !== `${api}/api/cart/validate`) return;
      validationResponses.push({ status: response.status(), body: await response.json().catch(() => null) });
    };
    page.on("response", recordValidation);
    try {
      await reviewButton.click();
      await expect(dialog).toBeVisible();
      await expect.poll(() => validationResponses).toContainEqual({ status: 200, body: { isValid: true, issues: [] } });
    } catch (error) {
      // Only this isolated journey's synthetic data is logged; never run it against a hosted API.
      console.error("Checkout review diagnostics:", JSON.stringify({
        validationResponses,
        alerts: await page.getByRole("alert").allTextContents(),
        invalidFields: await page.locator("input:invalid, [aria-invalid='true']").evaluateAll(elements =>
          elements.map(element => ({ name: element.getAttribute("name"), type: element.getAttribute("type"),
            message: element instanceof HTMLInputElement ? element.validationMessage : null }))),
        formInert: await page.locator("form").evaluateAll(elements => elements.map(element => (element as HTMLElement).inert)),
        dialogCount: await page.locator('[role="dialog"]').count(),
      }));
      throw error;
    } finally { page.off("response", recordValidation); }
    await expect.poll(() => dialog.evaluate(element => element.contains(document.activeElement))).toBe(true);
    await page.keyboard.press("Escape");
    await expect(dialog).not.toBeVisible();
    await expect(page.getByRole("button", { name: /Rendelés áttekintése/ })).toBeFocused();
    await page.keyboard.press("Enter");
    await expect(dialog).toBeVisible();
    await expect.poll(() => page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
    await page.getByLabel(/Általános Szerződési Feltételek/).check();
    await page.getByLabel(/Adatkezelési Tájékoztatót/).check();
    let orderId = "";
    // The real server commits; only the delivery of its response to this browser is interrupted.
    await page.route(`${api}/api/orders`, async route => {
      const response = await route.fetch();
      expect(response.status()).toBe(201);
      const order = await response.json();
      expect(order.totalPrice).toBe(1800);
      expect(order.items[0].quantity).toBe(2);
      orderId = order.id;
      await route.abort("failed");
    }, { times: 1 });
    await page.getByRole("button", { name: /Megrendelés elküldése/ }).click();
    await expect(page).toHaveURL(/rendeles-sikeres/);
    expect(orderId).not.toBe("");
    await page.reload();
    await expect(page).toHaveURL(/rendeles-sikeres/);
    await page.goto(`/fiokom/rendeleseim/${orderId}`);
    await page.getByRole("button", { name: "Rendelés lemondása" }).click();
    await page.getByLabel(/Lemondás oka/).fill("Real browser recovery test");
    await page.getByRole("button", { name: "Igen, lemondom" }).click();
    await expect(page.getByRole("dialog", { name: "Sikeres lemondás" })).toBeVisible();
  } finally { await other.close().catch(() => {}); }
});
