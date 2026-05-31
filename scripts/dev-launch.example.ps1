# Temporary launch mode.
#
# This script does NOT create or edit Terraria.exe.config.
# For normal testing, prefer build-test-package.ps1 and copy the first-level
# files from JueMingZ-TestPackage into the Terraria root directory.

$ErrorActionPreference = "Stop"

# Edit this path for your local Terraria installation. Do not commit private local paths.
$TerrariaExe = "F:\Games\GamePlats\Steam\steamapps\common\Terraria\Terraria.exe"

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

if (-not $env:DOTNET_CLI_HOME) {
    $env:DOTNET_CLI_HOME = Join-Path $repoRoot ".dotnet_home"
}
$env:APPDATA = Join-Path $repoRoot ".appdata"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

if (-not (Test-Path $TerrariaExe)) {
    Write-Error "Terraria.exe was not found. Edit `$TerrariaExe at the top of this script."
}

$packageScript = Join-Path $PSScriptRoot "build-test-package.ps1"
& $packageScript
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$terrariaDir = Split-Path -Parent $TerrariaExe
$packageDir = Join-Path $repoRoot "JueMingZ-TestPackage"

foreach ($fileName in @("JueMingZ.dll")) {
    Copy-Item (Join-Path $packageDir $fileName) (Join-Path $terrariaDir $fileName) -Force
}

$assemblyName = [System.Reflection.AssemblyName]::GetAssemblyName((Join-Path $packageDir "JueMingZ.dll"))
$startInfo = New-Object System.Diagnostics.ProcessStartInfo
$startInfo.FileName = $TerrariaExe
$startInfo.WorkingDirectory = $terrariaDir
$startInfo.UseShellExecute = $false
$startInfo.EnvironmentVariables["APPDOMAIN_MANAGER_ASM"] = "JueMingZ, Version=$($assemblyName.Version), Culture=neutral, PublicKeyToken=null"
$startInfo.EnvironmentVariables["APPDOMAIN_MANAGER_TYPE"] = "JueMingZ.Bootstrap.JueMingZAppDomainManager"

Write-Host "Starting Terraria with temporary AppDomainManager environment variables."
Write-Host "This script did not create Terraria.exe.config."
Write-Host "Copied JueMingZ.dll into: $terrariaDir"
Write-Host "For shareable testing, use JueMingZ-TestPackage instead."

[System.Diagnostics.Process]::Start($startInfo) | Out-Null
