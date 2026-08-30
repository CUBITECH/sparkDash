import XCTest
@testable import SparkDashCompanion

final class SparkDashClientTests: XCTestCase {
    func testDecodesStatusSummary() throws {
        let json = """
        {
          "schemaVersion": 1,
          "generatedAt": "2026-08-29T18:00:00.000Z",
          "refreshAfterSeconds": 1,
          "state": "degraded",
          "title": "sparkDash",
          "headline": "1 of 2 systems online",
          "statusText": "1 system offline",
          "dashboardPath": "/",
          "totalCount": 2,
          "onlineCount": 1,
          "offlineCount": 1,
          "units": [
            {
              "id": "spark-a",
              "name": "Spark A",
              "online": true,
              "statusText": "Online",
              "detailPath": "/spark/spark-a",
              "gpuUsage": 67,
              "gpuUsageText": "GPU 67%",
              "temperatureC": 71.2,
              "temperatureText": "71.2 °C",
              "memoryPercentage": 61,
              "memoryText": "Memory 61%",
              "llmActive": true,
              "llmText": "LLM 12.3 tok/s",
              "generationTps": 12.3
            }
          ]
        }
        """

        let summary = try SparkDashClient.decodeSummary(Data(json.utf8))

        XCTAssertEqual(summary.schemaVersion, 1)
        XCTAssertEqual(summary.state, .degraded)
        XCTAssertEqual(summary.onlineCount, 1)
        XCTAssertEqual(summary.units.first?.gpuUsage, 67)
        XCTAssertEqual(summary.units.first?.generationTps, 12.3)
    }

    func testBuildsSummaryEndpoint() throws {
        let endpoint = try SparkDashEndpoint(baseURL: "https://dash.example/spark/")

        XCTAssertEqual(
            endpoint.summaryURL.absoluteString,
            "https://dash.example/spark/api/status/summary"
        )
        XCTAssertEqual(
            endpoint.dashboardURL(path: "/spark/spark-a").absoluteString,
            "https://dash.example/spark/spark/spark-a"
        )
    }

    func testRejectsUnsupportedSchemes() {
        XCTAssertThrowsError(try SparkDashEndpoint(baseURL: "file:///tmp/sparkDash")) { error in
            XCTAssertEqual(error as? SparkDashClientError, .unsupportedScheme)
        }
    }
}
