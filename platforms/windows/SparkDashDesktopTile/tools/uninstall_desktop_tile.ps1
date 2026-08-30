[CmdletBinding()]
param(
    [switch]$RemoveSettings
)

$ErrorActionPreference = 'Stop'
$installDirectory = Join-Path $env:LOCALAPPDATA 'Programs\sparkDash Desktop Tile'
$shortcutPath = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\sparkDash Desktop Tile.lnk'

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
        throw 'The running desktop tile did not stop before uninstall.'
    }
}

Remove-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' `
    -Name 'sparkDash Desktop Tile' `
    -ErrorAction SilentlyContinue
Remove-Item $shortcutPath -Force -ErrorAction SilentlyContinue
if (Test-Path $installDirectory) {
    for ($attempt = 1; $attempt -le 8; $attempt++) {
        try {
            Remove-Item $installDirectory -Recurse -Force -ErrorAction Stop
            break
        }
        catch {
            if ($attempt -eq 8) {
                throw
            }
            Start-Sleep -Milliseconds (250 * $attempt)
        }
    }
}
if (Test-Path $installDirectory) {
    throw "Desktop tile installation directory still exists: $installDirectory"
}

if ($RemoveSettings) {
    Remove-Item (Join-Path $env:LOCALAPPDATA 'sparkDash\desktop-tile.json') `
        -Force `
        -ErrorAction SilentlyContinue
}

Write-Host 'Uninstalled sparkDash Desktop Tile.'
