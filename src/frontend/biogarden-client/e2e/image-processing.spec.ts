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
