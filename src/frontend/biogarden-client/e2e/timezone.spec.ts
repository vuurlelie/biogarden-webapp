import { expect, test } from "./test";
import { mockAccountOrdersApi, signInAsCustomer } from "./fixtures";

test.use({ timezoneId: "America/New_York" });

test("dates use Budapest independently of browser timezone and UTC suffix", async ({ page }) => {
  await signInAsCustomer(page);
  await mockAccountOrdersApi(page);
  await page.goto("/fiokom/rendeleseim");
  const results = await page.evaluate(async () => {
    const path = "/src/shared/utils/dateTime.ts";
    const dates = await import(/* @vite-ignore */ path);
    const labelsPath = "/src/features/account/utils/orderLabels.ts";
    const labels = await import(/* @vite-ignore */ labelsPath);
    return {
      october: labels.formatOrderDate("2026-10-09T05:12:00"),
      summer: labels.formatOrderDate("2026-07-09T05:12:00Z"),
      winter: labels.formatOrderDate("2026-01-09T05:12:00Z"),
      summerInput: dates.budapestInputToUtc("2026-07-09T07:12"),
      winterInput: dates.budapestInputToUtc("2026-01-09T07:12"),
      missing: dates.budapestInputToUtc("2026-03-29T02:30"),
      ambiguous: dates.budapestInputToUtc("2026-10-25T02:30"),
    };
  });
  expect(results.october).toContain("07:12");
  expect(results.summer).toContain("07:12");
  expect(results.winter).toContain("06:12");
  expect(results.summerInput).toBe("2026-07-09T05:12:00.000Z");
  expect(results.winterInput).toBe("2026-01-09T06:12:00.000Z");
  expect(results.missing).toBeNull();
  expect(results.ambiguous).toBeNull();
});
