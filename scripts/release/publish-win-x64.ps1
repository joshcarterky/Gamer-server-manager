Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$version = "0.2.0"
$releaseName = "NexusServerManager-v$version-win-x64-portable"
$publishDir = Join-Path $root "dist\GameServerManager-win-x64"
$portableDir = Join-Path $root "dist\GameServerManager-portable"
$zipPath = Join-Path $root "dist\$releaseName.zip"
$releaseDir = Join-Path $root "releases\v$version"
$releaseZipPath = Join-Path $releaseDir "$releaseName.zip"
$checksumsPath = Join-Path $releaseDir "checksums.txt"
$notesPath = Join-Path $releaseDir "release-notes.md"

Push-Location $root
try {
    if (Test-Path $publishDir) {
        Remove-Item -LiteralPath $publishDir -Recurse -Force
    }

    if (Test-Path $portableDir) {
        Remove-Item -LiteralPath $portableDir -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null

    dotnet build GameServerManager.sln --configuration Release
    dotnet run --project tests\GameServerManager.ProviderTests\GameServerManager.ProviderTests.csproj --configuration Release

    dotnet publish src\GameServerManager.App\GameServerManager.App.csproj `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -o $publishDir

    New-Item -ItemType Directory -Force -Path $portableDir | Out-Null
    Copy-Item -Path (Join-Path $publishDir "GameServerManager.App.exe") `
        -Destination (Join-Path $portableDir "GameServerManager.exe") `
        -Force
    Copy-Item -Path "README.md" -Destination (Join-Path $portableDir "README.md") -Force
    Copy-Item -Path "CHANGELOG.md" -Destination (Join-Path $portableDir "CHANGELOG.md") -Force
    Copy-Item -Path "LICENSE" -Destination (Join-Path $portableDir "LICENSE") -Force

    Compress-Archive -Path (Join-Path $portableDir "*") -DestinationPath $zipPath -Force
    Copy-Item -Path $zipPath -Destination $releaseZipPath -Force

    $hash = Get-FileHash -Path $releaseZipPath -Algorithm SHA256
    "$($hash.Hash)  $(Split-Path $releaseZipPath -Leaf)" | Set-Content -Path $checksumsPath

    @"
# Nexus Server Manager v$version

Pre-1.0 Windows x64 portable release for local testing.

## Highlights

- Profile-backed Servers tab using Data/servers.json
- Add, edit, delete, import, search, filter, and favorite server profiles
- Real process start/stop/restart for configured server executables
- CPU, RAM, uptime, and timestamp monitoring from tracked process IDs
- Console log viewer and manual zip backup creation
- Provider/data tests included in release build workflow

## Known Limits

- Minecraft Java query/RCON and game-specific admin actions need dedicated adapters.
- Start requires a valid executable path in each server profile.
- This is not a stable v1.0 public release yet.

## Artifact

- $releaseName.zip
"@ | Set-Content -Path $notesPath
}
finally {
    Pop-Location
}
