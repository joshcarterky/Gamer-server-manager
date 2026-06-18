Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$version = (Get-Content -LiteralPath (Join-Path $root "VERSION") -Raw).Trim().TrimStart("v")
$tag = "v$version"
$appProject = Join-Path $root "src\GameServerManager.App\GameServerManager.App.csproj"
$publishDir = Join-Path $root "dist\GameServerManager-win-x64"
$portableDir = Join-Path $root "dist\GameServerManager-portable"
$releaseDir = Join-Path $root "releases\$tag"
$publicDir = Join-Path $releaseDir "public"
$updaterFeedDir = Join-Path $releaseDir "updater-feed"
$productName = "NexusServerManager"
$setupFileName = "$productName-Setup-$tag-x64.exe"
$portableFileName = "$productName-Portable-$tag-x64.zip"
$checksumsFileName = "$productName-Checksums-$tag.txt"
$updaterFeedFileName = "$productName-UpdaterFeed-$tag.zip"
$setupPath = Join-Path $publicDir $setupFileName
$portableZip = Join-Path $publicDir $portableFileName
$checksumsPath = Join-Path $publicDir $checksumsFileName
$updateJsonPath = Join-Path $updaterFeedDir "update.json"
$releaseBodyPath = Join-Path $releaseDir "RELEASE_BODY.md"

function Assert-UnderRoot([string]$path) {
    $resolvedRoot = [System.IO.Path]::GetFullPath($root)
    $resolvedPath = [System.IO.Path]::GetFullPath($path)
    if (-not $resolvedPath.StartsWith($resolvedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify path outside repository: $resolvedPath"
    }
}

Push-Location $root
try {
    foreach ($path in @($publishDir, $portableDir, $releaseDir)) {
        Assert-UnderRoot $path
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Recurse -Force
        }
    }

    New-Item -ItemType Directory -Force -Path $publicDir | Out-Null
    New-Item -ItemType Directory -Force -Path $updaterFeedDir | Out-Null

    dotnet restore GameServerManager.sln
    dotnet build GameServerManager.sln --configuration Release --no-restore
    dotnet run --project tests\GameServerManager.ProviderTests\GameServerManager.ProviderTests.csproj --configuration Release

    dotnet publish $appProject `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=false `
        -p:Version=$version `
        -p:AssemblyVersion="$version.0" `
        -p:FileVersion="$version.0" `
        -o $publishDir

    New-Item -ItemType Directory -Force -Path $portableDir | Out-Null
    Copy-Item -Path (Join-Path $publishDir "*") -Destination $portableDir -Recurse -Force
    New-Item -ItemType File -Force -Path (Join-Path $portableDir "portable.flag") | Out-Null
    Copy-Item -Path "README.md" -Destination (Join-Path $portableDir "README.md") -Force
    Copy-Item -Path "CHANGELOG.md" -Destination (Join-Path $portableDir "CHANGELOG.md") -Force
    Copy-Item -Path "LICENSE" -Destination (Join-Path $portableDir "LICENSE") -Force

    Compress-Archive -Path (Join-Path $portableDir "*") -DestinationPath $portableZip -Force

    dotnet tool update -g vpk --version 1.2.0
    $env:PATH = "$env:USERPROFILE\.dotnet\tools;$env:PATH"
    vpk pack `
        --packId NexusServerManager `
        --packVersion $version `
        --packDir $publishDir `
        --mainExe GameServerManager.App.exe `
        --runtime win-x64 `
        --channel stable `
        --outputDir $updaterFeedDir

    $iscc = @(
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $iscc) { throw "Inno Setup 6 not found. Install it via: winget install JRSoftware.InnoSetup" }
    $env:APP_VERSION = $version
    $env:APP_SOURCE_DIR = $root
    $env:APP_OUTPUT_DIR = $publicDir
    $env:APP_OUTPUT_FILENAME = [System.IO.Path]::GetFileNameWithoutExtension($setupFileName)
    & $iscc (Join-Path $root "scripts\release\installer.iss")
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup compilation failed (exit code $LASTEXITCODE)." }
    if (-not (Test-Path $setupPath)) { throw "Inno Setup did not produce the expected file: $setupPath" }

    $artifacts = Get-ChildItem -LiteralPath $publicDir -File | Where-Object {
        $_.Name -ne $checksumsFileName
    }

    $checksumLines = foreach ($artifact in $artifacts) {
        $hash = Get-FileHash -LiteralPath $artifact.FullName -Algorithm SHA256
        "$($hash.Hash)  $($artifact.Name)"
    }
    $checksumLines | Set-Content -LiteralPath $checksumsPath

    $installerHash = (Get-FileHash -LiteralPath $setupPath -Algorithm SHA256).Hash
    $portableHash = (Get-FileHash -LiteralPath $portableZip -Algorithm SHA256).Hash
    $repositoryUrl = "https://github.com/joshcarterky/Gamer-server-manager"
    $releaseUrl = "$repositoryUrl/releases/tag/$tag"
    $downloadBaseUrl = "$repositoryUrl/releases/download/$tag"
    $metadata = [ordered]@{
        version = $version
        channel = "stable"
        releaseDate = (Get-Date).ToUniversalTime().ToString("yyyy-MM-dd")
        installerUrl = "$downloadBaseUrl/$setupFileName"
        portableUrl = "$downloadBaseUrl/$portableFileName"
        sha256 = [ordered]@{
            installer = $installerHash
            portable = $portableHash
        }
        releaseNotesUrl = $releaseUrl
        minimumSupportedVersion = "3.0.0"
    }
    $metadata | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $updateJsonPath

    $updaterFeedZip = Join-Path $releaseDir $updaterFeedFileName
    Compress-Archive -Path (Join-Path $updaterFeedDir "*") -DestinationPath $updaterFeedZip -Force

    $releaseNotes = Get-Content -LiteralPath "RELEASE_NOTES.md" -Raw
    $releaseBody = @"
# Nexus Server Manager $tag

## Download

Recommended for most users:

**Windows Installer**
Download: $setupFileName

Portable version:

**Portable ZIP**
Download: $portableFileName

## Which one should I download?

Use the installer if you want the easiest setup.

Use the portable ZIP if you want to run the app without installing it.

## Do not download these unless you are a developer

Files like update manifests, nupkg packages, checksums, or RELEASES files are for the updater system only.

## Release Notes

$releaseNotes
"@
    $releaseBody | Set-Content -LiteralPath $releaseBodyPath

    Write-Host "Public GitHub release assets written to $publicDir"
    Write-Host "Updater feed files written to $updaterFeedDir"
}
finally {
    Pop-Location
}
