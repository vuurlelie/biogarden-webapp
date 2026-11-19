import { mkdir, readFile, writeFile } from "node:fs/promises";
import { resolve } from "node:path";
import { resolveBuildEnvironment } from "./build-environment.mjs";

const environment = resolveBuildEnvironment(process.env.MODE ?? "production");
const siteUrl = environment.VITE_SITE_URL;
const apiBaseUrl = environment.VITE_API_BASE_URL;
const requirePostPreviews = environment.VITE_REQUIRE_SEO_POSTS === "true";
const outputDirectory = resolve("dist");
const siteName = "Bakony Kincse Biokertészet";
const defaultDescription =
  "Vegyszermentes, természetközeli szemléletű biokertészet Bakonyszentlászlón.";

const routes = [
  { path: "/", priority: "1.0", changefreq: "weekly" },
  { path: "/rolunk", priority: "0.8", changefreq: "monthly" },
  { path: "/termekek", priority: "0.9", changefreq: "weekly" },
  { path: "/tudastar", priority: "0.7", changefreq: "weekly" },
  { path: "/altalanos-szerzodesi-feltetelek", priority: "0.5", changefreq: "monthly" },
  { path: "/adatkezelesi-tajekoztato", priority: "0.5", changefreq: "monthly" },
  { path: "/rendelessel-kapcsolatos-keres", priority: "0.5", changefreq: "monthly" },
];

function escapeHtml(value) {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}

function absoluteUrl(path) {
  if (/^https?:\/\//i.test(path)) {
    return path;
  }

  return `${siteUrl}${path.startsWith("/") ? path : `/${path}`}`;
}

function absoluteApiUrl(path) {
  if (/^https?:\/\//i.test(path)) {
    return path;
  }

  if (!apiBaseUrl) {
    return absoluteUrl(path);
  }

  return `${apiBaseUrl}/${path.replace(/^\/+/, "")}`;
}

function apiUrl(path) {
  if (!apiBaseUrl) {
    return null;
  }

  const normalizedPath = path.replace(/^\/+/, "");

  if (apiBaseUrl.endsWith("/api") && normalizedPath.startsWith("api/")) {
    return `${apiBaseUrl}/${normalizedPath.slice(4)}`;
  }

  return `${apiBaseUrl}/${normalizedPath}`;
}

function stripHtml(value) {
  return String(value ?? "")
    .replace(/<script\b[^<]*(?:(?!<\/script>)<[^<]*)*<\/script>/gi, " ")
    .replace(/<style\b[^<]*(?:(?!<\/style>)<[^<]*)*<\/style>/gi, " ")
    .replace(/<[^>]*>/g, " ")
    .replace(/\s+/g, " ")
    .trim();
}

function createDescription(post) {
  const text = stripHtml(post.article);

  if (!text) {
    return `${post.title} - ${siteName} tudástár.`;
  }

  const sentences = text
    .match(/[^.!?]+[.!?]+|[^.!?]+$/g)
    ?.map((sentence) => sentence.trim())
    .filter(Boolean);
  const excerpt = sentences?.slice(0, 2).join(" ") || text;

  return excerpt.length > 280 ? `${excerpt.slice(0, 277).trim()}...` : excerpt;
}

function createSitemap(extraRoutes = []) {
  const urls = [...routes, ...extraRoutes]
    .map((route) => `  <url>
    <loc>${absoluteUrl(route.path)}</loc>
    <changefreq>${route.changefreq}</changefreq>
    <priority>${route.priority}</priority>
  </url>`)
    .join("\n");

  return `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
${urls}
</urlset>
`;
}

function createRobots() {
  return `User-agent: *
Allow: /
Disallow: /admin
Disallow: /fiokom

Sitemap: ${absoluteUrl("/sitemap.xml")}
`;
}

async function fetchPublicPosts() {
  if (!apiBaseUrl) {
    console.warn("[seo] VITE_API_BASE_URL is not set, skipping post preview pages.");
    return [];
  }

  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), 5000);

  try {
    const postsEndpoint = apiUrl("/api/posts?page=1&pageSize=200&sort=newest");
    const response = await fetch(
      postsEndpoint,
      {
        signal: controller.signal,
      }
    );

    if (!response.ok) {
      throw new Error(`HTTP ${response.status}`);
    }

    const data = await response.json();
    const posts = Array.isArray(data.items) ? data.items : [];

    return posts.filter((post) => !post.draft);
  } catch (error) {
    console.warn(`[seo] Could not fetch posts for preview pages: ${error.message}`);
    return [];
  } finally {
    clearTimeout(timeout);
  }
}

