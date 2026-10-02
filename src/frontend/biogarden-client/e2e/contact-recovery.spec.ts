import { expect, test } from "@playwright/test";
import { apiBaseUrl, mockPublicShopApi } from "./fixtures";

test("contact retry reuses the submission ID and edited content gets a new ID", async ({ page }) => {
  await mockPublicShopApi(page);
  const requests: { submissionId: string; message: string }[] = [];
  await page.route(`${apiBaseUrl}/api/contact`, async route => {
    requests.push(route.request().postDataJSON());
    if (requests.length < 3) await route.fulfill({ status: 503, json: { detail: "Próbáld újra." } });
    else await route.fulfill({ status: 202, json: { submissionId: requests.at(-1)!.submissionId } });
  });
  await page.goto("/");
  await page.getByRole("button", { name: /Írj nekünk itt|Irj nekunk itt/ }).click();
  await page.locator("#contact-name").fill("Visitor");
  await page.locator("#contact-email").fill("visitor@example.com");
  await page.locator("#contact-message").fill("Please tell me about your garden products.");
  const submit = page.locator("form").filter({ has: page.locator("#contact-message") }).locator('button[type="submit"]');
  await submit.click();
  await expect(page.getByRole("alert").filter({ hasText: "Próbáld újra." })).toBeVisible();
  await submit.click();
  await expect.poll(() => requests.length).toBe(2);
  expect(requests[1].submissionId).toBe(requests[0].submissionId);
  await page.locator("#contact-message").fill("An updated question about the garden products.");
  await submit.click();
  await expect(page.getByText("Köszönjük, rögzítettük az üzenetedet. Az emailes visszaigazolás később érkezhet.")).toBeVisible();
  expect(requests[2].submissionId).not.toBe(requests[0].submissionId);
});
