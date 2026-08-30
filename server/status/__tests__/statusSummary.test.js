import test from "node:test";
import assert from "node:assert/strict";
import { createStatusSummary } from "../statusSummary.js";

const NOW = new Date("2026-08-29T18:00:00.000Z");

function snapshot(overrides = {}) {
  return {
    id: "spark-a",
    name: "Spark A",
    online: true,
    metrics: {
      gpu: {
        usage: 67.4,
        temperature: 71.24,
        vram: { percentage: 54.8 },
        throttle: { thermal: false },
      },
      ram: { percentage: 48.2 },
      unifiedMemory: { percentage: 61.4 },
      llm: [
        {
          available: true,
          modelId: "deepseek-v4-flash-0731",
          slotsActive: 2,
          generationTps: 12.34,
          prefillTps: 0,
        },
      ],
    },
    ...overrides,
  };
}

test("createStatusSummary returns a stable empty-fleet contract", () => {
  assert.deepEqual(createStatusSummary([], NOW), {
    schemaVersion: 1,
    generatedAt: "2026-08-29T18:00:00.000Z",
    refreshAfterSeconds: 1,
    state: "empty",
    title: "sparkDash",
    headline: "No systems configured",
    statusText: "Open sparkDash to add a system",
    dashboardPath: "/",
    totalCount: 0,
    onlineCount: 0,
    offlineCount: 0,
    units: [],
  });
});

test("createStatusSummary exposes only compact read-only metrics", () => {
  const result = createStatusSummary(
    [
      snapshot({
        privateToken: "must-not-leak",
        ssh: { password: "must-not-leak" },
      }),
      snapshot({
        id: "spark/b",
        name: " Spark B ",
        online: false,
        metrics: {
          gpu: { usage: 99, temperature: 88, vram: { percentage: 90 } },
          unifiedMemory: { percentage: 95 },
          llm: [{ available: true, slotsActive: 3, generationTps: 50 }],
        },
      }),
    ],
    NOW
  );

  assert.equal(result.state, "degraded");
  assert.equal(result.headline, "1 of 2 systems online");
  assert.equal(result.statusText, "1 system offline");
  assert.equal(result.onlineCount, 1);
  assert.equal(result.offlineCount, 1);
  assert.deepEqual(result.units[0], {
    id: "spark-a",
    name: "Spark A",
    online: true,
    statusText: "Online",
    detailPath: "/spark/spark-a",
    gpuUsage: 67,
    gpuUsageText: "GPU 67%",
    temperatureC: 71.2,
    temperatureText: "71.2 °C",
    memoryPercentage: 61,
    memoryText: "Memory 61%",
    llmActive: true,
    llmText: "LLM 12.3 tok/s",
    generationTps: 12.3,
    llmModel: "deepseek-v4-flash-0731",
    thermalThrottle: false,
  });
  assert.deepEqual(result.units[1], {
    id: "spark/b",
    name: "Spark B",
    online: false,
    statusText: "Offline",
    detailPath: "/spark/spark%2Fb",
    gpuUsage: null,
    gpuUsageText: "GPU —",
    temperatureC: null,
    temperatureText: "— °C",
    memoryPercentage: null,
    memoryText: "Memory —",
    llmActive: false,
    llmText: "LLM unavailable",
    generationTps: null,
    llmModel: null,
    thermalThrottle: false,
  });
  assert.doesNotMatch(JSON.stringify(result), /must-not-leak/);
});

test("createStatusSummary keeps the full list without widget-specific projections", () => {
  const snapshots = Array.from({ length: 6 }, (_, index) =>
    snapshot({ id: `spark-${index + 1}`, name: `Spark ${index + 1}` })
  );

  const result = createStatusSummary(snapshots, NOW);

  assert.equal(result.state, "healthy");
  assert.equal(result.headline, "All 6 systems online");
  assert.deepEqual(
    result.units.map((unit) => unit.id),
    ["spark-1", "spark-2", "spark-3", "spark-4", "spark-5", "spark-6"]
  );
  assert.equal("widgetUnits" in result, false);
  assert.equal("moreCount" in result, false);
  assert.equal("moreLabel" in result, false);
});

test("createStatusSummary reports thermal throttling and available LLM models", () => {
  const result = createStatusSummary(
    [
      snapshot({
        metrics: {
          gpu: { throttle: { thermal: true } },
          llm: [
            { available: true, modelId: "model-a", slotsActive: 1, generationTps: 8 },
            { available: true, modelId: "model-b", slotsActive: 0, generationTps: 0 },
          ],
        },
      }),
    ],
    NOW
  );

  assert.equal(result.units[0].thermalThrottle, true);
  assert.equal(result.units[0].llmModel, "model-a · model-b");
});

test("createStatusSummary treats null and blank metrics as unavailable", () => {
  const result = createStatusSummary(
    [
      snapshot({
        metrics: {
          gpu: { usage: null, temperature: "", vram: { percentage: null } },
          ram: { percentage: null },
          unifiedMemory: null,
          llm: [{ available: true, slotsActive: null, generationTps: "", prefillTps: null }],
        },
      }),
    ],
    NOW
  );

  assert.equal(result.units[0].gpuUsage, null);
  assert.equal(result.units[0].temperatureC, null);
  assert.equal(result.units[0].memoryPercentage, null);
  assert.equal(result.units[0].llmActive, false);
  assert.equal(result.units[0].llmText, "LLM idle");
  assert.equal(result.units[0].generationTps, 0);
});

test("createStatusSummary normalizes invalid metrics and reports an idle LLM", () => {
  const result = createStatusSummary(
    [
      snapshot({
        metrics: {
          gpu: { usage: Number.NaN, temperature: Number.POSITIVE_INFINITY, vram: {} },
          ram: { percentage: 32.6 },
          unifiedMemory: null,
          llm: [{ available: true, slotsActive: 0, generationTps: 0, prefillTps: 0 }],
        },
      }),
    ],
    NOW
  );

  assert.equal(result.units[0].gpuUsage, null);
  assert.equal(result.units[0].temperatureC, null);
  assert.equal(result.units[0].memoryPercentage, 33);
  assert.equal(result.units[0].llmActive, false);
  assert.equal(result.units[0].llmText, "LLM idle");
  assert.equal(result.units[0].generationTps, 0);
});
