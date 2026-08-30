[CmdletBinding()]
param(
    [ValidateSet('x64', 'arm64')]
    [string]$Architecture = 'x64',
    [switch]$NoStart
)

$ErrorActionPreference = 'Stop'
$nativeRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$publisher = Join-Path $PSScriptRoot 'publish_desktop_tile.ps1'
$publishDirectory = Join-Path $nativeRoot "artifacts\desktop-tile\$Architecture"
$installDirectory = Join-Path $env:LOCALAPPDATA 'Programs\sparkDash Desktop Tile'
$installedExecutable = Join-Path $installDirectory 'SparkDash.DesktopTile.exe'

if (Test-Path $publishDirectory) {
    Remove-Item $publishDirectory -Recurse -Force
}

& $publisher -Architecture $Architecture -OutputDirectory $publishDirectory | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw "Desktop tile publish helper failed with exit code $LASTEXITCODE."
}

$publishedExecutable = Join-Path $publishDirectory 'SparkDash.DesktopTile.exe'
if (-not (Test-Path $publishedExecutable)) {
    throw "Desktop tile executable was not created: $publishedExecutable"
}

$runningProcesses = @(Get-Process 'SparkDash.DesktopTile' -ErrorAction SilentlyContinue)
if ($runningProcesses.Count -gt 0) {
    $runningProcesses | Stop-Process -Force
    $runningProcesses | Wait-Process -Timeout 10 -ErrorAction SilentlyContinue
    $stopDeadline = [DateTime]::UtcNow.AddSeconds(10)
    while ((Get-Process 'SparkDash.DesktopTile' -ErrorAction SilentlyContinue) -and
        [DateTime]::UtcNow -lt $stopDeadline) {
        Start-Sleep -Milliseconds 250
    }
    if (Get-Process 'SparkDash.DesktopTile' -ErrorAction SilentlyContinue) {
        throw 'The running desktop tile did not stop before the update.'
    }
}

New-Item -ItemType Directory -Path $installDirectory -Force | Out-Null
for ($attempt = 1; $attempt -le 8; $attempt++) {
    try {
        Get-ChildItem $installDirectory -Force -ErrorAction SilentlyContinue |
            Remove-Item -Recurse -Force
        break
    }
    catch {
        if ($attempt -eq 8) {
            throw
        }
        Start-Sleep -Milliseconds (250 * $attempt)
    }
}
Copy-Item (Join-Path $publishDirectory '*') $installDirectory -Recurse -Force

$startMenu = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
$shortcutPath = Join-Path $startMenu 'sparkDash Desktop Tile.lnk'
$wshShell = New-Object -ComObject WScript.Shell
try {
    $shortcut = $wshShell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $installedExecutable
    $shortcut.WorkingDirectory = $installDirectory
    $shortcut.IconLocation = "$installedExecutable,0"
    $shortcut.Description = 'Freely placeable local sparkDash status tile'
    $shortcut.Save()
}
finally {
    [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($wshShell)
}

if (-not $NoStart) {
    Start-Process $installedExecutable -WorkingDirectory $installDirectory
}

Write-Host "Installed sparkDash Desktop Tile: $installedExecutable"
Write-Host "Drag the header to move it; use the tray menu for visibility, topmost mode, and autostart."
