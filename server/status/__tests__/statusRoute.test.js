import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const serverEntry = fs.readFileSync(path.resolve(__dirname, "../../index.js"), "utf8");

test("server exposes the read-only companion status endpoint without the legacy widget route", () => {
  assert.match(serverEntry, /createStatusSummary/);
  assert.match(serverEntry, /app\.get\("\/api\/status\/summary"/);
  assert.match(serverEntry, /req\.path\.startsWith\("\/api\/"\)/);
  assert.match(serverEntry, /status\(404\)\.json\(\{ error: "API route not found" \}\)/);
  assert.doesNotMatch(serverEntry, /\/api\/widget\/summary/);
});
