import { expect, test } from "@playwright/test";
import {
  expectNoUnhandledApiCall,
  mockAdminOrdersApi,
  mockCancelledPaymentRetryApi,
  mockCatalogPaginationApi,
  mockCheckoutApi,
  mockPublicShopApi,
  mockSumUpReturnStatusApi,
  signInAsAdmin,
  successfulOrder,
} from "./fixtures";

test.beforeEach(async ({ page }) => {
  await page.route("http://localhost:5097/api/**", expectNoUnhandledApiCall);
});

test("public catalog pages request and render paged results", async ({ page }) => {
  await mockCatalogPaginationApi(page);

  const productRequests: string[] = [];
  const postRequests: string[] = [];

  page.on("request", (request) => {
    const url = new URL(request.url());

    if (url.origin === "http://localhost:5097" && url.pathname === "/api/products") {
      productRequests.push(url.search);
    }

    if (url.origin === "http://localhost:5097" && url.pathname === "/api/posts") {
      postRequests.push(url.search);
    }
  });

  await page.goto("/termekek");
  await expect(page.getByRole("link", { name: "Teszt levendula tea" }).first()).toBeVisible();
  await expect(page.getByRole("button", { name: "2" })).toBeVisible();
  await expect(productRequests.some((query) => query.includes("pageSize=12"))).toBe(true);

  await page.getByRole("button", { name: "2" }).click();
  await expect(page.getByRole("link", { name: "Masodik oldali szorp" }).first()).toBeVisible();
  await expect(productRequests.some((query) => query.includes("page=2"))).toBe(true);

  await page.goto("/tudastar");
  await expect(page.getByText("Elso teszt bejegyzes")).toBeVisible();
  await expect(postRequests.some((query) => query.includes("pageSize=4"))).toBe(true);

  await page.getByRole("button", { name: "2" }).first().click();
  await expect(page.getByText("Masodik oldali bejegyzes")).toBeVisible();
  await expect(postRequests.some((query) => query.includes("page=2"))).toBe(true);
});

test("customer can browse products, open details, and add an item to the cart", async ({ page }) => {
  await mockPublicShopApi(page);

  await page.goto("/termekek");
  await expect(page.getByRole("heading", { name: /Termekeink|Termékeink/ })).toBeVisible();
  await page.getByRole("link", { name: /Teszt levendula tea/ }).first().click();

  await expect(page).toHaveURL(/\/termekek\/1$/);
  await expect(page.getByRole("heading", { name: "Teszt levendula tea" })).toBeVisible();
  await expect(page.getByRole("heading", { name: /Kapcsolodo termekek|Kapcsolódó termékek/ })).toBeVisible();

  await page.getByRole("button", { name: /Kosarba teszem|Kosárba teszem/ }).click();
  await expect(page.getByRole("status")).toContainText(/bekerult a kosarba|bekerült a kosárba/);

  await page.goto("/kosar");
  await expect(page.getByRole("heading", { name: /Kosar|Kosár/ })).toBeVisible();
  await expect(page.getByRole("link", { name: /Teszt levendula tea/ }).first()).toBeVisible();
  await expect(page.getByRole("link", { name: /Szemelyes es szallitasi adatok megadasa|Személyes és szállítási adatok megadása/ })).toBeVisible();
});

test("guest checkout can create a pay-at-pickup order", async ({ page }) => {
  await mockCheckoutApi(page);

  await page.goto("/penztar");
  await page.getByLabel(/Nev|Név/).fill("Teszt Elek");
  await page.getByLabel(/Email cim|Email cím/).fill("teszt@example.com");
  await page.getByLabel(/Telefonszam|Telefonszám/).fill("+36301234567");

  await page.getByRole("button", { name: /Tovabb a penztarhoz|Tovább a pénztárhoz/ }).click();
  await page.getByLabel(/Fizetes atvetelkor|Fizetés átvételkor/).check();
  await page.getByRole("button", { name: /Rendeles attekintese|Rendelés áttekintése/ }).click();

  await expect(page.getByRole("dialog", { name: /Megrendeles attekintese|Megrendelés áttekintése/ })).toBeVisible();
  await page.getByLabel(/Altalanos Szerzodesi Feltetelek|Általános Szerződési Feltételek/).check();
  await page.getByLabel(/Adatkezelesi Tajekoztatot|Adatkezelési Tájékoztatót/).check();
  await page.getByRole("button", { name: /Megrendeles elkuldese|Megrendelés elküldése/ }).click();

  await expect(page).toHaveURL(/\/rendeles-sikeres$/);
  await expect(page.getByText("BG-2026-0001")).toBeVisible();
});

test("cancelled online payment shows retry state", async ({ page }) => {
  await mockCancelledPaymentRetryApi(page);

  await page.goto(`/penztar?sumup=cancelled&orderId=${successfulOrder.id}`);
  await expect(page.getByRole("heading", { name: /A fizetes megszakadt|A fizetés megszakadt/ })).toBeVisible();
  await expect(page.getByRole("button", { name: /Fizetes ujraprobalasa|Fizetés újrapróbálása/ })).toBeVisible();
});

test("successful SumUp return shows verified paid state", async ({ page }) => {
  await mockSumUpReturnStatusApi(page, 2);

  await page.goto(
    `/rendeles-sikeres?sumup_checkout_reference=${successfulOrder.id.replace(/-/g, "")}&orderId=${successfulOrder.id}`,
  );

  await expect(page.getByRole("heading", { name: /rendel/ })).toBeVisible();
  await expect(page.getByText(/sikeres volt/)).toBeVisible();
  await expect(page.getByText(successfulOrder.orderNumber)).toBeVisible();
});

test("failed SumUp return shows retry link", async ({ page }) => {
  await mockSumUpReturnStatusApi(page, 3);

  await page.goto(
    `/rendeles-sikeres?sumup_checkout_reference=${successfulOrder.id.replace(/-/g, "")}&orderId=${successfulOrder.id}`,
  );

  await expect(page.getByText(/nem siker/)).toBeVisible();
  await expect(page.getByRole("link", { name: /Fizet/ })).toHaveAttribute(
    "href",
    `/penztar?sumup=cancelled&orderId=${successfulOrder.id}`,
  );
});

test("admin can review an order and update its status", async ({ page }) => {
  await signInAsAdmin(page);
  await mockAdminOrdersApi(page);

  await page.goto("/admin/rendelesek");
  await expect(page.getByRole("heading", { name: /Rendelesek|Rendelések/ })).toBeVisible();
  await expect(page.getByText("BG-2026-0001")).toBeVisible();

  await page.getByRole("button", { name: /Reszletek|Részletek/ }).click();
  await expect(page.getByText("Teszt Kft.")).toBeVisible();
  await expect(page.getByText(/Elektronikus szamla \+ nyomtatott szamla|Elektronikus számla \+ nyomtatott számla/)).toBeVisible();

  await page.getByRole("button", { name: /Allapot modositasa|Állapot módosítása/ }).click();
  await page.getByLabel(/Rendeles allapota|Rendelés állapota/).selectOption("1");
  await page.getByLabel(/Fizetes allapota|Fizetés állapota/).selectOption("1");
  await page.getByRole("button", { name: /Mentes|Mentés/ }).click();

  await expect(page.getByRole("status")).toContainText(/BG-2026-0001.*frissult|BG-2026-0001.*frissült/);
});
