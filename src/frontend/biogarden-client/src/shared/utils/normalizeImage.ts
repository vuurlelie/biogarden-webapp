const MAX_SOURCE_BYTES = 20 * 1024 * 1024;
export const MAX_IMAGE_UPLOAD_BYTES = 2 * 1024 * 1024;

/** Centre-crop to the requested ratio, without enlarging the source. */
export async function normalizeImage(file: File, maxWidth: number, maxHeight: number): Promise<File> {
  if (!file.size || file.size > MAX_SOURCE_BYTES)
    throw new Error("A kiválasztott kép mérete legfeljebb 20 MB lehet.");
  if (!["image/jpeg", "image/png", "image/webp"].includes(file.type))
    throw new Error("Csak JPG, PNG vagy WEBP kép választható.");
  const url = await new Promise<string>((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(String(reader.result));
    reader.onerror = () => reject(new Error("A kiválasztott kép nem tölthető be."));
    reader.readAsDataURL(file);
  });
    const image = new Image();
    image.src = url;
    await image.decode();
    const ratio = maxWidth / maxHeight;
    const sourceWidth = Math.min(image.naturalWidth, image.naturalHeight * ratio);
    const sourceHeight = sourceWidth / ratio;
    const scale = Math.min(1, maxWidth / sourceWidth);
    const canvas = document.createElement("canvas");
    canvas.width = Math.max(1, Math.floor(sourceWidth * scale));
    canvas.height = Math.max(1, Math.floor(sourceHeight * scale));
    const context = canvas.getContext("2d");
    if (!context) throw new Error("A kép feldolgozása nem támogatott ebben a böngészőben.");
    context.fillStyle = "#ffffff";
    context.fillRect(0, 0, canvas.width, canvas.height);
    context.drawImage(image, (image.naturalWidth - sourceWidth) / 2, (image.naturalHeight - sourceHeight) / 2,
      sourceWidth, sourceHeight, 0, 0, canvas.width, canvas.height);
    for (const quality of [0.9, 0.82, 0.74, 0.66]) {
      const blob = await new Promise<Blob>((resolve, reject) => canvas.toBlob(
        result => result ? resolve(result) : reject(new Error("A kép feldolgozása nem sikerült.")), "image/jpeg", quality));
      if (blob.size <= MAX_IMAGE_UPLOAD_BYTES)
        return new File([blob], "image.jpg", { type: "image/jpeg" });
    }
    throw new Error("A kép mérete feldolgozás után is túl nagy. Válassz kisebb képet.");
}
