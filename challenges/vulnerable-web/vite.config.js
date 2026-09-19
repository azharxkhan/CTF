import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// Built output is served by the .NET API at /board (same origin as /api and /collect).
export default defineConfig({
  plugins: [react()],
  base: "/board/",
  build: {
    outDir: "../vulnerable-api/wwwroot/board",
    emptyOutDir: true,
  },
  server: {
    // Dev-only convenience: proxy API calls to the running .NET instance.
    proxy: {
      "/api": "http://localhost:5006",
      "/collect": "http://localhost:5006",
    },
  },
});
