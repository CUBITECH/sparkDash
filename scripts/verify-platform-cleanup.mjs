import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(__dirname, "..");
const obsoletePaths = [
  "public/widgets",
  "server/widgets",
  "platforms/windows/SparkDashWidgetProvider",
  "scripts/verify-windows-widget.mjs",
  "scripts/verify-native-windows-widget.mjs",
  "docs/windows-store-widget.md",
  ".github/workflows/platform-widgets.yml",
];

for (const obsoletePath of obsoletePaths) {
  assert.ok(
    !fs.existsSync(path.join(root, obsoletePath)),
    `obsolete widget artifact remains: ${obsoletePath}`
  );
}

const packageJson = fs.readFileSync(path.join(root, "package.json"), "utf8");
const packageData = JSON.parse(packageJson);
assert.match(packageData.scripts["verify:platform-companions"], /^npm run build && /);
assert.doesNotMatch(packageJson, /verify:(?:windows-widget|native-windows-widget|platform-widgets)/);
assert.doesNotMatch(packageJson, /SparkDashWidgetProvider|SparkDash\.WidgetProvider/);

const workflow = fs.readFileSync(
  path.join(root, ".github", "workflows", "platform-companions.yml"),
  "utf8"
);
assert.doesNotMatch(workflow, /push:\s*\r?\n\s+branches:/);

const viteConfig = fs.readFileSync(path.join(root, "vite.config.ts"), "utf8");
assert.doesNotMatch(viteConfig, /WindowsWidget|sparkdash-status|ms_ac_template|\bwidgets\b/);

console.log("Legacy Windows widget artifacts removed");
