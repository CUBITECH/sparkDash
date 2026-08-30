import SwiftUI

@main
struct SparkDashCompanionApp: App {
    @StateObject private var store = SummaryStore()

    var body: some Scene {
        MenuBarExtra {
            MenuBarContentView()
                .environmentObject(store)
        } label: {
            Label("sparkDash", systemImage: store.menuBarSymbol)
        }
        .menuBarExtraStyle(.window)

        Settings {
            SettingsView()
                .environmentObject(store)
        }
    }
}
