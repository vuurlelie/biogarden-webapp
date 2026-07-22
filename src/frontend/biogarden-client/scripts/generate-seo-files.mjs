import { mkdir, writeFile } from "node:fs/promises";
import { resolve } from "node:path";

const siteUrl = (process.env.VITE_SITE_URL ?? "https://bakonykincse.hu").replace(/\/$/, "");
const outputDirectory = resolve("dist");

const routes = [
  { path: "/", priority: "1.0", changefreq: "weekly" },
  { path: "/rolunk", priority: "0.8", changefreq: "monthly" },
  { path: "/termekek", priority: "0.9", changefreq: "weekly" },
  { path: "/tudastar", priority: "0.7", changefreq: "weekly" },
  { path: "/altalanos-szerzodesi-feltetelek", priority: "0.5", changefreq: "monthly" },
  { path: "/adatkezelesi-tajekoztato", priority: "0.5", changefreq: "monthly" },
  { path: "/rendelessel-kapcsolatos-keres", priority: "0.5", changefreq: "monthly" },
];

function absoluteUrl(path) {
  return `${siteUrl}${path}`;
}

function createSitemap() {
  const urls = routes
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

await mkdir(outputDirectory, { recursive: true });
await writeFile(resolve(outputDirectory, "sitemap.xml"), createSitemap(), "utf8");
await writeFile(resolve(outputDirectory, "robots.txt"), createRobots(), "utf8");
