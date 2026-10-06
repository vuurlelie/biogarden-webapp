import defaultImage from "../../assets/images/logo.png";
const SITE_NAME = "Bakony Kincse Biokertészet";
const DEFAULT_DESCRIPTION =
  "Bakony Kincse Biokertészet: természetes termékek, gyógynövények, teák, illóolajok és hidrolátumok Bakonyszentlászlóról.";
const DEFAULT_IMAGE = defaultImage;
const SITE_URL = (import.meta.env.VITE_SITE_URL as string | undefined)?.replace(/\/$/, "");

export type SeoProps = {
  title?: string;
  description?: string;
  image?: string;
  type?: "website" | "article" | "product";
  noIndex?: boolean;
  structuredData?: Record<string, unknown> | Array<Record<string, unknown>>;
};

function absoluteUrl(path: string) {
  if (/^https?:\/\//i.test(path)) return path;
  const origin = SITE_URL ?? window.location.origin;
  return `${origin}${path.startsWith("/") ? path : `/${path}`}`;
}

function upsertMeta(attribute: "name" | "property", key: string, content: string) {
  let element = document.head.querySelector<HTMLMetaElement>(`meta[${attribute}="${key}"]`);

  if (!element) {
    element = document.createElement("meta");
    element.setAttribute(attribute, key);
    document.head.appendChild(element);
  }

  element.content = content;
}

function upsertCanonical(href: string) {
  let element = document.head.querySelector<HTMLLinkElement>('link[rel="canonical"]');

  if (!element) {
    element = document.createElement("link");
    element.rel = "canonical";
    document.head.appendChild(element);
  }

  element.href = href;
}

function upsertStructuredData(data?: SeoProps["structuredData"]) {
  const elementId = "biogarden-structured-data";
  let element = document.head.querySelector<HTMLScriptElement>(`#${elementId}`);

  if (!data) {
    element?.remove();
    return;
  }

  if (!element) {
    element = document.createElement("script");
    element.id = elementId;
    element.type = "application/ld+json";
    document.head.appendChild(element);
  }

  element.textContent = JSON.stringify(data);
}

export function applyMetadata({ title, description = DEFAULT_DESCRIPTION, image = DEFAULT_IMAGE, type = "website", noIndex = false, structuredData }: SeoProps, path: string) {
  const pageTitle = title ? `${title} | ${SITE_NAME}` : SITE_NAME;
  const canonicalUrl = absoluteUrl(path);
  const imageUrl = absoluteUrl(image);
    document.documentElement.lang = "hu";
    document.title = pageTitle;

    upsertMeta("name", "description", description);
    upsertMeta("name", "robots", (noIndex || import.meta.env.VITE_INDEXING_ENABLED !== "true") ? "noindex, nofollow" : "index, follow");
    upsertMeta("property", "og:site_name", SITE_NAME);
    upsertMeta("property", "og:title", pageTitle);
    upsertMeta("property", "og:description", description);
    upsertMeta("property", "og:type", type);
    upsertMeta("property", "og:url", canonicalUrl);
    upsertMeta("property", "og:image", imageUrl);
    upsertMeta("name", "twitter:card", "summary_large_image");
    upsertMeta("name", "twitter:title", pageTitle);
    upsertMeta("name", "twitter:description", description);
    upsertMeta("name", "twitter:image", imageUrl);
    upsertCanonical(canonicalUrl);
    upsertStructuredData(structuredData);
}
