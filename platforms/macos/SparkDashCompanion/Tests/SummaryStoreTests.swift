import XCTest
@testable import SparkDashCompanion

private actor RefreshGate {
    private var continuations: [String: CheckedContinuation<SparkDashSummary, Error>] = [:]

    func fetch(_ serverURL: String) async throws -> SparkDashSummary {
        try await withCheckedThrowingContinuation { continuation in
            continuations[serverURL] = continuation
        }
    }

    func hasRequest(for serverURL: String) -> Bool {
        continuations[serverURL] != nil
    }

    func complete(_ serverURL: String, with summary: SparkDashSummary) {
        continuations.removeValue(forKey: serverURL)?.resume(returning: summary)
    }
}

private enum SummaryStoreTestError: Error {
    case requestDidNotStart(String)
}

final class SummaryStoreTests: XCTestCase {
    @MainActor
    func testChangingServerURLDiscardsInFlightResult() async throws {
        let suiteName = "SummaryStoreTests-\(UUID().uuidString)"
        let defaults = try XCTUnwrap(UserDefaults(suiteName: suiteName))
        defer { defaults.removePersistentDomain(forName: suiteName) }
        let gate = RefreshGate()
        let store = SummaryStore(
            defaults: defaults,
            fetchSummary: { serverURL in
                try await gate.fetch(serverURL)
            }
        )
        let serverA = "https://a.example"
        let serverB = "https://b.example"

        store.serverURL = serverA
        let requestA = store.refresh()
        try await waitForRequest(serverA, in: gate)

        store.serverURL = serverB
        XCTAssertNil(store.summary)
        let requestB = store.refresh()
        try await waitForRequest(serverB, in: gate)

        await gate.complete(serverB, with: summary(headline: "Server B"))
        await requestB?.value
        XCTAssertEqual(store.summary?.headline, "Server B")
        XCTAssertEqual(store.dashboardURL?.host, "b.example")

        await gate.complete(serverA, with: summary(headline: "Server A"))
        await requestA?.value
        XCTAssertEqual(store.summary?.headline, "Server B")
        XCTAssertEqual(store.dashboardURL?.host, "b.example")
    }

    private func waitForRequest(_ serverURL: String, in gate: RefreshGate) async throws {
        for _ in 0..<1_000 {
            if await gate.hasRequest(for: serverURL) {
                return
            }
            await Task.yield()
        }
        throw SummaryStoreTestError.requestDidNotStart(serverURL)
    }

    private func summary(headline: String) -> SparkDashSummary {
        SparkDashSummary(
            schemaVersion: 1,
            generatedAt: "2026-08-29T18:00:00.000Z",
            refreshAfterSeconds: 1,
            state: .healthy,
            title: "sparkDash",
            headline: headline,
            statusText: "Live status",
            dashboardPath: "/",
            totalCount: 0,
            onlineCount: 0,
            offlineCount: 0,
            units: []
        )
    }
}
