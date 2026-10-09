import { expect, test } from "./test";
import { apiBaseUrl, mockEmptyCartApi, product, signInAsAdmin } from "./fixtures";

test("article readers render the supported sanitized editor formatting", async ({ page }) => {
  await mockEmptyCartApi(page);
  // Sanitization itself is exercised through the real API and SQL in ArticleContentTests.
  await page.route(
    `${apiBaseUrl}/api/posts/1`,
    route => route.fulfill({ json: {
      id: 1, title: "Garden article", category: 0, image: "uploads/posts/defaultimg.png",
      publishedAt: "2026-10-01T10:00:00Z", draft: false,
      article: '<h2>Harvest advice</h2><p style="text-align: center"><span style="font-family: Georgia; font-size: 1.125rem; color: rgba(10, 20, 30, 1)"><strong>Fresh garden vegetables</strong></span></p><ol start="3"><li>Water carefully</li></ol><a href="https://example.com">Read more</a>',
    } })
  );
  await page.goto("/tudastar/1");
  const article = page.locator(".rich-text-content");
  await expect(article.getByRole(
    "heading",
    { name: "Harvest advice" }
  )).toBeVisible();
  await expect(article.locator("p")).toHaveCSS("text-align", "center");
  await expect(article.locator("span")).toHaveCSS("color", "rgb(10, 20, 30)");
  await expect(article.locator("strong")).toHaveText("Fresh garden vegetables");
  await expect(article.locator("ol")).toHaveAttribute("start", "3");
  await expect(article.getByRole(
    "link",
    { name: "Read more" }
  )).toHaveAttribute("href", "https://example.com");
});

for (const kind of ["products", "posts"] as const) {
  test(
    `${kind} failed save retains the uploaded image without requesting deletion`,
    async ({ page }) => {
      await signInAsAdmin(page);
      await mockEmptyCartApi(page);
      const record = kind === "products"
        ? { ...product, description: "Original description", stockQuantity: 8 }
        : { id: 1, title: "Harvest article", category: 0, article: "Fresh vegetables from our garden every day.", image: "uploads/posts/defaultimg.png", publishedAt: "2026-10-01T10:00:00Z", draft: true };
      await page.route(
        url => url.pathname === `/api/admin/${kind}`,
        route => route.fulfill({ json: {
          items: [record], page: 1, pageSize: 12, totalCount: 1, totalPages: 1,
        } })
      );
      let uploads = 0;
      let deletions = 0;
      await page.route(
        `${apiBaseUrl}/api/admin/${kind}/image`,
        async route => {
          if (route.request().method() === "DELETE")
            deletions++;
          else
            uploads++;
          await route.fulfill({ status: 201, json: { path: `/uploads/${kind}/retained.png` } });
        }
      );
      await page.route(
        `${apiBaseUrl}/api/admin/${kind}/1`,
        route => route.fulfill({ status: 503, json: { detail: "A mentés eredménye nem ismert." } })
      );
      await page.goto(kind === "products" ? "/admin/termekek" : "/admin/bejegyzesek");
      await page.getByRole(
        "button",
        { name: "Szerkesztés", exact: true }
      ).click();
      const dialog = page.getByRole("dialog");
      // A genuine browser-generated PNG also exercises the form's image resizing path.
      const bytes = await page.evaluate(() => {
        const canvas = document.createElement("canvas");
        canvas.width = canvas.height = 16;
        const context = canvas.getContext("2d")!;
        context.fillStyle = "green";
        context.fillRect(0, 0, 16, 16);
        return canvas.toDataURL("image/png").split(",")[1];
      });
      await dialog.locator('input[type="file"]').setInputFiles({ name: "garden.png", mimeType: "image/png", buffer: Buffer.from(bytes, "base64") });
      await expect(dialog.locator('img[src^="data:image/"]')).toBeVisible();
      await dialog.getByRole(
        "button",
        { name: "Mentés", exact: true }
      ).click();
      await expect(dialog.getByRole("alert")).toContainText("A mentés eredménye nem ismert.");
      expect(uploads).toBe(1);
      expect(deletions).toBe(0);
    }
  );
}
