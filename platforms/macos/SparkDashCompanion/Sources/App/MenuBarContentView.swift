import AppKit
import Combine
import SwiftUI

struct MenuBarContentView: View {
    @EnvironmentObject private var store: SummaryStore
    private let refreshTimer = Timer.publish(every: 1, on: .main, in: .common).autoconnect()

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            header
            Divider()
            content
            Divider()
            footer
        }
        .padding(16)
        .frame(width: 390)
        .onAppear { store.refresh() }
        .onReceive(refreshTimer) { _ in store.refresh() }
    }

    private var header: some View {
        HStack(spacing: 10) {
            Image(systemName: store.menuBarSymbol)
                .font(.title2)
                .foregroundStyle(statusColor)
            VStack(alignment: .leading, spacing: 2) {
                Text("sparkDash")
                    .font(.headline)
                Text(store.summary?.headline ?? "Connecting…")
                    .font(.subheadline)
                    .foregroundStyle(.secondary)
            }
            Spacer()
            if store.isRefreshing {
                ProgressView()
                    .controlSize(.small)
            }
        }
    }

    @ViewBuilder
    private var content: some View {
        if let summary = store.summary {
            HStack {
                metric(title: "Online", value: summary.onlineCount, color: .green)
                metric(title: "Offline", value: summary.offlineCount, color: .red)
                metric(title: "Total", value: summary.totalCount, color: .secondary)
            }

            if let errorMessage = store.errorMessage {
                Label("Last refresh failed: \(errorMessage)", systemImage: "exclamationmark.triangle")
                    .font(.caption)
                    .foregroundStyle(.orange)
                    .lineLimit(2)
            }

            if summary.units.isEmpty {
                Text("No monitored systems are configured.")
                    .foregroundStyle(.secondary)
            } else {
                VStack(spacing: 0) {
                    ForEach(Array(summary.units.prefix(8))) { unit in
                        UnitStatusRow(unit: unit)
                        if unit.id != summary.units.prefix(8).last?.id {
                            Divider()
                        }
                    }
                }
            }
        } else if let errorMessage = store.errorMessage {
            ContentUnavailableView(
                "sparkDash unavailable",
                systemImage: "network.slash",
                description: Text(errorMessage)
            )
        } else {
            HStack {
                Spacer()
                ProgressView("Loading status…")
                Spacer()
            }
            .padding(.vertical, 20)
        }
    }

    private var footer: some View {
        HStack {
            Button {
                store.openDashboard()
            } label: {
                Label("Open dashboard", systemImage: "arrow.up.forward.app")
            }
            .disabled(store.dashboardURL == nil)

            Spacer()

            Button {
                store.refresh()
            } label: {
                Image(systemName: "arrow.clockwise")
            }
            .help("Refresh now")

            SettingsLink {
                Image(systemName: "gear")
            }
            .help("Settings")

            Button {
                NSApplication.shared.terminate(nil)
            } label: {
                Image(systemName: "power")
            }
            .help("Quit sparkDash Companion")
        }
        .buttonStyle(.borderless)
    }

    private var statusColor: Color {
        switch store.summary?.state {
        case .healthy:
            return .green
        case .degraded:
            return .orange
        case .offline:
            return .red
        case .empty, nil:
            return .secondary
        }
    }

    private func metric(title: String, value: Int, color: Color) -> some View {
        VStack(spacing: 2) {
            Text("\(value)")
                .font(.title2.monospacedDigit().bold())
                .foregroundStyle(color)
            Text(title)
                .font(.caption)
                .foregroundStyle(.secondary)
        }
        .frame(maxWidth: .infinity)
    }
}

private struct UnitStatusRow: View {
    let unit: SparkDashUnitSummary

    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            HStack {
                Circle()
                    .fill(unit.online ? Color.green : Color.red)
                    .frame(width: 8, height: 8)
                Text(unit.name)
                    .fontWeight(.semibold)
                    .lineLimit(1)
                Spacer()
                Text(unit.statusText)
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
            Text("\(unit.gpuUsageText)  ·  \(unit.temperatureText)  ·  \(unit.memoryText)")
                .font(.caption)
                .foregroundStyle(.secondary)
                .lineLimit(1)
            Text(unit.llmText)
                .font(.caption2)
                .foregroundStyle(.tertiary)
        }
        .padding(.vertical, 8)
    }
}
