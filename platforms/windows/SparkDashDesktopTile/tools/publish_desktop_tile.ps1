[CmdletBinding()]
param(
    [ValidateSet('x64', 'arm64')]
    [string]$Architecture = 'x64',
    [Parameter(Mandatory)]
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$nativeRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$stagingRoot = Join-Path ([IO.Path]::GetTempPath()) "sparkdash-desktop-tile-$([Guid]::NewGuid().ToString('N'))"
$output = [IO.Path]::GetFullPath($OutputDirectory)

function Test-StrictDescendant {
    param(
        [string]$Candidate,
        [string]$AllowedRoot
    )

    $root = [IO.Path]::GetFullPath($AllowedRoot).TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar)
    $prefix = $root + [IO.Path]::DirectorySeparatorChar
    return $Candidate.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)
}

$allowedOutputRoots = @(
    (Join-Path $nativeRoot 'artifacts'),
    [IO.Path]::GetTempPath()
)
if (-not [string]::IsNullOrWhiteSpace($env:RUNNER_TEMP)) {
    $allowedOutputRoots += $env:RUNNER_TEMP
}
$outputIsAllowed = $allowedOutputRoots |
    Where-Object { Test-StrictDescendant -Candidate $output -AllowedRoot $_ } |
    Select-Object -First 1
if (-not $outputIsAllowed) {
    throw "Refusing to delete or publish to an unsafe output directory: $output"
}

try {
    New-Item -ItemType Directory -Path $stagingRoot -Force | Out-Null
    foreach ($directory in @('SparkDash.DesktopTile', 'SparkDash.DesktopTile.Core', 'SparkDash.StatusCore')) {
        Copy-Item (Join-Path $nativeRoot $directory) $stagingRoot -Recurse -Force
    }

    Get-ChildItem $stagingRoot -Directory -Recurse |
        Where-Object { $_.Name -in @('bin', 'obj') } |
        Sort-Object FullName -Descending |
        Remove-Item -Recurse -Force
    Get-ChildItem $stagingRoot -Filter 'packages.lock.json' -File -Recurse |
        Remove-Item -Force

    if (Test-Path $output) {
        Remove-Item $output -Recurse -Force
    }
    New-Item -ItemType Directory -Path $output -Force | Out-Null

    $stagedProject = Join-Path $stagingRoot 'SparkDash.DesktopTile\SparkDash.DesktopTile.csproj'
    & dotnet publish $stagedProject `
        --configuration Release `
        --runtime "win-$Architecture" `
        --self-contained true `
        --output $output `
        -p:Platform=$Architecture `
        -p:RestorePackagesWithLockFile=false
    if ($LASTEXITCODE -ne 0) {
        throw "Desktop tile publish failed with exit code $LASTEXITCODE."
    }

    $executable = Join-Path $output 'SparkDash.DesktopTile.exe'
    if (-not (Test-Path $executable)) {
        throw "Desktop tile executable was not created: $executable"
    }

    Write-Output $executable
}
finally {
    Remove-Item $stagingRoot -Recurse -Force -ErrorAction SilentlyContinue
}
