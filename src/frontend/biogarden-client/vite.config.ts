import { resolveBuildEnvironment } from "./scripts/build-environment.mjs";
import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";

const securityHeaders = {
  "Content-Security-Policy": "frame-ancestors 'none'",
  "X-Frame-Options": "DENY",
  "X-Content-Type-Options": "nosniff",
  "Referrer-Policy": "strict-origin-when-cross-origin",
  "Permissions-Policy": "camera=(), microphone=(), geolocation=(self), payment=()",
};

export default defineConfig(({ mode }) => {
  const env = resolveBuildEnvironment(mode);
  const indexingEnabled = env.VITE_INDEXING_ENABLED === "true";


  return {
    server: { headers: securityHeaders },
    preview: { headers: securityHeaders },
    define: {
      "import.meta.env.VITE_API_BASE_URL": JSON.stringify(env.VITE_API_BASE_URL),
      "import.meta.env.VITE_SITE_URL": JSON.stringify(env.VITE_SITE_URL),
    },
    plugins: [react(), tailwindcss(), {
      name: "indexing-policy",
      transformIndexHtml: () => [{ tag: "meta", attrs: { name: "robots", content: indexingEnabled ? "index, follow" : "noindex, nofollow" }, injectTo: "head" }],
    }],
    build: {
      rollupOptions: {
        output: {
          manualChunks(id): string | undefined {
            if (!id.includes("node_modules"))
              return undefined;
            if (id.includes("@tiptap") || id.includes("prosemirror"))
              return "editor";
            return "vendor";
          },
        },
      },
    },
  };
});
