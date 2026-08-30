# Windows desktop tile

The sparkDash Desktop Tile is a small, borderless Windows status window that can be placed anywhere on the desktop. It is an ordinary per-user desktop application. It requires no Microsoft Store publication and no Developer Mode.

## Features

- freely draggable, resizable dark status tile;
- optional **always on top** mode;
- remembers position, size, topmost state, and autostart preference;
- tray menu for show/hide, topmost mode, Windows startup, dashboard, and exit;
- refreshes the read-only local summary every second;
- plots a rolling 60-second generation-tokens-per-second sparkline for each displayed system;
- shows the available LLM model identifier for each system;
- outlines the complete system row in red while GPU thermal throttling is active;
- explicit offline state when sparkDash is not running;
- opens the full dashboard on demand.

The tile only reads:

```text
http://127.0.0.1:5555/api/status/summary
```

It disables redirects and proxy use through the shared loopback-only client. It never calls shutdown, wake, SSH, update, or other control routes. The backend remains local and must be running for live data.

## Build and test

From the repository root on Windows:

```powershell
npm run test:windows-desktop-tile
npm run build:windows-desktop-tile
npm run verify:windows-desktop-tile
```

## Install locally

```powershell
Set-Location platforms\windows\SparkDashDesktopTile
powershell -ExecutionPolicy Bypass -File .\tools\install_desktop_tile.ps1
```

The installer publishes a self-contained x64 executable, copies it to:

```text
%LOCALAPPDATA%\Programs\sparkDash Desktop Tile\
```

and creates a Start-menu shortcut. No administrator permission, certificate, Store account, or Developer Mode is required. For Windows on ARM64, add `-Architecture arm64`.

## Use

- Drag the header area to move the tile.
- Drag the lower-right grip to resize it.
- Select **PIN** to toggle always-on-top mode.
- Select **—** to hide the tile to the notification area.
- Right-click the tray icon for show/hide, always-on-top, startup, dashboard, or exit.
- Launching the Start-menu shortcut again brings the existing hidden tile back instead of starting a duplicate.
- The startup option writes only the current executable path to the current user's standard Windows `Run` key.

Settings are stored at:

```text
%LOCALAPPDATA%\sparkDash\desktop-tile.json
```

## Uninstall

```powershell
Set-Location platforms\windows\SparkDashDesktopTile
powershell -ExecutionPolicy Bypass -File .\tools\uninstall_desktop_tile.ps1
```

Pass `-RemoveSettings` to remove the saved position and preferences as well.
