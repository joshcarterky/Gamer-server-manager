Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root     = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$project  = Join-Path $root "src\GameServerManager.App\GameServerManager.App.csproj"
$buildOut = Join-Path $root "dist\GameServerManager-win-x64"
$testDir  = Join-Path $root "TestBuild"
$testExe  = Join-Path $testDir "GameServerManager.App.exe"

Write-Host "Building release..." -ForegroundColor Cyan
Push-Location $root
try {
    dotnet publish $project `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=false `
        --nologo `
        -v quiet `
        -o $buildOut
} finally {
    Pop-Location
}

Write-Host "Deploying to $testDir ..." -ForegroundColor Cyan
if (-not (Test-Path $testDir)) {
    New-Item -ItemType Directory -Force -Path $testDir | Out-Null
}
robocopy $buildOut $testDir /MIR /NFL /NDL /NJH /NJS /XD Data /XF portable.flag | Out-Null
if ($LASTEXITCODE -le 7) { $global:LASTEXITCODE = 0 }

# portable.flag keeps all data in the test folder itself (no AppData pollution)
$flag = Join-Path $testDir "portable.flag"
if (-not (Test-Path $flag)) {
    New-Item -ItemType File -Force -Path $flag | Out-Null
    Write-Host "Created portable.flag - data will stay in $testDir" -ForegroundColor DarkGray
}

Write-Host "Launching..." -ForegroundColor Green
Start-Process $testExe
