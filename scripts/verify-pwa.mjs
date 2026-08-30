import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(__dirname, "..");
const dist = path.join(root, "dist");
const manifest = JSON.parse(
  fs.readFileSync(path.join(dist, "manifest.webmanifest"), "utf8")
);
assert.equal(manifest.name, "sparkDash");
assert.ok(!Object.hasOwn(manifest, "widgets"), "PWA manifest must not register Windows widgets");

const viteConfig = fs.readFileSync(path.join(root, "vite.config.ts"), "utf8");
assert.match(viteConfig, /skipWaiting:\s*true/);
assert.match(viteConfig, /clientsClaim:\s*true/);

const serviceWorker = fs.readFileSync(path.join(dist, "sw.js"), "utf8");
assert.doesNotMatch(
  serviceWorker,
  /widgetinstall|widgetresume|widgetuninstall|widgetclick|sparkdash-status|\/api\/widget\/summary/i
);
assert.match(serviceWorker, /clientsClaim\(\)/);
assert.match(serviceWorker, /self\.skipWaiting\(\)/);
assert.match(serviceWorker, /index\.html/);

for (const obsoletePath of [
  path.join(dist, "widgets", "windows", "sparkdash-status.json"),
  path.join(dist, "widgets", "windows", "sparkdash-status.png"),
]) {
  assert.ok(!fs.existsSync(obsoletePath), `obsolete Windows widget artifact remains: ${obsoletePath}`);
}

console.log("Standard PWA build artifacts verified");
