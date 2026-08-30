import AppIntents
import SwiftUI
import WidgetKit

struct SparkDashWidgetConfigurationIntent: WidgetConfigurationIntent {
    static var title: LocalizedStringResource = "sparkDash server"
    static var description = IntentDescription("Choose the sparkDash server shown by this widget.")

    @Parameter(title: "Server URL", default: "http://127.0.0.1:5555")
    var serverURL: String
}

struct SparkDashWidgetEntry: TimelineEntry {
    let date: Date
    let serverURL: String
    let summary: SparkDashSummary?
    let errorMessage: String?
}

struct SparkDashTimelineProvider: AppIntentTimelineProvider {
    func placeholder(in context: Context) -> SparkDashWidgetEntry {
        SparkDashWidgetEntry(
            date: Date(),
            serverURL: "http://127.0.0.1:5555",
            summary: .preview,
            errorMessage: nil
        )
    }

    func snapshot(
        for configuration: SparkDashWidgetConfigurationIntent,
        in context: Context
    ) async -> SparkDashWidgetEntry {
        if context.isPreview {
            return placeholder(in: context)
        }
        return await loadEntry(serverURL: configuration.serverURL)
    }

    func timeline(
        for configuration: SparkDashWidgetConfigurationIntent,
        in context: Context
    ) async -> Timeline<SparkDashWidgetEntry> {
        let entry = await loadEntry(serverURL: configuration.serverURL)
        let requestedInterval = entry.summary?.refreshAfterSeconds ?? 1
        let nextRefresh = Date().addingTimeInterval(TimeInterval(max(1, requestedInterval)))
        return Timeline(entries: [entry], policy: .after(nextRefresh))
    }

    private func loadEntry(serverURL: String) async -> SparkDashWidgetEntry {
        do {
            let summary = try await SparkDashClient().fetchSummary(baseURL: serverURL)
            return SparkDashWidgetEntry(
                date: Date(),
                serverURL: serverURL,
                summary: summary,
                errorMessage: nil
            )
        } catch {
            return SparkDashWidgetEntry(
                date: Date(),
                serverURL: serverURL,
                summary: nil,
                errorMessage: error.localizedDescription
            )
        }
    }
}

struct SparkDashStatusWidget: Widget {
    private let kind = "SparkDashStatusWidget"

    var body: some WidgetConfiguration {
        AppIntentConfiguration(
            kind: kind,
            intent: SparkDashWidgetConfigurationIntent.self,
            provider: SparkDashTimelineProvider()
        ) { entry in
            SparkDashWidgetEntryView(entry: entry)
        }
        .configurationDisplayName("sparkDash status")
        .description("Shows system availability, GPU load, memory, and LLM activity.")
        .supportedFamilies([.systemSmall, .systemMedium, .systemLarge])
    }
}

private struct SparkDashWidgetEntryView: View {
    let entry: SparkDashWidgetEntry
    @Environment(\.widgetFamily) private var family

    var body: some View {
        Group {
            if let summary = entry.summary {
                summaryContent(summary)
            } else {
                unavailableContent
            }
        }
        .containerBackground(for: .widget) {
            Color(red: 0.055, green: 0.059, blue: 0.066)
        }
        .widgetURL(dashboardURL)
    }

    private func summaryContent(_ summary: SparkDashSummary) -> some View {
        VStack(alignment: .leading, spacing: family == .systemSmall ? 6 : 8) {
            HStack {
                Label("sparkDash", systemImage: "bolt.horizontal.circle.fill")
                    .font(.headline)
                    .foregroundStyle(stateColor(summary.state))
                Spacer()
                Text("\(summary.onlineCount)/\(summary.totalCount)")
                    .font(.caption.monospacedDigit().bold())
                    .foregroundStyle(.secondary)
            }

            Text(summary.headline)
                .font(family == .systemSmall ? .subheadline : .title3)
                .fontWeight(.bold)
                .lineLimit(2)

            if family == .systemSmall {
                if let unit = summary.units.first {
                    compactUnit(unit)
                } else {
                    Text(summary.statusText)
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
            } else {
                VStack(spacing: 5) {
                    ForEach(Array(summary.units.prefix(rowLimit))) { unit in
                        widgetUnitRow(unit)
                    }
                }
                if summary.units.count > rowLimit {
                    Text("+\(summary.units.count - rowLimit) more")
                        .font(.caption2)
                        .foregroundStyle(.secondary)
                }
            }

            Spacer(minLength: 0)
            Text(entry.date, style: .time)
                .font(.caption2)
                .foregroundStyle(.tertiary)
        }
        .padding(14)
    }

    private var unavailableContent: some View {
        VStack(alignment: .leading, spacing: 8) {
            Label("sparkDash", systemImage: "network.slash")
                .font(.headline)
                .foregroundStyle(.red)
            Text("Dashboard unavailable")
                .font(.title3.bold())
            Text(entry.errorMessage ?? "Check the configured server URL.")
                .font(.caption)
                .foregroundStyle(.secondary)
                .lineLimit(family == .systemSmall ? 3 : 5)
            Spacer()
            Text("Click to open sparkDash")
                .font(.caption2)
                .foregroundStyle(.tertiary)
        }
        .padding(14)
    }

    private func compactUnit(_ unit: SparkDashUnitSummary) -> some View {
        VStack(alignment: .leading, spacing: 3) {
            HStack(spacing: 5) {
                Circle()
                    .fill(unit.online ? Color.green : Color.red)
                    .frame(width: 7, height: 7)
                Text(unit.name)
                    .font(.caption.bold())
                    .lineLimit(1)
            }
            Text("\(unit.gpuUsageText) · \(unit.temperatureText)")
                .font(.caption2)
                .foregroundStyle(.secondary)
                .lineLimit(1)
            Text(unit.llmText)
                .font(.caption2)
                .foregroundStyle(.tertiary)
                .lineLimit(1)
        }
    }

    private func widgetUnitRow(_ unit: SparkDashUnitSummary) -> some View {
        VStack(alignment: .leading, spacing: 2) {
            HStack(spacing: 5) {
                Circle()
                    .fill(unit.online ? Color.green : Color.red)
                    .frame(width: 7, height: 7)
                Text(unit.name)
                    .font(.caption.bold())
                    .lineLimit(1)
                Spacer()
                Text(unit.statusText)
                    .font(.caption2)
                    .foregroundStyle(.secondary)
            }
            Text("\(unit.gpuUsageText)  ·  \(unit.temperatureText)  ·  \(unit.memoryText)")
                .font(.caption2)
                .foregroundStyle(.secondary)
                .lineLimit(1)
        }
    }

    private var rowLimit: Int {
        family == .systemLarge ? 4 : 2
    }

    private var dashboardURL: URL? {
        let path = entry.summary?.dashboardPath ?? "/"
        return try? SparkDashEndpoint(baseURL: entry.serverURL).dashboardURL(path: path)
    }

    private func stateColor(_ state: SparkDashFleetState) -> Color {
        switch state {
        case .healthy:
            return .green
        case .degraded:
            return .orange
        case .offline:
            return .red
        case .empty:
            return .secondary
        }
    }
}
