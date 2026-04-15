import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  build: {
    outDir: "../api/wwwroot",
    emptyOutDir: true,
  },
  clearScreen: false,
  server: {
    port: 8099,
    proxy: {
      "/api": {
        target: "http://localhost:8100",
        changeOrigin: true,
      },
      "/swagger": {
        target: "http://localhost:8100",
        changeOrigin: true,
      },
    },
  },
});
