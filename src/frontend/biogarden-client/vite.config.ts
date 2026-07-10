import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";

export default defineConfig({
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
});
