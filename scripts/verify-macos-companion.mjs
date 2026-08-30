import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(__dirname, "..");
const macRoot = path.join(root, "platforms", "macos", "SparkDashCompanion");

function read(relativePath) {
  return fs.readFileSync(path.join(macRoot, relativePath), "utf8");
}

const project = read("project.yml");
for (const target of ["SparkDashCompanion:", "SparkDashWidgetExtension:", "SparkDashCompanionTests:"]) {
  assert.ok(project.includes(target), `project.yml must declare ${target}`);
}
assert.match(project, /platform:\s*macOS/);
assert.match(project, /deploymentTarget:\s*["']?14\.0/);
assert.match(project, /com\.apple\.widgetkit-extension/);
assert.equal(
  (project.match(/NSLocalNetworkUsageDescription/g) ?? []).length,
  2,
  "app and widget extension must explain local-network access"
);

const models = read("Sources/Shared/SparkDashModels.swift");
assert.match(models, /struct SparkDashSummary/);
assert.match(models, /struct SparkDashUnitSummary/);
assert.match(models, /refreshAfterSeconds/);
assert.doesNotMatch(models, /widgetUnits|moreCount|moreLabel/);

const client = read("Sources/Shared/SparkDashClient.swift");
assert.match(client, /api\/status\/summary/);
assert.match(client, /reloadIgnoringLocalAndRemoteCacheData/);
assert.doesNotMatch(client, /shutdown|wake|update-hermes/i);

const app = read("Sources/App/SparkDashCompanionApp.swift");
assert.match(app, /MenuBarExtra/);
assert.match(app, /menuBarExtraStyle\(\.window\)/);
const menu = read("Sources/App/MenuBarContentView.swift");
assert.match(menu, /Timer\.publish\(every:\s*1/);
assert.match(menu, /SettingsLink/);
assert.match(menu, /Last refresh failed/);
const store = read("Sources/App/SummaryStore.swift");
assert.match(store, /refreshGeneration/);
assert.match(store, /Task\.isCancelled/);

const widget = read("Sources/Widget/SparkDashStatusWidget.swift");
assert.match(widget, /AppIntentConfiguration/);
assert.match(widget, /AppIntentTimelineProvider/);
assert.match(widget, /\.systemSmall/);
assert.match(widget, /\.systemMedium/);
assert.match(widget, /\.systemLarge/);
assert.match(widget, /\.after\(/);
assert.match(widget, /refreshAfterSeconds \?\? 1/);
assert.match(widget, /max\(1, requestedInterval\)/);
assert.doesNotMatch(widget, /900/);

const tests = read("Tests/SparkDashClientTests.swift");
assert.match(tests, /testDecodesStatusSummary/);
assert.match(tests, /testBuildsSummaryEndpoint/);
assert.match(tests, /testRejectsUnsupportedSchemes/);
const storeTests = read("Tests/SummaryStoreTests.swift");
assert.match(storeTests, /testChangingServerURLDiscardsInFlightResult/);

for (const entitlement of ["Config/App.entitlements", "Config/Widget.entitlements"]) {
  const contents = read(entitlement);
  assert.match(contents, /com\.apple\.security\.app-sandbox/);
  assert.match(contents, /com\.apple\.security\.network\.client/);
}

const workflow = fs.readFileSync(path.join(root, ".github", "workflows", "platform-companions.yml"), "utf8");
assert.match(workflow, /macos-15/);
assert.match(workflow, /xcodegen generate/);
assert.match(workflow, /xcodebuild/);
assert.match(workflow, /docs\/platform-companions\.md/);

const guide = fs.readFileSync(path.join(root, "docs", "platform-companions.md"), "utf8");
assert.match(guide, /directly on the macOS desktop/i);
assert.match(guide, /xcodegen generate/);
assert.match(guide, /\/api\/status\/summary/);
assert.match(guide, /requests a new timeline after one second/i);
assert.match(guide, /15 to 60 minutes/i);
assert.match(guide, /do not expose port 5555/i);

console.log("macOS companion source artifacts verified");
