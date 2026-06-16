Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$version = (Get-Content -LiteralPath (Join-Path $root "VERSION") -Raw).Trim().TrimStart("v")
$tag = "v$version"
$appProject = Join-Path $root "src\GameServerManager.App\GameServerManager.App.csproj"
$publishDir = Join-Path $root "dist\GameServerManager-win-x64"
$portableDir = Join-Path $root "dist\GameServerManager-portable"
$releaseDir = Join-Path $root "releases\$tag"
$portableZip = Join-Path $releaseDir "ServerManager-Portable-$tag.zip"
$checksumsPath = Join-Path $releaseDir "checksums.txt"
$updateJsonPath = Join-Path $releaseDir "update.json"
$releaseNotesPath = Join-Path $releaseDir "RELEASE_NOTES.md"

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

    New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null

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
        --outputDir $releaseDir

    $setup = Get-ChildItem -LiteralPath $releaseDir -Filter "*Setup*.exe" | Select-Object -First 1
    if ($setup) {
        Rename-Item -LiteralPath $setup.FullName -NewName "ServerManager-Setup-$tag.exe" -Force
    }

    Copy-Item -LiteralPath "RELEASE_NOTES.md" -Destination $releaseNotesPath -Force

    $artifacts = Get-ChildItem -LiteralPath $releaseDir -File | Where-Object {
        $_.Name -ne "checksums.txt" -and $_.Name -ne "update.json"
    }

    $checksumLines = foreach ($artifact in $artifacts) {
        $hash = Get-FileHash -LiteralPath $artifact.FullName -Algorithm SHA256
        "$($hash.Hash)  $($artifact.Name)"
    }
    $checksumLines | Set-Content -LiteralPath $checksumsPath

    $metadata = [ordered]@{
        version = $tag
        channel = "stable"
        releaseDateUtc = (Get-Date).ToUniversalTime().ToString("o")
        installer = "ServerManager-Setup-$tag.exe"
        portableZip = "ServerManager-Portable-$tag.zip"
        checksums = "checksums.txt"
        releaseNotes = "RELEASE_NOTES.md"
        minimumVersion = "v1.0.0"
    }
    $metadata | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $updateJsonPath

    Write-Host "Release artifacts written to $releaseDir"
}
finally {
    Pop-Location
}
