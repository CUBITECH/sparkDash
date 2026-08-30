const REFRESH_AFTER_SECONDS = 1;

function finiteNumber(value) {
  if (
    value == null ||
    typeof value === "boolean" ||
    (typeof value === "string" && value.trim() === "")
  ) {
    return null;
  }
  const number = Number(value);
  return Number.isFinite(number) ? number : null;
}

function rounded(value, digits = 0) {
  const number = finiteNumber(value);
  if (number == null) return null;
  const factor = 10 ** digits;
  return Math.round(number * factor) / factor;
}

function percentage(value) {
  const number = rounded(value);
  if (number == null) return null;
  return Math.min(100, Math.max(0, number));
}

function formatNumber(value, digits = 1) {
  return Number(value).toFixed(digits).replace(/\.0$/, "");
}

function unitName(snapshot) {
  const name = String(snapshot?.name || snapshot?.id || "System").trim();
  return (name || "System").slice(0, 80);
}

function memoryPercentage(metrics) {
  return percentage(
    metrics?.unifiedMemory?.percentage ??
      metrics?.ram?.percentage ??
      metrics?.gpu?.vram?.percentage
  );
}

function llmStatus(metrics) {
  const available = Array.isArray(metrics?.llm)
    ? metrics.llm.filter((entry) => entry?.available)
    : [];
  if (available.length === 0) {
    return {
      llmActive: false,
      llmText: "LLM unavailable",
      generationTps: null,
      llmModel: null,
    };
  }

  const models = [...new Set(
    available
      .map((entry) => String(entry?.modelId || "").trim())
      .filter(Boolean)
  )];
  const joinedModels = models.join(" · ");
  const llmModel = joinedModels
    ? joinedModels.length > 160
      ? `${joinedModels.slice(0, 157)}...`
      : joinedModels
    : null;

  const generationTps = rounded(
    Math.max(0, ...available.map((entry) => finiteNumber(entry.generationTps) ?? 0)),
    1
  );

  const active = available.some((entry) => {
    return (
      (finiteNumber(entry.slotsActive) ?? 0) > 0 ||
      (finiteNumber(entry.generationTps) ?? 0) > 0 ||
      (finiteNumber(entry.prefillTps) ?? 0) > 0
    );
  });
  if (!active) {
    return { llmActive: false, llmText: "LLM idle", generationTps: 0, llmModel };
  }

  return {
    llmActive: true,
    llmText:
      generationTps > 0
        ? `LLM ${formatNumber(generationTps)} tok/s`
        : "LLM active",
    generationTps,
    llmModel,
  };
}

function createUnitSummary(snapshot) {
  const id = String(snapshot?.id || "");
  const online = Boolean(snapshot?.online);
  const metrics = online ? snapshot?.metrics || {} : {};
  const gpuUsage = online ? percentage(metrics?.gpu?.usage) : null;
  const temperatureC = online ? rounded(metrics?.gpu?.temperature, 1) : null;
  const memory = online ? memoryPercentage(metrics) : null;
  const llm = online
    ? llmStatus(metrics)
    : {
        llmActive: false,
        llmText: "LLM unavailable",
        generationTps: null,
        llmModel: null,
      };
  const thermalThrottle = online && metrics?.gpu?.throttle?.thermal === true;

  return {
    id,
    name: unitName(snapshot),
    online,
    statusText: online ? "Online" : "Offline",
    detailPath: `/spark/${encodeURIComponent(id)}`,
    gpuUsage,
    gpuUsageText: gpuUsage == null ? "GPU —" : `GPU ${gpuUsage}%`,
    temperatureC,
    temperatureText:
      temperatureC == null ? "— °C" : `${formatNumber(temperatureC)} °C`,
    memoryPercentage: memory,
    memoryText: memory == null ? "Memory —" : `Memory ${memory}%`,
    thermalThrottle,
    ...llm,
  };
}

function fleetStatus(totalCount, onlineCount) {
  const offlineCount = totalCount - onlineCount;
  if (totalCount === 0) {
    return {
      state: "empty",
      headline: "No systems configured",
      statusText: "Open sparkDash to add a system",
    };
  }
  if (onlineCount === totalCount) {
    return {
      state: "healthy",
      headline: `All ${totalCount} ${totalCount === 1 ? "system" : "systems"} online`,
      statusText: "Live status",
    };
  }
  if (onlineCount === 0) {
    return {
      state: "offline",
      headline: `All ${totalCount} ${totalCount === 1 ? "system" : "systems"} offline`,
      statusText: "Open sparkDash to investigate",
    };
  }
  return {
    state: "degraded",
    headline: `${onlineCount} of ${totalCount} systems online`,
    statusText: `${offlineCount} ${offlineCount === 1 ? "system" : "systems"} offline`,
  };
}

/**
 * Create the intentionally small, read-only contract consumed by companion surfaces.
 * No registry config, network target, credential, or control action is included.
 *
 * @param {object[]} snapshots ordered SparkMonitor snapshots
 * @param {Date} [now]
 */
export function createStatusSummary(snapshots, now = new Date()) {
  const source = Array.isArray(snapshots) ? snapshots : [];
  const units = source.map(createUnitSummary);
  const totalCount = units.length;
  const onlineCount = units.filter((unit) => unit.online).length;
  const offlineCount = totalCount - onlineCount;
  const status = fleetStatus(totalCount, onlineCount);

  return {
    schemaVersion: 1,
    generatedAt: now.toISOString(),
    refreshAfterSeconds: REFRESH_AFTER_SECONDS,
    ...status,
    title: "sparkDash",
    dashboardPath: "/",
    totalCount,
    onlineCount,
    offlineCount,
    units,
  };
}
