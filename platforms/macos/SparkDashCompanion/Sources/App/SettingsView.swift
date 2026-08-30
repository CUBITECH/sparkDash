import SwiftUI

struct SettingsView: View {
    @EnvironmentObject private var store: SummaryStore

    var body: some View {
        Form {
            Section("Connection") {
                TextField("sparkDash URL", text: $store.serverURL)
                    .textFieldStyle(.roundedBorder)

                Text("Use localhost for a sparkDash instance on this Mac. Remote instances should use trusted HTTPS or a private network such as Tailscale.")
                    .font(.caption)
                    .foregroundStyle(.secondary)

                if isInsecureRemoteURL {
                    Label("Plain HTTP is intended only for localhost. macOS may block insecure remote requests.", systemImage: "exclamationmark.triangle")
                        .font(.caption)
                        .foregroundStyle(.orange)
                }

                HStack {
                    Button("Test connection") {
                        store.refresh()
                    }
                    Button("Reset to localhost") {
                        store.resetServerURL()
                    }
                }
            }

            if let errorMessage = store.errorMessage {
                Section("Last connection error") {
                    Text(errorMessage)
                        .foregroundStyle(.red)
                        .textSelection(.enabled)
                }
            }
        }
        .formStyle(.grouped)
        .padding()
        .frame(width: 520, height: 280)
    }

    private var isInsecureRemoteURL: Bool {
        guard let components = URLComponents(string: store.serverURL),
              components.scheme?.lowercased() == "http",
              let host = components.host?.lowercased() else {
            return false
        }
        return host != "localhost" && host != "127.0.0.1" && host != "::1"
    }
}
