import { readFile } from "node:fs/promises";
import { resolve } from "node:path";
import { loadEnv } from "vite";
const environment = { ...loadEnv(process.env.MODE ?? "production", process.cwd(), ""), ...process.env };

const siteUrl = (environment.VITE_SITE_URL ?? "https://bakonykincse.hu").replace(/\/$/, "");
const sitemap = await readFile(resolve("dist", "sitemap.xml"), "utf8");
const robots = await readFile(resolve("dist", "robots.txt"), "utf8");

const requiredUrls = [
  "/",
  "/rolunk",
  "/termekek",
  "/tudastar",
  "/altalanos-szerzodesi-feltetelek",
  "/adatkezelesi-tajekoztato",
  "/rendelessel-kapcsolatos-keres",
];

for (const path of requiredUrls) {
  const loc = `<loc>${siteUrl}${path}</loc>`;
  if (!sitemap.includes(loc)) {
    throw new Error(`Missing sitemap URL: ${loc}`);
  }
}

if (!sitemap.startsWith('<?xml version="1.0" encoding="UTF-8"?>')) {
  throw new Error("sitemap.xml must start with an XML declaration.");
}

if (!robots.includes("User-agent: *")) {
  throw new Error("robots.txt is missing a default user-agent rule.");
}

if (!robots.includes("Disallow: /admin") || !robots.includes("Disallow: /fiokom")) {
  throw new Error("robots.txt must disallow private/admin route discovery.");
}

if (!robots.includes(`Sitemap: ${siteUrl}/sitemap.xml`)) {
  throw new Error("robots.txt must point to the sitemap URL.");
}

const index = await readFile(resolve("dist", "index.html"), "utf8");
const expectedRobots = environment.VITE_INDEXING_ENABLED === "true" ? "index, follow" : "noindex, nofollow";
if (!index.includes(`name="robots" content="${expectedRobots}"`))
  throw new Error("Incorrect built indexing policy.");
