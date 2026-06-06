param(
    [switch]$Zip,
    [switch]$ZipOnly
)

$ErrorActionPreference = "Stop"

if ($ZipOnly) {
    $Zip = $true
}

$localDocsRootName = ([string][char]0x6587) + ([string][char]0x6863)

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

function Remove-DirectoryInsideRoot {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root,

        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if ((Test-Path -LiteralPath $Path) -and (Test-IsInsideDirectory -Parent $Root -Child $Path)) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
}

function Get-RelativePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root,

        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $fullRoot = [System.IO.Path]::GetFullPath($Root).TrimEnd([char[]]@('\', '/'))
    $fullPath = [System.IO.Path]::GetFullPath($Path)

    if ($fullPath.StartsWith($fullRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $fullPath.Substring($fullRoot.Length).TrimStart([char[]]@('\', '/'))
    }

    return $Path
}

function Test-IsRuntimeArtifactPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root,

        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $relative = Get-RelativePath -Root $Root -Path $Path
    $normalized = $relative.Replace('/', '\')
    $segments = $normalized -split '\\'

    if ($segments.Count -le 0) {
        return $false
    }

    if ($segments[0] -in @('.git', '.vs', '.dotnet_home', '.nuget', '.appdata', 'docs', $script:localDocsRootName, 'JueMingZ-TestPackage', 'JueMingZ-SourcePackage', 'references')) {
        return $true
    }

    if ($segments.Count -eq 1 -and $segments[0] -in @('logs', 'config', 'diagnostics')) {
        return $true
    }

    foreach ($segment in $segments) {
        if ($segment -in @('bin', 'obj')) {
            return $true
        }
    }

    return $false
}

function Copy-DirectoryFiltered {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root,

        [Parameter(Mandatory = $true)]
        [string]$Source,

        [Parameter(Mandatory = $true)]
        [string]$Destination
    )

    New-Item -ItemType Directory -Force -Path $Destination | Out-Null

    $excludedFileNames = @('JueMingZ-TestPackage.zip', 'JueMingZ-SourcePackage.zip', 'JueMing-Z-main.zip')
    $excludedExtensions = @('.log', '.tmp', '.pdb')
    $excludedGeneratedFiles = @('runtime-snapshot.json', 'feature-catalog.json', 'appsettings.json')

    foreach ($item in Get-ChildItem -LiteralPath $Source -Force) {
        if ($item.PSIsContainer) {
            if (Test-IsRuntimeArtifactPath -Root $Root -Path $item.FullName) {
                continue
            }

            Copy-DirectoryFiltered -Root $Root -Source $item.FullName -Destination (Join-Path $Destination $item.Name)
            continue
        }

        $relativeFile = Get-RelativePath -Root $Root -Path $item.FullName
        if (($excludedFileNames -contains $item.Name) -or
            ($excludedExtensions -contains $item.Extension.ToLowerInvariant()) -or
            ($excludedGeneratedFiles -contains $item.Name) -or
            (($relativeFile.Replace('/', '\')) -like 'scripts\_*.py') -or
            ($relativeFile -eq 'AGENTS.md') -or
            ($item.Name -like "action-events-*.jsonl")) {
            continue
        }

        Copy-Item -LiteralPath $item.FullName -Destination (Join-Path $Destination $item.Name) -Force
    }
}

function Copy-FileIfExists {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Source,

        [Parameter(Mandatory = $true)]
        [string]$Destination
    )

    if (Test-Path -LiteralPath $Source) {
        $parent = Split-Path -Parent $Destination
        New-Item -ItemType Directory -Force -Path $parent | Out-Null
        Copy-Item -LiteralPath $Source -Destination $Destination -Force
    }
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
            throw "Forbidden TerrariaDecompiled entries found in source archive: $($names -join ', ')"
        }
    }
    finally {
        $zip.Dispose()
    }
}

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$packageDir = Join-Path $repoRoot "JueMingZ-SourcePackage"
$zipPath = Join-Path $repoRoot "JueMingZ-SourcePackage.zip"

# Source package cleanup must stay inside the repo root; local docs,
# references, and generated packages are not public source payload.
Remove-DirectoryInsideRoot -Root $repoRoot -Path $packageDir
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

New-Item -ItemType Directory -Force -Path $packageDir | Out-Null

foreach ($name in @('src', 'scripts', 'tests')) {
    $source = Join-Path $repoRoot $name
    if (Test-Path -LiteralPath $source) {
        Copy-DirectoryFiltered -Root $repoRoot -Source $source -Destination (Join-Path $packageDir $name)
    }
}

foreach ($fileName in @('README.md', 'JueMingZ.sln', 'NuGet.Config', '.gitignore')) {
    Copy-FileIfExists -Source (Join-Path $repoRoot $fileName) -Destination (Join-Path $packageDir $fileName)
}

$thirdPartySource = Join-Path $repoRoot "external\ThirdParty"
$thirdPartyDestination = Join-Path $packageDir "external\ThirdParty"
Copy-FileIfExists -Source (Join-Path $thirdPartySource "0Harmony.dll") -Destination (Join-Path $thirdPartyDestination "0Harmony.dll")
Copy-FileIfExists -Source (Join-Path $thirdPartySource "README.md") -Destination (Join-Path $thirdPartyDestination "README.md")

$forbiddenRelativePaths = @(
    'JueMingZ-TestPackage',
    'JueMingZ-TestPackage.zip',
    'JueMingZ-SourcePackage',
    'JueMingZ-SourcePackage.zip',
    'JueMing-Z-main.zip',
    'AGENTS.md',
    'docs',
    $localDocsRootName,
    'references',
    'src\JueMingZ\bin',
    'src\JueMingZ\obj',
    'logs',
    'config',
    'diagnostics'
)

foreach ($relativePath in $forbiddenRelativePaths) {
    $candidate = Join-Path $packageDir $relativePath
    if (Test-Path -LiteralPath $candidate) {
        throw "Source package contains generated content: $relativePath"
    }
}

$requiredRelativePaths = @(
    '.gitignore',
    'README.md',
    'JueMingZ.sln',
    'NuGet.Config',
    'external\ThirdParty\0Harmony.dll',
    'src\JueMingZ\Config\AppSettings.cs',
    'src\JueMingZ\Config\ConfigService.cs',
    'src\JueMingZ\Diagnostics\DiagnosticActionRecorder.cs',
    'src\JueMingZ\Diagnostics\DiagnosticSnapshotWriter.cs',
    'src\JueMingZ\Diagnostics\FeatureCatalogWriter.cs',
    'src\JueMingZ\Actions\Executors\BuffPotionDirectUseExecutor.cs',
    'src\JueMingZ\Automation\BuffAndRecovery\BuffPotionCandidate.cs',
    'src\JueMingZ\UI\OperationWindowState.cs',
    'scripts\audit-project-health.ps1',
    'scripts\build-test-package.ps1',
    'scripts\build-source-package.ps1',
    'tests\JueMingZ.Tests\JueMingZ.Tests.csproj',
    'tests\JueMingZ.Tests\Program.cs'
)

foreach ($relativePath in $requiredRelativePaths) {
    $candidate = Join-Path $packageDir $relativePath
    if (-not (Test-Path -LiteralPath $candidate)) {
        throw "Source package missing required source file: $relativePath"
    }
}

$firstLevelEntries = Get-ChildItem -Path $packageDir -Force | Select-Object Name,Mode,Length

if ($Zip) {
    $itemsToZip = Get-ChildItem -LiteralPath $packageDir -Force
    Compress-Archive -Path $itemsToZip.FullName -DestinationPath $zipPath -Force
    Assert-ArchiveExcludesTerrariaDecompiled -ArchivePath $zipPath
}

Write-Host "JueMingZ source package created: $packageDir"
Write-Host "Included required source directories: Config, Diagnostics, Actions, Automation, UI, Input, Features, tests"
Write-Host "Excluded runtime artifact roots: logs, config, diagnostics"
Write-Host "Excluded local docs/reference/generated folders: AGENTS.md, docs (legacy backflow guard), $localDocsRootName, references, bin, obj, JueMingZ-TestPackage, JueMingZ-SourcePackage"
Write-Host "First-level entries:"
$firstLevelEntries | Format-Table -AutoSize
if ($Zip) {
    Write-Host "Zip created: $zipPath"
}

if ($ZipOnly) {
    Remove-DirectoryInsideRoot -Root $repoRoot -Path $packageDir
    Write-Host "Package directory removed because -ZipOnly was specified. Only zip remains."
}