async function readIndexTemplate() {
  try {
    return await readFile(resolve(outputDirectory, "index.html"), "utf8");
  } catch (error) {
    console.warn(`[seo] Could not read dist/index.html, skipping post preview pages: ${error.message}`);
    return null;
  }
}

function createSeoTags({ title, pageTitle, description, url, imageUrl, publishedAt }) {
  const safeTitle = escapeHtml(title);
  const safePageTitle = escapeHtml(pageTitle ?? title);
  const safeDescription = escapeHtml(description);
  const safeUrl = escapeHtml(url);
  const safeImageUrl = escapeHtml(imageUrl);
  const safePublishedAt = publishedAt ? escapeHtml(publishedAt) : null;

  return [
    `<meta name="description" content="${safeDescription}">`,
    `<link rel="canonical" href="${safeUrl}">`,
    `<meta property="og:site_name" content="${escapeHtml(siteName)}">`,
    `<meta property="og:title" content="${safeTitle}">`,
    `<meta property="og:description" content="${safeDescription}">`,
    '<meta property="og:type" content="article">',
    `<meta property="og:url" content="${safeUrl}">`,
    `<meta property="og:image" content="${safeImageUrl}">`,
    '<meta property="og:image:width" content="1200">',
    '<meta property="og:image:height" content="630">',
    `<meta property="og:image:alt" content="${safeTitle}">`,
    '<meta property="og:locale" content="hu_HU">',
    safePublishedAt ? `<meta property="article:published_time" content="${safePublishedAt}">` : "",
    '<meta name="twitter:card" content="summary_large_image">',
    `<meta name="twitter:title" content="${safeTitle}">`,
    `<meta name="twitter:description" content="${safeDescription}">`,
    `<meta name="twitter:image" content="${safeImageUrl}">`,
    `<meta name="twitter:image:alt" content="${safeTitle}">`,
    `<meta itemprop="name" content="${safePageTitle}">`,
    `<meta itemprop="description" content="${safeDescription}">`,
    `<meta itemprop="image" content="${safeImageUrl}">`,
  ].filter(Boolean).join("\n    ");
}

function createPostHtml(template, post) {
  const title = post.title;
  const pageTitle = `${post.title} | ${siteName}`;
  const description = createDescription(post);
  const url = absoluteUrl(`/tudastar/${post.id}`);
  const imageUrl = absoluteApiUrl(post.image);
  const tags = createSeoTags({
    title,
    pageTitle,
    description,
    url,
    imageUrl,
    publishedAt: post.publishedAt,
  });

  return template
    .replace(/<title>[\s\S]*?<\/title>/i, `<title>${escapeHtml(pageTitle)}</title>`)
    .replace(/<meta\s+name="description"[^>]*>/i, "")
    .replace("</head>", `    ${tags}\n  </head>`);
}

async function writePostPreviewPages(posts) {
  if (posts.length === 0) {
    return;
  }

  const template = await readIndexTemplate();

  if (!template) {
    return;
  }

  await Promise.all(
    posts.map(async (post) => {
      const directory = resolve(outputDirectory, "tudastar", String(post.id));
      await mkdir(
        directory,
        { recursive: true }
      );
      await writeFile(resolve(directory, "index.html"), createPostHtml(template, post), "utf8");
    }),
  );
}

await mkdir(
  outputDirectory,
  { recursive: true }
);
// Apply the same policy when generating previews separately from the Vite build.
const indexPath = resolve(outputDirectory, "index.html");
let index = await readFile(indexPath, "utf8");
const robotsMeta = `<meta name="robots" content="${environment.VITE_INDEXING_ENABLED === "true" ? "index, follow" : "noindex, nofollow"}">`;
index = index.replace(/<meta\s+name="robots"[^>]*>/gi, "").replace("</head>", `${robotsMeta}\n</head>`);
await writeFile(indexPath, index, "utf8");

const posts = await fetchPublicPosts();

if (requirePostPreviews && posts.length === 0) {
  throw new Error("[seo] No public posts were fetched, so Facebook preview pages would be missing.");
}

const postRoutes = posts.map((post) => ({
  path: `/tudastar/${post.id}`,
  priority: "0.6",
  changefreq: "monthly",
}));

await writeFile(resolve(outputDirectory, "sitemap.xml"), createSitemap(postRoutes), "utf8");
await writeFile(resolve(outputDirectory, "robots.txt"), createRobots(), "utf8");
await writePostPreviewPages(posts);
console.info(`[seo] Generated ${posts.length} post preview page(s).`);
