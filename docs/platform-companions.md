# Windows and macOS companion surfaces

sparkDash provides two glanceable, read-only native companions without exposing control actions:

- a freely placeable Windows desktop tile with tray and per-user autostart controls;
- a macOS 14+ companion containing a WidgetKit desktop widget and a live menu-bar panel.

Both companions consume `GET /api/status/summary`. The full React dashboard and its WebSocket remain unchanged.

## Shared status endpoint

Build and run the normal production server:

```sh
npm ci
npm run build
npm start
```

Verify the read-only payload:

```sh
curl http://127.0.0.1:5555/api/status/summary
```

The response contains availability, GPU, temperature, memory, and LLM status only. It deliberately excludes SSH configuration, secrets, and every mutation or power action.

For a companion on another machine, bind sparkDash only to a trusted interface and put it behind trusted HTTPS or a private-network reverse proxy. **Do not expose port 5555** directly to the public internet: other unauthenticated sparkDash routes include control operations.

## Windows desktop tile

The Windows implementation is an ordinary .NET 8 WPF desktop application. It does not register a Windows Widgets provider, use Store packaging, or require Developer Mode.

See [Windows desktop tile](./windows-desktop-tile.md) for installation, controls, security boundaries, and removal.

Useful checks:

```sh
npm run test:windows-desktop-tile
npm run build:windows-desktop-tile
npm run verify:windows-desktop-tile
```

## macOS companion and WidgetKit extension

The macOS implementation intentionally keeps WidgetKit. Apple supports placing widgets manually and directly on the macOS desktop, so this surface already provides the placement model that the Windows desktop tile had to implement as a normal app window. The menu-bar panel remains available for a denser live view.

Apple's current placement instructions: <https://support.apple.com/guide/mac-help/add-and-customize-widgets-mchl52be5da5/mac>

### Requirements

- macOS 14 Sonoma or newer
- Xcode 15 or newer
- [XcodeGen](https://github.com/yonaskolb/XcodeGen)
- a running sparkDash backend

Install XcodeGen and generate the project:

```sh
brew install xcodegen
cd platforms/macos/SparkDashCompanion
xcodegen generate
open SparkDashCompanion.xcodeproj
```

In Xcode:

1. Select the **SparkDashCompanion** scheme and **My Mac** destination.
2. If Xcode requests signing, choose your local development team for the app and widget targets.
3. Run the app once. It appears in the menu bar and intentionally has no Dock icon. Allow the local-network prompt so the companion and widget can reach sparkDash.
4. Open the gear button and set the sparkDash URL. The default is `http://127.0.0.1:5555`.
5. Control-click the desktop, choose **Edit Widgets**, search for **sparkDash status**, and drag it to any position on the desktop.
6. Control-click the added widget, choose **Edit “sparkDash status”**, and set its server URL.

The menu-bar URL and WidgetKit URL are configured separately. This avoids requiring an Apple App Group entitlement for a local test build.

### Refresh behavior

- The open menu-bar panel polls every second.
- WidgetKit requests a new timeline after one second. This is only a request: WidgetKit applies a dynamic daily refresh budget, typically resulting in actual widget reloads roughly every 15 to 60 minutes, and macOS may defer them further. Use the menu-bar panel when true one-second updates are required. See [Apple's WidgetKit refresh guidance](https://developer.apple.com/documentation/widgetkit/keeping-a-widget-up-to-date).
- Clicking the WidgetKit widget opens the configured sparkDash dashboard.

Local HTTP is permitted for localhost. For another computer, use trusted HTTPS; the project does not disable App Transport Security globally. A Tailscale Serve or reverse-proxy HTTPS endpoint is preferable to exposing the Express listener.

### Build and test from Terminal

After `xcodegen generate`:

```sh
xcodebuild \
  -project SparkDashCompanion.xcodeproj \
  -scheme SparkDashCompanion \
  -destination 'platform=macOS' \
  -derivedDataPath "$TMPDIR/SparkDashDerivedData" \
  CODE_SIGNING_ALLOWED=NO \
  test
```

Source-level checks that also run on Windows or Linux:

```sh
npm run verify:macos-companion
npm run verify:platform-companions
```

The CI workflow `.github/workflows/platform-companions.yml` generates the Xcode project and runs the native tests on a `macos-15` runner.

## Troubleshooting

- **Companion has no data:** open `.../api/status/summary` from the same machine and confirm that JSON is returned.
- **Remote URL fails on macOS:** use HTTPS. Plain LAN-IP HTTP is intentionally not granted a global ATS exception.
- **macOS widget is absent:** build and run the containing app once, then reopen the desktop widget gallery.
- **Generated Xcode project is stale:** remove the generated project locally and rerun `xcodegen generate`; the `.xcodeproj` is intentionally not committed.
