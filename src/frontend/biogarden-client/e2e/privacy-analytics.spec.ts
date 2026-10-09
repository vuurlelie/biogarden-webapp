import { expect, test, type Page } from "./test";
import { apiBaseUrl, expectNoUnhandledApiCall, mockEmptyCartApi } from "./fixtures";

type CapturedAnalyticsEvent = {
  name: string;
  payload: Record<string, unknown>;
};

test.beforeEach(async ({ page }) => {
  await page.route("http://localhost:5097/api/**", expectNoUnhandledApiCall);
  await captureAnalyticsEvents(page);
});

test("anonymous analytics waits for consent and can be withdrawn", async ({ page }) => {
  await mockEmptyCartApi(page);

  await page.goto("/");
  await expect(page.getByRole(
    "region",
    { name: "Adatvédelmi beállítások" }
  )).toBeVisible();
  await expect(capturedEvents(page)).resolves.toHaveLength(0);

  await page.getByRole(
    "button",
    { name: "Analitika engedélyezése" }
  ).click();
  await expect
    .poll(async () => (await capturedEvents(page)).some((event) => event.name === "page_view"))
    .toBe(true);

  const eventCountAfterConsent = (await capturedEvents(page)).length;
  await page.getByRole(
    "button",
    { name: "Adatvédelmi beállítások" }
  ).click();
  await page.getByLabel("Anonim látogatottsági analitika").uncheck();
  await page.getByRole(
    "button",
    { name: "Mentés" }
  ).click();

  await page.getByRole("link", { name: /Rólunk|Rolunk/ }).first().click();
  await page.waitForTimeout(250);
  await expect(capturedEvents(page)).resolves.toHaveLength(eventCountAfterConsent);
});

test("contact analytics event does not include submitted personal data", async ({ page }) => {
  await mockEmptyCartApi(page);
  await page.route(
    `${apiBaseUrl}/api/contact`,
    async (route) => {
      await route.fulfill({ status: 204 });
    }
  );
  await enableAnalyticsBeforeLoad(page);

  await page.goto("/");
  await page.getByRole("button", { name: /Írj nekünk itt|Irj nekunk itt/ }).click();
  await page.getByLabel(/Név|Nev/).fill("Teszt Elek");
  await page.getByLabel(/Email cím|Email cim/).fill("teszt@example.com");
  await page.getByLabel(/Telefonszám|Telefonszam/).fill("+36301234567");
  await page.getByLabel(/Tárgy|Targy/).fill("Analitika teszt");
  await page.getByLabel(/Üzenet|Uzenet/).fill("Ez egy hosszabb teszt üzenet személyes adattal.");
  await page.getByRole("button", { name: /Üzenet küldése|Uzenet kuldese/ }).click();

  const contactEvent = await waitForCapturedEvent(page, "contact_form_submitted");
  expect(contactEvent.payload).toEqual({ source: "footer" });

  const serializedEvent = JSON.stringify(contactEvent);
  expect(serializedEvent).not.toContain("Teszt Elek");
  expect(serializedEvent).not.toContain("teszt@example.com");
  expect(serializedEvent).not.toContain("+36301234567");
  expect(serializedEvent).not.toContain("Analitika teszt");
});

async function captureAnalyticsEvents(page: Page) {
  await page.addInitScript(() => {
    const analyticsWindow = window as typeof window & {
      __biogardenAnalyticsEvents: Array<{ name: string; payload: Record<string, unknown> }>;
    };

    analyticsWindow.__biogardenAnalyticsEvents = [];
    window.addEventListener(
      "biogarden:analytics-event",
      (event) => {
        analyticsWindow.__biogardenAnalyticsEvents.push(
          (event as CustomEvent<{ name: string; payload: Record<string, unknown> }>).detail,
        );
      }
    );
  });
}

async function enableAnalyticsBeforeLoad(page: Page) {
  await page.addInitScript(() => {
    window.localStorage.setItem(
      "biogarden_privacy_consent",
      JSON.stringify({
        necessary: true,
        analytics: true,
        version: 1,
        updatedAtUtc: new Date().toISOString(),
      }),
    );
  });
}

async function capturedEvents(page: Page): Promise<CapturedAnalyticsEvent[]> {
  return page.evaluate(() => {
    const analyticsWindow = window as typeof window & {
      __biogardenAnalyticsEvents?: CapturedAnalyticsEvent[];
    };

    return analyticsWindow.__biogardenAnalyticsEvents ?? [];
  });
}

async function waitForCapturedEvent(page: Page, name: string): Promise<CapturedAnalyticsEvent> {
  await expect
    .poll(async () => (await capturedEvents(page)).some((event) => event.name === name))
    .toBe(true);

  const events = await capturedEvents(page);
  const event = events.find((candidate) => candidate.name === name);
  if (!event)
    throw new Error(`Analytics event was not captured: ${name}`);

  return event;
}
