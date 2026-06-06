$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

# Keep dotnet caches in ignored repo-local folders so release builds do
# not leak machine-specific artifacts into the clean source boundary.
if (-not $env:DOTNET_CLI_HOME) {
    $env:DOTNET_CLI_HOME = Join-Path $repoRoot ".dotnet_home"
}
$env:APPDATA = Join-Path $repoRoot ".appdata"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error "dotnet CLI was not found. Install .NET SDK, or build JueMingZ.sln with Visual Studio / MSBuild."
}

dotnet build .\JueMingZ.sln -c Release -p:Platform=x86
$exitCode = $LASTEXITCODE
if ($exitCode -ne 0) {
    exit $exitCode
}
