Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
Push-Location $root
try {
    dotnet run --project src\GameServerManager.App\GameServerManager.App.csproj
}
finally {
    Pop-Location
}
