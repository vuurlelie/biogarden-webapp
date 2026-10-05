import { expect, test } from "@playwright/test";
import { mockPublicShopApi } from "./fixtures";

test("admin image processing crops without upscaling and rejects oversized sources before decoding", async ({ page }) => {
  await mockPublicShopApi(page);
  await page.goto("/termekek");
  const result = await page.evaluate(async () => {
    const modulePath = "/src/shared/utils/normalizeImage.ts";
    const { normalizeImage } = await import(/* @vite-ignore */ modulePath);
    const canvas = document.createElement("canvas");
    canvas.width = 400; canvas.height = 300;
    canvas.getContext("2d")!.fillRect(0, 0, 400, 300);
    const blob = await new Promise<Blob>(resolve => canvas.toBlob(value => resolve(value!), "image/png"));
    const file = new File([blob], "small.png", { type: "image/png" });
    const product = await createImageBitmap(await normalizeImage(file, 1200, 1200));
    const post = await createImageBitmap(await normalizeImage(file, 1600, 1200));
    let error = "";
    try { await normalizeImage(new File([new Uint8Array(20 * 1024 * 1024 + 1)], "large.jpg", { type: "image/jpeg" }), 1200, 1200); }
    catch (failure) { error = (failure as Error).message; }
    return { product: [product.width, product.height], post: [post.width, post.height], error };
  });
  expect(result.product).toEqual([300, 300]);
  expect(result.post).toEqual([400, 300]);
  expect(result.error).toContain("20 MB");
});

test("responsive image paths distinguish new uploads from legacy files", async ({ page }) => {
  await mockPublicShopApi(page);
  await page.goto("/termekek");
  const images = await page.evaluate(async () => {
    const modulePath = "/src/shared/utils/managedImage.ts";
    const { managedImage } = await import(/* @vite-ignore */ modulePath);
    const stem = "/uploads/products/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    return [managedImage(`${stem}.jpg`), managedImage(`${stem}-r1200.jpg`), managedImage(`${stem}-r300.png`)];
  });
  expect(images[0].srcSet).toBeUndefined();
  expect(images[1].srcSet).toContain("-480w.webp 480w");
  expect(images[1].srcSet).toContain("-960w.webp 960w");
  expect(images[1].srcSet).toContain("-1200w.webp 1200w");
  expect(images[2].srcSet.split(", ")).toHaveLength(1);
  expect(images[2].srcSet).toContain("-300w.webp 300w");
});

for (const width of [390, 1440]) {
  test(`homepage uses decoded responsive images at ${width}px`, async ({ page }) => {
    await mockPublicShopApi(page);
    await page.setViewportSize({ width, height: 900 });
    await page.goto("/");
    const first = page.getByRole("img", { name: "Biogarden slide 1", exact: true });
    await expect(first).toHaveAttribute("fetchpriority", "high");
    await expect.poll(() => first.evaluate((img: HTMLImageElement) => img.naturalWidth)).toBeGreaterThan(0);
    expect(await first.evaluate((img: HTMLImageElement) => img.currentSrc)).toContain(width < 768 ? "firstcarousel-768w.webp" : "firstcarousel-1664w.webp");
    await page.screenshot({ path: `test-results/home-images-${width}.png`, fullPage: true });
  });
}
