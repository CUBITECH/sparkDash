import Foundation

enum SparkDashClientError: Error, Equatable, LocalizedError {
    case invalidBaseURL
    case unsupportedScheme
    case invalidResponse
    case httpStatus(Int)
    case unsupportedSchema(Int)

    var errorDescription: String? {
        switch self {
        case .invalidBaseURL:
            return "Enter a valid sparkDash URL."
        case .unsupportedScheme:
            return "The sparkDash URL must use HTTP or HTTPS."
        case .invalidResponse:
            return "sparkDash returned an invalid response."
        case .httpStatus(let status):
            return "sparkDash returned HTTP \(status)."
        case .unsupportedSchema(let version):
            return "This companion does not support summary schema \(version)."
        }
    }
}

struct SparkDashEndpoint: Equatable, Sendable {
    let baseURL: URL

    init(baseURL rawValue: String) throws {
        let value = rawValue.trimmingCharacters(in: .whitespacesAndNewlines)
        guard var components = URLComponents(string: value),
              let scheme = components.scheme?.lowercased() else {
            throw SparkDashClientError.invalidBaseURL
        }
        guard scheme == "http" || scheme == "https" else {
            throw SparkDashClientError.unsupportedScheme
        }
        guard components.host != nil else {
            throw SparkDashClientError.invalidBaseURL
        }

        components.scheme = scheme
        components.query = nil
        components.fragment = nil
        guard let url = components.url else {
            throw SparkDashClientError.invalidBaseURL
        }
        baseURL = url
    }

    var summaryURL: URL {
        appending(path: "/api/status/summary")
    }

    func dashboardURL(path: String = "/") -> URL {
        appending(path: path)
    }

    private func appending(path relativePath: String) -> URL {
        var components = URLComponents(url: baseURL, resolvingAgainstBaseURL: false)!
        let basePath = components.path.trimmingCharacters(in: CharacterSet(charactersIn: "/"))
        let path = relativePath.trimmingCharacters(in: CharacterSet(charactersIn: "/"))
        components.path = "/" + [basePath, path].filter { !$0.isEmpty }.joined(separator: "/")
        components.query = nil
        components.fragment = nil
        return components.url!
    }
}

struct SparkDashClient: Sendable {
    static func decodeSummary(_ data: Data) throws -> SparkDashSummary {
        let summary = try JSONDecoder().decode(SparkDashSummary.self, from: data)
        guard summary.schemaVersion == 1 else {
            throw SparkDashClientError.unsupportedSchema(summary.schemaVersion)
        }
        return summary
    }

    func fetchSummary(baseURL: String) async throws -> SparkDashSummary {
        let endpoint = try SparkDashEndpoint(baseURL: baseURL)
        var request = URLRequest(
            url: endpoint.summaryURL,
            cachePolicy: .reloadIgnoringLocalAndRemoteCacheData,
            timeoutInterval: 10
        )
        request.setValue("application/json", forHTTPHeaderField: "Accept")

        let (data, response) = try await URLSession.shared.data(for: request)
        guard let httpResponse = response as? HTTPURLResponse else {
            throw SparkDashClientError.invalidResponse
        }
        guard (200..<300).contains(httpResponse.statusCode) else {
            throw SparkDashClientError.httpStatus(httpResponse.statusCode)
        }
        return try Self.decodeSummary(data)
    }
}
