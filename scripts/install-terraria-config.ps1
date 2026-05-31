param(
    [string]$TerrariaDir = "F:\Games\GamePlats\Steam\steamapps\common\Terraria"
)

$ErrorActionPreference = "Stop"

function Backup-IfExists {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (Test-Path $Path) {
        $backupPath = $Path + ".backup-" + (Get-Date -Format "yyyyMMddHHmmss")
        Copy-Item $Path $backupPath -Force
        return $backupPath
    }

    return $null
}

try {
    $repoRoot = Split-Path -Parent $PSScriptRoot
    Set-Location $repoRoot

    if (-not (Test-Path $TerrariaDir)) {
        Write-Error "Terraria directory was not found: $TerrariaDir"
    }

    $terrariaExe = Join-Path $TerrariaDir "Terraria.exe"
    if (-not (Test-Path $terrariaExe)) {
        Write-Error "Terraria.exe was not found: $terrariaExe"
    }

    $packageScript = Join-Path $PSScriptRoot "build-test-package.ps1"
    & $packageScript
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    $packageDir = Join-Path $repoRoot "JueMingZ-TestPackage"
    $filesToCopy = @(
        "Terraria.exe.config",
        "JueMingZ.dll",
        "Newtonsoft.Json.dll"
    )

    $copiedFiles = New-Object System.Collections.Generic.List[string]
    $backupFiles = New-Object System.Collections.Generic.List[string]

    foreach ($fileName in $filesToCopy) {
        $sourcePath = Join-Path $packageDir $fileName
        if (-not (Test-Path $sourcePath)) {
            continue
        }

        $targetPath = Join-Path $TerrariaDir $fileName
        $backupPath = Backup-IfExists -Path $targetPath
        if ($backupPath) {
            [void]$backupFiles.Add($backupPath)
        }

        Copy-Item $sourcePath $targetPath -Force
        [void]$copiedFiles.Add($targetPath)
    }

    Write-Host ""
    Write-Host "JueMingZ flat install completed."
    Write-Host "Test package: $packageDir"
    Write-Host "Terraria directory: $TerrariaDir"
    Write-Host "Copied files:"
    foreach ($copiedFile in $copiedFiles) {
        Write-Host "  $copiedFile"
    }

    if ($backupFiles.Count -gt 0) {
        Write-Host "Backups:"
        foreach ($backupFile in $backupFiles) {
            Write-Host "  $backupFile"
        }
    }
    else {
        Write-Host "Backups: none"
    }

    Write-Host ""
    Write-Host "Next steps:"
    Write-Host "1. Start Terraria from Steam or Terraria.exe."
    Write-Host "2. Press F5 in the main menu or world to toggle the JueMingZ main UI."
    Write-Host "3. Check logs: Documents\My Games\Terraria\JueMing-Z\logs"
    Write-Host "4. Check snapshot: Documents\My Games\Terraria\JueMing-Z\diagnostics\runtime-snapshot.json"
    Write-Host ""
    Write-Host "To uninstall, run scripts\uninstall-terraria-config.ps1 with the same -TerrariaDir."
}
catch {
    Write-Host ("Install script failed: {0}" -f $_.Exception.Message)
    exit 2
}
