import { readFileSync } from "node:fs";
import { defineConfig, mergeConfig } from "vite";
import base from "./vite.config";

export default defineConfig(async env => {
  if (!process.env.E2E_TLS_CERT || !process.env.E2E_TLS_KEY) throw new Error("Use the isolated .NET browser harness.");
  const config = typeof base === "function" ? await base(env) : await base;
  return mergeConfig(config, { server: { https: {
    cert: readFileSync(process.env.E2E_TLS_CERT), key: readFileSync(process.env.E2E_TLS_KEY),
  } } });
});
