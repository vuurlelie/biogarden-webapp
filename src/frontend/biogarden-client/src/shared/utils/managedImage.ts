const API_BASE_URL = import.meta.env.VITE_API_BASE_URL;

export function managedImage(path: string) {
  const relative = path.replace(/^\/+/, "");
  const src = `${API_BASE_URL}/${relative}`;
  const match = /^(uploads\/(?:products|posts)\/[a-f0-9]{32})-r([1-9][0-9]{0,3})\.(?:jpg|png|webp)$/.exec(relative);
  if (!match || Number(match[2]) > 4096) return { src };
  const widths = [...new Set([480, 960, 1600].map(width => Math.min(width, Number(match[2]))))];
  return { src, srcSet: widths.map(width => `${API_BASE_URL}/${match[1]}-${width}w.webp ${width}w`).join(", ") };
}
