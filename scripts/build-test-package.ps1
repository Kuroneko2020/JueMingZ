param(
    [switch]$Zip
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

function Assert-ArchiveExcludesTerrariaDecompiled {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ArchivePath
    )

    if (-not (Test-Path -LiteralPath $ArchivePath)) {
        throw "Archive does not exist: $ArchivePath"
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead($ArchivePath)
    try {
        $forbiddenEntries = @($zip.Entries | Where-Object {
                $_.FullName -match '(?i)TerrariaDecompiled'
            })
        if ($forbiddenEntries.Count -gt 0) {
            $names = $forbiddenEntries | ForEach-Object { $_.FullName }
            throw "Forbidden TerrariaDecompiled entries found in archive: $($names -join ', ')"
        }
    }
    finally {
        $zip.Dispose()
    }
}

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$buildScript = Join-Path $PSScriptRoot "build-release.ps1"
& $buildScript
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$outputDir = Join-Path $repoRoot "src\JueMingZ\bin\x86\Release\net472"
$packageDir = Join-Path $repoRoot "JueMingZ-TestPackage"
$zipPath = Join-Path $repoRoot "JueMingZ-TestPackage.zip"
$compileOnlyDependencyNames = @(
    "Terraria.exe",
    "Microsoft.Xna.Framework.dll",
    "Microsoft.Xna.Framework.Game.dll",
    "Microsoft.Xna.Framework.Graphics.dll",
    "ReLogic.dll",
    "Newtonsoft.Json.dll"
)

foreach ($name in $compileOnlyDependencyNames) {
    $candidate = Join-Path $outputDir $name
    if (Test-Path -LiteralPath $candidate) {
        throw "Compile-only Terraria/XNA/ReLogic dependency was copied to build output: $candidate"
    }
}

# Test package cleanup is limited to the repo package folder; compile-only
# game assemblies must never be shipped alongside the injected runtime.
if ((Test-Path $packageDir) -and (Test-IsInsideDirectory -Parent $repoRoot -Child $packageDir)) {
    try {
        Remove-Item -LiteralPath $packageDir -Recurse -Force -ErrorAction Stop
    }
    catch {
        Write-Warning "Failed to remove existing test package directory. Falling back to in-place file cleanup."
        $knownPackageFiles = @(
            "JueMingZ.dll",
            "0Harmony.dll",
            "Newtonsoft.Json.dll",
            "Terraria.exe.config",
            "README_测试说明.txt",
            "VERSION.txt"
        )
        foreach ($name in $knownPackageFiles) {
            $path = Join-Path $packageDir $name
            if (Test-Path -LiteralPath $path) {
                Remove-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue
            }
        }
    }
}

New-Item -ItemType Directory -Force -Path $packageDir | Out-Null

$jmzDll = Join-Path $outputDir "JueMingZ.dll"
if (-not (Test-Path $jmzDll)) {
    Write-Error "JueMingZ.dll was not found: $jmzDll"
}

Copy-Item $jmzDll (Join-Path $packageDir "JueMingZ.dll") -Force

$assemblyName = [System.Reflection.AssemblyName]::GetAssemblyName($jmzDll)
$assemblyDisplayName = "JueMingZ, Version=$($assemblyName.Version), Culture=neutral, PublicKeyToken=null"
$runtimeSourcePath = Join-Path $repoRoot "src\JueMingZ\Runtime\JueMingZRuntime.cs"
$runtimeVersion = "Unknown"
if (Test-Path $runtimeSourcePath) {
    $runtimeSource = Get-Content -Path $runtimeSourcePath -Raw
    $runtimeMatch = [System.Text.RegularExpressions.Regex]::Match($runtimeSource, 'public\s+const\s+string\s+Version\s*=\s*"([^"]+)"')
    if ($runtimeMatch.Success) {
        $runtimeVersion = $runtimeMatch.Groups[1].Value
    }
}

$configContent = @"
<?xml version="1.0" encoding="utf-8" ?>
<configuration>
  <runtime>
    <appDomainManagerAssembly value="$assemblyDisplayName" />
    <appDomainManagerType value="JueMingZ.Bootstrap.JueMingZAppDomainManager" />
  </runtime>
</configuration>
"@
Set-Content -Path (Join-Path $packageDir "Terraria.exe.config") -Value $configContent -Encoding UTF8

$readmeFileName = "README_测试说明.txt"
$readmeTemplate = Join-Path $repoRoot "文档\项目规则\测试包README模板.zh-CN.txt"
if (-not (Test-Path $readmeTemplate)) {
    Write-Error "README testing template was not found: $readmeTemplate"
}

$readmeOutputPath = Join-Path $packageDir $readmeFileName
$utf8Bom = New-Object System.Text.UTF8Encoding($true)
$templateText = [System.IO.File]::ReadAllText($readmeTemplate, [System.Text.Encoding]::UTF8)
$buildTimeLocal = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
$readmeText = $templateText.Replace("{{RuntimeVersion}}", $runtimeVersion).Replace("{{BuildTimeLocal}}", $buildTimeLocal)
[System.IO.File]::WriteAllText($readmeOutputPath, $readmeText, $utf8Bom)

if (-not (Test-Path -LiteralPath $readmeOutputPath)) {
    throw "README_测试说明.txt was not created: $readmeOutputPath"
}

$readmeCheckText = [System.IO.File]::ReadAllText($readmeOutputPath, [System.Text.Encoding]::UTF8)
if (-not $readmeCheckText.Contains($runtimeVersion)) {
    throw "README_测试说明.txt does not contain RuntimeVersion: $runtimeVersion"
}

$staleReadmePatterns = @(
    "只需要确认",
    "当前测试只需要",
    "松露虫提示文案确认",
    "猪鲨提示文案确认",
    "松露虫 / 猪鲨提示文案确认",
    "旧防回归清单：",
    "成功路径回归",
    "轻量回归"
)
foreach ($pattern in $staleReadmePatterns) {
    if ($readmeCheckText.Contains($pattern)) {
        throw "README_测试说明.txt contains stale focused-test wording: $pattern"
    }
}

$encodedReadmeFiles = Get-ChildItem -LiteralPath $packageDir -File -Filter "README_#U*.txt" -ErrorAction SilentlyContinue
if ($encodedReadmeFiles -and $encodedReadmeFiles.Count -gt 0) {
    $isWindows = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
        [System.Runtime.InteropServices.OSPlatform]::Windows)
    if ($isWindows) {
        throw "Encoded README files should not exist in test package on Windows."
    }

    Write-Warning "README_#U*.txt was observed in a non-Windows environment. Chinese filenames can display incorrectly in non-Windows/zip tools; use Windows local validation before treating this as a user-facing package failure."
}

$versionText = "RuntimeVersion=$runtimeVersion"
Set-Content -Path (Join-Path $packageDir "VERSION.txt") -Value $versionText -Encoding UTF8

foreach ($name in $compileOnlyDependencyNames) {
    $candidate = Join-Path $packageDir $name
    if (Test-Path -LiteralPath $candidate) {
        throw "Test package must not contain compile-only Terraria/XNA/ReLogic dependency: $candidate"
    }
}

if ($Zip) {
    if (Test-Path $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }

    $itemsToZip = Get-ChildItem -LiteralPath $packageDir -Force
    Compress-Archive -Path $itemsToZip.FullName -DestinationPath $zipPath -Force
    Assert-ArchiveExcludesTerrariaDecompiled -ArchivePath $zipPath
}

Write-Host "JueMingZ test package created: $packageDir"
Write-Host "RuntimeVersion: $runtimeVersion"
Write-Host "README generated: $readmeOutputPath"
Write-Host "First-level files:"
Get-ChildItem -Path $packageDir -File | Select-Object Name,Length | Format-Table -AutoSize
if ($Zip) {
    Write-Host "Zip created: $zipPath"
}
