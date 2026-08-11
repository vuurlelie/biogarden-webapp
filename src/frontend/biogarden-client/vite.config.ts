import { defineConfig, loadEnv } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";

const PRODUCTION_API_BASE_URL = "https://vuurlelie169-001-site1.jtempurl.com";

export default defineConfig(({ mode }) => {
  const apiBaseUrl = mode === "production"
    ? process.env.VITE_API_BASE_URL ?? PRODUCTION_API_BASE_URL
    : loadEnv(mode, process.cwd(), "VITE_").VITE_API_BASE_URL;

  return {
    define: {
      "import.meta.env.VITE_API_BASE_URL": JSON.stringify(apiBaseUrl),
    },
    plugins: [react(), tailwindcss()],
    build: {
      rollupOptions: {
        output: {
          manualChunks(id): string | undefined {
            if (!id.includes("node_modules")) return undefined;
            if (id.includes("@tiptap") || id.includes("prosemirror")) return "editor";
            return "vendor";
          },
        },
      },
    },
  };
});
