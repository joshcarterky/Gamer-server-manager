Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
Push-Location $root
try {
    dotnet restore
    dotnet build GameServerManager.sln
}
finally {
    Pop-Location
}
