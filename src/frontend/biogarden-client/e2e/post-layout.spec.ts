import { expect, test } from "./test";
import { apiBaseUrl, mockEmptyCartApi, mockKnowledgePostsApi } from "./fixtures";

for (const width of [390, 1440]) {
  for (const long of [false, true]) {
    test(`contact follows the full ${long ? "long" : "short"} article at ${width}px`, async ({ page }) => {
      await page.setViewportSize({ width, height: 900 });
      await mockEmptyCartApi(page);
      await mockKnowledgePostsApi(page);
      await page.route(`${apiBaseUrl}/api/posts/1`, route => route.fulfill({ json: {
        id: 1, title: "Layout test article", category: 0, image: "uploads/posts/defaultimg.png", publishedAt: "2026-07-20T10:00:00Z", draft: false, article: long ? "<p>Long article content.</p>".repeat(80) : "<p>Short article content.</p>",
      } }));
      await page.goto("/tudastar/1");
      const image = page.getByRole("img", { name: "Layout test article", exact: true });
      const text = page.locator(".rich-text-content");
      await expect(image).toBeVisible();
      await expect(text).toBeVisible();
      const imageBox = (await image.boundingBox())!;
      const textBox = (await text.boundingBox())!;
      const contactBox = (await page.locator("#contact").boundingBox())!;
      expect(contactBox.y).toBeGreaterThanOrEqual(Math.max(imageBox.y + imageBox.height, textBox.y + textBox.height));
    });
  }
}
