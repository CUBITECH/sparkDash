import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";
import { VitePWA, type ManifestOptions } from "vite-plugin-pwa";
import path from "path";

const manifest: Partial<ManifestOptions> = {
  id: "/",
  name: "sparkDash",
  short_name: "sparkDash",
  description: "Multi-DGX Spark Monitoring Dashboard",
  lang: "en",
  start_url: "/",
  scope: "/",
  display: "standalone",
  background_color: "#0a0a0a",
  theme_color: "#0a0a0a",
  icons: [
    {
      src: "/pwa-192x192.png",
      sizes: "192x192",
      type: "image/png",
    },
    {
      src: "/pwa-512x512.png",
      sizes: "512x512",
      type: "image/png",
      purpose: "any maskable",
    },
  ],
};

export default defineConfig({
  plugins: [
    react(),
    tailwindcss(),
    VitePWA({
      registerType: "autoUpdate",
      injectRegister: "script",
      workbox: {
        skipWaiting: true,
        clientsClaim: true,
        globPatterns: ["**/*.{js,css,html,png,svg,json,webmanifest}"],
        navigateFallbackDenylist: [/^\/api(?:\/|$)/, /^\/ws(?:\/|$)/],
      },
      manifest,
    }),
  ],
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "./src"),
    },
  },
  server: {
    host: "0.0.0.0",
    port: 5173,
    // Allow HMR when opened via LAN IP / Docker
    watch: {
      usePolling: process.env.CHOKIDAR_USEPOLLING === "1",
    },
    proxy: {
      "/api": "http://127.0.0.1:5555",
      "/ws": {
        target: "ws://127.0.0.1:5555",
        ws: true,
      },
    },
  },
  build: {
    outDir: "dist",
    emptyOutDir: true,
  },
});
