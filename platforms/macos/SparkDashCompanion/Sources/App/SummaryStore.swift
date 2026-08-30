import AppKit
import Combine
import Foundation

@MainActor
final class SummaryStore: ObservableObject {
    private static let defaultServerURL = "http://127.0.0.1:5555"
    private static let serverURLKey = "sparkDashServerURL"

    private let defaults: UserDefaults
    private let fetchSummary: @Sendable (String) async throws -> SparkDashSummary
    private var refreshTask: Task<Void, Never>?
    private var refreshGeneration = 0
    private var activeRefreshURL: String?

    @Published var serverURL: String {
        didSet {
            defaults.set(serverURL, forKey: Self.serverURLKey)
            guard oldValue != serverURL else { return }

            refreshGeneration += 1
            refreshTask?.cancel()
            refreshTask = nil
            activeRefreshURL = nil
            isRefreshing = false
            summary = nil
            errorMessage = nil
        }
    }
    @Published private(set) var summary: SparkDashSummary?
    @Published private(set) var errorMessage: String?
    @Published private(set) var isRefreshing = false

    init(
        defaults: UserDefaults = .standard,
        fetchSummary: @escaping @Sendable (String) async throws -> SparkDashSummary = { serverURL in
            try await SparkDashClient().fetchSummary(baseURL: serverURL)
        }
    ) {
        self.defaults = defaults
        self.fetchSummary = fetchSummary
        serverURL = defaults.string(forKey: Self.serverURLKey) ?? Self.defaultServerURL
    }

    var menuBarSymbol: String {
        switch summary?.state {
        case .healthy:
            return "bolt.horizontal.circle.fill"
        case .degraded:
            return "exclamationmark.triangle.fill"
        case .offline:
            return "xmark.circle.fill"
        case .empty:
            return "minus.circle.fill"
        case nil:
            return "bolt.horizontal.circle"
        }
    }

    var dashboardURL: URL? {
        try? SparkDashEndpoint(baseURL: serverURL).dashboardURL()
    }

    @discardableResult
    func refresh() -> Task<Void, Never>? {
        let requestedURL = serverURL
        if isRefreshing, activeRefreshURL == requestedURL {
            return refreshTask
        }

        refreshGeneration += 1
        let generation = refreshGeneration
        refreshTask?.cancel()
        activeRefreshURL = requestedURL
        isRefreshing = true
        let fetchSummary = self.fetchSummary

        let task = Task { [weak self] in
            do {
                let nextSummary = try await fetchSummary(requestedURL)
                guard let self, !Task.isCancelled else { return }
                guard self.isCurrentRefresh(generation: generation, serverURL: requestedURL) else {
                    return
                }
                self.summary = nextSummary
                self.errorMessage = nil
                self.finishRefresh(generation: generation, serverURL: requestedURL)
            } catch is CancellationError {
                guard let self else { return }
                self.finishRefresh(generation: generation, serverURL: requestedURL)
            } catch {
                guard let self, !Task.isCancelled else { return }
                guard self.isCurrentRefresh(generation: generation, serverURL: requestedURL) else {
                    return
                }
                self.errorMessage = error.localizedDescription
                self.finishRefresh(generation: generation, serverURL: requestedURL)
            }
        }
        refreshTask = task
        return task
    }

    func openDashboard() {
        guard let dashboardURL else { return }
        NSWorkspace.shared.open(dashboardURL)
    }

    func resetServerURL() {
        serverURL = Self.defaultServerURL
        refresh()
    }

    private func isCurrentRefresh(generation: Int, serverURL requestedURL: String) -> Bool {
        generation == refreshGeneration && requestedURL == serverURL
    }

    private func finishRefresh(generation: Int, serverURL requestedURL: String) {
        guard isCurrentRefresh(generation: generation, serverURL: requestedURL) else { return }
        activeRefreshURL = nil
        isRefreshing = false
    }
}
