import Foundation

enum SparkDashFleetState: String, Codable, Sendable {
    case empty
    case healthy
    case degraded
    case offline
}

struct SparkDashUnitSummary: Codable, Hashable, Identifiable, Sendable {
    let id: String
    let name: String
    let online: Bool
    let statusText: String
    let detailPath: String
    let gpuUsage: Double?
    let gpuUsageText: String
    let temperatureC: Double?
    let temperatureText: String
    let memoryPercentage: Double?
    let memoryText: String
    let llmActive: Bool
    let llmText: String
    let generationTps: Double?
}

struct SparkDashSummary: Codable, Equatable, Sendable {
    let schemaVersion: Int
    let generatedAt: String
    let refreshAfterSeconds: Int
    let state: SparkDashFleetState
    let title: String
    let headline: String
    let statusText: String
    let dashboardPath: String
    let totalCount: Int
    let onlineCount: Int
    let offlineCount: Int
    let units: [SparkDashUnitSummary]

    static let preview = SparkDashSummary(
        schemaVersion: 1,
        generatedAt: "2026-08-29T18:00:00.000Z",
        refreshAfterSeconds: 1,
        state: .healthy,
        title: "sparkDash",
        headline: "All 2 systems online",
        statusText: "Live status",
        dashboardPath: "/",
        totalCount: 2,
        onlineCount: 2,
        offlineCount: 0,
        units: [
            SparkDashUnitSummary(
                id: "spark-a",
                name: "DGX Spark A",
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
                generationTps: 12.3
            ),
            SparkDashUnitSummary(
                id: "spark-b",
                name: "DGX Spark B",
                online: true,
                statusText: "Online",
                detailPath: "/spark/spark-b",
                gpuUsage: 34,
                gpuUsageText: "GPU 34%",
                temperatureC: 58.4,
                temperatureText: "58.4 °C",
                memoryPercentage: 44,
                memoryText: "Memory 44%",
                llmActive: false,
                llmText: "LLM idle",
                generationTps: nil
            ),
        ]
    )
}
