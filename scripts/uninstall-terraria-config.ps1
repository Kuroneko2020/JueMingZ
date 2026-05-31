param(
    [string]$TerrariaDir = "F:\Games\GamePlats\Steam\steamapps\common\Terraria",
    [switch]$RemoveFiles
)

$ErrorActionPreference = "Stop"

function Test-IsInsideDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Parent,

        [Parameter(Mandatory = $true)]
        [string]$Child
    )

    $parentPath = [System.IO.Path]::GetFullPath($Parent).TrimEnd([char[]]@('\', '/')) + [System.IO.Path]::DirectorySeparatorChar
    $childPath = [System.IO.Path]::GetFullPath($Child).TrimEnd([char[]]@('\', '/')) + [System.IO.Path]::DirectorySeparatorChar
    return $childPath.StartsWith($parentPath, [System.StringComparison]::OrdinalIgnoreCase)
}

try {
    if (-not (Test-Path $TerrariaDir)) {
        Write-Error "Terraria directory was not found: $TerrariaDir"
    }

    $managerType = "JueMingZ.Bootstrap.JueMingZAppDomainManager"
    $configPath = Join-Path $TerrariaDir "Terraria.exe.config"

    if (Test-Path $configPath) {
        $configText = Get-Content -Raw -Path $configPath
        if ($configText -notlike "*$managerType*") {
            Write-Host "Terraria.exe.config does not contain JueMingZ AppDomainManager. No config change was made."
        }
        else {
            $backup = Get-ChildItem -Path $TerrariaDir -Filter "Terraria.exe.config.backup-*" -File |
                Sort-Object LastWriteTimeUtc -Descending |
                Where-Object {
                    $text = Get-Content -Raw -Path $_.FullName
                    $text -notlike "*$managerType*"
                } |
                Select-Object -First 1

            if ($backup) {
                Remove-Item -LiteralPath $configPath -Force
                Copy-Item $backup.FullName $configPath -Force
                Write-Host "Restored config backup: $($backup.FullName)"
            }
            else {
                Remove-Item -LiteralPath $configPath -Force
                Write-Host "Removed JueMingZ Terraria.exe.config. No non-JueMingZ backup was found to restore."
            }
        }
    }
    else {
        Write-Host "Terraria.exe.config was not found."
    }

    foreach ($fileName in @("JueMingZ.dll", "0Harmony.dll")) {
        $path = Join-Path $TerrariaDir $fileName
        if (Test-Path $path) {
            Remove-Item -LiteralPath $path -Force
            Write-Host "Removed: $path"
        }
    }

    $installDir = Join-Path $TerrariaDir "JueMingZDev"
    if ($RemoveFiles) {
        if ((Test-Path $installDir) -and (Test-IsInsideDirectory -Parent $TerrariaDir -Child $installDir)) {
            Remove-Item -LiteralPath $installDir -Recurse -Force
            Write-Host "Removed old install directory: $installDir"
        }
        else {
            Write-Host "Old JueMingZDev directory was not found."
        }
    }
}
catch {
    Write-Host ("Uninstall script failed: {0}" -f $_.Exception.Message)
    exit 2
}
