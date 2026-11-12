import { loadEnv } from "vite";

function origin(value, name) {
  let url;
  try { url = new URL(value); } catch { throw new Error(`${name} must be an absolute HTTP(S) origin.`); }
  if (!["http:", "https:"].includes(url.protocol) || url.username || url.password ||
      url.pathname !== "/" || url.search || url.hash)
    throw new Error(`${name} must be an HTTP(S) origin without credentials, path, query or fragment.`);
  if (url.protocol !== "https:" && !["localhost", "127.0.0.1", "[::1]"].includes(url.hostname))
    throw new Error(`${name} requires HTTPS outside loopback development.`);
  return url.origin;
}

export function resolveBuildEnvironment(mode, directory = process.cwd()) {
  const environment = { ...loadEnv(mode, directory, "VITE_"), ...process.env };
  const hosted = mode === "production";
  return {
    ...environment,
    VITE_API_BASE_URL: origin(environment.VITE_API_BASE_URL ??
      (hosted ? "https://api.raczkrisztina.dev" : "http://localhost:5097"), "VITE_API_BASE_URL"),
    VITE_SITE_URL: origin(environment.VITE_SITE_URL ??
      (hosted ? "https://raczkrisztina.dev" : "http://localhost:5173"), "VITE_SITE_URL"),
    VITE_INDEXING_ENABLED: environment.VITE_INDEXING_ENABLED ?? "false",
  };
}
