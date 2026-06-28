param(
    [switch]$IncludeSourcePackageZip,
    [switch]$AllowTestPackageReadme,
    [switch]$RequireFreshTestPackage,
    [Alias("Profile")]
    [ValidateSet("Full", "Fast")]
    [string]$AuditProfile = "Full",
    [Alias("Scope")]
    [ValidateSet(
        "All", "Base",
        "Information", "UI", "Combat", "Diagnostics", "Map", "Hotkey", "Blueprint", "BlueprintCreation", "BlueprintPlacement", "BlueprintTransform", "BlueprintAutoPlacement", "BlueprintHandheld", "BlueprintDiagnostics",
        "Notes", "Exploration", "ActionQueue", "Input", "Feature", "Structure", "Runtime", "Config", "GameState",
        "Fishing", "Movement", "Items", "Buffs", "World", "Npc"
    )]
    [string[]]$AuditScope = @("All")
)

$ErrorActionPreference = "Stop"

$script:FailCount = 0
$script:WarnCount = 0

function Write-Pass {
    param([Parameter(Mandatory = $true)][string]$Message)
    Write-Host "PASS $Message"
}

function Write-WarnHealth {
    param([Parameter(Mandatory = $true)][string]$Message)
    $script:WarnCount++
    Write-Host "WARN $Message"
}

function Write-FailHealth {
    param([Parameter(Mandatory = $true)][string]$Message)
    $script:FailCount++
    Write-Host "FAIL $Message"
}

function Write-TestPackageIssue {
    param(
        [Parameter(Mandatory = $true)][string]$Message,
        [switch]$Strict
    )

    if ($Strict) {
        Write-FailHealth $Message
    }
    else {
        Write-WarnHealth $Message
    }
}

function Read-TextIfExists {
    param([Parameter(Mandatory = $true)][string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) {
        return $null
    }

    return [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8)
}

function Read-KeyValueManifest {
    param([Parameter(Mandatory = $true)][string]$Path)

    $text = Read-TextIfExists -Path $Path
    if ($null -eq $text) {
        return $null
    }

    $manifest = @{}
    foreach ($line in ($text -split "\r?\n")) {
        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }

        $separatorIndex = $line.IndexOf("=")
        if ($separatorIndex -le 0) {
            continue
        }

        $key = $line.Substring(0, $separatorIndex).Trim()
        $value = $line.Substring($separatorIndex + 1).Trim()
        if (-not [string]::IsNullOrWhiteSpace($key)) {
            $manifest[$key] = $value
        }
    }

    return $manifest
}

function Get-Sha256Hex {
    param([Parameter(Mandatory = $true)][string]$Path)

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-AssemblyInformationalVersion {
    param([Parameter(Mandatory = $true)][string]$Path)

    $versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($Path)
    if ([string]::IsNullOrWhiteSpace($versionInfo.ProductVersion)) {
        return "Unknown"
    }

    return $versionInfo.ProductVersion
}

function ConvertFrom-CodePoints {
    param([Parameter(Mandatory = $true)][int[]]$CodePoints)

    $builder = New-Object System.Text.StringBuilder
    foreach ($codePoint in $CodePoints) {
        [void]$builder.Append([char]$codePoint)
    }

    return $builder.ToString()
}

function Get-LocalDocsRootName {
    return ConvertFrom-CodePoints @(0x6587, 0x6863)
}

function Join-LocalDocsPath {
    param(
        [Parameter(Mandatory = $true)][string]$RepoRoot,
        [Parameter(Mandatory = $true)][string[]]$Segments
    )

    $path = Join-Path $RepoRoot (Get-LocalDocsRootName)
    foreach ($segment in $Segments) {
        $path = Join-Path $path $segment
    }

    return $path
}

function Get-RegisteredFeatureCount {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $catalogDir = Join-Path $RepoRoot "src\JueMingZ\Features\Catalog"
    if (-not (Test-Path -LiteralPath $catalogDir)) {
        return 0
    }

    $matches = Select-String -Path (Join-Path $catalogDir "*.cs") -Pattern "FeatureDefinitionBuilder\.Create" -ErrorAction SilentlyContinue
    if ($null -eq $matches) {
        return 0
    }

    return @($matches).Count
}

function Get-DocumentationSizeGuard {
    param(
        [Parameter(Mandatory = $true)][string]$RepoRoot,
        [Parameter(Mandatory = $true)][string]$Kind
    )

    $featureCount = Get-RegisteredFeatureCount -RepoRoot $RepoRoot
    $extraFeatures = [Math]::Max(0, $featureCount - 30)

    if ($Kind -eq "CurrentStatus") {
        $baseLimit = 16384
        $hardLimit = 32768
        $bytesPerExtraFeature = 512
    }
    elseif ($Kind -eq "Handoff") {
        $baseLimit = 24576
        $hardLimit = 40960
        $bytesPerExtraFeature = 512
    }
    else {
        throw "Unknown documentation size guard kind: $Kind"
    }

    $adaptiveLimit = [Math]::Min($hardLimit, $baseLimit + ($extraFeatures * $bytesPerExtraFeature))
    return [PSCustomObject]@{
        Limit = $adaptiveLimit
        BaseLimit = $baseLimit
        HardLimit = $hardLimit
        FeatureCount = $featureCount
        ExtraFeatures = $extraFeatures
    }
}

function Get-ZipEntryText {
    param(
        [Parameter(Mandatory = $true)]$Zip,
        [Parameter(Mandatory = $true)][string]$EntryName
    )

    $normalized = $EntryName.Replace('\', '/')
    $entry = $null
    foreach ($candidate in $Zip.Entries) {
        if ($candidate.FullName.Replace('\', '/') -eq $normalized) {
            $entry = $candidate
            break
        }
    }

    if ($null -eq $entry) {
        return $null
    }

    $stream = $entry.Open()
    try {
        $reader = New-Object System.IO.StreamReader($stream, [System.Text.Encoding]::UTF8, $true)
        try {
            return $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

function Test-ZipTextMatchesWorkspace {
    param(
        [Parameter(Mandatory = $true)]$Zip,
        [Parameter(Mandatory = $true)][string]$RepoRoot,
        [Parameter(Mandatory = $true)][string]$RelativePath
    )

    $workspacePath = Join-Path $RepoRoot $RelativePath
    $workspaceText = Read-TextIfExists -Path $workspacePath
    if ($null -eq $workspaceText) {
        Write-FailHealth "Workspace key file missing before source package comparison: $RelativePath"
        return
    }

    $zipText = Get-ZipEntryText -Zip $Zip -EntryName $RelativePath
    if ($null -eq $zipText) {
        Write-FailHealth "Source package zip missing key file: $RelativePath"
        return
    }

    if ($zipText -eq $workspaceText) {
        Write-Pass "Source package key file matches workspace: $RelativePath"
    }
    else {
        Write-FailHealth "Source package key file is stale or differs from workspace: $RelativePath"
    }
}

function Test-GitIgnoreExpectation {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][bool]$ShouldBeIgnored,
        [Parameter(Mandatory = $true)][string]$Description
    )

    & git check-ignore --quiet --no-index -- $Path
    $exitCode = $LASTEXITCODE
    if ($exitCode -gt 1) {
        Write-FailHealth "git check-ignore failed for $Path"
        return
    }

    if ($ShouldBeIgnored) {
        if ($exitCode -eq 0) {
            Write-Pass (".gitignore ignores {0}: {1}" -f $Description, $Path)
        }
        else {
            Write-FailHealth ".gitignore does not ignore expected local/generated path: $Path"
        }
    }
    else {
        if ($exitCode -eq 1) {
            Write-Pass ".gitignore keeps standard source path trackable: $Path"
        }
        else {
            Write-FailHealth ".gitignore unexpectedly ignores standard source path: $Path"
        }
    }
}

function Get-TrackedFiles {
    $files = & git ls-files
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        Write-FailHealth "git ls-files failed while auditing tracked source boundaries."
        return @()
    }

    return @($files | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

function Test-GitSourceBoundary {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $localDocsRootName = Get-LocalDocsRootName
    $localDocsRootRegex = [System.Text.RegularExpressions.Regex]::Escape($localDocsRootName)
    $gitignorePath = Join-Path $RepoRoot ".gitignore"
    $gitignoreText = Read-TextIfExists -Path $gitignorePath
    if ($null -eq $gitignoreText) {
        Write-FailHealth ".gitignore is missing."
        return
    }

# GitHub must stay as clean source only; AGENTS, local docs, references,
# generated packages, logs, and caches remain local collaboration material.
    $requiredIgnoreSnippets = @(
        "**/bin/",
        "**/obj/",
        ".codex-tmp/",
        "_codex_log_inspect_*/",
        "AGENTS.md",
        "docs/",
        "$localDocsRootName/",
        "references/",
        "references/TerrariaDecompiled-*/",
        "scripts/_*.py",
        "external/TerrariaRefs/",
        "JueMingZ-TestPackage/",
        "JueMingZ-TestPackage.zip",
        "JueMingZ-SourcePackage/",
        "JueMingZ-SourcePackage.zip",
        "/logs/",
        "/config/",
        "/diagnostics/",
        "*.pdb"
    )

    $missingSnippets = @()
    foreach ($snippet in $requiredIgnoreSnippets) {
        if (-not $gitignoreText.Contains($snippet)) {
            $missingSnippets += $snippet
        }
    }

    if ($missingSnippets.Count -gt 0) {
        Write-FailHealth ".gitignore is missing required source-boundary entries: $($missingSnippets -join ', ')"
    }
    else {
        Write-Pass ".gitignore contains required clean-source boundary entries."
    }

    $ignoredSamples = @(
        @{ Path = "AGENTS.md"; Description = "local AI collaboration rules" },
        @{ Path = "docs/CURRENT_STATUS.md"; Description = "legacy docs backflow guard sample" },
        @{ Path = "$localDocsRootName/sample.md"; Description = "local Chinese documentation" },
        @{ Path = "references/TerrariaDecompiled-1.4.5.6/Player.cs"; Description = "decompiled Terraria references" },
        @{ Path = "文档/AI经验笔记/Terraria原版参考笔记/README.md"; Description = "local Terraria API experience notes" },
        @{ Path = "external/TerrariaRefs/Terraria.exe"; Description = "local compile-only Terraria references" },
        @{ Path = ".codex-tmp/smoke/sample.txt"; Description = "Codex temp output" },
        @{ Path = "_codex_log_inspect_20260506/sample.txt"; Description = "local log inspection output" },
        @{ Path = "scripts/_apply_changes.py"; Description = "temporary generated helper script" },
        @{ Path = "src/JueMingZ/bin/Release/JueMingZ.dll"; Description = "build output" },
        @{ Path = "src/JueMingZ/obj/Release/JueMingZ.csproj.FileListAbsolute.txt"; Description = "MSBuild obj output" },
        @{ Path = "JueMingZ-TestPackage/JueMingZ.dll"; Description = "test package output" },
        @{ Path = "JueMingZ-TestPackage.zip"; Description = "test package zip artifact" },
        @{ Path = "JueMingZ-SourcePackage/README.md"; Description = "source package temp folder" },
        @{ Path = "JueMingZ-SourcePackage.zip"; Description = "optional source zip artifact" },
        @{ Path = "logs/jueming-z-20260524.log"; Description = "runtime logs" },
        @{ Path = "config/appsettings.local.json"; Description = "local config" },
        @{ Path = "diagnostics/runtime-snapshot.json"; Description = "diagnostic snapshot" }
    )

    foreach ($sample in $ignoredSamples) {
        Test-GitIgnoreExpectation -Path $sample.Path -ShouldBeIgnored $true -Description $sample.Description
    }

    $trackableSamples = @(
        @{ Path = "README.md"; Description = "repo entry" },
        @{ Path = "JueMingZ.sln"; Description = "solution file" },
        @{ Path = "NuGet.Config"; Description = "NuGet config" },
        @{ Path = "src/JueMingZ/Runtime/JueMingZRuntime.cs"; Description = "runtime source" },
        @{ Path = "scripts/audit-project-health.ps1"; Description = "audit script" },
        @{ Path = "tests/JueMingZ.Tests/Program.cs"; Description = "test source" },
        @{ Path = "external/ThirdParty/0Harmony.dll"; Description = "committed third-party dependency" }
    )

    foreach ($sample in $trackableSamples) {
        Test-GitIgnoreExpectation -Path $sample.Path -ShouldBeIgnored $false -Description $sample.Description
    }

    $trackedFiles = Get-TrackedFiles
    if ($trackedFiles.Count -eq 0) {
        return
    }

    $trackedReferenceLeaks = @(
        $trackedFiles | Where-Object {
            $_ -like "references/*"
        }
    )

    if ($trackedReferenceLeaks.Count -gt 0) {
        Write-FailHealth "Git index still tracks local-only references: $($trackedReferenceLeaks -join ', ')"
    }
    else {
        Write-Pass "Git index excludes local-only references."
    }

    $trackedGeneratedLeaks = @(
        $trackedFiles | Where-Object {
            $_ -match '(^|/)(bin|obj)/' -or
            $_ -eq 'AGENTS.md' -or
            $_ -match '^docs/' -or
            $_ -match "^$localDocsRootRegex/" -or
            $_ -match '^\.codex-tmp/' -or
            $_ -match '^_codex_log_inspect_[^/]*/' -or
            $_ -match '^external/TerrariaRefs/' -or
            $_ -match '^scripts/_[^/]+\.py$' -or
            $_ -match '^JueMingZ-TestPackage/' -or
            $_ -match '^JueMingZ-SourcePackage/' -or
            $_ -match '^(JueMingZ-TestPackage\.zip|JueMingZ-SourcePackage\.zip|JueMing-Z-main\.zip)$' -or
            $_ -match '^(logs|config|diagnostics)/' -or
            $_ -match '\.pdb$'
        }
    )

    if ($trackedGeneratedLeaks.Count -gt 0) {
        Write-FailHealth "Git index still tracks generated/local-only artifacts: $($trackedGeneratedLeaks -join ', ')"
    }
    else {
        Write-Pass "Git index excludes generated artifacts and local diagnostics."
    }
}

function Get-RuntimeVersion {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)
    $runtimePath = Join-Path $RepoRoot "src\JueMingZ\Runtime\JueMingZRuntime.cs"
    $text = Read-TextIfExists -Path $runtimePath
    if ($null -eq $text) {
        Write-FailHealth "Runtime source missing: $runtimePath"
        return $null
    }

    $match = [System.Text.RegularExpressions.Regex]::Match($text, 'public\s+const\s+string\s+Version\s*=\s*"([^"]+)"')
    if (-not $match.Success) {
        Write-FailHealth "RuntimeVersion constant was not found."
        return $null
    }

    Write-Pass "RuntimeVersion found: $($match.Groups[1].Value)"
    return $match.Groups[1].Value
}

function Test-SourcePackageZip {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)
    $zipPath = Join-Path $RepoRoot "JueMingZ-SourcePackage.zip"
    $localDocsZipPrefix = (Get-LocalDocsRootName).ToLowerInvariant() + "/"
    if (-not (Test-Path -LiteralPath $zipPath)) {
        Write-WarnHealth "JueMingZ-SourcePackage.zip is absent; root absence is allowed before packaging."
        return
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        $badEntries = @()
        foreach ($entry in $zip.Entries) {
            $name = $entry.FullName.Replace('\', '/')
            $lower = $name.ToLowerInvariant()
            $segments = $lower -split '/'
            $firstSegment = if ($segments.Count -gt 0) { $segments[0] } else { "" }
            if ($lower.StartsWith("juemingz-testpackage/") -or
                $lower.StartsWith("juemingz-sourcepackage/") -or
                $lower.StartsWith("docs/") -or
                $lower.StartsWith($localDocsZipPrefix) -or
                $lower.StartsWith("references/") -or
                $lower.StartsWith("external/terrariarefs/") -or
                $lower -eq "agents.md" -or
                $lower -eq "juemingz-sourcepackage.zip" -or
                $lower -eq "juemingz-testpackage.zip" -or
                $lower -eq "jueming-z-main.zip" -or
                $segments -contains ".git" -or
                $segments -contains ".vs" -or
                $segments -contains "bin" -or
                $segments -contains "obj" -or
                $lower -match '^scripts/_[^/]+\.py$' -or
                $lower -match '^_codex_log_inspect_[^/]*/' -or
                $firstSegment -eq "logs" -or
                $firstSegment -eq "config" -or
                $firstSegment -eq "diagnostics" -or
                $lower.EndsWith(".pdb") -or
                $lower.EndsWith(".log") -or
                $lower.EndsWith(".tmp") -or
                [System.IO.Path]::GetFileName($lower) -like "action-events-*.jsonl" -or
                [System.IO.Path]::GetFileName($lower) -eq "runtime-snapshot.json") {
                $badEntries += $name
            }
        }

        if ($badEntries.Count -gt 0) {
            Write-FailHealth "Source package zip contains generated/forbidden entries: $($badEntries -join ', ')"
        }
        else {
            Write-Pass "Source package zip exists at root and internal contents look clean."
        }

        $keyFiles = @(
            ".gitignore",
            "README.md",
            "JueMingZ.sln",
            "NuGet.Config",
            "src/JueMingZ/JueMingZ.csproj",
            "src/JueMingZ/Runtime/JueMingZRuntime.cs",
            "tests/JueMingZ.Tests/JueMingZ.Tests.csproj",
            "tests/JueMingZ.Tests/Program.cs",
            "scripts/build-test-package.ps1",
            "scripts/build-source-package.ps1",
            "scripts/audit-project-health.ps1",
            "scripts/audit-risky-mutations.ps1"
        )

        foreach ($relativePath in $keyFiles) {
            Test-ZipTextMatchesWorkspace -Zip $zip -RepoRoot $RepoRoot -RelativePath $relativePath
        }
    }
    finally {
        $zip.Dispose()
    }
}

function Test-TestPackageFreshness {
    param(
        [Parameter(Mandatory = $true)][string]$RepoRoot,
        [Parameter(Mandatory = $true)][string]$RuntimeVersion,
        [Parameter(Mandatory = $true)][string]$PackageDir,
        [switch]$RequireFreshPackage
    )

    $versionPath = Join-Path $PackageDir "VERSION.txt"
    $packageDllPath = Join-Path $PackageDir "JueMingZ.dll"
    $buildOutputRelativePath = "src/JueMingZ/bin/x86/Release/net472/JueMingZ.dll"
    $buildOutputDllPath = Join-Path $RepoRoot $buildOutputRelativePath
    $manifest = Read-KeyValueManifest -Path $versionPath

    if ($null -eq $manifest) {
        Write-TestPackageIssue "Test package VERSION.txt could not be read as a freshness manifest." -Strict:$RequireFreshPackage
        return
    }

    $requiredKeys = @(
        "PackageManifestVersion",
        "BuildTestPackageScriptVersion",
        "RuntimeVersion",
        "AssemblyVersion",
        "AssemblyInformationalVersion",
        "JueMingZDllSha256",
        "BuildOutputRelativePath",
        "BuildOutputLastWriteUtc",
        "PackagedAtUtc"
    )

    $missingKeys = @()
    foreach ($key in $requiredKeys) {
        if (-not $manifest.ContainsKey($key) -or [string]::IsNullOrWhiteSpace([string]$manifest[$key])) {
            $missingKeys += $key
        }
    }

    if ($missingKeys.Count -gt 0) {
        Write-TestPackageIssue "Test package VERSION.txt lacks freshness manifest key(s): $($missingKeys -join ', ')" -Strict:$RequireFreshPackage
        return
    }

    $manifestVersion = [string]$manifest["PackageManifestVersion"]
    $manifestRuntimeVersion = [string]$manifest["RuntimeVersion"]
    $manifestAssemblyVersion = [string]$manifest["AssemblyVersion"]
    $manifestInformationalVersion = [string]$manifest["AssemblyInformationalVersion"]
    $manifestDllHash = ([string]$manifest["JueMingZDllSha256"]).ToLowerInvariant()
    $manifestBuildOutputRelativePath = [string]$manifest["BuildOutputRelativePath"]

    if ($manifestVersion -eq "1") {
        Write-Pass "Test package VERSION.txt contains freshness manifest version 1."
    }
    else {
        Write-TestPackageIssue "Test package VERSION.txt has unsupported PackageManifestVersion=$manifestVersion." -Strict:$RequireFreshPackage
    }

    if ($manifestRuntimeVersion -eq $RuntimeVersion) {
        Write-Pass "Test package freshness manifest RuntimeVersion matches $RuntimeVersion."
    }
    else {
        Write-TestPackageIssue "Test package freshness manifest RuntimeVersion '$manifestRuntimeVersion' does not match '$RuntimeVersion'." -Strict:$RequireFreshPackage
    }

    if ($manifestInformationalVersion -eq $RuntimeVersion) {
        Write-Pass "Test package assembly informational version is captured in the freshness manifest."
    }
    else {
        Write-TestPackageIssue "Test package manifest AssemblyInformationalVersion '$manifestInformationalVersion' does not match RuntimeVersion '$RuntimeVersion'." -Strict:$RequireFreshPackage
    }

    if ($manifestBuildOutputRelativePath -eq $buildOutputRelativePath) {
        Write-Pass "Test package freshness manifest points at the standard Release x86 build output."
    }
    else {
        Write-TestPackageIssue "Test package freshness manifest uses unexpected BuildOutputRelativePath '$manifestBuildOutputRelativePath'." -Strict:$RequireFreshPackage
    }

    if (-not (Test-Path -LiteralPath $packageDllPath)) {
        Write-TestPackageIssue "Test package freshness check cannot read JueMingZ.dll." -Strict:$RequireFreshPackage
        return
    }

    $packageHash = Get-Sha256Hex -Path $packageDllPath
    if ($packageHash -eq $manifestDllHash) {
        Write-Pass "Test package JueMingZ.dll hash matches VERSION.txt freshness manifest."
    }
    else {
        Write-TestPackageIssue "Test package JueMingZ.dll hash differs from VERSION.txt freshness manifest." -Strict:$RequireFreshPackage
    }

    $packageAssemblyName = [System.Reflection.AssemblyName]::GetAssemblyName($packageDllPath)
    $packageInformationalVersion = Get-AssemblyInformationalVersion -Path $packageDllPath
    if ([string]$packageAssemblyName.Version -eq $manifestAssemblyVersion) {
        Write-Pass "Test package assembly version matches VERSION.txt freshness manifest."
    }
    else {
        Write-TestPackageIssue "Test package assembly version '$($packageAssemblyName.Version)' differs from manifest '$manifestAssemblyVersion'." -Strict:$RequireFreshPackage
    }

    if ($packageInformationalVersion -eq $manifestInformationalVersion) {
        Write-Pass "Test package assembly informational version matches VERSION.txt freshness manifest."
    }
    else {
        Write-TestPackageIssue "Test package assembly informational version '$packageInformationalVersion' differs from manifest '$manifestInformationalVersion'." -Strict:$RequireFreshPackage
    }

    if (-not (Test-Path -LiteralPath $buildOutputDllPath)) {
        Write-TestPackageIssue "Current Release x86 build output is missing; cannot prove test package freshness: $buildOutputRelativePath" -Strict:$RequireFreshPackage
        return
    }

    $buildOutputHash = Get-Sha256Hex -Path $buildOutputDllPath
    if ($buildOutputHash -eq $packageHash) {
        Write-Pass "Test package JueMingZ.dll matches the current Release x86 build output hash."
    }
    else {
        Write-TestPackageIssue "Test package JueMingZ.dll is stale or hand-edited; hash differs from current Release x86 build output." -Strict:$RequireFreshPackage
    }
}

function Test-TestPackage {
    param(
        [Parameter(Mandatory = $true)][string]$RepoRoot,
        [Parameter(Mandatory = $true)][string]$RuntimeVersion,
        [switch]$AllowReadme,
        [switch]$RequireFreshPackage
    )
    # Default health audits treat root test packages as optional local artifacts.
    # Use -RequireFreshTestPackage when validating a package for delivery.
    $packageDir = Join-Path $RepoRoot "JueMingZ-TestPackage"
    if (-not (Test-Path -LiteralPath $packageDir)) {
        Write-TestPackageIssue "JueMingZ-TestPackage is absent; root absence is allowed before packaging." -Strict:$RequireFreshPackage
        return
    }

    foreach ($name in @("JueMingZ.dll", "Terraria.exe.config", "VERSION.txt")) {
        if (Test-Path -LiteralPath (Join-Path $packageDir $name)) {
            Write-Pass "Test package contains $name"
        }
        else {
            Write-TestPackageIssue "Test package missing first-level file: $name" -Strict:$RequireFreshPackage
        }
    }

    $harmonyPath = Join-Path $packageDir "0Harmony.dll"
    if (Test-Path -LiteralPath $harmonyPath) {
        Write-TestPackageIssue "Test package should not contain external 0Harmony.dll; Harmony is embedded in JueMingZ.dll." -Strict:$RequireFreshPackage
    }
    else {
        Write-Pass "Test package does not contain external 0Harmony.dll."
    }

    foreach ($name in @(
            "Terraria.exe",
            "Microsoft.Xna.Framework.dll",
            "Microsoft.Xna.Framework.Game.dll",
            "Microsoft.Xna.Framework.Graphics.dll",
            "ReLogic.dll",
            "Newtonsoft.Json.dll"
        )) {
        if (Test-Path -LiteralPath (Join-Path $packageDir $name)) {
            Write-TestPackageIssue "Test package should not contain compile-only Terraria/XNA/ReLogic dependency: $name" -Strict:$RequireFreshPackage
        }
        else {
            Write-Pass "Test package excludes compile-only dependency: $name"
        }
    }

    Test-TestPackageFreshness -RepoRoot $RepoRoot -RuntimeVersion $RuntimeVersion -PackageDir $packageDir -RequireFreshPackage:$RequireFreshPackage

    $readmeFileName = "README_" +
        ([string][char]0x6d4b) +
        ([string][char]0x8bd5) +
        ([string][char]0x8bf4) +
        ([string][char]0x660e) +
        ".txt"
    $readmePath = Join-Path $packageDir $readmeFileName
    $encoded = Get-ChildItem -LiteralPath $packageDir -File -Filter "README_#U*.txt" -ErrorAction SilentlyContinue
    $hasChineseReadme = Test-Path -LiteralPath $readmePath
    $hasEncodedReadme = $encoded -and $encoded.Count -gt 0
    $isWindows = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
        [System.Runtime.InteropServices.OSPlatform]::Windows)

    if (-not $AllowReadme) {
        if ($hasChineseReadme) {
            Write-TestPackageIssue "Default test package should not contain README_测试说明.txt; generate it only when the user explicitly asks." -Strict:$RequireFreshPackage
        }
        else {
            Write-Pass "Default test package omits README_测试说明.txt."
        }

        if ($hasEncodedReadme) {
            Write-TestPackageIssue "Default test package should not contain encoded README_#U*.txt files." -Strict:$RequireFreshPackage
        }
        else {
            Write-Pass "No encoded README_#U*.txt file found in default test package."
        }

        return
    }

    # Optional README delivery remains Windows-facing; non-Windows encoded names
    # require Windows validation before becoming fatal.
    if ($hasChineseReadme) {
        Write-Pass "Optional test package README exists."
    }
    elseif ($hasEncodedReadme -and -not $isWindows) {
        Write-WarnHealth "Optional Chinese README file is absent, but README_#U*.txt was observed outside Windows; require Windows validation before treating this as user-facing failure."
    }
    else {
        Write-TestPackageIssue "Optional test package README was requested but is missing." -Strict:$RequireFreshPackage
    }

    if ($encoded -and $encoded.Count -gt 0) {
        if ($isWindows) {
            Write-TestPackageIssue "Encoded README_#U*.txt exists in Windows test package." -Strict:$RequireFreshPackage
        }
        else {
            Write-WarnHealth "README_#U*.txt observed outside Windows; do not treat as user-facing failure without Windows validation."
        }
    }
    else {
        Write-Pass "No encoded README_#U*.txt file found in optional README package."
    }

    if ($hasChineseReadme) {
        $readmeText = Read-TextIfExists -Path $readmePath
        if ($null -eq $readmeText) {
            Write-TestPackageIssue "Test package README could not be read." -Strict:$RequireFreshPackage
        }
        elseif ($readmeText.Contains($RuntimeVersion)) {
            Write-Pass "Test package README contains RuntimeVersion $RuntimeVersion"
        }
        else {
            Write-TestPackageIssue "Test package README does not contain RuntimeVersion $RuntimeVersion" -Strict:$RequireFreshPackage
        }

        $projectRulesDir = ConvertFrom-CodePoints @(0x9879, 0x76ee, 0x89c4, 0x5219)
        $testPackageReadmeTemplateFile = (ConvertFrom-CodePoints @(0x6d4b, 0x8bd5, 0x5305)) + "README" + (ConvertFrom-CodePoints @(0x6a21, 0x677f)) + ".zh-CN.txt"
        $templatePath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @($projectRulesDir, $testPackageReadmeTemplateFile)
        $templateText = Read-TextIfExists -Path $templatePath
        if ($null -eq $templateText) {
            Write-TestPackageIssue "Test package README template missing." -Strict:$RequireFreshPackage
        }
        else {
            $stableTemplate = $templateText.Replace("{{RuntimeVersion}}", $RuntimeVersion)
            $missingTemplateLines = @()
            foreach ($line in ($stableTemplate -split "\r?\n")) {
                if ([string]::IsNullOrWhiteSpace($line) -or $line.Contains("{{BuildTimeLocal}}")) {
                    continue
                }

                if (-not $readmeText.Contains($line)) {
                    $missingTemplateLines += $line
                }
            }

            if ($missingTemplateLines.Count -gt 0) {
                Write-TestPackageIssue "Test package README is stale versus template; missing line(s): $($missingTemplateLines -join ' | ')" -Strict:$RequireFreshPackage
            }
            else {
                Write-Pass "Test package README content matches current template lines."
            }
        }

        $onlyNeedConfirm = ConvertFrom-CodePoints @(0x53ea, 0x9700, 0x8981, 0x786e, 0x8ba4)
        $currentTestOnlyNeed = ConvertFrom-CodePoints @(0x5f53, 0x524d, 0x6d4b, 0x8bd5, 0x53ea, 0x9700, 0x8981)
        $trufflePromptConfirm = ConvertFrom-CodePoints @(0x677e, 0x9732, 0x866b, 0x63d0, 0x793a, 0x6587, 0x6848, 0x786e, 0x8ba4)
        $fishronPromptConfirm = ConvertFrom-CodePoints @(0x732a, 0x9ca8, 0x63d0, 0x793a, 0x6587, 0x6848, 0x786e, 0x8ba4)
        $oldRegressionList = ConvertFrom-CodePoints @(0x65e7, 0x9632, 0x56de, 0x5f52, 0x6e05, 0x5355, 0xff1a)
        $staleReadmePatterns = @(
            $onlyNeedConfirm,
            $currentTestOnlyNeed,
            $trufflePromptConfirm,
            $fishronPromptConfirm,
            ($trufflePromptConfirm.Replace((ConvertFrom-CodePoints @(0x63d0, 0x793a, 0x6587, 0x6848, 0x786e, 0x8ba4)), " / ") + $fishronPromptConfirm),
            $oldRegressionList
        )

        $staleHits = @()
        foreach ($pattern in $staleReadmePatterns) {
            if ($readmeText -and $readmeText.Contains($pattern)) {
                $staleHits += $pattern
            }
        }

        if ($staleHits.Count -gt 0) {
            Write-TestPackageIssue "Test package README contains stale testing wording: $($staleHits -join ', ')" -Strict:$RequireFreshPackage
        }
        else {
            Write-Pass "Test package README has no known stale focused-test wording."
        }
    }
}

function Test-VersionConsistency {
    param(
        [Parameter(Mandatory = $true)][string]$RepoRoot,
        [Parameter(Mandatory = $true)][string]$RuntimeVersion,
        [switch]$RequireFreshPackage
    )

    $versionPath = Join-Path $RepoRoot "JueMingZ-TestPackage\VERSION.txt"
    $versionText = Read-TextIfExists -Path $versionPath
    if ($null -ne $versionText) {
        if ($versionText.Contains($RuntimeVersion)) {
            Write-Pass "VERSION.txt contains RuntimeVersion $RuntimeVersion"
        }
        else {
            Write-TestPackageIssue "VERSION.txt does not contain RuntimeVersion $RuntimeVersion" -Strict:$RequireFreshPackage
        }
    }
    else {
        Write-WarnHealth "Test package VERSION.txt absent before packaging."
    }

    $csprojPath = Join-Path $RepoRoot "src\JueMingZ\JueMingZ.csproj"
    if (-not (Test-Path -LiteralPath $csprojPath)) {
        Write-FailHealth "JueMingZ.csproj missing."
        return
    }

    [xml]$project = Get-Content -LiteralPath $csprojPath -Raw
    $version = [string]$project.Project.PropertyGroup.Version
    $assemblyVersion = [string]$project.Project.PropertyGroup.AssemblyVersion
    $fileVersion = [string]$project.Project.PropertyGroup.FileVersion
    $informationalVersion = [string]$project.Project.PropertyGroup.InformationalVersion
    $runtimeMain = $RuntimeVersion -replace '-.*$', ''

    if ($informationalVersion -eq $RuntimeVersion) {
        Write-Pass "InformationalVersion matches RuntimeVersion."
    }
    else {
        Write-FailHealth "InformationalVersion '$informationalVersion' does not match RuntimeVersion '$RuntimeVersion'."
    }

    if ($version -eq $runtimeMain -and $assemblyVersion -eq "$runtimeMain.0" -and $fileVersion -eq "$runtimeMain.0") {
        Write-Pass "csproj Version/AssemblyVersion/FileVersion match $runtimeMain."
    }
    else {
        Write-FailHealth "csproj versions are inconsistent: Version=$version AssemblyVersion=$assemblyVersion FileVersion=$fileVersion RuntimeMain=$runtimeMain"
    }
}

function Test-DocsConsistency {
    param(
        [Parameter(Mandatory = $true)][string]$RepoRoot,
        [Parameter(Mandatory = $true)][string]$RuntimeVersion
    )

    $projectRulesDir = ConvertFrom-CodePoints @(0x9879, 0x76ee, 0x89c4, 0x5219)
    $documentationRulesDir = (ConvertFrom-CodePoints @(0x6587, 0x6863)) + (ConvertFrom-CodePoints @(0x89c4, 0x5219))
    $featureIntroDir = ConvertFrom-CodePoints @(0x529f, 0x80fd, 0x4ecb, 0x7ecd)
    $combatPageDir = ConvertFrom-CodePoints @(0x6218, 0x6597, 0x9875)
    $movementPageDir = ConvertFrom-CodePoints @(0x79fb, 0x52a8, 0x9875)
    $updateRecordsDir = ConvertFrom-CodePoints @(0x66f4, 0x65b0, 0x8bb0, 0x5f55)
    $aiExperienceDir = "AI" + (ConvertFrom-CodePoints @(0x7ecf, 0x9a8c, 0x7b14, 0x8bb0))
    $currentPlanDir = ConvertFrom-CodePoints @(0x5f53, 0x524d, 0x5728, 0x505a, 0x8ba1, 0x5212)
    $archivePlanDir = ConvertFrom-CodePoints @(0x5f52, 0x6863, 0x5386, 0x53f2, 0x8ba1, 0x5212)
    $docChangeHistoryDir = ConvertFrom-CodePoints @(0x6587, 0x6863, 0x66f4, 0x6539, 0x5386, 0x53f2)

    $currentStatusFile = (ConvertFrom-CodePoints @(0x5f53, 0x524d, 0x4ed3, 0x5e93, 0x72b6, 0x6001)) + ".md"
    $coldStartFile = (ConvertFrom-CodePoints @(0x51b7, 0x542f, 0x52a8, 0x8bf4, 0x660e)) + ".md"
    $directoryFile = (ConvertFrom-CodePoints @(0x76ee, 0x5f55)) + ".md"
    $indexFile = (ConvertFrom-CodePoints @(0x7d22, 0x5f15)) + ".md"
    $documentationGuideFile = (ConvertFrom-CodePoints @(0x6587, 0x6863, 0x4e66, 0x5199, 0x89c4, 0x8303)) + ".md"
    $migrationMapFile = (ConvertFrom-CodePoints @(0x8fc1, 0x79fb, 0x6620, 0x5c04, 0x8868)) + ".md"
    $packagingRulesFile = "AI" + (ConvertFrom-CodePoints @(0x6253, 0x5305, 0x4ea4, 0x4ed8, 0x89c4, 0x5219)) + ".md"
    $testingRulesFile = "AI" + (ConvertFrom-CodePoints @(0x6d4b, 0x8bd5, 0x89c4, 0x5219)) + ".md"
    $diagnosticsRulesFile = "AI" + (ConvertFrom-CodePoints @(0x8bca, 0x65ad, 0x65e5, 0x5fd7, 0x8bf4, 0x660e)) + ".md"
    $engineeringRulesFile = (ConvertFrom-CodePoints @(0x5de5, 0x7a0b, 0x89c4, 0x5219)) + ".md"
    $featureIndexFile = (ConvertFrom-CodePoints @(0x529f, 0x80fd, 0x7d22, 0x5f15)) + ".md"
    $autoFacingFile = (ConvertFrom-CodePoints @(0x81ea, 0x52a8, 0x8f6c, 0x5411)) + ".md"
    $simulatedJumpFile = (ConvertFrom-CodePoints @(0x6a21, 0x62df, 0x8fde, 0x8df3)) + ".md"
    $continuousDashFile = (ConvertFrom-CodePoints @(0x8fde, 0x7eed, 0x51b2, 0x523a)) + ".md"
    $teleportCorrectionFile = (ConvertFrom-CodePoints @(0x4f20, 0x9001, 0x4fee, 0x6b63)) + ".md"
    $safeLandingFile = (ConvertFrom-CodePoints @(0x667a, 0x80fd, 0x9632, 0x6454)) + ".md"
    $testPackageReadmeTemplateFile = (ConvertFrom-CodePoints @(0x6d4b, 0x8bd5, 0x5305)) + "README" + (ConvertFrom-CodePoints @(0x6a21, 0x677f)) + ".zh-CN.txt"

    $packagingRules = Read-TextIfExists -Path (Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @($projectRulesDir, $packagingRulesFile))
    if ($null -eq $packagingRules) {
        Write-FailHealth "AI packaging rules document missing."
    }
    else {
        $optionalWord = ([string][char]0x53ef) + ([string][char]0x9009)
        $notRequiredWord = ([string][char]0x4e0d) + ([string][char]0x5f3a) + ([string][char]0x5236)
        $oldHarmonyWordingFound = $false
        foreach ($line in ($packagingRules -split "\r?\n")) {
            if ($line.Contains("Harmony") -and
                ($line.Contains($optionalWord) -or $line.Contains($notRequiredWord))) {
                $oldHarmonyWordingFound = $true
                break
            }
        }

        if ($oldHarmonyWordingFound) {
            Write-FailHealth "AI packaging rules still contain old optional Harmony wording."
        }
        else {
            Write-Pass "AI packaging rules no longer say Harmony is optional."
        }
    }

    $featureCatalog = Read-TextIfExists -Path (Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @($featureIntroDir, $featureIndexFile))
    $autoFacingDoc = Read-TextIfExists -Path (Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @($featureIntroDir, $combatPageDir, $autoFacingFile))
    $combatRegistrar = Read-TextIfExists -Path (Join-Path $RepoRoot "src\JueMingZ\Features\Catalog\CombatFeatureRegistrar.cs")
    if ($null -ne $featureCatalog -and $null -ne $autoFacingDoc -and $autoFacingDoc -match "combat\.auto_facing") {
        if (($null -ne $combatRegistrar) -and
            ($combatRegistrar -match "CombatAutoFacing") -and
            ($combatRegistrar -match "\.Hotkey\(true,\s*true\)")) {
            Write-Pass "combat.auto_facing hotkey documentation matches code."
        }
        else {
            Write-FailHealth "combat.auto_facing documentation says the feature is present but code lacks .Hotkey(true, true)."
        }
    }
    else {
        Write-WarnHealth "Could not confirm combat.auto_facing row in the new feature documentation."
    }

    $documentationGuide = Read-TextIfExists -Path (Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @($documentationRulesDir, $documentationGuideFile))
    if ($null -eq $documentationGuide) {
        Write-FailHealth "New documentation writing guide missing."
    }
    elseif ($documentationGuide.Contains($updateRecordsDir) -and
        $documentationGuide.Contains($migrationMapFile) -and
        $documentationGuide.Contains($projectRulesDir) -and
        $documentationGuide.Contains("UTF-8")) {
        Write-Pass "New documentation writing guide contains core governance rules."
    }
    else {
        Write-FailHealth "New documentation writing guide lacks expected governance rules."
    }

    $movementDocs = @()
    $movementDocs += Read-TextIfExists -Path (Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @($featureIntroDir, $movementPageDir, $simulatedJumpFile))
    $movementDocs += Read-TextIfExists -Path (Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @($featureIntroDir, $movementPageDir, $continuousDashFile))
    $movementDocs += Read-TextIfExists -Path (Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @($featureIntroDir, $movementPageDir, $teleportCorrectionFile))
    $movementDocs += Read-TextIfExists -Path (Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @($featureIntroDir, $movementPageDir, $safeLandingFile))
    $movementDomain = $movementDocs -join "`n"
    if ([string]::IsNullOrWhiteSpace($movementDomain)) {
        Write-FailHealth "New movement feature documents are missing."
    }
    elseif ($movementDomain.Contains("movement.simulated_multi_jump") -and
        $movementDomain.Contains("movement.continuous_dash") -and
        $movementDomain.Contains("movement.teleport_correction") -and
        $movementDomain.Contains("movement.fall_protection")) {
        Write-Pass "New movement feature documents cover staged movement features."
    }
    else {
        Write-FailHealth "New movement feature documents lack expected movement feature coverage."
    }

    $entryRelativePaths = @(
        "AGENTS.md",
        (Join-Path (Join-Path (Get-LocalDocsRootName) $documentationRulesDir) $directoryFile),
        (Join-Path (Get-LocalDocsRootName) $coldStartFile)
    )
    foreach ($relativePath in $entryRelativePaths) {
        $entryText = Read-TextIfExists -Path (Join-Path $RepoRoot $relativePath)
        if ($null -eq $entryText) {
            Write-FailHealth "Documentation entry file missing: $relativePath"
        }
        elseif ($entryText.Contains($documentationGuideFile)) {
            Write-Pass "Documentation entry references the new writing guide: $relativePath"
        }
        else {
            Write-FailHealth "Documentation entry does not reference the new writing guide: $relativePath"
        }
    }

    $publicReadme = Read-TextIfExists -Path (Join-Path $RepoRoot "README.md")
    if ($null -eq $publicReadme) {
        Write-FailHealth "Public README.md missing."
    }
    elseif ($publicReadme.Contains("docs/") -or
        $publicReadme.Contains((Get-LocalDocsRootName) + "/") -or
        $publicReadme.Contains("AGENTS.md")) {
        Write-FailHealth "Public README.md references local-only AI/maintainer documentation."
    }
    else {
        Write-Pass "Public README.md is self-contained and does not require local-only documentation."
    }

    $currentStatusPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @($currentStatusFile)
    if (Test-Path -LiteralPath $currentStatusPath) {
        Write-FailHealth "Deprecated current status document still exists; use recent update records for handoff instead."
    }
    else {
        Write-Pass "Deprecated current status document is absent."
    }

    $handoffPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @($coldStartFile)
    $handoff = Read-TextIfExists -Path $handoffPath
    if ($null -eq $handoff) {
        Write-FailHealth "New cold-start handoff document missing."
    }
    else {
        $handoffLength = (Get-Item -LiteralPath $handoffPath).Length
        $handoffGuard = Get-DocumentationSizeGuard -RepoRoot $RepoRoot -Kind "Handoff"
        if ($handoffLength -gt $handoffGuard.Limit) {
            Write-FailHealth "New cold-start handoff is over adaptive size guard ($handoffLength > $($handoffGuard.Limit) bytes; registeredFeatures=$($handoffGuard.FeatureCount)); keep handoff short and move details to feature docs or update records."
        }
        else {
            Write-Pass "New cold-start handoff stays below adaptive size guard ($handoffLength <= $($handoffGuard.Limit) bytes; registeredFeatures=$($handoffGuard.FeatureCount))."
        }

        if ($handoff.Contains("RuntimeVersion") -or $handoff -match "(?<![\d.])(?:1\.7\.\d+|0\.\d+)(?:-[A-Za-z0-9][A-Za-z0-9-]*)?(?![\d.])") {
            Write-FailHealth "Cold-start handoff contains concrete version handoff content; keep version details in update records."
        }
        else {
            Write-Pass "Cold-start handoff avoids concrete version handoff content."
        }
    }

    $updateIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @($updateRecordsDir, $indexFile)
    $updateIndex = Read-TextIfExists -Path $updateIndexPath
    if ($null -eq $updateIndex) {
        Write-FailHealth "Update record index missing."
    }
    elseif ($updateIndex.Contains($RuntimeVersion) -and
        $updateIndex.Contains("最近 3") -and
        $updateIndex.Contains("实际交接")) {
        Write-Pass "Update record index carries the current handoff role and RuntimeVersion."
    }
    else {
        Write-FailHealth "Update record index does not describe recent-record handoff or current RuntimeVersion."
    }

    $requiredRoleDocs = @(
        @{ Segments = @($documentationRulesDir, $directoryFile); Description = "documentation rules directory map" },
        @{ Segments = @($projectRulesDir, $indexFile); Description = "project rules index" },
        @{ Segments = @($featureIntroDir, $featureIndexFile); Description = "feature introduction index" },
        @{ Segments = @($updateRecordsDir, $indexFile); Description = "update records index" },
        @{ Segments = @($docChangeHistoryDir, $indexFile); Description = "documentation change history index" },
        @{ Segments = @($aiExperienceDir, $indexFile); Description = "AI experience notes index" },
        @{ Segments = @($currentPlanDir, $indexFile); Description = "current plans index" },
        @{ Segments = @($archivePlanDir, $indexFile); Description = "archived plans index" }
    )

    foreach ($roleDoc in $requiredRoleDocs) {
        $roleText = Read-TextIfExists -Path (Join-LocalDocsPath -RepoRoot $RepoRoot -Segments $roleDoc.Segments)
        if ($null -eq $roleText) {
            Write-FailHealth "Folder role document missing: $($roleDoc.Description)"
        }
        elseif ($roleText.Contains("限制") -or $roleText.Contains("不负责") -or $roleText.Contains("不是")) {
            Write-Pass "Folder role document declares limits: $($roleDoc.Description)"
        }
        else {
            Write-FailHealth "Folder role document lacks explicit limits: $($roleDoc.Description)"
        }
    }

    $diagnosticsDoc = Read-TextIfExists -Path (Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @($projectRulesDir, $diagnosticsRulesFile))
    if ($null -eq $diagnosticsDoc) {
        Write-FailHealth "New diagnostics rules document missing."
    }
    elseif ($diagnosticsDoc.Contains("runtime-snapshot.json") -and
        $diagnosticsDoc.Contains((ConvertFrom-CodePoints @(0x5e9f, 0x5f03, 0x5b57, 0x6bb5))) -and
        $diagnosticsDoc.Contains((ConvertFrom-CodePoints @(0x66f4, 0x65b0, 0x8bb0, 0x5f55)))) {
        Write-Pass "New diagnostics rules cover snapshot fields, deprecated fields, and history routing."
    }
    else {
        Write-FailHealth "New diagnostics rules lack expected diagnostics governance markers."
    }

    $featureLifecyclePath = Join-Path $RepoRoot "src\JueMingZ\Features\FeatureLifecycleStatus.cs"
    if (-not (Test-Path -LiteralPath $featureLifecyclePath)) {
        Write-FailHealth "FeatureLifecycleStatus metadata is missing."
    }
    else {
        Write-Pass "Feature lifecycle metadata exists."
    }

    $worldAutomationRegistrar = Read-TextIfExists -Path (Join-Path $RepoRoot "src\JueMingZ\Features\Catalog\WorldAutomationFeatureRegistrar.cs")
    if ($null -eq $worldAutomationRegistrar) {
        Write-FailHealth "WorldAutomationFeatureRegistrar.cs missing."
    }
    elseif ($worldAutomationRegistrar.Contains("FeatureLifecycleStatus.Implemented") -and
        $worldAutomationRegistrar.Contains("VisibleInMainUi(true)") -and
        $worldAutomationRegistrar.Contains("WorldAutomationTravelMenu")) {
        Write-Pass "Travel menu is visible and explicitly marked implemented."
    }
    else {
        Write-FailHealth "Travel menu metadata is missing or not marked implemented."
    }

    $runtimeDomain = Read-TextIfExists -Path (Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @($projectRulesDir, $engineeringRulesFile))
    if ($null -ne $runtimeDomain -and
        $runtimeDomain.Contains("GameStateSnapshot") -and
        $runtimeDomain.Contains("GameStateReader")) {
        Write-Pass "New engineering rules document GameState routing."
    }
    else {
        Write-FailHealth "New engineering rules do not document GameState routing."
    }

    $testReadmeTemplatePath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @($projectRulesDir, $testPackageReadmeTemplateFile)
    $testReadmeTemplate = Read-TextIfExists -Path $testReadmeTemplatePath
    if ($null -eq $testReadmeTemplate) {
        Write-FailHealth "Test package README template missing."
    }
    else {
        if ($testReadmeTemplate.Contains("{{RuntimeVersion}}") -and $testReadmeTemplate.Contains("{{BuildTimeLocal}}")) {
            Write-Pass "Test package README template still contains required placeholders."
        }
        else {
            Write-FailHealth "Test package README template is missing required placeholders."
        }

        $oldRegressionList = ConvertFrom-CodePoints @(0x65e7, 0x9632, 0x56de, 0x5f52, 0x6e05, 0x5355, 0xff1a)
        $successfulPathRegression = ConvertFrom-CodePoints @(0x6210, 0x529f, 0x8def, 0x5f84, 0x56de, 0x5f52)
        $lightRegression = ConvertFrom-CodePoints @(0x8f7b, 0x91cf, 0x56de, 0x5f52)
        $scopeLeakPatterns = @(
            $oldRegressionList,
            $successfulPathRegression,
            $lightRegression
        )

        $scopeLeakHits = @()
        foreach ($pattern in $scopeLeakPatterns) {
            if ($testReadmeTemplate -and $testReadmeTemplate.Contains($pattern)) {
                $scopeLeakHits += $pattern
            }
        }

        if ($scopeLeakHits.Count -gt 0) {
            Write-FailHealth "Test package README template contains broad or stale regression wording: $($scopeLeakHits -join ', ')"
        }
        else {
            Write-Pass "Test package README template has no known broad regression wording."
        }

        Write-Pass "Test package README template has no byte-size limit."
    }
}

function Test-InformationFishingFallbackCleanup {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $path = Join-Path $RepoRoot "src\JueMingZ\Automation\Information\InformationOverlayService.cs"
    $text = Read-TextIfExists -Path $path
    if ($null -eq $text) {
        Write-FailHealth "InformationOverlayService.cs missing."
        return
    }

    $oldRulePendingText = ([string][char]0x89c4) + ([string][char]0x5219) +
        ([string][char]0x89e3) + ([string][char]0x6790) +
        ([string][char]0x5f85) + ([string][char]0x5b9e) +
        ([string][char]0x673a) + ([string][char]0x786e) +
        ([string][char]0x8ba4)
    $oldCatchPrefixText = ([string][char]0x53ef) + ([string][char]0x9493) +
        ([string][char]0x9c7c) + ([string][char]0x83b7)
    $oldFishingPowerText = ([string][char]0x9c7c) + ([string][char]0x529b) + " "

    $forbidden = @(
        "BuildFishingCatchesLine",
        "ScanLiquidNear",
        "BuildBiomeSummaryForFishing",
        "LiquidScanResult",
        $oldRulePendingText,
        $oldCatchPrefixText,
        $oldFishingPowerText
    )

    $found = @()
    foreach ($pattern in $forbidden) {
        if ($text.Contains($pattern)) {
            $found += $pattern
        }
    }

    if ($found.Count -gt 0) {
        Write-FailHealth "InformationOverlayService.cs still contains old fishing catch fallback text/code: $($found -join ', ')"
    }
    else {
        Write-Pass "Information fishing catch overlay has no old fallback text/code."
    }
}

function Test-LegacyUiOverlayGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $legacyRoot = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy"
    if (-not (Test-Path -LiteralPath $legacyRoot)) {
        Write-FailHealth "Legacy UI source directory missing."
        return
    }

    $expectedPopupPanelUses = @{
        "LegacyMainWindow.Blueprint.cs" = 2
        "LegacyMainWindow.Blueprint.Placed.cs" = 1
        "LegacyMainWindow.MapEnhancement.cs" = 1
        "LegacyMainWindow.MapEnhancement.RevealedArea.cs" = 1
        "LegacyMainWindow.Misc.cs" = 1
        "LegacyMainWindow.Movement.cs" = 1
        "LegacyMainWindow.Rows.Recovery.cs" = 1
    }
    $expectedAddUiBlockerUses = @{
        "LegacyMainWindow.Blueprint.cs" = 2
        "LegacyMainWindow.Blueprint.Placed.cs" = 1
        "LegacyMainWindow.Fishing.FilterExact.cs" = 1
        "LegacyMainWindow.Fishing.FilterPresets.cs" = 1
        "LegacyMainWindow.Shared.cs" = 1
    }

    $popupCounts = @{}
    $blockerCounts = @{}
    foreach ($file in Get-ChildItem -LiteralPath $legacyRoot -Recurse -Filter "*.cs" -File) {
        $relative = $file.FullName.Substring($legacyRoot.Length + 1).Replace('\', '/')
        $text = Read-TextIfExists -Path $file.FullName
        if ($null -eq $text) {
            continue
        }

        $popupCount = [System.Text.RegularExpressions.Regex]::Matches($text, 'new\s+LegacyPopupPanelControl\b').Count
        if ($popupCount -gt 0) {
            $popupCounts[$relative] = $popupCount
        }

        $blockerCount = [System.Text.RegularExpressions.Regex]::Matches($text, '\bAddUiBlocker\s*\(').Count
        if ($blockerCount -gt 0) {
            $blockerCounts[$relative] = $blockerCount
        }
    }

    $unexpectedPopupUses = @()
    foreach ($path in $popupCounts.Keys) {
        if (-not $expectedPopupPanelUses.ContainsKey($path) -or
            $popupCounts[$path] -ne $expectedPopupPanelUses[$path]) {
            $unexpectedPopupUses += "$path=$($popupCounts[$path])"
        }
    }

    foreach ($path in $expectedPopupPanelUses.Keys) {
        if (-not $popupCounts.ContainsKey($path) -or
            $popupCounts[$path] -ne $expectedPopupPanelUses[$path]) {
            $unexpectedPopupUses += "$path=$($popupCounts[$path]) expected=$($expectedPopupPanelUses[$path])"
        }
    }

    if ($unexpectedPopupUses.Count -gt 0) {
        Write-FailHealth "Legacy UI popup panel usage changed outside the F5 overlay allowlist: $($unexpectedPopupUses -join ', ')"
    }
    else {
        Write-Pass "Legacy UI popup panel usage stays inside the overlay allowlist."
    }

    $unexpectedBlockerUses = @()
    foreach ($path in $blockerCounts.Keys) {
        if (-not $expectedAddUiBlockerUses.ContainsKey($path) -or
            $blockerCounts[$path] -ne $expectedAddUiBlockerUses[$path]) {
            $unexpectedBlockerUses += "$path=$($blockerCounts[$path])"
        }
    }

    foreach ($path in $expectedAddUiBlockerUses.Keys) {
        if (-not $blockerCounts.ContainsKey($path) -or
            $blockerCounts[$path] -ne $expectedAddUiBlockerUses[$path]) {
            $unexpectedBlockerUses += "$path=$($blockerCounts[$path]) expected=$($expectedAddUiBlockerUses[$path])"
        }
    }

    if ($unexpectedBlockerUses.Count -gt 0) {
        Write-FailHealth "Legacy UI AddUiBlocker usage changed outside the F5 overlay allowlist: $($unexpectedBlockerUses -join ', ')"
    }
    else {
        Write-Pass "Legacy UI AddUiBlocker usage stays inside the overlay allowlist."
    }

    $coordinatorPath = Join-Path $legacyRoot "LegacyUiOverlayCoordinator.cs"
    $coordinator = Read-TextIfExists -Path $coordinatorPath
    if ($null -ne $coordinator -and
        $coordinator.Contains("LastStackSignature") -and
        $coordinator.Contains("ShouldBlockMainScroll") -and
        $coordinator.Contains("HasActiveModalAt")) {
        Write-Pass "Legacy UI overlay coordinator still owns stack signature, scroll blocking, and modal hit-test helpers."
    }
    else {
        Write-FailHealth "Legacy UI overlay coordinator is missing expected overlay ownership helpers."
    }
}

function Test-CombatAimDiagnosticsGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $path = Join-Path $RepoRoot "src\JueMingZ\Automation\Combat\CombatAimItemCheckService.cs"
    $text = Read-TextIfExists -Path $path
    if ($null -eq $text) {
        Write-FailHealth "CombatAimItemCheckService.cs missing."
        return
    }

    $buildDecisionJsonCalls = [System.Text.RegularExpressions.Regex]::Matches($text, '\bBuildDecisionJson\s*\(')
    if ($buildDecisionJsonCalls.Count -eq 2) {
        Write-Pass "Combat aim ItemCheck diagnostics JSON has only the gated call and helper declaration."
    }
    else {
        Write-FailHealth "Combat aim ItemCheck diagnostics JSON call count changed; keep JSON building behind the action-event gate."
    }

    $recordMethod = [System.Text.RegularExpressions.Regex]::Match(
        $text,
        'public\s+static\s+void\s+RecordItemCheckAim[\s\S]*?internal\s+static\s+void\s+ResetLogThrottleForTesting')
    if (-not $recordMethod.Success) {
        Write-FailHealth "Combat aim ItemCheck diagnostics recorder method was not found."
        return
    }

    $recordText = $recordMethod.Value
    $gateIndex = $recordText.IndexOf("ShouldRecordLogLocked", [System.StringComparison]::Ordinal)
    $jsonIndex = $recordText.IndexOf("BuildDecisionJson(decision, mouseOverrideApplied, restored)", [System.StringComparison]::Ordinal)
    if ($gateIndex -ge 0 -and $jsonIndex -gt $gateIndex) {
        Write-Pass "Combat aim ItemCheck diagnostics JSON stays behind the log throttle and action-event gate."
    }
    else {
        Write-FailHealth "Combat aim ItemCheck diagnostics JSON must remain after the log throttle gate."
    }
}

function Test-PhasebladeQuickSwitchDiagnosticsGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $runtimePath = Join-Path $RepoRoot "src\JueMingZ\Automation\Combat\CombatPhasebladeQuickSwitchRuntimeService.cs"
    $executorPath = Join-Path $RepoRoot "src\JueMingZ\Actions\Executors\RawInputActionExecutor.cs"
    $runtimeText = Read-TextIfExists -Path $runtimePath
    $executorText = Read-TextIfExists -Path $executorPath
    if ($null -eq $runtimeText) {
        Write-FailHealth "CombatPhasebladeQuickSwitchRuntimeService.cs missing."
        return
    }

    if ($runtimeText.Contains("DiagnosticActionRecorder.Record")) {
        Write-FailHealth "Phaseblade quick switch runtime must not append action events from its tick path."
    }
    else {
        Write-Pass "Phaseblade quick switch runtime keeps action-event writes out of the tick path."
    }

    $enabledGateIndex = $runtimeText.IndexOf("!settings.CombatPhasebladeQuickSwitchEnabled", [System.StringComparison]::Ordinal)
    $playerReadIndex = $runtimeText.IndexOf("TerrariaInputCompat.TryGetLocalPlayer", [System.StringComparison]::Ordinal)
    $rightNotHeldIndex = $runtimeText.IndexOf("if (!rightHeld)", [System.StringComparison]::Ordinal)
    $hotbarScanIndex = $runtimeText.IndexOf("FindEligibleHotbarSlots(inventory, eligibleSlots)", [System.StringComparison]::Ordinal)
    if ($enabledGateIndex -ge 0 -and
        $playerReadIndex -gt $enabledGateIndex -and
        $rightNotHeldIndex -gt $enabledGateIndex -and
        $hotbarScanIndex -gt $rightNotHeldIndex) {
        Write-Pass "Phaseblade quick switch keeps disabled and right-not-held paths ahead of player/hotbar work."
    }
    else {
        Write-FailHealth "Phaseblade quick switch hotbar scan must stay behind enabled and right-held gates."
    }

    if ($null -eq $executorText) {
        Write-FailHealth "RawInputActionExecutor.cs missing."
        return
    }

    $phasebladeEventCalls = [System.Text.RegularExpressions.Regex]::Matches($executorText, '\bRecordPhasebladeQuickSwitchCompletionEvent\s*\(')
    if ($phasebladeEventCalls.Count -eq 2) {
        Write-Pass "Phaseblade quick switch action event remains a completion-only RawInput event."
    }
    else {
        Write-FailHealth "Phaseblade quick switch action event call count changed; keep JSON construction off idle/runtime paths."
    }
}

function Test-MapQuickAnnouncementGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $runtimePath = Join-Path $RepoRoot "src\JueMingZ\Automation\Information\MapQuickAnnouncementRuntimeService.cs"
    $diagnosticsPath = Join-Path $RepoRoot "src\JueMingZ\Automation\Information\MapQuickAnnouncementDiagnostics.cs"
    $runtimeText = Read-TextIfExists -Path $runtimePath
    $diagnosticsText = Read-TextIfExists -Path $diagnosticsPath
    if ($null -eq $runtimeText) {
        Write-FailHealth "MapQuickAnnouncementRuntimeService.cs missing."
        return
    }

    if ($runtimeText.Contains("InputActionQueue") -or $runtimeText.Contains("ItemCheck")) {
        Write-FailHealth "Map quick announcement runtime must not backflow into ActionQueue or ItemCheck paths."
    }
    else {
        Write-Pass "Map quick announcement runtime stays out of ActionQueue and ItemCheck paths."
    }

    $notTriggeredIndex = $runtimeText.IndexOf("if (!hotkeyState.Triggered)", [System.StringComparison]::Ordinal)
    $resolveIndex = $runtimeText.IndexOf("ports.ResolveCurrent", [System.StringComparison]::Ordinal)
    if ($notTriggeredIndex -ge 0 -and $resolveIndex -gt $notTriggeredIndex) {
        Write-Pass "Map quick announcement target resolution remains behind the trigger edge."
    }
    else {
        Write-FailHealth "Map quick announcement target resolution must stay behind the hotkey trigger edge."
    }

    if ($runtimeText.Contains("MapQuickAnnouncementDiagnostics.RecordRuntimeResult") -and
        $null -ne $diagnosticsText -and
        $diagnosticsText.Contains("GetSnapshot()")) {
        Write-Pass "Map quick announcement diagnostics use a cached runtime snapshot path."
    }
    else {
        Write-FailHealth "Map quick announcement diagnostics must publish through the cached runtime snapshot helper."
    }

    $snapshotPath = Join-Path $RepoRoot "src\JueMingZ\Diagnostics\DiagnosticSnapshot.cs"
    $snapshotWriterPath = Join-Path $RepoRoot "src\JueMingZ\Diagnostics\DiagnosticSnapshotWriter.Json.cs"
    $snapshotText = Read-TextIfExists -Path $snapshotPath
    $snapshotWriterText = Read-TextIfExists -Path $snapshotWriterPath
    $requiredDiagnosticFields = @(
        "MapQuickAnnouncementLastResolveDetail",
        "MapQuickAnnouncementLastTargetSource",
        "MapQuickAnnouncementLastUiHoverSource",
        "MapQuickAnnouncementLastUiHoverState",
        "MapQuickAnnouncementLastUiHoverHookStatus",
        "MapQuickAnnouncementLastPendingState",
        "MapQuickAnnouncementLastHoverCacheAgeUpdates",
        "MapQuickAnnouncementLastPlacementLookupSource",
        "MapQuickAnnouncementLastFallbackReason",
        "MapQuickAnnouncementLastVisibilityVerdict",
        "MapQuickAnnouncementLastVisibilityReason",
        "MapQuickAnnouncementLastVisibleLayers",
        "MapQuickAnnouncementLastBlockedLayers",
        "MapQuickAnnouncementLastCircuitOnly",
        "MapQuickAnnouncementLastEchoGate",
        "MapQuickAnnouncementLastInvisibleAir",
        "MapQuickAnnouncementLastVisibilityUnavailableReason"
    )
    $missingDiagnosticFields = @()
    foreach ($field in $requiredDiagnosticFields) {
        if ($null -eq $diagnosticsText -or
            -not $diagnosticsText.Contains($field.Replace("MapQuickAnnouncement", "")) -or
            $null -eq $snapshotText -or
            -not $snapshotText.Contains($field) -or
            $null -eq $snapshotWriterText -or
            -not $snapshotWriterText.Contains($field)) {
            $missingDiagnosticFields += $field
        }
    }

    if ($missingDiagnosticFields.Count -gt 0) {
        Write-FailHealth "Map quick announcement source/fallback diagnostic fields are incomplete: $($missingDiagnosticFields -join ', ')"
    }
    else {
        Write-Pass "Map quick announcement source/fallback diagnostics reach the cached snapshot JSON."
    }

    $targetResolverPath = Join-Path $RepoRoot "src\JueMingZ\Automation\Information\MapQuickAnnouncementTargetResolver.cs"
    $textBuilderPath = Join-Path $RepoRoot "src\JueMingZ\Automation\Information\MapQuickAnnouncementTextBuilder.cs"
    $visibilityServicePath = Join-Path $RepoRoot "src\JueMingZ\Automation\Information\MapQuickAnnouncementVisibilityService.cs"
    $visibilityModelsPath = Join-Path $RepoRoot "src\JueMingZ\Automation\Information\MapQuickAnnouncementVisibilityModels.cs"
    $targetResolverText = Read-TextIfExists -Path $targetResolverPath
    $textBuilderText = Read-TextIfExists -Path $textBuilderPath
    $visibilityServiceText = Read-TextIfExists -Path $visibilityServicePath
    $visibilityModelsText = Read-TextIfExists -Path $visibilityModelsPath

    if ($null -ne $targetResolverText -and
        $targetResolverText.Contains("MapQuickAnnouncementVisibilityService.Evaluate") -and
        $targetResolverText.Contains("FilterTileTarget") -and
        $targetResolverText.Contains("AllowsWorldLayer") -and
        $targetResolverText.Contains("AllowsCircuitLayer")) {
        Write-Pass "Map quick announcement resolver keeps world-layer visibility filtering in the dedicated service route."
    }
    else {
        Write-FailHealth "Map quick announcement resolver must call the visibility service and keep tile/wall/liquid/circuit layer filtering explicit."
    }

    if ($null -ne $textBuilderText -and
        $textBuilderText.Contains('InvisibleWorldText = "这里看不见东西"') -and
        $null -ne $targetResolverText -and
        $targetResolverText.Contains("BuildVisibilityBlockedResult") -and
        $targetResolverText.Contains('FailureReason = "visibilityBlocked"')) {
        Write-Pass "Map quick announcement keeps the fixed invisible-world text and visibilityBlocked result path."
    }
    else {
        Write-FailHealth "Map quick announcement must retain the fixed invisible-world text and visibilityBlocked result path."
    }

    if ($null -ne $targetResolverText -and
        $targetResolverText.Contains("BuildTileDetail(filteredTile)") -and
        $targetResolverText.Contains("tile:circuitOnly") -and
        $targetResolverText.Contains("WithVisibilitySummary") -and
        $null -ne $visibilityModelsText -and
        $visibilityModelsText.Contains("HasCircuitOnlyLayer")) {
        Write-Pass "Map quick announcement circuit-only path uses filtered targets and diagnostics without hidden world-layer detail."
    }
    else {
        Write-FailHealth "Map quick announcement circuit-only path must use filtered targets and keep hidden tile/wall/liquid detail out."
    }

    $visibilityBackflowLeaks = @()
    $forbiddenVisibilityPaths = @(
        "src/JueMingZ/Automation/Information/MapQuickAnnouncementRuntimeService.cs",
        "src/JueMingZ/Runtime/Diagnostics/RuntimeDiagnosticSnapshotBuilder.InventoryInformationFishing.cs"
    )
    foreach ($relativePath in $forbiddenVisibilityPaths) {
        $text = Read-TextIfExists -Path (Join-Path $RepoRoot $relativePath)
        if ($null -ne $text -and $text.Contains("MapQuickAnnouncementVisibilityService")) {
            $visibilityBackflowLeaks += $relativePath
        }
    }

    $uiRoot = Join-Path $RepoRoot "src\JueMingZ\UI"
    foreach ($file in Get-ChildItem -LiteralPath $uiRoot -Recurse -Filter "*.cs" -File -ErrorAction SilentlyContinue) {
        $text = Read-TextIfExists -Path $file.FullName
        if ($null -ne $text -and $text.Contains("MapQuickAnnouncementVisibilityService")) {
            $visibilityBackflowLeaks += $file.FullName.Substring($RepoRoot.Length).TrimStart('\', '/').Replace('\', '/')
        }
    }

    if ($visibilityBackflowLeaks.Count -gt 0) {
        Write-FailHealth "Map quick announcement visibility service must not backflow into runtime, diagnostics builder, or UI paths: $($visibilityBackflowLeaks -join ', ')"
    }
    else {
        Write-Pass "Map quick announcement visibility business stays out of runtime, diagnostics builder, and UI paths."
    }

    $uiMouseCompatPath = Join-Path $RepoRoot "src\JueMingZ\Compat\TerrariaUiMouseCompat.cs"
    $uiMouseCompatText = Read-TextIfExists -Path $uiMouseCompatPath
    if ($null -ne $uiMouseCompatText -and
        $uiMouseCompatText.Contains("TerrariaUiHoverSlotReadResult") -and
        $uiMouseCompatText.Contains("freshEmptySlot") -and
        $uiMouseCompatText.Contains("mouseLeft") -and
        $uiMouseCompatText.Contains("expired")) {
        Write-Pass "Map quick announcement UI hover read status distinguishes empty, moved, and expired slot evidence."
    }
    else {
        Write-FailHealth "Map quick announcement UI hover diagnostics must keep explicit empty/moved/expired read statuses."
    }

    $itemSlotHoverHookPath = Join-Path $RepoRoot "src\JueMingZ\Hooks\MapQuickAnnouncementItemSlotHoverHookInstaller.cs"
    $itemSlotHoverHookText = Read-TextIfExists -Path $itemSlotHoverHookPath
    if ($null -ne $itemSlotHoverHookText -and
        $itemSlotHoverHookText.Contains("GetMouseHoverCandidateSummaryForTesting") -and
        $itemSlotHoverHookText.Contains("GetSelectedMouseHoverSignatureForTesting") -and
        $itemSlotHoverHookText.Contains("RecordItemSlotHoverHookInstallResult")) {
        Write-Pass "Map quick announcement ItemSlot hover hook keeps candidate diagnostics and install status reporting."
    }
    else {
        Write-FailHealth "Map quick announcement ItemSlot hover hook must keep candidate summary and install status diagnostics."
    }

    $informationRoot = Join-Path $RepoRoot "src\JueMingZ\Automation\Information"
    $actionEventLeaks = @()
    foreach ($file in Get-ChildItem -LiteralPath $informationRoot -Filter "MapQuickAnnouncement*.cs" -File -ErrorAction SilentlyContinue) {
        $text = Read-TextIfExists -Path $file.FullName
        if ($null -ne $text -and $text.Contains("DiagnosticActionRecorder")) {
            $actionEventLeaks += $file.FullName.Substring($RepoRoot.Length).TrimStart('\', '/').Replace('\', '/')
        }
    }

    if ($actionEventLeaks.Count -gt 0) {
        Write-FailHealth "Map quick announcement source must not append action events from runtime paths: $($actionEventLeaks -join ', ')"
    }
    else {
        Write-Pass "Map quick announcement keeps action-event writes out of runtime source files."
    }

    $placementCachePath = "src/JueMingZ/Automation/Information/MapQuickAnnouncementPlacementNameCache.cs"
    $placementCacheText = Read-TextIfExists -Path (Join-Path $RepoRoot $placementCachePath)
    if ($null -ne $placementCacheText -and
        $placementCacheText.Contains("EnsureInitialized()") -and
        $placementCacheText.Contains("_initialized") -and
        $placementCacheText.Contains("BuildLookupFromContentSamples")) {
        Write-Pass "Map quick announcement placement item scan remains behind the one-time cache initializer."
    }
    else {
        Write-FailHealth "Map quick announcement placement item lookup must stay behind the lazy one-time cache initializer."
    }

    $placementScanLeaks = @()
    foreach ($file in Get-ChildItem -LiteralPath $informationRoot -Filter "MapQuickAnnouncement*.cs" -File -ErrorAction SilentlyContinue) {
        $relative = $file.FullName.Substring($RepoRoot.Length).TrimStart('\', '/').Replace('\', '/')
        if ($relative -eq $placementCachePath) {
            continue
        }

        $text = Read-TextIfExists -Path $file.FullName
        if ($null -ne $text -and
            ($text.Contains("DerivedPlacementDetails") -or
             $text.Contains("ContentSamples.ItemsByType") -or
             $text.Contains("BuildLookupFromContentSamples"))) {
            $placementScanLeaks += $relative
        }
    }

    if ($placementScanLeaks.Count -gt 0) {
        Write-FailHealth "Map quick announcement placement item scans must not move into runtime/resolve files: $($placementScanLeaks -join ', ')"
    }
    else {
        Write-Pass "Map quick announcement runtime/resolve files do not scan all placement items per announcement."
    }

    $srcRoot = Join-Path $RepoRoot "src\JueMingZ"
    $allowedConsumePaths = @(
        "src/JueMingZ/Automation/MapEnhancement/MapCustomMarkerInteractionService.cs",
        "src/JueMingZ/Automation/Information/MapQuickAnnouncementRuntimeService.cs",
        "src/JueMingZ/Automation/Search/SearchItemPickRuntimeService.cs",
        "src/JueMingZ/UI/MapFootprintPlaybackOverlay.cs",
        "src/JueMingZ/UI/UiMouseCaptureService.cs",
        "src/JueMingZ/Compat/TerrariaUiMouseCompat.cs"
    )
    $consumeCounts = @{}
    $unexpectedConsumePaths = @()
    foreach ($file in Get-ChildItem -LiteralPath $srcRoot -Recurse -Filter "*.cs" -File) {
        $relative = $file.FullName.Substring($RepoRoot.Length).TrimStart('\', '/').Replace('\', '/')
        $text = Read-TextIfExists -Path $file.FullName
        if ($null -eq $text) {
            continue
        }

        $count = [System.Text.RegularExpressions.Regex]::Matches($text, '\bTryConsumeMouseTriggerInput\s*\(').Count
        if ($count -le 0) {
            continue
        }

        $consumeCounts[$relative] = $count
        if ($allowedConsumePaths -notcontains $relative) {
            $unexpectedConsumePaths += "$relative=$count"
        }
    }

    $missingConsumePaths = @()
    foreach ($allowedPath in $allowedConsumePaths) {
        if (-not $consumeCounts.ContainsKey($allowedPath)) {
            $missingConsumePaths += $allowedPath
        }
    }

    if ($unexpectedConsumePaths.Count -gt 0 -or $missingConsumePaths.Count -gt 0) {
        Write-FailHealth "Controlled UI mouse consume path changed; unexpected=$($unexpectedConsumePaths -join ', ') missing=$($missingConsumePaths -join ', ')"
    }
    else {
        Write-Pass "Controlled UI mouse input consumption remains centralized in approved runtime services, fullscreen overlays, UI capture service, and Terraria UI compat."
    }

    $actionsRoot = Join-Path $RepoRoot "src\JueMingZ\Actions"
    $actionBackflow = @()
    foreach ($file in Get-ChildItem -LiteralPath $actionsRoot -Recurse -Filter "*.cs" -File -ErrorAction SilentlyContinue) {
        $text = Read-TextIfExists -Path $file.FullName
        if ($null -ne $text -and $text.Contains("MapQuickAnnouncement")) {
            $actionBackflow += $file.FullName.Substring($RepoRoot.Length).TrimStart('\', '/').Replace('\', '/')
        }
    }

    if ($actionBackflow.Count -gt 0) {
        Write-FailHealth "Map quick announcement must not introduce ActionQueue action backflow: $($actionBackflow -join ', ')"
    }
    else {
        Write-Pass "Map quick announcement has no ActionQueue action backflow."
    }
}

function Test-FeatureToggleHotkeyGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $hotkeySettingsPath = Join-Path $RepoRoot "src\JueMingZ\Config\HotkeySettings.cs"
    $configServicePath = Join-Path $RepoRoot "src\JueMingZ\Config\ConfigService.cs"
    $chordPath = Join-Path $RepoRoot "src\JueMingZ\Config\FeatureToggleHotkeyChord.cs"
    $targetCatalogPath = Join-Path $RepoRoot "src\JueMingZ\Config\FeatureToggleHotkeyTargetCatalog.cs"
    $conflictRegistryPath = Join-Path $RepoRoot "src\JueMingZ\Config\FeatureToggleHotkeyConflictRegistry.cs"
    $runtimeServicePath = Join-Path $RepoRoot "src\JueMingZ\Input\FeatureToggleHotkeyService.cs"
    $dispatcherPath = Join-Path $RepoRoot "src\JueMingZ\Runtime\RuntimeAutomationDispatcher.cs"
    $uiPath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyMainWindow.FeatureToggleHotkeys.cs"
    $vectorIconPath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\Controls\LegacyVectorIconRenderer.cs"
    $testPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.FeatureToggleHotkeyTests.cs"
    $dispatchTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.RuntimeDiagnosticsAndDispatchTests.cs"
    $featureDocPath = Join-Path $RepoRoot "文档\功能介绍\F5通用\功能主开关快捷键.md"
    $featureIndexPath = Join-Path $RepoRoot "文档\功能介绍\功能索引.md"
    $diagnosticRulesPath = Join-Path $RepoRoot "文档\项目规则\AI诊断日志说明.md"
    $quickItemDocPath = Join-Path $RepoRoot "文档\功能介绍\物品页\快捷物品.md"
    $autoMiningDocPath = Join-Path $RepoRoot "文档\功能介绍\杂项页\自动挖矿.md"
    $quickAnnouncementDocPath = Join-Path $RepoRoot "文档\功能介绍\地图加强页\快捷宣告.md"
    $continuousDashDocPath = Join-Path $RepoRoot "文档\功能介绍\移动页\连续冲刺.md"

    $hotkeySettingsText = Read-TextIfExists -Path $hotkeySettingsPath
    $configServiceText = Read-TextIfExists -Path $configServicePath
    $chordText = Read-TextIfExists -Path $chordPath
    $targetCatalogText = Read-TextIfExists -Path $targetCatalogPath
    $conflictRegistryText = Read-TextIfExists -Path $conflictRegistryPath
    $runtimeServiceText = Read-TextIfExists -Path $runtimeServicePath
    $dispatcherText = Read-TextIfExists -Path $dispatcherPath
    $uiText = Read-TextIfExists -Path $uiPath
    $vectorIconText = Read-TextIfExists -Path $vectorIconPath
    $testText = Read-TextIfExists -Path $testPath
    $dispatchTestText = Read-TextIfExists -Path $dispatchTestPath
    $featureDocText = Read-TextIfExists -Path $featureDocPath
    $featureIndexText = Read-TextIfExists -Path $featureIndexPath
    $diagnosticRulesText = Read-TextIfExists -Path $diagnosticRulesPath
    $quickItemDocText = Read-TextIfExists -Path $quickItemDocPath
    $autoMiningDocText = Read-TextIfExists -Path $autoMiningDocPath
    $quickAnnouncementDocText = Read-TextIfExists -Path $quickAnnouncementDocPath
    $continuousDashDocText = Read-TextIfExists -Path $continuousDashDocPath

    if ($null -eq $hotkeySettingsText -or $null -eq $configServiceText -or $null -eq $chordText -or
        $null -eq $targetCatalogText -or $null -eq $conflictRegistryText -or $null -eq $runtimeServiceText -or
        $null -eq $dispatcherText -or $null -eq $uiText -or $null -eq $vectorIconText -or
        $null -eq $testText -or $null -eq $dispatchTestText -or $null -eq $featureDocText -or
        $null -eq $featureIndexText -or $null -eq $diagnosticRulesText) {
        Write-FailHealth "Feature toggle hotkey source, tests, diagnostics rules, and feature docs must exist before governance can be audited."
        return
    }

    if ($hotkeySettingsText.Contains("public int ConfigVersion { get; set; } = 4") -and
        $hotkeySettingsText.Contains("ToggleHotkeysByTargetId = new Dictionary<string, string>()") -and
        $hotkeySettingsText.Contains("LastNonOffModeByTargetId = new Dictionary<string, string>()") -and
        $configServiceText.Contains("NormalizeFeatureToggleHotkeys") -and
        $configServiceText.Contains("NormalizeFeatureToggleLastModes") -and
        $testText.Contains("FeatureToggleHotkeySettingsDefaultEmpty")) {
        Write-Pass "Feature toggle hotkey config fields default empty and migrate through dedicated normalization."
    }
    else {
        Write-FailHealth "Feature toggle hotkey config must keep ToggleHotkeysByTargetId/LastNonOffModeByTargetId default empty and normalized during migration."
    }

    if ($chordText.Contains("key.Length <= 0") -and
        $chordText.Contains("Escape") -and
        $chordText.Contains("modifier.Length > 0") -and
        $chordText.Contains("IsMouseToken") -and
        $testText.Contains('AssertInvalidFeatureToggleChord("Ctrl+Alt+K")') -and
        $testText.Contains('AssertInvalidFeatureToggleChord("MouseLeft")') -and
        $testText.Contains('AssertInvalidFeatureToggleChord("Esc")')) {
        Write-Pass "Feature toggle hotkey parser keeps the single-main-key or one-modifier grammar."
    }
    else {
        Write-FailHealth "Feature toggle hotkey parser/tests must reject pure modifiers, Esc, mouse keys, and multi-modifier chords."
    }

    $requiredTargets = @(
        "buff.auto_heal",
        "FeatureIds.InventoryQuickItemHotkeys",
        "FeatureIds.WorldAutomationAutoMining",
        "FeatureIds.MapQuickAnnouncement",
        "fishing.cut_rod_skip",
        "information.chest_name_labels",
        "FeatureIds.CombatAutoClicker",
        "FeatureIds.MovementContinuousDash"
    )
    $missingTargets = @()
    foreach ($target in $requiredTargets) {
        if (-not $targetCatalogText.Contains($target)) {
            $missingTargets += $target
        }
    }

    $excludedTargets = @(
        "FeatureIds.SearchMain",
        "FeatureIds.CombatAutoAim",
        "FeatureIds.MapDeathHistory",
        "FeatureIds.MapWorldDayCount",
        "FeatureIds.MapRevealedAreaRatio",
        "FeatureIds.FishingQuickRename",
        "FeatureIds.FishingFilter",
        "information.info_panel_position"
    )
    $catalogExcludedLeaks = @()
    foreach ($target in $excludedTargets) {
        if ($targetCatalogText.Contains($target)) {
            $catalogExcludedLeaks += $target
        }
    }

    if ($missingTargets.Count -eq 0 -and
        $catalogExcludedLeaks.Count -eq 0 -and
        $testText.Contains("FeatureToggleHotkeyEligibleAndExcludedTargets") -and
        $featureDocText.Contains("## 支持目标") -and
        $featureDocText.Contains("## 排除项") -and
        $featureDocText.Contains("search.main") -and
        $featureDocText.Contains("combat.auto_aim")) {
        Write-Pass "Feature toggle hotkey target catalog and docs keep eligible/excluded coverage."
    }
    else {
        Write-FailHealth "Feature toggle hotkey target catalog/docs incomplete; missing=$($missingTargets -join ', ') excludedLeaks=$($catalogExcludedLeaks -join ', ')"
    }

    if ($hotkeySettingsText.Contains("automation.auto_mining already uses that legacy table as its mining trigger") -and
        $conflictRegistryText.Contains("FeatureToggleHotkeyConflictType.AutoMiningTrigger") -and
        $conflictRegistryText.Contains("自动挖矿 的采集按键") -and
        $runtimeServiceText.Contains("ValidateAutoMiningMode") -and
        $runtimeServiceText.Contains("missingMiningTriggerHotkey") -and
        $testText.Contains("FeatureToggleHotkeyRuntimeBlocksAutoMiningHotkeyWithoutTrigger") -and
        $autoMiningDocText.Contains("采集触发键") -and
        $autoMiningDocText.Contains("ToggleHotkeysByTargetId[automation.auto_mining]")) {
        Write-Pass "Feature toggle hotkey keeps auto-mining trigger hotkeys separate from master-toggle hotkeys."
    }
    else {
        Write-FailHealth "Auto-mining collection trigger and feature-toggle hotkey separation must remain in source, tests, and docs."
    }

    $forbiddenRuntimeTokens = @(
        "InputActionQueue",
        "TryEnqueue",
        "MapQuickAnnouncementRuntimeService",
        "QuickItemHotkeyService",
        "TerrariaInputCompat",
        "TryConsumeMouseTriggerInput",
        "controlUseItem",
        "selectedItem"
    )
    $runtimeBackflowLeaks = @()
    foreach ($token in $forbiddenRuntimeTokens) {
        if ($runtimeServiceText.Contains($token)) {
            $runtimeBackflowLeaks += $token
        }
    }

    $legacyIndex = $dispatcherText.IndexOf("TargetingLegacyUiActions", [System.StringComparison]::Ordinal)
    $featureToggleIndex = $dispatcherText.IndexOf("TargetingFeatureToggleHotkeys", [System.StringComparison]::Ordinal)
    $mapMarkerIndex = $dispatcherText.IndexOf("TargetingMapCustomMarkers", [System.StringComparison]::Ordinal)
    $diagnosticHotkeyIndex = $dispatcherText.IndexOf("TargetingDiagnosticHotkeys", [System.StringComparison]::Ordinal)
    if ($runtimeBackflowLeaks.Count -eq 0 -and
        $runtimeServiceText.Contains('private const string Scenario = "Hotkey.FeatureToggle"') -and
        $runtimeServiceText.Contains("DiagnosticActionRecorder.RecordHotkeyEvent") -and
        $testText.Contains("FeatureToggleHotkeyRuntimeDoesNotMutateActionHotkeyPayloads") -and
        $dispatchTestText.Contains("targeting.feature-toggle-hotkeys|targeting.feature-toggle-hotkeys|1") -and
        $legacyIndex -ge 0 -and $featureToggleIndex -gt $legacyIndex -and
        $mapMarkerIndex -gt $featureToggleIndex -and $diagnosticHotkeyIndex -gt $mapMarkerIndex) {
        Write-Pass "Feature toggle hotkey runtime remains an event-level settings toggle between Legacy UI actions and map marker targeting."
    }
    else {
        Write-FailHealth "Feature toggle hotkey runtime must not submit actions or enter quick-item/quick-announcement/Compat input paths; leaks=$($runtimeBackflowLeaks -join ', ')"
    }

    if ($uiText.Contains('FeatureToggleHotkeyIconId = "keyboard"') -and
        $uiText.Contains("FeatureToggleHotkeyReserveWidth = 30") -and
        $vectorIconText.Contains('case "keyboard":') -and
        $testText.Contains("FeatureToggleHotkeyUiReserveAndIconContract") -and
        -not $uiText.Contains(".svg") -and
        -not $uiText.Contains(".png") -and
        -not $uiText.Contains(".bmp")) {
        Write-Pass "Feature toggle hotkey UI keeps the shared keyboard icon and 30px reserve without external image assets."
    }
    else {
        Write-FailHealth "Feature toggle hotkey UI must use the shared keyboard vector icon, reserve 30px, and avoid external bitmap/SVG assets."
    }

    if ($featureIndexText.Contains("F5通用") -and
        $featureIndexText.Contains("功能主开关快捷键.md") -and
        $featureDocText.Contains("默认全部未绑定") -and
        $featureDocText.Contains("Hotkey.FeatureToggle") -and
        $featureDocText.Contains("仍需用户实机确认") -and
        $diagnosticRulesText.Contains("scenario=Hotkey.FeatureToggle") -and
        $diagnosticRulesText.Contains("它不新增 runtime snapshot 字段") -and
        $quickItemDocText.Contains('只切换 `inventory.quick_item_hotkeys` 主开关') -and
        $quickAnnouncementDocText.Contains('只切换 `map.quick_announcement` 主开关') -and
        $continuousDashDocText.Contains("noLastNonOffMode")) {
        Write-Pass "Feature toggle hotkey docs describe diagnostics, exclusions, internal-hotkey separation, and user real-machine confirmation scope."
    }
    else {
        Write-FailHealth "Feature toggle hotkey feature docs/index/diagnostic rules must describe Hotkey.FeatureToggle, no snapshot fields, exclusions, special internal-hotkey boundaries, and remaining real-machine checks."
    }
}

function Test-F5MultiPageUiLayoutGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $mapUiPath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyMainWindow.MapEnhancement.cs"
    $searchUiPath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyMainWindow.Search.cs"
    $searchChestUiPath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyMainWindow.Search.ChestLocator.cs"
    $searchChestStatePath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\SearchChestLocatorUiState.cs"
    $fishingUiPath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyMainWindow.Fishing.cs"
    $miscUiPath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyMainWindow.Misc.cs"
    $mapQuickAnnouncementTestsPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.MapQuickAnnouncementTests.cs"
    $mapMarkerTestsPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.PlayerWorldMapMarkerTests.cs"
    $searchQueryTestsPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.SearchQueryTests.cs"
    $searchChestTestsPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.SearchChestLocatorTests.cs"
    $layoutTestsPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.LegacyUiLayoutCacheTests.cs"
    $featureIndexPath = Join-Path $RepoRoot "文档\功能介绍\功能索引.md"
    $quickAnnouncementDocPath = Join-Path $RepoRoot "文档\功能介绍\地图加强页\快捷宣告.md"
    $deathHistoryDocPath = Join-Path $RepoRoot "文档\功能介绍\地图加强页\死亡信息.md"
    $worldDayDocPath = Join-Path $RepoRoot "文档\功能介绍\地图加强页\世界天数.md"
    $revealedAreaDocPath = Join-Path $RepoRoot "文档\功能介绍\地图加强页\揭示区域.md"
    $persistentDeathDocPath = Join-Path $RepoRoot "文档\功能介绍\地图加强页\死亡点常驻.md"
    $footprintsDocPath = Join-Path $RepoRoot "文档\功能介绍\地图加强页\足迹.md"
    $rareDirectionDocPath = Join-Path $RepoRoot "文档\功能介绍\地图加强页\稀有生物显示方向.md"
    $merchantDirectionDocPath = Join-Path $RepoRoot "文档\功能介绍\地图加强页\旅商显示方向.md"
    $markerDocPath = Join-Path $RepoRoot "文档\功能介绍\地图加强页\地图标记.md"
    $searchDocPath = Join-Path $RepoRoot "文档\功能介绍\搜索查询页\搜索查询.md"
    $chestLocatorDocPath = Join-Path $RepoRoot "文档\功能介绍\搜索查询页\箱内物品定位.md"
    $quickRenameDocPath = Join-Path $RepoRoot "文档\功能介绍\钓鱼页\快捷改名.md"
    $fishingFilterDocPath = Join-Path $RepoRoot "文档\功能介绍\钓鱼页\钓鱼过滤.md"
    $developerMenuDocPath = Join-Path $RepoRoot "文档\功能介绍\杂项页\开发者菜单.md"

    $mapUiText = Read-TextIfExists -Path $mapUiPath
    $searchUiText = Read-TextIfExists -Path $searchUiPath
    $searchChestUiText = Read-TextIfExists -Path $searchChestUiPath
    $searchChestStateText = Read-TextIfExists -Path $searchChestStatePath
    $fishingUiText = Read-TextIfExists -Path $fishingUiPath
    $miscUiText = Read-TextIfExists -Path $miscUiPath
    $mapQuickAnnouncementTestsText = Read-TextIfExists -Path $mapQuickAnnouncementTestsPath
    $mapMarkerTestsText = Read-TextIfExists -Path $mapMarkerTestsPath
    $searchQueryTestsText = Read-TextIfExists -Path $searchQueryTestsPath
    $searchChestTestsText = Read-TextIfExists -Path $searchChestTestsPath
    $layoutTestsText = Read-TextIfExists -Path $layoutTestsPath
    $featureIndexText = Read-TextIfExists -Path $featureIndexPath
    $quickAnnouncementDocText = Read-TextIfExists -Path $quickAnnouncementDocPath
    $deathHistoryDocText = Read-TextIfExists -Path $deathHistoryDocPath
    $worldDayDocText = Read-TextIfExists -Path $worldDayDocPath
    $revealedAreaDocText = Read-TextIfExists -Path $revealedAreaDocPath
    $persistentDeathDocText = Read-TextIfExists -Path $persistentDeathDocPath
    $footprintsDocText = Read-TextIfExists -Path $footprintsDocPath
    $rareDirectionDocText = Read-TextIfExists -Path $rareDirectionDocPath
    $merchantDirectionDocText = Read-TextIfExists -Path $merchantDirectionDocPath
    $markerDocText = Read-TextIfExists -Path $markerDocPath
    $searchDocText = Read-TextIfExists -Path $searchDocPath
    $chestLocatorDocText = Read-TextIfExists -Path $chestLocatorDocPath
    $quickRenameDocText = Read-TextIfExists -Path $quickRenameDocPath
    $fishingFilterDocText = Read-TextIfExists -Path $fishingFilterDocPath
    $developerMenuDocText = Read-TextIfExists -Path $developerMenuDocPath

    if ($null -eq $mapUiText -or $null -eq $searchUiText -or $null -eq $searchChestUiText -or $null -eq $searchChestStateText -or $null -eq $fishingUiText -or $null -eq $miscUiText -or $null -eq $mapQuickAnnouncementTestsText -or $null -eq $mapMarkerTestsText -or $null -eq $searchQueryTestsText -or $null -eq $searchChestTestsText -or $null -eq $layoutTestsText -or $null -eq $featureIndexText -or $null -eq $quickAnnouncementDocText -or $null -eq $deathHistoryDocText -or $null -eq $worldDayDocText -or $null -eq $revealedAreaDocText -or $null -eq $persistentDeathDocText -or $null -eq $footprintsDocText -or $null -eq $rareDirectionDocText -or $null -eq $merchantDirectionDocText -or $null -eq $markerDocText -or $null -eq $searchDocText -or $null -eq $chestLocatorDocText -or $null -eq $quickRenameDocText -or $null -eq $fishingFilterDocText -or $null -eq $developerMenuDocText) {
        Write-FailHealth "F5 multi-page UI layout source, tests, and feature docs must exist before layout governance can be audited."
        return
    }

    $expectedMapOrder = '死亡信息`、`世界天数`、`揭示区域`、`死亡点常驻`、`足迹`、`稀有生物显示方向`、`旅商显示方向`、`快捷宣告`、`地图标记'

    if ($mapUiText.Contains("MapDeathHistoryRowIndex = 0") -and
        $mapUiText.Contains("MapWorldDayCountRowIndex = 1") -and
        $mapUiText.Contains("MapRevealedAreaRatioRowIndex = 2") -and
        $mapUiText.Contains("MapPersistentDeathMarkersRowIndex = 3") -and
        $mapUiText.Contains("MapFootprintsDisplayRowIndex = 4") -and
        $mapUiText.Contains("MapRareCreatureDirectionRowIndex = 5") -and
        $mapUiText.Contains("MapTravellingMerchantDirectionRowIndex = 6") -and
        $mapUiText.Contains("MapQuickAnnouncementRowIndex = 7") -and
        $mapUiText.Contains("MapCustomMarkersRowIndex = 8") -and
        $mapUiText.Contains("MapQuickAnnouncementSlotWidth = 64") -and
        $mapUiText.Contains("MapQuickAnnouncementSeparatorWidth = 12") -and
        $mapUiText.Contains('MapQuickAnnouncementOnTooltip = "按下三个快捷键对光标位置内容进行广播"') -and
        $mapUiText.Contains('var onWidth = ModeButtonWidth("开启")') -and
        $mapUiText.Contains('var offWidth = ModeButtonWidth("关闭")') -and
        $mapUiText.Contains('"+",') -and
        $mapUiText.Contains("CalculateMapDeathHistoryButtonRectsForTesting") -and
        $mapUiText.Contains("GetMapEnhancementRowOrderForTesting") -and
        $mapUiText.Contains("CalculateMapQuickAnnouncementLayoutForTesting")) {
        Write-Pass "F5 map page layout keeps the frozen row order, death-history geometry hooks, quick-announcement plus separators, and standard switch widths."
    }
    else {
        Write-FailHealth "F5 map page layout must keep rows 0-8 as death/history/world/revealed/persistent-death/footprints/rare/merchant/quick-announcement/map-marker, quick announcement 64px + separators, and standard switch widths."
    }

    if ($mapQuickAnnouncementTestsText.Contains("Map enhancement row order contract must expose all nine rows.") -and
        $mapQuickAnnouncementTestsText.Contains("Map death history details button must sit left of the rightmost count button") -and
        $mapQuickAnnouncementTestsText.Contains("Map quick announcement row must use 64px key slots, plus separators, and standard switch widths.") -and
        $mapQuickAnnouncementTestsText.Contains("Map quick announcement row must keep visual plus separators between the three keys without adding command rects.") -and
        $mapMarkerTestsText.Contains("Map custom marker list must stay attached to the final map marker row without an extra setting gap.")) {
        Write-Pass "F5 map page layout tests cover row order, death-history button positions, quick-announcement geometry, and marker list bottom attachment."
    }
    else {
        Write-FailHealth "F5 map page tests must cover row order, death-history button positions, quick-announcement 64px/+ geometry, and marker list attachment to the final marker row."
    }

    if ($quickAnnouncementDocText.Contains($expectedMapOrder) -and
        $quickAnnouncementDocText.Contains('中间用 `+` 做视觉分隔') -and
        $quickAnnouncementDocText.Contains("按下三个快捷键对光标位置内容进行广播") -and
        $deathHistoryDocText.Contains('次数，`详情` 按钮位于次数左侧') -and
        $worldDayDocText.Contains($expectedMapOrder) -and
        $revealedAreaDocText.Contains($expectedMapOrder) -and
        $persistentDeathDocText.Contains($expectedMapOrder) -and
        $footprintsDocText.Contains('位于 `死亡点常驻` 之后、`稀有生物显示方向` 之前') -and
        $rareDirectionDocText.Contains('在 `足迹` 后、`旅商显示方向` 前') -and
        $merchantDirectionDocText.Contains('在 `稀有生物显示方向` 后显示 `旅商显示方向`') -and
        $markerDocText.Contains('最后显示 `地图标记` 标准开关，位于 `快捷宣告` 后') -and
        $markerDocText.Contains('列表第一行贴着 `地图标记` 主开关行下沿')) {
        Write-Pass "F5 map feature docs keep the frozen row order, quick-announcement tooltip/plus text, death-history position, and marker-list bottom contract."
    }
    else {
        Write-FailHealth "F5 map feature docs must describe the frozen row order, quick-announcement +/tooltip contract, death-history count/details positions, and map marker list as part of the final marker group."
    }

    if ($searchUiText.Contains("SearchSharedInputLabelWidth = 96") -and
        $searchUiText.Contains("SearchSharedActionButtonWidth = 84") -and
        $searchUiText.Contains("SearchSharedClearButtonWidth = 48") -and
        $searchUiText.Contains("SearchSharedControlGap = 6") -and
        $searchUiText.Contains("SearchInputPickWidth = SearchSharedActionButtonWidth") -and
        $searchUiText.Contains("CalculateSearchSharedInputWidth(rowWidth)") -and
        $searchChestUiText.Contains("SearchChestLocatorLabelWidth = SearchSharedInputLabelWidth") -and
        $searchChestUiText.Contains("SearchChestLocatorSubmitWidth = SearchSharedActionButtonWidth") -and
        $searchChestUiText.Contains("SearchChestLocatorClearWidth = SearchSharedClearButtonWidth") -and
        $searchChestUiText.Contains("SearchChestLocatorControlGap = SearchSharedControlGap") -and
        $searchChestUiText.Contains('SearchChestLocatorSubmitButtonText = "定位容器"') -and
        $searchChestUiText.Contains("CalculateSearchSharedInputWidth(row.Width)") -and
        $searchChestStateText.Contains("点击定位容器")) {
        Write-Pass "F5 search page keeps shared query/chest-locator row geometry and the locator submit wording."
    }
    else {
        Write-FailHealth "F5 search page must keep shared 96/84/48/6 input geometry for query and chest locator rows, and keep the submit wording as 定位容器."
    }

    if ($searchQueryTestsText.Contains("Expected search query input row to use the shared label, action button, clear button, gap, and input widths.") -and
        $searchQueryTestsText.Contains("geometry[0] != 96") -and
        $searchQueryTestsText.Contains("geometry[1] != 84") -and
        $searchQueryTestsText.Contains("geometry[2] != 48") -and
        $searchQueryTestsText.Contains("geometry[3] != 6") -and
        $searchChestTestsText.Contains("Expected chest locator and search query input rows to share label reserve, action width, clear width, gap, and input width.") -and
        $searchChestTestsText.Contains("chest locator submit button label") -and
        $searchChestTestsText.Contains("点击定位容器")) {
        Write-Pass "F5 search layout tests cover shared geometry, 定位容器 wording, and default status text."
    }
    else {
        Write-FailHealth "F5 search layout tests must cover shared geometry, 定位容器 wording, and the default status text."
    }

    if ($searchDocText.Contains('按钮宽度与上方箱内定位的“定位容器”一致') -and
        $searchDocText.Contains("两个输入框也使用同一宽度") -and
        $chestLocatorDocText.Contains('提交按钮显示“定位容器”') -and
        $chestLocatorDocText.Contains('该按钮与下方搜索查询的“选择物品”等宽') -and
        $chestLocatorDocText.Contains('输入框也与“查询物品”输入框等宽') -and
        $featureIndexText.Contains('`选择物品` 与 `定位容器` 等宽') -and
        $featureIndexText.Contains("两个输入框等宽")) {
        Write-Pass "F5 search feature docs and index keep shared button/input geometry and 定位容器 wording."
    }
    else {
        Write-FailHealth "F5 search feature docs and function index must describe equal-width 选择物品/定位容器 buttons, equal input widths, and the submit wording as 定位容器."
    }

    if ($miscUiText.Contains("pending ? 0 : ModeButtonWidth(actionLabel)") -and
        $miscUiText.Contains('CalculateSingleMiscActionButtonWidth("开启", rowWidth, ModeButtonWidth("开启"))') -and
        $fishingUiText.Contains('"自动存放鱼"') -and
        $fishingUiText.Contains('"切杆跳过"') -and
        $fishingUiText.Contains('"快捷改名"') -and
        $fishingUiText.Contains("GetFishingPageTopOrderForTesting") -and
        $fishingUiText.Contains("GetFishingFilterContentYForTesting") -and
        $fishingUiText.Contains("LegacyUiMetrics.RowHeight * 5") -and
        $layoutTestsText.Contains("Expected developer menu default button to use standard width while the confirm warning stays readable.") -and
        $layoutTestsText.Contains("fishing page cut-rod row") -and
        $layoutTestsText.Contains("fishing page quick rename row") -and
        $layoutTestsText.Contains("Expected fishing filter start to keep the existing five-row content offset.") -and
        $layoutTestsText.Contains("Expected fishing content height to remain tied to the unchanged filter start.")) {
        Write-Pass "F5 misc/fishing UI and tests keep developer-menu default width, readable warning width, fishing row order, and filter height contract."
    }
    else {
        Write-FailHealth "F5 misc/fishing UI and tests must keep developer-menu default standard width, readable warning width, cut-rod above quick-rename, and unchanged five-row filter height."
    }

    if ($developerMenuDocText.Contains('按钮默认显示“开启”，按钮宽度与 F5 标准二态开关一致') -and
        $developerMenuDocText.Contains("确认态保留更宽的可读按钮") -and
        $quickRenameDocText.Contains('该行位于“切杆跳过”下方') -and
        $fishingFilterDocText.Contains('“切杆跳过”位于“快捷改名”上方') -and
        $fishingFilterDocText.Contains("过滤区仍在这两行之后") -and
        $featureIndexText.Contains('`开发者菜单` 默认 `开启` 为标准开关宽度') -and
        $featureIndexText.Contains('`切杆跳过` 位于 `快捷改名` 上方')) {
        Write-Pass "F5 misc/fishing feature docs and index keep the developer-menu and fishing row-order contracts."
    }
    else {
        Write-FailHealth "F5 misc/fishing feature docs and function index must describe developer-menu default standard width, readable warning width, and cut-rod above quick-rename."
    }
}

function Test-UserNotesGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $tabBarPath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyTabBar.cs"
    $vectorIconPath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\Controls\LegacyVectorIconRenderer.cs"
    $featureIdsPath = Join-Path $RepoRoot "src\JueMingZ\Common\FeatureIds.cs"
    $registrarPath = Join-Path $RepoRoot "src\JueMingZ\Features\Catalog\InformationFeatureRegistrar.cs"
    $categoryPath = Join-Path $RepoRoot "src\JueMingZ\Features\FeatureUserCategory.cs"
    $storePath = Join-Path $RepoRoot "src\JueMingZ\Automation\Information\Notes\UserNotesStore.cs"
    $modelsPath = Join-Path $RepoRoot "src\JueMingZ\Automation\Information\Notes\UserNotesModels.cs"
    $cachePath = Join-Path $RepoRoot "src\JueMingZ\Automation\Information\Notes\UserNotesCache.cs"
    $diagnosticsPath = Join-Path $RepoRoot "src\JueMingZ\Automation\Information\Notes\UserNotesDiagnostics.cs"
    $metricsPath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyUiMetrics.cs"
    $notesWindowPath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyMainWindow.Notes.cs"
    $notesSharedPath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyMainWindow.Shared.cs"
    $notesStatePath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\UserNotesUiState.cs"
    $multilineInputPath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyMultilineTextInput.cs"
    $legacyTextInputPath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyTextInput.cs"
    $textInputCompatPath = Join-Path $RepoRoot "src\JueMingZ\Compat\TerrariaTextInputCompat.cs"
    $scrollPath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyUiInput.Scroll.cs"
    $mouseInputPath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyUiInput.Mouse.cs"
    $layersPath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyMainWindow.Layers.cs"
    $userNotesActionPath = Join-Path $RepoRoot "src\JueMingZ\Input\LegacyUiActionService.UserNotes.cs"
    $pinnedOverlayPath = Join-Path $RepoRoot "src\JueMingZ\UI\UserNotesPinnedOverlay.cs"
    $pinnedOverlayCoordinatesPath = Join-Path $RepoRoot "src\JueMingZ\UI\UserNotesPinnedOverlayCoordinates.cs"
    $pinnedOverlayStatePath = Join-Path $RepoRoot "src\JueMingZ\UI\UserNotesPinnedOverlayState.cs"
    $interfaceLayerCallbacksPath = Join-Path $RepoRoot "src\JueMingZ\Hooks\InterfaceLayerHookCallbacks.cs"
    $hookInstallerPath = Join-Path $RepoRoot "src\JueMingZ\Bootstrap\HookInstaller.cs"
    $playerInputHookPath = Join-Path $RepoRoot "src\JueMingZ\Hooks\PlayerInputScrollHookInstaller.cs"
    $hotbarHookPath = Join-Path $RepoRoot "src\JueMingZ\Hooks\ScrollHotbarHookInstaller.cs"
    $storeTestsPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.UserNotesStoreTests.cs"
    $uiTestsPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.UserNotesUiTests.cs"
    $overlayTestsPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.UserNotesOverlayTests.cs"
    $interfaceLayerTestsPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.InterfaceLayerHookTests.cs"
    $programTestsPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.cs"
    $featureDocPath = Join-Path $RepoRoot "文档\功能介绍\F5通用\笔记页.md"
    $featureIndexPath = Join-Path $RepoRoot "文档\功能介绍\功能索引.md"
    $diagnosticRulesPath = Join-Path $RepoRoot "文档\项目规则\AI诊断日志说明.md"
    $legacyPlan06Path = Join-Path $RepoRoot "文档\归档历史计划\笔记页与悬挂便签实现\06-诊断测试文档审计护栏.md"
    $feedbackPlan06Path = Join-Path $RepoRoot "文档\归档历史计划\笔记页实机反馈修复\06-诊断测试文档审计护栏.md"
    $systemPlan05Path = Join-Path $RepoRoot "文档\归档历史计划\悬挂浮窗系统性修复\05-诊断测试文档审计护栏.md"

    $tabBarText = Read-TextIfExists -Path $tabBarPath
    $vectorIconText = Read-TextIfExists -Path $vectorIconPath
    $featureIdsText = Read-TextIfExists -Path $featureIdsPath
    $registrarText = Read-TextIfExists -Path $registrarPath
    $categoryText = Read-TextIfExists -Path $categoryPath
    $storeText = Read-TextIfExists -Path $storePath
    $modelsText = Read-TextIfExists -Path $modelsPath
    $cacheText = Read-TextIfExists -Path $cachePath
    $diagnosticsText = Read-TextIfExists -Path $diagnosticsPath
    $metricsText = Read-TextIfExists -Path $metricsPath
    $notesWindowText = Read-TextIfExists -Path $notesWindowPath
    $notesSharedText = Read-TextIfExists -Path $notesSharedPath
    $notesStateText = Read-TextIfExists -Path $notesStatePath
    $multilineInputText = Read-TextIfExists -Path $multilineInputPath
    $legacyTextInputText = Read-TextIfExists -Path $legacyTextInputPath
    $textInputCompatText = Read-TextIfExists -Path $textInputCompatPath
    $scrollText = Read-TextIfExists -Path $scrollPath
    $mouseInputText = Read-TextIfExists -Path $mouseInputPath
    $layersText = Read-TextIfExists -Path $layersPath
    $userNotesActionText = Read-TextIfExists -Path $userNotesActionPath
    $pinnedOverlayText = Read-TextIfExists -Path $pinnedOverlayPath
    $pinnedOverlayCoordinatesText = Read-TextIfExists -Path $pinnedOverlayCoordinatesPath
    $pinnedOverlayStateText = Read-TextIfExists -Path $pinnedOverlayStatePath
    $interfaceLayerCallbacksText = Read-TextIfExists -Path $interfaceLayerCallbacksPath
    $hookInstallerText = Read-TextIfExists -Path $hookInstallerPath
    $playerInputHookText = Read-TextIfExists -Path $playerInputHookPath
    $hotbarHookText = Read-TextIfExists -Path $hotbarHookPath
    $storeTestsText = Read-TextIfExists -Path $storeTestsPath
    $uiTestsText = Read-TextIfExists -Path $uiTestsPath
    $overlayTestsText = Read-TextIfExists -Path $overlayTestsPath
    $interfaceLayerTestsText = Read-TextIfExists -Path $interfaceLayerTestsPath
    $programTestsText = Read-TextIfExists -Path $programTestsPath
    $featureDocText = Read-TextIfExists -Path $featureDocPath
    $featureIndexText = Read-TextIfExists -Path $featureIndexPath
    $diagnosticRulesText = Read-TextIfExists -Path $diagnosticRulesPath
    $legacyPlan06Text = Read-TextIfExists -Path $legacyPlan06Path
    $feedbackPlan06Text = Read-TextIfExists -Path $feedbackPlan06Path
    $systemPlan05Text = Read-TextIfExists -Path $systemPlan05Path

    if ($null -eq $tabBarText -or $null -eq $vectorIconText -or $null -eq $featureIdsText -or
        $null -eq $registrarText -or $null -eq $categoryText -or $null -eq $storeText -or $null -eq $modelsText -or
        $null -eq $cacheText -or $null -eq $diagnosticsText -or $null -eq $metricsText -or $null -eq $notesWindowText -or
        $null -eq $notesSharedText -or $null -eq $notesStateText -or $null -eq $multilineInputText -or
        $null -eq $legacyTextInputText -or $null -eq $textInputCompatText -or $null -eq $scrollText -or
        $null -eq $mouseInputText -or $null -eq $layersText -or $null -eq $userNotesActionText -or
        $null -eq $pinnedOverlayText -or $null -eq $pinnedOverlayCoordinatesText -or
        $null -eq $pinnedOverlayStateText -or $null -eq $interfaceLayerCallbacksText -or $null -eq $hookInstallerText -or
        $null -eq $playerInputHookText -or $null -eq $hotbarHookText -or $null -eq $storeTestsText -or
        $null -eq $uiTestsText -or $null -eq $overlayTestsText -or $null -eq $interfaceLayerTestsText -or
        $null -eq $programTestsText -or $null -eq $featureDocText -or $null -eq $featureIndexText -or
        $null -eq $diagnosticRulesText -or $null -eq $legacyPlan06Text -or $null -eq $feedbackPlan06Text -or
        $null -eq $systemPlan05Text) {
        Write-FailHealth "User notes source, tests, feature docs, diagnostics rules, and plan coverage matrix must exist before governance can be audited."
        return
    }

    if ($tabBarText.Contains('new LegacyTabDefinition("hotkeys", "笔记", "note"') -and
        $vectorIconText.Contains('case "note":') -and
        $vectorIconText.Contains("DrawNote(mask)") -and
        $uiTestsText.Contains("UserNotesTabKeepsHotkeysPageIdAndUsesNoteIcon") -and
        $featureDocText.Contains('page id 仍是 `hotkeys`') -and
        $featureIndexText.Contains("F5通用/笔记页.md")) {
        Write-Pass "User notes keep the stable hotkeys page id with the note display/icon contract."
    }
    else {
        Write-FailHealth "User notes must keep page id hotkeys, display 笔记, use the built-in note vector icon, and document that boundary."
    }

    if ($featureIdsText.Contains('InformationUserNotes = "information.user_notes"') -and
        $registrarText.Contains("FeatureIds.InformationUserNotes") -and
        $registrarText.Contains(".Domain(FeatureCodeDomain.Information)") -and
        $registrarText.Contains(".Category(FeatureUserCategory.MoreInformation)") -and
        $registrarText.Contains(".Actions(InputActionKind.None)") -and
        $registrarText.Contains(".GameState(GameStateKind.UiState)") -and
        $registrarText.Contains(".VisibleInMainUi(false)") -and
        $registrarText.Contains(".Implemented(false)") -and
        -not $categoryText.Contains("UserNotes") -and
        $storeTestsText.Contains("FeatureCatalogExposesUserNotesAsPlannedHiddenInformation")) {
        Write-Pass "User notes FeatureId and registrar stay information/ui-only without adding a user category."
    }
    else {
        Write-FailHealth "User notes FeatureId/registrar must remain information.user_notes, Information/MoreInformation, Actions=None, UiState, planned hidden, with no new FeatureUserCategory."
    }

    if ($storeText.Contains('Path.Combine(ConfigService.ConfigDirectory, "notes")') -and
        $storeText.Contains('get { return Path.Combine(_notesDirectory, "index.json"); }') -and
        $storeText.Contains('NormalizeNoteId') -and
        $storeText.Contains('CreateTempPath') -and
        $storeText.Contains('File.Replace(_tempPath, _targetPath, null, true)') -and
        $storeText.Contains('File.Move(_tempPath, _targetPath)') -and
        $storeText.Contains('RollbackCommitted') -and
        $cacheText.Contains('UserNotesSnapshot') -and
        $storeTestsText.Contains('AssertPathUnderConfigDirectory(UserNotesStore.GetDefaultNotesDirectory()') -and
        $storeTestsText.Contains('UserNotesCacheSnapshotDoesNotTouchDisk') -and
        -not $storeText.Contains("player-worlds")) {
        Write-Pass "User notes storage stays under ConfigService notes with safe writes and cache-only draw snapshots."
    }
    else {
        Write-FailHealth "User notes storage must use ConfigService.ConfigDirectory\\notes, index/body files, safe temp commit, delete/save failure protection, and cache snapshots without player-world storage."
    }

    $hotPathLeaks = @()
    $hotPathTexts = @(
        @{ Name = "LegacyMainWindow.Notes"; Text = $notesWindowText },
        @{ Name = "UserNotesUiState"; Text = $notesStateText },
        @{ Name = "UserNotesPinnedOverlay"; Text = $pinnedOverlayText },
        @{ Name = "UserNotesPinnedOverlayState"; Text = $pinnedOverlayStateText }
    )
    foreach ($hotPath in $hotPathTexts) {
        foreach ($token in @("File.", "Directory.", "index.json", ".txt")) {
            if ($hotPath.Text.Contains($token)) {
                $hotPathLeaks += "$($hotPath.Name):$token"
            }
        }
    }

    if ($hotPathLeaks.Count -eq 0 -and
        $notesWindowText.Contains("UserNotesUiState.BuildLayout") -and
        $notesStateText.Contains("GetCache().Snapshot") -and
        $layersText.Contains('string.Equals(selectedPage, "hotkeys", StringComparison.Ordinal)') -and
        $scrollText.Contains("UserNotesUiState.TryConsumeNestedScroll")) {
        Write-Pass "User notes F5 and pinned draw/layout hot paths avoid file IO and route through cache/nested-scroll state."
    }
    else {
        Write-FailHealth "User notes draw/hover/layout hot paths must avoid File/Directory/index/body IO and use cache snapshots; leaks=$($hotPathLeaks -join ', ')"
    }

    if ($multilineInputText.Contains("allowNewLine") -and
        $multilineInputText.Contains('InsertTextLocked("\n", allowNewLine, maxLength)') -and
        $multilineInputText.Contains("TryAttachImeCompositionPanel") -and
        $legacyTextInputText.Contains('Replace("\n", string.Empty)') -and
        $notesStateText.Contains("LegacyMultilineTextInput") -and
        $uiTestsText.Contains("UserNotesBodyEditorSavesNewlinesAndKeepsDraftOnFailure") -and
        $uiTestsText.Contains("UserNotesMultilineTextInputHandlesCursorSubmitAndCancel")) {
        Write-Pass "User notes body editor uses the dedicated multiline input path instead of the newline-stripping short input."
    }
    else {
        Write-FailHealth "User notes body editor must keep a dedicated multiline editor with newline, cursor, IME, save-failure, and cancel tests."
    }

    if ($metricsText.Contains("public const float ButtonTextScale = 0.88f;") -and
        $metricsText.Contains("public const float TabTextScale = 0.96f;") -and
        $notesStateText.Contains("private const int BodyTextInset = 10;") -and
        $notesStateText.Contains("private const int BodyLineHeight = 24;") -and
        $notesStateText.Contains("private const float TitleTextScale = 0.86f;") -and
        $notesStateText.Contains("private const float BodyTextScale = 0.76f;") -and
        $notesStateText.Contains("ResolveBodyTextViewport") -and
        $notesStateText.Contains("ResolveBodyEditorImeLineY") -and
        $notesWindowText.Contains("UserNotesUiState.ResolveBodyTextViewport(bodyRect)") -and
        $notesWindowText.Contains("UserNotesUiState.BodyLineHeightForLayout") -and
        $notesWindowText.Contains("UserNotesUiState.BodyTextScaleForLayout") -and
        $uiTestsText.Contains("LegacyUiMetrics.TabTextScale") -and
        $uiTestsText.Contains("UserNotesCardBodyViewportMatchesLayoutAndScroll")) {
        Write-Pass "User notes F5 card and shared menu text keep enlarged scale, line-height, scroll, hit-test, and IME geometry."
    }
    else {
        Write-FailHealth "User notes F5 card body and shared menu metrics must keep enlarged scale with shared draw/scroll/click/IME viewport geometry."
    }

    if ($notesSharedText.Contains("IsTextEditorFocusAllowedForClickResolution") -and
        $notesSharedText.Contains("LegacyMultilineTextInput.IsAnyFocused") -and
        $userNotesActionText.Contains('SaveActiveEditor("pin")') -and
        $userNotesActionText.Contains('SaveActiveEditor("add")') -and
        $userNotesActionText.Contains('SaveActiveEditor("delete")') -and
        $uiTestsText.Contains("UserNotesMultilineFocusAllowsButtonClickResolution") -and
        $uiTestsText.Contains("UserNotesEditingCommandsSaveThenContinueOrStopOnFailure") -and
        $uiTestsText.Contains("UserNotesTitleEditorSavesAndCancels")) {
        Write-Pass "User notes active multiline editor clicks keep save-then-continue behavior for card commands."
    }
    else {
        Write-FailHealth "User notes active multiline editor must allow card command hit-test and keep save-success-then-continue / save-failure-stop coverage."
    }

    if ($multilineInputText.Contains("TerrariaTextInputCompat.BeginTextInput();") -and
        $multilineInputText.Contains("UpdateInputCaptureGuard") -and
        $textInputCompatText.Contains("CurrentInputTextTakerOverride") -and
        $textInputCompatText.Contains("TrySetCurrentInputTextTakerOverride") -and
        $uiTestsText.Contains("UserNotesMultilineTextInputArmsAndReleasesNativeCapture")) {
        Write-Pass "User notes multiline editor keeps native text input capture/re-arm/release protection."
    }
    else {
        Write-FailHealth "User notes multiline editor must keep native text input capture, CurrentInputTextTakerOverride sentinel handling, and capture/release tests."
    }

    $mutationLeaks = @()
    $notesBehaviorTexts = @(
        @{ Name = "LegacyUiActionService.UserNotes"; Text = $userNotesActionText },
        @{ Name = "UserNotesPinnedOverlay"; Text = $pinnedOverlayText },
        @{ Name = "UserNotesPinnedOverlayState"; Text = $pinnedOverlayStateText },
        @{ Name = "UserNotesUiState"; Text = $notesStateText },
        @{ Name = "UserNotesStore"; Text = $storeText }
    )
    foreach ($behavior in $notesBehaviorTexts) {
        foreach ($token in @("InputActionQueue", "TryEnqueue", "controlUseItem", "selectedItem", "statLife", "statMana", "buffType", "buffTime", "AddBuff", "QuickStack")) {
            if ($behavior.Text.Contains($token)) {
                $mutationLeaks += "$($behavior.Name):$token"
            }
        }
    }

    if ($mutationLeaks.Count -eq 0 -and
        $pinnedOverlayText.Contains("UiMouseCaptureService.CaptureForOperationWindow") -and
        $pinnedOverlayText.Contains("UiMouseCaptureService.ConsumeScrollForOperationWindow") -and
        $pinnedOverlayText.Contains("TerrariaUiMouseCompat.TryConsumeMouseTriggerInputOnceForUi") -and
        $hookInstallerText.Contains("UserNotesPinnedOverlay.UpdatePrefixGuard") -and
        $playerInputHookText.Contains("UserNotesPinnedOverlay.UpdateAfterPlayerInputGuard") -and
        $playerInputHookText.Contains('UiInputFrameClock.BeginInputFrame(phase)') -and
        $playerInputHookText.Contains('"PlayerInputScrollHook.Postfix."') -and
        $pinnedOverlayText.Contains('UpdateInputGuard("UserNotesPinnedOverlay.UpdateAfterPlayerInputGuard", true, false, null)') -and
        $pinnedOverlayText.Contains("PendingToolbarPress") -and
        $pinnedOverlayText.Contains("TryApplyPendingToolbarPress") -and
        $pinnedOverlayText.Contains("TryResolveOsClientOverlayPoint") -and
        $pinnedOverlayText.Contains("ScaleAlphaForTesting") -and
        $pinnedOverlayText.Contains("ForegroundAlphaForTesting") -and
        $pinnedOverlayText.Contains("PremultiplyForAlphaBlendForTesting") -and
        $pinnedOverlayText.Contains("DrawOpacitySurfaceRoundedRect") -and
        $pinnedOverlayText.Contains("capturePendingToolbarPress") -and
        $pinnedOverlayText.Contains("IsPendingDragPress") -and
        $pinnedOverlayText.Contains("ConsumeMouseButtonsForUi(!preserveLeftHold") -and
        $pinnedOverlayText.Contains("ConsumeMouseButtonsForUi(bool includeLeftButton") -and
        $pinnedOverlayText.Contains("!hit.MouseInside ||") -and
        $pinnedOverlayText.Contains("interaction.ScrollConsumed ? 0 : rawScrollDelta") -and
        $modelsText.Contains("OpacityPercent = 0;") -and
        $pinnedOverlayStateText.Contains("OpacityPercent = 0") -and
        $pinnedOverlayStateText.Contains("nextOpacity == hit.OpacityPercent") -and
        $pinnedOverlayStateText.Contains("return ShouldCaptureMouse(frame, mouseX, mouseY);") -and
        $pinnedOverlayStateText.Contains('var interaction = new UserNotesPinnedOverlayInteraction()') -and
        $pinnedOverlayStateText.Contains('_lastInteraction = new UserNotesPinnedOverlayInteraction()') -and
        -not $pinnedOverlayStateText.Contains('UserNotesPinnedOverlayInteraction.None') -and
        $hotbarHookText.Contains("UserNotesPinnedOverlay.ShouldSuppressHotbarScrollFromHook") -and
        $overlayTestsText.Contains("UserNotesPinnedOverlayScrollDragOpacityAndCloseUsePinnedState") -and
        $overlayTestsText.Contains("UserNotesPinnedOverlayProcessesClickAfterPlayerInput") -and
        $overlayTestsText.Contains("UserNotesPinnedOverlayTransfersPrefixPressToPlayerInputToolbarHit") -and
        $overlayTestsText.Contains("UserNotesPinnedOverlayTransfersPrefixPressWhenTerrariaCoordinatesMissNote") -and
        $overlayTestsText.Contains("UserNotesPinnedOverlayTransfersPrefixPressToPlayerInputDragAndKeepsHeldLeft") -and
        $overlayTestsText.Contains("UserNotesPinnedOverlayOpacityDefaultsAndClampsWithoutWrap") -and
        $overlayTestsText.Contains("UserNotesPinnedOverlayDragCaptureBlocksNonLeftMouseAndKeepsHeldLeft") -and
        $overlayTestsText.Contains("UserNotesPinnedOverlayPostPlayerInputWheelScrollsBody") -and
        $overlayTestsText.Contains("UserNotesPinnedOverlayVisualSurfaceWheelBlocksHotbarWithoutFakeWheel") -and
        $overlayTestsText.Contains("UserNotesPinnedOverlayRepeatedToolbarClicksKeepEdgesAndWheel") -and
        $overlayTestsText.Contains("default to 0 percent stored opacity") -and
        $overlayTestsText.Contains("ScaleAlphaForTesting(168, 0)") -and
        $overlayTestsText.Contains("UserNotesPinnedOverlayRightEdgeUsesScreenMouseAndClamps") -and
        $overlayTestsText.Contains("UserNotesPinnedOverlayScreenCoordinatesMatchFrozenRightSideSample") -and
        $overlayTestsText.Contains("UserNotesPinnedOverlayInitialPlacementUsesScreenExtentUnderUiScale") -and
        $programTestsText.Contains("user notes pinned overlay processes click after player input") -and
        $programTestsText.Contains("user notes pinned overlay transfers prefix press to player input toolbar hit") -and
        $programTestsText.Contains("user notes pinned overlay transfers prefix press when Terraria coordinates miss note") -and
        $programTestsText.Contains("user notes pinned overlay transfers prefix press to player input drag and keeps held left") -and
        $programTestsText.Contains("user notes pinned overlay opacity defaults and clamps without wrap") -and
        $programTestsText.Contains("user notes pinned overlay drag capture blocks non-left mouse and keeps held left") -and
        $programTestsText.Contains("user notes pinned overlay post player input wheel scrolls body") -and
        $programTestsText.Contains("user notes pinned overlay visual surface wheel blocks hotbar without fake wheel") -and
        $programTestsText.Contains("user notes pinned overlay repeated toolbar clicks keep edges and wheel") -and
        $programTestsText.Contains("user notes pinned overlay right edge uses screen mouse and clamps") -and
        $programTestsText.Contains("user notes pinned overlay screen coordinates match frozen right side sample") -and
        $programTestsText.Contains("user notes pinned overlay initial placement uses screen extent under UI scale") -and
        $interfaceLayerTestsText.Contains("UserNotesPinnedOverlay.DrawInterfaceLayer") -and
        $interfaceLayerTestsText.Contains("GetUserNotesPinnedOverlayScaleTypeNameForTesting")) {
        Write-Pass "User notes pinned overlay stays UI-only and uses controlled prefix/post-PlayerInput mouse/scroll consumption guards, one-shot toolbar press transfer including stale Terraria coordinate misses, right-edge screen mouse coverage, drag held-left preservation with non-left mouse blocking, default transparent premultiplied non-wrapping background opacity, repeated toolbar edge coverage, visual-surface wheel isolation, and post-PlayerInput body wheel coverage."
    }
    else {
        Write-FailHealth "User notes pinned overlay must not submit actions or mutate game state, must avoid mutable static interaction state, and must use prefix/post-PlayerInput/hotbar scroll guards with postfix-click, toolbar press-transfer including stale Terraria coordinate misses, right-edge screen mouse coverage, drag held-left preservation plus non-left mouse blocking, default transparent premultiplied foreground-separated opacity clamp, visual-surface wheel isolation, repeated toolbar edge, and post-PlayerInput wheel tests; leaks=$($mutationLeaks -join ', ')"
    }

    if ($pinnedOverlayStateText.Contains("ToolbarRect") -and
        $pinnedOverlayStateText.Contains("ResolveToolbarRect") -and
        $pinnedOverlayStateText.Contains("ResolveBodyRect") -and
        $pinnedOverlayStateText.Contains("rect.Y + BodyPadding") -and
        $pinnedOverlayStateText.Contains("internal const int LineHeight = 36;") -and
        $pinnedOverlayStateText.Contains("internal const int DefaultVisibleBodyLines = 8;") -and
        $pinnedOverlayStateText.Contains("internal const int DefaultHeight = BodyPadding * 2 + LineHeight * DefaultVisibleBodyLines;") -and
        $pinnedOverlayStateText.Contains("internal const int MaxHeight = 360;") -and
        $pinnedOverlayStateText.Contains("internal const float BodyTextScale = 1.20f;") -and
        $pinnedOverlayStateText.Contains("DragHandleMinWidth = 84") -and
        $pinnedOverlayStateText.Contains("BuildBodyLines(note.Body, bodyRect.Width)") -and
        $pinnedOverlayText.Contains("item.ToolbarRect") -and
        $pinnedOverlayText.Contains("UserNotesPinnedOverlayState.BodyTextScale") -and
        $notesStateText.Contains("PinnedOverlayBodyWrapTextScale = UserNotesPinnedOverlayState.BodyTextScale") -and
        $overlayTestsText.Contains("UserNotesPinnedOverlayBodyStartsAtContentTopWhenToolbarHidden") -and
        $overlayTestsText.Contains("UserNotesPinnedOverlayBodyWrapMatchesDrawScaleWithoutEllipsis") -and
        $overlayTestsText.Contains("UserNotesPinnedOverlayToolbarHandleIsCenteredAndSeparatedFromButtons") -and
        $overlayTestsText.Contains("Expected default pinned note height to fit at least eight body text lines.") -and
        $programTestsText.Contains("user notes pinned overlay toolbar handle is centered and separated from buttons")) {
        Write-Pass "User notes pinned overlay keeps readable enlarged body text, eight-line default height, toolbar/content separation, centered long drag handle, and wrap/draw scale consistency."
    }
    else {
        Write-FailHealth "User notes pinned overlay must keep BodyRect/ToolbarRect separation, no invisible header reservation, 1.20 wrap/draw scale, 36 line height, eight-line default height, centered long drag handle, and no-ellipsis tests."
    }

    if ($mouseInputText.Contains("ReadMouseForInterfaceOverlay") -and
        $mouseInputText.Contains("applyMainDrawScale") -and
        $mouseInputText.Contains("ResolveInterfaceOverlayMouse") -and
        $mouseInputText.Contains("OsClientScreen") -and
        $mouseInputText.Contains("AppendInterfaceOverlayMode") -and
        $pinnedOverlayText.Contains("LegacyUiInput.ReadMouseForInterfaceOverlay") -and
        $pinnedOverlayText.Contains("ResolveCoordinateContext") -and
        $pinnedOverlayCoordinatesText.Contains("ScreenUnscaled") -and
        $pinnedOverlayCoordinatesText.Contains("ResolveScreenContext") -and
        $notesStateText.Contains("UserNotesPinnedOverlayCoordinates.ResolveCurrentScreenContext") -and
        $pinnedOverlayStateText.Contains("CoordinateMode") -and
        $interfaceLayerCallbacksText.Contains("UserNotesPinnedOverlayDispatcherLayerName") -and
        $interfaceLayerCallbacksText.Contains('ParseScaleValue(_scaleType, "None")') -and
        $interfaceLayerTestsText.Contains("GetUserNotesPinnedOverlayDispatcherRouteNamesForTesting") -and
        $overlayTestsText.Contains("UserNotesPinnedOverlayRightEdgeUsesScreenMouseAndClamps") -and
        $overlayTestsText.Contains("UserNotesPinnedOverlayScreenCoordinatesMatchFrozenRightSideSample") -and
        $overlayTestsText.Contains("UserNotesPinnedOverlayInitialPlacementUsesScreenExtentUnderUiScale") -and
        $overlayTestsText.Contains("UserNotesPinnedOverlayScaledMouseHitsVisualControls")) {
        Write-Pass "User notes pinned overlay keeps screen-coordinate draw, hit-test, initial placement, right-edge clamp, and high unscaled layer coverage under UI scale."
    }
    else {
        Write-FailHealth "User notes pinned overlay must use screen-space overlay mouse coordinates, prefer OS client coordinates for scaled overlays, clamp by the unscaled screen extent, use a dedicated None-scale high layer, and keep scaled close/opacity/drag/right-edge/frozen-sample hit-test coverage."
    }

    if ($diagnosticsText.Contains("DiagnosticActionRecorder.RecordCustomEvent") -and
        $diagnosticsText.Contains('\"featureId\":\"information.user_notes\"') -and
        $userNotesActionText.Contains("Ui.Notes.DeleteConfirm") -and
        $pinnedOverlayText.Contains("Ui.Notes.Wheel") -and
        $pinnedOverlayText.Contains("Ui.Notes.Drag") -and
        $pinnedOverlayText.Contains("Ui.Notes.Opacity") -and
        $diagnosticRulesText.Contains("scenario=Ui.Notes.*") -and
        $diagnosticRulesText.Contains("不新增 runtime snapshot 字段") -and
        $featureDocText.Contains("## 诊断字段") -and
        $featureDocText.Contains("Ui.Notes.Opacity") -and
        $featureDocText.Contains('同一次命令链路继续执行的 `Ui.Notes.Pin`') -and
        $featureDocText.Contains('05-诊断测试文档审计护栏') -and
        $diagnosticRulesText.Contains('同一时间窗口先看 `Ui.Notes.Save` 是否成功') -and
        $diagnosticRulesText.Contains('`Ui.Notes.Wheel` 只表示正文滚轮实际改变 offset') -and
        $diagnosticRulesText.Contains('overlayScreenWidth/Height') -and
        $diagnosticRulesText.Contains('buttonConsumeMessage') -and
        $featureDocText.Contains("仍需用户实机确认") -and
        $legacyPlan06Text.Contains("## 覆盖矩阵") -and
        $feedbackPlan06Text.Contains("## 覆盖矩阵") -and
        $systemPlan05Text.Contains("## 覆盖矩阵") -and
        $systemPlan05Text.Contains("ScreenUnscaled") -and
        $systemPlan05Text.Contains("视觉区域滚轮") -and
        $systemPlan05Text.Contains("held-left") -and
        $systemPlan05Text.Contains("居中长拖动 handle")) {
        Write-Pass "User notes docs, diagnostics, and archived coverage matrix describe real Ui.Notes action events and pinned-overlay guardrails without inventing runtime snapshot fields."
    }
    else {
        Write-FailHealth "User notes feature docs, diagnostics rules, and plan coverage matrix must describe actual Ui.Notes events, no runtime snapshot fields, ScreenUnscaled coordinates, visual-surface wheel isolation, held-left drag capture, centered long handle, and remaining real-machine checks."
    }

    if ($programTestsText.Contains("user notes store missing index uses config notes directory") -and
        $programTestsText.Contains("user notes body editor saves newlines and keeps draft on failure") -and
        $programTestsText.Contains("user notes body editor auto scrolls caret into viewport") -and
        $programTestsText.Contains("user notes pinned overlay processes click after player input") -and
        $programTestsText.Contains("user notes pinned overlay transfers prefix press to player input toolbar hit") -and
        $programTestsText.Contains("user notes pinned overlay transfers prefix press when Terraria coordinates miss note") -and
        $programTestsText.Contains("user notes pinned overlay transfers prefix press to player input drag and keeps held left") -and
        $programTestsText.Contains("user notes pinned overlay opacity defaults and clamps without wrap") -and
        $programTestsText.Contains("user notes pinned overlay drag capture blocks non-left mouse and keeps held left") -and
        $programTestsText.Contains("user notes pinned overlay post player input wheel scrolls body") -and
        $programTestsText.Contains("user notes pinned overlay visual surface wheel blocks hotbar without fake wheel") -and
        $programTestsText.Contains("user notes pinned overlay repeated toolbar clicks keep edges and wheel") -and
        $programTestsText.Contains("user notes pinned overlay right edge uses screen mouse and clamps") -and
        $programTestsText.Contains("user notes pinned overlay screen coordinates match frozen right side sample") -and
        $programTestsText.Contains("user notes pinned overlay initial placement uses screen extent under UI scale") -and
        $programTestsText.Contains("user notes pinned overlay toolbar handle is centered and separated from buttons") -and
        $programTestsText.Contains("user notes pinned overlay scroll drag opacity and close use pinned state") -and
        $storeTestsText.Contains("UserNotesSaveIndexFailureRollsBackBody") -and
        $uiTestsText.Contains("UserNotesNestedScrollConsumesOnlyScrollableBody") -and
        $overlayTestsText.Contains("UserNotesPinnedOverlayStoreReloadAndDeleteSync")) {
        Write-Pass "User notes console tests keep storage, UI, editor, nested scroll, and pinned overlay coverage anchored."
    }
    else {
        Write-FailHealth "User notes console tests must cover store isolation/safe write, UI layout/nested scroll, multiline editor, and pinned overlay behavior."
    }
}

function Test-MapCustomMarkerGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $registrarPath = Join-Path $RepoRoot "src\JueMingZ\Features\Catalog\MapEnhancementFeatureRegistrar.cs"
    $rootPath = Join-Path $RepoRoot "src\JueMingZ\Records\PlayerWorldFeatureDataRoot.cs"
    $modelsPath = Join-Path $RepoRoot "src\JueMingZ\Records\PlayerWorldMapMarkerModels.cs"
    $stylesPath = Join-Path $RepoRoot "src\JueMingZ\Records\PlayerWorldMapMarkerStyles.cs"
    $storePath = Join-Path $RepoRoot "src\JueMingZ\Records\PlayerWorldMapMarkerStore.cs"
    $cachePath = Join-Path $RepoRoot "src\JueMingZ\Records\PlayerWorldMapMarkerCache.cs"
    $diagnosticsPath = Join-Path $RepoRoot "src\JueMingZ\Diagnostics\PlayerWorldMapMarkerDiagnostics.cs"
    $traceRecorderPath = Join-Path $RepoRoot "src\JueMingZ\Diagnostics\PlayerWorldMapMarkerTraceRecorder.cs"
    $snapshotPath = Join-Path $RepoRoot "src\JueMingZ\Diagnostics\DiagnosticSnapshot.cs"
    $snapshotWriterPath = Join-Path $RepoRoot "src\JueMingZ\Diagnostics\DiagnosticSnapshotWriter.Json.cs"
    $snapshotBuilderPath = Join-Path $RepoRoot "src\JueMingZ\Runtime\Diagnostics\RuntimeDiagnosticSnapshotBuilder.Bootstrap.cs"
    $diagnosticUiSnapshotBuilderPath = Join-Path $RepoRoot "src\JueMingZ\Runtime\Diagnostics\RuntimeDiagnosticSnapshotBuilder.DiagnosticUi.cs"
    $interactionPath = Join-Path $RepoRoot "src\JueMingZ\Automation\MapEnhancement\MapCustomMarkerInteractionService.cs"
    $mapCompatPath = Join-Path $RepoRoot "src\JueMingZ\Compat\MapCustomMarkerMapCompat.cs"
    $debugHotkeyPath = Join-Path $RepoRoot "src\JueMingZ\Input\DebugHotkeyService.cs"
    $handlerPath = Join-Path $RepoRoot "src\JueMingZ\Input\LegacyUiActionService.MapEnhancementHandlers.cs"
    $interfaceLayerPath = Join-Path $RepoRoot "src\JueMingZ\Hooks\InterfaceLayerHookCallbacks.cs"
    $fullscreenPickerDrawInstallerPath = Join-Path $RepoRoot "src\JueMingZ\Hooks\MapCustomMarkerFullscreenMapDrawInstaller.cs"
    $stylePickerOverlayPath = Join-Path $RepoRoot "src\JueMingZ\UI\MapCustomMarkerStylePickerOverlay.cs"
    $legacyWindowStatePath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyMainUiState.Window.cs"
    $uiMouseCaptureServicePath = Join-Path $RepoRoot "src\JueMingZ\UI\UiMouseCaptureService.cs"
    $uiMouseCompatPath = Join-Path $RepoRoot "src\JueMingZ\Compat\TerrariaUiMouseCompat.cs"
    $mapEnhancementUiPath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyMainWindow.MapEnhancement.cs"
    $markerUiPath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyMainWindow.MapEnhancement.Markers.cs"
    $mapLayerPath = Join-Path $RepoRoot "src\JueMingZ\Hooks\PlayerWorldMapMarkerMapLayer.cs"
    $fullscreenCompatPath = Join-Path $RepoRoot "src\JueMingZ\Compat\MapFullscreenCompat.cs"
    $testPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.PlayerWorldMapMarkerTests.cs"
    $uiInputTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.UiInputFrameTests.cs"
    $interfaceTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.InterfaceLayerHookTests.cs"
    $mapMarkerFeatureDocPath = Join-Path $RepoRoot "文档\功能介绍\地图加强页\地图标记.md"
    $diagnosticRulesPath = Join-Path $RepoRoot "文档\项目规则\AI诊断日志说明.md"

    $registrarText = Read-TextIfExists -Path $registrarPath
    $rootText = Read-TextIfExists -Path $rootPath
    $modelsText = Read-TextIfExists -Path $modelsPath
    $stylesText = Read-TextIfExists -Path $stylesPath
    $storeText = Read-TextIfExists -Path $storePath
    $cacheText = Read-TextIfExists -Path $cachePath
    $diagnosticsText = Read-TextIfExists -Path $diagnosticsPath
    $traceRecorderText = Read-TextIfExists -Path $traceRecorderPath
    $snapshotText = Read-TextIfExists -Path $snapshotPath
    $snapshotWriterText = Read-TextIfExists -Path $snapshotWriterPath
    $snapshotBuilderText = Read-TextIfExists -Path $snapshotBuilderPath
    $diagnosticUiSnapshotBuilderText = Read-TextIfExists -Path $diagnosticUiSnapshotBuilderPath
    $interactionText = Read-TextIfExists -Path $interactionPath
    $mapCompatText = Read-TextIfExists -Path $mapCompatPath
    $debugHotkeyText = Read-TextIfExists -Path $debugHotkeyPath
    $handlerText = Read-TextIfExists -Path $handlerPath
    $interfaceLayerText = Read-TextIfExists -Path $interfaceLayerPath
    $fullscreenPickerDrawInstallerText = Read-TextIfExists -Path $fullscreenPickerDrawInstallerPath
    $stylePickerOverlayText = Read-TextIfExists -Path $stylePickerOverlayPath
    $legacyWindowStateText = Read-TextIfExists -Path $legacyWindowStatePath
    $uiMouseCaptureServiceText = Read-TextIfExists -Path $uiMouseCaptureServicePath
    $uiMouseCompatText = Read-TextIfExists -Path $uiMouseCompatPath
    $mapEnhancementUiText = Read-TextIfExists -Path $mapEnhancementUiPath
    $markerUiText = Read-TextIfExists -Path $markerUiPath
    $mapLayerText = Read-TextIfExists -Path $mapLayerPath
    $fullscreenCompatText = Read-TextIfExists -Path $fullscreenCompatPath
    $testText = Read-TextIfExists -Path $testPath
    $uiInputTestText = Read-TextIfExists -Path $uiInputTestPath
    $interfaceTestText = Read-TextIfExists -Path $interfaceTestPath
    $mapMarkerFeatureDocText = Read-TextIfExists -Path $mapMarkerFeatureDocPath
    $diagnosticRulesText = Read-TextIfExists -Path $diagnosticRulesPath

    if ($null -eq $registrarText -or $null -eq $rootText -or $null -eq $modelsText -or $null -eq $stylesText -or $null -eq $storeText -or $null -eq $cacheText -or $null -eq $diagnosticsText -or $null -eq $traceRecorderText -or $null -eq $snapshotText -or $null -eq $snapshotWriterText -or $null -eq $snapshotBuilderText -or $null -eq $diagnosticUiSnapshotBuilderText -or $null -eq $interactionText -or $null -eq $mapCompatText -or $null -eq $debugHotkeyText -or $null -eq $handlerText -or $null -eq $interfaceLayerText -or $null -eq $fullscreenPickerDrawInstallerText -or $null -eq $stylePickerOverlayText -or $null -eq $legacyWindowStateText -or $null -eq $uiMouseCaptureServiceText -or $null -eq $uiMouseCompatText -or $null -eq $mapEnhancementUiText -or $null -eq $markerUiText -or $null -eq $mapLayerText -or $null -eq $fullscreenCompatText -or $null -eq $testText -or $null -eq $uiInputTestText -or $null -eq $interfaceTestText -or $null -eq $mapMarkerFeatureDocText -or $null -eq $diagnosticRulesText) {
        Write-FailHealth "Map custom marker registrar, models/styles, store/cache, diagnostics, interaction, coordinate compat, UI layer, handler, map layer, fullscreen compat, docs, and tests must exist as separate responsibilities."
        return
    }

    if ($registrarText.Contains("FeatureIds.MapCustomMarkers") -and
        $registrarText.Contains(".Domain(FeatureCodeDomain.MapEnhancement)") -and
        $registrarText.Contains(".Category(FeatureUserCategory.MapEnhancement)") -and
        $registrarText.Contains(".Actions(InputActionKind.None)") -and
        $registrarText.Contains(".Implemented(true)") -and
        $registrarText.Contains("LocalAssistPendingMultiplayerVerification")) {
        Write-Pass "Map custom markers stay registered as an implemented map-enhancement feature with no ActionQueue requirement."
    }
    else {
        Write-FailHealth "map.custom_markers must stay in MapEnhancementFeatureRegistrar with MapEnhancement domain/category, None actions, and pending multiplayer verification."
    }

    if ($rootText.Contains('MapMarkersFileName = "map-markers.json"') -and
        $storeText.Contains("BuildPlayerWorldFeatureFilePath(pairId, PlayerWorldFeatureDataRoot.MapMarkersFileName)") -and
        $storeText.Contains("PlayerWorldFeatureDataStore.TryWriteJson") -and
        $cacheText.Contains("PlayerWorldMapMarkerStore.ReadForPair")) {
        Write-Pass "Map marker JSON remains isolated under player-world pair paths and written by the marker store."
    }
    else {
        Write-FailHealth "map-markers.json must stay behind PlayerWorldFeatureDataRoot.MapMarkersFileName and PlayerWorldMapMarkerStore safe writes."
    }

    if ($modelsText.Contains("LegacyFallenStarIconItemId = 75") -and
        $modelsText.Contains("ReplacementBedIconItemId = 224") -and
        $modelsText.Contains("return ReplacementBedIconItemId") -and
        $stylesText.Contains("ReplacementBedIconItemId") -and
        -not $stylesText.Contains('"坠星"') -and
        $testText.Contains("PlayerWorldMapMarkersLegacyFallenStarIconMapsToBed")) {
        Write-Pass "Map marker style whitelist replaces fallen star with bed and maps legacy 75 markers forward."
    }
    else {
        Write-FailHealth "Map marker icon whitelist must replace fallen star with bed, map legacy item 75 to item 224, and keep a regression test."
    }

    if ($modelsText.Contains("MaxMarkersPerPair = 120") -and
        $modelsText.Contains("MaxCachedMarkers = MaxMarkersPerPair") -and
        $testText.Contains("must stay at 120")) {
        Write-Pass "Map marker per-pair and cache limits stay capped at 120."
    }
    else {
        Write-FailHealth "Map marker per-pair and cache limits must stay capped at 120 with regression coverage."
    }

    if ($modelsText.Contains('ToString("yyMMddHHmm"') -and
        $interactionText.Contains("DateTime.Now") -and
        $interactionText.Contains("FormatDefaultName(localNow)") -and
        $interactionText.Contains("BuildMarkerRecordForTesting") -and
        $testText.Contains("2606160905") -and
        $testText.Contains("PlayerWorldMapMarkersCreatedMarkerDefaultsToTimestampName") -and
        -not $interactionText.Contains("Name = string.Empty")) {
        Write-Pass "Map marker creation defaults the marker name to a local yyMMddHHmm timestamp."
    }
    else {
        Write-FailHealth "Map marker creation must default new marker names to local yyMMddHHmm and keep regression coverage."
    }

    if ($markerUiText.Contains("CalculateMapMarkerListBodyHeightForTesting") -and
        $markerUiText.Contains("GetMapMarkerListHorizontalInsetForTesting") -and
        $markerUiText.Contains("MapMarkerListPageSize = 10") -and
        $markerUiText.Contains("CalculateMapMarkerVisibleCountForPage") -and
        $mapEnhancementUiText.Contains('"map-custom-markers-page:"') -and
        $mapEnhancementUiText.Contains("地图标记（已到标记上限）") -and
        $mapEnhancementUiText.Contains("CalculateMapMarkerListContentYForTesting") -and
        $mapEnhancementUiText.Contains("MapCustomMarkersRowIndex = 8") -and
        $mapEnhancementUiText.Contains("CalculateMapEnhancementRowContentY(MapCustomMarkersRowIndex) + LegacyUiMetrics.RowHeight") -and
        -not $mapEnhancementUiText.Contains("var markerListY = LegacyUiMetrics.RowHeight * 5 + LegacyUiMetrics.SettingRowGap * 5") -and
        $markerUiText.Contains("empty-silent") -and
        $markerUiText.Contains("attached-link-card+same-width+paged-10+empty-silent+focused-confirm") -and
        -not $markerUiText.Contains("暂无地图标记") -and
        $markerUiText.Contains("ShouldShowMapMarkerConfirmButton") -and
        $markerUiText.Contains("LegacyTextInput.IsFocused(BuildMapMarkerNameInputId") -and
        -not $markerUiText.Contains("DrawSubPanelClipped") -and
        $handlerText.Contains("HandleMapCustomMarkersPage") -and
        $handlerText.Contains('\"mapCustomMarkerPageIndex\"') -and
        $handlerText.Contains('string.Equals(action, "confirm-name"') -and
        $handlerText.Contains("nameInputNotFocused") -and
        $testText.Contains("paged-10") -and
        $testText.Contains("empty-silent") -and
        $testText.Contains("paginate by 10 rows") -and
        $testText.Contains("已到标记上限") -and
        $testText.Contains("MapCustomMarkerConfirmNameCommandSavesAndClearsFocus") -and
        $testText.Contains("ShouldShowMapMarkerConfirmButtonForTesting") -and
        $testText.Contains("GetMapMarkerVisibleActionIdsForTesting") -and
        $testText.Contains("BuildMapMarkerConfirmCommandIdForTesting") -and
        $testText.Contains("attach to the main row") -and
        $testText.Contains("final map marker row") -and
        $testText.Contains("omit the duplicate subtitle row") -and
        $testText.Contains("silent zero-height empty state") -and
        -not $testText.Contains("section+subpanel-card+empty-text-only+confirm-button")) {
        Write-Pass "Map marker F5 list keeps attached same-width paged no-subtitle name-save coverage and a silent empty state without locking the old subpanel or always-visible confirm visual contract."
    }
    else {
        Write-FailHealth "Map marker F5 list must keep attached same-width 10-row pagination, limit label, confirm-name coverage, and no old subpanel/card or always-visible confirm visual contract."
    }

    $srcRoot = Join-Path $RepoRoot "src\JueMingZ"
    $writeJsonLeaks = @()
    $mapMarkerBusinessLeaks = @()
    $forbiddenUiOnlyLeaks = @()
    foreach ($file in Get-ChildItem -LiteralPath $srcRoot -Recurse -Filter "*.cs" -File) {
        $relative = $file.FullName.Substring($RepoRoot.Length).TrimStart('\', '/').Replace('\', '/')
        $text = Read-TextIfExists -Path $file.FullName
        if ($null -eq $text) {
            continue
        }

        if ($text.Contains("MapMarkersFileName") -and
            $text.Contains("PlayerWorldFeatureDataStore.TryWriteJson") -and
            $relative -ne "src/JueMingZ/Records/PlayerWorldMapMarkerStore.cs") {
            $writeJsonLeaks += $relative
        }

        if (($relative -eq "src/JueMingZ/Runtime/JueMingZRuntime.cs" -or
             $relative -eq "src/JueMingZ/Input/LegacyUiActionService.cs" -or
             $relative -eq "src/JueMingZ/Records/PlayerWorldBehaviorStore.cs") -and
            ($text.Contains("MapCustomMarkers") -or $text.Contains("PlayerWorldMapMarker") -or $text.Contains("map.custom_markers"))) {
            $mapMarkerBusinessLeaks += $relative
        }

        if ($relative -like "src/JueMingZ/Automation/MapEnhancement/*" -or
            $relative -like "src/JueMingZ/Input/LegacyUiActionService.MapEnhancementHandlers.cs" -or
            $relative -like "src/JueMingZ/UI/Legacy/LegacyMainWindow.MapEnhancement*.cs" -or
            $relative -like "src/JueMingZ/Compat/MapFullscreenCompat.cs") {
            if ($text.Contains("Player.Teleport") -or
                [regex]::IsMatch($text, '\b(player|Player)\.position\s*=') -or
                [regex]::IsMatch($text, '\b(player|Player)\.velocity\s*=') -or
                $text.Contains("fallStart") -or
                $text.Contains("noFallDmg") -or
                $text.Contains("AddBuff") -or
                $text.Contains(".stack =") -or
                $text.Contains("InputActionQueue") -or
                $text.Contains("BuildTeleportRodRequest") -or
                $text.Contains("TryEnqueue")) {
                $forbiddenUiOnlyLeaks += $relative
            }
        }
    }

    if ($writeJsonLeaks.Count -gt 0) {
        Write-FailHealth "Map marker JSON writes must not leave PlayerWorldMapMarkerStore: $($writeJsonLeaks -join ', ')"
    }
    else {
        Write-Pass "Map marker JSON writes remain confined to PlayerWorldMapMarkerStore."
    }

    if ($mapMarkerBusinessLeaks.Count -gt 0) {
        Write-FailHealth "Map marker business logic must not backflow into runtime, legacy UI service main file, or behavior store: $($mapMarkerBusinessLeaks -join ', ')"
    }
    else {
        Write-Pass "Map marker business logic stays out of runtime, legacy UI service main file, and behavior store."
    }

    if ($forbiddenUiOnlyLeaks.Count -gt 0) {
        Write-FailHealth "Map marker jump/UI-only handlers must not move players, mutate state, consume buffs/items, or submit ActionQueue movement: $($forbiddenUiOnlyLeaks -join ', ')"
    }
    else {
        Write-Pass "Map marker jump and UI-only placeholders do not contain player movement, potion, buff, inventory, or ActionQueue paths."
    }

    if ($mapLayerText.Contains("PlayerWorldMapMarkerCache.ReadCurrent()") -and
        $mapLayerText.Contains("MaxDrawnMarkers = PlayerWorldMapMarkerConstants.MaxMarkersPerPair") -and
        $mapLayerText.Contains("GetUnclampedDrawRegion") -and
        $mapLayerText.Contains("PlayerWorldMapMarkerTraceRecorder.RecordDrawIfPending") -and
        $traceRecorderText.Contains('"markerDraw"') -and
        $traceRecorderText.Contains("DrawDeltaFromRightClickX") -and
        $testText.Contains("PlayerWorldMapMarkerTraceDrawEventIncludesScreenDelta") -and
        -not $mapLayerText.Contains("TryWriteJson")) {
        Write-Pass "Map marker IMapLayer draws from cache with culling, one-shot draw trace diagnostics, and no JSON writes."
    }
    else {
        Write-FailHealth "PlayerWorldMapMarkerMapLayer must only read marker cache, cull before draw, cap drawn markers, record one-shot draw trace diagnostics, and never write JSON."
    }

    if ($fullscreenCompatText.Contains("Main.mapFullscreen = true") -and
        $fullscreenCompatText.Contains("Main.mapFullscreenPos") -and
        $fullscreenCompatText.Contains("Main.mapFullscreenScale") -and
        -not $fullscreenCompatText.Contains("Player.Teleport") -and
        -not $fullscreenCompatText.Contains("InputActionQueue") -and
        -not $fullscreenCompatText.Contains("TryWriteJson")) {
        Write-Pass "Map fullscreen jump compat remains limited to fullscreen-map UI fields."
    }
    else {
        Write-FailHealth "MapFullscreenCompat must only write fullscreen-map UI state and must not expand into player movement, ActionQueue, or JSON writes."
    }

    $requiredDiagnosticFields = @(
        "PlayerWorldMapMarkersEnabled",
        "PlayerWorldMapMarkersLastStatus",
        "PlayerWorldMapMarkersLastMessage",
        "PlayerWorldMapMarkersLastPairId",
        "PlayerWorldMapMarkersCount",
        "PlayerWorldMapMarkersReadFailed",
        "PlayerWorldMapMarkersWriteFailed",
        "PlayerWorldMapMarkersLimitExceeded",
        "PlayerWorldMapMarkersCulledByCacheLimit",
        "PlayerWorldMapMarkersLastOperation",
        "PlayerWorldMapMarkersLastUiAction",
        "PlayerWorldMapMarkersLastJumpResult",
        "MapMarkerLastJumpRequestedTileX",
        "MapMarkerLastJumpRequestedTileY",
        "MapMarkerLastJumpWrittenMapPosX",
        "MapMarkerLastJumpWrittenMapPosY",
        "MapMarkerLastJumpScale",
        "MapMarkerLastJumpReleasedUiCapture",
        "MapMarkerLastJumpClearedPanState",
        "MapMarkerLastJumpConsumedButtonPulse",
        "MapMarkerLastJumpVanillaMapInputHandoff",
        "MapMarkerLastBlockedReason",
        "MapMarkerLastTransformRoute",
        "MapMarkerLastTransformScreenWidth",
        "MapMarkerLastTransformScreenHeight",
        "MapMarkerLastTransformMapTopLeftX",
        "MapMarkerLastTransformMapTopLeftY",
        "MapMarkerLastTransformScale",
        "MapMarkerLastTransformMapFullscreenPosX",
        "MapMarkerLastTransformMapFullscreenPosY",
        "MapMarkerLastTransformGameUpdateCount",
        "MapMarkerLastTransformUtc",
        "MapMarkerLastRightClickMouseX",
        "MapMarkerLastRightClickMouseY",
        "MapMarkerLastRightClickTileX",
        "MapMarkerLastRightClickTileY",
        "MapMarkerLastRightClickTransformSource",
        "MapMarkerLastRightClickFallbackReason",
        "MapMarkerLastRightClickMapFullscreenPosX",
        "MapMarkerLastRightClickMapFullscreenPosY",
        "MapMarkerLastRightClickMapScale",
        "MapMarkerLastRightClickTransformAgeUpdates",
        "PlayerWorldMapMarkersUiOnlyActionCount",
        "MapMarkerPickerOpen",
        "MapMarkerPickerAnchorScreenX",
        "MapMarkerPickerAnchorScreenY",
        "MapMarkerPickerPanelX",
        "MapMarkerPickerPanelY",
        "MapMarkerPickerPanelClamped",
        "MapMarkerPickerLastDraw",
        "MapMarkerPickerLastFullscreenDraw",
        "MapMarkerPickerDrawRoute",
        "MapMarkerPickerDrawSkippedReason",
        "MapMarkerPickerLastClick",
        "MapMarkerPickerLastCloseReason",
        "PlayerWorldMapMarkersLastReadUtc",
        "PlayerWorldMapMarkersLastWriteUtc"
    )
    $missingDiagnosticFields = @()
    foreach ($field in $requiredDiagnosticFields) {
        if (-not $snapshotText.Contains($field) -or
            -not $snapshotWriterText.Contains($field) -or
            -not $snapshotBuilderText.Contains($field)) {
            $missingDiagnosticFields += $field
        }
    }

    if ($diagnosticsText.Contains("RecordUiAction") -and
        $diagnosticsText.Contains("RecordJumpState") -and
        $diagnosticsText.Contains("RecordFullscreenTransform") -and
        $diagnosticsText.Contains("RecordRightClick") -and
        $testText.Contains("PlayerWorldMapMarkerDiagnosticsRecordsUiActionAndJumpState") -and
        $testText.Contains("PlayerWorldMapMarkerDiagnosticsRecordsCoordinateTransformState") -and
        $missingDiagnosticFields.Count -eq 0) {
        Write-Pass "Map marker runtime snapshot covers core read/write status, picker state, transform coordinates, jump state, and UI action summaries."
    }
    else {
        Write-FailHealth "Map marker core diagnostics must include read/write status, transform coordinates, picker draw/click/close state, last UI action, last jump state, and UI-only action count in runtime snapshot JSON."
    }

    $screenToTileBody = [System.Text.RegularExpressions.Regex]::Match(
        $mapCompatText,
        'internal\s+static\s+MapCustomMarkerMapPoint\s+ScreenToTile\([\s\S]*?internal\s+static\s+Vector2\s+BuildFallbackMapTopLeftForTesting',
        [System.Text.RegularExpressions.RegexOptions]::Singleline).Value
    $fallbackScreenToTileUsesSharedOrigin =
        $screenToTileBody.Contains("BuildFallbackMapTopLeft") -and
        $screenToTileBody.Contains("ScreenToTileFromTransform") -and
        -not [System.Text.RegularExpressions.Regex]::IsMatch($screenToTileBody, 'mouse[XY]\s*[-+]\s*.*screen(?:Width|Height)\s*/\s*2f')
    $screenToTileFromTransformBody = [System.Text.RegularExpressions.Regex]::Match(
        $mapCompatText,
        'private\s+static\s+MapCustomMarkerMapPoint\s+ScreenToTileFromTransform\([\s\S]*?private\s+static\s+long\s+ReadGameUpdateCountOrUnknown',
        [System.Text.RegularExpressions.RegexOptions]::Singleline).Value
    $screenToTileUsesRecordedOverlayOrigin =
        $screenToTileFromTransformBody.Contains("mouseX - mapTopLeftX") -and
        $screenToTileFromTransformBody.Contains("mouseY - mapTopLeftY") -and
        -not $screenToTileFromTransformBody.Contains("FullscreenMapIconOriginTileOffset") -and
        -not [System.Text.RegularExpressions.Regex]::IsMatch($screenToTileFromTransformBody, 'mapTopLeft[XY]\s*-\s*[^;\r\n]*scale')
    if ($mapCompatText.Contains("RecordFullscreenTransform") -and
        $mapCompatText.Contains("TryScreenToTileFromLastTransform") -and
        $mapCompatText.Contains("RecordFullscreenDrawMousePoint") -and
        $mapCompatText.Contains("ResolveFullscreenMouseTile") -and
        $mapCompatText.Contains("TryScreenToTileFromLastDrawMouse") -and
        $mapCompatText.Contains("fullscreenDrawMouse") -and
        $mapCompatText.Contains("ScreenToTileFromTransform") -and
        $mapCompatText.Contains("AreScreenSizesCompatible") -and
        $mapCompatText.Contains("IsTransformViewStateFresh") -and
        $mapCompatText.Contains("viewStateMismatch") -and
        $mapCompatText.Contains("BuildFallbackMapTopLeft") -and
        $screenToTileUsesRecordedOverlayOrigin -and
        $fallbackScreenToTileUsesSharedOrigin -and
        $stylePickerOverlayText.Contains("MapCustomMarkerMapCompat.RecordFullscreenTransform") -and
        $stylePickerOverlayText.Contains("RecordFullscreenTransform(transform)") -and
        $interactionText.Contains("RecordRightClick(point)") -and
        $interactionText.Contains("CreatePlacement(point)") -and
        $testText.Contains("TryScreenToTileFromLastTransformForTesting") -and
        $testText.Contains("stale transform cache") -and
        $testText.Contains("MapCustomMarkerFullscreenDrawMouseSampleWinsOverUpdateMouse") -and
        $testText.Contains("draw-phase mouse tile sample") -and
        $testText.Contains("pending placement must freeze the right-click tile") -and
        $testText.Contains("round-trip with the MapOverlayDrawContext draw path") -and
        $testText.Contains("UI scale-equivalent screen size") -and
        $testText.Contains("same overlay origin")) {
        Write-Pass "Map marker fullscreen coordinates prefer the draw-phase mouse sample, keep cached OnPostFullscreenMapDraw overlay origin fallback, and round-trip with the map-layer draw path."
    }
    else {
        Write-FailHealth "Map marker right-click coordinates must prefer the cached fullscreen draw mouse sample, use the recorded overlay origin without a second 10-tile subtraction, record diagnostics, and keep fallback on the shared transform path."
    }

    if ($mapMarkerFeatureDocText.Contains("viewStateMismatch") -and
        $mapMarkerFeatureDocText.Contains("fullscreenDrawMouse") -and
        $diagnosticRulesText.Contains("fullscreenDrawMouse") -and
        $mapMarkerFeatureDocText.Contains("MapMarkerLastRightClickTransformAgeUpdates") -and
        $mapMarkerFeatureDocText.Contains("UI scale 等价 screen size") -and
        $diagnosticRulesText.Contains("MapMarkerLastRightClickFallbackReason") -and
        $diagnosticRulesText.Contains("screenSizeMismatch") -and
        $diagnosticRulesText.Contains("viewStateMismatch") -and
        $diagnosticRulesText.Contains("MapMarkerLastRightClickTransformAgeUpdates") -and
        $diagnosticRulesText.Contains("MapMarkerLastTransformMapFullscreenPosX/Y") -and
        $mapMarkerFeatureDocText.Contains("map-marker-events-YYYYMMDD.jsonl") -and
        $diagnosticRulesText.Contains("MapMarkerTraceEventsPath") -and
        $diagnosticRulesText.Contains("projection.tileCenterDeltaX/Y") -and
        $mapMarkerFeatureDocText.Contains("markerDraw") -and
        $diagnosticRulesText.Contains("markerDraw") -and
        $mapMarkerFeatureDocText.Contains("draw.deltaFromRightClickX/Y") -and
        $diagnosticRulesText.Contains("draw.deltaFromRightClickX/Y")) {
        Write-Pass "Map marker feature and diagnostics docs explain right-click transform source, fallback reason, view-state mismatch, transform age, coordinate trace events, and draw-position comparison events."
    }
    else {
        Write-FailHealth "Map marker docs must explain transform source, screenSizeMismatch, viewStateMismatch, transform-age diagnostics, coordinate trace events, and markerDraw screen-delta comparison for user-returned snapshots."
    }

    if ($stylePickerOverlayText.Contains('VisualContract = "icon-cells-only"') -and
        $stylePickerOverlayText.Contains("var desiredX = anchorX;") -and
        $stylePickerOverlayText.Contains("var desiredY = anchorY;") -and
        -not $stylePickerOverlayText.Contains("UiPrimitiveRenderer.DrawRoundedRect") -and
        $testText.Contains("must sit directly at the right-click anchor") -and
        $testText.Contains('"icon-cells-only"')) {
        Write-Pass "Map marker style picker stays anchored to the right-click point and keeps the icon-cells-only visual contract."
    }
    else {
        Write-FailHealth "Map marker style picker must sit at the right-click anchor by default, clamp only at screen edges, and avoid a large rounded panel background."
    }

    $mapPickerInGameDispatcher = [System.Text.RegularExpressions.Regex]::IsMatch(
        $interfaceLayerText,
        'GameOverlay(?:Fallback)?DispatcherDrawers\s*=\s*\{[^}]*MapCustomMarkerStylePickerOverlay\.DrawInterfaceLayer',
        [System.Text.RegularExpressions.RegexOptions]::Singleline)
    $mapPickerInUiDispatcher = [System.Text.RegularExpressions.Regex]::IsMatch(
        $interfaceLayerText,
        'UiOverlay(?:Fallback)?DispatcherDrawers\s*=\s*\{[^}]*MapCustomMarkerStylePickerOverlay\.DrawInterfaceLayer',
        [System.Text.RegularExpressions.RegexOptions]::Singleline)

    if ($mapPickerInUiDispatcher -and -not $mapPickerInGameDispatcher -and
        $fullscreenPickerDrawInstallerText.Contains("Main.OnPostFullscreenMapDraw") -and
        $fullscreenPickerDrawInstallerText.Contains("MapCustomMarkerStylePickerOverlay.DrawFullscreenMapLayer") -and
        $stylePickerOverlayText.Contains("DrawFullscreenMapLayer") -and
        $stylePickerOverlayText.Contains("spriteBatch.Begin") -and
        $stylePickerOverlayText.Contains("Main.UIScaleMatrix") -and
        $stylePickerOverlayText.Contains("FullscreenMapRoute") -and
        $stylePickerOverlayText.Contains("RecordPickerDraw(route)") -and
        $stylePickerOverlayText.Contains("RecordPickerDrawSkipped") -and
        $interfaceTestText.Contains("MapCustomMarkerStylePickerOverlay.DrawInterfaceLayer")) {
        Write-Pass "Map marker style picker keeps UI-scale dispatcher fallback and has a fullscreen-map draw hook with route diagnostics."
    }
    else {
        Write-FailHealth "Map marker style picker must stay out of GameOverlayDispatcher and draw through both the UI-scale dispatcher fallback and Terraria.Main.OnPostFullscreenMapDraw with picker route diagnostics."
    }

    if ($interactionText.Contains("_ignoreRightCloseUntilReleased") -and
        $interactionText.Contains("ReleaseRightCloseGateIfNeeded") -and
        $interactionText.Contains("rightClickIgnored") -and
        $testText.Contains("MapCustomMarkerRightClickReleaseGateRequiresReleaseBeforeClose")) {
        Write-Pass "Map marker right-click close waits for button release after opening the picker."
    }
    else {
        Write-FailHealth "Map marker picker must ignore right-click close until the opening right button has been released, with a regression test."
    }

    if ($handlerText.Contains("RecordUiAction") -and
        $handlerText.Contains("uiOnlyNotImplemented") -and
        $handlerText.Contains('\"requestedTileX\"') -and
        $handlerText.Contains('\"requestedTileY\"') -and
        $handlerText.Contains('\"tileX\"') -and
        $handlerText.Contains('\"tileY\"') -and
        $handlerText.Contains('\"writtenMapPosX\"') -and
        $handlerText.Contains('\"writtenMapPosY\"') -and
        $handlerText.Contains('\"scale\"') -and
        $handlerText.Contains('\"resultCode\"') -and
        $handlerText.Contains('\"writeStatus\"') -and
        $handlerText.Contains('\"mouseCaptured\"') -and
        $handlerText.Contains('\"releasedUiCapture\"') -and
        $handlerText.Contains('\"closedF5\"') -and
        $handlerText.Contains('\"clearedPanState\"') -and
        $handlerText.Contains('\"consumedJumpButtonPulse\"') -and
        $handlerText.Contains('\"vanillaMapInputHandoff\"') -and
        $handlerText.Contains("HideForMapCustomMarkerJumpAndReleaseCapture") -and
        $handlerText.Contains("RecordJumpState") -and
        $legacyWindowStateText.Contains('ConsumeMouseTriggerForOperationWindow("MouseLeft"') -and
        $legacyWindowStateText.Contains("ConsumedJumpButtonPulse") -and
        $uiMouseCaptureServiceText.Contains("ConsumeMouseTriggerForOperationWindow") -and
        $uiMouseCaptureServiceText.Contains("TryConsumeMouseTriggerInput") -and
        $handlerText.Contains("nameInputNotFocused") -and
        $handlerText.Contains("must not scan paths") -and
        $handlerText.Contains("move the player")) {
        Write-Pass "Map marker UI command metadata covers jump diagnostics and UI-only placeholder boundaries."
    }
    else {
        Write-FailHealth "Map marker UI actions must keep featureId/markerId/tile/scale/result/mouse metadata and explicit UI-only placeholder result codes."
    }

    if ($uiMouseCompatText.Contains("_activeTriggerSuppressionObservedGameDown") -and
        $uiMouseCompatText.Contains("ReadMouseTriggerGameDownState") -and
        $uiMouseCompatText.Contains("HasActiveTriggerSuppressionObservedGameDown") -and
        $uiMouseCompatText.Contains("IsMouseButtonDownFallback") -and
        $testText.Contains("MapCustomMarkerJumpReleaseClosesF5AndUiCapture") -and
        $testText.Contains("must stop after release so vanilla fullscreen map input can take over")) {
        Write-Pass "Map marker jump trigger suppression observes Terraria game-state release before falling back to OS button state."
    }
    else {
        Write-FailHealth "Map marker jump trigger suppression must stop after Terraria game-state release and keep the vanilla fullscreen-map handoff regression test."
    }

    $requiredF5HotkeyFields = @(
        "LegacyMainUiLastF5HotkeyDecision",
        "LegacyMainUiLastF5HotkeyReason",
        "LegacyMainUiLastF5HotkeyDown",
        "LegacyMainUiLastF5HotkeyWasDown",
        "LegacyMainUiLastF5HotkeyDebounceRemainingMs",
        "LegacyMainUiLastF5HotkeyUtc"
    )
    $missingF5HotkeyFields = @()
    foreach ($field in $requiredF5HotkeyFields) {
        if (-not $snapshotText.Contains($field) -or
            -not $snapshotWriterText.Contains($field) -or
            -not $diagnosticUiSnapshotBuilderText.Contains($field)) {
            $missingF5HotkeyFields += $field
        }
    }

    if ($missingF5HotkeyFields.Count -eq 0 -and
        $debugHotkeyText.Contains("EvaluateF5HotkeyForTesting") -and
        $debugHotkeyText.Contains("gameInputUnavailable") -and
        $debugHotkeyText.Contains("notForeground") -and
        $debugHotkeyText.Contains("NextWasDown") -and
        $debugHotkeyText.Contains("RecordF5HotkeyDecision") -and
        -not $debugHotkeyText.Contains("ToggleDebounce") -and
        -not $debugHotkeyText.Contains('"debounce"') -and
        $uiInputTestText.Contains("LegacyMainF5HotkeyEdgeTracksPhysicalPressAcrossGates") -and
        $uiInputTestText.Contains("rapidRepress") -and
        $uiInputTestText.Contains("DiagnosticSnapshotWritesLegacyMainF5HotkeyState") -and
        -not $uiInputTestText.Contains("AssertF5Decision(debounce")) {
        Write-Pass "Map marker F5 hotkey diagnostics stay covered without locking a fixed debounce contract."
    }
    else {
        Write-FailHealth "F5 hotkey edge diagnostics must keep snapshot fields and gate-aware tests without requiring a fixed debounce swallow."
    }

    if ($testText.Contains("PlayerWorldMapMarkersDiagnosticsWrittenToSnapshot") -and
        $testText.Contains("PlayerWorldMapMarkerDiagnosticsRecordsUiActionAndJumpState") -and
        $testText.Contains("MapFullscreenJumpTargetClampsPositionAndScale") -and
        $testText.Contains("MapFullscreenJumpTargetFailsWithoutWorldDimensions") -and
        $testText.Contains("MapFullscreenJumpClearsPanState") -and
        $testText.Contains("MapCustomMarkerJumpReleaseClosesF5AndUiCapture") -and
        $testText.Contains("must stop after release so vanilla fullscreen map input can take over") -and
        $testText.Contains("MapCustomMarkerConfirmNameCommandSavesAndClearsFocus") -and
        $testText.Contains("MapCustomMarkerRightClickReleaseGateRequiresReleaseBeforeClose") -and
        $testText.Contains("MapCustomMarkerFullscreenCoordinateClamp") -and
        $testText.Contains("MapCustomMarkerStyleWhitelistAndPickerClamp") -and
        $testText.Contains("MapCustomMarkerFullscreenPickerDrawRouteUsesPostFullscreenMapDraw") -and
        $testText.Contains("Only navigation, teleport and autopilot must stay UI-only") -and
        $uiInputTestText.Contains("LegacyMainF5HotkeyEdgeTracksPhysicalPressAcrossGates")) {
        Write-Pass "Map marker tests cover diagnostics, fullscreen draw route, right-click close gating, jump clamp/fail-soft, F5 edge handling, and UI-only action classification."
    }
    else {
        Write-FailHealth "Map marker tests must cover diagnostics JSON, fullscreen draw route, right-click close gating, jump target clamp/fail-soft, and navigation/teleport/autopilot UI-only classification."
    }
}

function Test-MapDirectionHintGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $registrarPath = Join-Path $RepoRoot "src\JueMingZ\Features\Catalog\MapEnhancementFeatureRegistrar.cs"
    $featureIdsPath = Join-Path $RepoRoot "src\JueMingZ\Common\FeatureIds.cs"
    $appSettingsPath = Join-Path $RepoRoot "src\JueMingZ\Config\AppSettings.cs"
    $configServicePath = Join-Path $RepoRoot "src\JueMingZ\Config\ConfigService.cs"
    $settingsSnapshotPath = Join-Path $RepoRoot "src\JueMingZ\Runtime\RuntimeSettingsSnapshot.cs"
    $settingsSnapshotProviderPath = Join-Path $RepoRoot "src\JueMingZ\Runtime\RuntimeSettingsSnapshotProvider.cs"
    $runtimePath = Join-Path $RepoRoot "src\JueMingZ\Runtime\JueMingZRuntime.cs"
    $targetServicePath = Join-Path $RepoRoot "src\JueMingZ\Automation\MapEnhancement\MapDirectionHintTargetService.cs"
    $targetSnapshotPath = Join-Path $RepoRoot "src\JueMingZ\Automation\MapEnhancement\MapDirectionHintTargetSnapshot.cs"
    $rareResolverPath = Join-Path $RepoRoot "src\JueMingZ\Automation\MapEnhancement\MapRareCreatureDirectionTargetResolver.cs"
    $merchantResolverPath = Join-Path $RepoRoot "src\JueMingZ\Automation\MapEnhancement\MapTravellingMerchantDirectionTargetResolver.cs"
    $townResolverPath = Join-Path $RepoRoot "src\JueMingZ\Automation\MapEnhancement\MapTravellingMerchantTownResolver.cs"
    $projectionPath = Join-Path $RepoRoot "src\JueMingZ\UI\MapDirectionHintProjection.cs"
    $overlayPath = Join-Path $RepoRoot "src\JueMingZ\UI\MapDirectionHintOverlay.cs"
    $diagnosticsPath = Join-Path $RepoRoot "src\JueMingZ\Diagnostics\MapDirectionHintDiagnostics.cs"
    $snapshotPath = Join-Path $RepoRoot "src\JueMingZ\Diagnostics\DiagnosticSnapshot.cs"
    $snapshotWriterPath = Join-Path $RepoRoot "src\JueMingZ\Diagnostics\DiagnosticSnapshotWriter.Json.cs"
    $snapshotBuilderPath = Join-Path $RepoRoot "src\JueMingZ\Runtime\Diagnostics\RuntimeDiagnosticSnapshotBuilder.Bootstrap.cs"
    $handlerPath = Join-Path $RepoRoot "src\JueMingZ\Input\LegacyUiActionService.MapEnhancementHandlers.cs"
    $uiPath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyMainWindow.MapEnhancement.cs"
    $testPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.MapDirectionHintTests.cs"
    $programPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.cs"
    $rareDocPath = Join-Path $RepoRoot "文档\功能介绍\地图加强页\稀有生物显示方向.md"
    $merchantDocPath = Join-Path $RepoRoot "文档\功能介绍\地图加强页\旅商显示方向.md"
    $featureIndexPath = Join-Path $RepoRoot "文档\功能介绍\功能索引.md"
    $diagnosticRulesPath = Join-Path $RepoRoot "文档\项目规则\AI诊断日志说明.md"

    $registrarText = Read-TextIfExists -Path $registrarPath
    $featureIdsText = Read-TextIfExists -Path $featureIdsPath
    $appSettingsText = Read-TextIfExists -Path $appSettingsPath
    $configServiceText = Read-TextIfExists -Path $configServicePath
    $settingsSnapshotText = Read-TextIfExists -Path $settingsSnapshotPath
    $settingsSnapshotProviderText = Read-TextIfExists -Path $settingsSnapshotProviderPath
    $runtimeText = Read-TextIfExists -Path $runtimePath
    $targetServiceText = Read-TextIfExists -Path $targetServicePath
    $targetSnapshotText = Read-TextIfExists -Path $targetSnapshotPath
    $rareResolverText = Read-TextIfExists -Path $rareResolverPath
    $merchantResolverText = Read-TextIfExists -Path $merchantResolverPath
    $townResolverText = Read-TextIfExists -Path $townResolverPath
    $projectionText = Read-TextIfExists -Path $projectionPath
    $overlayText = Read-TextIfExists -Path $overlayPath
    $diagnosticsText = Read-TextIfExists -Path $diagnosticsPath
    $snapshotText = Read-TextIfExists -Path $snapshotPath
    $snapshotWriterText = Read-TextIfExists -Path $snapshotWriterPath
    $snapshotBuilderText = Read-TextIfExists -Path $snapshotBuilderPath
    $handlerText = Read-TextIfExists -Path $handlerPath
    $uiText = Read-TextIfExists -Path $uiPath
    $testText = Read-TextIfExists -Path $testPath
    $programText = Read-TextIfExists -Path $programPath
    $rareDocText = Read-TextIfExists -Path $rareDocPath
    $merchantDocText = Read-TextIfExists -Path $merchantDocPath
    $featureIndexText = Read-TextIfExists -Path $featureIndexPath
    $diagnosticRulesText = Read-TextIfExists -Path $diagnosticRulesPath

    if ($null -eq $registrarText -or $null -eq $featureIdsText -or $null -eq $appSettingsText -or $null -eq $configServiceText -or $null -eq $settingsSnapshotText -or $null -eq $settingsSnapshotProviderText -or $null -eq $runtimeText -or $null -eq $targetServiceText -or $null -eq $targetSnapshotText -or $null -eq $rareResolverText -or $null -eq $merchantResolverText -or $null -eq $townResolverText -or $null -eq $projectionText -or $null -eq $overlayText -or $null -eq $diagnosticsText -or $null -eq $snapshotText -or $null -eq $snapshotWriterText -or $null -eq $snapshotBuilderText -or $null -eq $handlerText -or $null -eq $uiText -or $null -eq $testText -or $null -eq $programText -or $null -eq $rareDocText -or $null -eq $merchantDocText -or $null -eq $featureIndexText -or $null -eq $diagnosticRulesText) {
        Write-FailHealth "Map direction hint feature, runtime, overlay, diagnostics, docs, and tests must exist as separate responsibilities."
        return
    }

    if ($featureIdsText.Contains('MapRareCreatureDirection = "map.rare_creature_direction"') -and
        $featureIdsText.Contains('MapTravellingMerchantDirection = "map.travelling_merchant_direction"') -and
        $registrarText.Contains("FeatureIds.MapRareCreatureDirection") -and
        $registrarText.Contains("FeatureIds.MapTravellingMerchantDirection") -and
        $registrarText.Contains(".Domain(FeatureCodeDomain.MapEnhancement)") -and
        $registrarText.Contains(".Category(FeatureUserCategory.MapEnhancement)") -and
        $registrarText.Contains(".Actions(InputActionKind.None)") -and
        $registrarText.Contains(".Implemented(true)") -and
        $appSettingsText.Contains("MapRareCreatureDirectionEnabled") -and
        $appSettingsText.Contains("MapTravellingMerchantDirectionEnabled") -and
        $configServiceText.Contains("FeatureIds.MapRareCreatureDirection") -and
        $configServiceText.Contains("FeatureIds.MapTravellingMerchantDirection") -and
        $settingsSnapshotText.Contains("MapRareCreatureDirectionEnabled") -and
        $settingsSnapshotText.Contains("MapTravellingMerchantDirectionEnabled") -and
        $settingsSnapshotProviderText.Contains("_mapRareCreatureDirectionEnabled") -and
        $settingsSnapshotProviderText.Contains("_mapTravellingMerchantDirectionEnabled") -and
        $uiText.Contains("稀有生物显示方向") -and
        $uiText.Contains("旅商显示方向") -and
        $handlerText.Contains("displayOnly") -and
        $handlerText.Contains("Ui.Toggle.MapRareCreatureDirection") -and
        $handlerText.Contains("Ui.Toggle.MapTravellingMerchantDirection") -and
        $testText.Contains("MapDirectionHintConfigDefaultsFeatureSyncAndRuntimeSnapshot") -and
        $testText.Contains("LegacyMapDirectionHintHandlersToggleSettings")) {
        Write-Pass "Map direction hints remain map-enhancement display features with config, runtime snapshot sync, F5 rows, and display-only UI metadata."
    }
    else {
        Write-FailHealth "Map direction hint features must stay on the map enhancement page with default-off settings, RuntimeSettingsSnapshot sync, None actions, and display-only UI metadata."
    }

    $gameReadIndex = $runtimeText.IndexOf('"game-state-read"', [System.StringComparison]::Ordinal)
    $directionIndex = $runtimeText.IndexOf('"map-direction-hints-targeting"', [System.StringComparison]::Ordinal)
    $inputGateIndex = $runtimeText.IndexOf('"input-focus-guard"', [System.StringComparison]::Ordinal)
    if ($gameReadIndex -ge 0 -and
        $directionIndex -gt $gameReadIndex -and
        $inputGateIndex -gt $directionIndex -and
        $runtimeText.Contains("RunMapDirectionHintsTargeting") -and
        $runtimeText.Contains("MapDirectionHintTargetService.Tick") -and
        $targetServiceText.Contains("ScanCadenceTicks = 15") -and
        $targetServiceText.Contains("MaxObservedNpcs = 200") -and
        $targetServiceText.Contains("GetRenderSnapshot") -and
        $targetSnapshotText.Contains("MapDirectionHintRenderSnapshot") -and
        $targetSnapshotText.Contains("MapTravellingMerchantDirectionRenderTarget") -and
        $targetSnapshotText.Contains("MapRareCreatureDirectionRenderTarget") -and
        $targetServiceText.Contains("MapDirectionHintDiagnostics.RecordTravellingMerchantTarget") -and
        $targetServiceText.Contains("MapDirectionHintDiagnostics.RecordRareCreatureTarget") -and
        $testText.Contains("MapDirectionHintTargetServiceBuildsSnapshotAndHonorsCadence") -and
        $testText.Contains("MapDirectionHintRenderSnapshotStaysTargetOnly")) {
        Write-Pass "Map direction hint targeting stays a 15-tick read-only stage and publishes a target-only render snapshot for Draw."
    }
    else {
        Write-FailHealth "Map direction hint target scan must stay in a 15-tick runtime stage and publish a target-only render snapshot for Draw."
    }

    if ($rareResolverText.Contains("LifeformAnalyzerInfoIndex = 11") -and
        $rareResolverText.Contains("MaxDistancePixels = 1300f") -and
        $rareResolverText.Contains("HasLifeformAnalyzer") -and
        $rareResolverText.Contains("InfoAccessoryHidden") -and
        $rareResolverText.Contains("npc.Rarity > rare.Rarity") -and
        $rareResolverText.Contains("distanceSquared >= maxDistanceSquared") -and
        $testText.Contains("MapRareCreatureGatesLifeformAnalyzerAndHiddenInfo") -and
        $testText.Contains("MapRareCreatureResolverSelectsHighestRarityWithinRadius") -and
        $testText.Contains("MapRareCreatureProjectionDrawsArrowAndWeakensOnScreen")) {
        Write-Pass "Rare creature direction keeps lifeform analyzer and hideInfo[11] gates, strict 1300px radius, highest-rarity selection, and on-screen arrow-only weakening."
    }
    else {
        Write-FailHealth "Rare creature direction must keep lifeform analyzer/hideInfo gates, strict 1300px target selection, highest rarity preference, and projection tests."
    }

    if ($merchantResolverText.Contains("TravellingMerchantNpcType = NPCID.TravellingMerchant") -and
        $townResolverText.Contains('SourcePylon = "pylon"') -and
        $townResolverText.Contains('SourceTownCluster = "townCluster"') -and
        $townResolverText.Contains('SourcePointBiome = "pointBiome"') -and
        $townResolverText.Contains('SourceUnknown = "unknown"') -and
        $townResolverText.Contains('Confidence = "low"') -and
        $townResolverText.Contains('Label = "环境未知"') -and
        $testText.Contains("MapTravellingMerchantResolverSelectsNpcId368AndHidesOnScreen") -and
        $testText.Contains("MapTravellingMerchantEdgeLabelUsesEllipseAndThreeLines") -and
        $testText.Contains("MapTravellingMerchantTownResolverUsesClusterBiomeAndUnknownSources")) {
        Write-Pass "Travelling merchant direction keeps NPCID 368 targeting, screen-edge labels, source-ranked town labels, low-confidence nearby labels, and unknown fallback."
    }
    else {
        Write-FailHealth "Travelling merchant direction must keep NPCID 368 targeting and pylon/townCluster/pointBiome/unknown label source semantics."
    }

    if ($overlayText.Contains("MapDirectionHintTargetService.GetRenderSnapshot") -and
        -not $overlayText.Contains("MapDirectionHintTargetService.GetSnapshot") -and
        $overlayText.Contains("MapDirectionHintDiagnostics.RecordTravellingMerchantProjection") -and
        $overlayText.Contains("MapDirectionHintDiagnostics.RecordRareCreatureProjection") -and
        $overlayText.Contains("DrawTravellingMerchantLabel") -and
        $overlayText.Contains("DrawRareCreatureArrow") -and
        -not ([System.Text.RegularExpressions.Regex]::IsMatch($overlayText, "Main\.npc|NPC\[|Main\.PylonSystem|Pylons|MapTravellingMerchantTownResolver")) -and
        -not $overlayText.Contains("TryWriteJson") -and
        -not $overlayText.Contains("DiagnosticSnapshotWriter") -and
        -not $overlayText.Contains("PlayerWorldFeatureDataStore")) {
        Write-Pass "Map direction hint overlay draws only from cached target snapshots and does not scan NPCs, pylons, biomes, or write JSON."
    }
    else {
        Write-FailHealth "Map direction hint Draw path must only read cached snapshots; NPC/pylon/biome scans and JSON writes belong outside Draw."
    }

    $directionFiles = @($targetServicePath, $targetSnapshotPath, $rareResolverPath, $merchantResolverPath, $townResolverPath, $projectionPath, $overlayPath, $diagnosticsPath)
    $forbiddenMutationPattern = '(Player\.Teleport|NetMessage|\.statLife\s*=|\.statMana\s*=|\.velocity\s*=|\.position\s*=|\.fallStart\s*=|\.noFallDmg\s*=|AddBuff\s*\(|\.buffType\s*=|\.buffTime\s*=|\.stack\s*=|Main\.tile|NPC\[[^\]]+\]\s*=|WorldGen\.)'
    $mutationLeaks = @()
    $actionBackflow = @()
    foreach ($path in $directionFiles) {
        $text = Read-TextIfExists -Path $path
        if ($null -eq $text) {
            continue
        }

        if ([System.Text.RegularExpressions.Regex]::IsMatch($text, $forbiddenMutationPattern)) {
            $mutationLeaks += $path.Substring($RepoRoot.Length).TrimStart('\', '/').Replace('\', '/')
        }

        if ($text.Contains("InputActionQueue") -or
            $text.Contains("InputActionRequest") -or
            $text.Contains("ActionKind")) {
            $actionBackflow += $path.Substring($RepoRoot.Length).TrimStart('\', '/').Replace('\', '/')
        }
    }

    if ($mutationLeaks.Count -eq 0 -and $actionBackflow.Count -eq 0) {
        Write-Pass "Map direction hint runtime, resolver, overlay, projection, and diagnostics files stay out of ActionQueue and player/NPC/tile/network mutations."
    }
    else {
        if ($mutationLeaks.Count -gt 0) {
            Write-FailHealth "Map direction hints must not mutate player/NPC/tile/network state: $($mutationLeaks -join ', ')"
        }

        if ($actionBackflow.Count -gt 0) {
            Write-FailHealth "Map direction hints must not introduce ActionQueue/request backflow: $($actionBackflow -join ', ')"
        }
    }

    $requiredSnapshotFields = @(
        "MapDirectionHintTargetScanCadenceTicks",
        "MapRareCreatureDirectionEnabled",
        "MapRareCreatureDirectionGateReason",
        "MapRareCreatureDirectionHasLifeformAnalyzer",
        "MapRareCreatureDirectionInfoAccessoryHidden",
        "MapRareCreatureDirectionTargetActive",
        "MapRareCreatureDirectionTargetRarity",
        "MapRareCreatureDirectionOnScreen",
        "MapRareCreatureDirectionShouldDrawLabel",
        "MapRareCreatureDirectionArrowGlyph",
        "MapRareCreatureDirectionLastScanAgeTicks",
        "MapTravellingMerchantDirectionEnabled",
        "MapTravellingMerchantDirectionTargetActive",
        "MapTravellingMerchantDirectionTownLabelSource",
        "MapTravellingMerchantDirectionTownLabelConfidence",
        "MapTravellingMerchantDirectionMatchedPylonDistanceTiles",
        "MapTravellingMerchantDirectionLastScanAgeTicks"
    )
    $missingSnapshotFields = @()
    foreach ($field in $requiredSnapshotFields) {
        if (-not $snapshotText.Contains($field) -or
            -not $snapshotWriterText.Contains($field) -or
            -not $snapshotBuilderText.Contains($field) -or
            -not $diagnosticRulesText.Contains($field)) {
            $missingSnapshotFields += $field
        }
    }

    if ($missingSnapshotFields.Count -eq 0 -and
        $diagnosticsText.Contains("MapRareCreatureDirectionGateReason") -and
        $diagnosticsText.Contains("MapTravellingMerchantDirectionTownLabelSource") -and
        $testText.Contains("MapTravellingMerchantDiagnosticsWriteRuntimeSnapshotJson") -and
        $testText.Contains("MapRareCreatureDiagnosticsWriteRuntimeSnapshotJson") -and
        $programText.Contains("map rare creature diagnostics write runtime snapshot json") -and
        -not $programText.Contains('RunExpectedFailure("map rare creature') -and
        -not $programText.Contains('RunExpectedFailure("map travelling merchant')) {
        Write-Pass "Map direction hint runtime snapshot fields are wired through diagnostics, builder, JSON writer, tests, and diagnostic docs."
    }
    else {
        Write-FailHealth "Map direction hint diagnostics must keep required runtime snapshot fields in DTO, builder, writer, tests, and diagnostic docs. Missing: $($missingSnapshotFields -join ', ')"
    }

    if ($rareDocText.Contains("MapRareCreatureDirectionGateReason") -and
        $rareDocText.Contains("lifeformAnalyzerMissing") -and
        $rareDocText.Contains("MapDirectionHintTargetScanCadenceTicks") -and
        $merchantDocText.Contains("MapDirectionHintTargetScanCadenceTicks") -and
        $merchantDocText.Contains("MapTravellingMerchantDirectionTownLabelSource") -and
        $featureIndexText.Contains("健康审计锁住两个方向提示") -and
        $diagnosticRulesText.Contains("Map / Direction Hints") -and
        $diagnosticRulesText.Contains("gateBlocked") -and
        $diagnosticRulesText.Contains("onScreenArrowOnly")) {
        Write-Pass "Map direction hint feature docs and diagnostic rules explain cadence, rare-creature gates, merchant label sources, and read-only boundaries."
    }
    else {
        Write-FailHealth "Map direction hint feature docs and diagnostic rules must explain shared cadence, rare-creature gates, merchant label sources, and read-only boundaries."
    }
}

function Test-MapFootprintGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $registrarPath = Join-Path $RepoRoot "src\JueMingZ\Features\Catalog\MapEnhancementFeatureRegistrar.cs"
    $featureIdsPath = Join-Path $RepoRoot "src\JueMingZ\Common\FeatureIds.cs"
    $appSettingsPath = Join-Path $RepoRoot "src\JueMingZ\Config\AppSettings.cs"
    $configServicePath = Join-Path $RepoRoot "src\JueMingZ\Config\ConfigService.cs"
    $settingsSnapshotPath = Join-Path $RepoRoot "src\JueMingZ\Runtime\RuntimeSettingsSnapshot.cs"
    $settingsSnapshotProviderPath = Join-Path $RepoRoot "src\JueMingZ\Runtime\RuntimeSettingsSnapshotProvider.cs"
    $runtimePath = Join-Path $RepoRoot "src\JueMingZ\Runtime\JueMingZRuntime.cs"
    $rootPath = Join-Path $RepoRoot "src\JueMingZ\Records\PlayerWorldFeatureDataRoot.cs"
    $modelsPath = Join-Path $RepoRoot "src\JueMingZ\Records\PlayerWorldFootprintModels.cs"
    $storePath = Join-Path $RepoRoot "src\JueMingZ\Records\PlayerWorldFootprintStore.cs"
    $cachePath = Join-Path $RepoRoot "src\JueMingZ\Records\PlayerWorldFootprintCache.cs"
    $servicePath = Join-Path $RepoRoot "src\JueMingZ\Records\PlayerWorldFootprintService.cs"
    $diagnosticsPath = Join-Path $RepoRoot "src\JueMingZ\Diagnostics\PlayerWorldFootprintDiagnostics.cs"
    $renderCachePath = Join-Path $RepoRoot "src\JueMingZ\Automation\MapEnhancement\MapFootprintRenderCache.cs"
    $playbackStatePath = Join-Path $RepoRoot "src\JueMingZ\Automation\MapEnhancement\MapFootprintPlaybackState.cs"
    $mapLayerPath = Join-Path $RepoRoot "src\JueMingZ\Hooks\PlayerWorldFootprintMapLayer.cs"
    $mapLayerInstallerPath = Join-Path $RepoRoot "src\JueMingZ\Hooks\PlayerWorldFootprintMapLayerInstaller.cs"
    $overlayInstallerPath = Join-Path $RepoRoot "src\JueMingZ\Hooks\MapFootprintFullscreenOverlayInstaller.cs"
    $overlayPath = Join-Path $RepoRoot "src\JueMingZ\UI\MapFootprintPlaybackOverlay.cs"
    $mapFullscreenCompatPath = Join-Path $RepoRoot "src\JueMingZ\Compat\MapFullscreenCompat.cs"
    $uiMouseCompatPath = Join-Path $RepoRoot "src\JueMingZ\Compat\TerrariaUiMouseCompat.cs"
    $diagnosticMouseReaderPath = Join-Path $RepoRoot "src\JueMingZ\UI\DiagnosticMouseStateReader.cs"
    $uiMouseCaptureServicePath = Join-Path $RepoRoot "src\JueMingZ\UI\UiMouseCaptureService.cs"
    $hookInstallerPath = Join-Path $RepoRoot "src\JueMingZ\Bootstrap\HookInstaller.cs"
    $playerInputScrollHookInstallerPath = Join-Path $RepoRoot "src\JueMingZ\Hooks\PlayerInputScrollHookInstaller.cs"
    $handlerPath = Join-Path $RepoRoot "src\JueMingZ\Input\LegacyUiActionService.MapEnhancementHandlers.cs"
    $snapshotPath = Join-Path $RepoRoot "src\JueMingZ\Diagnostics\DiagnosticSnapshot.cs"
    $snapshotWriterPath = Join-Path $RepoRoot "src\JueMingZ\Diagnostics\DiagnosticSnapshotWriter.Json.cs"
    $snapshotBuilderPath = Join-Path $RepoRoot "src\JueMingZ\Runtime\Diagnostics\RuntimeDiagnosticSnapshotBuilder.Bootstrap.cs"
    $testPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.PlayerWorldFootprintTests.cs"
    $programPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.cs"
    $featureDocPath = Join-Path $RepoRoot "文档\功能介绍\地图加强页\足迹.md"
    $diagnosticRulesPath = Join-Path $RepoRoot "文档\项目规则\AI诊断日志说明.md"

    $registrarText = Read-TextIfExists -Path $registrarPath
    $featureIdsText = Read-TextIfExists -Path $featureIdsPath
    $appSettingsText = Read-TextIfExists -Path $appSettingsPath
    $configServiceText = Read-TextIfExists -Path $configServicePath
    $settingsSnapshotText = Read-TextIfExists -Path $settingsSnapshotPath
    $settingsSnapshotProviderText = Read-TextIfExists -Path $settingsSnapshotProviderPath
    $runtimeText = Read-TextIfExists -Path $runtimePath
    $rootText = Read-TextIfExists -Path $rootPath
    $modelsText = Read-TextIfExists -Path $modelsPath
    $storeText = Read-TextIfExists -Path $storePath
    $cacheText = Read-TextIfExists -Path $cachePath
    $serviceText = Read-TextIfExists -Path $servicePath
    $diagnosticsText = Read-TextIfExists -Path $diagnosticsPath
    $renderCacheText = Read-TextIfExists -Path $renderCachePath
    $playbackStateText = Read-TextIfExists -Path $playbackStatePath
    $mapLayerText = Read-TextIfExists -Path $mapLayerPath
    $mapLayerInstallerText = Read-TextIfExists -Path $mapLayerInstallerPath
    $overlayInstallerText = Read-TextIfExists -Path $overlayInstallerPath
    $overlayText = Read-TextIfExists -Path $overlayPath
    $mapFullscreenCompatText = Read-TextIfExists -Path $mapFullscreenCompatPath
    $uiMouseCompatText = Read-TextIfExists -Path $uiMouseCompatPath
    $diagnosticMouseReaderText = Read-TextIfExists -Path $diagnosticMouseReaderPath
    $uiMouseCaptureServiceText = Read-TextIfExists -Path $uiMouseCaptureServicePath
    $hookInstallerText = Read-TextIfExists -Path $hookInstallerPath
    $playerInputScrollHookInstallerText = Read-TextIfExists -Path $playerInputScrollHookInstallerPath
    $handlerText = Read-TextIfExists -Path $handlerPath
    $snapshotText = Read-TextIfExists -Path $snapshotPath
    $snapshotWriterText = Read-TextIfExists -Path $snapshotWriterPath
    $snapshotBuilderText = Read-TextIfExists -Path $snapshotBuilderPath
    $testText = Read-TextIfExists -Path $testPath
    $programText = Read-TextIfExists -Path $programPath
    $featureDocText = Read-TextIfExists -Path $featureDocPath
    $diagnosticRulesText = Read-TextIfExists -Path $diagnosticRulesPath

    if ($null -eq $registrarText -or $null -eq $featureIdsText -or $null -eq $appSettingsText -or $null -eq $configServiceText -or $null -eq $settingsSnapshotText -or $null -eq $settingsSnapshotProviderText -or $null -eq $runtimeText -or $null -eq $rootText -or $null -eq $modelsText -or $null -eq $storeText -or $null -eq $cacheText -or $null -eq $serviceText -or $null -eq $diagnosticsText -or $null -eq $renderCacheText -or $null -eq $playbackStateText -or $null -eq $mapLayerText -or $null -eq $mapLayerInstallerText -or $null -eq $overlayInstallerText -or $null -eq $overlayText -or $null -eq $diagnosticMouseReaderText -or $null -eq $uiMouseCaptureServiceText -or $null -eq $hookInstallerText -or $null -eq $playerInputScrollHookInstallerText -or $null -eq $handlerText -or $null -eq $snapshotText -or $null -eq $snapshotWriterText -or $null -eq $snapshotBuilderText -or $null -eq $testText -or $null -eq $programText -or $null -eq $featureDocText -or $null -eq $diagnosticRulesText) {
        Write-FailHealth "Map footprints registrar, config, runtime, store/cache/service, render/playback, diagnostics, docs, and tests must exist as separate responsibilities."
        return
    }

    if ($featureIdsText.Contains('MapFootprints = "map.footprints"') -and
        $registrarText.Contains("FeatureIds.MapFootprints") -and
        $registrarText.Contains(".Domain(FeatureCodeDomain.MapEnhancement)") -and
        $registrarText.Contains(".Category(FeatureUserCategory.MapEnhancement)") -and
        $registrarText.Contains(".Actions(InputActionKind.None)") -and
        $registrarText.Contains(".Implemented(true)") -and
        $registrarText.Contains("LocalAssistPendingMultiplayerVerification")) {
        Write-Pass "Map footprints stay registered as an implemented map-enhancement display feature with no ActionQueue requirement."
    }
    else {
        Write-FailHealth "map.footprints must stay in MapEnhancementFeatureRegistrar with MapEnhancement domain/category, None actions, implemented status, and pending multiplayer verification."
    }

    if ($appSettingsText.Contains("MapFootprintsDisplayEnabled") -and
        $configServiceText.Contains("FeatureIds.MapFootprints") -and
        $settingsSnapshotText.Contains("MapFootprintsDisplayEnabled") -and
        $settingsSnapshotProviderText.Contains("_mapFootprintsDisplayEnabled") -and
        $handlerText.Contains("displayOnly") -and
        $handlerText.Contains("recordingAffected") -and
        $testText.Contains("MapFootprintsDisplayConfigDefaultsAndFeatureSync")) {
        Write-Pass "Map footprints display config remains a display-only switch synced to settings and UI metadata."
    }
    else {
        Write-FailHealth "MapFootprintsDisplayEnabled must remain a display-only config with FeatureSettings/runtime snapshot sync and recordingAffected=false UI metadata."
    }

    if ($rootText.Contains('FootprintsFileName = "footprints.json"') -and
        $storeText.Contains("BuildPlayerWorldFeatureFilePath(pairId, PlayerWorldFeatureDataRoot.FootprintsFileName)") -and
        $storeText.Contains("PlayerWorldFeatureDataStore.TryWriteJson") -and
        $storeText.Contains("identityUnavailable") -and
        $cacheText.Contains("PlayerWorldFootprintStore.ReadForPair") -and
        $modelsText.Contains("MaxRetainedHours = 200L") -and
        $testText.Contains("PlayerWorldFootprintsIdentityUnavailableDoesNotWriteUnknown")) {
        Write-Pass "Map footprints JSON stays pair-scoped, safe-written by the store, capped at 200h, and fail-closed without unknown buckets."
    }
    else {
        Write-FailHealth "footprints.json must stay behind pair-scoped PlayerWorldFootprintStore safe writes, 200h retention, and identity fail-closed coverage."
    }

    $gameReadIndex = $runtimeText.IndexOf('"game-state-read"', [System.StringComparison]::Ordinal)
    $recordingIndex = $runtimeText.IndexOf('"player-world-footprints-recording"', [System.StringComparison]::Ordinal)
    $renderIndex = $runtimeText.IndexOf('"player-world-footprints-render-cache"', [System.StringComparison]::Ordinal)
    $inputGateIndex = $runtimeText.IndexOf('"input-focus-guard"', [System.StringComparison]::Ordinal)
    if ($gameReadIndex -ge 0 -and $recordingIndex -gt $gameReadIndex -and $renderIndex -gt $recordingIndex -and $inputGateIndex -gt $renderIndex -and
        $runtimeText.Contains("RunPlayerWorldFootprintsRecording") -and
        $runtimeText.Contains("PlayerWorldFootprintService.Tick") -and
        $runtimeText.Contains("MapFootprintRenderCache.Tick")) {
        Write-Pass "Map footprints recording and render-cache stages stay after game-state-read and before input/UI/action gates."
    }
    else {
        Write-FailHealth "Map footprints runtime stages must stay after game-state-read and before input-focus/action gates, with render cache after recording."
    }

    if (-not $serviceText.Contains("MapFootprintsDisplayEnabled") -and
        -not $serviceText.Contains("GameInputAvailable") -and
        -not $serviceText.Contains("InputActionQueue") -and
        -not $serviceText.Contains("ActionSubmitting") -and
        $serviceText.Contains("PlayerWorldIdentityRuntimeCache.TryResolveCurrentCached") -and
        $serviceText.Contains("FlushPending") -and
        $testText.Contains("PlayerWorldFootprintRecorderDisplayOffStillRecords") -and
        $testText.Contains("PlayerWorldFootprintRecorderUiStatesDoNotBlockRecording") -and
        $testText.Contains("PlayerWorldFootprintRecorderWallClockGapBreaksWithoutDuration")) {
        Write-Pass "Map footprints recorder ignores display/UI/action gates and keeps identity, flush, and no-tick gap tests."
    }
    else {
        Write-FailHealth "Map footprints recorder must not read MapFootprintsDisplayEnabled, UI input focus, or ActionQueue gates; tests must cover display-off, UI states, and wall-clock gaps."
    }

    $forbiddenMutationPattern = '(Player\.Teleport|NetMessage|\.statLife\s*=|\.statMana\s*=|\.velocity\s*=|\.position\s*=|\.fallStart\s*=|\.noFallDmg\s*=|AddBuff\s*\(|\.buffType\s*=|\.buffTime\s*=|\.stack\s*=|Main\.tile|NPC\[[^\]]+\]\s*=|WorldGen\.)'
    $footprintRuntimeFiles = @($servicePath, $renderCachePath, $playbackStatePath, $mapLayerPath, $overlayPath, $diagnosticsPath)
    $mutationLeaks = @()
    foreach ($path in $footprintRuntimeFiles) {
        $text = Read-TextIfExists -Path $path
        if ($null -ne $text -and [System.Text.RegularExpressions.Regex]::IsMatch($text, $forbiddenMutationPattern)) {
            $mutationLeaks += $path.Substring($RepoRoot.Length).TrimStart('\', '/').Replace('\', '/')
        }
    }

    if ($mutationLeaks.Count -eq 0) {
        Write-Pass "Map footprints runtime, draw, playback, and diagnostics files avoid player/world/inventory/NPC/tile/network mutations."
    }
    else {
        Write-FailHealth "Map footprints must not mutate player/world/inventory/NPC/tile/network state: $($mutationLeaks -join ', ')"
    }

    if ($renderCacheText.Contains("PlayerWorldFootprintService.TryGetInMemoryForPair") -and
        $renderCacheText.Contains("PlayerWorldFootprintCache.ReadForPair") -and
        $renderCacheText.Contains("BuildDrawPlan") -and
        $renderCacheText.Contains("cursorTicks") -and
        $renderCacheText.Contains("pendingStart = end;") -and
        $renderCacheText.Contains("TryClipLineToScreen") -and
        $renderCacheText.Contains("ClipLineParameter") -and
        $renderCacheText.Contains("MapFootprintsDisplayEnabled") -and
        -not $renderCacheText.Contains("TryWriteJson") -and
        -not $renderCacheText.Contains("SaveForPair") -and
        -not $renderCacheText.Contains("PlayerWorldFootprintStore") -and
        $testText.Contains("MapFootprintRenderCacheBuildsLinesWithoutCrossSegmentConnection") -and
        $testText.Contains("MapFootprintRenderDrawPlanCullsThinsAndLimits") -and
        $testText.Contains("MapFootprintRenderDrawPlanAdvancesAfterCulledLine") -and
        $testText.Contains("MapFootprintRenderDrawPlanRejectsUnclippedReentryLongLineSpec") -and
        $testText.Contains("MapFootprintRenderDrawPlanClipsViewportEdges") -and
        -not $programText.Contains('RunExpectedFailure("map footprint render draw plan rejects unclipped reentry long line"') -and
        $testText.Contains("MapFootprintPlaybackDrawPlanSlicesCurrentLine")) {
        Write-Pass "Map footprints render cache stays read-only, display-gated, clips screen-space draw commands, advances culled draw starts, and covers cull/thin/limit/time-slice drawing."
    }
    else {
        Write-FailHealth "Map footprints render cache must only read in-memory/cache snapshots, clip screen-space draw commands, advance pending starts after culled lines, keep cursor time-slice draw planning, and avoid JSON/store writes."
    }

    if ($mapLayerText.Contains("MapFootprintRenderCache.GetSnapshot") -and
        $mapLayerText.Contains("MapFootprintRenderCache.BuildDrawPlan") -and
        $mapLayerText.Contains("PlayerWorldFootprintDiagnostics.RecordMapDraw") -and
        $mapLayerText.Contains("PlayerWorldFootprintDiagnostics.RecordMapDrawDetail") -and
        $mapLayerText.Contains("BuildDrawDiagnostics") -and
        $mapLayerText.Contains("LineThicknessPixels = 1f") -and
        $mapLayerText.Contains("MagicPixelSourceRectangle") -and
        $mapLayerText.Contains("new Rectangle(0, 0, 1, 1)") -and
        $mapLayerText.Contains("MagicPixel asset is 1x1000") -and
        $mapLayerText.Contains("MagicPixelSourceRectangle,") -and
        -not $mapLayerText.Contains("command.Start,`r`n                null,") -and
        -not $mapLayerText.Contains("command.Start,`n                null,") -and
        $mapLayerText.Contains("MapFootprintRenderCache.DefaultMaxDrawnLines") -and
        $mapLayerText.Contains("MapFootprintRenderCache.DefaultMinDrawPixelStep") -and
        $renderCacheText.Contains("DefaultMaxDrawnLines = 6000") -and
        $renderCacheText.Contains("DefaultMinDrawPixelStep = 1.5f") -and
        -not $mapLayerText.Contains("TryWriteJson") -and
        -not $mapLayerText.Contains("PlayerWorldFootprintStore") -and
        -not $mapLayerText.Contains("PlayerWorldFootprintCache.Read")) {
        Write-Pass "Map footprint map layer draws from render cache with 1px lines, 1x1 MagicPixel source crop, cull/thin/limit diagnostics, and no JSON/store reads."
    }
    else {
        Write-FailHealth "PlayerWorldFootprintMapLayer must draw only from render cache, keep 1px footprint lines, crop MagicPixel to a 1x1 source rectangle, keep draw limits, and avoid JSON/store/cache file reads."
    }

    if ($overlayInstallerText.Contains("Main.OnPostFullscreenMapDraw") -and
        $overlayText.Contains("DrawFullscreenMapLayer") -and
        $overlayText.Contains("UpdatePrefixGuard") -and
        $hookInstallerText.Contains("MapFootprintPlaybackOverlay.UpdatePrefixGuard") -and
        $overlayText.Contains("ReadFullscreenUiFrame(true)") -and
        $overlayText.Contains("ReadFullscreenUiFrame(false)") -and
        $overlayText.Contains("BuildFullscreenUiFrame") -and
        $overlayText.Contains("RememberDrawFrameExtent") -and
        $overlayText.Contains("ApplyLastDrawFrameExtent") -and
        $overlayText.Contains("/FullscreenUi") -and
        -not $overlayText.Contains("LegacyUiInput.ReadMouse()") -and
        $playbackStateText.Contains("var safeWidth = Math.Max(1, screenWidth)") -and
        $playbackStateText.Contains("var safeHeight = Math.Max(1, screenHeight)") -and
        $playbackStateText.Contains("safeHeight - bottomMargin - height") -and
        -not $overlayText.Contains("MapOverlayDrawContext") -and
        -not $playbackStateText.Contains("MapOverlayDrawContext") -and
        $testText.Contains("MapFootprintPlaybackDefaultsToLatestPausedAndScreenSpaceLayout") -and
        $testText.Contains("MapFootprintPlaybackFullscreenUiScaleHitTestCapturesBar") -and
        $testText.Contains("MapFootprintPlaybackPrefixUsesLastDrawExtentForHitTest")) {
        Write-Pass "Map footprint playback overlay stays fullscreen UI screen-space, UIScale-aware, bottom-safe-margin based, and prefix hit-test follows the last draw extent."
    }
    else {
        Write-FailHealth "Map footprint playback UI must use OnPostFullscreenMapDraw plus fullscreen UI-scale screen-size layout, reuse the last draw extent for prefix hit-test, and avoid F5 base-logical or map-content coordinates."
    }

    if ($playbackStateText.Contains("ShouldSuppressFullscreenMapLeftInput") -and
        $playbackStateText.Contains("ShouldSuppressFullscreenMapNonLeftInput") -and
        $playbackStateText.Contains("ShouldClearFullscreenMapPanState") -and
        $playbackStateText.Contains("releaseOwnedByPlayback") -and
        $playbackStateText.Contains("var playbackSurfaceCaptured = hit.BarHovered || _dragging || releaseOwnedByPlayback") -and
        $playbackStateText.Contains("interaction.MouseCaptured = playbackSurfaceCaptured") -and
        $playbackStateText.Contains("interaction.ScrollConsumed = interaction.MouseCaptured") -and
        $playbackStateText.Contains("DisplayTimelineEndTicks") -and
        $overlayText.Contains("ReadForFullscreenMapOverlay") -and
        $overlayText.Contains("UpdateAfterPlayerInputGuard") -and
        $overlayText.Contains("PlayerInput can rewrite Main.mouseLeft") -and
        $overlayText.Contains("FullscreenMapNonLeftMouseTriggerTokens") -and
        $overlayText.Contains('TerrariaUiMouseCompat.TryConsumeMouseTriggerInput("MouseLeft"') -and
        $overlayText.Contains("TerrariaUiMouseCompat.TryConsumeMouseTriggerInputOnceForUi") -and
        $overlayText.Contains("MapFullscreenCompat.TryClearFullscreenMapPanState") -and
        $overlayText.Contains("TerrariaUiMouseCompat.TryMarkUiMouseCapture") -and
        $overlayText.Contains("TerrariaUiMouseCompat.TryConsumeUiScroll") -and
        $uiMouseCompatText.Contains("TryConsumeMouseTriggerInputOnceForUi") -and
        $mapFullscreenCompatText.Contains("TryClearFullscreenMapPanState") -and
        $playerInputScrollHookInstallerText.Contains("MapFootprintPlaybackOverlay.UpdateAfterPlayerInputGuard") -and
        $overlayText.Contains("raw.GameInputAvailable") -and
        $overlayText.Contains("raw.TerrariaReadAvailable && raw.TerrariaLeftDown") -and
        $diagnosticMouseReaderText.Contains("ReadForFullscreenMapOverlay") -and
        $diagnosticMouseReaderText.Contains("FullscreenOverlayGateBypass") -and
        $overlayText.Contains("CalculateTrackFilledWidth") -and
        $overlayText.Contains("RecordPlaybackPrefixInput") -and
        $overlayText.Contains("RecordPlaybackDrawInput") -and
        $uiMouseCaptureServiceText.Contains("TerrariaUiMouseCompat.TryConsumeMouseTriggerInput") -and
        $testText.Contains("MapFootprintPlaybackHandlesRateDragAndInputHandoff") -and
        $testText.Contains("MapFootprintPlaybackAfterPlayerInputGuardSuppressesRewrittenLeft") -and
        $testText.Contains("MapFootprintPlaybackCapturesWheelAndNonLeftMouseInsideBar") -and
        $testText.Contains("MapFootprintPlaybackClearsPanStateForPlaybackOwnedLeft") -and
        $testText.Contains("MapFootprintPlaybackOutsideMapActionsDoNotChangePlayback") -and
        $testText.Contains("MapFootprintPlaybackReleaseFrameStopsBeforeNextOutsideDrag") -and
        $testText.Contains("MapFootprintPlaybackPausedProgressDisplayEndStaysStable") -and
        $testText.Contains("MapFootprintPlaybackFullscreenMouseKeepsReadableClickWhenGlobalGateFalseSpec") -and
        $testText.Contains("MapFootprintPlaybackFullscreenMouseDoesNotUseOsFallbackWhenGlobalGateFalse") -and
        $testText.Contains("MapFootprintPlaybackFullscreenCaptureClearsClickWhenGlobalGateFalse") -and
        -not $programText.Contains('RunExpectedFailure("map footprint playback fullscreen mouse keeps readable click when global gate false"')) {
        Write-Pass "Map footprint playback input consumption keeps fullscreen bar left ownership through PlayerInput postfix, blocks wheel/non-left tokens, clears pan anchors for playback-owned left input, preserves outside map handoff, keeps gate-false Terraria mouse reads, and paused display progress uses a frozen visible timeline end."
    }
    else {
        Write-FailHealth "Map footprint playback input must keep explicit left ownership, suppress rewritten MouseLeft after PlayerInput, block wheel/non-left tokens, clear fullscreen map pan anchors only for playback-owned left input, keep outside map handoff, keep gate-false Terraria mouse reads without OS fallback, forbid expected-failure rollback, and preserve paused display-end coverage."
    }

    $footprintFiles = @($servicePath, $renderCachePath, $playbackStatePath, $mapLayerPath, $overlayPath, $mapLayerInstallerPath, $overlayInstallerPath, $diagnosticsPath, $storePath, $cachePath)
    $actionBackflow = @()
    foreach ($path in $footprintFiles) {
        $text = Read-TextIfExists -Path $path
        if ($null -ne $text -and ($text.Contains("InputActionQueue") -or $text.Contains("InputActionRequest") -or $text.Contains("ActionKind"))) {
            $actionBackflow += $path.Substring($RepoRoot.Length).TrimStart('\', '/').Replace('\', '/')
        }
    }

    if ($actionBackflow.Count -eq 0) {
        Write-Pass "Map footprints feature files stay out of ActionQueue and input action request paths."
    }
    else {
        Write-FailHealth "Map footprints must not introduce ActionQueue/request backflow: $($actionBackflow -join ', ')"
    }

    $requiredDiagnosticFields = @(
        "MapFootprintsDisplayEnabled",
        "PlayerWorldFootprintsLastStatus",
        "PlayerWorldFootprintsLastDecision",
        "PlayerWorldFootprintsLastMessage",
        "PlayerWorldFootprintsLastPairId",
        "PlayerWorldFootprintsIdentityResolved",
        "PlayerWorldFootprintsIsRecording",
        "PlayerWorldFootprintsReadFailed",
        "PlayerWorldFootprintsWriteFailed",
        "PlayerWorldFootprintsRetentionTrimmed",
        "PlayerWorldFootprintsMaxRetainedHours",
        "PlayerWorldFootprintsRetainedHours",
        "PlayerWorldFootprintsSegmentCount",
        "PlayerWorldFootprintsPointCount",
        "PlayerWorldFootprintsBreakCount",
        "PlayerWorldFootprintsTimelineStartTicks",
        "PlayerWorldFootprintsTimelineEndTicks",
        "PlayerWorldFootprintsLastPointTileX",
        "PlayerWorldFootprintsLastPointTileY",
        "PlayerWorldFootprintsLastPointDurationTicks",
        "PlayerWorldFootprintsLastRecordRuntimeTick",
        "PlayerWorldFootprintsLastFlushStatus",
        "PlayerWorldFootprintsLastReadUtc",
        "PlayerWorldFootprintsLastRecordUtc",
        "PlayerWorldFootprintsLastWriteUtc",
        "MapFootprintsRenderCacheStatus",
        "MapFootprintsRenderCacheMessage",
        "MapFootprintsRenderCachePairId",
        "MapFootprintsRenderCacheSource",
        "MapFootprintsRenderCacheSegmentCount",
        "MapFootprintsRenderCachePointCount",
        "MapFootprintsRenderCacheLineCount",
        "MapFootprintsRenderCacheDataSignature",
        "MapFootprintsRenderCacheLimitHit",
        "MapFootprintsLastDrawStatus",
        "MapFootprintsLastDrawMessage",
        "MapFootprintsLastDrawPairId",
        "MapFootprintsCachedLineCount",
        "MapFootprintsDrawnLineCount",
        "MapFootprintsCulledLineCount",
        "MapFootprintsThinnedLineCount",
        "MapFootprintsDrawLimitSkippedLineCount",
        "MapFootprintsDrawLimitHit",
        "MapFootprintsLastDrawUtc",
        "MapFootprintsDrawRoute",
        "MapFootprintsDrawScreenWidth",
        "MapFootprintsDrawScreenHeight",
        "MapFootprintsDrawGameUpdateCount",
        "MapFootprintsDrawMapFullscreenPosX",
        "MapFootprintsDrawMapFullscreenPosY",
        "MapFootprintsDrawMapFullscreenScale",
        "MapFootprintsDrawTransformMapPositionX",
        "MapFootprintsDrawTransformMapPositionY",
        "MapFootprintsDrawTransformMapOffsetX",
        "MapFootprintsDrawTransformMapOffsetY",
        "MapFootprintsDrawTransformMapScale",
        "MapFootprintsDrawTransformOpacity",
        "MapFootprintsDrawCommandSampleCount",
        "MapFootprintsDrawAbnormalLongLineCount",
        "MapFootprintsDrawLongLineThresholdPixels",
        "MapFootprintsDrawMaxLinePixels",
        "MapFootprintsDrawMaxLineSegmentIndex",
        "MapFootprintsDrawFirstSegmentIndex",
        "MapFootprintsDrawFirstStartTileX",
        "MapFootprintsDrawFirstStartTileY",
        "MapFootprintsDrawFirstEndTileX",
        "MapFootprintsDrawFirstEndTileY",
        "MapFootprintsDrawFirstStartScreenX",
        "MapFootprintsDrawFirstStartScreenY",
        "MapFootprintsDrawFirstEndScreenX",
        "MapFootprintsDrawFirstEndScreenY",
        "MapFootprintsDrawLastSegmentIndex",
        "MapFootprintsDrawLastStartTileX",
        "MapFootprintsDrawLastStartTileY",
        "MapFootprintsDrawLastEndTileX",
        "MapFootprintsDrawLastEndTileY",
        "MapFootprintsDrawLastStartScreenX",
        "MapFootprintsDrawLastStartScreenY",
        "MapFootprintsDrawLastEndScreenX",
        "MapFootprintsDrawLastEndScreenY",
        "MapFootprintsDrawLongestSegmentIndex",
        "MapFootprintsDrawLongestStartTileX",
        "MapFootprintsDrawLongestStartTileY",
        "MapFootprintsDrawLongestEndTileX",
        "MapFootprintsDrawLongestEndTileY",
        "MapFootprintsDrawLongestStartScreenX",
        "MapFootprintsDrawLongestStartScreenY",
        "MapFootprintsDrawLongestEndScreenX",
        "MapFootprintsDrawLongestEndScreenY",
        "MapFootprintsPlaybackOverlayStatus",
        "MapFootprintsPlaybackOverlayMessage",
        "MapFootprintsPlaybackPairId",
        "MapFootprintsPlaybackPaused",
        "MapFootprintsPlaybackRate",
        "MapFootprintsPlaybackCursorTicks",
        "MapFootprintsPlaybackTimelineStartTicks",
        "MapFootprintsPlaybackLatestTicks",
        "MapFootprintsPlaybackProgress",
        "MapFootprintsPlaybackAtLatest",
        "MapFootprintsPlaybackDragging",
        "MapFootprintsPlaybackMouseCaptured",
        "MapFootprintsPlaybackBarHovered",
        "MapFootprintsPlaybackLastInteraction",
        "MapFootprintsPlaybackLastUpdateUtc",
        "MapFootprintsPlaybackPrefixHitTarget",
        "MapFootprintsPlaybackPrefixMouseReadMode",
        "MapFootprintsPlaybackPrefixMouseX",
        "MapFootprintsPlaybackPrefixMouseY",
        "MapFootprintsPlaybackPrefixMouseReadAvailable",
        "MapFootprintsPlaybackPrefixBarHovered",
        "MapFootprintsPlaybackPrefixMouseCaptured",
        "MapFootprintsPlaybackPrefixClickConsumed",
        "MapFootprintsPlaybackPrefixScrollConsumed",
        "MapFootprintsPlaybackPrefixShouldSuppressLeftInput",
        "MapFootprintsPlaybackPrefixShouldSuppressNonLeftInput",
        "MapFootprintsPlaybackPrefixShouldClearPanState",
        "MapFootprintsPlaybackPrefixLeftInputSuppressed",
        "MapFootprintsPlaybackPrefixNonLeftInputSuppressed",
        "MapFootprintsPlaybackPrefixScrollSuppressed",
        "MapFootprintsPlaybackPrefixPanStateClearAttempted",
        "MapFootprintsPlaybackPrefixPanStateClearSucceeded",
        "MapFootprintsPlaybackPrefixLeftDown",
        "MapFootprintsPlaybackPrefixLeftPressed",
        "MapFootprintsPlaybackPrefixLeftReleased",
        "MapFootprintsPlaybackPrefixScrollDelta",
        "MapFootprintsPlaybackPrefixGameUpdateCount",
        "MapFootprintsPlaybackPrefixMainMouseLeftBefore",
        "MapFootprintsPlaybackPrefixMainMouseLeftAfter",
        "MapFootprintsPlaybackPrefixMainMouseLeftReleaseBefore",
        "MapFootprintsPlaybackPrefixMainMouseLeftReleaseAfter",
        "MapFootprintsPlaybackPrefixMainMouseRightBefore",
        "MapFootprintsPlaybackPrefixMainMouseRightAfter",
        "MapFootprintsPlaybackPrefixMainMouseRightReleaseBefore",
        "MapFootprintsPlaybackPrefixMainMouseRightReleaseAfter",
        "MapFootprintsPlaybackPrefixMainMouseScrollWheelBefore",
        "MapFootprintsPlaybackPrefixMainMouseScrollWheelAfter",
        "MapFootprintsPlaybackPrefixMainOldMouseScrollWheelBefore",
        "MapFootprintsPlaybackPrefixMainOldMouseScrollWheelAfter",
        "MapFootprintsPlaybackPrefixMainMouseInterfaceBefore",
        "MapFootprintsPlaybackPrefixMainMouseInterfaceAfter",
        "MapFootprintsPlaybackPrefixMainBlockMouseBefore",
        "MapFootprintsPlaybackPrefixMainBlockMouseAfter",
        "MapFootprintsPlaybackPrefixPlayerMouseInterfaceBefore",
        "MapFootprintsPlaybackPrefixPlayerMouseInterfaceAfter",
        "MapFootprintsPlaybackPrefixUtc",
        "MapFootprintsPlaybackAfterPlayerInputGuardActive",
        "MapFootprintsPlaybackAfterPlayerInputShouldSuppressLeftInput",
        "MapFootprintsPlaybackAfterPlayerInputShouldSuppressNonLeftInput",
        "MapFootprintsPlaybackAfterPlayerInputReleaseFrame",
        "MapFootprintsPlaybackAfterPlayerInputMainMouseLeftBefore",
        "MapFootprintsPlaybackAfterPlayerInputMainMouseLeftAfter",
        "MapFootprintsPlaybackAfterPlayerInputMainMouseLeftReleaseBefore",
        "MapFootprintsPlaybackAfterPlayerInputMainMouseLeftReleaseAfter",
        "MapFootprintsPlaybackAfterPlayerInputMainMouseRightBefore",
        "MapFootprintsPlaybackAfterPlayerInputMainMouseRightAfter",
        "MapFootprintsPlaybackAfterPlayerInputMainMouseRightReleaseBefore",
        "MapFootprintsPlaybackAfterPlayerInputMainMouseRightReleaseAfter",
        "MapFootprintsPlaybackAfterPlayerInputGameUpdateCount",
        "MapFootprintsPlaybackAfterPlayerInputUtc",
        "MapFootprintsPlaybackDrawHitTarget",
        "MapFootprintsPlaybackDrawMouseReadMode",
        "MapFootprintsPlaybackDrawMouseX",
        "MapFootprintsPlaybackDrawMouseY",
        "MapFootprintsPlaybackDrawMouseReadAvailable",
        "MapFootprintsPlaybackDrawBarHovered",
        "MapFootprintsPlaybackDrawMainMouseLeft",
        "MapFootprintsPlaybackDrawMainMouseLeftRelease",
        "MapFootprintsPlaybackDrawMainMouseRight",
        "MapFootprintsPlaybackDrawMainMouseRightRelease",
        "MapFootprintsPlaybackDrawMainMouseScrollWheel",
        "MapFootprintsPlaybackDrawMainOldMouseScrollWheel",
        "MapFootprintsPlaybackDrawMainMouseInterface",
        "MapFootprintsPlaybackDrawMainBlockMouse",
        "MapFootprintsPlaybackDrawPlayerMouseInterface",
        "MapFootprintsPlaybackDrawGameUpdateCount",
        "MapFootprintsPlaybackDrawUtc"
    )
    $missingDiagnosticFields = @()
    foreach ($field in $requiredDiagnosticFields) {
        if (-not $snapshotText.Contains($field) -or
            -not $snapshotWriterText.Contains($field) -or
            -not $snapshotBuilderText.Contains($field)) {
            $missingDiagnosticFields += $field
        }
    }

    if ($diagnosticsText.Contains("RecordRuntime") -and
        $diagnosticsText.Contains("RecordRenderCache") -and
        $diagnosticsText.Contains("RecordMapDraw") -and
        $diagnosticsText.Contains("RecordMapDrawDetail") -and
        $diagnosticsText.Contains("RecordPlaybackOverlay") -and
        $diagnosticsText.Contains("RecordPlaybackPrefixInput") -and
        $diagnosticsText.Contains("RecordPlaybackAfterPlayerInputGuard") -and
        $diagnosticsText.Contains("RecordPlaybackDrawInput") -and
        $missingDiagnosticFields.Count -eq 0 -and
        $testText.Contains("PlayerWorldFootprintsDiagnosticsWrittenToSnapshot") -and
        $testText.Contains("MapFootprintsDrawLongLineThresholdPixels") -and
        $testText.Contains("MapFootprintsDrawLongestStartTileX") -and
        $testText.Contains("MapFootprintsDrawLongestStartScreenX") -and
        $testText.Contains("MapFootprintsPlaybackPrefixMouseReadMode") -and
        $testText.Contains("FullscreenOverlayGateBypass/FullscreenUi") -and
        $testText.Contains("MapFootprintsPlaybackPrefixMouseReadAvailable") -and
        $testText.Contains("MapFootprintsPlaybackPrefixShouldSuppressLeftInput") -and
        $testText.Contains("MapFootprintsPlaybackPrefixShouldSuppressNonLeftInput") -and
        $testText.Contains("MapFootprintsPlaybackPrefixPanStateClearSucceeded") -and
        $testText.Contains("MapFootprintsPlaybackAfterPlayerInputGuardActive") -and
        $testText.Contains("MapFootprintsPlaybackAfterPlayerInputMainMouseRightAfter") -and
        $testText.Contains("MapFootprintsPlaybackPrefixMainMouseLeftAfter") -and
        $testText.Contains("MapFootprintsPlaybackPrefixMainMouseLeftReleaseAfter") -and
        $testText.Contains("MapFootprintsPlaybackPrefixMainMouseRightAfter") -and
        $testText.Contains("MapFootprintsPlaybackPrefixMainOldMouseScrollWheelAfter") -and
        $testText.Contains("MapFootprintsPlaybackDrawMouseReadMode") -and
        $testText.Contains("MapFootprintsPlaybackDrawMouseReadAvailable") -and
        $testText.Contains("MapFootprintsPlaybackDrawMainMouseRight") -and
        $testText.Contains("MapFootprintsPlaybackDrawMainMouseScrollWheel") -and
        $testText.Contains("MapFootprintsPlaybackDrawGameUpdateCount") -and
        $programText.Contains("player-world footprints diagnostics written to snapshot")) {
        Write-Pass "Map footprints recorder/render/playback diagnostics reach runtime snapshot JSON and console coverage."
    }
    else {
        Write-FailHealth "Map footprints diagnostics must expose recorder, render, draw, and playback fields through DiagnosticSnapshot, JSON writer, builder, and JSON-focused tests. Missing=$($missingDiagnosticFields -join ', ')"
    }

    if ($featureDocText.Contains("MapFootprintsDisplayEnabled") -and
        $featureDocText.Contains("display-only") -and
        $featureDocText.Contains("记录服务不读取该开关") -and
        $featureDocText.Contains("PlayerWorldFootprintsLastDecision") -and
        $featureDocText.Contains("MapFootprintsPlaybackProgress") -and
        $featureDocText.Contains("MapFootprintsDrawMaxLinePixels") -and
        $featureDocText.Contains("draw command 的 screen start/end 是裁剪后的最终绘制端点") -and
        $featureDocText.Contains("tile start/end 仍保留原始缓存线段端点") -and
        $featureDocText.Contains("MapFootprintsPlaybackPrefixShouldSuppressLeftInput") -and
        $featureDocText.Contains("MapFootprintsPlaybackPrefixPanStateClearSucceeded") -and
        $featureDocText.Contains("MapFootprintsPlaybackAfterPlayerInputGuardActive") -and
        $featureDocText.Contains("MapFootprintsPlaybackDrawMainMouseRight") -and
        $featureDocText.Contains("MapFootprintsPlaybackDrawMouseReadMode") -and
        $featureDocText.Contains("MapFootprintsPlaybackDrawMainMouseLeftRelease") -and
        $featureDocText.Contains("FullscreenOverlayGateBypass") -and
        $featureDocText.Contains("Draw、UI 和 cache 读取路径不得写 JSON") -and
        $diagnosticRulesText.Contains("PlayerWorldFootprintsLastDecision") -and
        $diagnosticRulesText.Contains("MapFootprintsPlaybackProgress") -and
        $diagnosticRulesText.Contains("MapFootprintsDrawMaxLinePixels") -and
        $diagnosticRulesText.Contains("screen 坐标表示最终裁剪后的 draw command 端点") -and
        $diagnosticRulesText.Contains("tile 坐标仍保留原始缓存线段端点") -and
        $diagnosticRulesText.Contains("MapFootprintsPlaybackPrefixShouldSuppressLeftInput") -and
        $diagnosticRulesText.Contains("MapFootprintsPlaybackPrefixPanStateClearSucceeded") -and
        $diagnosticRulesText.Contains("MapFootprintsPlaybackAfterPlayerInputGuardActive") -and
        $diagnosticRulesText.Contains("MapFootprintsPlaybackDrawMainMouseRight") -and
        $diagnosticRulesText.Contains("MapFootprintsPlaybackPrefixMainMouseLeftAfter") -and
        $diagnosticRulesText.Contains("MapFootprintsPlaybackDrawMouseReadMode") -and
        $diagnosticRulesText.Contains("FullscreenOverlayGateBypass") -and
        $diagnosticRulesText.Contains("display-only") -and
        $diagnosticRulesText.Contains("inputUiStillRecording")) {
        Write-Pass "Map footprints feature and diagnostics docs explain display-only recording, snapshot fields, draw/UI boundaries, and decision meanings."
    }
    else {
        Write-FailHealth "Map footprints docs must explain display-only recording, runtime snapshot fields, draw/UI no-JSON boundaries, input handoff, and decision meanings."
    }
}

function Test-DiagnosticLifecycleGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $diagnosticRulesPath = Join-Path $RepoRoot "文档\项目规则\AI诊断日志说明.md"
    $engineeringRulesPath = Join-Path $RepoRoot "文档\项目规则\工程规则.md"
    $testRulesPath = Join-Path $RepoRoot "文档\项目规则\AI测试规则.md"
    $plan05Path = Join-Path $RepoRoot "文档\归档历史计划\诊断日志轻量化治理\05-审计与文档同步.md"
    $mapMarkerFeatureDocPath = Join-Path $RepoRoot "文档\功能介绍\地图加强页\地图标记.md"
    $safeLandingFeatureDocPath = Join-Path $RepoRoot "文档\功能介绍\移动页\智能防摔.md"
    $mapTraceRecorderPath = Join-Path $RepoRoot "src\JueMingZ\Diagnostics\PlayerWorldMapMarkerTraceRecorder.cs"
    $appSettingsPath = Join-Path $RepoRoot "src\JueMingZ\Config\AppSettings.cs"
    $snapshotPath = Join-Path $RepoRoot "src\JueMingZ\Diagnostics\DiagnosticSnapshot.cs"
    $snapshotWriterPath = Join-Path $RepoRoot "src\JueMingZ\Diagnostics\DiagnosticSnapshotWriter.Json.cs"
    $mapSnapshotBuilderPath = Join-Path $RepoRoot "src\JueMingZ\Runtime\Diagnostics\RuntimeDiagnosticSnapshotBuilder.Bootstrap.cs"
    $movementSnapshotBuilderPath = Join-Path $RepoRoot "src\JueMingZ\Runtime\Diagnostics\RuntimeDiagnosticSnapshotBuilder.Movement.cs"
    $mapMarkerTestsPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.PlayerWorldMapMarkerTests.cs"
    $runtimeTestsPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.RuntimeDiagnosticsAndDispatchTests.cs"

    $diagnosticRulesText = Read-TextIfExists -Path $diagnosticRulesPath
    $engineeringRulesText = Read-TextIfExists -Path $engineeringRulesPath
    $testRulesText = Read-TextIfExists -Path $testRulesPath
    $plan05Text = Read-TextIfExists -Path $plan05Path
    $mapMarkerFeatureDocText = Read-TextIfExists -Path $mapMarkerFeatureDocPath
    $safeLandingFeatureDocText = Read-TextIfExists -Path $safeLandingFeatureDocPath
    $mapTraceRecorderText = Read-TextIfExists -Path $mapTraceRecorderPath
    $appSettingsText = Read-TextIfExists -Path $appSettingsPath
    $snapshotText = Read-TextIfExists -Path $snapshotPath
    $snapshotWriterText = Read-TextIfExists -Path $snapshotWriterPath
    $mapSnapshotBuilderText = Read-TextIfExists -Path $mapSnapshotBuilderPath
    $movementSnapshotBuilderText = Read-TextIfExists -Path $movementSnapshotBuilderPath
    $mapMarkerTestsText = Read-TextIfExists -Path $mapMarkerTestsPath
    $runtimeTestsText = Read-TextIfExists -Path $runtimeTestsPath

    if ($null -eq $diagnosticRulesText -or $null -eq $engineeringRulesText -or $null -eq $testRulesText -or
        $null -eq $plan05Text -or $null -eq $mapMarkerFeatureDocText -or $null -eq $safeLandingFeatureDocText -or
        $null -eq $mapTraceRecorderText -or $null -eq $appSettingsText -or $null -eq $snapshotText -or
        $null -eq $snapshotWriterText -or $null -eq $mapSnapshotBuilderText -or $null -eq $movementSnapshotBuilderText -or
        $null -eq $mapMarkerTestsText -or $null -eq $runtimeTestsText) {
        Write-FailHealth "Diagnostic lifecycle governance needs rules, involved feature docs, map trace recorder, snapshot writer/builders, and regression tests."
        return
    }

    $requiredLifecycleTokens = @(
        "DiagnosticLifecycle",
        "DiagnosticOutputLayer",
        "DiagnosticExitCondition",
        "DiagnosticEnableScope",
        "DiagnosticReplacement",
        "DiagnosticRecoveryCondition",
        "SnapshotCore",
        "SnapshotModuleSummary",
        "ActionEvent",
        "PerformanceEvent",
        "ExplicitDetail",
        "TraceJsonl",
        "ArchivedDoc"
    )
    $missingLifecycleTokens = @()
    foreach ($token in $requiredLifecycleTokens) {
        if (-not $diagnosticRulesText.Contains($token)) {
            $missingLifecycleTokens += $token
        }
    }

    if ($missingLifecycleTokens.Count -eq 0 -and
        $testRulesText.Contains("临时字段永久存在") -and
        $testRulesText.Contains("归档字段") -and
        $plan05Text.Contains("正常水平契约") -and
        $plan05Text.Contains("Archived")) {
        Write-Pass "Diagnostic lifecycle and output-layer rules stay documented for health audit and tests."
    }
    else {
        Write-FailHealth "Diagnostic lifecycle rules must keep lifecycle/output-layer anchors and test guidance; missing=$($missingLifecycleTokens -join ', ')"
    }

    if ($engineeringRulesText.Contains("Draw / hit-test / layout / tooltip") -and
        $engineeringRulesText.Contains("不得为了填诊断字段主动刷新世界") -and
        $engineeringRulesText.Contains("模块 trace JSONL") -and
        $engineeringRulesText.Contains('字段存在类审计只适用于仍属 `Core` 或明确处于 `Stabilization` 的字段')) {
        Write-Pass "Diagnostic high-frequency guardrails stay anchored in engineering rules."
    }
    else {
        Write-FailHealth "Engineering rules must keep diagnostic high-frequency guardrails for draw/hit-test/layout/tooltip, trace JSONL, and scoped field-existence audits."
    }

    $traceFields = @(
        "MapMarkerTraceEventsPath",
        "MapMarkerLastTraceEventWrittenAtUtc",
        "MapMarkerLastTraceEventType",
        "MapMarkerLastTraceMarkerId"
    )
    $missingTraceFields = @()
    foreach ($field in $traceFields) {
        if (-not $snapshotText.Contains($field) -or
            -not $snapshotWriterText.Contains($field) -or
            -not $mapSnapshotBuilderText.Contains($field)) {
            $missingTraceFields += $field
        }
    }

    if ($missingTraceFields.Count -eq 0 -and
        $appSettingsText.Contains("EnableTraceLog = false") -and
        $mapTraceRecorderText.Contains("TraceEventsPathForSnapshot") -and
        $mapTraceRecorderText.Contains("TraceRecordingEnabled ? TraceEventsPath : string.Empty") -and
        $mapTraceRecorderText.Contains("settings.EnableTraceLog") -and
        $mapTraceRecorderText.Contains("if (!TraceRecordingEnabled)") -and
        $mapMarkerTestsText.Contains("PlayerWorldMapMarkerTraceRecorderRequiresTraceLog") -and
        $mapMarkerFeatureDocText.Contains("DiagnosticLifecycle=ActiveInvestigation") -and
        $mapMarkerFeatureDocText.Contains("DiagnosticEnableScope=AppSettings.EnableTraceLog=true") -and
        $diagnosticRulesText.Contains("DiagnosticLifecycle=ActiveInvestigation") -and
        $diagnosticRulesText.Contains("DiagnosticOutputLayer=TraceJsonl") -and
        $diagnosticRulesText.Contains("DiagnosticRecoveryCondition") -and
        $diagnosticRulesText.Contains('常规回传先看 `PlayerWorldMapMarkers*`')) {
        Write-Pass "Map marker coordinate trace is audited as an explicit ActiveInvestigation trace, not as permanent core snapshot output."
    }
    else {
        Write-FailHealth "Map marker coordinate trace must keep EnableTraceLog gating, trace fields/writer wiring, tests, and ActiveInvestigation docs. Missing fields=$($missingTraceFields -join ', ')"
    }

    $archivedSafeLandingFields = @(
        "MovementSafeLandingHasCushionBlock",
        "MovementSafeLandingCushionBlock",
        "MovementSafeLandingBlockPlacement"
    )
    $archivedRuntimeLeaks = @()
    foreach ($field in $archivedSafeLandingFields) {
        if ($snapshotText.Contains($field) -or
            $snapshotWriterText.Contains($field) -or
            $movementSnapshotBuilderText.Contains($field)) {
            $archivedRuntimeLeaks += $field
        }
    }

    if ($archivedRuntimeLeaks.Count -eq 0 -and
        $runtimeTestsText.Contains('AssertDoesNotContain(json, "MovementSafeLandingHasCushionBlock")') -and
        $runtimeTestsText.Contains('AssertDoesNotContain(json, "MovementSafeLandingCushionBlock")') -and
        $runtimeTestsText.Contains('AssertDoesNotContain(json, "MovementSafeLandingBlockPlacement")') -and
        $diagnosticRulesText.Contains("DiagnosticLifecycle=Archived") -and
        $diagnosticRulesText.Contains("DiagnosticOutputLayer=ArchivedDoc") -and
        $diagnosticRulesText.Contains("MovementSafeLandingSelectedStrategyId") -and
        $diagnosticRulesText.Contains("Movement.SafeLanding") -and
        $safeLandingFeatureDocText.Contains("当前已归档") -and
        $safeLandingFeatureDocText.Contains("传送杖目标字段")) {
        Write-Pass "SafeLanding cushion-block legacy snapshot fields stay archived with replacement docs and JSON absence tests."
    }
    else {
        Write-FailHealth "SafeLanding cushion-block fields must stay out of current snapshot JSON while docs explain replacement and recovery. Leaks=$($archivedRuntimeLeaks -join ', ')"
    }

    if ($diagnosticRulesText.Contains('`Ui.Notes.InputTrace`') -and
        $diagnosticRulesText.Contains('蓝图 `BlueprintDiagnostics*`') -and
        $diagnosticRulesText.Contains('`MapFootprintsPlayback*`') -and
        $diagnosticRulesText.Contains("DiagnosticLifecycle=Stabilization") -and
        $diagnosticRulesText.Contains("不得在用户仍未实机确认时提前归档") -and
        $diagnosticRulesText.Contains("不作为 05 的下线目标")) {
        Write-Pass "Notes, blueprint, and footprint diagnostics keep Stabilization scope instead of being prematurely archived."
    }
    else {
        Write-FailHealth "Diagnostic rules must state Ui.Notes.InputTrace, blueprint diagnostics, and MapFootprintsPlayback* remain Stabilization and are not archived by this audit-sync stage."
    }
}

function Test-ActionQueueDirectEnqueueGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $srcRoot = Join-Path $RepoRoot "src\JueMingZ"
    if (-not (Test-Path -LiteralPath $srcRoot)) {
        Write-FailHealth "JueMingZ source root missing while auditing ActionQueue direct enqueue governance."
        return
    }

    $expectedExceptionCounts = @{}
    $expectedDirectCallCounts = @{}

    $tagCounts = @{}
    $directCallCounts = @{}
    $unexpectedStaticCalls = @()
    $files = Get-ChildItem -LiteralPath $srcRoot -Recurse -Filter "*.cs" -File
    foreach ($file in $files) {
        $relative = $file.FullName.Substring($RepoRoot.Length).TrimStart('\', '/').Replace('\', '/')
        $text = Read-TextIfExists -Path $file.FullName
        if ($null -eq $text) {
            continue
        }

        $tagCount = [System.Text.RegularExpressions.Regex]::Matches($text, 'ACTION_QUEUE_DIRECT_ENQUEUE_EXCEPTION').Count
        if ($tagCount -gt 0) {
            $tagCounts[$relative] = $tagCount
        }

        if ($relative -eq "src/JueMingZ/Actions/InputActionQueue.cs") {
            continue
        }

        $hasInputQueueParameter = [System.Text.RegularExpressions.Regex]::IsMatch($text, '\bInputActionQueue\s+queue\b')
        $lines = $text -split "\r?\n"
        for ($index = 0; $index -lt $lines.Count; $index++) {
            $line = $lines[$index]
            $lineNumber = $index + 1
            if ([System.Text.RegularExpressions.Regex]::IsMatch($line, '\bActionQueue\.Enqueue\s*\(')) {
                $unexpectedStaticCalls += "${relative}:$lineNumber"
            }

            if ($hasInputQueueParameter -and
                [System.Text.RegularExpressions.Regex]::IsMatch($line, '\bqueue\.Enqueue\s*\(')) {
                if (-not $directCallCounts.ContainsKey($relative)) {
                    $directCallCounts[$relative] = 0
                }

                $directCallCounts[$relative]++
            }
        }
    }

    $unexpectedTags = @()
    foreach ($path in $tagCounts.Keys) {
        if (-not $expectedExceptionCounts.ContainsKey($path) -or
            $tagCounts[$path] -ne $expectedExceptionCounts[$path]) {
            $unexpectedTags += "$path=$($tagCounts[$path])"
        }
    }

    foreach ($path in $expectedExceptionCounts.Keys) {
        $actual = 0
        if ($tagCounts.ContainsKey($path)) {
            $actual = $tagCounts[$path]
        }

        if ($actual -ne $expectedExceptionCounts[$path]) {
            $unexpectedTags += "$path=$actual expected=$($expectedExceptionCounts[$path])"
        }
    }

    if ($unexpectedTags.Count -gt 0) {
        Write-FailHealth "ACTION_QUEUE_DIRECT_ENQUEUE_EXCEPTION comments are no longer allowed in production source: $($unexpectedTags -join ', ')"
    }
    else {
        Write-Pass "No ActionQueue direct enqueue exception comments remain in production source."
    }

    $unexpectedDirectCalls = @()
    foreach ($path in $directCallCounts.Keys) {
        if (-not $expectedDirectCallCounts.ContainsKey($path) -or
            $directCallCounts[$path] -ne $expectedDirectCallCounts[$path]) {
            $unexpectedDirectCalls += "$path=$($directCallCounts[$path])"
        }
    }

    foreach ($path in $expectedDirectCallCounts.Keys) {
        $actual = 0
        if ($directCallCounts.ContainsKey($path)) {
            $actual = $directCallCounts[$path]
        }

        if ($actual -ne $expectedDirectCallCounts[$path]) {
            $unexpectedDirectCalls += "$path=$actual expected=$($expectedDirectCallCounts[$path])"
        }
    }

    if ($unexpectedStaticCalls.Count -gt 0) {
        Write-FailHealth "Runtime/static ActionQueue.Enqueue calls must migrate to TryEnqueue or be explicitly evaluated: $($unexpectedStaticCalls -join ', ')"
    }
    else {
        Write-Pass "No Runtime/static ActionQueue.Enqueue calls bypass admission."
    }

    if ($unexpectedDirectCalls.Count -gt 0) {
        Write-FailHealth "InputActionQueue direct Enqueue calls are no longer allowed in production source: $($unexpectedDirectCalls -join ', ')"
    }
    else {
        Write-Pass "No InputActionQueue direct Enqueue calls bypass admission in production source."
    }
}

function Test-NewFeatureBoundaryGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $docs = @(
        @{
            Name = "AGENTS.md"
            Path = Join-Path $RepoRoot "AGENTS.md"
            Required = @(
                "职责边界底线",
                "新功能必须按职责 / 功能边界",
                "FeatureDefinition",
                "公共 Runtime / Compat / 巨型 Service"
            )
        },
        @{
            Name = "冷启动说明.md"
            Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("冷启动说明.md")
            Required = @(
                "职责边界：新功能必须按职责 / 功能边界",
                "公共 Runtime、Compat、巨型 Service"
            )
        },
        @{
            Name = "工程规则.md"
            Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "工程规则.md")
            Required = @(
                "职责边界底线",
                "新功能业务主体必须落在自己的 service",
                "JueMingZRuntime",
                "TerrariaInputCompat",
                "MovementSafeLandingService"
            )
        },
        @{
            Name = "AI测试规则.md"
            Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "AI测试规则.md")
            Required = @(
                "后续新增功能不得把业务判断塞进",
                "TerrariaInputCompat 拆分回归关注",
                "MovementSafeLandingCompat 拆分回归关注",
                "MovementSafeLandingService 拆分回归关注"
            )
        }
    )

    foreach ($doc in $docs) {
        $text = Read-TextIfExists -Path $doc.Path
        if ($null -eq $text) {
            Write-FailHealth "New feature boundary document missing: $($doc.Name)"
            continue
        }

        $missing = @()
        foreach ($required in $doc.Required) {
            if (-not $text.Contains($required)) {
                $missing += $required
            }
        }

        if ($missing.Count -gt 0) {
            Write-FailHealth "New feature boundary document $($doc.Name) lacks required anchor(s): $($missing -join ', ')"
        }
        else {
            Write-Pass "New feature boundary document $($doc.Name) keeps the required anchors."
        }
    }

    $srcRoot = Join-Path $RepoRoot "src\JueMingZ"
    if (-not (Test-Path -LiteralPath $srcRoot)) {
        Write-FailHealth "JueMingZ source root missing while auditing new feature boundary governance."
        return
    }

    $featureRegistrationLeaks = @()
    foreach ($file in Get-ChildItem -LiteralPath $srcRoot -Recurse -Filter "*.cs" -File) {
        $relative = $file.FullName.Substring($RepoRoot.Length).TrimStart('\', '/').Replace('\', '/')
        $text = Read-TextIfExists -Path $file.FullName
        if ($null -eq $text -or -not $text.Contains("FeatureDefinitionBuilder.Create")) {
            continue
        }

        if (-not ($relative -like "src/JueMingZ/Features/Catalog/*")) {
            $featureRegistrationLeaks += $relative
        }
    }

    if ($featureRegistrationLeaks.Count -gt 0) {
        Write-FailHealth "FeatureDefinitionBuilder.Create must stay in feature catalog registrars: $($featureRegistrationLeaks -join ', ')"
    }
    else {
        Write-Pass "Feature registration remains isolated to feature catalog registrars."
    }

    $requiredSourceFiles = @(
        "src\JueMingZ\Runtime\RuntimeServiceScheduler.cs",
        "src\JueMingZ\Compat\TerrariaInputCompat.Selection.cs",
        "src\JueMingZ\Compat\TerrariaInputCompat.MouseTarget.cs",
        "src\JueMingZ\Compat\TerrariaInputCompat.UseItem.cs",
        "src\JueMingZ\Compat\TerrariaInputCompat.Movement.cs",
        "src\JueMingZ\Compat\TerrariaInputCompat.UiInput.cs",
        "src\JueMingZ\Compat\TerrariaInputCompat.TileInteraction.cs",
        "src\JueMingZ\Compat\TerrariaInputCompat.Reflection.cs",
        "src\JueMingZ\Compat\MovementSafeLandingCompat.Analysis.cs",
        "src\JueMingZ\Compat\MovementSafeLandingCompat.AnalysisHelpers.cs",
        "src\JueMingZ\Compat\MovementSafeLandingCompat.LandingProbe.cs",
        "src\JueMingZ\Compat\MovementSafeLandingCompat.Reflection.cs",
        "src\JueMingZ\Automation\Movement\MovementSafeLandingService.Diagnostics.cs",
        "src\JueMingZ\Automation\Movement\MovementSafeLandingService.Requests.cs",
        "src\JueMingZ\Automation\Movement\MovementSafeLandingService.Recovery.cs",
        "src\JueMingZ\Automation\Movement\MovementSafeLandingService.DescentGuard.cs",
        "tests\JueMingZ.Tests\Program.InputActionQueueTests.cs"
    )

    $missingSourceFiles = @()
    foreach ($relative in $requiredSourceFiles) {
        $path = Join-Path $RepoRoot $relative
        if (-not (Test-Path -LiteralPath $path)) {
            $missingSourceFiles += $relative
        }
    }

    if ($missingSourceFiles.Count -gt 0) {
        Write-FailHealth "Expected boundary-split source files are missing: $($missingSourceFiles -join ', ')"
    }
    else {
        Write-Pass "Boundary-split source files for Runtime, Compat, SafeLanding, and queue tests exist."
    }
}

function Test-DeepStructureBoundaryGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    function Get-DeepStructureLineCount {
        param([Parameter(Mandatory = $true)][string]$Text)
        return @($Text -split "\r?\n").Count
    }

    function Test-DeepStructureAnchors {
        param(
            [Parameter(Mandatory = $true)][string]$RepoRoot,
            [Parameter(Mandatory = $true)]$Spec
        )

        $path = Join-Path $RepoRoot $Spec.Path
        $text = Read-TextIfExists -Path $path
        if ($null -eq $text) {
            Write-FailHealth "Deep structure boundary file missing: $($Spec.Path)"
            return
        }

        $missing = @()
        foreach ($anchor in $Spec.Required) {
            if (-not $text.Contains($anchor)) {
                $missing += $anchor
            }
        }

        if ($missing.Count -gt 0) {
            Write-FailHealth "Deep structure boundary anchors missing from $($Spec.Path): $($missing -join ', ')"
        }
        else {
            Write-Pass "Deep structure boundary anchors remain in $($Spec.Path)."
        }
    }

    function Test-DeepStructureForbiddenPatterns {
        param(
            [Parameter(Mandatory = $true)][string]$RepoRoot,
            [Parameter(Mandatory = $true)]$Spec
        )

        $path = Join-Path $RepoRoot $Spec.Path
        $text = Read-TextIfExists -Path $path
        if ($null -eq $text) {
            Write-FailHealth "Deep structure boundary file missing for forbidden-pattern audit: $($Spec.Path)"
            return
        }

        $matches = @()
        foreach ($pattern in $Spec.Patterns) {
            if ([System.Text.RegularExpressions.Regex]::IsMatch($text, $pattern)) {
                $matches += $pattern
            }
        }

        if ($matches.Count -gt 0) {
            Write-FailHealth "Deep structure old pit appears to own split responsibility again in $($Spec.Path): $($matches -join ', ')"
        }
        else {
            Write-Pass "Deep structure old pit remains thin in $($Spec.Path)."
        }
    }

    $docAnchors = @(
        @{
            Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "工程规则.md")
            Name = "工程规则.md"
            Required = @(
                "结构回流健康审计",
                "旧深坑主文件",
                "Runtime diagnostics snapshot builder",
                "旧综合测试入口只保留导航注释"
            )
        },
        @{
            Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "AI测试规则.md")
            Name = "AI测试规则.md"
            Required = @(
                "结构回流健康审计",
                "旧综合测试入口只保留导航注释",
                "InputActionQueueTests",
                "超过约 1000 行"
            )
        }
    )

    foreach ($doc in $docAnchors) {
        $text = Read-TextIfExists -Path $doc.Path
        if ($null -eq $text) {
            Write-FailHealth "Deep structure governance document missing: $($doc.Name)"
            continue
        }

        $missing = @()
        foreach ($required in $doc.Required) {
            if (-not $text.Contains($required)) {
                $missing += $required
            }
        }

        if ($missing.Count -gt 0) {
            Write-FailHealth "Deep structure governance document $($doc.Name) lacks required anchor(s): $($missing -join ', ')"
        }
        else {
            Write-Pass "Deep structure governance document $($doc.Name) keeps required anchors."
        }
    }

    $requiredFiles = @(
        "src\JueMingZ\Runtime\JueMingZRuntime.cs",
        "src\JueMingZ\Runtime\RuntimeAutomationDispatcher.cs",
        "src\JueMingZ\Runtime\RuntimeDispatchStep.cs",
        "src\JueMingZ\Runtime\RuntimeGameStateReadOptionsBuilder.cs",
        "src\JueMingZ\Runtime\RuntimeStartupDiagnosticNoop.cs",
        "src\JueMingZ\Runtime\Diagnostics\RuntimeDiagnosticSnapshotBuilder.cs",
        "src\JueMingZ\Runtime\Diagnostics\RuntimeDiagnosticSnapshotBuilder.Bootstrap.cs",
        "src\JueMingZ\Runtime\Diagnostics\RuntimeDiagnosticSnapshotBuilder.ActionQueue.cs",
        "src\JueMingZ\Runtime\Diagnostics\RuntimeDiagnosticSnapshotBuilder.DiagnosticUi.cs",
        "src\JueMingZ\Runtime\Diagnostics\RuntimeDiagnosticSnapshotBuilder.Performance.cs",
        "src\JueMingZ\Runtime\Diagnostics\RuntimeDiagnosticSnapshotBuilder.InventoryInformationFishing.cs",
        "src\JueMingZ\Runtime\Diagnostics\RuntimeDiagnosticSnapshotBuilder.Movement.cs",
        "src\JueMingZ\Runtime\Diagnostics\RuntimeDiagnosticSnapshotBuilder.CombatRecovery.cs",
        "src\JueMingZ\Actions\InputActionQueue.cs",
        "src\JueMingZ\Actions\InputActionQueueModels.cs",
        "src\JueMingZ\Actions\InputActionQueue.Admission.cs",
        "src\JueMingZ\Actions\InputActionQueue.CleanupLease.cs",
        "src\JueMingZ\Actions\InputActionQueue.Scheduler.cs",
        "src\JueMingZ\Actions\InputActionQueue.Execution.cs",
        "src\JueMingZ\Actions\InputActionQueue.Snapshot.cs",
        "src\JueMingZ\Actions\InputActionResultStore.cs",
        "src\JueMingZ\Compat\TerrariaInputCompat.UseItem.cs",
        "src\JueMingZ\Compat\TerrariaInputCompat.UseItem.Scope.cs",
        "src\JueMingZ\Compat\TerrariaInputCompat.UseItem.PhysicalInput.cs",
        "src\JueMingZ\Compat\TerrariaInputCompat.UseItem.Read.cs",
        "src\JueMingZ\Compat\TerrariaInputCompat.UseItem.UiContext.cs",
        "src\JueMingZ\Compat\TerrariaInputCompat.UseItem.ItemCheckBridge.cs",
        "src\JueMingZ\Compat\TerrariaInputCompat.Movement.cs",
        "src\JueMingZ\Compat\TerrariaInputCompat.Movement.Direction.cs",
        "src\JueMingZ\Compat\TerrariaInputCompat.Movement.Read.cs",
        "src\JueMingZ\Compat\TerrariaInputCompat.Movement.JumpProfile.cs",
        "src\JueMingZ\Compat\TerrariaInputCompat.Movement.Emitters.cs",
        "src\JueMingZ\Compat\TerrariaInputCompat.Movement.Capabilities.cs",
        "src\JueMingZ\Compat\TerrariaInputCompat.Movement.AutoFacing.cs",
        "src\JueMingZ\Automation\Movement\MovementSafeLandingService.Recovery.cs",
        "src\JueMingZ\Automation\Movement\MovementSafeLandingService.Recovery.ResultRouter.cs",
        "src\JueMingZ\Automation\Movement\MovementSafeLandingService.Recovery.DisabledCleanup.cs",
        "src\JueMingZ\Automation\Movement\MovementSafeLandingService.Recovery.TemporaryEquipment.cs",
        "src\JueMingZ\Automation\Movement\MovementSafeLandingService.Recovery.MountCancel.cs",
        "src\JueMingZ\Automation\Movement\MovementSafeLandingService.Recovery.GravityRestore.cs",
        "src\JueMingZ\Automation\Movement\MovementSafeLandingService.Recovery.Shared.cs",
        "src\JueMingZ\Compat\MovementSafeLandingEquipmentCompat.cs",
        "src\JueMingZ\Compat\MovementSafeLandingEquipmentCompat.Catalog.cs",
        "src\JueMingZ\Compat\MovementSafeLandingEquipmentCompat.Scan.cs",
        "src\JueMingZ\Compat\MovementSafeLandingEquipmentCompat.PlanBuilder.cs",
        "src\JueMingZ\Compat\MovementSafeLandingEquipmentCompat.CapabilityRead.cs",
        "src\JueMingZ\Compat\MovementSafeLandingEquipmentCompat.Store.cs",
        "src\JueMingZ\Compat\MovementSafeLandingEquipmentCompat.Apply.cs",
        "src\JueMingZ\Compat\MovementSafeLandingEquipmentCompat.FunctionalRefresh.cs",
        "src\JueMingZ\Compat\MovementSafeLandingEquipmentCompat.Restore.cs",
        "src\JueMingZ\Compat\MovementSafeLandingEquipmentCompat.RestoreSafety.cs",
        "src\JueMingZ\Compat\MovementSafeLandingEquipmentCompat.Reflection.cs",
        "src\JueMingZ\Compat\MovementSafeLandingEquipmentCompat.Result.cs",
        "src\JueMingZ\Input\LegacyUiActionService.cs",
        "src\JueMingZ\Input\LegacyUiActionService.CommandRouter.cs",
        "src\JueMingZ\Input\LegacyUiActionService.GeneralDiagnostics.cs",
        "src\JueMingZ\Input\LegacyUiActionService.GeneralSettings.cs",
        "src\JueMingZ\Input\LegacyUiActionService.WorldAutomationHandlers.cs",
        "src\JueMingZ\Input\LegacyUiActionService.InventoryListHandlers.cs",
        "src\JueMingZ\Input\LegacyUiActionService.InventoryQuickItemHandlers.cs",
        "src\JueMingZ\Input\LegacyUiActionService.NpcQuickReforgeHandlers.cs",
        "src\JueMingZ\Input\LegacyUiActionService.AutoRecoveryHandlers.cs",
        "src\JueMingZ\Input\LegacyUiActionService.CombatHandlers.cs",
        "src\JueMingZ\Input\LegacyUiActionService.FishingHandlers.cs",
        "src\JueMingZ\Input\LegacyUiActionService.MovementHandlers.cs",
        "tests\JueMingZ.Tests\Program.CombatAndQueueTests.cs",
        "tests\JueMingZ.Tests\Program.CombatAutoClickerTests.cs",
        "tests\JueMingZ.Tests\Program.CombatPhasebladeQuickSwitchTests.cs",
        "tests\JueMingZ.Tests\Program.CombatFlailComboTests.cs",
        "tests\JueMingZ.Tests\Program.ItemCheckWriterArbiterTests.cs",
        "tests\JueMingZ.Tests\Program.CombatAimProjectileProfileTests.cs",
        "tests\JueMingZ.Tests\Program.CombatAimMotionSolverTests.cs",
        "tests\JueMingZ.Tests\Program.CombatAimCursorPolicyTests.cs",
        "tests\JueMingZ.Tests\Program.CombatAimFlailControlTests.cs",
        "tests\JueMingZ.Tests\Program.CombatAimFlailReleaseTailTests.cs",
        "tests\JueMingZ.Tests\Program.CombatAimSpecialProjectileTests.cs",
        "tests\JueMingZ.Tests\Program.CombatAimReleaseHoldTests.cs",
        "tests\JueMingZ.Tests\Program.CombatAimSharedHelpers.cs",
        "tests\JueMingZ.Tests\Program.ActionCatalogTests.cs",
        "tests\JueMingZ.Tests\Program.ActionChannelResolverTests.cs",
        "tests\JueMingZ.Tests\Program.InventoryAutomationActionTests.cs",
        "tests\JueMingZ.Tests\Program.WorldAutomationActionTests.cs",
        "tests\JueMingZ.Tests\Program.RuntimeDiagnosticsAndDispatchTests.cs",
        "tests\JueMingZ.Tests\Program.FeatureCatalogTests.cs",
        "tests\JueMingZ.Tests\Program.AppSettingsDefaultTests.cs",
        "tests\JueMingZ.Tests\Program.ConfigIsolationTests.cs",
        "tests\JueMingZ.Tests\Program.ActionCatalogSharedHelpers.cs",
        "tests\JueMingZ.Tests\Program.SafeLandingTests.cs",
        "tests\JueMingZ.Tests\Program.SafeLandingEquipmentTests.cs",
        "tests\JueMingZ.Tests\Program.SafeLandingAnalysisAndStrategyTests.cs",
        "tests\JueMingZ.Tests\Program.TravelMenuTests.cs",
        "tests\JueMingZ.Tests\Program.FishingInformationUiTests.cs",
        "tests\JueMingZ.Tests\Program.FishingAutomationTests.cs",
        "tests\JueMingZ.Tests\Program.InformationOverlayUiTests.cs",
        "tests\JueMingZ.Tests\Program.InformationFishingCatchTests.cs",
        "tests\JueMingZ.Tests\Program.InformationChestLabelTests.cs",
        "tests\JueMingZ.Tests\Program.LegacyUiInteractionTests.cs"
    )

    $missingFiles = @()
    foreach ($relative in $requiredFiles) {
        if (-not (Test-Path -LiteralPath (Join-Path $RepoRoot $relative))) {
            $missingFiles += $relative
        }
    }

    if ($missingFiles.Count -gt 0) {
        Write-FailHealth "Deep structure split boundary file(s) missing: $($missingFiles -join ', ')"
    }
    else {
        Write-Pass "Deep structure split boundary files exist."
    }

    $generalHandlersPath = Join-Path $RepoRoot "src\JueMingZ\Input\LegacyUiActionService.GeneralHandlers.cs"
    if (Test-Path -LiteralPath $generalHandlersPath) {
        Write-FailHealth "LegacyUiActionService.GeneralHandlers.cs must not be recreated as a catch-all handler."
    }
    else {
        Write-Pass "LegacyUiActionService.GeneralHandlers.cs remains deleted."
    }

    $anchorSpecs = @(
        @{
            Path = "src\JueMingZ\Runtime\JueMingZRuntime.cs"
            Required = @("RuntimeDiagnosticSnapshotBuilder.Build", "RuntimeAutomationDispatcher.DispatchAutomationRequests", "RuntimeGameStateReadOptionsBuilder.Build")
        },
        @{
            Path = "src\JueMingZ\Runtime\Diagnostics\RuntimeDiagnosticSnapshotBuilder.cs"
            Required = @("public static DiagnosticSnapshot Build", "WriteActionQueue", "WritePerformanceAndConfig", "WriteInventoryInformationFishing", "WriteCombatAndRecovery")
        },
        @{
            Path = "src\JueMingZ\Runtime\RuntimeAutomationDispatcher.cs"
            Required = @("RunTargetingAndUiActions", "DispatchAutomationRequests", "ShouldDispatchFishingAutomation", "RuntimeStartupDiagnosticNoop.QueueIfReady")
        },
        @{
            Path = "src\JueMingZ\Actions\InputActionQueue.Admission.cs"
            Required = @("BuildAdmissionLocked", "ApplyAcceptedAdmissionLocked", "BuildPendingConflictSummaryLocked", "TryFindPreemptableBackgroundPendingLocked")
        },
        @{
            Path = "src\JueMingZ\Actions\InputActionQueue.Execution.cs"
            Required = @("TryStartNextActionLocked", "UpdateRunningActionLocked", "CancelRunningLocked", "RecordResultLocked")
        },
        @{
            Path = "src\JueMingZ\Actions\InputActionQueue.Scheduler.cs"
            Required = @("SelectNextStartableActionLocked", "ExpirePendingLocked", "IsExpiredBeforeStart")
        },
        @{
            Path = "src\JueMingZ\Compat\TerrariaInputCompat.UseItem.Scope.cs"
            Required = @("ScopedUseItemTakeover", "TryBeginScopedUseItemTakeover", "TryRestoreScopedUseItemTakeover")
        },
        @{
            Path = "src\JueMingZ\Compat\TerrariaInputCompat.UseItem.ItemCheckBridge.cs"
            Required = @("TryCaptureUseItemInputState", "TryApplyUseItemOverrideForItemCheck", "TryRestoreUseItemInputState", "TryApplyPhasebladeQuickSwitchForItemCheck")
        },
        @{
            Path = "src\JueMingZ\Compat\TerrariaInputCompat.Movement.Emitters.cs"
            Required = @("TryPrimeJumpReleaseForNextTick", "TryReleaseSafeLandingControlInputs", "TrySetNamedControlInput")
        },
        @{
            Path = "src\JueMingZ\Compat\TerrariaInputCompat.Movement.JumpProfile.cs"
            Required = @("TryReadJumpInputProfile", "ReadMountJumpProfile", "ReadEquippedMovementAssistProfile")
        },
        @{
            Path = "src\JueMingZ\Compat\TerrariaInputCompat.Movement.Capabilities.cs"
            Required = @("ReadMountJumpProfile", "ReadEquippedMovementAssistProfile", "TryResolveMountNoFallDamage")
        },
        @{
            Path = "src\JueMingZ\Automation\Movement\MovementSafeLandingService.Recovery.ResultRouter.cs"
            Required = @("RouteSafeLandingActionQueueResult", "TemporaryEquipmentApplyCompleted", "TemporaryEquipmentRestoreCompleted")
        },
        @{
            Path = "src\JueMingZ\Automation\Movement\MovementSafeLandingService.Recovery.TemporaryEquipment.cs"
            Required = @("HandleTemporaryEquipmentRestore", "TryHandleTemporaryEquipmentActivation", "TryEnqueueTemporaryEquipmentRestore")
        },
        @{
            Path = "src\JueMingZ\Compat\MovementSafeLandingEquipmentCompat.PlanBuilder.cs"
            Required = @("TryBuildTemporaryEquipmentPlan", "TryBuildCushionBlockHotbarPlan")
        },
        @{
            Path = "src\JueMingZ\Compat\MovementSafeLandingEquipmentCompat.Store.cs"
            Required = @("RegisterApplyPlan", "TryApplyRegisteredPlan", "TryRestoreRegisteredRecords", "TryTakeRestoreResult")
        },
        @{
            Path = "src\JueMingZ\Input\LegacyUiActionService.CommandRouter.cs"
            Required = @("Element id order is the legacy command protocol", "Domain handlers may update settings", "game-state mutations still belong to Actions/Compat")
        },
        @{
            Path = "tests\JueMingZ.Tests\Program.CombatAndQueueTests.cs"
            Required = @("coverage is split by domain")
        },
        @{
            Path = "tests\JueMingZ.Tests\Program.ActionCatalogTests.cs"
            Required = @("coverage is split into channel resolver")
        },
        @{
            Path = "tests\JueMingZ.Tests\Program.SafeLandingTests.cs"
            Required = @("SafeLanding coverage is split")
        },
        @{
            Path = "tests\JueMingZ.Tests\Program.FishingInformationUiTests.cs"
            Required = @("Fishing, information overlay, chest labels, and legacy UI interaction tests are split")
        },
        @{
            Path = "tests\JueMingZ.Tests\Program.cs"
            Required = @("InstallProcessConfigDirectoryIsolation();", "TestProcessConfigDirectoryIsolatedFromUserDocuments")
        },
        @{
            Path = "tests\JueMingZ.Tests\Program.TestSupport.cs"
            Required = @("User config is real data", "RealUserConfigDirectory", "SetConfigDirectoryForTesting")
        },
        @{
            Path = "tests\JueMingZ.Tests\Program.ConfigIsolationTests.cs"
            Required = @("TestConfigIsolationGuardRejectsRealDocumentsDirectory", "ConfigServiceSaveAllWritesOnlyTestConfigDirectory", "ConfigServiceInitializeWritesOnlyTestConfigDirectory", "LegacyUiFeatureToggleSaveWritesOnlyTestConfigDirectory")
        }
    )

    foreach ($spec in $anchorSpecs) {
        Test-DeepStructureAnchors -RepoRoot $RepoRoot -Spec $spec
    }

    $forbiddenSpecs = @(
        @{
            Path = "src\JueMingZ\Actions\InputActionQueue.cs"
            Patterns = @(
                "private\s+InputActionAdmissionResult\s+BuildAdmissionLocked",
                "private\s+void\s+ApplyAcceptedAdmissionLocked",
                "private\s+InputActionRequest\s+SelectNextStartableActionLocked",
                "private\s+InputActionResult\s+TryStartNextActionLocked",
                "public\s+InputActionQueueSnapshot\s+GetSnapshot"
            )
        },
        @{
            Path = "src\JueMingZ\Compat\TerrariaInputCompat.UseItem.cs"
            Patterns = @("ScopedUseItemTakeover", "TryReadPhysicalUseItemHeld", "TryCaptureUseItemInputState", "TryApplyUseItemOverrideForItemCheck")
        },
        @{
            Path = "src\JueMingZ\Compat\TerrariaInputCompat.Movement.cs"
            Patterns = @("TryReadPlayerDirection", "TrySetControlDown", "TryReadJumpInputProfile", "BeginAutoFacingDirectionOverride")
        },
        @{
            Path = "src\JueMingZ\Automation\Movement\MovementSafeLandingService.Recovery.cs"
            Patterns = @(
                "private\s+static\s+void\s+RouteSafeLandingActionQueueResult",
                "private\s+static\s+bool\s+TryHandleDisabledResidualState",
                "private\s+static\s+void\s+HandleTemporaryEquipmentRestore",
                "private\s+static\s+bool\s+TryHandlePendingSafeLandingMountCancel",
                "private\s+static\s+bool\s+TryHandlePendingSafeLandingGravityRestore"
            )
        },
        @{
            Path = "src\JueMingZ\Compat\MovementSafeLandingEquipmentCompat.cs"
            Patterns = @("CandidateMatchesCategory", "ScanSources", "TryBuildTemporaryEquipmentPlan", "TryApplyRegisteredPlan", "TryRestoreRegisteredRecords", "TryIsSafeToRestoreTemporaryEquipment")
        }
    )

    foreach ($spec in $forbiddenSpecs) {
        Test-DeepStructureForbiddenPatterns -RepoRoot $RepoRoot -Spec $spec
    }

    $thinBudgets = @(
        @{ Path = "src\JueMingZ\Runtime\JueMingZRuntime.cs"; Max = 1000 },
        @{ Path = "src\JueMingZ\Actions\InputActionQueue.cs"; Max = 1000 },
        @{ Path = "src\JueMingZ\Compat\TerrariaInputCompat.UseItem.cs"; Max = 200 },
        @{ Path = "src\JueMingZ\Compat\TerrariaInputCompat.Movement.cs"; Max = 200 },
        @{ Path = "src\JueMingZ\Automation\Movement\MovementSafeLandingService.Recovery.cs"; Max = 200 },
        @{ Path = "src\JueMingZ\Compat\MovementSafeLandingEquipmentCompat.cs"; Max = 200 },
        @{ Path = "src\JueMingZ\Input\LegacyUiActionService.cs"; Max = 1000 },
        @{ Path = "src\JueMingZ\Input\LegacyUiActionService.CommandRouter.cs"; Max = 1000 }
    )

    foreach ($budget in $thinBudgets) {
        $path = Join-Path $RepoRoot $budget.Path
        $text = Read-TextIfExists -Path $path
        if ($null -eq $text) {
            continue
        }

        $lineCount = Get-DeepStructureLineCount -Text $text
        if ($lineCount -gt $budget.Max) {
            Write-FailHealth "Deep structure old pit line budget exceeded: $($budget.Path) has $lineCount line(s), max $($budget.Max)."
        }
        else {
            Write-Pass "Deep structure old pit line budget ok: $($budget.Path) has $lineCount line(s)."
        }
    }

    $navigationTests = @(
        "tests\JueMingZ.Tests\Program.CombatAndQueueTests.cs",
        "tests\JueMingZ.Tests\Program.ActionCatalogTests.cs",
        "tests\JueMingZ.Tests\Program.SafeLandingTests.cs",
        "tests\JueMingZ.Tests\Program.FishingInformationUiTests.cs"
    )

    foreach ($relative in $navigationTests) {
        $path = Join-Path $RepoRoot $relative
        $text = Read-TextIfExists -Path $path
        if ($null -eq $text) {
            continue
        }

        $lineCount = Get-DeepStructureLineCount -Text $text
        $hasMethodBody = [System.Text.RegularExpressions.Regex]::IsMatch(
            $text,
            '\b(private|internal|public)\s+static\s+(?!partial\s+class)[A-Za-z0-9_<>,\[\]\.]+\s+[A-Za-z_][A-Za-z0-9_]*\s*\(')
        if ($lineCount -gt 200 -or $hasMethodBody) {
            Write-FailHealth "Old comprehensive test entry must remain navigation-only: $relative has $lineCount line(s), methodBody=$hasMethodBody."
        }
        else {
            Write-Pass "Old comprehensive test entry remains navigation-only: $relative."
        }
    }

    $largeSingleResponsibilityFiles = @(
        @{ Path = "src\JueMingZ\Input\LegacyUiActionService.FishingFilter.cs"; Reason = "single fishing filter command/picker domain" },
        @{ Path = "tests\JueMingZ.Tests\Program.InputActionQueueTests.cs"; Reason = "single ActionQueue admission/channel contract suite" },
        @{ Path = "tests\JueMingZ.Tests\Program.RuntimeDiagnosticsAndDispatchTests.cs"; Reason = "single Runtime diagnostics/dispatch contract suite" },
        @{ Path = "tests\JueMingZ.Tests\Program.SafeLandingEquipmentTests.cs"; Reason = "single SafeLanding equipment transaction suite" },
        @{ Path = "tests\JueMingZ.Tests\Program.SafeLandingAnalysisAndStrategyTests.cs"; Reason = "single SafeLanding analysis/strategy suite" }
    )

    foreach ($entry in $largeSingleResponsibilityFiles) {
        $path = Join-Path $RepoRoot $entry.Path
        $text = Read-TextIfExists -Path $path
        if ($null -eq $text) {
            continue
        }

        $lineCount = Get-DeepStructureLineCount -Text $text
        if ($lineCount -gt 1000) {
            Write-WarnHealth "Large single-responsibility file remains above line alarm: $($entry.Path) has $lineCount line(s); accepted reason: $($entry.Reason). Split if responsibilities widen."
        }
    }
}

function Test-IterationLogNumbers {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)
    $updatesDir = ConvertFrom-CodePoints @(0x66f4, 0x65b0, 0x8bb0, 0x5f55)
    $indexFile = (ConvertFrom-CodePoints @(0x7d22, 0x5f15)) + ".md"
    $logDir = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @($updatesDir)
    if (-not (Test-Path -LiteralPath $logDir)) {
        Write-WarnHealth "Update record directory missing."
        return
    }

    $numbers = @{}
    foreach ($file in Get-ChildItem -LiteralPath $logDir -Filter "*.md" -File) {
        $match = [System.Text.RegularExpressions.Regex]::Match($file.Name, '^(\d{4})-')
        if ($match.Success) {
            $number = $match.Groups[1].Value
            if (-not $numbers.ContainsKey($number)) {
                $numbers[$number] = @()
            }

            $numbers[$number] += $file.Name
        }
    }

    $knownDuplicateNumbers = @("0005", "0055")
    foreach ($key in ($numbers.Keys | Sort-Object)) {
        if ($numbers[$key].Count -gt 1) {
            if ($knownDuplicateNumbers -contains $key) {
                Write-Pass "Known historical duplicate update-record number ${key} is documented and left untouched."
            }
            else {
                Write-FailHealth "New duplicate update-record number ${key}: $($numbers[$key] -join ', ')"
            }
        }
    }

    if (-not $numbers.ContainsKey("0017")) {
        Write-Pass "Missing update-record number 0017 is a documented historical gap and should not be backfilled by renaming."
    }

    if (Test-Path -LiteralPath (Join-Path $logDir $indexFile)) {
        Write-Pass "Update record index exists."
    }
    else {
        Write-WarnHealth "Update record index missing."
    }
}

function Test-PlayerWorldExplorationGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $servicePath = Join-Path $RepoRoot "src\JueMingZ\Records\PlayerWorldExplorationService.cs"
    $modelPath = Join-Path $RepoRoot "src\JueMingZ\Records\PlayerWorldExplorationModels.cs"
    $playtimePath = Join-Path $RepoRoot "src\JueMingZ\Records\PlayerWorldPlaytimeService.cs"
    $readerPath = Join-Path $RepoRoot "src\JueMingZ\Records\PlayerWorldExplorationMapReader.cs"
    $cachePath = Join-Path $RepoRoot "src\JueMingZ\Records\PlayerWorldExplorationCache.cs"
    $uiPath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyMainWindow.MapEnhancement.cs"
    $uiDetailsPath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyMainWindow.MapEnhancement.RevealedArea.cs"
    $testPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.PlayerWorldExplorationTests.cs"
    $identityTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.PlayerWorldIdentityStoreTests.cs"

    $serviceText = Read-TextIfExists -Path $servicePath
    $modelText = Read-TextIfExists -Path $modelPath
    $playtimeText = Read-TextIfExists -Path $playtimePath
    $readerText = Read-TextIfExists -Path $readerPath
    $cacheText = Read-TextIfExists -Path $cachePath
    $uiMainText = Read-TextIfExists -Path $uiPath
    $uiDetailsText = Read-TextIfExists -Path $uiDetailsPath
    $uiMainForAudit = ""
    $uiDetailsForAudit = ""
    if ($null -ne $uiMainText) {
        $uiMainForAudit = $uiMainText
    }
    if ($null -ne $uiDetailsText) {
        $uiDetailsForAudit = $uiDetailsText
    }
    $uiText = $uiMainForAudit + "`n" + $uiDetailsForAudit
    $testText = Read-TextIfExists -Path $testPath
    $identityTestText = Read-TextIfExists -Path $identityTestPath

    if ($null -eq $serviceText -or $null -eq $modelText -or $null -eq $playtimeText -or $null -eq $readerText -or $null -eq $cacheText -or $null -eq $uiMainText -or $null -eq $uiDetailsText -or $null -eq $testText -or $null -eq $identityTestText) {
        Write-FailHealth "Player-world exploration/playtime service, reader, UI partial, cache, and test files must exist as separate responsibilities."
        return
    }

    if ($serviceText.Contains("Stopwatch.StartNew()") -and
        $serviceText.Contains("GetScanBudgetLocked") -and
        $serviceText.Contains("ShouldApplyPerformanceBackoff") -and
        $modelText.Contains("PerformanceScanTimeBudgetMs") -and
        $modelText.Contains("FastScanTimeBudgetMs") -and
        $modelText.Contains("PerformanceBackoffScanCadenceTicks") -and
        $serviceText.Contains("PlayerWorldFeatureDataStore.TryWriteJson")) {
        Write-Pass "Player-world exploration uses mode-specific Stopwatch budgets, cadence backoff, and throttled summary writes in the service layer."
    }
    else {
        Write-FailHealth "Player-world exploration scanning must keep mode-specific time budgets, backoff cadence, and summary writes in PlayerWorldExplorationService."
    }

    $directRevealReadPattern = "^\s*[^`"]*\.IsRevealed\("
    $invalidRevealReaders = @()
    Get-ChildItem -LiteralPath (Join-Path $RepoRoot "src\JueMingZ") -Recurse -Filter "*.cs" | ForEach-Object {
        $path = $_.FullName
        $text = Read-TextIfExists -Path $path
        if ($text -and [regex]::IsMatch($text, $directRevealReadPattern, [System.Text.RegularExpressions.RegexOptions]::Multiline) -and ($path -ne $readerPath)) {
            $invalidRevealReaders += $path
        }
    }
    if ($readerText.Contains(".IsRevealed(") -and $invalidRevealReaders.Count -eq 0) {
        Write-Pass "Player-world exploration reads Main.Map.IsRevealed only through the map reader."
    }
    else {
        Write-FailHealth "Player-world exploration must not read map reveal state outside PlayerWorldExplorationMapReader."
    }

    if ($uiText.Contains("PlayerWorldExplorationCache.ReadCurrent()") -and
        -not $uiText.Contains("PlayerWorldExplorationService.") -and
        -not $uiText.Contains("PlayerWorldFeatureDataStore.TryWriteJson")) {
        Write-Pass "Map enhancement UI reads cached exploration summary only and does not scan or write JSON."
    }
    else {
        Write-FailHealth "Map enhancement UI must read PlayerWorldExplorationCache only; scanning and JSON writes belong outside Draw."
    }

    $tooltipMatch = [regex]::Match(
        $uiMainText,
        "private static string\[\] BuildMapRevealedAreaRatioTooltipLines\([^)]*\)\s*\{(?<body>.*?)\n        \}",
        [System.Text.RegularExpressions.RegexOptions]::Singleline)
    $tooltipBody = if ($tooltipMatch.Success) { $tooltipMatch.Groups["body"].Value } else { "" }
    if ($tooltipBody.Contains("MapRevealedAreaRatioClickTooltip") -and
        -not $tooltipBody.Contains("已揭示") -and
        -not $tooltipBody.Contains("扫描中") -and
        -not $tooltipBody.Contains("上次统计")) {
        Write-Pass "Map revealed-area hover tooltip stays as the click hint; detailed statistics live in the modal."
    }
    else {
        Write-FailHealth "Map revealed-area hover tooltip must not reintroduce revealed/scanning/last-stat details."
    }

    if ($serviceText.Contains("PlayerWorldIdentityRuntimeCache.TryResolveCurrentCached") -and
        $playtimeText.Contains("PlayerWorldIdentityRuntimeCache.TryResolveCurrentCached") -and
        -not $serviceText.Contains("PlayerWorldIdentityResolver.TryResolveCurrent(") -and
        -not $playtimeText.Contains("PlayerWorldIdentityResolver.TryResolveCurrent(")) {
        Write-Pass "Player-world exploration/playtime cadence paths use cached identity resolution instead of the persisting resolver."
    }
    else {
        Write-FailHealth "Player-world exploration/playtime cadence paths must not call the persisting identity resolver directly."
    }

    if (-not $modelText.Contains("RescanCadenceTicks") -and
        -not $serviceText.Contains("RescanCadenceTicks") -and
        -not $serviceText.Contains("_nextRescanTick") -and
        -not $serviceText.Contains('"rescan"')) {
        Write-Pass "Player-world exploration complete scans no longer carry automatic 3600-tick rescan state."
    }
    else {
        Write-FailHealth "Player-world exploration must not reintroduce automatic complete-state rescan cadence."
    }

    $pauseIndex = $serviceText.IndexOf("if (IsPausedByUserLocked())")
    $readIndex = $serviceText.IndexOf("reader.TryIsRevealed")
    if ($serviceText.Contains("PauseScanning()") -and
        $serviceText.Contains("StartScanning()") -and
        $serviceText.Contains("PlayerWorldExplorationScanModes") -and
        $pauseIndex -ge 0 -and
        $readIndex -gt $pauseIndex) {
        Write-Pass "Player-world exploration exposes scan control API and checks paused state before reading tiles."
    }
    else {
        Write-FailHealth "Player-world exploration scan control must stop tile reads while paused and expose start/pause/mode APIs."
    }

    if ($testText.Contains("PlayerWorldExplorationPerformanceModeUsesTimeBudgetAndBackoff") -and
        $testText.Contains("PlayerWorldExplorationFastModeAdvancesMoreThanPerformanceMode") -and
        $testText.Contains("PlayerWorldExplorationFastModeCompleteStaysIdleWithoutRescan") -and
        $testText.Contains("PlayerWorldExplorationPauseStopsTileReadsInBothModes") -and
        $testText.Contains("PlayerWorldExplorationCompleteStaysIdlePastLegacyRescanCadence") -and
        $testText.Contains("PlayerWorldExplorationPauseStopsTileReadsAndStartResumesCursor") -and
        $testText.Contains("PlayerWorldExplorationIdleStartPerformsManualRefresh") -and
        $testText.Contains("PlayerWorldExplorationPairChangeDoesNotReviveOldPairScan") -and
        $testText.Contains("PlayerWorldExplorationModeSwitchDoesNotClearCompletedResult") -and
        $testText.Contains("PlayerWorldExplorationLegacySummaryDefaultsToPerformanceState") -and
        $testText.Contains("LegacyMapRevealedAreaTooltipAndDetailsLines") -and
        $testText.Contains("LegacyMapRevealedAreaDetailsPopupRegistersAsModal") -and
        $testText.Contains("LegacyMapRevealedAreaCommandsDrivePopupAndScanControl") -and
        $testText.Contains("PlayerWorldExplorationDiagnosticsWrittenToSnapshot") -and
        $identityTestText.Contains("PlayerWorldIdentityRuntimeCacheThrottlesExplorationIdentityPersistence") -and
        $identityTestText.Contains("PlayerWorldIdentityRuntimeCacheThrottlesPlaytimeIdentityPersistence")) {
        Write-Pass "Player-world exploration regressions cover time budget, fast mode, idle, pause/start, UI modal commands, diagnostics, legacy summaries, and identity hot paths."
    }
    else {
        Write-FailHealth "Player-world exploration tests must cover time budget/backoff, fast mode, idle no-rescan, paused no-tile-read, start resume/manual refresh, pair change, mode switch, legacy summary defaults, UI modal commands, diagnostics, and identity hot paths."
    }
}

function Test-BlueprintDiagnosticsGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $diagnosticsPath = Join-Path $RepoRoot "src\JueMingZ\Automation\Blueprint\BlueprintDiagnostics.cs"
    $projectionPath = Join-Path $RepoRoot "src\JueMingZ\Automation\Blueprint\BlueprintProjectionService.cs"
    $materialsPath = Join-Path $RepoRoot "src\JueMingZ\Automation\Blueprint\BlueprintMaterialService.cs"
    $autoPlacementPath = Join-Path $RepoRoot "src\JueMingZ\Automation\Blueprint\BlueprintAutoPlacementService.cs"
    $snapshotPath = Join-Path $RepoRoot "src\JueMingZ\Diagnostics\DiagnosticSnapshot.cs"
    $writerPath = Join-Path $RepoRoot "src\JueMingZ\Diagnostics\DiagnosticSnapshotWriter.Json.cs"
    $builderPath = Join-Path $RepoRoot "src\JueMingZ\Runtime\Diagnostics\RuntimeDiagnosticSnapshotBuilder.Blueprint.cs"
    $uiActionPath = Join-Path $RepoRoot "src\JueMingZ\Input\LegacyUiActionService.Blueprint.cs"
    $entryStatePath = Join-Path $RepoRoot "src\JueMingZ\Automation\Blueprint\BlueprintEntryState.cs"
    $executorPath = Join-Path $RepoRoot "src\JueMingZ\Actions\Executors\BlueprintAutoPlaceActionExecutor.cs"
    $testPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintDiagnosticsTests.cs"
    $programPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.cs"
    $functionDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $diagnosticsDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "AI诊断日志说明.md")
    $plan17Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图功能实现", "17-诊断性能测试审计护栏.md")

    $diagnosticsText = Read-TextIfExists -Path $diagnosticsPath
    $projectionText = Read-TextIfExists -Path $projectionPath
    $materialsText = Read-TextIfExists -Path $materialsPath
    $autoPlacementText = Read-TextIfExists -Path $autoPlacementPath
    $snapshotText = Read-TextIfExists -Path $snapshotPath
    $writerText = Read-TextIfExists -Path $writerPath
    $builderText = Read-TextIfExists -Path $builderPath
    $uiActionText = Read-TextIfExists -Path $uiActionPath
    $entryStateText = Read-TextIfExists -Path $entryStatePath
    $executorText = Read-TextIfExists -Path $executorPath
    $testText = Read-TextIfExists -Path $testPath
    $programText = Read-TextIfExists -Path $programPath
    $functionDocText = Read-TextIfExists -Path $functionDocPath
    $diagnosticsDocText = Read-TextIfExists -Path $diagnosticsDocPath
    $plan17Text = Read-TextIfExists -Path $plan17Path

    if ($diagnosticsText -and
        $diagnosticsText.Contains("TemplateCountCacheMs") -and
        $diagnosticsText.Contains("Blueprint.Projection.Resolve") -and
        $diagnosticsText.Contains("Blueprint.Materials.Resolve") -and
        $diagnosticsText.Contains("Blueprint.AutoPlacement.CandidateScan") -and
        $diagnosticsText.Contains("RecordOperationIfNeeded")) {
        Write-Pass "Blueprint diagnostics helper records throttled projection/material/auto-placement performance counters."
    }
    else {
        Write-FailHealth "Blueprint diagnostics helper must own stage-17 performance counters and thresholded performance-events wiring."
    }

    if ($projectionText -and $materialsText -and $autoPlacementText -and
        $projectionText.Contains("BlueprintDiagnostics.RecordProjectionResolve") -and
        $materialsText.Contains("BlueprintDiagnostics.RecordMaterialResolve") -and
        $autoPlacementText.Contains("BlueprintDiagnostics.RecordAutoPlacementCandidateScan")) {
        Write-Pass "Blueprint projection, material, and auto-placement scans feed the stage-17 diagnostics counters."
    }
    else {
        Write-FailHealth "Blueprint diagnostics counters must be fed by projection resolve, material resolve, and auto-placement candidate scan paths."
    }

    $requiredSnapshotFields = @(
        "BlueprintDiagnosticsTemplateCount",
        "BlueprintDiagnosticsInstanceCount",
        "BlueprintDiagnosticsHiddenInstanceCount",
        "BlueprintDiagnosticsMaterialMissingStackTotal",
        "BlueprintProjectionAverageResolveElapsedMs",
        "BlueprintMaterialsAverageResolveElapsedMs",
        "BlueprintAutoPlacementLastFailureReason",
        "BlueprintAutoPlacementAverageCandidateScanElapsedMs",
        "BlueprintPerformanceLastScenario"
    )
    $missingSnapshotFields = @()
    foreach ($field in $requiredSnapshotFields) {
        if (-not ($snapshotText -and $snapshotText.Contains($field) -and $writerText -and $writerText.Contains($field) -and $builderText -and $builderText.Contains($field))) {
            $missingSnapshotFields += $field
        }
    }

    if ($missingSnapshotFields.Count -eq 0) {
        Write-Pass "Blueprint runtime snapshot, JSON writer, and builder expose stage-17 diagnostics fields."
    }
    else {
        Write-FailHealth "Blueprint runtime snapshot fields missing from DTO/writer/builder: $($missingSnapshotFields -join ', ')"
    }

    if ($uiActionText -and $executorText -and
        $uiActionText.Contains("Ui.Blueprint.Entry") -and
        $uiActionText.Contains("FinishCreateSave") -and
        $uiActionText.Contains("FinishCreateUse") -and
        $uiActionText.Contains("StartErase") -and
        $entryStateText -and $entryStateText.Contains("MirrorPreviewHorizontal") -and
        $uiActionText.Contains("Ui.Blueprint.Placed.") -and
        $uiActionText.Contains("Ui.Blueprint.ReplacementCategory") -and
        $executorText.Contains("ScenarioNames.BlueprintAutoPlace") -and
        $executorText.Contains("directWorldMutationAttempted") -and
        $executorText.Contains("inventoryMutationAttempted")) {
        Write-Pass "Blueprint action events cover create/save, preview/place, erase, mirror, replacement, and auto-place request/result contracts."
    }
    else {
        Write-FailHealth "Blueprint action events must keep create/save, preview/place, erase, mirror, replacement, and auto-place diagnostics anchors."
    }

    if ($testText -and $programText -and
        $testText.Contains("BlueprintDiagnosticsAggregateRuntimeSnapshotJson") -and
        $testText.Contains("BlueprintDiagnosticsPerformanceCountersAverageCosts") -and
        $programText.Contains("blueprint diagnostics aggregate runtime snapshot json") -and
        $programText.Contains("blueprint diagnostics performance counters average costs")) {
        Write-Pass "Blueprint stage-17 console tests are registered."
    }
    else {
        Write-FailHealth "Blueprint stage-17 diagnostics/performance tests must be present and registered."
    }

    $blueprintSourceDir = Join-Path $RepoRoot "src\JueMingZ\Automation\Blueprint"
    $blueprintSourceText = ""
    if (Test-Path -LiteralPath $blueprintSourceDir) {
        $sourceFiles = Get-ChildItem -LiteralPath $blueprintSourceDir -Filter "*.cs" -File -ErrorAction SilentlyContinue
        foreach ($file in $sourceFiles) {
            $blueprintSourceText += "`n" + (Read-TextIfExists -Path $file.FullName)
        }
    }

    $forbiddenPatterns = @(
        "Main\.tile\s*\[",
        "\.stack\s*=",
        "NetMessage\.Send",
        "controlUseItem\s*=",
        "selectedItem\s*=",
        "\.AddBuff\s*\(",
        "buffType\s*\[",
        "buffTime\s*\[",
        "statLife\s*=",
        "statMana\s*="
    )
    $violations = @()
    foreach ($pattern in $forbiddenPatterns) {
        if ($blueprintSourceText -match $pattern) {
            $violations += $pattern
        }
    }

    if ($violations.Count -eq 0) {
        Write-Pass "Blueprint automation source has no naked world/inventory/player/input mutation patterns outside controlled executors."
    }
    else {
        Write-FailHealth "Blueprint automation source contains forbidden naked mutation patterns: $($violations -join ', ')"
    }

    if ($functionDocText -and $diagnosticsDocText -and $plan17Text -and
        $functionDocText.Contains("BlueprintDiagnosticsTemplateCount") -and
        $functionDocText.Contains("BlueprintAutoPlacementAverageCandidateScanElapsedMs") -and
        $diagnosticsDocText.Contains("BlueprintDiagnosticsTemplateCount") -and
        $diagnosticsDocText.Contains("BlueprintAutoPlacementLastFailureReason") -and
        $plan17Text.Contains("0.854-blueprint-diagnostics-performance-audit")) {
        Write-Pass "Blueprint stage-17 function, diagnostics, and plan documents are synchronized."
    }
    else {
        Write-FailHealth "Blueprint stage-17 documents must describe new diagnostics fields, average-cost counters, and the 0.854 completion record."
    }
}

function Test-BlueprintHandheldActionBarGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $statePath = Join-Path $RepoRoot "src\JueMingZ\Automation\Blueprint\BlueprintHandheldActionBarState.cs"
    $overlayPath = Join-Path $RepoRoot "src\JueMingZ\UI\BlueprintHandheldActionBarOverlay.cs"
    $diagnosticMouseReaderPath = Join-Path $RepoRoot "src\JueMingZ\UI\DiagnosticMouseStateReader.cs"
    $legacyUiInputMousePath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyUiInput.Mouse.cs"
    $legacyUiInputCommandsPath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyUiInput.Commands.cs"
    $legacyUiActionServicePath = Join-Path $RepoRoot "src\JueMingZ\Input\LegacyUiActionService.cs"
    $runtimeDispatcherPath = Join-Path $RepoRoot "src\JueMingZ\Runtime\RuntimeAutomationDispatcher.cs"
    $interfaceLayerCallbacksPath = Join-Path $RepoRoot "src\JueMingZ\Hooks\InterfaceLayerHookCallbacks.cs"
    $uiActionPath = Join-Path $RepoRoot "src\JueMingZ\Input\LegacyUiActionService.Blueprint.cs"
    $snapshotPath = Join-Path $RepoRoot "src\JueMingZ\Diagnostics\DiagnosticSnapshot.cs"
    $writerPath = Join-Path $RepoRoot "src\JueMingZ\Diagnostics\DiagnosticSnapshotWriter.Json.cs"
    $builderPath = Join-Path $RepoRoot "src\JueMingZ\Runtime\Diagnostics\RuntimeDiagnosticSnapshotBuilder.Blueprint.cs"
    $testPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintHandheldActionBarTests.cs"
    $interfaceLayerTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.InterfaceLayerHookTests.cs"
    $runtimeTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.RuntimeDiagnosticsAndDispatchTests.cs"
    $programPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.cs"
    $functionDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $diagnosticsDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "AI诊断日志说明.md")
    $plan07Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "蓝图功能实机反馈修补", "07-手持操作栏动态按钮与真实命令接线.md")
    if (-not (Test-Path -LiteralPath $plan07Path)) {
        $plan07Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图功能实机反馈修补", "07-手持操作栏动态按钮与真实命令接线.md")
    }
    $plan04Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "蓝图手持凝胶操作栏", "04-诊断测试文档审计.md")
    if (-not (Test-Path -LiteralPath $plan04Path)) {
        $plan04Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图手持凝胶操作栏", "04-诊断测试文档审计.md")
    }

    $stateText = Read-TextIfExists -Path $statePath
    $overlayText = Read-TextIfExists -Path $overlayPath
    $diagnosticMouseReaderText = Read-TextIfExists -Path $diagnosticMouseReaderPath
    $legacyUiInputMouseText = Read-TextIfExists -Path $legacyUiInputMousePath
    $legacyUiInputCommandsText = Read-TextIfExists -Path $legacyUiInputCommandsPath
    $legacyUiActionServiceText = Read-TextIfExists -Path $legacyUiActionServicePath
    $runtimeDispatcherText = Read-TextIfExists -Path $runtimeDispatcherPath
    $interfaceLayerCallbacksText = Read-TextIfExists -Path $interfaceLayerCallbacksPath
    $uiActionText = Read-TextIfExists -Path $uiActionPath
    $snapshotText = Read-TextIfExists -Path $snapshotPath
    $writerText = Read-TextIfExists -Path $writerPath
    $builderText = Read-TextIfExists -Path $builderPath
    $testText = Read-TextIfExists -Path $testPath
    $interfaceLayerTestText = Read-TextIfExists -Path $interfaceLayerTestPath
    $runtimeTestText = Read-TextIfExists -Path $runtimeTestPath
    $programText = Read-TextIfExists -Path $programPath
    $functionDocText = Read-TextIfExists -Path $functionDocPath
    $diagnosticsDocText = Read-TextIfExists -Path $diagnosticsDocPath
    $plan07Text = Read-TextIfExists -Path $plan07Path
    $plan04Text = Read-TextIfExists -Path $plan04Path

    if ($stateText -and
        $stateText.Contains("CommandElementPrefix") -and
        $stateText.Contains("blueprint-handheld-action-bar:") -and
        $stateText.Contains("ResultCodeUiOnlyNotImplemented") -and
        $stateText.Contains("ResultCodeEntryWiredDeferred") -and
        $stateText.Contains("ButtonIdSave") -and
        $stateText.Contains("ButtonIdClearSelection") -and
        $stateText.Contains("ButtonIdOpenPlacedList") -and
        $stateText.Contains("ButtonIdClearPlaced") -and
        $stateText.Contains("ButtonIdRegionModify") -and
        $stateText.Contains("ButtonIdMirror") -and
        $stateText.Contains("清除选区") -and
        $stateText.Contains("已放置蓝图列表") -and
        $stateText.Contains("RecordDeferredBusinessClick") -and
        $stateText.Contains("BlueprintCreationHasPendingSelection") -and
        $stateText.Contains("BlueprintHasPlacedInstances") -and
        $stateText.Contains("Tooltip") -and
        $stateText.Contains("BuildDiagnostics") -and
        $stateText.Contains("_lastCommandLeftDown") -and
        $stateText.Contains("_lastCaptureLeftDown") -and
        $stateText.Contains("_pendingAfterPlayerInputCommandEdge") -and
        $stateText.Contains("input.AfterPlayerInput") -and
        $stateText.Contains("replayPendingAfterPlayerInputEdge") -and
        $stateText.Contains("PlayerInput postfix replay") -and
        $stateText.Contains("BuildPointerOwnerId") -and
        $stateText.Contains("PointerOwnershipReasonLeft") -and
        $stateText.Contains("_lastMouseReadMode") -and
        $stateText.Contains("_lastOwnershipReason") -and
        $stateText.Contains("input.MouseReadMode") -and
        $stateText.Contains("BlueprintHandheldActionBarDiagnostics")) {
        Write-Pass "Blueprint handheld action bar state owns dedicated command ids, stage-03 placed-list/deferred command ids, dynamic matrix inputs including clear-selection, tooltips, split command/capture click memory, postfix replay memory, UI pointer owner metadata, and lightweight diagnostics."
    }
    else {
        Write-FailHealth "Blueprint handheld action bar state must keep dedicated command ids, stage-03 placed-list/deferred command ids, dynamic matrix inputs including clear-selection, tooltips, split command/capture click memory, postfix stale-edge replay memory, UI pointer owner metadata, and lightweight diagnostics ownership."
    }

    if ($stateText -and
        -not $stateText.Contains("return Hidden(HiddenReasonGameInputUnavailable") -and
        -not $stateText.Contains("return Hidden(HiddenReasonPlayerInventoryOpen") -and
        -not $stateText.Contains("return Hidden(HiddenReasonChestOpen")) {
        Write-Pass "Blueprint handheld action bar keeps inventory, chest, and game-input-unavailable as visible display-gate contexts."
    }
    else {
        Write-FailHealth "Blueprint handheld action bar must not treat player inventory, chest, or game-input-unavailable as hard hidden reasons."
    }

    if ($overlayText -and
        $overlayText.Contains("dynamic-buttons") -and
        $overlayText.Contains("create-enters-mask") -and
        $overlayText.Contains("save-captures-mask") -and
        $overlayText.Contains("clear-selection") -and
        $overlayText.Contains("open-library-real") -and
        $overlayText.Contains("open-placed-list-real") -and
        $overlayText.Contains("stage03-deferred-placed-commands") -and
        $overlayText.Contains("mouse-consume") -and
        $overlayText.Contains("legacy-ui-theme") -and
        $overlayText.Contains("vanilla-ui-skin") -and
        $overlayText.Contains("button-text-scale-0.78") -and
        $overlayText.Contains("internal const float ButtonTextScale = 0.78f;") -and
        $overlayText.Contains("LegacyUiTheme.DrawPanel") -and
        $overlayText.Contains("LegacyUiTheme.DrawButtonClipped") -and
        $overlayText.Contains("ResolveButtonTextScale") -and
        $overlayText.Contains("PopulateDynamicBlueprintState") -and
        $overlayText.Contains("GetCachedSummary") -and
        $overlayText.Contains("GetDiagnostics") -and
        $overlayText.Contains("ReadForBlueprintHandheldActionBarOverlay") -and
        $overlayText.Contains("ReadMouseForBlueprintHandheldOverlay") -and
        $overlayText.Contains("MouseReadMode = mouse.ReadMode") -and
        $overlayText.Contains("BuildPointerOwnerId") -and
        $overlayText.Contains("RegisterPointerOwnerForCurrentFrame") -and
        $overlayText.Contains('UpdateInputGuard("BlueprintHandheldActionBarOverlay.UpdateAfterPlayerInputGuard", true, true)') -and
        $overlayText.Contains("no-blueprint-refresh") -and
        $overlayText.Contains("no-library-refresh") -and
        $overlayText.Contains("no-input-action-queue") -and
        -not $overlayText.Contains("BlueprintEntryState.ApplyCommand") -and
        -not $overlayText.Contains("InputActionQueue") -and
        -not $overlayText.Contains("ForceRefreshForMaterialWindow") -and
        -not $overlayText.Contains("ForceRefreshForAutoPlacement")) {
        Write-Pass "Blueprint handheld overlay remains draw/input-only, exposes the stage-03 placed-list/deferred command contract, uses cached dynamic state, uses the legacy UI theme skin path, uses its dedicated gate-bypass mouse reader, and does not refresh blueprint caches or submit InputActionQueue actions."
    }
    else {
        Write-FailHealth "Blueprint handheld overlay must expose dynamic create/save/open-library/open-placed/deferred-command contract, use cached state and LegacyUiTheme skin rendering with 0.78 base text scale, use the dedicated gate-bypass mouse reader, and avoid blueprint refresh or InputActionQueue calls."
    }

    if ($diagnosticMouseReaderText -and $legacyUiInputMouseText -and
        $diagnosticMouseReaderText.Contains("ReadForBlueprintHandheldActionBarOverlay") -and
        $diagnosticMouseReaderText.Contains("BlueprintHandheldOverlayGateBypass") -and
        $legacyUiInputMouseText.Contains("ReadMouseForBlueprintHandheldOverlay") -and
        $legacyUiInputMouseText.Contains("ResolveBlueprintHandheldOverlayMouse") -and
        $legacyUiInputMouseText.Contains("preferOsClientWhenGateOpen") -and
        $legacyUiInputMouseText.Contains("preserveTerrariaInputWhenGateClosed") -and
        $legacyUiInputMouseText.Contains("overlayTerrariaInputAvailable && raw.TerrariaLeftDown")) {
        Write-Pass "Blueprint handheld action bar has a dedicated gate-closed in-process mouse reader without OS fallback and prefers fresh OS client coordinates while the input gate is open."
    }
    else {
        Write-FailHealth "Blueprint handheld action bar must use a dedicated gate-closed mouse reader that preserves only Terraria in-process input, does not restore OS fallback, and prefers fresh OS client coordinates while the input gate is open."
    }

    if ($interfaceLayerCallbacksText -and $interfaceLayerTestText -and
        $interfaceLayerCallbacksText.Contains("BlueprintHandheldActionBarDispatcherLayerName") -and
        $interfaceLayerCallbacksText.Contains("BlueprintHandheldActionBarDispatcherDrawers") -and
        $interfaceLayerCallbacksText.Contains("DrawBlueprintHandheldActionBarDispatcherLayer") -and
        $interfaceLayerCallbacksText.Contains("GetBlueprintHandheldActionBarScaleTypeNameForTesting") -and
        $interfaceLayerCallbacksText.Contains('ParseScaleValue(_scaleType, "None")') -and
        $interfaceLayerTestText.Contains("GetBlueprintHandheldActionBarDispatcherRouteNamesForTesting") -and
        $interfaceLayerTestText.Contains("GetBlueprintHandheldActionBarScaleTypeNameForTesting") -and
        $interfaceLayerTestText.Contains("blueprint handheld action bar unscaled dispatcher routes")) {
        Write-Pass "Blueprint handheld action bar uses a dedicated None-scale interface dispatcher so physical mouse hit-test coordinates match the visual layer."
    }
    else {
        Write-FailHealth "Blueprint handheld action bar must stay on its dedicated InterfaceScaleType.None dispatcher, with interface-layer tests covering route names and scale type."
    }

    if ($legacyUiInputCommandsText -and $legacyUiActionServiceText -and $runtimeDispatcherText -and
        $legacyUiInputCommandsText.Contains("TryDrainCommandByElementPrefix") -and
        $legacyUiInputCommandsText.Contains("CountPendingCommandsByElementPrefix") -and
        $legacyUiActionServiceText.Contains("UpdateBlueprintHandheldCommandsWhenInputUnavailable") -and
        $legacyUiActionServiceText.Contains("BlueprintHandheldActionBarState.CommandElementPrefix") -and
        $runtimeDispatcherText.Contains("UpdateBlueprintHandheldCommandsWhenInputUnavailable") -and
        $runtimeDispatcherText.Contains("gameInputAvailable=false") -and
        $runtimeDispatcherText.Contains("hotkeys and gameplay action submitters stay closed")) {
        Write-Pass "Blueprint handheld UI commands have a narrow game-input-unavailable drain path that keeps other input services closed."
    }
    else {
        Write-FailHealth "Blueprint handheld UI commands must keep a narrow game-input-unavailable drain path by command prefix while other input services remain closed."
    }

    $handlerMatch = [System.Text.RegularExpressions.Regex]::Match(
        $uiActionText,
        "private\s+static\s+void\s+HandleBlueprintHandheldActionBarCommand[\s\S]*?private\s+static\s+void\s+HandleBlueprintReplacementMode")
    $handlerText = if ($handlerMatch.Success) { $handlerMatch.Value } else { "" }
    $hasStage03DeferredBusiness = $handlerText.Contains("RecordDeferredBusinessClick") -and
        $handlerText.Contains("ResultCodeEntryWiredDeferred") -and
        $handlerText.Contains("deferredBusiness")
    $hasStage07PlacedBusiness = $handlerText.Contains("BlueprintPlacedInstanceUiState.ClearAllCurrentWorld") -and
        ($handlerText.Contains("BlueprintEraseRegionState.BeginErase") -or
            ($handlerText.Contains("StartOrCancelBlueprintRegionModify") -and
                $handlerText.Contains("BlueprintEntryCommands.StartRegionModify") -and
                $handlerText.Contains("eraseInputActive"))) -and
        (($handlerText.Contains("BlueprintPlacedInstanceTransformState.BeginMove") -and
                $handlerText.Contains("BlueprintPlacedInstanceTransformState.BeginMirror")) -or
            ($handlerText.Contains("StartOrCancelBlueprintMove") -and
                ($handlerText.Contains("StartBlueprintMirror") -or
                    $handlerText.Contains("StartOrCancelBlueprintMirror")))) -and
        $handlerText.Contains("transformInputActive") -and
        $handlerText.Contains('\"uiOnly\":false')
    if ($handlerText.Contains("Ui.Blueprint.HandheldActionBar") -and
        $handlerText.Contains("RecordCommandResultClick") -and
        $handlerText.Contains("RecordPlaceholderClick") -and
        $handlerText.Contains("BlueprintEntryCommands.StartCreate") -and
        $handlerText.Contains("BlueprintEntryCommands.FinishCreateSave") -and
        $handlerText.Contains("BlueprintCaptureService.CapturePendingMaskAndSave(false)") -and
        $handlerText.Contains("BlueprintLibraryUiState.NotifyTemplateCreated") -and
        $handlerText.Contains("BlueprintEntryState.MarkCaptureSaved") -and
        $handlerText.Contains("BlueprintEntryState.RecordCaptureFailure") -and
        $handlerText.Contains("BlueprintEntryCommands.ClearSelection") -and
        $handlerText.Contains("BlueprintLibraryUiState.OpenLibrary") -and
        $handlerText.Contains("BlueprintEntryCommands.OpenLibrary") -and
        $handlerText.Contains("BlueprintPlacedInstanceUiState.OpenManagement") -and
        $handlerText.Contains("BlueprintEntryCommands.OpenPlacedInstances") -and
        ($hasStage03DeferredBusiness -or $hasStage07PlacedBusiness) -and
        $handlerText.Contains("BlueprintEntryState.ApplyCommand") -and
        $handlerText.Contains('\"submitted\":false') -and
        $handlerText.Contains('BoolRaw(!entry.PlaceholderOnly)') -and
        $handlerText.Contains('\"implemented\":true') -and
        $handlerText.Contains('\"implemented\":false') -and
        $handlerText.Contains('\"uiOnly\":true') -and
        $handlerText.Contains("BuildBlueprintHandheldActionMetadata") -and
        $handlerText.Contains('\"mouseReadMode\"') -and
        $handlerText.Contains('\"ownerId\"') -and
        $handlerText.Contains('\"inputTrace\"') -and
        $handlerText.Contains('\"ownershipTrace\"') -and
        $handlerText.Contains("result.ResultCode") -and
        -not $handlerText.Contains("InputActionQueue") -and
        -not $handlerText.Contains("ForceRefresh")) {
        Write-Pass "Blueprint handheld action bar command handler wires create/save/clear-selection/open-library/open-placed and accepts either stage-03 deferred placed commands or current real placed-instance governance without InputActionQueue paths."
    }
    else {
        Write-FailHealth "Blueprint handheld action bar handler must wire create/save/clear-selection/open-library/open-placed to real UI state commands, and keep either stage-03 deferred placed commands or current real placed-instance governance without InputActionQueue paths."
    }

    $requiredSnapshotFields = @(
        "BlueprintHandheldActionBarVisible",
        "BlueprintHandheldActionBarBlockedReason",
        "BlueprintHandheldActionBarToolItemId",
        "BlueprintHandheldActionBarSelectedItemType",
        "BlueprintHandheldActionBarLastAction",
        "BlueprintHandheldActionBarLastResultCode",
        "BlueprintHandheldActionBarHoveredButtonId",
        "BlueprintHandheldActionBarPressedButtonId",
        "BlueprintHandheldActionBarLastMouseReadMode",
        "BlueprintHandheldActionBarLastOwnershipReason",
        "BlueprintHandheldActionBarLastInputTrace",
        "BlueprintHandheldActionBarLastOwnershipTrace",
        "BlueprintWorldOverlayLastInputTrace"
    )
    $missingSnapshotFields = @()
    foreach ($field in $requiredSnapshotFields) {
        if (-not ($snapshotText -and $snapshotText.Contains($field) -and $writerText -and $writerText.Contains($field) -and $builderText -and $builderText.Contains($field))) {
            $missingSnapshotFields += $field
        }
    }

    if ($missingSnapshotFields.Count -eq 0) {
        Write-Pass "Blueprint handheld action bar snapshot summary fields are wired through DTO, JSON writer, and runtime builder."
    }
    else {
        Write-FailHealth "Blueprint handheld action bar snapshot fields missing from DTO/writer/builder: $($missingSnapshotFields -join ', ')"
    }

    if ($testText -and $programText -and
        $testText.Contains("BlueprintHandheldActionBarDynamicButtonMatrix") -and
        $testText.Contains("BlueprintHandheldActionBarDisplayGatesStayVisibleAndUiOnly") -and
        $testText.Contains("BlueprintHandheldActionBarAfterPlayerInputGuardSubmitsFreshClickEdge") -and
        $testText.Contains("BlueprintHandheldActionBarPostfixReplaysStalePrefixPress") -and
        $testText.Contains("BlueprintHandheldActionBarGateClosedMouseKeepsTerrariaClick") -and
        $testText.Contains("BlueprintHandheldActionBarGateOpenMousePrefersOsClientCoordinate") -and
        $testText.Contains("BlueprintHandheldActionBarPhysicalBottomCenterRejectsUiScaleLogicalExtent") -and
        $testText.Contains("old UI-scale logical extent false positive") -and
        $runtimeTestText.Contains("QueueBlueprintHandheldActionBarCommandForTesting") -and
        $runtimeTestText.Contains("gameInputAvailable=false should drain already queued blueprint handheld UI commands only") -and
        $testText.Contains("BlueprintHandheldActionBarVisualStyleUsesLegacyThemeAndStableTextScale") -and
        $testText.Contains("BlueprintHandheldActionBarOverlayStaysUiOnlyAndNoScan") -and
        $testText.Contains("no-library-refresh") -and
        $testText.Contains("clear-selection") -and
        $testText.Contains("ButtonIdClearSelection") -and
        $testText.Contains("ButtonIdOpenPlacedList") -and
        $testText.Contains("ButtonIdClearPlaced") -and
        $testText.Contains("ButtonIdRegionModify") -and
        $testText.Contains("ButtonIdMirror") -and
        $testText.Contains("ResultCodeEntryWiredDeferred") -and
        $testText.Contains("stage03-deferred-placed-commands") -and
        $testText.Contains("selectionCleared") -and
        $testText.Contains("BlueprintHandheldActionBarRealCommandsAndDeferredPlacedCommands") -and
        $testText.Contains("BlueprintHandheldActionBarDiagnosticsSnapshotJson") -and
        $testText.Contains("LastMouseReadMode") -and
        $testText.Contains("LastOwnershipReason") -and
        $testText.Contains("BlueprintHandheldActionBarLastInputTrace") -and
        $testText.Contains("BlueprintWorldOverlayLastInputTrace") -and
        $testText.Contains("disabled save ownership reason") -and
        $testText.Contains("outside handheld click must not claim ownership reason") -and
        $programText.Contains("blueprint handheld action bar dynamic button matrix") -and
        $programText.Contains("blueprint handheld action bar display gates stay visible and UI-only") -and
        $programText.Contains("blueprint handheld action bar after PlayerInput guard submits fresh click edge") -and
        $programText.Contains("blueprint handheld action bar postfix replays stale prefix press") -and
        $programText.Contains("blueprint handheld action bar gate-closed mouse keeps Terraria click") -and
        $programText.Contains("blueprint handheld action bar gate-open mouse prefers OS client coordinate") -and
        $programText.Contains("blueprint handheld action bar physical bottom-center rejects UI-scale logical extent") -and
        $programText.Contains("blueprint handheld action bar real commands and deferred placed commands") -and
        $programText.Contains("blueprint handheld action bar visual style uses legacy theme and stable text scale") -and
        $programText.Contains("blueprint handheld action bar diagnostics snapshot json")) {
        Write-Pass "Blueprint handheld action bar console tests cover dynamic buttons, clear-selection, display-gate visibility, post-PlayerInput command timing, stale prefix edge replay, gate-open OS coordinates, physical bottom-center frame hit-test, gate-closed Terraria mouse input, themed visual scale, no-scan, real create/save/open-library/open-placed commands, stage-03 deferred placed commands, and snapshot JSON."
    }
    else {
        Write-FailHealth "Blueprint handheld action bar tests must cover dynamic buttons, clear-selection, display-gate visibility, post-PlayerInput command timing, stale prefix edge replay, gate-open OS coordinates, physical bottom-center frame hit-test, gate-closed Terraria mouse input, themed visual scale, no-scan, real create/save/open-library/open-placed commands, stage-03 deferred placed commands, and snapshot JSON diagnostics."
    }

    if ($functionDocText -and $diagnosticsDocText -and $plan04Text -and $plan07Text -and
        $functionDocText.Contains("BlueprintHandheldActionBarVisible") -and
        $functionDocText.Contains("BlueprintHandheldActionBarLastMouseReadMode") -and
        $functionDocText.Contains("BlueprintHandheldActionBarLastOwnershipReason") -and
        $functionDocText.Contains("BlueprintHandheldActionBarLastInputTrace") -and
        $functionDocText.Contains("BlueprintWorldOverlayLastInputTrace") -and
        $functionDocText.Contains("Ui.Blueprint.HandheldActionBar") -and
        $functionDocText.Contains("mouseReadMode") -and
        $functionDocText.Contains("ownerId") -and
        $functionDocText.Contains("保存蓝图") -and
        $functionDocText.Contains("清除选区") -and
        $functionDocText.Contains("after-PlayerInput") -and
        $functionDocText.Contains("BlueprintHandheldOverlayGateBypass") -and
        $functionDocText.Contains("打开蓝图库") -and
        $functionDocText.Contains("已放置蓝图列表") -and
        $functionDocText.Contains("entryWiredDeferred") -and
        $functionDocText.Contains("open-placed-list-real") -and
        $functionDocText.Contains("stage03-deferred-placed-commands") -and
        $diagnosticsDocText.Contains("BlueprintHandheldActionBarVisible") -and
        $diagnosticsDocText.Contains("BlueprintHandheldActionBarLastMouseReadMode") -and
        $diagnosticsDocText.Contains("BlueprintHandheldActionBarLastOwnershipReason") -and
        $diagnosticsDocText.Contains("BlueprintHandheldActionBarLastInputTrace") -and
        $diagnosticsDocText.Contains("BlueprintWorldOverlayLastInputTrace") -and
        $diagnosticsDocText.Contains("ownerId") -and
        $diagnosticsDocText.Contains("DiagnosticLifecycle=Stabilization") -and
        $diagnosticsDocText.Contains("save-captures-mask") -and
        $diagnosticsDocText.Contains("clear-selection") -and
        $diagnosticsDocText.Contains("entryWiredDeferred") -and
        $diagnosticsDocText.Contains("stage03-deferred-placed-commands") -and
        $plan04Text.Contains("0.871-blueprint-handheld-diagnostics-audit") -and
        $plan07Text.Contains("0.878-blueprint-handheld-dynamic-actions")) {
        Write-Pass "Blueprint handheld action bar function, diagnostics, stage-04, and stage-07 plan docs are synchronized."
    }
    else {
        Write-FailHealth "Blueprint handheld action bar docs must describe dynamic buttons, real save/library/placed-list commands, stage-03 deferred placed commands, snapshot summary fields, Stabilization lifecycle, and 0.878 stage-07 completion."
    }
}

function Test-BlueprintUiClickStage02Governance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $ownershipPath = Join-Path $RepoRoot "src\JueMingZ\UI\UiPointerOwnershipService.cs"
    $capturePath = Join-Path $RepoRoot "src\JueMingZ\UI\UiMouseCaptureService.cs"
    $handheldPath = Join-Path $RepoRoot "src\JueMingZ\UI\BlueprintHandheldActionBarOverlay.cs"
    $creationPath = Join-Path $RepoRoot "src\JueMingZ\UI\BlueprintCreationOverlay.cs"
    $placementPath = Join-Path $RepoRoot "src\JueMingZ\UI\BlueprintPlacementPreviewOverlay.cs"
    $erasePath = Join-Path $RepoRoot "src\JueMingZ\UI\BlueprintEraseRegionOverlay.cs"
    $uiInputTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.UiInputFrameTests.cs"
    $creationTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintCreationTests.cs"
    $programPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.cs"
    $functionDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $diagnosticsDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "AI诊断日志说明.md")
    $plan02Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "蓝图手持栏UI点击所有权治理", "02-跨Overlay输入所有权闭环.md")
    if (-not (Test-Path -LiteralPath $plan02Path)) {
        $plan02Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图手持栏UI点击所有权治理", "02-跨Overlay输入所有权闭环.md")
    }
    $updateRecordPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "0.902-蓝图UI指针所有权闭环-2606212345.md")
    $docHistoryPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "蓝图UI指针所有权闭环-2606212345.md")
    $updateIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "索引.md")
    $docHistoryIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "索引.md")

    $ownershipText = Read-TextIfExists -Path $ownershipPath
    $captureText = Read-TextIfExists -Path $capturePath
    $handheldText = Read-TextIfExists -Path $handheldPath
    $creationText = Read-TextIfExists -Path $creationPath
    $placementText = Read-TextIfExists -Path $placementPath
    $eraseText = Read-TextIfExists -Path $erasePath
    $uiInputTestText = Read-TextIfExists -Path $uiInputTestPath
    $creationTestText = Read-TextIfExists -Path $creationTestPath
    $programText = Read-TextIfExists -Path $programPath
    $functionDocText = Read-TextIfExists -Path $functionDocPath
    $diagnosticsDocText = Read-TextIfExists -Path $diagnosticsDocPath
    $plan02Text = Read-TextIfExists -Path $plan02Path
    $updateRecordText = Read-TextIfExists -Path $updateRecordPath
    $docHistoryText = Read-TextIfExists -Path $docHistoryPath
    $updateIndexText = Read-TextIfExists -Path $updateIndexPath
    $docHistoryIndexText = Read-TextIfExists -Path $docHistoryIndexPath

    if ($ownershipText -and
        $ownershipText.Contains("UiPointerOwnershipService") -and
        $ownershipText.Contains("RegisterPointerOwnerForCurrentFrame") -and
        $ownershipText.Contains("ResolveWorldLeftDown") -and
        $ownershipText.Contains("IsLeftConsumedThisFrame") -and
        $ownershipText.Contains("PointerBlocksWorldLeft") -and
        $ownershipText.Contains("PointerBlocksHoverOrDrag") -and
        $ownershipText.Contains("OS physical buttons cannot be cleared") -and
        $ownershipText.Contains("LeftConsumed")) {
        Write-Pass "Blueprint UI click stage 02 has a shared frame-local pointer ownership and consumed-left primitive."
    }
    else {
        Write-FailHealth "Blueprint UI click stage 02 must keep UiPointerOwnershipService with frame-local ownership, consumed-left state, and OS-left revival gate."
    }

    if ($captureText -and
        $captureText.Contains("EnsureOperationWindowPointerOwned") -and
        $captureText.Contains("MarkOperationWindowLeftConsumed") -and
        $captureText.Contains("mouse-left-consumed")) {
        Write-Pass "UiMouseCaptureService preserves consumed-left ownership separately from capture cache."
    }
    else {
        Write-FailHealth "UiMouseCaptureService must preserve same-frame consumed-left ownership when MouseLeft is consumed."
    }

    if ($handheldText -and
        $handheldText.Contains("RegisterPointerOwnership") -and
        $handheldText.Contains("RegisterPointerOwnerForCurrentFrame") -and
        $handheldText.Contains("BlueprintHandheldActionBar") -and
        $handheldText.Contains("BuildPointerOwnerId")) {
        Write-Pass "Blueprint handheld action bar registers frame/button/blank-bar pointer ownership."
    }
    else {
        Write-FailHealth "Blueprint handheld action bar must register pointer ownership for button, disabled button, and blank bar hits."
    }

    $creationPointerGateOk =
        $creationText -and
        $creationText.Contains("UiPointerOwnershipService.ResolveWorldLeftDown(raw)") -and
        $creationText.Contains("UiPointerOwnershipService.ResolveWorldPointerOwnership(raw)") -and
        $creationText.Contains("pointerUiOwned") -and
        $creationText.Contains("pointerBlocksCreation") -and
        $creationText.Contains("legacyUiOwned || vanillaUiOwned || pointerBlocksHoverOrDrag") -and
        $creationText.Contains("ShouldBlockCreationForPointerOwnership") -and
        $creationText.Contains("ownership.PointerBlocksHoverOrDrag")
    $placementPointerGateOk =
        $placementText -and
        $placementText.Contains("UiPointerOwnershipService.ResolveWorldLeftDown(raw)") -and
        $placementText.Contains("UiPointerOwnershipService.ResolveWorldPointerOwnership(raw)") -and
        $placementText.Contains("pointerUiOwned") -and
        $placementText.Contains("pointerBlocksPlacement") -and
        $placementText.Contains("legacyUiOwned || vanillaUiOwned || pointerBlocksPlacement") -and
        $placementText.Contains("ShouldBlockPlacementForPointerOwnership") -and
        $placementText.Contains("ownership.PointerBlocksWorldLeft || ownership.PointerBlocksHoverOrDrag")
    $erasePointerGateOk =
        $eraseText -and
        $eraseText.Contains("UiPointerOwnershipService.ResolveWorldLeftDown(raw)") -and
        $eraseText.Contains("UiPointerOwnershipService.ResolveWorldPointerOwnership(raw)") -and
        $eraseText.Contains("pointerUiOwned") -and
        $eraseText.Contains("pointerBlocksErase") -and
        $eraseText.Contains("legacyUiOwned || vanillaUiOwned || pointerBlocksErase") -and
        $eraseText.Contains("ShouldBlockEraseForPointerOwnership") -and
        $eraseText.Contains("return ownership.PointerBlocksHoverOrDrag")
    $worldOverlayOk =
        $creationPointerGateOk -and
        $placementPointerGateOk -and
        $erasePointerGateOk
    if ($worldOverlayOk) {
        Write-Pass "Blueprint creation, placement, and erase world overlays query shared pointer ownership and split consumed-left from hover/drag ownership."
    }
    else {
        Write-FailHealth "Blueprint creation, placement, and erase overlays must all use shared pointer ownership and consumed-left gating."
    }

    if ($uiInputTestText -and $creationTestText -and $programText -and
        $uiInputTestText.Contains("UiPointerOwnershipConsumedLeftSurvivesCaptureReset") -and
        $creationTestText.Contains("BlueprintUiPointerOwnershipBlocksWorldOverlayClicks") -and
        $creationTestText.Contains("Expected UI-consumed OS left to be gated") -and
        $creationTestText.Contains("Expected UI-owned placement click to consume without confirming an instance") -and
        $creationTestText.Contains("Expected UI-owned erase click to consume without erasing an instance region") -and
        $programText.Contains("UI pointer ownership consumed left survives capture reset") -and
        $programText.Contains("blueprint UI pointer ownership blocks world overlay clicks")) {
        Write-Pass "Blueprint UI click stage 02 console tests cover consumed-left persistence, OS-left revival, creation mask, placement, and erase gates."
    }
    else {
        Write-FailHealth "Blueprint UI click stage 02 tests must cover consumed-left persistence, OS-left revival, creation mask, placement, and erase gates."
    }

    if ($functionDocText -and $diagnosticsDocText -and $plan02Text -and
        $functionDocText.Contains("UiPointerOwnershipService") -and
        $functionDocText.Contains("OS 物理左键") -and
        $diagnosticsDocText.Contains("0.902-blueprint-ui-pointer-ownership") -and
        $diagnosticsDocText.Contains("UiPointerOwnershipService") -and
        $plan02Text.Contains("状态：已完成") -and
        $plan02Text.Contains("UiPointerOwnershipService") -and
        $plan02Text.Contains("BlueprintUiPointerOwnershipBlocksWorldOverlayClicks") -and
        $plan02Text.Contains("Test-BlueprintUiClickStage02Governance")) {
        Write-Pass "Blueprint UI click stage 02 function, diagnostics, and plan docs are synchronized."
    }
    else {
        Write-FailHealth "Blueprint UI click stage 02 docs must describe shared ownership, OS-left revival gate, tests, health audit, and completed stage state."
    }

    if ($updateRecordText -and $docHistoryText -and $updateIndexText -and $docHistoryIndexText -and
        $updateRecordText.Contains("0.902-blueprint-ui-pointer-ownership") -and
        $updateRecordText.Contains("BlueprintUiPointerOwnershipBlocksWorldOverlayClicks") -and
        $updateRecordText.Contains("Test-BlueprintUiClickStage02Governance") -and
        $docHistoryText.Contains("UiPointerOwnershipService") -and
        $docHistoryText.Contains("Test-BlueprintUiClickStage02Governance") -and
        $updateIndexText.Contains("0.902-蓝图UI指针所有权闭环-2606212345.md") -and
        $docHistoryIndexText.Contains("蓝图UI指针所有权闭环-2606212345.md")) {
        Write-Pass "Blueprint UI click stage 02 update record and document history are synchronized."
    }
    else {
        Write-FailHealth "Blueprint UI click stage 02 must synchronize update index/record and document history with the ownership tests and health audit."
    }
}

function Test-BlueprintHotbarOwnershipStage03Governance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $handheldPath = Join-Path $RepoRoot "src\JueMingZ\UI\BlueprintHandheldActionBarOverlay.cs"
    $creationPath = Join-Path $RepoRoot "src\JueMingZ\UI\BlueprintCreationOverlay.cs"
    $placementPath = Join-Path $RepoRoot "src\JueMingZ\UI\BlueprintPlacementPreviewOverlay.cs"
    $erasePath = Join-Path $RepoRoot "src\JueMingZ\UI\BlueprintEraseRegionOverlay.cs"
    $aggregateTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintHandheldUiClickOwnershipTests.cs"
    $programPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.cs"
    $functionDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $diagnosticsDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "AI诊断日志说明.md")
    $plan03Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "蓝图快捷栏完全无效修复", "03-所有权登记与点穿阻断.md")
    if (-not (Test-Path -LiteralPath $plan03Path)) {
        $plan03Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图快捷栏完全无效修复", "03-所有权登记与点穿阻断.md")
    }
    $updateRecordPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "0.914-蓝图快捷栏所有权点穿阻断-2606220220.md")
    $updateIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "索引.md")
    $docHistoryPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "蓝图快捷栏所有权点穿阻断-2606220220.md")
    $docHistoryIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "索引.md")

    $handheldText = Read-TextIfExists -Path $handheldPath
    $creationText = Read-TextIfExists -Path $creationPath
    $placementText = Read-TextIfExists -Path $placementPath
    $eraseText = Read-TextIfExists -Path $erasePath
    $aggregateTestText = Read-TextIfExists -Path $aggregateTestPath
    $programText = Read-TextIfExists -Path $programPath
    $functionDocText = Read-TextIfExists -Path $functionDocPath
    $diagnosticsDocText = Read-TextIfExists -Path $diagnosticsDocPath
    $plan03Text = Read-TextIfExists -Path $plan03Path
    $updateRecordText = Read-TextIfExists -Path $updateRecordPath
    $updateIndexText = Read-TextIfExists -Path $updateIndexPath
    $docHistoryText = Read-TextIfExists -Path $docHistoryPath
    $docHistoryIndexText = Read-TextIfExists -Path $docHistoryIndexPath

    if ($handheldText -and
        $handheldText.Contains("RegisterPointerOwnershipForTesting") -and
        $handheldText.Contains("interaction.ShouldConsumeLeftInput || (mouse != null && mouse.LeftDown)") -and
        $handheldText.Contains("interaction.ShouldConsumeLeftInput") -and
        $handheldText.Contains("RegisterPointerOwnerForCurrentFrame")) {
        Write-Pass "Blueprint hotbar stage 03 registers handheld left-consumed ownership directly from bar hit interaction."
    }
    else {
        Write-FailHealth "Blueprint hotbar stage 03 must mark left-consumed ownership from handheld bar hit registration, before compat trigger cleanup."
    }

    $worldOverlayOk =
        $creationText -and $placementText -and $eraseText -and
        $creationText.Contains("UiPointerOwnershipService.ResolveWorldLeftDown(raw)") -and
        $placementText.Contains("UiPointerOwnershipService.ResolveWorldLeftDown(raw)") -and
        $eraseText.Contains("UiPointerOwnershipService.ResolveWorldLeftDown(raw)") -and
        $creationText.Contains("pointerUiOwned") -and
        $placementText.Contains("pointerUiOwned") -and
        $eraseText.Contains("pointerUiOwned")
    if ($worldOverlayOk) {
        Write-Pass "Blueprint hotbar stage 03 keeps creation, placement, and erase world overlays behind shared pointer ownership gates."
    }
    else {
        Write-FailHealth "Blueprint hotbar stage 03 must keep creation, placement, and erase world overlays behind shared pointer ownership gates."
    }

    if ($aggregateTestText -and $programText -and
        $aggregateTestText.Contains("BlueprintHandheldActionBarOwnershipRegistrationConsumesLeftForBarHits") -and
        $aggregateTestText.Contains("Expected outside handheld clicks to leave world overlay left input available") -and
        $aggregateTestText.Contains("Expected disabled save ownership registration test to avoid command click") -and
        $programText.Contains("blueprint handheld action bar ownership registration consumes left for bar hits")) {
        Write-Pass "Blueprint hotbar stage 03 console tests cover enabled, disabled, blank, and outside handheld ownership edges."
    }
    else {
        Write-FailHealth "Blueprint hotbar stage 03 tests must cover enabled, disabled, blank, and outside handheld ownership edges."
    }

    if ($functionDocText -and $diagnosticsDocText -and $plan03Text -and
        $functionDocText.Contains("0.914-blueprint-hotbar-ownership-gate") -and
        $functionDocText.Contains("BlueprintHandheldActionBarOwnershipRegistrationConsumesLeftForBarHits") -and
        $diagnosticsDocText.Contains("0.914-blueprint-hotbar-ownership-gate") -and
        $diagnosticsDocText.Contains("BlueprintHandheldActionBarOwnershipRegistrationConsumesLeftForBarHits") -and
        $plan03Text.Contains("状态：已完成") -and
        $plan03Text.Contains('RuntimeVersion：`0.914-blueprint-hotbar-ownership-gate`') -and
        $plan03Text.Contains("BlueprintHandheldActionBarOwnershipRegistrationConsumesLeftForBarHits") -and
        $plan03Text.Contains("Test-BlueprintHotbarOwnershipStage03Governance")) {
        Write-Pass "Blueprint hotbar stage 03 function, diagnostics, and plan docs are synchronized."
    }
    else {
        Write-FailHealth "Blueprint hotbar stage 03 docs must describe RuntimeVersion, ownership registration test, scoped health audit, and completed state."
    }

    if ($updateRecordText -and $updateIndexText -and $docHistoryText -and $docHistoryIndexText -and
        $updateRecordText.Contains("0.914-blueprint-hotbar-ownership-gate") -and
        $updateRecordText.Contains("BlueprintHandheldActionBarOwnershipRegistrationConsumesLeftForBarHits") -and
        $updateRecordText.Contains("Test-BlueprintHotbarOwnershipStage03Governance") -and
        $updateRecordText.Contains("不生成测试包") -and
        $docHistoryText.Contains("BlueprintHandheldActionBarOwnershipRegistrationConsumesLeftForBarHits") -and
        $docHistoryText.Contains("Test-BlueprintHotbarOwnershipStage03Governance") -and
        $updateIndexText.Contains("0.914-蓝图快捷栏所有权点穿阻断-2606220220.md") -and
        $docHistoryIndexText.Contains("蓝图快捷栏所有权点穿阻断-2606220220.md")) {
        Write-Pass "Blueprint hotbar stage 03 update record and document history are synchronized."
    }
    else {
        Write-FailHealth "Blueprint hotbar stage 03 must synchronize update record/index and document history with the ownership audit."
    }
}

function Test-BlueprintHotbarDeadClickStage04Governance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $handheldPath = Join-Path $RepoRoot "src\JueMingZ\UI\BlueprintHandheldActionBarOverlay.cs"
    $legacyInputPath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyUiInput.Mouse.cs"
    $diagnosticMouseReaderPath = Join-Path $RepoRoot "src\JueMingZ\UI\DiagnosticMouseStateReader.cs"
    $creationPath = Join-Path $RepoRoot "src\JueMingZ\UI\BlueprintCreationOverlay.cs"
    $placementPath = Join-Path $RepoRoot "src\JueMingZ\UI\BlueprintPlacementPreviewOverlay.cs"
    $erasePath = Join-Path $RepoRoot "src\JueMingZ\UI\BlueprintEraseRegionOverlay.cs"
    $aggregateTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintHandheldUiClickOwnershipTests.cs"
    $programPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.cs"
    $functionDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $diagnosticsDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "AI诊断日志说明.md")
    $plan00Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "蓝图快捷栏完全无效修复", "00-基准.md")
    if (-not (Test-Path -LiteralPath $plan00Path)) {
        $plan00Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图快捷栏完全无效修复", "00-基准.md")
    }
    $plan04Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "蓝图快捷栏完全无效修复", "04-诊断测试与审计防线.md")
    if (-not (Test-Path -LiteralPath $plan04Path)) {
        $plan04Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图快捷栏完全无效修复", "04-诊断测试与审计防线.md")
    }
    $currentPlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "索引.md")
    $archivePlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "索引.md")
    $updateRecordPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "0.915-蓝图快捷栏诊断测试审计-2606220231.md")
    $updateIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "索引.md")
    $docHistoryPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "蓝图快捷栏诊断测试审计-2606220231.md")
    $docHistoryIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "索引.md")

    $handheldText = Read-TextIfExists -Path $handheldPath
    $legacyInputText = Read-TextIfExists -Path $legacyInputPath
    $diagnosticMouseReaderText = Read-TextIfExists -Path $diagnosticMouseReaderPath
    $creationText = Read-TextIfExists -Path $creationPath
    $placementText = Read-TextIfExists -Path $placementPath
    $eraseText = Read-TextIfExists -Path $erasePath
    $aggregateTestText = Read-TextIfExists -Path $aggregateTestPath
    $programText = Read-TextIfExists -Path $programPath
    $functionDocText = Read-TextIfExists -Path $functionDocPath
    $diagnosticsDocText = Read-TextIfExists -Path $diagnosticsDocPath
    $plan00Text = Read-TextIfExists -Path $plan00Path
    $plan04Text = Read-TextIfExists -Path $plan04Path
    $currentPlanIndexText = Read-TextIfExists -Path $currentPlanIndexPath
    $archivePlanIndexText = Read-TextIfExists -Path $archivePlanIndexPath
    $updateRecordText = Read-TextIfExists -Path $updateRecordPath
    $updateIndexText = Read-TextIfExists -Path $updateIndexPath
    $docHistoryText = Read-TextIfExists -Path $docHistoryPath
    $docHistoryIndexText = Read-TextIfExists -Path $docHistoryIndexPath

    $hotbarInputOk =
        $handheldText -and $legacyInputText -and
        $handheldText.Contains("BuildFrameForTesting") -and
        $handheldText.Contains("RegisterPointerOwnershipForTesting") -and
        $diagnosticMouseReaderText -and
        $diagnosticMouseReaderText.Contains("ReadForBlueprintHandheldActionBarOverlay") -and
        $diagnosticMouseReaderText.Contains("BlueprintHandheldOverlayGateBypass") -and
        $legacyInputText.Contains("ReadMouseForBlueprintHandheldOverlay") -and
        $legacyInputText.Contains("ReadAvailable")
    if ($hotbarInputOk) {
        Write-Pass "Blueprint hotbar dead-click stage 04 keeps frame hit-test, handheld reader, and ownership seams available."
    }
    else {
        Write-FailHealth "Blueprint hotbar dead-click stage 04 must keep frame hit-test, handheld reader, and ownership seams available."
    }

    $worldOverlayOk =
        $creationText -and $placementText -and $eraseText -and
        $creationText.Contains("UiPointerOwnershipService.ResolveWorldLeftDown(raw)") -and
        $placementText.Contains("UiPointerOwnershipService.ResolveWorldLeftDown(raw)") -and
        $eraseText.Contains("UiPointerOwnershipService.ResolveWorldLeftDown(raw)") -and
        $creationText.Contains("pointerUiOwned") -and
        $placementText.Contains("pointerUiOwned") -and
        $eraseText.Contains("pointerUiOwned")
    if ($worldOverlayOk) {
        Write-Pass "Blueprint hotbar dead-click stage 04 keeps creation, placement, and erase world overlay gates wired."
    }
    else {
        Write-FailHealth "Blueprint hotbar dead-click stage 04 must keep creation, placement, and erase world overlay gates wired."
    }

    if ($aggregateTestText -and $programText -and
        $aggregateTestText.Contains("BlueprintHotbarDeadClickRegressionContractsStayWired") -and
        $aggregateTestText.Contains("BlueprintHotbarPhysicalCoordinateRegressionContractsStayWired") -and
        $aggregateTestText.Contains("BlueprintHandheldActionBarPhysicalBottomCenterRejectsUiScaleLogicalExtent") -and
        $aggregateTestText.Contains("BlueprintHandheldUiClickOwnershipContractsStayWired") -and
        $aggregateTestText.Contains("BlueprintHotbarDeadClickHotkeyPathStaysIndependentOfHandheldOwnership") -and
        $aggregateTestText.Contains("ScenarioNames.BlueprintActionHotkey") -and
        $programText.Contains("blueprint hotbar dead click regression contracts stay wired")) {
        Write-Pass "Blueprint hotbar dead-click stage 04 aggregate regression is registered and now reuses the corrected physical-coordinate hit-test, ownership, world gates, diagnostics, and G hotkey adjacency."
    }
    else {
        Write-FailHealth "Blueprint hotbar dead-click stage 04 must register BlueprintHotbarDeadClickRegressionContractsStayWired and reuse the corrected physical-coordinate, ownership, and hotkey-adjacent contracts."
    }

    if ($functionDocText -and $diagnosticsDocText -and
        $functionDocText.Contains("0.915-blueprint-hotbar-diagnostics-audit") -and
        $functionDocText.Contains("BlueprintHotbarDeadClickRegressionContractsStayWired") -and
        $functionDocText.Contains("Test-BlueprintHotbarDeadClickStage04Governance") -and
        $diagnosticsDocText.Contains("0.915-blueprint-hotbar-diagnostics-audit") -and
        $diagnosticsDocText.Contains("DiagnosticLifecycle=Stabilization") -and
        $diagnosticsDocText.Contains("BlueprintHotbarDeadClickRegressionContractsStayWired") -and
        $diagnosticsDocText.Contains("Test-BlueprintHotbarDeadClickStage04Governance")) {
        Write-Pass "Blueprint hotbar dead-click stage 04 function and diagnostics docs describe the aggregate audit and stabilization exit policy."
    }
    else {
        Write-FailHealth "Blueprint hotbar dead-click stage 04 docs must describe the 0.915 aggregate audit, scoped health audit, and stabilization exit policy."
    }

    $stage04BeforeCloseout =
        $currentPlanIndexText -and
        $currentPlanIndexText.Contains('`04-诊断测试与审计防线.md` 已完成') -and
        $currentPlanIndexText.Contains('下一阶段唯一入口为 `05-验证打包与归档收口.md`') -and
        $currentPlanIndexText.Contains('用户本轮明确要求不要启动 `05`')
    $stage04AfterCloseout =
        $plan00Text -and $currentPlanIndexText -and $archivePlanIndexText -and
        $plan00Text.Contains('状态：`05-验证打包与归档收口` 已完成') -and
        $plan00Text.Contains('0.916-blueprint-hotbar-closeout') -and
        $currentPlanIndexText.Contains("文档/归档历史计划/蓝图快捷栏完全无效修复/") -and
        $archivePlanIndexText.Contains("0.916-blueprint-hotbar-closeout")
    if ($plan04Text -and
        $plan04Text.Contains("状态：已完成") -and
        $plan04Text.Contains('RuntimeVersion：`0.915-blueprint-hotbar-diagnostics-audit`') -and
        $plan04Text.Contains("BlueprintHotbarDeadClickRegressionContractsStayWired") -and
        $plan04Text.Contains("Test-BlueprintHotbarDeadClickStage04Governance") -and
        $plan04Text.Contains("未新增 AI 经验笔记") -and
        $plan04Text.Contains('用户本轮明确要求不要启动 `05`') -and
        ($stage04BeforeCloseout -or $stage04AfterCloseout)) {
        Write-Pass "Blueprint hotbar dead-click stage 04 plan files record completion, aggregate audit, no-experience-note review, and explicit 05 handoff/archive state."
    }
    else {
        Write-FailHealth "Blueprint hotbar dead-click stage 04 plan files must record completion, aggregate audit, no-experience-note review, and explicit 05 handoff/archive state."
    }

    if ($updateRecordText -and $updateIndexText -and $docHistoryText -and $docHistoryIndexText -and
        $updateRecordText.Contains("0.915-blueprint-hotbar-diagnostics-audit") -and
        $updateRecordText.Contains("BlueprintHotbarDeadClickRegressionContractsStayWired") -and
        $updateRecordText.Contains("Test-BlueprintHotbarDeadClickStage04Governance") -and
        $updateRecordText.Contains("不生成测试包") -and
        $updateRecordText.Contains("未新增 AI 经验笔记") -and
        $updateIndexText.Contains("0.915-蓝图快捷栏诊断测试审计-2606220231.md") -and
        $docHistoryText.Contains("BlueprintHotbarDeadClickRegressionContractsStayWired") -and
        $docHistoryText.Contains("Test-BlueprintHotbarDeadClickStage04Governance") -and
        $docHistoryIndexText.Contains("蓝图快捷栏诊断测试审计-2606220231.md")) {
        Write-Pass "Blueprint hotbar dead-click stage 04 update record and document history are synchronized."
    }
    else {
        Write-FailHealth "Blueprint hotbar dead-click stage 04 must synchronize update record/index and document history with the aggregate audit."
    }
}

function Test-BlueprintHotbarPhysicalCoordinateStage04Governance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $auditPath = Join-Path $RepoRoot "scripts\audit-project-health.ps1"
    $handheldTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintHandheldActionBarTests.cs"
    $aggregateTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintHandheldUiClickOwnershipTests.cs"
    $programPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.cs"
    $functionDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $diagnosticsDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "AI诊断日志说明.md")
    $plan00Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "蓝图快捷栏物理坐标闭环修复", "00-基准.md")
    if (-not (Test-Path -LiteralPath $plan00Path)) {
        $plan00Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图快捷栏物理坐标闭环修复", "00-基准.md")
    }

    $plan04Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "蓝图快捷栏物理坐标闭环修复", "04-回归测试与审计防线.md")
    if (-not (Test-Path -LiteralPath $plan04Path)) {
        $plan04Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图快捷栏物理坐标闭环修复", "04-回归测试与审计防线.md")
    }

    $currentPlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "索引.md")
    $archivePlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "索引.md")
    $updateRecordPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "0.921-蓝图快捷栏物理回归审计-2606220505.md")
    $updateIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "索引.md")
    $docHistoryPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "蓝图快捷栏物理回归审计-2606220505.md")
    $docHistoryIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "索引.md")

    $auditText = Read-TextIfExists -Path $auditPath
    $handheldTestText = Read-TextIfExists -Path $handheldTestPath
    $aggregateTestText = Read-TextIfExists -Path $aggregateTestPath
    $programText = Read-TextIfExists -Path $programPath
    $functionDocText = Read-TextIfExists -Path $functionDocPath
    $diagnosticsDocText = Read-TextIfExists -Path $diagnosticsDocPath
    $plan00Text = Read-TextIfExists -Path $plan00Path
    $plan04Text = Read-TextIfExists -Path $plan04Path
    $currentPlanIndexText = Read-TextIfExists -Path $currentPlanIndexPath
    $archivePlanIndexText = Read-TextIfExists -Path $archivePlanIndexPath
    $updateRecordText = Read-TextIfExists -Path $updateRecordPath
    $updateIndexText = Read-TextIfExists -Path $updateIndexPath
    $docHistoryText = Read-TextIfExists -Path $docHistoryPath
    $docHistoryIndexText = Read-TextIfExists -Path $docHistoryIndexPath

    if ($handheldTestText -and
        $handheldTestText.Contains("BlueprintHandheldActionBarPhysicalBottomCenterRejectsUiScaleLogicalExtent") -and
        $handheldTestText.Contains("old UI-scale logical extent false positive") -and
        $handheldTestText.Contains("expectedLogicalHeight") -and
        $handheldTestText.Contains("AssertBlueprintHandheldPhysicalLayout") -and
        $handheldTestText.Contains("AssertDoesNotContain(mouse.ReadMode, " + [char]34 + "ScreenToUi" + [char]34 + ")")) {
        Write-Pass "Blueprint physical coordinate stage 04 locks physical bottom-center layout and the old UI-scale logical false-positive counterexample."
    }
    else {
        Write-FailHealth "Blueprint physical coordinate stage 04 must lock physical bottom-center layout and reject the old UI-scale logical false positive."
    }

    if ($aggregateTestText -and $programText -and
        $aggregateTestText.Contains("BlueprintHotbarPhysicalCoordinateRegressionContractsStayWired") -and
        $aggregateTestText.Contains("BlueprintHandheldActionBarPhysicalBottomCenterRejectsUiScaleLogicalExtent") -and
        $aggregateTestText.Contains("BlueprintHandheldUiClickOwnershipContractsStayWired") -and
        $aggregateTestText.Contains("BlueprintHotbarDeadClickHotkeyPathStaysIndependentOfHandheldOwnership") -and
        $programText.Contains("blueprint hotbar physical coordinate regression contracts stay wired")) {
        Write-Pass "Blueprint physical coordinate stage 04 aggregate regression covers physical position, click hit-test, ownership/world gates, and G hotkey adjacency."
    }
    else {
        Write-FailHealth "Blueprint physical coordinate stage 04 must register the aggregate physical-coordinate regression with ownership, world-gate, and hotkey adjacency coverage."
    }

    if ($auditText -and
        $auditText.Contains("Test-BlueprintHotbarPhysicalCoordinateStage04Governance") -and
        $auditText.Contains("0.921-blueprint-hotbar-physical-regression-audit") -and
        $auditText.Contains("0.921-蓝图快捷栏物理回归审计-2606220505.md")) {
        Write-Pass "Blueprint physical coordinate stage 04 health audit function is present and wired to the 0.921 stage contract."
    }
    else {
        Write-FailHealth "Blueprint physical coordinate stage 04 health audit must be wired to the 0.921 stage contract."
    }

    if ($functionDocText -and $diagnosticsDocText -and
        $functionDocText.Contains("0.921-blueprint-hotbar-physical-regression-audit") -and
        $functionDocText.Contains("0.916") -and
        $functionDocText.Contains("旧测试假阳性") -and
        $functionDocText.Contains("物理坐标闭环") -and
        $functionDocText.Contains("BlueprintHotbarPhysicalCoordinateRegressionContractsStayWired") -and
        $functionDocText.Contains("Test-BlueprintHotbarPhysicalCoordinateStage04Governance") -and
        $diagnosticsDocText.Contains("0.921-blueprint-hotbar-physical-regression-audit") -and
        $diagnosticsDocText.Contains("0.916") -and
        $diagnosticsDocText.Contains("旧测试假阳性") -and
        $diagnosticsDocText.Contains("物理坐标闭环") -and
        $diagnosticsDocText.Contains("BlueprintHotbarPhysicalCoordinateRegressionContractsStayWired") -and
        $diagnosticsDocText.Contains("Test-BlueprintHotbarPhysicalCoordinateStage04Governance")) {
        Write-Pass "Blueprint feature and diagnostics docs describe the physical coordinate regression audit and old false-positive guard."
    }
    else {
        Write-FailHealth "Blueprint feature and diagnostics docs must describe the 0.921 physical coordinate regression audit, 0.916 failure, and old false-positive guard."
    }

    $stage04BeforeCloseout =
        $currentPlanIndexText -and
        $currentPlanIndexText.Contains('下一阶段唯一入口为 `05-验证打包与归档收口.md`') -and
        $currentPlanIndexText.Contains("0.921-blueprint-hotbar-physical-regression-audit")
    $stage04AfterCloseout =
        $plan00Text -and $archivePlanIndexText -and
        $plan00Text.Contains('状态：`05-验证打包与归档收口` 已完成') -and
        $plan00Text.Contains("0.922-blueprint-hotbar-physical-closeout") -and
        $archivePlanIndexText.Contains("0.922-blueprint-hotbar-physical-closeout")

    if ($plan00Text -and $plan04Text -and
        $plan00Text.Contains('`04` 已完成') -and
        $plan00Text.Contains("0.921-blueprint-hotbar-physical-regression-audit") -and
        $plan04Text.Contains("状态：已完成") -and
        $plan04Text.Contains('RuntimeVersion：`0.921-blueprint-hotbar-physical-regression-audit`') -and
        $plan04Text.Contains("BlueprintHotbarPhysicalCoordinateRegressionContractsStayWired") -and
        $plan04Text.Contains("Test-BlueprintHotbarPhysicalCoordinateStage04Governance") -and
        $plan04Text.Contains("未生成测试包") -and
        $plan04Text.Contains("接力自检") -and
        ($stage04BeforeCloseout -or $stage04AfterCloseout)) {
        Write-Pass "Blueprint physical coordinate stage 04 plan files record completion, no-package scope, and the 05 handoff/archive state."
    }
    else {
        Write-FailHealth "Blueprint physical coordinate stage 04 plan files must record completion, no-package scope, and the 05 handoff/archive state."
    }

    if ($updateRecordText -and $updateIndexText -and $docHistoryText -and $docHistoryIndexText -and
        $updateRecordText.Contains('RuntimeVersion：`0.921-blueprint-hotbar-physical-regression-audit`') -and
        $updateRecordText.Contains("BlueprintHotbarPhysicalCoordinateRegressionContractsStayWired") -and
        $updateRecordText.Contains("Test-BlueprintHotbarPhysicalCoordinateStage04Governance") -and
        $updateRecordText.Contains("不生成测试包") -and
        $updateRecordText.Contains("旧测试假阳性") -and
        $updateIndexText.Contains("0.921-蓝图快捷栏物理回归审计-2606220505.md") -and
        $docHistoryText.Contains("BlueprintHotbarPhysicalCoordinateRegressionContractsStayWired") -and
        $docHistoryText.Contains("Test-BlueprintHotbarPhysicalCoordinateStage04Governance") -and
        $docHistoryIndexText.Contains("蓝图快捷栏物理回归审计-2606220505.md")) {
        Write-Pass "Blueprint physical coordinate stage 04 update record and document-change history are synchronized."
    }
    else {
        Write-FailHealth "Blueprint physical coordinate stage 04 update record and document-change history must be synchronized."
    }
}

function Test-BlueprintHotbarPhysicalCoordinateStage05CloseoutGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $auditPath = Join-Path $RepoRoot "scripts\audit-project-health.ps1"
    $currentPlanDirPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "蓝图快捷栏物理坐标闭环修复")
    $archivedPlan00Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图快捷栏物理坐标闭环修复", "00-基准.md")
    $archivedPlan05Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图快捷栏物理坐标闭环修复", "05-验证打包与归档收口.md")
    $currentPlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "索引.md")
    $archivePlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "索引.md")
    $blueprintDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $diagnosticsDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "AI诊断日志说明.md")
    $updateIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "索引.md")
    $updateRecordPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "0.922-蓝图快捷栏物理坐标验证收口-2606220520.md")
    $docHistoryIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "索引.md")
    $docHistoryRecordPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "蓝图快捷栏物理坐标验证收口-2606220520.md")

    $auditText = Read-TextIfExists -Path $auditPath
    $plan00Text = Read-TextIfExists -Path $archivedPlan00Path
    $plan05Text = Read-TextIfExists -Path $archivedPlan05Path
    $currentPlanIndexText = Read-TextIfExists -Path $currentPlanIndexPath
    $archivePlanIndexText = Read-TextIfExists -Path $archivePlanIndexPath
    $blueprintDocText = Read-TextIfExists -Path $blueprintDocPath
    $diagnosticsDocText = Read-TextIfExists -Path $diagnosticsDocPath
    $updateIndexText = Read-TextIfExists -Path $updateIndexPath
    $updateRecordText = Read-TextIfExists -Path $updateRecordPath
    $docHistoryIndexText = Read-TextIfExists -Path $docHistoryIndexPath
    $docHistoryRecordText = Read-TextIfExists -Path $docHistoryRecordPath

    if ($auditText -and
        $auditText.Contains("Test-BlueprintHotbarPhysicalCoordinateStage05CloseoutGovernance") -and
        $auditText.Contains("0.922-blueprint-hotbar-physical-closeout") -and
        $auditText.Contains("0.922-蓝图快捷栏物理坐标验证收口-2606220520.md")) {
        Write-Pass "Blueprint physical-coordinate stage-05 closeout health audit is present and wired to the 0.922 closeout contract."
    }
    else {
        Write-FailHealth "Blueprint physical-coordinate stage-05 closeout health audit must lock the 0.922 closeout contract and update record."
    }

    if (-not (Test-Path -LiteralPath $currentPlanDirPath) -and
        $plan00Text -and
        $plan00Text.Contains('状态：`05-验证打包与归档收口` 已完成') -and
        $plan00Text.Contains("0.922-blueprint-hotbar-physical-closeout") -and
        $plan00Text.Contains("自动串行接力终止") -and
        $plan05Text -and
        $plan05Text.Contains("状态：已完成") -and
        $plan05Text.Contains("0.922-blueprint-hotbar-physical-closeout") -and
        $plan05Text.Contains("JueMingZ-TestPackage") -and
        $plan05Text.Contains("严格新鲜包健康审计") -and
        $plan05Text.Contains("不再创建新对话")) {
        Write-Pass "Blueprint physical-coordinate plan is archived with the 0.922 closeout, package delivery, and no further handoff."
    }
    else {
        Write-FailHealth "Stage-05 closeout must move the blueprint physical-coordinate plan to archive and mark 05 complete with package/fresh-audit scope."
    }

    if ($currentPlanIndexText -and
        $currentPlanIndexText.Contains("当前没有正在推进的计划") -and
        $currentPlanIndexText.Contains("文档/归档历史计划/蓝图快捷栏物理坐标闭环修复/") -and
        $archivePlanIndexText -and
        $archivePlanIndexText.Contains("文档/归档历史计划/蓝图快捷栏物理坐标闭环修复/") -and
        $archivePlanIndexText.Contains("0.922-blueprint-hotbar-physical-closeout") -and
        $archivePlanIndexText.Contains("自动接力已终止")) {
        Write-Pass "Current and archived plan indices record the blueprint physical-coordinate closeout and relay termination."
    }
    else {
        Write-FailHealth "Stage-05 physical-coordinate closeout must update current and archived plan indices."
    }

    if ($blueprintDocText -and
        $blueprintDocText.Contains("0.922-blueprint-hotbar-physical-closeout") -and
        $blueprintDocText.Contains("文档/归档历史计划/蓝图快捷栏物理坐标闭环修复/00-基准.md") -and
        $blueprintDocText.Contains("不新增蓝图用户可见行为") -and
        $blueprintDocText.Contains("真实屏幕底部居中") -and
        $diagnosticsDocText -and
        $diagnosticsDocText.Contains("0.922-blueprint-hotbar-physical-closeout") -and
        $diagnosticsDocText.Contains("不新增诊断字段") -and
        $diagnosticsDocText.Contains("真实屏幕底部居中")) {
        Write-Pass "Blueprint feature and diagnostics docs record the 0.922 physical-coordinate closeout without expanding runtime or diagnostics scope."
    }
    else {
        Write-FailHealth "Stage-05 physical-coordinate closeout must update blueprint feature and diagnostics docs with no-new-runtime/no-new-diagnostics scope."
    }

    if ($updateIndexText -and
        $updateIndexText.Contains("0.922-蓝图快捷栏物理坐标验证收口-2606220520.md") -and
        $updateRecordText -and
        $updateRecordText.Contains('RuntimeVersion：`0.922-blueprint-hotbar-physical-closeout`') -and
        $updateRecordText.Contains("JueMingZ-TestPackage") -and
        $updateRecordText.Contains("-RequireFreshTestPackage") -and
        $docHistoryIndexText -and
        $docHistoryIndexText.Contains("蓝图快捷栏物理坐标验证收口-2606220520.md") -and
        $docHistoryRecordText -and
        $docHistoryRecordText.Contains("0.922-blueprint-hotbar-physical-closeout")) {
        Write-Pass "Stage-05 blueprint physical-coordinate update record and document-change history are synchronized."
    }
    else {
        Write-FailHealth "Stage-05 physical-coordinate update record, update index, and document-change history must reference the 0.922 closeout."
    }
}

function Test-BlueprintHotbarDeadClickStage05CloseoutGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $auditPath = Join-Path $RepoRoot "scripts\audit-project-health.ps1"
    $currentPlanDirPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "蓝图快捷栏完全无效修复")
    $archivedPlan00Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图快捷栏完全无效修复", "00-基准.md")
    $archivedPlan05Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图快捷栏完全无效修复", "05-验证打包与归档收口.md")
    $currentPlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "索引.md")
    $archivePlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "索引.md")
    $blueprintDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $diagnosticsDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "AI诊断日志说明.md")
    $updateIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "索引.md")
    $updateRecordPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "0.916-蓝图快捷栏验证打包收口-2606220344.md")
    $docHistoryIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "索引.md")
    $docHistoryRecordPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "蓝图快捷栏验证打包收口-2606220344.md")

    $auditText = Read-TextIfExists -Path $auditPath
    $plan00Text = Read-TextIfExists -Path $archivedPlan00Path
    $plan05Text = Read-TextIfExists -Path $archivedPlan05Path
    $currentPlanIndexText = Read-TextIfExists -Path $currentPlanIndexPath
    $archivePlanIndexText = Read-TextIfExists -Path $archivePlanIndexPath
    $blueprintDocText = Read-TextIfExists -Path $blueprintDocPath
    $diagnosticsDocText = Read-TextIfExists -Path $diagnosticsDocPath
    $updateIndexText = Read-TextIfExists -Path $updateIndexPath
    $updateRecordText = Read-TextIfExists -Path $updateRecordPath
    $docHistoryIndexText = Read-TextIfExists -Path $docHistoryIndexPath
    $docHistoryRecordText = Read-TextIfExists -Path $docHistoryRecordPath

    if ($auditText -and
        $auditText.Contains("Test-BlueprintHotbarDeadClickStage05CloseoutGovernance") -and
        $auditText.Contains("0.916-blueprint-hotbar-closeout") -and
        $auditText.Contains("0.916-蓝图快捷栏验证打包收口-2606220344.md")) {
        Write-Pass "Blueprint hotbar dead-click stage-05 closeout health audit is present and wired to the 0.916 closeout contract."
    }
    else {
        Write-FailHealth "Blueprint hotbar dead-click stage-05 closeout health audit must lock the 0.916 closeout contract and update record."
    }

    if (-not (Test-Path -LiteralPath $currentPlanDirPath) -and
        $plan00Text -and
        $plan00Text.Contains('状态：`05-验证打包与归档收口` 已完成') -and
        $plan00Text.Contains("0.916-blueprint-hotbar-closeout") -and
        $plan00Text.Contains("自动串行接力终止") -and
        $plan05Text -and
        $plan05Text.Contains("状态：已完成") -and
        $plan05Text.Contains("0.916-blueprint-hotbar-closeout") -and
        $plan05Text.Contains("JueMingZ-TestPackage") -and
        $plan05Text.Contains("严格新鲜包健康审计") -and
        $plan05Text.Contains("不再创建新对话")) {
        Write-Pass "Blueprint hotbar dead-click plan is archived with the 0.916 closeout, package delivery, and no further handoff."
    }
    else {
        Write-FailHealth "Stage-05 closeout must move the blueprint hotbar dead-click plan to archive and mark 05 complete with package/fresh-audit scope."
    }

    if ($currentPlanIndexText -and
        $currentPlanIndexText.Contains("当前没有正在推进的计划") -and
        $currentPlanIndexText.Contains("文档/归档历史计划/蓝图快捷栏完全无效修复/") -and
        $archivePlanIndexText -and
        $archivePlanIndexText.Contains("文档/归档历史计划/蓝图快捷栏完全无效修复/") -and
        $archivePlanIndexText.Contains("0.916-blueprint-hotbar-closeout") -and
        $archivePlanIndexText.Contains("自动接力已终止")) {
        Write-Pass "Current and archived plan indices record the blueprint hotbar closeout and relay termination."
    }
    else {
        Write-FailHealth "Stage-05 closeout must update current and archived plan indices."
    }

    if ($blueprintDocText -and
        $blueprintDocText.Contains("0.916-blueprint-hotbar-closeout") -and
        $blueprintDocText.Contains("文档/归档历史计划/蓝图快捷栏完全无效修复/00-基准.md") -and
        $blueprintDocText.Contains("不新增蓝图用户可见行为") -and
        $diagnosticsDocText -and
        $diagnosticsDocText.Contains("0.916-blueprint-hotbar-closeout") -and
        $diagnosticsDocText.Contains("不新增诊断字段")) {
        Write-Pass "Blueprint feature and diagnostics docs record the 0.916 closeout without expanding runtime or diagnostics scope."
    }
    else {
        Write-FailHealth "Stage-05 closeout must update blueprint feature and diagnostics docs with the no-new-runtime/no-new-diagnostics scope."
    }

    if ($updateIndexText -and
        $updateIndexText.Contains("0.916-蓝图快捷栏验证打包收口-2606220344.md") -and
        $updateRecordText -and
        $updateRecordText.Contains('RuntimeVersion：`0.916-blueprint-hotbar-closeout`') -and
        $updateRecordText.Contains("JueMingZ-TestPackage") -and
        $updateRecordText.Contains("-RequireFreshTestPackage") -and
        $docHistoryIndexText -and
        $docHistoryIndexText.Contains("蓝图快捷栏验证打包收口-2606220344.md") -and
        $docHistoryRecordText -and
        $docHistoryRecordText.Contains("0.916-blueprint-hotbar-closeout")) {
        Write-Pass "Stage-05 blueprint hotbar update record and document-change history are synchronized."
    }
    else {
        Write-FailHealth "Stage-05 update record, update index, and document-change history must reference the 0.916 closeout."
    }
}

function Test-BlueprintUiClickStage04Governance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $ownershipPath = Join-Path $RepoRoot "src\JueMingZ\UI\UiPointerOwnershipService.cs"
    $handheldPath = Join-Path $RepoRoot "src\JueMingZ\UI\BlueprintHandheldActionBarOverlay.cs"
    $creationPath = Join-Path $RepoRoot "src\JueMingZ\UI\BlueprintCreationOverlay.cs"
    $placementPath = Join-Path $RepoRoot "src\JueMingZ\UI\BlueprintPlacementPreviewOverlay.cs"
    $erasePath = Join-Path $RepoRoot "src\JueMingZ\UI\BlueprintEraseRegionOverlay.cs"
    $aggregateTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintHandheldUiClickOwnershipTests.cs"
    $programPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.cs"
    $functionDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $diagnosticsDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "AI诊断日志说明.md")
    $plan00Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "蓝图手持栏UI点击所有权治理", "00-基准.md")
    if (-not (Test-Path -LiteralPath $plan00Path)) {
        $plan00Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图手持栏UI点击所有权治理", "00-基准.md")
    }
    $plan04Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "蓝图手持栏UI点击所有权治理", "04-诊断测试与审计防线.md")
    if (-not (Test-Path -LiteralPath $plan04Path)) {
        $plan04Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图手持栏UI点击所有权治理", "04-诊断测试与审计防线.md")
    }
    $currentPlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "索引.md")
    $archivePlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "索引.md")
    $updateRecordPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "0.904-蓝图UI点击诊断审计-2606220014.md")
    $updateIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "索引.md")
    $docHistoryPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "蓝图UI点击诊断审计-2606220014.md")
    $docHistoryIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "索引.md")

    $ownershipText = Read-TextIfExists -Path $ownershipPath
    $handheldText = Read-TextIfExists -Path $handheldPath
    $creationText = Read-TextIfExists -Path $creationPath
    $placementText = Read-TextIfExists -Path $placementPath
    $eraseText = Read-TextIfExists -Path $erasePath
    $aggregateTestText = Read-TextIfExists -Path $aggregateTestPath
    $programText = Read-TextIfExists -Path $programPath
    $functionDocText = Read-TextIfExists -Path $functionDocPath
    $diagnosticsDocText = Read-TextIfExists -Path $diagnosticsDocPath
    $plan00Text = Read-TextIfExists -Path $plan00Path
    $plan04Text = Read-TextIfExists -Path $plan04Path
    $currentPlanIndexText = Read-TextIfExists -Path $currentPlanIndexPath
    $archivePlanIndexText = Read-TextIfExists -Path $archivePlanIndexPath
    $updateRecordText = Read-TextIfExists -Path $updateRecordPath
    $updateIndexText = Read-TextIfExists -Path $updateIndexPath
    $docHistoryText = Read-TextIfExists -Path $docHistoryPath
    $docHistoryIndexText = Read-TextIfExists -Path $docHistoryIndexPath

    $worldOverlayOk =
        $ownershipText -and
        $ownershipText.Contains("ResolveWorldLeftDown") -and
        $ownershipText.Contains("LeftConsumed") -and
        $handheldText -and
        $handheldText.Contains("RegisterPointerOwnership") -and
        $handheldText.Contains("BuildPointerOwnerId") -and
        $creationText -and $placementText -and $eraseText -and
        $creationText.Contains("UiPointerOwnershipService.ResolveWorldLeftDown(raw)") -and
        $placementText.Contains("UiPointerOwnershipService.ResolveWorldLeftDown(raw)") -and
        $eraseText.Contains("UiPointerOwnershipService.ResolveWorldLeftDown(raw)") -and
        $creationText.Contains("UiPointerOwnershipService.ResolveWorldPointerOwnership(raw)") -and
        $placementText.Contains("UiPointerOwnershipService.ResolveWorldPointerOwnership(raw)") -and
        $eraseText.Contains("UiPointerOwnershipService.ResolveWorldPointerOwnership(raw)") -and
        $creationText.Contains("legacyUiOwned || vanillaUiOwned || pointerBlocksHoverOrDrag") -and
        $placementText.Contains("legacyUiOwned || vanillaUiOwned || pointerBlocksPlacement") -and
        $eraseText.Contains("legacyUiOwned || vanillaUiOwned || pointerBlocksErase") -and
        $creationText.Contains("ownership.PointerBlocksHoverOrDrag") -and
        $placementText.Contains("ownership.PointerBlocksWorldLeft || ownership.PointerBlocksHoverOrDrag") -and
        $eraseText.Contains("return ownership.PointerBlocksHoverOrDrag")
    if ($worldOverlayOk) {
        Write-Pass "Blueprint UI click stage 04 keeps the shared ownership primitive and all world overlay ownership queries wired with split world-left and hover blockers."
    }
    else {
        Write-FailHealth "Blueprint UI click stage 04 must keep UiPointerOwnershipService, handheld owner registration, and creation/placement/erase ownership queries wired."
    }

    if ($aggregateTestText -and $programText -and
        $aggregateTestText.Contains("BlueprintHandheldUiClickOwnershipContractsStayWired") -and
        $aggregateTestText.Contains("UiPointerOwnershipConsumedLeftSurvivesCaptureReset") -and
        $aggregateTestText.Contains("BlueprintUiPointerOwnershipBlocksWorldOverlayClicks") -and
        $aggregateTestText.Contains("BlueprintHandheldActionBarInputCapturesOnlyInsideBar") -and
        $aggregateTestText.Contains("BlueprintHandheldActionBarAfterPlayerInputGuardSubmitsFreshClickEdge") -and
        $aggregateTestText.Contains("BlueprintHandheldActionBarPostfixReplaysStalePrefixPress") -and
        $aggregateTestText.Contains("BlueprintHandheldActionBarRealCommandsAndDeferredPlacedCommands") -and
        $aggregateTestText.Contains("BlueprintHandheldActionBarDiagnosticsSnapshotJson") -and
        $programText.Contains("blueprint handheld UI click ownership contracts stay wired")) {
        Write-Pass "Blueprint UI click stage 04 aggregate console regression is registered and reuses the stage 02/03 behavior contracts."
    }
    else {
        Write-FailHealth "Blueprint UI click stage 04 must register BlueprintHandheldUiClickOwnershipContractsStayWired and reuse the stage 02/03 behavior tests."
    }

    if ($functionDocText -and $diagnosticsDocText -and
        $functionDocText.Contains("0.904-blueprint-ui-click-diagnostics-audit") -and
        $functionDocText.Contains("BlueprintHandheldUiClickOwnershipContractsStayWired") -and
        $functionDocText.Contains("Test-BlueprintUiClickStage04Governance") -and
        $diagnosticsDocText.Contains("0.904-blueprint-ui-click-diagnostics-audit") -and
        $diagnosticsDocText.Contains("BlueprintHandheldUiClickOwnershipContractsStayWired") -and
        $diagnosticsDocText.Contains("Test-BlueprintUiClickStage04Governance")) {
        Write-Pass "Blueprint function and diagnostics docs describe the stage 04 aggregate ownership audit."
    }
    else {
        Write-FailHealth "Blueprint function and diagnostics docs must describe the 0.904 stage 04 aggregate test and health audit."
    }

    $stage04BeforeCloseout =
        $currentPlanIndexText -and
        $plan00Text.Contains('`04` 已完成诊断测试与审计防线') -and
        $plan00Text.Contains('`05` 待执行') -and
        $currentPlanIndexText.Contains('下一步只执行 `05-验证打包与归档收口.md`')
    $stage04AfterCloseout =
        $currentPlanIndexText -and $archivePlanIndexText -and
        $plan00Text.Contains('状态：`05-验证打包与归档收口` 已完成') -and
        $plan00Text.Contains('0.905-blueprint-ui-click-closeout') -and
        $currentPlanIndexText.Contains("文档/归档历史计划/蓝图手持栏UI点击所有权治理/") -and
        $archivePlanIndexText.Contains("0.905-blueprint-ui-click-closeout")
    if ($plan00Text -and $plan04Text -and
        $plan04Text.Contains("状态：已完成") -and
        $plan04Text.Contains('RuntimeVersion：`0.904-blueprint-ui-click-diagnostics-audit`') -and
        $plan04Text.Contains("BlueprintHandheldUiClickOwnershipContractsStayWired") -and
        $plan04Text.Contains("Test-BlueprintUiClickStage04Governance") -and
        $plan04Text.Contains("未新增 AI 经验笔记") -and
        ($stage04BeforeCloseout -or $stage04AfterCloseout)) {
        Write-Pass "Blueprint UI click stage 04 plan files record completion, aggregate test, audit, and handoff state."
    }
    else {
        Write-FailHealth "Blueprint UI click stage 04 plan files must record completion, aggregate test, audit, no-experience-note review, and 05 handoff/archive state."
    }

    if ($updateRecordText -and $updateIndexText -and $docHistoryText -and $docHistoryIndexText -and
        $updateRecordText.Contains("0.904-blueprint-ui-click-diagnostics-audit") -and
        $updateRecordText.Contains("BlueprintHandheldUiClickOwnershipContractsStayWired") -and
        $updateRecordText.Contains("Test-BlueprintUiClickStage04Governance") -and
        $updateRecordText.Contains("不生成测试包") -and
        $updateRecordText.Contains("未新增 AI 经验笔记") -and
        $updateIndexText.Contains("0.904-蓝图UI点击诊断审计-2606220014.md") -and
        $docHistoryText.Contains("BlueprintHandheldUiClickOwnershipContractsStayWired") -and
        $docHistoryText.Contains("Test-BlueprintUiClickStage04Governance") -and
        $docHistoryIndexText.Contains("蓝图UI点击诊断审计-2606220014.md")) {
        Write-Pass "Blueprint UI click stage 04 update record and document history are synchronized."
    }
    else {
        Write-FailHealth "Blueprint UI click stage 04 must synchronize update record/index and document history with the aggregate test and health audit."
    }
}

function Test-BlueprintWorldOverlayPointerOwnershipStage05Governance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $creationPath = Join-Path $RepoRoot "src\JueMingZ\UI\BlueprintCreationOverlay.cs"
    $placementPath = Join-Path $RepoRoot "src\JueMingZ\UI\BlueprintPlacementPreviewOverlay.cs"
    $erasePath = Join-Path $RepoRoot "src\JueMingZ\UI\BlueprintEraseRegionOverlay.cs"
    $aggregateTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintHandheldUiClickOwnershipTests.cs"
    $programPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.cs"
    $functionDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $diagnosticsDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "AI诊断日志说明.md")
    $plan00Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "蓝图创建选区UI所有权误拦修复", "00-基准.md")
    if (-not (Test-Path -LiteralPath $plan00Path)) {
        $plan00Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图创建选区UI所有权误拦修复", "00-基准.md")
    }
    $plan05Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "蓝图创建选区UI所有权误拦修复", "05-回归测试与审计防线.md")
    if (-not (Test-Path -LiteralPath $plan05Path)) {
        $plan05Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图创建选区UI所有权误拦修复", "05-回归测试与审计防线.md")
    }
    $currentPlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "索引.md")
    $archivePlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "索引.md")
    $updateRecordPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "0.928-蓝图选区所有权回归审计-2606221548.md")
    $updateIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "索引.md")
    $docHistoryPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "蓝图选区所有权回归审计-2606221548.md")
    $docHistoryIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "索引.md")

    $creationText = Read-TextIfExists -Path $creationPath
    $placementText = Read-TextIfExists -Path $placementPath
    $eraseText = Read-TextIfExists -Path $erasePath
    $aggregateTestText = Read-TextIfExists -Path $aggregateTestPath
    $programText = Read-TextIfExists -Path $programPath
    $functionDocText = Read-TextIfExists -Path $functionDocPath
    $diagnosticsDocText = Read-TextIfExists -Path $diagnosticsDocPath
    $plan00Text = Read-TextIfExists -Path $plan00Path
    $plan05Text = Read-TextIfExists -Path $plan05Path
    $currentPlanIndexText = Read-TextIfExists -Path $currentPlanIndexPath
    $archivePlanIndexText = Read-TextIfExists -Path $archivePlanIndexPath
    $updateRecordText = Read-TextIfExists -Path $updateRecordPath
    $updateIndexText = Read-TextIfExists -Path $updateIndexPath
    $docHistoryText = Read-TextIfExists -Path $docHistoryPath
    $docHistoryIndexText = Read-TextIfExists -Path $docHistoryIndexPath

    $worldOverlayNarrowGateOk =
        $creationText -and $placementText -and $eraseText -and
        $creationText.Contains("UiPointerOwnershipService.ResolveWorldPointerOwnership(raw)") -and
        $placementText.Contains("UiPointerOwnershipService.ResolveWorldPointerOwnership(raw)") -and
        $eraseText.Contains("UiPointerOwnershipService.ResolveWorldPointerOwnership(raw)") -and
        $creationText.Contains("ShouldBlockCreationForPointerOwnership") -and
        $placementText.Contains("ShouldBlockPlacementForPointerOwnership") -and
        $eraseText.Contains("ShouldBlockEraseForPointerOwnership") -and
        $creationText.Contains("ownership.PointerBlocksHoverOrDrag") -and
        $placementText.Contains("ownership.PointerBlocksWorldLeft || ownership.PointerBlocksHoverOrDrag") -and
        $eraseText.Contains("return ownership.PointerBlocksHoverOrDrag")
    if ($worldOverlayNarrowGateOk) {
        Write-Pass "Blueprint world overlays keep split pointer ownership gates: creation/erase hover uses bounds hit, placement click gate still includes consumed-left."
    }
    else {
        Write-FailHealth "Blueprint creation, placement, and erase overlays must keep ResolveWorldPointerOwnership(raw) and split consumed-left from owner bounds hit."
    }

    if ($aggregateTestText -and $programText -and
        $aggregateTestText.Contains("BlueprintWorldOverlayPointerOwnershipContractsStayWired") -and
        $aggregateTestText.Contains("BlueprintHandheldUiClickOwnershipContractsStayWired") -and
        $aggregateTestText.Contains("BlueprintCreationPointerOwnershipNarrowingKeepsWorldHoverAndMask") -and
        $aggregateTestText.Contains("BlueprintPlacementErasePointerOwnershipNarrowingKeepsWorldInput") -and
        $aggregateTestText.Contains("BlueprintHandheldActionBarOwnershipRegistrationConsumesLeftForBarHits") -and
        $aggregateTestText.Contains("BlueprintHotbarDeadClickHotkeyPathStaysIndependentOfHandheldOwnership") -and
        $programText.Contains("blueprint world overlay pointer ownership contracts stay wired")) {
        Write-Pass "Blueprint stage-05 aggregate console regression is registered and reuses creation, placement/erase, handheld ownership, and hotkey-adjacent contracts."
    }
    else {
        Write-FailHealth "Blueprint stage-05 must register BlueprintWorldOverlayPointerOwnershipContractsStayWired and reuse the creation hover, placement/erase, handheld ownership, and Hotkey.BlueprintAction contracts."
    }

    if ($functionDocText -and $diagnosticsDocText -and
        $functionDocText.Contains("0.928-blueprint-world-overlay-ownership-audit") -and
        $functionDocText.Contains("BlueprintWorldOverlayPointerOwnershipContractsStayWired") -and
        $functionDocText.Contains("Test-BlueprintWorldOverlayPointerOwnershipStage05Governance") -and
        $diagnosticsDocText.Contains("0.928-blueprint-world-overlay-ownership-audit") -and
        $diagnosticsDocText.Contains("BlueprintWorldOverlayPointerOwnershipContractsStayWired") -and
        $diagnosticsDocText.Contains("Test-BlueprintWorldOverlayPointerOwnershipStage05Governance") -and
        $diagnosticsDocText.Contains("DiagnosticLifecycle=Stabilization")) {
        Write-Pass "Blueprint function and diagnostics docs describe the stage-05 world overlay pointer ownership audit."
    }
    else {
        Write-FailHealth "Blueprint function and diagnostics docs must describe 0.928, the aggregate regression, health audit, and stabilization diagnostics boundary."
    }

    $stage05BeforeCloseout =
        $currentPlanIndexText -and
        $plan00Text.Contains('`05-回归测试与审计防线` 已完成') -and
        $plan00Text.Contains('下一入口为 `06-验证打包与归档收口.md`') -and
        $currentPlanIndexText.Contains('下一阶段唯一入口是 `06-验证打包与归档收口.md`')
    $stage05AfterCloseout =
        $currentPlanIndexText -and $archivePlanIndexText -and
        $plan00Text.Contains('状态：`06-验证打包与归档收口` 已完成') -and
        $plan00Text.Contains('0.929') -and
        $currentPlanIndexText.Contains("文档/归档历史计划/蓝图创建选区UI所有权误拦修复/") -and
        $archivePlanIndexText.Contains("蓝图创建选区UI所有权误拦修复")
    if ($plan00Text -and $plan05Text -and
        $plan05Text.Contains("状态：已完成") -and
        $plan05Text.Contains('RuntimeVersion：`0.928-blueprint-world-overlay-ownership-audit`') -and
        $plan05Text.Contains("BlueprintWorldOverlayPointerOwnershipContractsStayWired") -and
        $plan05Text.Contains("Test-BlueprintWorldOverlayPointerOwnershipStage05Governance") -and
        $plan05Text.Contains("不生成测试包") -and
        $plan05Text.Contains("未新增 AI 经验笔记") -and
        ($stage05BeforeCloseout -or $stage05AfterCloseout)) {
        Write-Pass "Blueprint stage-05 plan files record completion, aggregate regression, scoped audit, no-package boundary, and 06 handoff/archive state."
    }
    else {
        Write-FailHealth "Blueprint stage-05 plan files must record completion, aggregate regression, health audit, no-package/no-experience-note review, and 06 handoff/archive state."
    }

    if ($updateRecordText -and $updateIndexText -and $docHistoryText -and $docHistoryIndexText -and
        $updateRecordText.Contains("0.928-blueprint-world-overlay-ownership-audit") -and
        $updateRecordText.Contains("BlueprintWorldOverlayPointerOwnershipContractsStayWired") -and
        $updateRecordText.Contains("Test-BlueprintWorldOverlayPointerOwnershipStage05Governance") -and
        $updateRecordText.Contains("不生成测试包") -and
        $updateRecordText.Contains("本地验证不等于用户实机验收") -and
        $updateRecordText.Contains("未新增 AI 经验笔记") -and
        $updateIndexText.Contains("0.928-蓝图选区所有权回归审计-2606221548.md") -and
        $docHistoryText.Contains("BlueprintWorldOverlayPointerOwnershipContractsStayWired") -and
        $docHistoryText.Contains("Test-BlueprintWorldOverlayPointerOwnershipStage05Governance") -and
        $docHistoryIndexText.Contains("蓝图选区所有权回归审计-2606221548.md")) {
        Write-Pass "Blueprint stage-05 update record and document-change history are synchronized."
    }
    else {
        Write-FailHealth "Blueprint stage-05 must synchronize update record/index and document-change history with the aggregate regression and health audit."
    }
}

function Get-BlueprintCreationFlickerPlanSegments {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $currentPlanDirPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "蓝图创建闪烁修复版")
    if (Test-Path -LiteralPath $currentPlanDirPath) {
        return @("当前在做计划", "蓝图创建闪烁修复版")
    }

    return @("归档历史计划", "蓝图创建闪烁修复版")
}

function Test-BlueprintCreationFlickerPointerOwnershipStage02Governance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $ownershipPath = Join-Path $RepoRoot "src\JueMingZ\UI\UiPointerOwnershipService.cs"
    $creationPath = Join-Path $RepoRoot "src\JueMingZ\UI\BlueprintCreationOverlay.cs"
    $placementPath = Join-Path $RepoRoot "src\JueMingZ\UI\BlueprintPlacementPreviewOverlay.cs"
    $erasePath = Join-Path $RepoRoot "src\JueMingZ\UI\BlueprintEraseRegionOverlay.cs"
    $diagnosticsPath = Join-Path $RepoRoot "src\JueMingZ\Automation\Blueprint\BlueprintUiClickDiagnostics.cs"
    $testPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintHandheldUiClickOwnershipTests.cs"
    $programPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.cs"
    $functionDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $diagnosticsDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "AI诊断日志说明.md")
    $planRootSegments = Get-BlueprintCreationFlickerPlanSegments -RepoRoot $RepoRoot
    $plan00Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments ($planRootSegments + @("00-基准.md"))
    $plan02Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments ($planRootSegments + @("02-pointer所有权语义拆分.md"))
    $currentPlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "索引.md")
    $updateRecordPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "0.938-蓝图pointer所有权语义拆分-2606222329.md")
    $updateIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "索引.md")
    $docHistoryPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "蓝图pointer所有权语义拆分-2606222329.md")
    $docHistoryIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "索引.md")

    $ownershipText = Read-TextIfExists -Path $ownershipPath
    $creationText = Read-TextIfExists -Path $creationPath
    $placementText = Read-TextIfExists -Path $placementPath
    $eraseText = Read-TextIfExists -Path $erasePath
    $diagnosticsText = Read-TextIfExists -Path $diagnosticsPath
    $testText = Read-TextIfExists -Path $testPath
    $programText = Read-TextIfExists -Path $programPath
    $functionDocText = Read-TextIfExists -Path $functionDocPath
    $diagnosticsDocText = Read-TextIfExists -Path $diagnosticsDocPath
    $plan00Text = Read-TextIfExists -Path $plan00Path
    $plan02Text = Read-TextIfExists -Path $plan02Path
    $currentPlanIndexText = Read-TextIfExists -Path $currentPlanIndexPath
    $updateRecordText = Read-TextIfExists -Path $updateRecordPath
    $updateIndexText = Read-TextIfExists -Path $updateIndexPath
    $docHistoryText = Read-TextIfExists -Path $docHistoryPath
    $docHistoryIndexText = Read-TextIfExists -Path $docHistoryIndexPath

    if ($ownershipText -and $creationText -and $placementText -and $eraseText -and $diagnosticsText -and
        $ownershipText.Contains("PointerBlocksWorldLeft") -and
        $ownershipText.Contains("PointerBlocksHoverOrDrag") -and
        $creationText.Contains("ownership.PointerBlocksHoverOrDrag") -and
        -not $creationText.Contains("return ownership.LeftConsumed || ownership.BoundsHit") -and
        $placementText.Contains("ownership.PointerBlocksWorldLeft || ownership.PointerBlocksHoverOrDrag") -and
        $eraseText.Contains("return ownership.PointerBlocksHoverOrDrag") -and
        $diagnosticsText.Contains("pointerBlocksWorldLeft") -and
        $diagnosticsText.Contains("pointerBlocksHoverOrDrag")) {
        Write-Pass "Blueprint creation flicker stage 02 splits pointer-owned diagnostics, world-left blocking, and hover/drag blocking."
    }
    else {
        Write-FailHealth "Blueprint creation flicker stage 02 must split pointerBlocksWorldLeft from pointerBlocksHoverOrDrag and keep diagnostics explicit."
    }

    if ($testText -and $programText -and
        $testText.Contains("BlueprintPointerOwnershipSemanticsSplitBlocksWorldLeftNotHover") -and
        $testText.Contains("pointerBlocksWorldLeft=true") -and
        $testText.Contains("pointerBlocksHoverOrDrag=false") -and
        $testText.Contains("pointerBlocksCreation=false") -and
        $programText.Contains("blueprint pointer ownership semantics split blocks world left not hover")) {
        Write-Pass "Blueprint creation flicker stage 02 targeted console regression is registered."
    }
    else {
        Write-FailHealth "Blueprint creation flicker stage 02 must register the targeted pointer ownership semantics regression."
    }

    if ($functionDocText -and $diagnosticsDocText -and $plan00Text -and $plan02Text -and $currentPlanIndexText -and
        $functionDocText.Contains("0.938-blueprint-pointer-ownership-semantics") -and
        $functionDocText.Contains("pointerBlocksWorldLeft") -and
        $functionDocText.Contains("pointerBlocksHoverOrDrag") -and
        $diagnosticsDocText.Contains("0.938-blueprint-pointer-ownership-semantics") -and
        $diagnosticsDocText.Contains("pointerBlocksWorldLeft") -and
        $diagnosticsDocText.Contains("pointerBlocksHoverOrDrag") -and
        $plan00Text.Contains('`02-pointer所有权语义拆分.md` 已完成') -and
        $plan00Text.Contains('03-物理左键边沿与创建状态机.md') -and
        $plan02Text.Contains("状态：已完成") -and
        $plan02Text.Contains('RuntimeVersion：`0.938-blueprint-pointer-ownership-semantics`') -and
        $plan02Text.Contains("BlueprintPointerOwnershipSemanticsSplitBlocksWorldLeftNotHover") -and
        $plan02Text.Contains("Test-BlueprintCreationFlickerPointerOwnershipStage02Governance") -and
        $plan02Text.Contains("不生成测试包") -and
        $plan02Text.Contains("未新增 AI 经验笔记") -and
        ($currentPlanIndexText.Contains("03-物理左键边沿与创建状态机.md") -or
            $currentPlanIndexText.Contains("06-验证打包与归档收口.md") -or
            $currentPlanIndexText.Contains("文档/归档历史计划/蓝图创建闪烁修复版/"))) {
        Write-Pass "Blueprint creation flicker stage 02 plan, function doc, diagnostics doc, and current plan index are synchronized."
    }
    else {
        Write-FailHealth "Blueprint creation flicker stage 02 docs must record completion, split diagnostics, no-package boundary, and 03 handoff."
    }

    if ($updateRecordText -and $updateIndexText -and $docHistoryText -and $docHistoryIndexText -and
        $updateRecordText.Contains('RuntimeVersion：`0.938-blueprint-pointer-ownership-semantics`') -and
        $updateRecordText.Contains("BlueprintPointerOwnershipSemanticsSplitBlocksWorldLeftNotHover") -and
        $updateRecordText.Contains("Test-BlueprintCreationFlickerPointerOwnershipStage02Governance") -and
        $updateRecordText.Contains("不生成测试包") -and
        $updateRecordText.Contains("未新增 AI 经验笔记") -and
        $updateIndexText.Contains("0.938-蓝图pointer所有权语义拆分-2606222329.md") -and
        $docHistoryText.Contains("0.938-blueprint-pointer-ownership-semantics") -and
        $docHistoryText.Contains("Test-BlueprintCreationFlickerPointerOwnershipStage02Governance") -and
        $docHistoryIndexText.Contains("蓝图pointer所有权语义拆分-2606222329.md")) {
        Write-Pass "Blueprint creation flicker stage 02 update record and document-change history are synchronized."
    }
    else {
        Write-FailHealth "Blueprint creation flicker stage 02 must synchronize update record/index and document-change history."
    }
}

function Test-BlueprintCreationFlickerPhysicalLeftStage03Governance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $creationPath = Join-Path $RepoRoot "src\JueMingZ\UI\BlueprintCreationOverlay.cs"
    $maskPath = Join-Path $RepoRoot "src\JueMingZ\Automation\Blueprint\BlueprintCreationMaskState.cs"
    $diagnosticsPath = Join-Path $RepoRoot "src\JueMingZ\Automation\Blueprint\BlueprintUiClickDiagnostics.cs"
    $testPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintHandheldUiClickOwnershipTests.cs"
    $programPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.cs"
    $functionDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $diagnosticsDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "AI诊断日志说明.md")
    $planRootSegments = Get-BlueprintCreationFlickerPlanSegments -RepoRoot $RepoRoot
    $plan00Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments ($planRootSegments + @("00-基准.md"))
    $plan03Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments ($planRootSegments + @("03-物理左键边沿与创建状态机.md"))
    $currentPlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "索引.md")
    $updateRecordPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "0.939-蓝图物理左键边沿状态机-2606222348.md")
    $updateIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "索引.md")
    $docHistoryPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "蓝图物理左键边沿状态机-2606222348.md")
    $docHistoryIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "索引.md")

    $creationText = Read-TextIfExists -Path $creationPath
    $maskText = Read-TextIfExists -Path $maskPath
    $diagnosticsText = Read-TextIfExists -Path $diagnosticsPath
    $testText = Read-TextIfExists -Path $testPath
    $programText = Read-TextIfExists -Path $programPath
    $functionDocText = Read-TextIfExists -Path $functionDocPath
    $diagnosticsDocText = Read-TextIfExists -Path $diagnosticsDocPath
    $plan00Text = Read-TextIfExists -Path $plan00Path
    $plan03Text = Read-TextIfExists -Path $plan03Path
    $currentPlanIndexText = Read-TextIfExists -Path $currentPlanIndexPath
    $updateRecordText = Read-TextIfExists -Path $updateRecordPath
    $updateIndexText = Read-TextIfExists -Path $updateIndexPath
    $docHistoryText = Read-TextIfExists -Path $docHistoryPath
    $docHistoryIndexText = Read-TextIfExists -Path $docHistoryIndexPath

    if ($creationText -and $maskText -and $diagnosticsText -and
        $creationText.Contains("_wasPhysicalLeftDown") -and
        $creationText.Contains("ResolvePhysicalLeftDown(raw)") -and
        $creationText.Contains("BuildPointerInputFromPhysicalEdgesForTesting") -and
        $creationText.Contains("deriving LeftReleased from it would turn a consume into a fake") -and
        $creationText.Contains("UiPointerOwnershipService.ResolveWorldLeftDown(raw)") -and
        -not $creationText.Contains("!leftDown && _wasLeftDown") -and
        $maskText.Contains("WorldLeftDown") -and
        $maskText.Contains("PhysicalLeftDown") -and
        $diagnosticsText.Contains("physicalLeft")) {
        Write-Pass "Blueprint creation flicker stage 03 separates physical-left edges from resolved world-left and records physicalLeft diagnostics."
    }
    else {
        Write-FailHealth "Blueprint creation flicker stage 03 must derive creation press/release from physical-left edges while keeping ResolveWorldLeftDown as the world-left blocker."
    }

    if ($testText -and $programText -and
        $testText.Contains("BlueprintCreationPhysicalLeftEdgesIgnoreConsumedWorldLeft") -and
        $testText.Contains("physicalLeft=true") -and
        $testText.Contains("leftReleased=false") -and
        $testText.Contains("physicalLeft=false") -and
        $programText.Contains("blueprint creation physical left edges ignore consumed world left")) {
        Write-Pass "Blueprint creation flicker stage 03 targeted physical-left console regression is registered."
    }
    else {
        Write-FailHealth "Blueprint creation flicker stage 03 must register a targeted consumed-left physical-edge regression."
    }

    if ($functionDocText -and $diagnosticsDocText -and $plan00Text -and $plan03Text -and $currentPlanIndexText -and
        $functionDocText.Contains("0.939-blueprint-physical-left-edges") -and
        $functionDocText.Contains("physicalLeft") -and
        $diagnosticsDocText.Contains("0.939-blueprint-physical-left-edges") -and
        $diagnosticsDocText.Contains("physicalLeft") -and
        $plan00Text.Contains('`03-物理左键边沿与创建状态机.md` 已完成') -and
        $plan00Text.Contains("04-坐标域与相邻overlay一致性.md") -and
        $plan03Text.Contains("状态：已完成") -and
        $plan03Text.Contains('RuntimeVersion：`0.939-blueprint-physical-left-edges`') -and
        $plan03Text.Contains("BlueprintCreationPhysicalLeftEdgesIgnoreConsumedWorldLeft") -and
        $plan03Text.Contains("Test-BlueprintCreationFlickerPhysicalLeftStage03Governance") -and
        $plan03Text.Contains("不生成测试包") -and
        $plan03Text.Contains("未新增 AI 经验笔记") -and
        ($currentPlanIndexText.Contains("04-坐标域与相邻overlay一致性.md") -or
            $currentPlanIndexText.Contains("06-验证打包与归档收口.md") -or
            $currentPlanIndexText.Contains("文档/归档历史计划/蓝图创建闪烁修复版/"))) {
        Write-Pass "Blueprint creation flicker stage 03 plan, function doc, diagnostics doc, and current plan index are synchronized."
    }
    else {
        Write-FailHealth "Blueprint creation flicker stage 03 docs must record completion, physicalLeft diagnostics, no-package boundary, and 04 handoff."
    }

    if ($updateRecordText -and $updateIndexText -and $docHistoryText -and $docHistoryIndexText -and
        $updateRecordText.Contains('RuntimeVersion：`0.939-blueprint-physical-left-edges`') -and
        $updateRecordText.Contains("BlueprintCreationPhysicalLeftEdgesIgnoreConsumedWorldLeft") -and
        $updateRecordText.Contains("Test-BlueprintCreationFlickerPhysicalLeftStage03Governance") -and
        $updateRecordText.Contains("不生成测试包") -and
        $updateRecordText.Contains("未新增 AI 经验笔记") -and
        $updateIndexText.Contains("0.939-蓝图物理左键边沿状态机-2606222348.md") -and
        $docHistoryText.Contains("0.939-blueprint-physical-left-edges") -and
        $docHistoryText.Contains("Test-BlueprintCreationFlickerPhysicalLeftStage03Governance") -and
        $docHistoryIndexText.Contains("蓝图物理左键边沿状态机-2606222348.md")) {
        Write-Pass "Blueprint creation flicker stage 03 update record and document-change history are synchronized."
    }
    else {
        Write-FailHealth "Blueprint creation flicker stage 03 must synchronize update record/index and document-change history."
    }
}

function Test-BlueprintCreationFlickerCoordinateDomainStage04Governance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $ownershipPath = Join-Path $RepoRoot "src\JueMingZ\UI\UiPointerOwnershipService.cs"
    $placementPath = Join-Path $RepoRoot "src\JueMingZ\UI\BlueprintPlacementPreviewOverlay.cs"
    $erasePath = Join-Path $RepoRoot "src\JueMingZ\UI\BlueprintEraseRegionOverlay.cs"
    $testPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintHandheldUiClickOwnershipTests.cs"
    $programPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.cs"
    $functionDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $diagnosticsDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "AI诊断日志说明.md")
    $planRootSegments = Get-BlueprintCreationFlickerPlanSegments -RepoRoot $RepoRoot
    $plan00Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments ($planRootSegments + @("00-基准.md"))
    $plan04Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments ($planRootSegments + @("04-坐标域与相邻overlay一致性.md"))
    $currentPlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "索引.md")
    $updateRecordPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "0.940-蓝图坐标域相邻overlay一致性-2606230001.md")
    $updateIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "索引.md")
    $docHistoryPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "蓝图坐标域相邻overlay一致性-2606230001.md")
    $docHistoryIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "索引.md")

    $ownershipText = Read-TextIfExists -Path $ownershipPath
    $placementText = Read-TextIfExists -Path $placementPath
    $eraseText = Read-TextIfExists -Path $erasePath
    $testText = Read-TextIfExists -Path $testPath
    $programText = Read-TextIfExists -Path $programPath
    $functionDocText = Read-TextIfExists -Path $functionDocPath
    $diagnosticsDocText = Read-TextIfExists -Path $diagnosticsDocPath
    $plan00Text = Read-TextIfExists -Path $plan00Path
    $plan04Text = Read-TextIfExists -Path $plan04Path
    $currentPlanIndexText = Read-TextIfExists -Path $currentPlanIndexPath
    $updateRecordText = Read-TextIfExists -Path $updateRecordPath
    $updateIndexText = Read-TextIfExists -Path $updateIndexPath
    $docHistoryText = Read-TextIfExists -Path $docHistoryPath
    $docHistoryIndexText = Read-TextIfExists -Path $docHistoryIndexPath

    if ($ownershipText -and $placementText -and $eraseText -and
        $ownershipText.Contains("raw.GameInputAvailable && hasOs") -and
        $ownershipText.Contains("Handheld owner bounds are registered in draw/client-screen") -and
        $ownershipText.Contains('source = "OsClient"') -and
        $placementText.Contains("ownership.PointerBlocksWorldLeft || ownership.PointerBlocksHoverOrDrag") -and
        $eraseText.Contains("return ownership.PointerBlocksHoverOrDrag")) {
        Write-Pass "Blueprint creation flicker stage 04 keeps handheld owner-bounds hit testing in the OS client coordinate domain while preserving placement consumed-left blocking and erase hover/drag bounds blocking."
    }
    else {
        Write-FailHealth "Blueprint creation flicker stage 04 must prefer OS client mouse for handheld owner bounds and keep placement/erase pointer ownership split."
    }

    if ($testText -and $programText -and
        $testText.Contains("BlueprintHandheldOwnerBoundsUseOsClientCoordinateDomain") -and
        $testText.Contains("pointerOwnerMouseSource=OsClient") -and
        $testText.Contains("Terraria raw must not make handheld owner bounds hit") -and
        $testText.Contains("BlueprintPlacementErasePointerOwnershipNarrowingKeepsWorldInput") -and
        $programText.Contains("blueprint handheld owner bounds use OS client coordinate domain")) {
        Write-Pass "Blueprint creation flicker stage 04 targeted coordinate-domain and adjacent overlay console regressions are registered."
    }
    else {
        Write-FailHealth "Blueprint creation flicker stage 04 must register handheld owner coordinate-domain and adjacent overlay regressions."
    }

    if ($functionDocText -and $diagnosticsDocText -and $plan00Text -and $plan04Text -and $currentPlanIndexText -and
        $functionDocText.Contains("0.940-blueprint-coordinate-domain-overlay-consistency") -and
        $functionDocText.Contains("BlueprintHandheldOwnerBoundsUseOsClientCoordinateDomain") -and
        $functionDocText.Contains("Test-BlueprintCreationFlickerCoordinateDomainStage04Governance") -and
        $diagnosticsDocText.Contains("0.940-blueprint-coordinate-domain-overlay-consistency") -and
        $diagnosticsDocText.Contains("pointerOwnerMouseSource=OsClient") -and
        $plan00Text.Contains('`04-坐标域与相邻overlay一致性.md` 已完成') -and
        $plan00Text.Contains("05-回归测试与审计防线.md") -and
        $plan04Text.Contains("状态：已完成") -and
        $plan04Text.Contains('RuntimeVersion：`0.940-blueprint-coordinate-domain-overlay-consistency`') -and
        $plan04Text.Contains("BlueprintHandheldOwnerBoundsUseOsClientCoordinateDomain") -and
        $plan04Text.Contains("Test-BlueprintCreationFlickerCoordinateDomainStage04Governance") -and
        $plan04Text.Contains("不生成测试包") -and
        $plan04Text.Contains("未新增 AI 经验笔记") -and
        ($currentPlanIndexText.Contains("05-回归测试与审计防线.md") -or
            $currentPlanIndexText.Contains("06-验证打包与归档收口.md") -or
            $currentPlanIndexText.Contains("文档/归档历史计划/蓝图创建闪烁修复版/"))) {
        Write-Pass "Blueprint creation flicker stage 04 plan, function doc, diagnostics doc, and current plan index are synchronized."
    }
    else {
        Write-FailHealth "Blueprint creation flicker stage 04 docs must record coordinate-domain consistency, no-package boundary, and 05 handoff."
    }

    if ($updateRecordText -and $updateIndexText -and $docHistoryText -and $docHistoryIndexText -and
        $updateRecordText.Contains('RuntimeVersion：`0.940-blueprint-coordinate-domain-overlay-consistency`') -and
        $updateRecordText.Contains("BlueprintHandheldOwnerBoundsUseOsClientCoordinateDomain") -and
        $updateRecordText.Contains("Test-BlueprintCreationFlickerCoordinateDomainStage04Governance") -and
        $updateRecordText.Contains("不生成测试包") -and
        $updateRecordText.Contains("未新增 AI 经验笔记") -and
        $updateIndexText.Contains("0.940-蓝图坐标域相邻overlay一致性-2606230001.md") -and
        $docHistoryText.Contains("0.940-blueprint-coordinate-domain-overlay-consistency") -and
        $docHistoryText.Contains("Test-BlueprintCreationFlickerCoordinateDomainStage04Governance") -and
        $docHistoryIndexText.Contains("蓝图坐标域相邻overlay一致性-2606230001.md")) {
        Write-Pass "Blueprint creation flicker stage 04 update record and document-change history are synchronized."
    }
    else {
        Write-FailHealth "Blueprint creation flicker stage 04 must synchronize update record/index and document-change history."
    }
}

function Test-BlueprintCreationFlickerStage05Governance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $ownershipPath = Join-Path $RepoRoot "src\JueMingZ\UI\UiPointerOwnershipService.cs"
    $creationPath = Join-Path $RepoRoot "src\JueMingZ\UI\BlueprintCreationOverlay.cs"
    $placementPath = Join-Path $RepoRoot "src\JueMingZ\UI\BlueprintPlacementPreviewOverlay.cs"
    $erasePath = Join-Path $RepoRoot "src\JueMingZ\UI\BlueprintEraseRegionOverlay.cs"
    $diagnosticsPath = Join-Path $RepoRoot "src\JueMingZ\Automation\Blueprint\BlueprintUiClickDiagnostics.cs"
    $testPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintHandheldUiClickOwnershipTests.cs"
    $programPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.cs"
    $functionDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $diagnosticsDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "AI诊断日志说明.md")
    $planRootSegments = Get-BlueprintCreationFlickerPlanSegments -RepoRoot $RepoRoot
    $plan00Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments ($planRootSegments + @("00-基准.md"))
    $plan05Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments ($planRootSegments + @("05-回归测试与审计防线.md"))
    $currentPlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "索引.md")
    $updateRecordPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "0.941-蓝图创建闪烁回归审计-2606230018.md")
    $updateIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "索引.md")
    $docHistoryPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "蓝图创建闪烁回归审计-2606230018.md")
    $docHistoryIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "索引.md")

    $ownershipText = Read-TextIfExists -Path $ownershipPath
    $creationText = Read-TextIfExists -Path $creationPath
    $placementText = Read-TextIfExists -Path $placementPath
    $eraseText = Read-TextIfExists -Path $erasePath
    $diagnosticsText = Read-TextIfExists -Path $diagnosticsPath
    $testText = Read-TextIfExists -Path $testPath
    $programText = Read-TextIfExists -Path $programPath
    $functionDocText = Read-TextIfExists -Path $functionDocPath
    $diagnosticsDocText = Read-TextIfExists -Path $diagnosticsDocPath
    $plan00Text = Read-TextIfExists -Path $plan00Path
    $plan05Text = Read-TextIfExists -Path $plan05Path
    $currentPlanIndexText = Read-TextIfExists -Path $currentPlanIndexPath
    $updateRecordText = Read-TextIfExists -Path $updateRecordPath
    $updateIndexText = Read-TextIfExists -Path $updateIndexPath
    $docHistoryText = Read-TextIfExists -Path $docHistoryPath
    $docHistoryIndexText = Read-TextIfExists -Path $docHistoryIndexPath

    if ($ownershipText -and $creationText -and $placementText -and $eraseText -and $diagnosticsText -and
        $ownershipText.Contains("PointerBlocksWorldLeft") -and
        $ownershipText.Contains("PointerBlocksHoverOrDrag") -and
        $ownershipText.Contains("Handheld owner bounds are registered in draw/client-screen") -and
        $creationText.Contains("ResolvePhysicalLeftDown(raw)") -and
        $creationText.Contains("deriving LeftReleased from it would turn a consume into a fake") -and
        $creationText.Contains("ownership.PointerBlocksHoverOrDrag") -and
        $placementText.Contains("ownership.PointerBlocksWorldLeft || ownership.PointerBlocksHoverOrDrag") -and
        $eraseText.Contains("return ownership.PointerBlocksHoverOrDrag") -and
        $diagnosticsText.Contains("pointerBlocksWorldLeft") -and
        $diagnosticsText.Contains("pointerBlocksHoverOrDrag") -and
        $diagnosticsText.Contains("physicalLeft")) {
        Write-Pass "Blueprint creation flicker stage 05 keeps the repaired ownership, physical-left, coordinate-domain, adjacent overlay, and diagnostics contracts wired."
    }
    else {
        Write-FailHealth "Blueprint creation flicker stage 05 must keep split pointer ownership, physical-left edges, OS-client owner bounds, placement/erase gates, and diagnostics anchors wired."
    }

    if ($testText -and $programText -and
        $testText.Contains("BlueprintCreationFlickerFixContractsStayWired") -and
        $testText.Contains("BlueprintPointerOwnershipSemanticsSplitBlocksWorldLeftNotHover") -and
        $testText.Contains("BlueprintCreationPhysicalLeftEdgesIgnoreConsumedWorldLeft") -and
        $testText.Contains("BlueprintHandheldOwnerBoundsUseOsClientCoordinateDomain") -and
        $testText.Contains("BlueprintWorldOverlayOwnershipDiagnosticsIncludeSnapshotDetails") -and
        $testText.Contains("BlueprintWorldOverlayPointerOwnershipContractsStayWired") -and
        $testText.Contains("BlueprintCreationDiagnosticContractsStayWired") -and
        $programText.Contains("blueprint creation flicker fix contracts stay wired")) {
        Write-Pass "Blueprint creation flicker stage 05 aggregate console regression is registered."
    }
    else {
        Write-FailHealth "Blueprint creation flicker stage 05 must register BlueprintCreationFlickerFixContractsStayWired and reuse the stage 02-04 plus diagnostic aggregate regressions."
    }

    if ($functionDocText -and $diagnosticsDocText -and
        $functionDocText.Contains("0.941-blueprint-creation-flicker-audit") -and
        $functionDocText.Contains("BlueprintCreationFlickerFixContractsStayWired") -and
        $functionDocText.Contains("Test-BlueprintCreationFlickerStage05Governance") -and
        $diagnosticsDocText.Contains("0.941-blueprint-creation-flicker-audit") -and
        $diagnosticsDocText.Contains("BlueprintCreationFlickerFixContractsStayWired") -and
        $diagnosticsDocText.Contains("Test-BlueprintCreationFlickerStage05Governance") -and
        $diagnosticsDocText.Contains("不新增 trace JSONL")) {
        Write-Pass "Blueprint function and diagnostics docs describe the creation flicker stage 05 audit boundary."
    }
    else {
        Write-FailHealth "Blueprint function and diagnostics docs must describe 0.941, the aggregate regression, scoped health audit, and no-trace boundary."
    }

    if ($plan00Text -and $plan05Text -and $currentPlanIndexText -and
        $plan00Text.Contains('`05-回归测试与审计防线.md` 已完成') -and
        $plan00Text.Contains("0.941-blueprint-creation-flicker-audit") -and
        $plan00Text.Contains("06-验证打包与归档收口.md") -and
        $plan05Text.Contains("状态：已完成") -and
        $plan05Text.Contains('RuntimeVersion：`0.941-blueprint-creation-flicker-audit`') -and
        $plan05Text.Contains("BlueprintCreationFlickerFixContractsStayWired") -and
        $plan05Text.Contains("Test-BlueprintCreationFlickerStage05Governance") -and
        $plan05Text.Contains("不生成测试包") -and
        $plan05Text.Contains("未新增 AI 经验笔记") -and
        $currentPlanIndexText.Contains("0.941-blueprint-creation-flicker-audit") -and
        ($currentPlanIndexText.Contains("06-验证打包与归档收口.md") -or
            $currentPlanIndexText.Contains("文档/归档历史计划/蓝图创建闪烁修复版/"))) {
        Write-Pass "Blueprint creation flicker stage 05 plan files record completion, aggregate regression, scoped audit, no-package boundary, and 06 handoff."
    }
    else {
        Write-FailHealth "Blueprint creation flicker stage 05 plan files must record completion, aggregate regression, scoped audit, no-package/no-experience-note review, and 06 handoff."
    }

    if ($updateRecordText -and $updateIndexText -and $docHistoryText -and $docHistoryIndexText -and
        $updateRecordText.Contains('RuntimeVersion：`0.941-blueprint-creation-flicker-audit`') -and
        $updateRecordText.Contains("BlueprintCreationFlickerFixContractsStayWired") -and
        $updateRecordText.Contains("Test-BlueprintCreationFlickerStage05Governance") -and
        $updateRecordText.Contains("不生成测试包") -and
        $updateRecordText.Contains("本地验证不等于用户实机验收") -and
        $updateRecordText.Contains("未新增 AI 经验笔记") -and
        $updateIndexText.Contains("0.941-蓝图创建闪烁回归审计-2606230018.md") -and
        $docHistoryText.Contains("0.941-blueprint-creation-flicker-audit") -and
        $docHistoryText.Contains("BlueprintCreationFlickerFixContractsStayWired") -and
        $docHistoryText.Contains("Test-BlueprintCreationFlickerStage05Governance") -and
        $docHistoryIndexText.Contains("蓝图创建闪烁回归审计-2606230018.md")) {
        Write-Pass "Blueprint creation flicker stage 05 update record and document-change history are synchronized."
    }
    else {
        Write-FailHealth "Blueprint creation flicker stage 05 must synchronize update record/index and document-change history with the aggregate regression and health audit."
    }
}

function Test-BlueprintCreationFlickerStage06CloseoutGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $auditPath = Join-Path $RepoRoot "scripts\audit-project-health.ps1"
    $currentPlanDirPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "蓝图创建闪烁修复版")
    $archivedPlan00Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图创建闪烁修复版", "00-基准.md")
    $archivedPlan06Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图创建闪烁修复版", "06-验证打包与归档收口.md")
    $currentPlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "索引.md")
    $archivePlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "索引.md")
    $blueprintDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $diagnosticsDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "AI诊断日志说明.md")
    $updateIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "索引.md")
    $updateRecordPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "0.942-蓝图创建闪烁验证收口-2606230024.md")
    $docHistoryIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "索引.md")
    $docHistoryRecordPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "蓝图创建闪烁验证收口-2606230024.md")

    $auditText = Read-TextIfExists -Path $auditPath
    $plan00Text = Read-TextIfExists -Path $archivedPlan00Path
    $plan06Text = Read-TextIfExists -Path $archivedPlan06Path
    $currentPlanIndexText = Read-TextIfExists -Path $currentPlanIndexPath
    $archivePlanIndexText = Read-TextIfExists -Path $archivePlanIndexPath
    $blueprintDocText = Read-TextIfExists -Path $blueprintDocPath
    $diagnosticsDocText = Read-TextIfExists -Path $diagnosticsDocPath
    $updateIndexText = Read-TextIfExists -Path $updateIndexPath
    $updateRecordText = Read-TextIfExists -Path $updateRecordPath
    $docHistoryIndexText = Read-TextIfExists -Path $docHistoryIndexPath
    $docHistoryRecordText = Read-TextIfExists -Path $docHistoryRecordPath

    if ($auditText -and
        $auditText.Contains("Test-BlueprintCreationFlickerStage06CloseoutGovernance") -and
        $auditText.Contains("0.942-blueprint-creation-flicker-closeout") -and
        $auditText.Contains("0.942-蓝图创建闪烁验证收口-2606230024.md")) {
        Write-Pass "Blueprint creation flicker stage 06 closeout health audit is present and wired to the 0.942 closeout contract."
    }
    else {
        Write-FailHealth "Blueprint creation flicker stage 06 closeout health audit must lock the 0.942 closeout contract and update record."
    }

    if (-not (Test-Path -LiteralPath $currentPlanDirPath) -and
        $plan00Text -and
        $plan00Text.Contains('状态：`06-验证打包与归档收口` 已完成') -and
        $plan00Text.Contains("0.942-blueprint-creation-flicker-closeout") -and
        $plan00Text.Contains("自动串行接力终止") -and
        $plan06Text -and
        $plan06Text.Contains("状态：已完成") -and
        $plan06Text.Contains("0.942-blueprint-creation-flicker-closeout") -and
        $plan06Text.Contains("JueMingZ-TestPackage") -and
        $plan06Text.Contains("严格新鲜包健康审计") -and
        $plan06Text.Contains("不再创建新对话")) {
        Write-Pass "Blueprint creation flicker plan is archived with the 0.942 closeout, package delivery, and no further handoff."
    }
    else {
        Write-FailHealth "Stage-06 closeout must move the blueprint creation flicker plan to archive and mark 06 complete with package/fresh-audit scope."
    }

    if ($currentPlanIndexText -and
        $currentPlanIndexText.Contains("当前没有正在推进的计划") -and
        $currentPlanIndexText.Contains("文档/归档历史计划/蓝图创建闪烁修复版/") -and
        $currentPlanIndexText.Contains("0.941-blueprint-creation-flicker-audit") -and
        $archivePlanIndexText -and
        $archivePlanIndexText.Contains("文档/归档历史计划/蓝图创建闪烁修复版/") -and
        $archivePlanIndexText.Contains("0.942-blueprint-creation-flicker-closeout") -and
        $archivePlanIndexText.Contains("自动接力已终止")) {
        Write-Pass "Current and archived plan indices record the blueprint creation flicker closeout and relay termination."
    }
    else {
        Write-FailHealth "Stage-06 blueprint creation flicker closeout must update current and archived plan indices."
    }

    if ($blueprintDocText -and
        $blueprintDocText.Contains("0.942-blueprint-creation-flicker-closeout") -and
        $blueprintDocText.Contains("文档/归档历史计划/蓝图创建闪烁修复版/00-基准.md") -and
        $blueprintDocText.Contains("BlueprintCreationFlickerFixContractsStayWired") -and
        $blueprintDocText.Contains("不新增蓝图用户可见行为") -and
        $diagnosticsDocText -and
        $diagnosticsDocText.Contains("0.942-blueprint-creation-flicker-closeout") -and
        $diagnosticsDocText.Contains("不新增诊断字段") -and
        $diagnosticsDocText.Contains("不新增 trace JSONL")) {
        Write-Pass "Blueprint feature and diagnostics docs record the 0.942 closeout without expanding runtime or diagnostics scope."
    }
    else {
        Write-FailHealth "Stage-06 blueprint creation flicker closeout must update blueprint feature and diagnostics docs with no-new-runtime/no-new-diagnostics scope."
    }

    if ($updateIndexText -and
        $updateIndexText.Contains("0.942-蓝图创建闪烁验证收口-2606230024.md") -and
        $updateRecordText -and
        $updateRecordText.Contains('RuntimeVersion：`0.942-blueprint-creation-flicker-closeout`') -and
        $updateRecordText.Contains("JueMingZ-TestPackage") -and
        $updateRecordText.Contains("-RequireFreshTestPackage") -and
        $updateRecordText.Contains("BlueprintCreationFlickerFixContractsStayWired") -and
        $docHistoryIndexText -and
        $docHistoryIndexText.Contains("蓝图创建闪烁验证收口-2606230024.md") -and
        $docHistoryRecordText -and
        $docHistoryRecordText.Contains("0.942-blueprint-creation-flicker-closeout")) {
        Write-Pass "Stage-06 blueprint creation flicker update record and document-change history are synchronized."
    }
    else {
        Write-FailHealth "Stage-06 blueprint creation flicker update record, update index, and document-change history must reference the 0.942 closeout."
    }
}

function Test-BlueprintCreationInputPhaseTraceStage02Governance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $diagnosticsPath = Join-Path $RepoRoot "src\JueMingZ\Automation\Blueprint\BlueprintUiClickDiagnostics.cs"
    $snapshotPath = Join-Path $RepoRoot "src\JueMingZ\Diagnostics\DiagnosticSnapshot.cs"
    $writerPath = Join-Path $RepoRoot "src\JueMingZ\Diagnostics\DiagnosticSnapshotWriter.Json.cs"
    $builderPath = Join-Path $RepoRoot "src\JueMingZ\Runtime\Diagnostics\RuntimeDiagnosticSnapshotBuilder.Blueprint.cs"
    $testPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintHandheldUiClickOwnershipTests.cs"
    $programPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.cs"
    $functionDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $diagnosticsDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "AI诊断日志说明.md")
    $plan00Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "蓝图创建闪烁诊断版", "00-基准.md")
    $plan02Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "蓝图创建闪烁诊断版", "02-prefix与after诊断分槽.md")
    $currentPlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "索引.md")
    $archivePlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "索引.md")
    $updateRecordPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "0.932-蓝图创建输入阶段分槽-2606221949.md")
    $updateIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "索引.md")
    $docHistoryPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "蓝图创建输入阶段分槽-2606221949.md")
    $docHistoryIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "索引.md")

    if (-not (Test-Path -LiteralPath $plan00Path)) {
        $plan00Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图创建闪烁诊断版", "00-基准.md")
    }

    if (-not (Test-Path -LiteralPath $plan02Path)) {
        $plan02Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图创建闪烁诊断版", "02-prefix与after诊断分槽.md")
    }

    $diagnosticsText = Read-TextIfExists -Path $diagnosticsPath
    $snapshotText = Read-TextIfExists -Path $snapshotPath
    $writerText = Read-TextIfExists -Path $writerPath
    $builderText = Read-TextIfExists -Path $builderPath
    $testText = Read-TextIfExists -Path $testPath
    $programText = Read-TextIfExists -Path $programPath
    $functionDocText = Read-TextIfExists -Path $functionDocPath
    $diagnosticsDocText = Read-TextIfExists -Path $diagnosticsDocPath
    $plan00Text = Read-TextIfExists -Path $plan00Path
    $plan02Text = Read-TextIfExists -Path $plan02Path
    $currentPlanIndexText = Read-TextIfExists -Path $currentPlanIndexPath
    $archivePlanIndexText = Read-TextIfExists -Path $archivePlanIndexPath
    if ($archivePlanIndexText) {
        $currentPlanIndexText = "$currentPlanIndexText`n$archivePlanIndexText"
    }
    $updateRecordText = Read-TextIfExists -Path $updateRecordPath
    $updateIndexText = Read-TextIfExists -Path $updateIndexPath
    $docHistoryText = Read-TextIfExists -Path $docHistoryPath
    $docHistoryIndexText = Read-TextIfExists -Path $docHistoryIndexPath

    $requiredFields = @(
        "BlueprintCreationPrefixWorldOverlayInputTrace",
        "BlueprintCreationAfterPlayerInputWorldOverlayInputTrace"
    )
    $missingFields = @()
    foreach ($field in $requiredFields) {
        if (-not ($snapshotText -and $snapshotText.Contains($field) -and
                  $writerText -and $writerText.Contains($field) -and
                  $builderText -and $builderText.Contains($field))) {
            $missingFields += $field
        }
    }

    if ($missingFields.Count -eq 0 -and
        $diagnosticsText -and
        $diagnosticsText.Contains("CreationPrefixWorldOverlayInputTrace") -and
        $diagnosticsText.Contains("CreationAfterPlayerInputWorldOverlayInputTrace") -and
        $diagnosticsText.Contains("_worldOverlayInputTrace = trace") -and
        $diagnosticsText.Contains("IsCreationPrefix") -and
        $diagnosticsText.Contains("IsCreationAfterPlayerInput") -and
        $diagnosticsText.Contains("after-player-input replay cannot hide the prefix facts")) {
        Write-Pass "Blueprint creation world-overlay phase traces keep fixed prefix/after slots while preserving the legacy last-summary field."
    }
    else {
        Write-FailHealth "Blueprint stage-02 diagnostics must wire BlueprintCreationPrefixWorldOverlayInputTrace and BlueprintCreationAfterPlayerInputWorldOverlayInputTrace through DTO/writer/builder and keep BlueprintWorldOverlayLastInputTrace as the legacy last summary. Missing: $($missingFields -join ', ')"
    }

    if ($testText -and $programText -and
        $testText.Contains("BlueprintCreationWorldOverlayPhaseTraceSlotsKeepPrefixAndAfter") -and
        $testText.Contains("TestCreationPrefix") -and
        $testText.Contains("TestCreationAfter") -and
        $testText.Contains("OnlyAfter") -and
        $testText.Contains("BlueprintCreationPrefixWorldOverlayInputTrace") -and
        $testText.Contains("BlueprintCreationAfterPlayerInputWorldOverlayInputTrace") -and
        $testText.Contains("Creation prefix world overlay trace must not be overwritten by after-player-input") -and
        $programText.Contains("blueprint creation world overlay phase trace slots keep prefix and after")) {
        Write-Pass "Blueprint stage-02 console regression locks prefix/after separation, only-after empty-prefix behavior, and snapshot JSON fields."
    }
    else {
        Write-FailHealth "Blueprint stage-02 must register a console regression for creation prefix/after phase slots, JSON output, and only-after empty-prefix behavior."
    }

    if ($functionDocText -and $diagnosticsDocText -and
        $functionDocText.Contains("0.932-blueprint-creation-input-phase-trace") -and
        $functionDocText.Contains("BlueprintCreationPrefixWorldOverlayInputTrace") -and
        $functionDocText.Contains("BlueprintCreationAfterPlayerInputWorldOverlayInputTrace") -and
        $functionDocText.Contains("BlueprintWorldOverlayLastInputTrace") -and
        $functionDocText.Contains("不改变 creation / placement / erase") -and
        $diagnosticsDocText.Contains("0.932-blueprint-creation-input-phase-trace") -and
        $diagnosticsDocText.Contains("BlueprintCreationPrefixWorldOverlayInputTrace") -and
        $diagnosticsDocText.Contains("BlueprintCreationAfterPlayerInputWorldOverlayInputTrace") -and
        $diagnosticsDocText.Contains("兼容摘要") -and
        $diagnosticsDocText.Contains("不新增 trace JSONL")) {
        Write-Pass "Blueprint function and diagnostics docs describe the stage-02 phase trace fields, compatibility summary, and no-behavior-change boundary."
    }
    else {
        Write-FailHealth "Blueprint stage-02 docs must describe 0.932 phase trace fields, the compatibility summary, no trace JSONL, and no behavior-change boundary."
    }

    if ($plan00Text -and $plan02Text -and $currentPlanIndexText -and
        $plan00Text.Contains('`02-prefix与after诊断分槽`') -and
        $plan00Text.Contains('已完成，RuntimeVersion `0.932-blueprint-creation-input-phase-trace`') -and
        ($plan00Text.Contains('下一入口为 `03-创建状态机清空原因追踪.md`') -or
            $plan00Text.Contains('下一入口为 `04-诊断回归与审计防线.md`') -or
            $plan00Text.Contains('下一入口为 `05-验证打包与回传口径.md`') -or
            $plan00Text.Contains("0.935-blueprint-creation-diagnostic-package")) -and
        $plan02Text.Contains("状态：已完成") -and
        $plan02Text.Contains('RuntimeVersion：`0.932-blueprint-creation-input-phase-trace`') -and
        $plan02Text.Contains("BlueprintCreationWorldOverlayPhaseTraceSlotsKeepPrefixAndAfter") -and
        $plan02Text.Contains("Test-BlueprintCreationInputPhaseTraceStage02Governance") -and
        $plan02Text.Contains("不生成测试包") -and
        $plan02Text.Contains('不实现、修改或验证 `03`') -and
        ($currentPlanIndexText.Contains('已完成 `02-prefix与after诊断分槽.md`') -or
            $currentPlanIndexText.Contains('`02` 已把 creation prefix') -or
            $currentPlanIndexText.Contains("0.935-blueprint-creation-diagnostic-package")) -and
        ($currentPlanIndexText.Contains('后续唯一入口为 `03-创建状态机清空原因追踪.md`') -or
            $currentPlanIndexText.Contains('后续唯一入口为 `04-诊断回归与审计防线.md`') -or
            $currentPlanIndexText.Contains('后续唯一入口为 `05-验证打包与回传口径.md`') -or
            $currentPlanIndexText.Contains("0.935-blueprint-creation-diagnostic-package"))) {
        Write-Pass "Blueprint diagnostic stage-02 plan files record completion, no-package boundary, scoped regression/audit, and stage-03 handoff."
    }
    else {
        Write-FailHealth "Blueprint diagnostic stage-02 plan files must record completion, 0.932 version, scoped regression/audit, no-package/no-03 boundary, and stage-03 handoff."
    }

    if ($updateRecordText -and $updateIndexText -and $docHistoryText -and $docHistoryIndexText -and
        $updateRecordText.Contains("0.932-blueprint-creation-input-phase-trace") -and
        $updateRecordText.Contains("BlueprintCreationWorldOverlayPhaseTraceSlotsKeepPrefixAndAfter") -and
        $updateRecordText.Contains("Test-BlueprintCreationInputPhaseTraceStage02Governance") -and
        $updateRecordText.Contains("不生成测试包") -and
        $updateRecordText.Contains("本地验证不等于用户实机验收") -and
        $updateRecordText.Contains("未新增 AI 经验笔记") -and
        $updateIndexText.Contains("0.932-蓝图创建输入阶段分槽-2606221949.md") -and
        $docHistoryText.Contains("BlueprintCreationPrefixWorldOverlayInputTrace") -and
        $docHistoryText.Contains("Test-BlueprintCreationInputPhaseTraceStage02Governance") -and
        $docHistoryIndexText.Contains("蓝图创建输入阶段分槽-2606221949.md")) {
        Write-Pass "Blueprint diagnostic stage-02 update record and document-change history are synchronized."
    }
    else {
        Write-FailHealth "Blueprint diagnostic stage-02 must synchronize update record/index and document-change history with phase trace fields and scoped audit."
    }
}

function Test-BlueprintCreationClearReasonTraceStage03Governance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $diagnosticsPath = Join-Path $RepoRoot "src\JueMingZ\Automation\Blueprint\BlueprintUiClickDiagnostics.cs"
    $creationOverlayPath = Join-Path $RepoRoot "src\JueMingZ\UI\BlueprintCreationOverlay.cs"
    $snapshotPath = Join-Path $RepoRoot "src\JueMingZ\Diagnostics\DiagnosticSnapshot.cs"
    $writerPath = Join-Path $RepoRoot "src\JueMingZ\Diagnostics\DiagnosticSnapshotWriter.Json.cs"
    $builderPath = Join-Path $RepoRoot "src\JueMingZ\Runtime\Diagnostics\RuntimeDiagnosticSnapshotBuilder.Blueprint.cs"
    $actionServicePath = Join-Path $RepoRoot "src\JueMingZ\Input\LegacyUiActionService.Blueprint.cs"
    $hotkeyServicePath = Join-Path $RepoRoot "src\JueMingZ\Input\BlueprintEntryHotkeyService.cs"
    $testPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintHandheldUiClickOwnershipTests.cs"
    $programPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.cs"
    $functionDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $diagnosticsDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "AI诊断日志说明.md")
    $plan00Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "蓝图创建闪烁诊断版", "00-基准.md")
    $plan03Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "蓝图创建闪烁诊断版", "03-创建状态机清空原因追踪.md")
    $currentPlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "索引.md")
    $archivePlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "索引.md")
    $updateRecordPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "0.933-蓝图创建清空原因追踪-2606222034.md")
    $updateIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "索引.md")
    $docHistoryPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "蓝图创建清空原因追踪-2606222034.md")
    $docHistoryIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "索引.md")

    if (-not (Test-Path -LiteralPath $plan00Path)) {
        $plan00Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图创建闪烁诊断版", "00-基准.md")
    }

    if (-not (Test-Path -LiteralPath $plan03Path)) {
        $plan03Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图创建闪烁诊断版", "03-创建状态机清空原因追踪.md")
    }

    $diagnosticsText = Read-TextIfExists -Path $diagnosticsPath
    $creationOverlayText = Read-TextIfExists -Path $creationOverlayPath
    $snapshotText = Read-TextIfExists -Path $snapshotPath
    $writerText = Read-TextIfExists -Path $writerPath
    $builderText = Read-TextIfExists -Path $builderPath
    $actionServiceText = Read-TextIfExists -Path $actionServicePath
    $hotkeyServiceText = Read-TextIfExists -Path $hotkeyServicePath
    $testText = Read-TextIfExists -Path $testPath
    $programText = Read-TextIfExists -Path $programPath
    $functionDocText = Read-TextIfExists -Path $functionDocPath
    $diagnosticsDocText = Read-TextIfExists -Path $diagnosticsDocPath
    $plan00Text = Read-TextIfExists -Path $plan00Path
    $plan03Text = Read-TextIfExists -Path $plan03Path
    $currentPlanIndexText = Read-TextIfExists -Path $currentPlanIndexPath
    $archivePlanIndexText = Read-TextIfExists -Path $archivePlanIndexPath
    if ($archivePlanIndexText) {
        $currentPlanIndexText = "$currentPlanIndexText`n$archivePlanIndexText"
    }
    $updateRecordText = Read-TextIfExists -Path $updateRecordPath
    $updateIndexText = Read-TextIfExists -Path $updateIndexPath
    $docHistoryText = Read-TextIfExists -Path $docHistoryPath
    $docHistoryIndexText = Read-TextIfExists -Path $docHistoryIndexPath

    if ($diagnosticsText -and $creationOverlayText -and
        $diagnosticsText.Contains("CreationLastClearReasonTrace") -and
        $diagnosticsText.Contains("RecordCreationStateTransition") -and
        $diagnosticsText.Contains("beforeDragging") -and
        $diagnosticsText.Contains("afterDragging") -and
        $diagnosticsText.Contains("beforeMaskCount") -and
        $diagnosticsText.Contains("worldMouseSource") -and
        $diagnosticsText.Contains("pointerBlocksCreation") -and
        $diagnosticsText.Contains("This is diagnostics only") -and
        $creationOverlayText.Contains("BlueprintUiClickDiagnostics.RecordCreationStateTransition") -and
        $creationOverlayText.Contains("UiPointerOwnershipService.ResolveWorldLeftDown(raw)") -and
        $creationOverlayText.Contains("BlueprintCreationMaskState.HandlePointer(input)")) {
        Write-Pass "Blueprint stage-03 records creation clear/stop reasons after HandlePointer without changing world input decisions."
    }
    else {
        Write-FailHealth "Blueprint stage-03 diagnostics must record creation clear/stop reasons, before/after state, mouse source, and UI ownership details without changing HandlePointer or ResolveWorldLeftDown behavior."
    }

    if ($snapshotText -and $writerText -and $builderText -and
        $snapshotText.Contains("BlueprintCreationLastClearReasonTrace") -and
        $writerText.Contains("BlueprintCreationLastClearReasonTrace") -and
        $builderText.Contains("BlueprintCreationLastClearReasonTrace")) {
        Write-Pass "Blueprint stage-03 runtime snapshot wires BlueprintCreationLastClearReasonTrace through DTO, builder, and JSON writer."
    }
    else {
        Write-FailHealth "Blueprint stage-03 must wire BlueprintCreationLastClearReasonTrace through DiagnosticSnapshot, builder, and JSON writer."
    }

    if ($actionServiceText -and $hotkeyServiceText -and
        $actionServiceText.Contains("creationClearTrace") -and
        $actionServiceText.Contains("BuildBlueprintCreationActionMetadata") -and
        $hotkeyServiceText.Contains("RecordBlueprintActionHotkeyEvent") -and
        $hotkeyServiceText.Contains("creationClearTrace") -and
        $hotkeyServiceText.Contains("ScenarioNames.BlueprintActionHotkey")) {
        Write-Pass "Blueprint stage-03 action events attach the latest creation clear trace to handheld, F5 entry, and hotkey metadata."
    }
    else {
        Write-FailHealth "Blueprint stage-03 must attach creationClearTrace to related blueprint action event metadata."
    }

    if ($testText -and $programText -and
        $testText.Contains("BlueprintCreationClearReasonTraceRecordsStateAndCoordinates") -and
        $testText.Contains("reason=uiOwned") -and
        $testText.Contains("reason=worldMiss") -and
        $testText.Contains("reason=selectionToggled") -and
        $testText.Contains("BlueprintCreationLastClearReasonTrace") -and
        $programText.Contains("blueprint creation clear reason trace records state and coordinates")) {
        Write-Pass "Blueprint stage-03 console regression locks UI-owned, world-miss, and release clear reason traces."
    }
    else {
        Write-FailHealth "Blueprint stage-03 must register a console regression for creation clear reason trace fields and JSON output."
    }

    if ($functionDocText -and $diagnosticsDocText -and
        $functionDocText.Contains("0.933-blueprint-creation-clear-reason-trace") -and
        $functionDocText.Contains("BlueprintCreationLastClearReasonTrace") -and
        $functionDocText.Contains("creationClearTrace") -and
        $diagnosticsDocText.Contains("0.933-blueprint-creation-clear-reason-trace") -and
        $diagnosticsDocText.Contains("BlueprintCreationLastClearReasonTrace") -and
        $diagnosticsDocText.Contains("creationClearTrace") -and
        $diagnosticsDocText.Contains('不改变 `HandlePointer(...)`')) {
        Write-Pass "Blueprint function and diagnostics docs describe the stage-03 clear reason trace and no-behavior-change boundary."
    }
    else {
        Write-FailHealth "Blueprint stage-03 docs must describe 0.933 clear reason trace fields, action metadata, and no behavior-change boundary."
    }

    if ($plan00Text -and $plan03Text -and $currentPlanIndexText -and
        ($plan00Text.Contains('`03-创建状态机清空原因追踪` 已完成') -or
            $plan00Text.Contains('已完成，RuntimeVersion `0.933-blueprint-creation-clear-reason-trace`')) -and
        ($plan00Text.Contains('下一入口为 `04-诊断回归与审计防线.md`') -or
            $plan00Text.Contains('下一入口为 `05-验证打包与回传口径.md`') -or
            $plan00Text.Contains("0.935-blueprint-creation-diagnostic-package")) -and
        $plan00Text.Contains("0.933-blueprint-creation-clear-reason-trace") -and
        $plan03Text.Contains("状态：已完成") -and
        $plan03Text.Contains('RuntimeVersion 已推进到 `0.933-blueprint-creation-clear-reason-trace`') -and
        $plan03Text.Contains("BlueprintCreationClearReasonTraceRecordsStateAndCoordinates") -and
        $plan03Text.Contains("Test-BlueprintCreationClearReasonTraceStage03Governance") -and
        $plan03Text.Contains("不生成测试包") -and
        $plan03Text.Contains('不在本会话实现、修改或验证 `04`') -and
        ($currentPlanIndexText.Contains('已完成 `03-创建状态机清空原因追踪.md`') -or
            $currentPlanIndexText.Contains('`03` 已接入 `BlueprintCreationLastClearReasonTrace`') -or
            $currentPlanIndexText.Contains("0.935-blueprint-creation-diagnostic-package")) -and
        ($currentPlanIndexText.Contains('后续唯一入口为 `04-诊断回归与审计防线.md`') -or
            $currentPlanIndexText.Contains('后续唯一入口为 `05-验证打包与回传口径.md`') -or
            $currentPlanIndexText.Contains("0.935-blueprint-creation-diagnostic-package"))) {
        Write-Pass "Blueprint diagnostic stage-03 plan files record completion, no-package boundary, scoped regression/audit, and stage-04 handoff."
    }
    else {
        Write-FailHealth "Blueprint diagnostic stage-03 plan files must record completion, 0.933 version, scoped regression/audit, no-package/no-04 boundary, and stage-04 handoff."
    }

    if ($updateRecordText -and $updateIndexText -and $docHistoryText -and $docHistoryIndexText -and
        $updateRecordText.Contains("0.933-blueprint-creation-clear-reason-trace") -and
        $updateRecordText.Contains("BlueprintCreationClearReasonTraceRecordsStateAndCoordinates") -and
        $updateRecordText.Contains("Test-BlueprintCreationClearReasonTraceStage03Governance") -and
        $updateRecordText.Contains("不生成测试包") -and
        $updateRecordText.Contains("本地验证不等于用户实机验收") -and
        $updateRecordText.Contains("未新增 AI 经验笔记") -and
        $updateIndexText.Contains("0.933-蓝图创建清空原因追踪-2606222034.md") -and
        $docHistoryText.Contains("BlueprintCreationLastClearReasonTrace") -and
        $docHistoryText.Contains("Test-BlueprintCreationClearReasonTraceStage03Governance") -and
        $docHistoryIndexText.Contains("蓝图创建清空原因追踪-2606222034.md")) {
        Write-Pass "Blueprint diagnostic stage-03 update record and document-change history are synchronized."
    }
    else {
        Write-FailHealth "Blueprint diagnostic stage-03 must synchronize update record/index and document-change history with clear reason trace fields and scoped audit."
    }
}

function Test-BlueprintCreationDiagnosticStage04Governance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $actionServicePath = Join-Path $RepoRoot "src\JueMingZ\Input\LegacyUiActionService.Blueprint.cs"
    $hotkeyServicePath = Join-Path $RepoRoot "src\JueMingZ\Input\BlueprintEntryHotkeyService.cs"
    $testPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintHandheldUiClickOwnershipTests.cs"
    $programPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.cs"
    $auditPath = Join-Path $RepoRoot "scripts\audit-project-health.ps1"
    $functionDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $diagnosticsDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "AI诊断日志说明.md")
    $plan00Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "蓝图创建闪烁诊断版", "00-基准.md")
    $plan04Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "蓝图创建闪烁诊断版", "04-诊断回归与审计防线.md")
    $currentPlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "索引.md")
    $archivePlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "索引.md")
    $updateRecordPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "0.934-蓝图创建诊断回归审计-2606222054.md")
    $updateIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "索引.md")
    $docHistoryPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "蓝图创建诊断回归审计-2606222054.md")
    $docHistoryIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "索引.md")

    if (-not (Test-Path -LiteralPath $plan00Path)) {
        $plan00Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图创建闪烁诊断版", "00-基准.md")
    }

    if (-not (Test-Path -LiteralPath $plan04Path)) {
        $plan04Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图创建闪烁诊断版", "04-诊断回归与审计防线.md")
    }

    $actionServiceText = Read-TextIfExists -Path $actionServicePath
    $hotkeyServiceText = Read-TextIfExists -Path $hotkeyServicePath
    $testText = Read-TextIfExists -Path $testPath
    $programText = Read-TextIfExists -Path $programPath
    $auditText = Read-TextIfExists -Path $auditPath
    $functionDocText = Read-TextIfExists -Path $functionDocPath
    $diagnosticsDocText = Read-TextIfExists -Path $diagnosticsDocPath
    $plan00Text = Read-TextIfExists -Path $plan00Path
    $plan04Text = Read-TextIfExists -Path $plan04Path
    $currentPlanIndexText = Read-TextIfExists -Path $currentPlanIndexPath
    $archivePlanIndexText = Read-TextIfExists -Path $archivePlanIndexPath
    if ($archivePlanIndexText) {
        $currentPlanIndexText = "$currentPlanIndexText`n$archivePlanIndexText"
    }
    $updateRecordText = Read-TextIfExists -Path $updateRecordPath
    $updateIndexText = Read-TextIfExists -Path $updateIndexPath
    $docHistoryText = Read-TextIfExists -Path $docHistoryPath
    $docHistoryIndexText = Read-TextIfExists -Path $docHistoryIndexPath

    if ($testText -and $programText -and
        $testText.Contains("BlueprintCreationDiagnosticContractsStayWired") -and
        $testText.Contains("BlueprintCreationActionMetadataCarriesClearTrace") -and
        $testText.Contains("BlueprintCreationWorldOverlayPhaseTraceSlotsKeepPrefixAndAfter") -and
        $testText.Contains("BlueprintCreationClearReasonTraceRecordsStateAndCoordinates") -and
        $testText.Contains("BlueprintWorldOverlayPointerOwnershipContractsStayWired") -and
        $testText.Contains("BlueprintHotbarPhysicalCoordinateRegressionContractsStayWired") -and
        $testText.Contains("worldMouseSource=OsClient") -and
        $testText.Contains("creationClearTrace") -and
        $programText.Contains("blueprint creation diagnostic contracts stay wired")) {
        Write-Pass "Blueprint stage-04 aggregate regression locks phase slots, clear reasons, coordinate source, action metadata, and adjacent world-overlay/hotbar/hotkey paths."
    }
    else {
        Write-FailHealth "Blueprint stage-04 must register BlueprintCreationDiagnosticContractsStayWired and cover phase slots, clear reason traces, action metadata, adjacent world-overlay ownership, hotbar physical coordinates, and hotkey paths."
    }

    if ($actionServiceText -and $hotkeyServiceText -and
        $actionServiceText.Contains("BuildBlueprintCreationActionMetadataForTesting") -and
        $actionServiceText.Contains("BuildBlueprintHandheldActionMetadataForTesting") -and
        $actionServiceText.Contains("creationClearTrace") -and
        $hotkeyServiceText.Contains("BuildBlueprintActionHotkeyMetadata") -and
        $hotkeyServiceText.Contains("BuildBlueprintActionHotkeyMetadataForTesting") -and
        $hotkeyServiceText.Contains("creationClearTrace")) {
        Write-Pass "Blueprint stage-04 action metadata tests read the same creationClearTrace fragments used by F5 entry, handheld bar, and hotkey events."
    }
    else {
        Write-FailHealth "Blueprint stage-04 must keep creationClearTrace metadata test seams wired to F5 entry, handheld bar, and Hotkey.BlueprintAction event metadata."
    }

    if ($auditText -and
        $auditText.Contains("Test-BlueprintCreationDiagnosticStage04Governance") -and
        $auditText.Contains("Test-BlueprintCreationDiagnosticStage04Governance -RepoRoot `$RepoRoot") -and
        $auditText.Contains("BlueprintCreationDiagnosticContractsStayWired")) {
        Write-Pass "Blueprint scoped health audit includes the stage-04 diagnostic aggregate governance anchor."
    }
    else {
        Write-FailHealth "Blueprint scoped health audit must call Test-BlueprintCreationDiagnosticStage04Governance and describe the aggregate diagnostic contract."
    }

    if ($functionDocText -and $diagnosticsDocText -and
        $functionDocText.Contains("0.934-blueprint-creation-diagnostic-audit") -and
        $functionDocText.Contains("BlueprintCreationDiagnosticContractsStayWired") -and
        $functionDocText.Contains("Test-BlueprintCreationDiagnosticStage04Governance") -and
        $functionDocText.Contains("不生成测试包") -and
        $functionDocText.Contains("不代表蓝图创建闪烁已修复") -and
        $diagnosticsDocText.Contains("0.934-blueprint-creation-diagnostic-audit") -and
        $diagnosticsDocText.Contains("DiagnosticLifecycle=ActiveInvestigation") -and
        $diagnosticsDocText.Contains("BlueprintCreationDiagnosticContractsStayWired") -and
        $diagnosticsDocText.Contains("Test-BlueprintCreationDiagnosticStage04Governance") -and
        $diagnosticsDocText.Contains("不新增 trace JSONL")) {
        Write-Pass "Blueprint function and diagnostics docs describe the stage-04 aggregate regression, diagnostic lifecycle, and no-package/no-behavior-change boundary."
    }
    else {
        Write-FailHealth "Blueprint stage-04 docs must describe 0.934 aggregate diagnostics, ActiveInvestigation fields, no trace JSONL, no-package, and no-fix boundary."
    }

    if ($plan00Text -and $plan04Text -and $currentPlanIndexText -and
        $plan00Text.Contains('`04-诊断回归与审计防线`') -and
        $plan00Text.Contains('已完成，RuntimeVersion `0.934-blueprint-creation-diagnostic-audit`') -and
        ($plan00Text.Contains('下一入口为 `05-验证打包与回传口径.md`') -or
            $plan00Text.Contains("0.935-blueprint-creation-diagnostic-package")) -and
        $plan04Text.Contains("状态：已完成") -and
        $plan04Text.Contains('RuntimeVersion：`0.934-blueprint-creation-diagnostic-audit`') -and
        $plan04Text.Contains("BlueprintCreationDiagnosticContractsStayWired") -and
        $plan04Text.Contains("Test-BlueprintCreationDiagnosticStage04Governance") -and
        $plan04Text.Contains("不生成测试包") -and
        $plan04Text.Contains('不在本会话实现、修改或验证 `05`') -and
        (($currentPlanIndexText.Contains('已完成 `04-诊断回归与审计防线.md`') -and
            $currentPlanIndexText.Contains('后续唯一入口为 `05-验证打包与回传口径.md`')) -or
            ($currentPlanIndexText.Contains("文档/归档历史计划/蓝图创建闪烁诊断版/") -and
                $currentPlanIndexText.Contains("0.935-blueprint-creation-diagnostic-package")))) {
        Write-Pass "Blueprint diagnostic stage-04 plan files record completion, scoped regression/audit, no-package boundary, and stage-05 handoff."
    }
    else {
        Write-FailHealth "Blueprint diagnostic stage-04 plan files must record completion, 0.934 version, aggregate regression/audit, no-package/no-05 boundary, and stage-05 handoff."
    }

    if ($updateRecordText -and $updateIndexText -and $docHistoryText -and $docHistoryIndexText -and
        $updateRecordText.Contains("0.934-blueprint-creation-diagnostic-audit") -and
        $updateRecordText.Contains("BlueprintCreationDiagnosticContractsStayWired") -and
        $updateRecordText.Contains("Test-BlueprintCreationDiagnosticStage04Governance") -and
        $updateRecordText.Contains("不生成测试包") -and
        $updateRecordText.Contains("本地验证不等于用户实机验收") -and
        $updateRecordText.Contains("未新增 AI 经验笔记") -and
        $updateIndexText.Contains("0.934-蓝图创建诊断回归审计-2606222054.md") -and
        $docHistoryText.Contains("BlueprintCreationDiagnosticContractsStayWired") -and
        $docHistoryText.Contains("Test-BlueprintCreationDiagnosticStage04Governance") -and
        $docHistoryIndexText.Contains("蓝图创建诊断回归审计-2606222054.md")) {
        Write-Pass "Blueprint diagnostic stage-04 update record and document-change history are synchronized."
    }
    else {
        Write-FailHealth "Blueprint diagnostic stage-04 must synchronize update record/index and document-change history with aggregate regression and scoped audit."
    }
}

function Test-BlueprintCreationDiagnosticStage05CloseoutGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $auditPath = Join-Path $RepoRoot "scripts\audit-project-health.ps1"
    $currentPlanDirPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "蓝图创建闪烁诊断版")
    $archivedPlan00Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图创建闪烁诊断版", "00-基准.md")
    $archivedPlan05Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图创建闪烁诊断版", "05-验证打包与回传口径.md")
    $currentPlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "索引.md")
    $archivePlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "索引.md")
    $blueprintDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $diagnosticsDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "AI诊断日志说明.md")
    $updateIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "索引.md")
    $updateRecordPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "0.935-蓝图创建诊断包验证收口-2606222116.md")
    $docHistoryIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "索引.md")
    $docHistoryRecordPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "蓝图创建诊断包验证收口-2606222116.md")

    $auditText = Read-TextIfExists -Path $auditPath
    $plan00Text = Read-TextIfExists -Path $archivedPlan00Path
    $plan05Text = Read-TextIfExists -Path $archivedPlan05Path
    $currentPlanIndexText = Read-TextIfExists -Path $currentPlanIndexPath
    $archivePlanIndexText = Read-TextIfExists -Path $archivePlanIndexPath
    $blueprintDocText = Read-TextIfExists -Path $blueprintDocPath
    $diagnosticsDocText = Read-TextIfExists -Path $diagnosticsDocPath
    $updateIndexText = Read-TextIfExists -Path $updateIndexPath
    $updateRecordText = Read-TextIfExists -Path $updateRecordPath
    $docHistoryIndexText = Read-TextIfExists -Path $docHistoryIndexPath
    $docHistoryRecordText = Read-TextIfExists -Path $docHistoryRecordPath

    if ($auditText -and
        $auditText.Contains("Test-BlueprintCreationDiagnosticStage05CloseoutGovernance") -and
        $auditText.Contains("0.935-blueprint-creation-diagnostic-package") -and
        $auditText.Contains("0.935-蓝图创建诊断包验证收口-2606222116.md")) {
        Write-Pass "Blueprint creation diagnostic stage-05 closeout health audit is present and wired to the 0.935 diagnostic package contract."
    }
    else {
        Write-FailHealth "Blueprint creation diagnostic stage-05 closeout health audit must lock the 0.935 package closeout contract and update record."
    }

    if (-not (Test-Path -LiteralPath $currentPlanDirPath) -and
        $plan00Text -and
        $plan00Text.Contains('状态：`05-验证打包与回传口径` 已完成') -and
        $plan00Text.Contains("0.935-blueprint-creation-diagnostic-package") -and
        $plan00Text.Contains("自动串行接力终止") -and
        $plan05Text -and
        $plan05Text.Contains("状态：已完成") -and
        $plan05Text.Contains("0.935-blueprint-creation-diagnostic-package") -and
        $plan05Text.Contains("JueMingZ-TestPackage") -and
        $plan05Text.Contains("严格新鲜包健康审计") -and
        $plan05Text.Contains("Test-BlueprintCreationDiagnosticStage05CloseoutGovernance") -and
        $plan05Text.Contains("不再创建新对话")) {
        Write-Pass "Blueprint creation diagnostic plan is archived with the 0.935 package delivery and no further handoff."
    }
    else {
        Write-FailHealth "Stage-05 closeout must move the blueprint creation diagnostic plan to archive and mark 05 complete with package/fresh-audit scope."
    }

    if ($currentPlanIndexText -and
        $currentPlanIndexText.Contains("当前没有正在推进的计划") -and
        $currentPlanIndexText.Contains("文档/归档历史计划/蓝图创建闪烁诊断版/") -and
        $archivePlanIndexText -and
        $archivePlanIndexText.Contains("文档/归档历史计划/蓝图创建闪烁诊断版/") -and
        $archivePlanIndexText.Contains("0.935-blueprint-creation-diagnostic-package") -and
        $archivePlanIndexText.Contains("自动接力已终止")) {
        Write-Pass "Current and archived plan indices record the blueprint creation diagnostic package closeout and relay termination."
    }
    else {
        Write-FailHealth "Stage-05 creation diagnostic closeout must update current and archived plan indices."
    }

    if ($blueprintDocText -and
        $blueprintDocText.Contains("0.935-blueprint-creation-diagnostic-package") -and
        $blueprintDocText.Contains("文档/归档历史计划/蓝图创建闪烁诊断版/00-基准.md") -and
        $blueprintDocText.Contains("不修复蓝图创建闪烁") -and
        $blueprintDocText.Contains("不新增 trace JSONL") -and
        $diagnosticsDocText -and
        $diagnosticsDocText.Contains("0.935-blueprint-creation-diagnostic-package") -and
        $diagnosticsDocText.Contains("DiagnosticLifecycle=ActiveInvestigation") -and
        $diagnosticsDocText.Contains("runtime-snapshot.json") -and
        $diagnosticsDocText.Contains("action-events-YYYYMMDD.jsonl")) {
        Write-Pass "Blueprint feature and diagnostics docs record the 0.935 diagnostic package closeout without claiming a behavior fix."
    }
    else {
        Write-FailHealth "Stage-05 creation diagnostic closeout must update blueprint feature and diagnostics docs with no-fix/no-new-trace and user return-file scope."
    }

    if ($updateIndexText -and
        $updateIndexText.Contains("0.935-蓝图创建诊断包验证收口-2606222116.md") -and
        $updateRecordText -and
        $updateRecordText.Contains('RuntimeVersion：`0.935-blueprint-creation-diagnostic-package`') -and
        $updateRecordText.Contains("JueMingZ-TestPackage") -and
        $updateRecordText.Contains("-RequireFreshTestPackage") -and
        $docHistoryIndexText -and
        $docHistoryIndexText.Contains("蓝图创建诊断包验证收口-2606222116.md") -and
        $docHistoryRecordText -and
        $docHistoryRecordText.Contains("0.935-blueprint-creation-diagnostic-package")) {
        Write-Pass "Stage-05 blueprint creation diagnostic update record and document-change history are synchronized."
    }
    else {
        Write-FailHealth "Stage-05 creation diagnostic update record, update index, and document-change history must reference the 0.935 package closeout."
    }
}

function Test-BlueprintWorldOverlayPointerOwnershipStage06CloseoutGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $auditPath = Join-Path $RepoRoot "scripts\audit-project-health.ps1"
    $currentPlanDirPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "蓝图创建选区UI所有权误拦修复")
    $archivedPlan00Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图创建选区UI所有权误拦修复", "00-基准.md")
    $archivedPlan06Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图创建选区UI所有权误拦修复", "06-验证打包与归档收口.md")
    $currentPlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "索引.md")
    $archivePlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "索引.md")
    $blueprintDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $diagnosticsDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "AI诊断日志说明.md")
    $updateIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "索引.md")
    $updateRecordPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "0.929-蓝图选区所有权验证收口-2606221601.md")
    $docHistoryIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "索引.md")
    $docHistoryRecordPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "蓝图选区所有权验证收口-2606221601.md")

    $auditText = Read-TextIfExists -Path $auditPath
    $plan00Text = Read-TextIfExists -Path $archivedPlan00Path
    $plan06Text = Read-TextIfExists -Path $archivedPlan06Path
    $currentPlanIndexText = Read-TextIfExists -Path $currentPlanIndexPath
    $archivePlanIndexText = Read-TextIfExists -Path $archivePlanIndexPath
    $blueprintDocText = Read-TextIfExists -Path $blueprintDocPath
    $diagnosticsDocText = Read-TextIfExists -Path $diagnosticsDocPath
    $updateIndexText = Read-TextIfExists -Path $updateIndexPath
    $updateRecordText = Read-TextIfExists -Path $updateRecordPath
    $docHistoryIndexText = Read-TextIfExists -Path $docHistoryIndexPath
    $docHistoryRecordText = Read-TextIfExists -Path $docHistoryRecordPath

    if ($auditText -and
        $auditText.Contains("Test-BlueprintWorldOverlayPointerOwnershipStage06CloseoutGovernance") -and
        $auditText.Contains("0.929-blueprint-world-overlay-ownership-closeout") -and
        $auditText.Contains("0.929-蓝图选区所有权验证收口-2606221601.md")) {
        Write-Pass "Blueprint world-overlay ownership stage-06 closeout health audit is present and wired to the 0.929 closeout contract."
    }
    else {
        Write-FailHealth "Blueprint world-overlay ownership stage-06 closeout health audit must lock the 0.929 closeout contract and update record."
    }

    if (-not (Test-Path -LiteralPath $currentPlanDirPath) -and
        $plan00Text -and
        $plan00Text.Contains('状态：`06-验证打包与归档收口` 已完成') -and
        $plan00Text.Contains("0.929-blueprint-world-overlay-ownership-closeout") -and
        $plan00Text.Contains("自动串行接力终止") -and
        $plan06Text -and
        $plan06Text.Contains("状态：已完成") -and
        $plan06Text.Contains("0.929-blueprint-world-overlay-ownership-closeout") -and
        $plan06Text.Contains("JueMingZ-TestPackage") -and
        $plan06Text.Contains("严格新鲜包健康审计") -and
        $plan06Text.Contains("不再创建新对话")) {
        Write-Pass "Blueprint world-overlay ownership plan is archived with the 0.929 closeout, package delivery, and no further handoff."
    }
    else {
        Write-FailHealth "Stage-06 closeout must move the blueprint world-overlay ownership plan to archive and mark 06 complete with package/fresh-audit scope."
    }

    if ($currentPlanIndexText -and
        $currentPlanIndexText.Contains("当前没有正在推进的计划") -and
        $currentPlanIndexText.Contains("文档/归档历史计划/蓝图创建选区UI所有权误拦修复/") -and
        $archivePlanIndexText -and
        $archivePlanIndexText.Contains("文档/归档历史计划/蓝图创建选区UI所有权误拦修复/") -and
        $archivePlanIndexText.Contains("0.929-blueprint-world-overlay-ownership-closeout") -and
        $archivePlanIndexText.Contains("自动接力已终止")) {
        Write-Pass "Current and archived plan indices record the blueprint world-overlay ownership closeout and relay termination."
    }
    else {
        Write-FailHealth "Stage-06 world-overlay ownership closeout must update current and archived plan indices."
    }

    if ($blueprintDocText -and
        $blueprintDocText.Contains("0.929-blueprint-world-overlay-ownership-closeout") -and
        $blueprintDocText.Contains("文档/归档历史计划/蓝图创建选区UI所有权误拦修复/00-基准.md") -and
        $blueprintDocText.Contains("BlueprintWorldOverlayPointerOwnershipContractsStayWired") -and
        $blueprintDocText.Contains("不新增用户可见蓝图行为") -and
        $diagnosticsDocText -and
        $diagnosticsDocText.Contains("0.929-blueprint-world-overlay-ownership-closeout") -and
        $diagnosticsDocText.Contains("不新增诊断字段") -and
        $diagnosticsDocText.Contains("BlueprintWorldOverlayLastInputTrace")) {
        Write-Pass "Blueprint feature and diagnostics docs record the 0.929 closeout without expanding runtime or diagnostics scope."
    }
    else {
        Write-FailHealth "Stage-06 world-overlay ownership closeout must update blueprint feature and diagnostics docs with no-new-runtime/no-new-diagnostics scope."
    }

    if ($updateIndexText -and
        $updateIndexText.Contains("0.929-蓝图选区所有权验证收口-2606221601.md") -and
        $updateRecordText -and
        $updateRecordText.Contains('RuntimeVersion：`0.929-blueprint-world-overlay-ownership-closeout`') -and
        $updateRecordText.Contains("JueMingZ-TestPackage") -and
        $updateRecordText.Contains("-RequireFreshTestPackage") -and
        $docHistoryIndexText -and
        $docHistoryIndexText.Contains("蓝图选区所有权验证收口-2606221601.md") -and
        $docHistoryRecordText -and
        $docHistoryRecordText.Contains("0.929-blueprint-world-overlay-ownership-closeout")) {
        Write-Pass "Stage-06 world-overlay ownership update record and document-change history are synchronized."
    }
    else {
        Write-FailHealth "Stage-06 world-overlay ownership update record, update index, and document-change history must reference the 0.929 closeout."
    }
}

function Test-BlueprintUiClickStage05CloseoutGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $auditPath = Join-Path $RepoRoot "scripts\audit-project-health.ps1"
    $currentPlanDirPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "蓝图手持栏UI点击所有权治理")
    $archivedPlan00Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图手持栏UI点击所有权治理", "00-基准.md")
    $archivedPlan05Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图手持栏UI点击所有权治理", "05-验证打包与归档收口.md")
    $currentPlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "索引.md")
    $archivePlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "索引.md")
    $blueprintDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $diagnosticsDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "AI诊断日志说明.md")
    $updateIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "索引.md")
    $updateRecordPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "0.905-蓝图UI点击验证收口-2606220031.md")
    $docHistoryIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "索引.md")
    $docHistoryRecordPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "蓝图UI点击验证收口-2606220031.md")

    $auditText = Read-TextIfExists -Path $auditPath
    $plan00Text = Read-TextIfExists -Path $archivedPlan00Path
    $plan05Text = Read-TextIfExists -Path $archivedPlan05Path
    $currentPlanIndexText = Read-TextIfExists -Path $currentPlanIndexPath
    $archivePlanIndexText = Read-TextIfExists -Path $archivePlanIndexPath
    $blueprintDocText = Read-TextIfExists -Path $blueprintDocPath
    $diagnosticsDocText = Read-TextIfExists -Path $diagnosticsDocPath
    $updateIndexText = Read-TextIfExists -Path $updateIndexPath
    $updateRecordText = Read-TextIfExists -Path $updateRecordPath
    $docHistoryIndexText = Read-TextIfExists -Path $docHistoryIndexPath
    $docHistoryRecordText = Read-TextIfExists -Path $docHistoryRecordPath

    if ($auditText -and
        $auditText.Contains("Test-BlueprintUiClickStage05CloseoutGovernance") -and
        $auditText.Contains("0.905-blueprint-ui-click-closeout") -and
        $auditText.Contains("0.905-蓝图UI点击验证收口-2606220031.md")) {
        Write-Pass "Blueprint UI click stage-05 closeout health audit is present and wired to the 0.905 closeout contract."
    }
    else {
        Write-FailHealth "Blueprint UI click stage-05 closeout health audit must lock the 0.905 closeout contract and update record."
    }

    if (-not (Test-Path -LiteralPath $currentPlanDirPath) -and
        $plan00Text -and
        $plan00Text.Contains('状态：`05-验证打包与归档收口` 已完成') -and
        $plan00Text.Contains("0.905-blueprint-ui-click-closeout") -and
        $plan00Text.Contains("自动串行接力终止") -and
        $plan05Text -and
        $plan05Text.Contains("状态：已完成") -and
        $plan05Text.Contains("0.905-blueprint-ui-click-closeout") -and
        $plan05Text.Contains("JueMingZ-TestPackage") -and
        $plan05Text.Contains("严格新鲜包健康审计") -and
        $plan05Text.Contains("不创建后续")) {
        Write-Pass "Blueprint UI click plan is archived with the 0.905 closeout, package delivery, and no further handoff."
    }
    else {
        Write-FailHealth "Stage-05 closeout must move the blueprint UI click plan to archive and mark 05 complete with package/fresh-audit scope."
    }

    if ($currentPlanIndexText -and
        $currentPlanIndexText.Contains("当前没有正在推进的计划") -and
        $currentPlanIndexText.Contains("文档/归档历史计划/蓝图手持栏UI点击所有权治理/") -and
        $archivePlanIndexText -and
        $archivePlanIndexText.Contains("文档/归档历史计划/蓝图手持栏UI点击所有权治理/") -and
        $archivePlanIndexText.Contains("0.905-blueprint-ui-click-closeout") -and
        $archivePlanIndexText.Contains("自动接力已终止")) {
        Write-Pass "Current and archived plan indices record the blueprint UI click closeout and relay termination."
    }
    else {
        Write-FailHealth "Stage-05 closeout must update current and archived plan indices."
    }

    if ($blueprintDocText -and
        $blueprintDocText.Contains("0.905-blueprint-ui-click-closeout") -and
        $blueprintDocText.Contains("文档/归档历史计划/蓝图手持栏UI点击所有权治理/00-基准.md") -and
        $blueprintDocText.Contains("不新增用户可见蓝图运行时行为") -and
        $diagnosticsDocText -and
        $diagnosticsDocText.Contains("0.905-blueprint-ui-click-closeout") -and
        $diagnosticsDocText.Contains("不新增诊断字段")) {
        Write-Pass "Blueprint feature and diagnostics docs record the 0.905 closeout without expanding runtime or diagnostics scope."
    }
    else {
        Write-FailHealth "Stage-05 closeout must update blueprint feature and diagnostics docs with the no-new-runtime/no-new-diagnostics scope."
    }

    if ($updateIndexText -and
        $updateIndexText.Contains("0.905-蓝图UI点击验证收口-2606220031.md") -and
        $updateRecordText -and
        $updateRecordText.Contains('RuntimeVersion：`0.905-blueprint-ui-click-closeout`') -and
        $updateRecordText.Contains("JueMingZ-TestPackage") -and
        $updateRecordText.Contains("-RequireFreshTestPackage") -and
        $docHistoryIndexText -and
        $docHistoryIndexText.Contains("蓝图UI点击验证收口-2606220031.md") -and
        $docHistoryRecordText -and
        $docHistoryRecordText.Contains("0.905-blueprint-ui-click-closeout")) {
        Write-Pass "Stage-05 update record and document-change history are synchronized."
    }
    else {
        Write-FailHealth "Stage-05 update record, update index, and document-change history must reference the 0.905 closeout."
    }
}

function Test-BlueprintActionShortcutGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $uiPath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyMainWindow.Blueprint.cs"
    $hotkeyPath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyMainWindow.Blueprint.Hotkey.cs"
    $uiActionPath = Join-Path $RepoRoot "src\JueMingZ\Input\LegacyUiActionService.Blueprint.cs"
    $routerPath = Join-Path $RepoRoot "src\JueMingZ\Input\LegacyUiActionService.CommandRouter.cs"
    $creationStatePath = Join-Path $RepoRoot "src\JueMingZ\Automation\Blueprint\BlueprintCreationMaskState.cs"
    $creationOverlayPath = Join-Path $RepoRoot "src\JueMingZ\UI\BlueprintCreationOverlay.cs"
    $capturePath = Join-Path $RepoRoot "src\JueMingZ\Automation\Blueprint\BlueprintCaptureService.cs"
    $templateStorePath = Join-Path $RepoRoot "src\JueMingZ\Automation\Blueprint\BlueprintTemplateLibraryStore.cs"
    $storagePathsPath = Join-Path $RepoRoot "src\JueMingZ\Automation\Blueprint\BlueprintStoragePaths.cs"
    $featureIdsPath = Join-Path $RepoRoot "src\JueMingZ\Common\FeatureIds.cs"
    $conflictPath = Join-Path $RepoRoot "src\JueMingZ\Config\FeatureToggleHotkeyConflictRegistry.cs"
    $testPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintEntryTests.cs"
    $libraryTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintLibraryTests.cs"
    $storageTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintStorageTests.cs"
    $creationTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintCreationTests.cs"
    $programPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.cs"
    $functionDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $plan03Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "蓝图功能实机反馈修补", "03-F5蓝图创建保存入口.md")
    if (-not (Test-Path -LiteralPath $plan03Path)) {
        $plan03Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图功能实机反馈修补", "03-F5蓝图创建保存入口.md")
    }

    $uiText = Read-TextIfExists -Path $uiPath
    $hotkeyText = Read-TextIfExists -Path $hotkeyPath
    $uiActionText = Read-TextIfExists -Path $uiActionPath
    $routerText = Read-TextIfExists -Path $routerPath
    $creationStateText = Read-TextIfExists -Path $creationStatePath
    $creationOverlayText = Read-TextIfExists -Path $creationOverlayPath
    $captureText = Read-TextIfExists -Path $capturePath
    $templateStoreText = Read-TextIfExists -Path $templateStorePath
    $storagePathsText = Read-TextIfExists -Path $storagePathsPath
    $featureIdsText = Read-TextIfExists -Path $featureIdsPath
    $conflictText = Read-TextIfExists -Path $conflictPath
    $testText = Read-TextIfExists -Path $testPath
    $libraryTestText = Read-TextIfExists -Path $libraryTestPath
    $storageTestText = Read-TextIfExists -Path $storageTestPath
    $creationTestText = Read-TextIfExists -Path $creationTestPath
    $programText = Read-TextIfExists -Path $programPath
    $functionDocText = Read-TextIfExists -Path $functionDocPath
    $plan03Text = Read-TextIfExists -Path $plan03Path

    if ($uiText -and
        $uiText.Contains("BlueprintActionShortcutVisualContract") -and
        $uiText.Contains("real-create-save-library-entry") -and
        $uiText.Contains("short-hotkey-fields") -and
        $uiText.Contains("BlueprintActionHotkeyInputMaxWidth") -and
        $uiText.Contains("auto-mining-hotkey-shape") -and
        $uiText.Contains("blueprint-action-hotkey:") -and
        $uiText.Contains("blueprint-action-entry:") -and
        $uiText.Contains("BlueprintCreateAction") -and
        $uiText.Contains("BlueprintSaveAction") -and
        $uiText.Contains("BlueprintLibraryAction") -and
        $uiText.Contains("左键按住滑动选区，可多选") -and
        $uiText.Contains("退出创建蓝图") -and
        $uiText.Contains("IsBlueprintCreateActionExitState") -and
        $uiText.Contains("start-create-toggle-exit-label") -and
        $uiText.Contains("保存当前选区为蓝图") -and
        $uiText.Contains("打开蓝图库。") -and
        $uiText.Contains("双击录入采集按键。")) {
        Write-Pass "Blueprint F5 create/save/library shortcut rows keep the visual ids, shortened hotkey fields, dynamic create/exit tooltip, action hotkeys, and real UI entry contract."
    }
    else {
        Write-FailHealth "Blueprint F5 create/save/library shortcut rows must keep independent shortened action hotkey fields, real create/save/library action buttons, dynamic create/exit tooltip, and the required tooltips."
    }

    if ($featureIdsText -and
        $featureIdsText.Contains('BlueprintCreateAction = "blueprint.create"') -and
        $featureIdsText.Contains('BlueprintSaveAction = "blueprint.save"') -and
        $featureIdsText.Contains('BlueprintLibraryAction = "blueprint.library"') -and
        $hotkeyText -and
        $hotkeyText.Contains("TrySaveBlueprintActionHotkey") -and
        $hotkeyText.Contains("NormalizeBlueprintHotkeyTargetId") -and
        $conflictText -and
        $conflictText.Contains("FeatureToggleHotkeyConflictType.BlueprintAction") -and
        $conflictText.Contains("蓝图创建快捷键") -and
        $conflictText.Contains("蓝图保存快捷键") -and
        $conflictText.Contains("蓝图库打开快捷键")) {
        Write-Pass "Blueprint create/save/library hotkeys use separate action keys and participate in hotkey conflict detection."
    }
    else {
        Write-FailHealth "Blueprint create/save/library hotkeys must not reuse blueprint.main and must be covered by conflict detection."
    }

    $handlerMatch = [System.Text.RegularExpressions.Regex]::Match(
        $uiActionText,
        "private\s+static\s+void\s+HandleBlueprintActionEntryCommand[\s\S]*?private\s+static\s+void\s+HandleBlueprintToolItemCommand")
    $handlerText = if ($handlerMatch.Success) { $handlerMatch.Value } else { "" }
    if ($routerText -and
        $routerText.Contains("blueprint-action-hotkey:") -and
        $routerText.Contains("blueprint-action-entry:") -and
        $handlerText.Contains("Ui.Blueprint.CreateSaveEntry") -and
        $handlerText.Contains('\"submitted\":false') -and
        $handlerText.Contains('BoolRaw(!result.PlaceholderOnly)') -and
        $handlerText.Contains('\"uiOnly\":true') -and
        $handlerText.Contains("BlueprintEntryState.ApplyCommand") -and
        $handlerText.Contains("BlueprintCaptureService.CapturePendingMaskAndSave") -and
        $handlerText.Contains("BlueprintLibraryUiState.NotifyTemplateCreated") -and
        $handlerText.Contains("BlueprintLibraryUiState.OpenLibrary") -and
        $handlerText.Contains("RevealBlueprintLibraryMenuIfOpened") -and
        $handlerText.Contains("BlueprintEntryState.MarkCaptureSaved") -and
        -not $handlerText.Contains("stage03UiOnlyNotImplemented") -and
        -not $handlerText.Contains("ForceRefresh")) {
        Write-Pass "Blueprint F5 create/save/library action buttons enter the creation mask/save/library UI path without refreshing projection or material caches."
    }
    else {
        Write-FailHealth "Blueprint F5 create/save/library action buttons must call the real create/save/library UI state path and avoid projection/material refreshes."
    }

    if ($uiText -and
        $uiActionText -and
        $libraryTestText -and
        $storageTestText -and
        $templateStoreText -and
        $storagePathsText -and
        $programText -and
        $uiText.Contains("stage06-two-column-fixed-cards") -and
        $uiText.Contains("preview-scales-to-fit") -and
        $uiText.Contains("stage07-name-edit-delete-confirm") -and
        $uiText.Contains("stage08-import-export-windows-dialog") -and
        $uiText.Contains("stage09-layout-use-real-template-snapshot") -and
        $uiText.Contains("stage02-title-row-tools") -and
        $uiText.Contains("card-material-toggle") -and
        $uiText.Contains("card-buttons-no-tooltips") -and
        $uiText.Contains("summary-placed-count") -and
        $uiText.Contains("CalculateBlueprintLibraryImportButtonRect") -and
        $uiText.Contains('BuildCommandId("import", string.Empty)') -and
        $uiText.Contains("BlueprintLibraryCardColumns = 2") -and
        $uiText.Contains("BlueprintLibraryCardHeight") -and
        $uiText.Contains("CalculateBlueprintLibraryCardRect") -and
        $uiText.Contains("TryResolveBlueprintTemplatePreviewLayout") -and
        $uiText.Contains("BlueprintLibraryPreviewMaxDrawCells") -and
        $uiText.Contains("DrawBlueprintTemplateNameInput") -and
        $uiText.Contains("ConfirmNameAction") -and
        $uiText.Contains('deleteConfirming ? "确认" : "删除"') -and
        $uiText.Contains('"layout-use"') -and
        $uiText.Contains('"layout-export"') -and
        $uiText.Contains('"layout-materials"') -and
        $uiActionText.Contains('"layout-name"') -and
        $uiActionText.Contains('HandleBlueprintLibraryAction(command, "name", templateId)') -and
        $uiActionText.Contains('HandleBlueprintLibraryAction(command, "delete", templateId)') -and
        $uiActionText.Contains('HandleBlueprintLibraryAction(command, "export", templateId)') -and
        $uiActionText.Contains('HandleBlueprintLibraryAction(command, "use", templateId)') -and
        $uiActionText.Contains('HandleBlueprintLibraryAction(command, "materials", templateId)') -and
        $uiActionText.Contains("BlueprintLibraryUiState.ImportTemplate") -and
        $uiActionText.Contains("BlueprintLibraryUiState.ToggleMaterialList") -and
        $uiActionText.Contains('\"importPath\"') -and
        $uiActionText.Contains('"layout-use"') -and
        $uiActionText.Contains('"layout-export"') -and
        $uiActionText.Contains('"layout-delete"') -and
        $uiActionText.Contains('"layout-materials"') -and
        $uiActionText.Contains("PlaceholderOnly") -and
        $templateStoreText.Contains("ImportTemplate") -and
        $templateStoreText.Contains("ResolveImportPath") -and
        $templateStoreText.Contains("ambiguousImportFile") -and
        $templateStoreText.Contains("draft.TemplateId = string.Empty") -and
        $storagePathsText.Contains("BuildDefaultImportDirectory") -and
        $storagePathsText.Contains("ImportDirectoryName") -and
        $storagePathsText.Contains("BuildDefaultExportDirectory") -and
        $storagePathsText.Contains("BuildDefaultExportFileName") -and
        $libraryTestText.Contains("BlueprintLibraryTwoColumnCardsPreviewAndLayoutButtons") -and
        $libraryTestText.Contains("BlueprintLibraryStage02FileDialogAndMaterialContracts") -and
        $libraryTestText.Contains("FakeBlueprintFileDialogService") -and
        $libraryTestText.Contains("BlueprintLibraryStage07NamingRenameDeleteConfirmKeepsInstances") -and
        $libraryTestText.Contains("BlueprintLibraryStage08ImportExportDiagnostics") -and
        $libraryTestText.Contains("BlueprintLibraryStage09UseSnapshotAndInstanceBoundary") -and
        $libraryTestText.Contains("stage09-layout-use-real-template-snapshot") -and
        $libraryTestText.Contains("Edited Instance Snapshot") -and
        $libraryTestText.Contains("CalculateBlueprintLibraryImportButtonRectForTesting") -and
        $libraryTestText.Contains("LastImportPath") -and
        $libraryTestText.Contains("DeleteConfirmTemplateId") -and
        $libraryTestText.Contains("TemplateSnapshot.Name") -and
        $libraryTestText.Contains("TryResolveBlueprintTemplatePreviewLayoutForTesting") -and
        $libraryTestText.Contains("HandleBlueprintLibraryActionForTesting") -and
        $storageTestText.Contains("BlueprintTemplateImportUsesSuffixAndKeepsExistingLibraryOnFailure") -and
        $storageTestText.Contains("Shared Import 2") -and
        $programText.Contains("blueprint library two-column cards preview and layout buttons") -and
        $programText.Contains("blueprint library stage 02 file dialog and material contracts") -and
        $programText.Contains("blueprint library stage 07 naming rename delete confirm keeps instances") -and
        $programText.Contains("blueprint library stage 08 import export diagnostics") -and
        $programText.Contains("blueprint library stage 09 use snapshot and instance boundary") -and
        $programText.Contains("blueprint template import uses suffix and keeps existing library on failure")) {
        Write-Pass "Blueprint library keeps two-column fixed cards, preview scaling, stage 02 Windows dialog/material UI, stage 07 name/delete commands, stage 08 import/export, stage 09 real use snapshot boundary, and registered regression coverage."
    }
    else {
        Write-FailHealth "Blueprint library must keep two-column fixed cards, preview scaling, stage 02 Windows dialog/material UI, stage 07 name/delete commands, stage 08 import/export, stage 09 real use snapshot boundary, and registered tests."
    }

    if ($testText -and
        $creationTestText -and
        $testText.Contains("BlueprintActionHotkeysUseSeparateKeysAndConflictSources") -and
        $testText.Contains("BlueprintCreateSaveActionCommandsEnterMaskAndSaveWithoutProjectionScan") -and
        $testText.Contains("BlueprintCreateActionButtonSyncsExitStateWithSharedToggle") -and
        $testText.Contains("BlueprintLibrarySubmenuAndShortcutRowsOpenSameUiState") -and
        $testText.Contains("BlueprintLibraryAction") -and
        $testText.Contains("退出创建蓝图") -and
        $creationTestText.Contains("BlueprintCreationMaskSelectsAirAndTracksBounds") -and
        $programText.Contains("blueprint action hotkeys use separate keys and conflict sources") -and
        $programText.Contains("blueprint create save action commands enter mask and save without projection scan") -and
        $programText.Contains("blueprint create action button syncs exit state with shared toggle") -and
        $programText.Contains("blueprint library submenu and shortcut rows open same UI state") -and
        $programText.Contains("blueprint creation mask selects air and tracks bounds")) {
        Write-Pass "Blueprint create/save/library shortcut console tests cover hotkey separation, real create/save, F5 create/exit sync, library submenu, and are registered."
    }
    else {
        Write-FailHealth "Blueprint create/save/library shortcut tests must cover hotkey separation/conflicts, real create/save no-projection-scan commands, F5 create/exit sync, library submenu, and air-select mask bounds."
    }

    if ($creationStateText -and
        $creationStateText.Contains("HoverTileHit") -and
        $creationStateText.Contains("ContentKnown") -and
        $creationStateText.Contains("HasSelectableContent") -and
        $creationStateText.Contains("IsSelectableTile") -and
        $creationStateText.Contains("tileUnavailable") -and
        -not $creationStateText.Contains('"airSkipped"') -and
        $captureText.Contains("TryHasSelectableContent") -and
        $captureText.Contains("HasSelectableContent(BlueprintWorldTileSnapshot") -and
        $captureText.Contains("TryResolveSelectedBounds") -and
        $captureText.Contains("Air cells are stored as template bounds") -and
        $captureText.Contains("Projection/material/auto-place only consume Cells") -and
        $creationOverlayText.Contains("world-hover+air-select+lower-saturation-lower-alpha-no-border") -and
        $creationOverlayText.Contains("continuous-row-runs") -and
        $creationOverlayText.Contains("DrawSelectedRuns") -and
        $creationOverlayText.Contains("DrawTileFill") -and
        $creationOverlayText.Contains("TryHasSelectableContent") -and
        -not $creationOverlayText.Contains("DrawRectBorderClipped")) {
        Write-Pass "Blueprint creation mask selects readable air, tracks hover/bounds, keeps content-only capture cells, and draws lower-saturation lower-alpha continuous no-border overlay."
    }
    else {
        Write-FailHealth "Blueprint creation mask must select readable air, fail closed on unavailable tiles, preserve air-inclusive bounds with content-only Cells, and draw lower-saturation lower-alpha continuous no-border overlay."
    }

    if ($functionDocText -and
        $functionDocText.Contains("blueprint.create") -and
        $functionDocText.Contains("blueprint.save") -and
        $functionDocText.Contains("左键按住滑动选区，可多选") -and
        $functionDocText.Contains("保存当前选区为蓝图") -and
        $plan03Text -and
        $plan03Text.Contains("0.874-blueprint-f5-create-save-entry")) {
        Write-Pass "Blueprint function docs and stage-03 plan describe the F5 create/save shortcut rows and action hotkey ids."
    }
    else {
        Write-FailHealth "Blueprint docs and stage-03 plan must describe blueprint.create/blueprint.save, tooltips, and the 0.874 completion record."
    }
}

function Join-BlueprintPlacementPlanPath {
    param(
        [Parameter(Mandatory = $true)][string]$RepoRoot,
        [Parameter(Mandatory = $true)][string]$Leaf
    )

    $currentPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "蓝图放置与实例治理修复", $Leaf)
    if (Test-Path -LiteralPath $currentPath) {
        return $currentPath
    }

    return Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图放置与实例治理修复", $Leaf)
}

function Get-BlueprintFeedbackAutoplacePlanDirectory {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $currentPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "蓝图实机反馈与自动放置治理")
    if (Test-Path -LiteralPath $currentPath) {
        return $currentPath
    }

    return Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图实机反馈与自动放置治理")
}

function Join-BlueprintFeedbackAutoplacePlanPath {
    param(
        [Parameter(Mandatory = $true)][string]$RepoRoot,
        [Parameter(Mandatory = $true)][string]$Leaf
    )

    return Join-Path (Get-BlueprintFeedbackAutoplacePlanDirectory -RepoRoot $RepoRoot) $Leaf
}

function Test-BlueprintPlacementVersionMetadata {
    param(
        [string]$RuntimeText,
        [string]$CsprojText,
        [Parameter(Mandatory = $true)][string[]]$AllowedRuntimeVersions
    )

    if (-not $RuntimeText -or -not $CsprojText) {
        return $false
    }

    $effectiveAllowedRuntimeVersions = @($AllowedRuntimeVersions)
    if ($effectiveAllowedRuntimeVersions -contains "0.954-blueprint-placement-closeout") {
        foreach ($forwardVersion in @(
                "0.955-blueprint-feedback-autoplace-plan",
                "0.956-blueprint-feedback-fact-freeze",
                "0.957-blueprint-f5-library-submenus",
                "0.958-blueprint-placed-list-layout",
                "0.959-blueprint-handheld-hit-status",
                "0.960-blueprint-projection-progress-material",
                "0.961-blueprint-move-interaction-linkage",
                "0.962-blueprint-region-continuous-linkage",
                "0.963-blueprint-mirror-one-shot",
                "0.964-blueprint-autoplace-entry-governance",
                "0.965-blueprint-autoplace-execution-chain",
                "0.966-blueprint-regression-diagnostics-audit",
                "0.967-blueprint-feedback-autoplace-closeout",
                "0.968-blueprint-world-lifecycle-refresh",
                "0.969-blueprint-region-physical-left",
                "0.970-blueprint-handheld-unscaled-layer",
                "0.971-blueprint-placement-release-gate",
                "0.972-blueprint-erase-empty-region",
                "0.973-blueprint-action-hotkey-layout")) {
            if ($effectiveAllowedRuntimeVersions -notcontains $forwardVersion) {
                $effectiveAllowedRuntimeVersions += $forwardVersion
            }
        }
    }

    if (($effectiveAllowedRuntimeVersions -contains "0.958-blueprint-placed-list-layout") -and
        ($effectiveAllowedRuntimeVersions -notcontains "0.959-blueprint-handheld-hit-status")) {
        $effectiveAllowedRuntimeVersions += "0.959-blueprint-handheld-hit-status"
    }

    if (($effectiveAllowedRuntimeVersions -contains "0.959-blueprint-handheld-hit-status") -and
        ($effectiveAllowedRuntimeVersions -notcontains "0.960-blueprint-projection-progress-material")) {
        $effectiveAllowedRuntimeVersions += "0.960-blueprint-projection-progress-material"
    }

    if (($effectiveAllowedRuntimeVersions -contains "0.960-blueprint-projection-progress-material") -and
        ($effectiveAllowedRuntimeVersions -notcontains "0.961-blueprint-move-interaction-linkage")) {
        $effectiveAllowedRuntimeVersions += "0.961-blueprint-move-interaction-linkage"
    }

    if (($effectiveAllowedRuntimeVersions -contains "0.961-blueprint-move-interaction-linkage") -and
        ($effectiveAllowedRuntimeVersions -notcontains "0.962-blueprint-region-continuous-linkage")) {
        $effectiveAllowedRuntimeVersions += "0.962-blueprint-region-continuous-linkage"
    }

    if (($effectiveAllowedRuntimeVersions -contains "0.962-blueprint-region-continuous-linkage") -and
        ($effectiveAllowedRuntimeVersions -notcontains "0.963-blueprint-mirror-one-shot")) {
        $effectiveAllowedRuntimeVersions += "0.963-blueprint-mirror-one-shot"
    }

    if (($effectiveAllowedRuntimeVersions -contains "0.963-blueprint-mirror-one-shot") -and
        ($effectiveAllowedRuntimeVersions -notcontains "0.964-blueprint-autoplace-entry-governance")) {
        $effectiveAllowedRuntimeVersions += "0.964-blueprint-autoplace-entry-governance"
    }

    if (($effectiveAllowedRuntimeVersions -contains "0.964-blueprint-autoplace-entry-governance") -and
        ($effectiveAllowedRuntimeVersions -notcontains "0.965-blueprint-autoplace-execution-chain")) {
        $effectiveAllowedRuntimeVersions += "0.965-blueprint-autoplace-execution-chain"
    }

    if (($effectiveAllowedRuntimeVersions -contains "0.965-blueprint-autoplace-execution-chain") -and
        ($effectiveAllowedRuntimeVersions -notcontains "0.966-blueprint-regression-diagnostics-audit")) {
        $effectiveAllowedRuntimeVersions += "0.966-blueprint-regression-diagnostics-audit"
    }

    if (($effectiveAllowedRuntimeVersions -contains "0.966-blueprint-regression-diagnostics-audit") -and
        ($effectiveAllowedRuntimeVersions -notcontains "0.967-blueprint-feedback-autoplace-closeout")) {
        $effectiveAllowedRuntimeVersions += "0.967-blueprint-feedback-autoplace-closeout"
    }

    if (($effectiveAllowedRuntimeVersions -contains "0.967-blueprint-feedback-autoplace-closeout") -and
        ($effectiveAllowedRuntimeVersions -notcontains "0.968-blueprint-world-lifecycle-refresh")) {
        $effectiveAllowedRuntimeVersions += "0.968-blueprint-world-lifecycle-refresh"
    }

    if (($effectiveAllowedRuntimeVersions -contains "0.968-blueprint-world-lifecycle-refresh") -and
        ($effectiveAllowedRuntimeVersions -notcontains "0.969-blueprint-region-physical-left")) {
        $effectiveAllowedRuntimeVersions += "0.969-blueprint-region-physical-left"
    }

    if (($effectiveAllowedRuntimeVersions -contains "0.969-blueprint-region-physical-left") -and
        ($effectiveAllowedRuntimeVersions -notcontains "0.970-blueprint-handheld-unscaled-layer")) {
        $effectiveAllowedRuntimeVersions += "0.970-blueprint-handheld-unscaled-layer"
    }

    if (($effectiveAllowedRuntimeVersions -contains "0.970-blueprint-handheld-unscaled-layer") -and
        ($effectiveAllowedRuntimeVersions -notcontains "0.971-blueprint-placement-release-gate")) {
        $effectiveAllowedRuntimeVersions += "0.971-blueprint-placement-release-gate"
    }

    if (($effectiveAllowedRuntimeVersions -contains "0.971-blueprint-placement-release-gate") -and
        ($effectiveAllowedRuntimeVersions -notcontains "0.972-blueprint-erase-empty-region")) {
        $effectiveAllowedRuntimeVersions += "0.972-blueprint-erase-empty-region"
    }

    if (($effectiveAllowedRuntimeVersions -contains "0.972-blueprint-erase-empty-region") -and
        ($effectiveAllowedRuntimeVersions -notcontains "0.973-blueprint-action-hotkey-layout")) {
        $effectiveAllowedRuntimeVersions += "0.973-blueprint-action-hotkey-layout"
    }

    foreach ($runtimeVersion in $effectiveAllowedRuntimeVersions) {
        $versionPrefix = ($runtimeVersion -split '-', 2)[0]
        if ($RuntimeText.Contains($runtimeVersion) -and
            $CsprojText.Contains("<Version>$versionPrefix</Version>") -and
            $CsprojText.Contains("<AssemblyVersion>$versionPrefix.0</AssemblyVersion>") -and
            $CsprojText.Contains("<FileVersion>$versionPrefix.0</FileVersion>") -and
            $CsprojText.Contains("<InformationalVersion>$runtimeVersion</InformationalVersion>")) {
            return $true
        }
    }

    return $false
}

function Test-BlueprintPlacementStage02LibraryUiGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $dialogPath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\BlueprintFileDialogService.cs"
    $libraryStatePath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\BlueprintLibraryUiState.cs"
    $mainWindowPath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyMainWindow.Blueprint.cs"
    $actionPath = Join-Path $RepoRoot "src\JueMingZ\Input\LegacyUiActionService.Blueprint.cs"
    $storagePath = Join-Path $RepoRoot "src\JueMingZ\Automation\Blueprint\BlueprintStoragePaths.cs"
    $csprojPath = Join-Path $RepoRoot "src\JueMingZ\JueMingZ.csproj"
    $libraryTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintLibraryTests.cs"
    $programPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.cs"
    $auditPath = Join-Path $RepoRoot "scripts\audit-project-health.ps1"
    $functionDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $plan00Path = Join-BlueprintPlacementPlanPath -RepoRoot $RepoRoot -Leaf "00-基准.md"
    $plan02Path = Join-BlueprintPlacementPlanPath -RepoRoot $RepoRoot -Leaf "02-F5蓝图库与文件选择UI.md"
    $currentPlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "索引.md")
    $updateIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "索引.md")
    $updateDirectory = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录")
    $docHistoryIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "索引.md")
    $docHistoryDirectory = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史")

    $updateRecordPath = ""
    if (Test-Path -LiteralPath $updateDirectory) {
        $record = Get-ChildItem -LiteralPath $updateDirectory -Filter "0.947-F5蓝图库文件选择UI-*.md" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($record) {
            $updateRecordPath = $record.FullName
        }
    }

    $docHistoryRecordPath = ""
    if (Test-Path -LiteralPath $docHistoryDirectory) {
        $record = Get-ChildItem -LiteralPath $docHistoryDirectory -Filter "蓝图F5蓝图库文件选择UI-*.md" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($record) {
            $docHistoryRecordPath = $record.FullName
        }
    }

    $dialogText = Read-TextIfExists -Path $dialogPath
    $libraryStateText = Read-TextIfExists -Path $libraryStatePath
    $mainWindowText = Read-TextIfExists -Path $mainWindowPath
    $actionText = Read-TextIfExists -Path $actionPath
    $storageText = Read-TextIfExists -Path $storagePath
    $csprojText = Read-TextIfExists -Path $csprojPath
    $libraryTestText = Read-TextIfExists -Path $libraryTestPath
    $programText = Read-TextIfExists -Path $programPath
    $auditText = Read-TextIfExists -Path $auditPath
    $functionDocText = Read-TextIfExists -Path $functionDocPath
    $plan00Text = Read-TextIfExists -Path $plan00Path
    $plan02Text = Read-TextIfExists -Path $plan02Path
    $currentPlanIndexText = Read-TextIfExists -Path $currentPlanIndexPath
    $updateIndexText = Read-TextIfExists -Path $updateIndexPath
    $updateRecordText = if ([string]::IsNullOrWhiteSpace($updateRecordPath)) { $null } else { Read-TextIfExists -Path $updateRecordPath }
    $docHistoryIndexText = Read-TextIfExists -Path $docHistoryIndexPath
    $docHistoryRecordText = if ([string]::IsNullOrWhiteSpace($docHistoryRecordPath)) { $null } else { Read-TextIfExists -Path $docHistoryRecordPath }

    if ($dialogText -and
        $dialogText.Contains("IBlueprintFileDialogService") -and
        $dialogText.Contains("OpenFileDialog") -and
        $dialogText.Contains("SaveFileDialog") -and
        $dialogText.Contains("dialogCancelled") -and
        $dialogText.Contains("dialogUnavailable") -and
        $csprojText -and
        $csprojText.Contains("System.Windows.Forms")) {
        Write-Pass "Blueprint stage 02 keeps Windows file dialogs behind the narrow BlueprintFileDialogService wrapper."
    }
    else {
        Write-FailHealth "Blueprint stage 02 file dialogs must stay behind IBlueprintFileDialogService with OpenFileDialog/SaveFileDialog and System.Windows.Forms reference."
    }

    if ($libraryStateText -and
        $libraryStateText.Contains("SetFileDialogServiceForTesting") -and
        $libraryStateText.Contains("ChooseImportJsonPath") -and
        $libraryStateText.Contains("ChooseExportJsonPath") -and
        $libraryStateText.Contains("ToggleMaterialList") -and
        $libraryStateText.Contains("BuildTemplateMaterialListText") -and
        $libraryStateText.Contains("ExpandedMaterialTemplateId") -and
        $storageText -and
        $storageText.Contains("BuildDefaultExportDirectory") -and
        $storageText.Contains("BuildDefaultExportFileName")) {
        Write-Pass "Blueprint library UI state uses dialog-selected paths and template-local material expansion without broad refresh work."
    }
    else {
        Write-FailHealth "Blueprint library UI state must route import/export through the dialog wrapper and keep material expansion as template-local UI state."
    }

    if ($mainWindowText -and
        $mainWindowText.Contains("stage02-title-row-tools") -and
        $mainWindowText.Contains("card-material-toggle") -and
        $mainWindowText.Contains("card-buttons-no-tooltips") -and
        $mainWindowText.Contains("summary-placed-count") -and
        $mainWindowText.Contains("DrawBlueprintPlacedSubmenu") -and
        $mainWindowText.Contains("已放置蓝图列表") -and
        $mainWindowText.Contains("layout-materials") -and
        $mainWindowText.Contains('"放置"') -and
        $actionText -and
        $actionText.Contains("layout-materials") -and
        $actionText.Contains("ToggleMaterialList")) {
        Write-Pass "Blueprint stage 02 F5 UI exposes title-row tools, placed-list submenu, card material toggle, and no-tooltip card actions."
    }
    else {
        Write-FailHealth "Blueprint stage 02 F5 UI must expose title-row import/paging, placed-list submenu, card material toggle, and route layout-materials to UI state only."
    }

    if ($libraryTestText -and
        $libraryTestText.Contains("BlueprintLibraryStage02FileDialogAndMaterialContracts") -and
        $libraryTestText.Contains("FakeBlueprintFileDialogService") -and
        $libraryTestText.Contains("layout-materials") -and
        $libraryTestText.Contains("BuildDefaultExportFileNameForTesting") -and
        $programText -and
        $programText.Contains("blueprint library stage 02 file dialog and material contracts")) {
        Write-Pass "Blueprint stage 02 console regression covers file-dialog routing, default export names, and material-list toggling."
    }
    else {
        Write-FailHealth "Blueprint stage 02 must keep the file-dialog/material console regression registered."
    }

    if ($auditText -and
        $auditText.Contains("Test-BlueprintPlacementStage02LibraryUiGovernance") -and
        $auditText.Contains("0.947-F5蓝图库文件选择UI")) {
        Write-Pass "Blueprint scoped health audit includes the stage 02 library UI governance check."
    }
    else {
        Write-FailHealth "Blueprint scoped health audit must include Test-BlueprintPlacementStage02LibraryUiGovernance and the 0.947 record anchor."
    }

    if ($plan02Text -and
        $plan02Text.Contains("状态：已完成") -and
        $plan02Text.Contains('RuntimeVersion：`0.947-blueprint-library-file-dialog-ui`') -and
        $plan02Text.Contains("OpenFileDialog") -and
        $plan02Text.Contains("SaveFileDialog") -and
        $plan02Text.Contains("BlueprintLibraryStage02FileDialogAndMaterialContracts") -and
        $plan02Text.Contains("Test-BlueprintPlacementStage02LibraryUiGovernance") -and
        $plan02Text.Contains("本轮不生成测试包") -and
        $plan00Text -and
        $plan00Text.Contains("02-F5蓝图库与文件选择UI.md") -and
        ($plan00Text.Contains("0.950-blueprint-visual-material-contrast") -or
            $plan00Text.Contains("0.951-blueprint-clear-placed-region-trim") -or
            $plan00Text.Contains("0.952-blueprint-move-mirror-governance") -or
            $plan00Text.Contains("0.953-blueprint-placement-regression-audit") -or
            $plan00Text.Contains("0.954-blueprint-placement-closeout")) -and
        $plan00Text.Contains("03-手持快捷栏命令矩阵.md") -and
        $currentPlanIndexText -and
        $currentPlanIndexText.Contains("蓝图放置与实例治理修复") -and
        ($currentPlanIndexText.Contains("0.950-blueprint-visual-material-contrast") -or
            $currentPlanIndexText.Contains("0.951-blueprint-clear-placed-region-trim") -or
            $currentPlanIndexText.Contains("0.952-blueprint-move-mirror-governance") -or
            $currentPlanIndexText.Contains("0.953-blueprint-placement-regression-audit") -or
            $currentPlanIndexText.Contains("0.954-blueprint-placement-closeout"))) {
        Write-Pass "Blueprint placement plan files keep stage 02 completion evidence while current status has advanced beyond 0.947."
    }
    else {
        Write-FailHealth "Blueprint placement plan files must keep stage 02 completion evidence while allowing the current plan index to advance beyond 0.947."
    }

    if ($functionDocText -and
        $functionDocText.Contains("0.947-blueprint-library-file-dialog-ui") -and
        $functionDocText.Contains("Windows 文件选择窗口") -and
        $functionDocText.Contains("材料列表") -and
        $updateIndexText -and
        $updateIndexText.Contains("0.947-F5蓝图库文件选择UI") -and
        $updateRecordText -and
        $updateRecordText.Contains('RuntimeVersion：`0.947-blueprint-library-file-dialog-ui`') -and
        $updateRecordText.Contains("BlueprintLibraryStage02FileDialogAndMaterialContracts") -and
        $updateRecordText.Contains("Test-BlueprintPlacementStage02LibraryUiGovernance") -and
        $docHistoryIndexText -and
        $docHistoryIndexText.Contains("蓝图F5蓝图库文件选择UI") -and
        $docHistoryRecordText -and
        $docHistoryRecordText.Contains("0.947-blueprint-library-file-dialog-ui") -and
        $docHistoryRecordText.Contains("本轮不生成测试包")) {
        Write-Pass "Blueprint stage 02 feature doc, update record, and document history are synchronized."
    }
    else {
        Write-FailHealth "Blueprint stage 02 must synchronize feature doc, update index/record, and document history."
    }
}

function Test-BlueprintPlacementStage03HandheldCommandMatrixGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $statePath = Join-Path $RepoRoot "src\JueMingZ\Automation\Blueprint\BlueprintHandheldActionBarState.cs"
    $overlayPath = Join-Path $RepoRoot "src\JueMingZ\UI\BlueprintHandheldActionBarOverlay.cs"
    $actionPath = Join-Path $RepoRoot "src\JueMingZ\Input\LegacyUiActionService.Blueprint.cs"
    $capturePath = Join-Path $RepoRoot "src\JueMingZ\Automation\Blueprint\BlueprintCaptureService.cs"
    $handheldTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintHandheldActionBarTests.cs"
    $captureTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintCaptureTests.cs"
    $programPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.cs"
    $csprojPath = Join-Path $RepoRoot "src\JueMingZ\JueMingZ.csproj"
    $runtimePath = Join-Path $RepoRoot "src\JueMingZ\Runtime\JueMingZRuntime.cs"
    $functionDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $diagnosticsDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "AI诊断日志说明.md")
    $plan00Path = Join-BlueprintPlacementPlanPath -RepoRoot $RepoRoot -Leaf "00-基准.md"
    $plan03Path = Join-BlueprintPlacementPlanPath -RepoRoot $RepoRoot -Leaf "03-手持快捷栏命令矩阵.md"
    $currentPlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "索引.md")
    $updateIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "索引.md")
    $updateDirectory = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录")
    $docHistoryIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "索引.md")
    $docHistoryDirectory = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史")

    $updateRecordPath = ""
    if (Test-Path -LiteralPath $updateDirectory) {
        $record = Get-ChildItem -LiteralPath $updateDirectory -Filter "0.948-手持快捷栏命令矩阵-*.md" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($record) {
            $updateRecordPath = $record.FullName
        }
    }

    $docHistoryRecordPath = ""
    if (Test-Path -LiteralPath $docHistoryDirectory) {
        $record = Get-ChildItem -LiteralPath $docHistoryDirectory -Filter "蓝图手持快捷栏命令矩阵-*.md" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($record) {
            $docHistoryRecordPath = $record.FullName
        }
    }

    $stateText = Read-TextIfExists -Path $statePath
    $overlayText = Read-TextIfExists -Path $overlayPath
    $actionText = Read-TextIfExists -Path $actionPath
    $captureText = Read-TextIfExists -Path $capturePath
    $handheldTestText = Read-TextIfExists -Path $handheldTestPath
    $captureTestText = Read-TextIfExists -Path $captureTestPath
    $programText = Read-TextIfExists -Path $programPath
    $csprojText = Read-TextIfExists -Path $csprojPath
    $runtimeText = Read-TextIfExists -Path $runtimePath
    $functionDocText = Read-TextIfExists -Path $functionDocPath
    $diagnosticsDocText = Read-TextIfExists -Path $diagnosticsDocPath
    $plan00Text = Read-TextIfExists -Path $plan00Path
    $plan03Text = Read-TextIfExists -Path $plan03Path
    $currentPlanIndexText = Read-TextIfExists -Path $currentPlanIndexPath
    $updateIndexText = Read-TextIfExists -Path $updateIndexPath
    $updateRecordText = if ([string]::IsNullOrWhiteSpace($updateRecordPath)) { $null } else { Read-TextIfExists -Path $updateRecordPath }
    $docHistoryIndexText = Read-TextIfExists -Path $docHistoryIndexPath
    $docHistoryRecordText = if ([string]::IsNullOrWhiteSpace($docHistoryRecordPath)) { $null } else { Read-TextIfExists -Path $docHistoryRecordPath }

    if (Test-BlueprintPlacementVersionMetadata -RuntimeText $runtimeText -CsprojText $csprojText -AllowedRuntimeVersions @(
            "0.950-blueprint-visual-material-contrast",
            "0.951-blueprint-clear-placed-region-trim",
            "0.952-blueprint-move-mirror-governance",
            "0.953-blueprint-placement-regression-audit",
            "0.954-blueprint-placement-closeout",
            "0.955-blueprint-feedback-autoplace-plan",
            "0.956-blueprint-feedback-fact-freeze",
            "0.957-blueprint-f5-library-submenus",
            "0.958-blueprint-placed-list-layout")) {
        Write-Pass "Blueprint stage 03 evidence is preserved while current version metadata has advanced to a later blueprint placement stage."
    }
    else {
        Write-FailHealth "Blueprint stage 03 evidence must remain documented while current RuntimeVersion and project metadata advance to 0.950-blueprint-visual-material-contrast."
    }

    if ($stateText -and
        $stateText.Contains("ButtonIdOpenPlacedList") -and
        $stateText.Contains("ButtonIdClearPlaced") -and
        $stateText.Contains("ButtonIdRegionModify") -and
        $stateText.Contains("ButtonIdMirror") -and
        $stateText.Contains("ResultCodeEntryWiredDeferred") -and
        $stateText.Contains("已放置蓝图列表") -and
        $stateText.Contains("清空放置") -and
        $stateText.Contains("区域修改") -and
        $stateText.Contains("镜像") -and
        $stateText.Contains("清除选区") -and
        $stateText.Contains("清除所有选区") -and
        $stateText.Contains("RecordDeferredBusinessClick") -and
        $captureText -and
        $captureText.Contains("选区内啥也没有喔") -and
        $overlayText -and
        $overlayText.Contains("open-placed-list-real") -and
        $overlayText.Contains("stage03-deferred-placed-commands")) {
        Write-Pass "Blueprint stage 03 handheld state exposes placed-list, clear/move/region/mirror, create-state clear text, pure-air message, and visual contract tokens."
    }
    else {
        Write-FailHealth "Blueprint stage 03 handheld state must expose the placed-list/deferred command matrix, create-state clear text, pure-air message, and visual contract tokens."
    }

    if ($actionText -and
        $actionText.Contains("BlueprintPlacedInstanceUiState.OpenManagement") -and
        $actionText.Contains("BlueprintEntryCommands.OpenPlacedInstances") -and
        $actionText.Contains("RevealBlueprintPlacedMenuIfOpened") -and
        (($actionText.Contains("IsBlueprintHandheldDeferredBusinessButton") -and
                $actionText.Contains("RecordDeferredBusinessClick") -and
                $actionText.Contains("ResultCodeEntryWiredDeferred") -and
                $actionText.Contains("deferredBusiness")) -or
            ($actionText.Contains("BlueprintPlacedInstanceTransformState.BeginMove") -and
                $actionText.Contains("BlueprintPlacedInstanceTransformState.BeginMirror") -and
                $actionText.Contains("transformInputActive")))) {
        Write-Pass "Blueprint stage 03 handheld action handler opens the placed-list UI; later stages may replace deferred move/mirror with real handlers."
    }
    else {
        Write-FailHealth "Blueprint stage 03 handheld action handler must open placed-list UI and record deferred clear/move/region/mirror commands with entryWiredDeferred."
    }

    if ($handheldTestText -and
        $handheldTestText.Contains("BlueprintHandheldActionBarRealCommandsAndDeferredPlacedCommands") -and
        $handheldTestText.Contains("ButtonIdOpenPlacedList") -and
        $handheldTestText.Contains("ButtonIdClearPlaced") -and
        $handheldTestText.Contains("ButtonIdRegionModify") -and
        $handheldTestText.Contains("ButtonIdMirror") -and
        $handheldTestText.Contains("ResultCodeEntryWiredDeferred") -and
        $captureTestText -and
        $captureTestText.Contains("BlueprintCaptureRejectsPureAirSelectionExplicitly") -and
        $captureTestText.Contains("emptyContent") -and
        $programText -and
        $programText.Contains("blueprint capture rejects pure air selection explicitly") -and
        $programText.Contains("blueprint handheld action bar real commands and deferred placed commands")) {
        Write-Pass "Blueprint stage 03 console tests cover placed-list opening, deferred commands, new clear text, and pure-air messaging."
    }
    else {
        Write-FailHealth "Blueprint stage 03 must keep registered console tests for placed-list opening, deferred commands, new clear text, and pure-air messaging."
    }

    if ($functionDocText -and
        $functionDocText.Contains("0.948-blueprint-handheld-command-matrix") -and
        $functionDocText.Contains("已放置蓝图列表") -and
        $functionDocText.Contains("entryWiredDeferred") -and
        $functionDocText.Contains("选区内啥也没有喔") -and
        $functionDocText.Contains("open-placed-list-real") -and
        $functionDocText.Contains("stage03-deferred-placed-commands") -and
        $diagnosticsDocText -and
        $diagnosticsDocText.Contains("entryWiredDeferred") -and
        $diagnosticsDocText.Contains("stage03-deferred-placed-commands") -and
        $diagnosticsDocText.Contains("选区内啥也没有喔")) {
        Write-Pass "Blueprint stage 03 function and diagnostics docs describe placed-list, deferred commands, pure-air messaging, and visual contract tokens."
    }
    else {
        Write-FailHealth "Blueprint stage 03 function and diagnostics docs must describe placed-list, deferred commands, pure-air messaging, and visual contract tokens."
    }

    if ($plan03Text -and
        $plan03Text.Contains("状态：已完成") -and
        $plan03Text.Contains('RuntimeVersion：`0.948-blueprint-handheld-command-matrix`') -and
        $plan03Text.Contains('未在本会话实现或验证 `04`') -and
        $plan03Text.Contains("不生成测试包") -and
        $plan03Text.Contains("Test-BlueprintPlacementStage03HandheldCommandMatrixGovernance") -and
        $plan00Text -and
        ($plan00Text.Contains("0.950-blueprint-visual-material-contrast") -or
            $plan00Text.Contains("0.951-blueprint-clear-placed-region-trim") -or
            $plan00Text.Contains("0.952-blueprint-move-mirror-governance") -or
            $plan00Text.Contains("0.953-blueprint-placement-regression-audit") -or
            $plan00Text.Contains("0.954-blueprint-placement-closeout")) -and
        ($plan00Text.Contains('下一入口为 `06-清空放置与区域修改.md`') -or
            $plan00Text.Contains('下一入口为 `07-移动与镜像治理.md`') -or
            $plan00Text.Contains('下一入口为 `08-回归诊断与审计防线.md`') -or
            $plan00Text.Contains('下一入口为 `09-验证打包与归档收口.md`')) -and
        $currentPlanIndexText -and
        ($currentPlanIndexText.Contains("0.950-blueprint-visual-material-contrast") -or
            $currentPlanIndexText.Contains("0.951-blueprint-clear-placed-region-trim") -or
            $currentPlanIndexText.Contains("0.952-blueprint-move-mirror-governance") -or
            $currentPlanIndexText.Contains("0.953-blueprint-placement-regression-audit") -or
            $currentPlanIndexText.Contains("0.954-blueprint-placement-closeout")) -and
        ($currentPlanIndexText.Contains('下一入口为 `06-清空放置与区域修改.md`') -or
            $currentPlanIndexText.Contains('下一入口为 `07-移动与镜像治理.md`') -or
            $currentPlanIndexText.Contains('下一入口为 `08-回归诊断与审计防线.md`') -or
            $currentPlanIndexText.Contains('下一入口为 `09-验证打包与归档收口.md`'))) {
        Write-Pass "Blueprint placement plan files preserve stage 03 completion while current status has advanced to a later blueprint placement stage."
    }
    else {
        Write-FailHealth "Blueprint placement plan files must record stage 03 completion, 0.948 version, no-package scope, and main-project locked next-stage boundary."
    }

    if ($updateIndexText -and
        $updateIndexText.Contains("0.948-手持快捷栏命令矩阵") -and
        $updateRecordText -and
        $updateRecordText.Contains('RuntimeVersion：`0.948-blueprint-handheld-command-matrix`') -and
        $updateRecordText.Contains("Test-BlueprintPlacementStage03HandheldCommandMatrixGovernance") -and
        $updateRecordText.Contains("不生成测试包") -and
        $docHistoryIndexText -and
        $docHistoryIndexText.Contains("蓝图手持快捷栏命令矩阵") -and
        $docHistoryRecordText -and
        $docHistoryRecordText.Contains("0.948-blueprint-handheld-command-matrix") -and
        $docHistoryRecordText.Contains("不生成测试包")) {
        Write-Pass "Blueprint stage 03 update record and document-change history are synchronized."
    }
    else {
        Write-FailHealth "Blueprint stage 03 must synchronize update index/record and document-change history for 0.948."
    }
}

function Test-BlueprintPlacementStage04InstancePersistenceGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $projectionPath = Join-Path $RepoRoot "src\JueMingZ\Automation\Blueprint\BlueprintProjectionService.cs"
    $placedStatePath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\BlueprintPlacedInstanceUiState.cs"
    $instanceStorePath = Join-Path $RepoRoot "src\JueMingZ\Automation\Blueprint\BlueprintWorldInstanceStore.cs"
    $placementTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintPlacementPreviewTests.cs"
    $placedTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintPlacedInstanceTests.cs"
    $projectionTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintProjectionTests.cs"
    $programPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.cs"
    $csprojPath = Join-Path $RepoRoot "src\JueMingZ\JueMingZ.csproj"
    $runtimePath = Join-Path $RepoRoot "src\JueMingZ\Runtime\JueMingZRuntime.cs"
    $auditPath = Join-Path $RepoRoot "scripts\audit-project-health.ps1"
    $functionDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $diagnosticsDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "AI诊断日志说明.md")
    $plan00Path = Join-BlueprintPlacementPlanPath -RepoRoot $RepoRoot -Leaf "00-基准.md"
    $plan04Path = Join-BlueprintPlacementPlanPath -RepoRoot $RepoRoot -Leaf "04-放置实例持久化与重叠解析.md"
    $currentPlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "索引.md")
    $updateIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "索引.md")
    $updateDirectory = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录")
    $docHistoryIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "索引.md")
    $docHistoryDirectory = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史")

    $updateRecordPath = ""
    if (Test-Path -LiteralPath $updateDirectory) {
        $record = Get-ChildItem -LiteralPath $updateDirectory -Filter "0.949-放置实例持久化与重叠解析-*.md" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($record) {
            $updateRecordPath = $record.FullName
        }
    }

    $docHistoryRecordPath = ""
    if (Test-Path -LiteralPath $docHistoryDirectory) {
        $record = Get-ChildItem -LiteralPath $docHistoryDirectory -Filter "蓝图放置实例持久化重叠解析-*.md" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($record) {
            $docHistoryRecordPath = $record.FullName
        }
    }

    $projectionText = Read-TextIfExists -Path $projectionPath
    $placedStateText = Read-TextIfExists -Path $placedStatePath
    $instanceStoreText = Read-TextIfExists -Path $instanceStorePath
    $placementTestText = Read-TextIfExists -Path $placementTestPath
    $placedTestText = Read-TextIfExists -Path $placedTestPath
    $projectionTestText = Read-TextIfExists -Path $projectionTestPath
    $programText = Read-TextIfExists -Path $programPath
    $csprojText = Read-TextIfExists -Path $csprojPath
    $runtimeText = Read-TextIfExists -Path $runtimePath
    $auditText = Read-TextIfExists -Path $auditPath
    $functionDocText = Read-TextIfExists -Path $functionDocPath
    $diagnosticsDocText = Read-TextIfExists -Path $diagnosticsDocPath
    $plan00Text = Read-TextIfExists -Path $plan00Path
    $plan04Text = Read-TextIfExists -Path $plan04Path
    $currentPlanIndexText = Read-TextIfExists -Path $currentPlanIndexPath
    $updateIndexText = Read-TextIfExists -Path $updateIndexPath
    $updateRecordText = if ([string]::IsNullOrWhiteSpace($updateRecordPath)) { $null } else { Read-TextIfExists -Path $updateRecordPath }
    $docHistoryIndexText = Read-TextIfExists -Path $docHistoryIndexPath
    $docHistoryRecordText = if ([string]::IsNullOrWhiteSpace($docHistoryRecordPath)) { $null } else { Read-TextIfExists -Path $docHistoryRecordPath }

    if (Test-BlueprintPlacementVersionMetadata -RuntimeText $runtimeText -CsprojText $csprojText -AllowedRuntimeVersions @(
            "0.950-blueprint-visual-material-contrast",
            "0.951-blueprint-clear-placed-region-trim",
            "0.952-blueprint-move-mirror-governance",
            "0.953-blueprint-placement-regression-audit",
            "0.954-blueprint-placement-closeout",
            "0.955-blueprint-feedback-autoplace-plan",
            "0.956-blueprint-feedback-fact-freeze",
            "0.957-blueprint-f5-library-submenus",
            "0.958-blueprint-placed-list-layout")) {
        Write-Pass "Blueprint stage 04 evidence is preserved while current version metadata has advanced to a later blueprint placement stage."
    }
    else {
        Write-FailHealth "Blueprint stage 04 evidence must remain documented while current RuntimeVersion and project metadata advance to 0.950-blueprint-visual-material-contrast."
    }

    if ($projectionText -and
        $projectionText.Contains("RefreshAfterWorldInstancesChanged") -and
        $projectionText.Contains("Instance writes are the explicit mutation boundary") -and
        $projectionText.Contains("Overlap is resolved only in this transient") -and
        $placedStateText -and
        $placedStateText.Contains("RefreshProjectionAfterWorldInstancesChanged") -and
        $placedStateText.Contains("NotifyInstanceCreated") -and
        $placedStateText.Contains("OpenManagement") -and
        -not $placedStateText.Contains("ForceRefreshForMaterialWindow") -and
        $instanceStoreText -and
        $instanceStoreText.Contains("TemplateSnapshot = template.Clone()") -and
        $instanceStoreText.Contains("Placed instances own a snapshot copy")) {
        Write-Pass "Blueprint stage 04 keeps projection refresh at explicit instance mutation boundaries and preserves placed template snapshots."
    }
    else {
        Write-FailHealth "Blueprint stage 04 must refresh projection after instance mutations, avoid stage-05 material refresh, and keep placed snapshots isolated from templates."
    }

    if ($placementTestText -and
        $placementTestText.Contains("BlueprintPlacementConfirmRefreshesProjectionAndPlacedList") -and
        $placedTestText -and
        $placedTestText.Contains("BlueprintWorldInstancesPersistHiddenEraseLayerAndSnapshotsOnReload") -and
        $projectionTestText -and
        $projectionTestText.Contains("BlueprintProjectionStage04LaterInstanceCoversEarlierWithoutMutatingSnapshots") -and
        $projectionTestText.Contains("HasProjectedLayerAt") -and
        $programText -and
        $programText.Contains("blueprint placement confirm refreshes projection and placed list") -and
        $programText.Contains("blueprint world instances persist hidden erase layer and snapshots on reload") -and
        $programText.Contains("blueprint projection stage 04 later instance covers earlier without mutating snapshots")) {
        Write-Pass "Blueprint stage 04 console regressions cover placement confirm refresh, persisted instance fields, and transient overlap resolution."
    }
    else {
        Write-FailHealth "Blueprint stage 04 must keep placement/storage/projection regressions registered in the console test runner."
    }

    if ($auditText -and
        $auditText.Contains("Test-BlueprintPlacementStage04InstancePersistenceGovernance") -and
        $auditText.Contains("0.949-放置实例持久化与重叠解析")) {
        Write-Pass "Blueprint scoped health audit includes the stage 04 instance persistence governance check."
    }
    else {
        Write-FailHealth "Blueprint scoped health audit must include Test-BlueprintPlacementStage04InstancePersistenceGovernance and the 0.949 record anchor."
    }

    if ($functionDocText -and
        $functionDocText.Contains("0.949-blueprint-placement-instance-persistence") -and
        $functionDocText.Contains("RefreshAfterWorldInstancesChanged") -and
        $functionDocText.Contains("重叠") -and
        $functionDocText.Contains("05") -and
        $diagnosticsDocText -and
        $diagnosticsDocText.Contains("0.949-blueprint-placement-instance-persistence") -and
        $diagnosticsDocText.Contains("RefreshAfterWorldInstancesChanged") -and
        $diagnosticsDocText.Contains("draw / hit-test") -and
        $diagnosticsDocText.Contains("Test-BlueprintPlacementStage04InstancePersistenceGovernance")) {
        Write-Pass "Blueprint stage 04 function and diagnostics docs describe instance persistence, projection refresh, and the stage-05 boundary."
    }
    else {
        Write-FailHealth "Blueprint stage 04 function and diagnostics docs must describe 0.949 projection refresh, overlap semantics, and the stage-05 boundary."
    }

    if ($plan04Text -and
        $plan04Text.Contains("状态：已完成") -and
        $plan04Text.Contains('RuntimeVersion：`0.949-blueprint-placement-instance-persistence`') -and
        $plan04Text.Contains('未在本会话实现或验证 `05`') -and
        $plan04Text.Contains("不生成测试包") -and
        $plan04Text.Contains("Test-BlueprintPlacementStage04InstancePersistenceGovernance") -and
        $plan00Text -and
        ($plan00Text.Contains("0.950-blueprint-visual-material-contrast") -or
            $plan00Text.Contains("0.951-blueprint-clear-placed-region-trim") -or
            $plan00Text.Contains("0.952-blueprint-move-mirror-governance") -or
            $plan00Text.Contains("0.953-blueprint-placement-regression-audit") -or
            $plan00Text.Contains("0.954-blueprint-placement-closeout")) -and
        ($plan00Text.Contains('下一入口为 `06-清空放置与区域修改.md`') -or
            $plan00Text.Contains('下一入口为 `07-移动与镜像治理.md`') -or
            $plan00Text.Contains('下一入口为 `08-回归诊断与审计防线.md`') -or
            $plan00Text.Contains('下一入口为 `09-验证打包与归档收口.md`')) -and
        $currentPlanIndexText -and
        ($currentPlanIndexText.Contains("0.950-blueprint-visual-material-contrast") -or
            $currentPlanIndexText.Contains("0.951-blueprint-clear-placed-region-trim") -or
            $currentPlanIndexText.Contains("0.952-blueprint-move-mirror-governance") -or
            $currentPlanIndexText.Contains("0.953-blueprint-placement-regression-audit") -or
            $currentPlanIndexText.Contains("0.954-blueprint-placement-closeout")) -and
        ($currentPlanIndexText.Contains('下一入口为 `06-清空放置与区域修改.md`') -or
            $currentPlanIndexText.Contains('下一入口为 `07-移动与镜像治理.md`') -or
            $currentPlanIndexText.Contains('下一入口为 `08-回归诊断与审计防线.md`') -or
            $currentPlanIndexText.Contains('下一入口为 `09-验证打包与归档收口.md`'))) {
        Write-Pass "Blueprint placement plan files record stage 04 completion while current status has advanced to a later blueprint placement stage."
    }
    else {
        Write-FailHealth "Blueprint placement plan files must record stage 04 completion, 0.949 version, no-package scope, and next-stage boundary."
    }

    if ($updateIndexText -and
        $updateIndexText.Contains("0.949-放置实例持久化与重叠解析") -and
        $updateRecordText -and
        $updateRecordText.Contains('RuntimeVersion：`0.949-blueprint-placement-instance-persistence`') -and
        $updateRecordText.Contains("Test-BlueprintPlacementStage04InstancePersistenceGovernance") -and
        $updateRecordText.Contains("不生成测试包") -and
        $docHistoryIndexText -and
        $docHistoryIndexText.Contains("蓝图放置实例持久化重叠解析") -and
        $docHistoryRecordText -and
        $docHistoryRecordText.Contains("0.949-blueprint-placement-instance-persistence") -and
        $docHistoryRecordText.Contains("不生成测试包")) {
        Write-Pass "Blueprint stage 04 update record and document-change history are synchronized."
    }
    else {
        Write-FailHealth "Blueprint stage 04 must synchronize update index/record and document-change history for 0.949."
    }
}

function Test-BlueprintPlacementStage05VisualMaterialGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $overlayPath = Join-Path $RepoRoot "src\JueMingZ\UI\BlueprintProjectionOverlay.cs"
    $ghostPath = Join-Path $RepoRoot "src\JueMingZ\UI\BlueprintProjectionGhostRenderer.cs"
    $primitivePath = Join-Path $RepoRoot "src\JueMingZ\UI\UiPrimitiveRenderer.cs"
    $materialPath = Join-Path $RepoRoot "src\JueMingZ\Automation\Blueprint\BlueprintMaterialService.cs"
    $placedStatePath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\BlueprintPlacedInstanceUiState.cs"
    $placedUiPath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyMainWindow.Blueprint.Placed.cs"
    $legacyBlueprintPath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyMainWindow.Blueprint.cs"
    $projectionTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintProjectionTests.cs"
    $materialTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintMaterialTests.cs"
    $programPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.cs"
    $csprojPath = Join-Path $RepoRoot "src\JueMingZ\JueMingZ.csproj"
    $runtimePath = Join-Path $RepoRoot "src\JueMingZ\Runtime\JueMingZRuntime.cs"
    $auditPath = Join-Path $RepoRoot "scripts\audit-project-health.ps1"
    $functionDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $diagnosticsDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "AI诊断日志说明.md")
    $plan00Path = Join-BlueprintPlacementPlanPath -RepoRoot $RepoRoot -Leaf "00-基准.md"
    $plan05Path = Join-BlueprintPlacementPlanPath -RepoRoot $RepoRoot -Leaf "05-世界虚影与材料对照.md"
    $currentPlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "索引.md")
    $updateIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "索引.md")
    $updateDirectory = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录")
    $docHistoryIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "索引.md")
    $docHistoryDirectory = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史")

    $updateRecordPath = ""
    if (Test-Path -LiteralPath $updateDirectory) {
        $record = Get-ChildItem -LiteralPath $updateDirectory -Filter "0.950-世界虚影与材料对照-*.md" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($record) {
            $updateRecordPath = $record.FullName
        }
    }

    $docHistoryRecordPath = ""
    if (Test-Path -LiteralPath $docHistoryDirectory) {
        $record = Get-ChildItem -LiteralPath $docHistoryDirectory -Filter "蓝图世界虚影与材料对照-*.md" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($record) {
            $docHistoryRecordPath = $record.FullName
        }
    }

    $overlayText = Read-TextIfExists -Path $overlayPath
    $ghostText = Read-TextIfExists -Path $ghostPath
    $primitiveText = Read-TextIfExists -Path $primitivePath
    $materialText = Read-TextIfExists -Path $materialPath
    $placedStateText = Read-TextIfExists -Path $placedStatePath
    $placedUiText = Read-TextIfExists -Path $placedUiPath
    $legacyBlueprintText = Read-TextIfExists -Path $legacyBlueprintPath
    $projectionTestText = Read-TextIfExists -Path $projectionTestPath
    $materialTestText = Read-TextIfExists -Path $materialTestPath
    $programText = Read-TextIfExists -Path $programPath
    $csprojText = Read-TextIfExists -Path $csprojPath
    $runtimeText = Read-TextIfExists -Path $runtimePath
    $auditText = Read-TextIfExists -Path $auditPath
    $functionDocText = Read-TextIfExists -Path $functionDocPath
    $diagnosticsDocText = Read-TextIfExists -Path $diagnosticsDocPath
    $plan00Text = Read-TextIfExists -Path $plan00Path
    $plan05Text = Read-TextIfExists -Path $plan05Path
    $currentPlanIndexText = Read-TextIfExists -Path $currentPlanIndexPath
    $updateIndexText = Read-TextIfExists -Path $updateIndexPath
    $updateRecordText = if ([string]::IsNullOrWhiteSpace($updateRecordPath)) { $null } else { Read-TextIfExists -Path $updateRecordPath }
    $docHistoryIndexText = Read-TextIfExists -Path $docHistoryIndexPath
    $docHistoryRecordText = if ([string]::IsNullOrWhiteSpace($docHistoryRecordPath)) { $null } else { Read-TextIfExists -Path $docHistoryRecordPath }

    if (Test-BlueprintPlacementVersionMetadata -RuntimeText $runtimeText -CsprojText $csprojText -AllowedRuntimeVersions @(
            "0.950-blueprint-visual-material-contrast",
            "0.951-blueprint-clear-placed-region-trim",
            "0.952-blueprint-move-mirror-governance",
            "0.953-blueprint-placement-regression-audit",
            "0.954-blueprint-placement-closeout",
            "0.955-blueprint-feedback-autoplace-plan",
            "0.956-blueprint-feedback-fact-freeze",
            "0.957-blueprint-f5-library-submenus",
            "0.958-blueprint-placed-list-layout")) {
        Write-Pass "Blueprint stage 05 version metadata is synchronized or has advanced to stage 06."
    }
    else {
        Write-FailHealth "Blueprint stage 05 must synchronize RuntimeVersion and project version metadata to 0.950-blueprint-visual-material-contrast."
    }

    if ($overlayText -and
        $overlayText.Contains("appearance-ghost") -and
        $overlayText.Contains("yellow-missing") -and
        $overlayText.Contains("red-conflict") -and
        $overlayText.Contains("fulfilled-no-mask") -and
        $overlayText.Contains("GetCachedSnapshotForDraw") -and
        $ghostText -and
        $ghostText.Contains("TextureAssets.Tile") -and
        $ghostText.Contains("TextureAssets.Wall") -and
        $ghostText.Contains("TryGetTileAndRequestIfNotReady") -and
        $ghostText.Contains("TryGetWallAndRequestIfNotReady") -and
        $ghostText.Contains("BlueprintCaptureWireFlags") -and
        $primitiveText -and
        $primitiveText.Contains("public static bool DrawTextureSourceRectClipped")) {
        Write-Pass "Blueprint stage 05 world projection draws tile, wall, wire, and actuator appearance ghosts from cached projection data."
    }
    else {
        Write-FailHealth "Blueprint stage 05 projection overlay must use cached projection data, appearance ghost contracts, Terraria tile/wall textures, paint-system lookups, and source-rect drawing."
    }

    if ($materialText -and
        $materialText.Contains("ForceRefreshForPlacedInstanceList") -and
        $materialText.Contains("GetCachedSnapshotForDraw") -and
        $placedStateText -and
        $placedStateText.Contains("ForceRefreshForPlacedInstanceList") -and
        $placedStateText.Contains("draw paths keep reading the cached snapshots") -and
        $placedUiText -and
        $placedUiText.Contains("BuildBlueprintPlacedMaterialLines") -and
        ($placedUiText.Contains("取消显示") -or
            ($placedUiText.Contains("取消放置") -and
                $placedUiText.Contains("点击隐藏此蓝图") -and
                $placedUiText.Contains("点击显示此蓝图"))) -and
        $placedUiText.Contains("虚空袋") -and
        $placedUiText.Contains("GetCachedSnapshotForDraw") -and
        $legacyBlueprintText -and
        $legacyBlueprintText.Contains("placed-list-comparison") -and
        $legacyBlueprintText.Contains("materials-cache-only")) {
        Write-Pass "Blueprint stage 05 placed list refreshes material comparison at explicit instance boundaries and draws cached backpack/void-bag counts."
    }
    else {
        Write-FailHealth "Blueprint stage 05 placed list must expose cancel-display or stage-03 hide/show/cancel-place text, material comparison rows, explicit placed-list refresh, and cache-only draw behavior."
    }

    if ($projectionTestText -and
        $projectionTestText.Contains("stage-05 yellow ghost semantics") -and
        $projectionTestText.Contains("ShouldDrawProjectionLayerForTesting") -and
        $materialTestText -and
        $materialTestText.Contains("BlueprintPlacedListRefreshesMaterialComparisonWithoutDrawScan") -and
        $materialTestText.Contains("ReadCount != 1") -and
        $programText -and
        $programText.Contains("blueprint placed list refreshes material comparison without draw scan")) {
        Write-Pass "Blueprint stage 05 console regressions cover visual semantics and placed-list material cache behavior."
    }
    else {
        Write-FailHealth "Blueprint stage 05 projection/material regressions must be present and registered in the console test runner."
    }

    if ($auditText -and
        $auditText.Contains("Test-BlueprintPlacementStage05VisualMaterialGovernance") -and
        $auditText.Contains("0.950-世界虚影与材料对照")) {
        Write-Pass "Blueprint scoped health audit includes the stage 05 visual/material governance check."
    }
    else {
        Write-FailHealth "Blueprint scoped health audit must include Test-BlueprintPlacementStage05VisualMaterialGovernance and the 0.950 record anchor."
    }

    if ($functionDocText -and
        $functionDocText.Contains("0.950-blueprint-visual-material-contrast") -and
        $functionDocText.Contains("appearance-ghost") -and
        $functionDocText.Contains("取消显示") -and
        $functionDocText.Contains("虚空袋") -and
        $diagnosticsDocText -and
        $diagnosticsDocText.Contains("0.950-blueprint-visual-material-contrast") -and
        $diagnosticsDocText.Contains("ForceRefreshForPlacedInstanceList") -and
        $diagnosticsDocText.Contains("Test-BlueprintPlacementStage05VisualMaterialGovernance")) {
        Write-Pass "Blueprint stage 05 function and diagnostics docs describe world ghost semantics and placed-list material refresh boundaries."
    }
    else {
        Write-FailHealth "Blueprint stage 05 function and diagnostics docs must describe 0.950 ghost/material semantics and the scoped health audit."
    }

    if ($plan05Text -and
        $plan05Text.Contains("状态：已完成") -and
        $plan05Text.Contains('RuntimeVersion：`0.950-blueprint-visual-material-contrast`') -and
        $plan05Text.Contains('未在本会话实现或验证 `06`') -and
        $plan05Text.Contains("不生成测试包") -and
        $plan05Text.Contains("Test-BlueprintPlacementStage05VisualMaterialGovernance") -and
        $plan00Text -and
        ($plan00Text.Contains("0.950-blueprint-visual-material-contrast") -or
            $plan00Text.Contains("0.951-blueprint-clear-placed-region-trim") -or
            $plan00Text.Contains("0.952-blueprint-move-mirror-governance") -or
            $plan00Text.Contains("0.953-blueprint-placement-regression-audit") -or
            $plan00Text.Contains("0.954-blueprint-placement-closeout")) -and
        ($plan00Text.Contains('下一入口为 `06-清空放置与区域修改.md`') -or
            $plan00Text.Contains('下一入口为 `07-移动与镜像治理.md`') -or
            $plan00Text.Contains('下一入口为 `08-回归诊断与审计防线.md`') -or
            $plan00Text.Contains('下一入口为 `09-验证打包与归档收口.md`')) -and
        $currentPlanIndexText -and
        ($currentPlanIndexText.Contains("0.950-blueprint-visual-material-contrast") -or
            $currentPlanIndexText.Contains("0.951-blueprint-clear-placed-region-trim") -or
            $currentPlanIndexText.Contains("0.952-blueprint-move-mirror-governance") -or
            $currentPlanIndexText.Contains("0.953-blueprint-placement-regression-audit") -or
            $currentPlanIndexText.Contains("0.954-blueprint-placement-closeout")) -and
        ($currentPlanIndexText.Contains('下一入口为 `06-清空放置与区域修改.md`') -or
            $currentPlanIndexText.Contains('下一入口为 `07-移动与镜像治理.md`') -or
            $currentPlanIndexText.Contains('下一入口为 `08-回归诊断与审计防线.md`') -or
            $currentPlanIndexText.Contains('下一入口为 `09-验证打包与归档收口.md`'))) {
        Write-Pass "Blueprint placement plan files record stage 05 completion, no-package scope, and next-stage boundary."
    }
    else {
        Write-FailHealth "Blueprint placement plan files must record stage 05 completion, 0.950 version, no-package scope, and main-project locked next-stage boundary."
    }

    if ($updateIndexText -and
        $updateIndexText.Contains("0.950-世界虚影与材料对照") -and
        $updateRecordText -and
        $updateRecordText.Contains('RuntimeVersion：`0.950-blueprint-visual-material-contrast`') -and
        $updateRecordText.Contains("Test-BlueprintPlacementStage05VisualMaterialGovernance") -and
        $updateRecordText.Contains("不生成测试包") -and
        $docHistoryIndexText -and
        $docHistoryIndexText.Contains("蓝图世界虚影与材料对照") -and
        $docHistoryRecordText -and
        $docHistoryRecordText.Contains("0.950-blueprint-visual-material-contrast") -and
        $docHistoryRecordText.Contains("不生成测试包")) {
        Write-Pass "Blueprint stage 05 update record and document-change history are synchronized."
    }
    else {
        Write-FailHealth "Blueprint stage 05 must synchronize update index/record and document-change history for 0.950."
    }
}

function Test-BlueprintPlacementStage06ClearRegionGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $placedStatePath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\BlueprintPlacedInstanceUiState.cs"
    $eraseStatePath = Join-Path $RepoRoot "src\JueMingZ\Automation\Blueprint\BlueprintEraseRegionState.cs"
    $actionPath = Join-Path $RepoRoot "src\JueMingZ\Input\LegacyUiActionService.Blueprint.cs"
    $handheldStatePath = Join-Path $RepoRoot "src\JueMingZ\Automation\Blueprint\BlueprintHandheldActionBarState.cs"
    $placedTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintPlacedInstanceTests.cs"
    $handheldTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintHandheldActionBarTests.cs"
    $eraseTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintEraseTests.cs"
    $programPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.cs"
    $csprojPath = Join-Path $RepoRoot "src\JueMingZ\JueMingZ.csproj"
    $runtimePath = Join-Path $RepoRoot "src\JueMingZ\Runtime\JueMingZRuntime.cs"
    $auditPath = Join-Path $RepoRoot "scripts\audit-project-health.ps1"
    $functionDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $diagnosticsDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "AI诊断日志说明.md")
    $plan00Path = Join-BlueprintPlacementPlanPath -RepoRoot $RepoRoot -Leaf "00-基准.md"
    $plan06Path = Join-BlueprintPlacementPlanPath -RepoRoot $RepoRoot -Leaf "06-清空放置与区域修改.md"
    $currentPlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "索引.md")
    $updateIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "索引.md")
    $updateDirectory = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录")
    $docHistoryIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "索引.md")
    $docHistoryDirectory = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史")

    $updateRecordPath = ""
    if (Test-Path -LiteralPath $updateDirectory) {
        $record = Get-ChildItem -LiteralPath $updateDirectory -Filter "0.951-清空放置与区域修改-*.md" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($record) {
            $updateRecordPath = $record.FullName
        }
    }

    $docHistoryRecordPath = ""
    if (Test-Path -LiteralPath $docHistoryDirectory) {
        $record = Get-ChildItem -LiteralPath $docHistoryDirectory -Filter "蓝图清空放置与区域修改-*.md" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($record) {
            $docHistoryRecordPath = $record.FullName
        }
    }

    $placedStateText = Read-TextIfExists -Path $placedStatePath
    $eraseStateText = Read-TextIfExists -Path $eraseStatePath
    $actionText = Read-TextIfExists -Path $actionPath
    $handheldStateText = Read-TextIfExists -Path $handheldStatePath
    $placedTestText = Read-TextIfExists -Path $placedTestPath
    $handheldTestText = Read-TextIfExists -Path $handheldTestPath
    $eraseTestText = Read-TextIfExists -Path $eraseTestPath
    $programText = Read-TextIfExists -Path $programPath
    $csprojText = Read-TextIfExists -Path $csprojPath
    $runtimeText = Read-TextIfExists -Path $runtimePath
    $auditText = Read-TextIfExists -Path $auditPath
    $functionDocText = Read-TextIfExists -Path $functionDocPath
    $diagnosticsDocText = Read-TextIfExists -Path $diagnosticsDocPath
    $plan00Text = Read-TextIfExists -Path $plan00Path
    $plan06Text = Read-TextIfExists -Path $plan06Path
    $currentPlanIndexText = Read-TextIfExists -Path $currentPlanIndexPath
    $updateIndexText = Read-TextIfExists -Path $updateIndexPath
    $updateRecordText = if ([string]::IsNullOrWhiteSpace($updateRecordPath)) { $null } else { Read-TextIfExists -Path $updateRecordPath }
    $docHistoryIndexText = Read-TextIfExists -Path $docHistoryIndexPath
    $docHistoryRecordText = if ([string]::IsNullOrWhiteSpace($docHistoryRecordPath)) { $null } else { Read-TextIfExists -Path $docHistoryRecordPath }

    if (Test-BlueprintPlacementVersionMetadata -RuntimeText $runtimeText -CsprojText $csprojText -AllowedRuntimeVersions @(
            "0.951-blueprint-clear-placed-region-trim",
            "0.952-blueprint-move-mirror-governance",
            "0.953-blueprint-placement-regression-audit",
            "0.954-blueprint-placement-closeout",
            "0.955-blueprint-feedback-autoplace-plan",
            "0.956-blueprint-feedback-fact-freeze",
            "0.957-blueprint-f5-library-submenus",
            "0.958-blueprint-placed-list-layout")) {
        Write-Pass "Blueprint stage 06 version metadata is synchronized or has advanced to stage 07."
    }
    else {
        Write-FailHealth "Blueprint stage 06 must synchronize RuntimeVersion and project version metadata to 0.951-blueprint-clear-placed-region-trim."
    }

    if ($placedStateText -and
        $placedStateText.Contains("ClearAllCurrentWorld") -and
        $placedStateText.Contains("模板和世界内容未修改") -and
        $placedStateText.Contains("RefreshProjectionAfterWorldInstancesChanged") -and
        $eraseStateText -and
        $eraseStateText.Contains("noVisibleInstances") -and
        $eraseStateText.Contains("MaxEraseCells") -and
        $actionText -and
        $actionText.Contains("ButtonIdClearPlaced") -and
        $actionText.Contains("ClearAllCurrentWorld") -and
        $actionText.Contains("ButtonIdRegionModify") -and
        ($actionText.Contains("BeginErase(string.Empty)") -or
            ($actionText.Contains("StartOrCancelBlueprintRegionModify") -and
                $actionText.Contains("BlueprintEntryCommands.StartRegionModify") -and
                $actionText.Contains("eraseInputActive"))) -and
        $actionText.Contains('\"uiOnly\":false') -and
        $handheldStateText -and
        $handheldStateText.Contains("ButtonIdClearPlaced") -and
        $handheldStateText.Contains("ButtonIdRegionModify")) {
        Write-Pass "Blueprint stage 06 handheld clear and region-modify commands route to real placed-instance governance."
    }
    else {
        Write-FailHealth "Blueprint stage 06 must route handheld clear-placed and region-modify to real instance clearing / erase-mask services, not deferred placeholders."
    }

    if ($placedTestText -and
        $placedTestText.Contains("BlueprintPlacedInstanceClearAllCurrentWorldKeepsTemplatesAndRefreshesCaches") -and
        $placedTestText.Contains("Clear-all must affect only the current world instance file") -and
        $handheldTestText -and
        $handheldTestText.Contains("handheld clear-placed command result") -and
        $handheldTestText.Contains("handheld region-modify starts erase mode") -and
        $eraseTestText -and
        $eraseTestText.Contains("BlueprintEraseSelectionPrefersSelectedAndTopLayerFallback") -and
        $programText -and
        $programText.Contains("blueprint placed instance clear all current world keeps templates and refreshes caches")) {
        Write-Pass "Blueprint stage 06 console regressions cover clear-all, handheld real routing, and erase region priority."
    }
    else {
        Write-FailHealth "Blueprint stage 06 must register clear-all / region-modify console regressions and keep erase priority tests wired."
    }

    if ($auditText -and
        $auditText.Contains("Test-BlueprintPlacementStage06ClearRegionGovernance") -and
        $auditText.Contains("0.951-清空放置与区域修改")) {
        Write-Pass "Blueprint scoped health audit includes the stage 06 clear/region governance check."
    }
    else {
        Write-FailHealth "Blueprint scoped health audit must include Test-BlueprintPlacementStage06ClearRegionGovernance and the 0.951 record anchor."
    }

    if ($functionDocText -and
        $functionDocText.Contains("0.951-blueprint-clear-placed-region-trim") -and
        $functionDocText.Contains("clearPlaced") -and
        $functionDocText.Contains("eraseStartedHoverTarget") -and
        $diagnosticsDocText -and
        $diagnosticsDocText.Contains("0.951-blueprint-clear-placed-region-trim") -and
        $diagnosticsDocText.Contains("ClearAllCurrentWorld") -and
        $diagnosticsDocText.Contains("Test-BlueprintPlacementStage06ClearRegionGovernance")) {
        Write-Pass "Blueprint stage 06 function and diagnostics docs describe clear/region command semantics."
    }
    else {
        Write-FailHealth "Blueprint stage 06 function and diagnostics docs must describe 0.951 clear/region semantics and the scoped health audit."
    }

    if ($plan06Text -and
        $plan06Text.Contains("状态：已完成") -and
        $plan06Text.Contains('RuntimeVersion：`0.951-blueprint-clear-placed-region-trim`') -and
        $plan06Text.Contains('未在本会话实现或验证 `07`') -and
        $plan06Text.Contains("Test-BlueprintPlacementStage06ClearRegionGovernance") -and
        $plan00Text -and
        ($plan00Text.Contains("0.951-blueprint-clear-placed-region-trim") -or
            $plan00Text.Contains("0.952-blueprint-move-mirror-governance") -or
            $plan00Text.Contains("0.953-blueprint-placement-regression-audit") -or
            $plan00Text.Contains("0.954-blueprint-placement-closeout")) -and
        ($plan00Text.Contains('下一入口为 `07-移动与镜像治理.md`') -or
            $plan00Text.Contains('下一入口为 `08-回归诊断与审计防线.md`') -or
            $plan00Text.Contains('下一入口为 `09-验证打包与归档收口.md`')) -and
        $currentPlanIndexText -and
        ($currentPlanIndexText.Contains("0.951-blueprint-clear-placed-region-trim") -or
            $currentPlanIndexText.Contains("0.952-blueprint-move-mirror-governance") -or
            $currentPlanIndexText.Contains("0.953-blueprint-placement-regression-audit") -or
            $currentPlanIndexText.Contains("0.954-blueprint-placement-closeout")) -and
        ($currentPlanIndexText.Contains('下一入口为 `07-移动与镜像治理.md`') -or
            $currentPlanIndexText.Contains('下一入口为 `08-回归诊断与审计防线.md`') -or
            $currentPlanIndexText.Contains('下一入口为 `09-验证打包与归档收口.md`'))) {
        Write-Pass "Blueprint placement plan files record stage 06 completion, no-package scope, and next-stage boundary."
    }
    else {
        Write-FailHealth "Blueprint placement plan files must record stage 06 completion, 0.951 version, no-package scope, and main-project locked next-stage boundary."
    }

    if ($updateIndexText -and
        $updateIndexText.Contains("0.951-清空放置与区域修改") -and
        $updateRecordText -and
        $updateRecordText.Contains('RuntimeVersion：`0.951-blueprint-clear-placed-region-trim`') -and
        $updateRecordText.Contains("Test-BlueprintPlacementStage06ClearRegionGovernance") -and
        $updateRecordText.Contains("不生成测试包") -and
        $docHistoryIndexText -and
        $docHistoryIndexText.Contains("蓝图清空放置与区域修改") -and
        $docHistoryRecordText -and
        $docHistoryRecordText.Contains("0.951-blueprint-clear-placed-region-trim") -and
        $docHistoryRecordText.Contains("不生成测试包")) {
        Write-Pass "Blueprint stage 06 update record and document-change history are synchronized."
    }
    else {
        Write-FailHealth "Blueprint stage 06 must synchronize update index/record and document-change history for 0.951."
    }
}

function Test-BlueprintPlacementStage07MoveMirrorGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $modelPath = Join-Path $RepoRoot "src\JueMingZ\Automation\Blueprint\BlueprintModels.cs"
    $transformStatePath = Join-Path $RepoRoot "src\JueMingZ\Automation\Blueprint\BlueprintPlacedInstanceTransformState.cs"
    $transformOverlayPath = Join-Path $RepoRoot "src\JueMingZ\UI\BlueprintPlacedInstanceTransformOverlay.cs"
    $actionPath = Join-Path $RepoRoot "src\JueMingZ\Input\LegacyUiActionService.Blueprint.cs"
    $handheldStatePath = Join-Path $RepoRoot "src\JueMingZ\Automation\Blueprint\BlueprintHandheldActionBarState.cs"
    $hookPath = Join-Path $RepoRoot "src\JueMingZ\Bootstrap\HookInstaller.cs"
    $inputHookPath = Join-Path $RepoRoot "src\JueMingZ\Hooks\PlayerInputScrollHookInstaller.cs"
    $placedTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintPlacedInstanceTests.cs"
    $handheldTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintHandheldActionBarTests.cs"
    $eraseTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintEraseTests.cs"
    $programPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.cs"
    $csprojPath = Join-Path $RepoRoot "src\JueMingZ\JueMingZ.csproj"
    $runtimePath = Join-Path $RepoRoot "src\JueMingZ\Runtime\JueMingZRuntime.cs"
    $auditPath = Join-Path $RepoRoot "scripts\audit-project-health.ps1"
    $functionDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $diagnosticsDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "AI诊断日志说明.md")
    $plan00Path = Join-BlueprintPlacementPlanPath -RepoRoot $RepoRoot -Leaf "00-基准.md"
    $plan07Path = Join-BlueprintPlacementPlanPath -RepoRoot $RepoRoot -Leaf "07-移动与镜像治理.md"
    $currentPlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "索引.md")
    $updateIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "索引.md")
    $updateDirectory = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录")
    $docHistoryIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "索引.md")
    $docHistoryDirectory = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史")

    $updateRecordPath = ""
    if (Test-Path -LiteralPath $updateDirectory) {
        $record = Get-ChildItem -LiteralPath $updateDirectory -Filter "0.952-移动与镜像治理-*.md" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($record) {
            $updateRecordPath = $record.FullName
        }
    }

    $docHistoryRecordPath = ""
    if (Test-Path -LiteralPath $docHistoryDirectory) {
        $record = Get-ChildItem -LiteralPath $docHistoryDirectory -Filter "蓝图移动与镜像治理-*.md" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($record) {
            $docHistoryRecordPath = $record.FullName
        }
    }

    $modelText = Read-TextIfExists -Path $modelPath
    $transformStateText = Read-TextIfExists -Path $transformStatePath
    $transformOverlayText = Read-TextIfExists -Path $transformOverlayPath
    $actionText = Read-TextIfExists -Path $actionPath
    $handheldStateText = Read-TextIfExists -Path $handheldStatePath
    $hookText = Read-TextIfExists -Path $hookPath
    $inputHookText = Read-TextIfExists -Path $inputHookPath
    $placedTestText = Read-TextIfExists -Path $placedTestPath
    $handheldTestText = Read-TextIfExists -Path $handheldTestPath
    $eraseTestText = Read-TextIfExists -Path $eraseTestPath
    $programText = Read-TextIfExists -Path $programPath
    $csprojText = Read-TextIfExists -Path $csprojPath
    $runtimeText = Read-TextIfExists -Path $runtimePath
    $auditText = Read-TextIfExists -Path $auditPath
    $functionDocText = Read-TextIfExists -Path $functionDocPath
    $diagnosticsDocText = Read-TextIfExists -Path $diagnosticsDocPath
    $plan00Text = Read-TextIfExists -Path $plan00Path
    $plan07Text = Read-TextIfExists -Path $plan07Path
    $currentPlanIndexText = Read-TextIfExists -Path $currentPlanIndexPath
    $updateIndexText = Read-TextIfExists -Path $updateIndexPath
    $updateRecordText = if ([string]::IsNullOrWhiteSpace($updateRecordPath)) { $null } else { Read-TextIfExists -Path $updateRecordPath }
    $docHistoryIndexText = Read-TextIfExists -Path $docHistoryIndexPath
    $docHistoryRecordText = if ([string]::IsNullOrWhiteSpace($docHistoryRecordPath)) { $null } else { Read-TextIfExists -Path $docHistoryRecordPath }

    if (Test-BlueprintPlacementVersionMetadata -RuntimeText $runtimeText -CsprojText $csprojText -AllowedRuntimeVersions @(
            "0.952-blueprint-move-mirror-governance",
            "0.953-blueprint-placement-regression-audit",
            "0.954-blueprint-placement-closeout",
            "0.955-blueprint-feedback-autoplace-plan",
            "0.956-blueprint-feedback-fact-freeze",
            "0.957-blueprint-f5-library-submenus",
            "0.958-blueprint-placed-list-layout")) {
        Write-Pass "Blueprint stage 07 version metadata is synchronized or has advanced to stage 08."
    }
    else {
        Write-FailHealth "Blueprint stage 07 must synchronize RuntimeVersion and project version metadata to 0.952-blueprint-move-mirror-governance."
    }

    if ($modelText -and
        $modelText.Contains("AutoPlacementProgressState") -and
        $transformStateText -and
        $transformStateText.Contains("BeginMove") -and
        $transformStateText.Contains("BeginMirror") -and
        $transformStateText.Contains("TryMirrorHorizontal") -and
        $transformStateText.Contains("autoPlacementProgressActive") -and
        $transformStateText.Contains("RefreshAfterInstanceMutation") -and
        $transformStateText.Contains("模板、世界实物") -and
        -not $transformStateText.Contains("InputActionQueue") -and
        $transformOverlayText -and
        $transformOverlayText.Contains("placed-transform") -and
        $transformOverlayText.Contains("ShouldBlockTransformForPointerOwnership") -and
        $hookText.Contains("BlueprintPlacedInstanceTransformOverlay.UpdatePrefixGuard") -and
        $inputHookText.Contains("BlueprintPlacedInstanceTransformOverlay.UpdateAfterPlayerInputGuard")) {
        Write-Pass "Blueprint stage 07 transform state moves/mirrors placed instances only, blocks auto-progress markers, and registers world input guards."
    }
    else {
        Write-FailHealth "Blueprint stage 07 must add a placed-instance transform state/overlay, auto-progress fail-closed marker, cache refresh, and no ActionQueue/world-tile mutation path."
    }

    if ($actionText -and
        $actionText.Contains("BlueprintPlacedInstanceTransformState.BeginMove") -and
        $actionText.Contains("BlueprintPlacedInstanceTransformState.BeginMirror") -and
        $actionText.Contains("transformInputActive") -and
        $actionText.Contains('\"uiOnly\":false') -and
        $actionText.Contains("placedTransform") -and
        $handheldStateText -and
        ($handheldStateText.Contains('new BlueprintHandheldActionBarButtonDefinition(ButtonIdMove, "移动蓝图", 4, "只能移动已放置蓝图")') -or
            ($handheldStateText.Contains('MoveButtonTooltip = "点击蓝图使其进入浮动状态重新放置"') -and
                $handheldStateText.Contains('MoveCancelButtonLabel = "取消移动"'))) -and
        $handheldStateText.Contains('new BlueprintHandheldActionBarButtonDefinition(ButtonIdMirror, "镜像", 6, "镜像已放置蓝图")')) {
        Write-Pass "Blueprint stage 07 handheld move/mirror buttons route to real placed-instance transform selection."
    }
    else {
        Write-FailHealth "Blueprint stage 07 handheld move/mirror commands must start real transform selection and expose placedTransform UI state."
    }

    if ($placedTestText -and
        $placedTestText.Contains("BlueprintPlacedInstanceMoveKeepsSnapshotStateAndRefreshesCaches") -and
        $placedTestText.Contains("BlueprintPlacedInstanceMirrorUsesServiceAndFailsClosed") -and
        $placedTestText.Contains("autoPlacementProgressActive") -and
        $handheldTestText -and
        $handheldTestText.Contains("handheld move starts transform mode") -and
        $handheldTestText.Contains("handheld mirror starts transform mode") -and
        $eraseTestText -and
        $eraseTestText.Contains("RefreshAfterWorldInstancesChanged") -and
        $programText -and
        $programText.Contains("blueprint placed instance move keeps snapshot state and refreshes caches") -and
        $programText.Contains("blueprint placed instance mirror uses service and fails closed")) {
        Write-Pass "Blueprint stage 07 console regressions cover move, mirror fail-closed, auto-progress marker, handheld routing, and cache refresh boundaries."
    }
    else {
        Write-FailHealth "Blueprint stage 07 must register move/mirror console regressions and keep direct erase-state projection refresh tests deterministic."
    }

    if ($auditText -and
        $auditText.Contains("Test-BlueprintPlacementStage07MoveMirrorGovernance") -and
        $auditText.Contains("0.952-移动与镜像治理")) {
        Write-Pass "Blueprint scoped health audit includes the stage 07 move/mirror governance check."
    }
    else {
        Write-FailHealth "Blueprint scoped health audit must include Test-BlueprintPlacementStage07MoveMirrorGovernance and the 0.952 record anchor."
    }

    if ($functionDocText -and
        $functionDocText.Contains("0.952-blueprint-move-mirror-governance") -and
        $functionDocText.Contains("moveTargetSelectStarted") -and
        $functionDocText.Contains("mirrorTargetSelectStarted") -and
        $functionDocText.Contains("autoPlacementProgressActive") -and
        $diagnosticsDocText -and
        $diagnosticsDocText.Contains("0.952-blueprint-move-mirror-governance") -and
        $diagnosticsDocText.Contains("placedTransform") -and
        $diagnosticsDocText.Contains("Test-BlueprintPlacementStage07MoveMirrorGovernance")) {
        Write-Pass "Blueprint stage 07 function and diagnostics docs describe move/mirror semantics and action-event state."
    }
    else {
        Write-FailHealth "Blueprint stage 07 function and diagnostics docs must describe 0.952 move/mirror semantics, placedTransform diagnostics, and the scoped health audit."
    }

    if ($plan07Text -and
        $plan07Text.Contains("状态：已完成") -and
        $plan07Text.Contains('RuntimeVersion：`0.952-blueprint-move-mirror-governance`') -and
        $plan07Text.Contains('未在本会话实现或验证 `08`') -and
        $plan07Text.Contains("不生成测试包") -and
        $plan07Text.Contains("Test-BlueprintPlacementStage07MoveMirrorGovernance") -and
        $plan00Text -and
        ($plan00Text.Contains("0.952-blueprint-move-mirror-governance") -or
            $plan00Text.Contains("0.953-blueprint-placement-regression-audit") -or
            $plan00Text.Contains("0.954-blueprint-placement-closeout")) -and
        ($plan00Text.Contains('下一入口为 `08-回归诊断与审计防线.md`') -or
            $plan00Text.Contains('下一入口为 `09-验证打包与归档收口.md`')) -and
        $currentPlanIndexText -and
        ($currentPlanIndexText.Contains("0.952-blueprint-move-mirror-governance") -or
            $currentPlanIndexText.Contains("0.953-blueprint-placement-regression-audit") -or
            $currentPlanIndexText.Contains("0.954-blueprint-placement-closeout")) -and
        ($currentPlanIndexText.Contains('下一入口为 `08-回归诊断与审计防线.md`') -or
            $currentPlanIndexText.Contains('下一入口为 `09-验证打包与归档收口.md`'))) {
        Write-Pass "Blueprint placement plan files record stage 07 completion, no-package scope, and next-stage boundary."
    }
    else {
        Write-FailHealth "Blueprint placement plan files must record stage 07 completion, 0.952 version, no-package scope, and main-project locked next-stage boundary."
    }

    if ($updateIndexText -and
        $updateIndexText.Contains("0.952-移动与镜像治理") -and
        $updateRecordText -and
        $updateRecordText.Contains('RuntimeVersion：`0.952-blueprint-move-mirror-governance`') -and
        $updateRecordText.Contains("Test-BlueprintPlacementStage07MoveMirrorGovernance") -and
        $updateRecordText.Contains("不生成测试包") -and
        $docHistoryIndexText -and
        $docHistoryIndexText.Contains("蓝图移动与镜像治理") -and
        $docHistoryRecordText -and
        $docHistoryRecordText.Contains("0.952-blueprint-move-mirror-governance") -and
        $docHistoryRecordText.Contains("不生成测试包")) {
        Write-Pass "Blueprint stage 07 update record and document-change history are synchronized."
    }
    else {
        Write-FailHealth "Blueprint stage 07 must synchronize update index/record and document-change history for 0.952."
    }
}

function Test-BlueprintPlacementStage08RegressionDiagnosticsGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $diagnosticsTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintDiagnosticsTests.cs"
    $programPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.cs"
    $auditPath = Join-Path $RepoRoot "scripts\audit-project-health.ps1"
    $csprojPath = Join-Path $RepoRoot "src\JueMingZ\JueMingZ.csproj"
    $runtimePath = Join-Path $RepoRoot "src\JueMingZ\Runtime\JueMingZRuntime.cs"
    $actionPath = Join-Path $RepoRoot "src\JueMingZ\Input\LegacyUiActionService.Blueprint.cs"
    $compatDirectory = Join-Path $RepoRoot "src\JueMingZ\Compat"
    $functionDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $diagnosticsDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "AI诊断日志说明.md")
    $plan00Path = Join-BlueprintPlacementPlanPath -RepoRoot $RepoRoot -Leaf "00-基准.md"
    $plan08Path = Join-BlueprintPlacementPlanPath -RepoRoot $RepoRoot -Leaf "08-回归诊断与审计防线.md"
    $currentPlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "索引.md")
    $updateIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "索引.md")
    $updateDirectory = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录")
    $docHistoryIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "索引.md")
    $docHistoryDirectory = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史")

    $updateRecordPath = ""
    if (Test-Path -LiteralPath $updateDirectory) {
        $record = Get-ChildItem -LiteralPath $updateDirectory -Filter "0.953-蓝图回归诊断审计-*.md" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($record) {
            $updateRecordPath = $record.FullName
        }
    }

    $docHistoryRecordPath = ""
    if (Test-Path -LiteralPath $docHistoryDirectory) {
        $record = Get-ChildItem -LiteralPath $docHistoryDirectory -Filter "蓝图回归诊断审计-*.md" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($record) {
            $docHistoryRecordPath = $record.FullName
        }
    }

    $diagnosticsTestText = Read-TextIfExists -Path $diagnosticsTestPath
    $programText = Read-TextIfExists -Path $programPath
    $auditText = Read-TextIfExists -Path $auditPath
    $csprojText = Read-TextIfExists -Path $csprojPath
    $runtimeText = Read-TextIfExists -Path $runtimePath
    $actionText = Read-TextIfExists -Path $actionPath
    $functionDocText = Read-TextIfExists -Path $functionDocPath
    $diagnosticsDocText = Read-TextIfExists -Path $diagnosticsDocPath
    $plan00Text = Read-TextIfExists -Path $plan00Path
    $plan08Text = Read-TextIfExists -Path $plan08Path
    $currentPlanIndexText = Read-TextIfExists -Path $currentPlanIndexPath
    $updateIndexText = Read-TextIfExists -Path $updateIndexPath
    $updateRecordText = if ([string]::IsNullOrWhiteSpace($updateRecordPath)) { $null } else { Read-TextIfExists -Path $updateRecordPath }
    $docHistoryIndexText = Read-TextIfExists -Path $docHistoryIndexPath
    $docHistoryRecordText = if ([string]::IsNullOrWhiteSpace($docHistoryRecordPath)) { $null } else { Read-TextIfExists -Path $docHistoryRecordPath }

    $compatText = ""
    if (Test-Path -LiteralPath $compatDirectory) {
        foreach ($compatFile in Get-ChildItem -LiteralPath $compatDirectory -Filter "*.cs" -File -ErrorAction SilentlyContinue) {
            $compatText += "`n" + (Read-TextIfExists -Path $compatFile.FullName)
        }
    }

    if (Test-BlueprintPlacementVersionMetadata -RuntimeText $runtimeText -CsprojText $csprojText -AllowedRuntimeVersions @(
            "0.953-blueprint-placement-regression-audit",
            "0.954-blueprint-placement-closeout",
            "0.955-blueprint-feedback-autoplace-plan",
            "0.956-blueprint-feedback-fact-freeze",
            "0.957-blueprint-f5-library-submenus",
            "0.958-blueprint-placed-list-layout")) {
        Write-Pass "Blueprint stage 08 version metadata is synchronized or has advanced to stage 09 closeout."
    }
    else {
        Write-FailHealth "Blueprint stage 08 must synchronize RuntimeVersion and project version metadata to 0.953-blueprint-placement-regression-audit."
    }

    if ($diagnosticsTestText -and
        $diagnosticsTestText.Contains("BlueprintPlacementStage08RegressionDiagnosticsContractsStayWired") -and
        $diagnosticsTestText.Contains("BlueprintLibraryStage10DiagnosticsAuditContractsStayWired") -and
        $diagnosticsTestText.Contains("BlueprintHandheldUiClickOwnershipContractsStayWired") -and
        $diagnosticsTestText.Contains("BlueprintCreationFlickerFixContractsStayWired") -and
        $diagnosticsTestText.Contains("BlueprintMenuUiStateDoesNotRefreshProjectionOrMaterials") -and
        $diagnosticsTestText.Contains("BlueprintHandheldActionBarRealCommandsAndDeferredPlacedCommands") -and
        $diagnosticsTestText.Contains("BlueprintPlacementConfirmRefreshesProjectionAndPlacedList") -and
        $diagnosticsTestText.Contains("BlueprintProjectionStage04LaterInstanceCoversEarlierWithoutMutatingSnapshots") -and
        $diagnosticsTestText.Contains("BlueprintProjectionUiOverlayAndDiagnosticsContracts") -and
        $diagnosticsTestText.Contains("BlueprintPlacedListRefreshesMaterialComparisonWithoutDrawScan") -and
        $diagnosticsTestText.Contains("BlueprintPlacedInstanceClearAllCurrentWorldKeepsTemplatesAndRefreshesCaches") -and
        $diagnosticsTestText.Contains("BlueprintEraseSingleInstanceClipsProjectionAndMaterials") -and
        $diagnosticsTestText.Contains("BlueprintPlacedInstanceMoveKeepsSnapshotStateAndRefreshesCaches") -and
        $diagnosticsTestText.Contains("BlueprintPlacedInstanceMirrorUsesServiceAndFailsClosed") -and
        $diagnosticsTestText.Contains("BlueprintDiagnosticsAggregateRuntimeSnapshotJson") -and
        $programText -and
        $programText.Contains("blueprint placement stage 08 regression diagnostics contracts stay wired")) {
        Write-Pass "Blueprint stage 08 aggregate regression test is registered and reuses stage 02-07 plus old creation/ownership contracts."
    }
    else {
        Write-FailHealth "Blueprint stage 08 must register an aggregate console regression covering library UI, handheld, placement, projection/materials, clear/erase, move/mirror, diagnostics, and ownership contracts."
    }

    if ($auditText -and
        $auditText.Contains("Test-BlueprintPlacementStage08RegressionDiagnosticsGovernance") -and
        $auditText.Contains("0.953-blueprint-placement-regression-audit") -and
        $auditText.Contains("BlueprintPlacementStage08RegressionDiagnosticsContractsStayWired")) {
        Write-Pass "Blueprint scoped health audit includes the stage 08 regression diagnostics governance check."
    }
    else {
        Write-FailHealth "Blueprint scoped health audit must include Test-BlueprintPlacementStage08RegressionDiagnosticsGovernance and the 0.953 record anchor."
    }

    if ($runtimeText -and
        -not $runtimeText.Contains("BlueprintPlacedInstanceTransformState.BeginMove") -and
        -not $runtimeText.Contains("BlueprintPlacedInstanceTransformState.BeginMirror") -and
        -not $runtimeText.Contains("ClearAllCurrentWorld") -and
        -not $compatText.Contains("BlueprintPlacedInstanceTransformState") -and
        -not $compatText.Contains("BlueprintWorldInstanceStore") -and
        $actionText -and
        $actionText.Contains("BlueprintPlacedInstanceUiState.ClearAllCurrentWorld") -and
        $actionText.Contains("BlueprintPlacedInstanceTransformState.BeginMove") -and
        $actionText.Contains("BlueprintPlacedInstanceTransformState.BeginMirror")) {
        Write-Pass "Blueprint stage 08 keeps placed-instance business out of Runtime and Compat while preserving the UI command router as a thin entry."
    }
    else {
        Write-FailHealth "Blueprint stage 08 must keep placed-instance business out of Runtime/Compat and preserve only the existing thin UI command routing."
    }

    if ($functionDocText -and
        $functionDocText.Contains("0.953-blueprint-placement-regression-audit") -and
        $functionDocText.Contains("BlueprintPlacementStage08RegressionDiagnosticsContractsStayWired") -and
        $functionDocText.Contains("不新增用户可见蓝图行为") -and
        $diagnosticsDocText -and
        $diagnosticsDocText.Contains("0.953-blueprint-placement-regression-audit") -and
        $diagnosticsDocText.Contains("BlueprintPlacementStage08RegressionDiagnosticsContractsStayWired") -and
        $diagnosticsDocText.Contains("不新增 trace JSONL") -and
        $diagnosticsDocText.Contains("Test-BlueprintPlacementStage08RegressionDiagnosticsGovernance")) {
        Write-Pass "Blueprint stage 08 function and diagnostics docs describe aggregate regression/audit scope without new runtime diagnostics."
    }
    else {
        Write-FailHealth "Blueprint stage 08 function and diagnostics docs must describe the 0.953 aggregate regression/audit scope without new runtime diagnostics."
    }

    if ($plan08Text -and
        $plan08Text.Contains("状态：已完成") -and
        $plan08Text.Contains('RuntimeVersion：`0.953-blueprint-placement-regression-audit`') -and
        $plan08Text.Contains("BlueprintPlacementStage08RegressionDiagnosticsContractsStayWired") -and
        $plan08Text.Contains("Test-BlueprintPlacementStage08RegressionDiagnosticsGovernance") -and
        $plan08Text.Contains("未生成测试包") -and
        $plan08Text.Contains('未实现、修改或验证 `09`') -and
        $plan00Text -and
        ($plan00Text.Contains("0.953-blueprint-placement-regression-audit") -or
            $plan00Text.Contains("0.954-blueprint-placement-closeout")) -and
        ($plan00Text.Contains('下一入口为 `09-验证打包与归档收口.md`') -or
            $plan00Text.Contains("已完成并归档")) -and
        $currentPlanIndexText -and
        ($currentPlanIndexText.Contains("0.953-blueprint-placement-regression-audit") -or
            $currentPlanIndexText.Contains("0.954-blueprint-placement-closeout")) -and
        ($currentPlanIndexText.Contains('下一入口为 `09-验证打包与归档收口.md`') -or
            $currentPlanIndexText.Contains("已按 `09-验证打包与归档收口.md` 完成"))) {
        Write-Pass "Blueprint placement plan files record stage 08 completion, no-package scope, and next-stage boundary."
    }
    else {
        Write-FailHealth "Blueprint placement plan files must record stage 08 completion, 0.953 version, no-package scope, and stage 09 boundary."
    }

    if ($updateIndexText -and
        $updateIndexText.Contains("0.953-蓝图回归诊断审计") -and
        $updateRecordText -and
        $updateRecordText.Contains('RuntimeVersion：`0.953-blueprint-placement-regression-audit`') -and
        $updateRecordText.Contains("BlueprintPlacementStage08RegressionDiagnosticsContractsStayWired") -and
        $updateRecordText.Contains("Test-BlueprintPlacementStage08RegressionDiagnosticsGovernance") -and
        $updateRecordText.Contains("不生成测试包") -and
        $docHistoryIndexText -and
        $docHistoryIndexText.Contains("蓝图回归诊断审计") -and
        $docHistoryRecordText -and
        $docHistoryRecordText.Contains("0.953-blueprint-placement-regression-audit") -and
        $docHistoryRecordText.Contains("不生成测试包")) {
        Write-Pass "Blueprint stage 08 update record and document-change history are synchronized."
    }
    else {
        Write-FailHealth "Blueprint stage 08 must synchronize update index/record and document-change history for 0.953."
    }
}

function Test-BlueprintPlacementStage09CloseoutGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $auditPath = Join-Path $RepoRoot "scripts\audit-project-health.ps1"
    $csprojPath = Join-Path $RepoRoot "src\JueMingZ\JueMingZ.csproj"
    $runtimePath = Join-Path $RepoRoot "src\JueMingZ\Runtime\JueMingZRuntime.cs"
    $functionDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $diagnosticsDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "AI诊断日志说明.md")
    $currentPlanDirectory = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "蓝图放置与实例治理修复")
    $archivePlanDirectory = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图放置与实例治理修复")
    $plan00Path = Join-BlueprintPlacementPlanPath -RepoRoot $RepoRoot -Leaf "00-基准.md"
    $plan09Path = Join-BlueprintPlacementPlanPath -RepoRoot $RepoRoot -Leaf "09-验证打包与归档收口.md"
    $currentPlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "索引.md")
    $archivePlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "索引.md")
    $updateIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "索引.md")
    $updateDirectory = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录")
    $docHistoryIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "索引.md")
    $docHistoryDirectory = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史")

    $updateRecordPath = ""
    if (Test-Path -LiteralPath $updateDirectory) {
        $record = Get-ChildItem -LiteralPath $updateDirectory -Filter "0.954-蓝图放置治理验证收口-*.md" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($record) {
            $updateRecordPath = $record.FullName
        }
    }

    $docHistoryRecordPath = ""
    if (Test-Path -LiteralPath $docHistoryDirectory) {
        $record = Get-ChildItem -LiteralPath $docHistoryDirectory -Filter "蓝图放置治理验证收口-*.md" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($record) {
            $docHistoryRecordPath = $record.FullName
        }
    }

    $auditText = Read-TextIfExists -Path $auditPath
    $csprojText = Read-TextIfExists -Path $csprojPath
    $runtimeText = Read-TextIfExists -Path $runtimePath
    $functionDocText = Read-TextIfExists -Path $functionDocPath
    $diagnosticsDocText = Read-TextIfExists -Path $diagnosticsDocPath
    $plan00Text = Read-TextIfExists -Path $plan00Path
    $plan09Text = Read-TextIfExists -Path $plan09Path
    $currentPlanIndexText = Read-TextIfExists -Path $currentPlanIndexPath
    $archivePlanIndexText = Read-TextIfExists -Path $archivePlanIndexPath
    $updateIndexText = Read-TextIfExists -Path $updateIndexPath
    $updateRecordText = if ([string]::IsNullOrWhiteSpace($updateRecordPath)) { $null } else { Read-TextIfExists -Path $updateRecordPath }
    $docHistoryIndexText = Read-TextIfExists -Path $docHistoryIndexPath
    $docHistoryRecordText = if ([string]::IsNullOrWhiteSpace($docHistoryRecordPath)) { $null } else { Read-TextIfExists -Path $docHistoryRecordPath }

    if (Test-BlueprintPlacementVersionMetadata -RuntimeText $runtimeText -CsprojText $csprojText -AllowedRuntimeVersions @("0.954-blueprint-placement-closeout", "0.955-blueprint-feedback-autoplace-plan", "0.956-blueprint-feedback-fact-freeze", "0.957-blueprint-f5-library-submenus", "0.958-blueprint-placed-list-layout")) {
        Write-Pass "Blueprint stage 09 closeout version metadata is synchronized."
    }
    else {
        Write-FailHealth "Blueprint stage 09 must synchronize RuntimeVersion and project version metadata to 0.954-blueprint-placement-closeout."
    }

    if ((Test-Path -LiteralPath $archivePlanDirectory) -and
        -not (Test-Path -LiteralPath $currentPlanDirectory) -and
        $plan00Text -and
        $plan00Text.Contains("状态：已完成") -and
        $plan00Text.Contains("0.954-blueprint-placement-closeout") -and
        $plan09Text -and
        $plan09Text.Contains("状态：已完成") -and
        $plan09Text.Contains("JueMingZ-TestPackage") -and
        $plan09Text.Contains("-RequireFreshTestPackage") -and
        $plan09Text.Contains("不创建后续")) {
        Write-Pass "Blueprint placement plan is archived with stage 09 closeout, default package, strict freshness audit, and relay termination recorded."
    }
    else {
        Write-FailHealth "Blueprint stage 09 must archive the placement plan and mark 00/09 complete with package, strict freshness audit, and no-next-thread scope."
    }

    if ($currentPlanIndexText -and
        $currentPlanIndexText.Contains("当前没有正在推进的计划") -and
        $currentPlanIndexText.Contains("蓝图放置与实例治理修复") -and
        $currentPlanIndexText.Contains("0.954-blueprint-placement-closeout") -and
        $archivePlanIndexText -and
        $archivePlanIndexText.Contains("蓝图放置与实例治理修复") -and
        $archivePlanIndexText.Contains("0.954-blueprint-placement-closeout") -and
        $archivePlanIndexText.Contains("JueMingZ-TestPackage")) {
        Write-Pass "Blueprint stage 09 current and archive plan indexes are synchronized."
    }
    else {
        Write-FailHealth "Blueprint stage 09 must remove the plan from current work and add a 0.954 closeout entry to archive/current indexes."
    }

    if ($functionDocText -and
        $functionDocText.Contains("0.954-blueprint-placement-closeout") -and
        $functionDocText.Contains('默认 `JueMingZ-TestPackage`') -and
        $functionDocText.Contains("严格新鲜包健康审计") -and
        $diagnosticsDocText -and
        $diagnosticsDocText.Contains("0.954-blueprint-placement-closeout") -and
        $diagnosticsDocText.Contains("不新增 runtime snapshot 字段") -and
        $diagnosticsDocText.Contains("不新增 trace JSONL")) {
        Write-Pass "Blueprint stage 09 function and diagnostics docs describe closeout/package scope without new diagnostics."
    }
    else {
        Write-FailHealth "Blueprint stage 09 function and diagnostics docs must describe 0.954 closeout, default package, strict freshness audit, and no new diagnostics."
    }

    if ($updateIndexText -and
        $updateIndexText.Contains("0.954-蓝图放置治理验证收口") -and
        $updateRecordText -and
        $updateRecordText.Contains('RuntimeVersion：`0.954-blueprint-placement-closeout`') -and
        $updateRecordText.Contains("JueMingZ-TestPackage") -and
        $updateRecordText.Contains("-RequireFreshTestPackage") -and
        $updateRecordText.Contains("不生成源码包") -and
        $docHistoryIndexText -and
        $docHistoryIndexText.Contains("蓝图放置治理验证收口") -and
        $docHistoryRecordText -and
        $docHistoryRecordText.Contains("0.954-blueprint-placement-closeout") -and
        $docHistoryRecordText.Contains("严格新鲜包健康审计")) {
        Write-Pass "Blueprint stage 09 update record and document-change history are synchronized."
    }
    else {
        Write-FailHealth "Blueprint stage 09 must synchronize update index/record and document-change history for 0.954 closeout."
    }

    if ($auditText -and
        $auditText.Contains("Test-BlueprintPlacementStage09CloseoutGovernance") -and
        $auditText.Contains("0.954-blueprint-placement-closeout") -and
        $auditText.Contains("Join-BlueprintPlacementPlanPath")) {
        Write-Pass "Blueprint scoped health audit includes the stage 09 closeout governance check and archived-plan path resolution."
    }
    else {
        Write-FailHealth "Blueprint scoped health audit must include Test-BlueprintPlacementStage09CloseoutGovernance and archived-plan path resolution."
    }
}

function Test-BlueprintPlacementReleaseGateGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $statePath = Join-Path $RepoRoot "src\JueMingZ\Automation\Blueprint\BlueprintPlacementPreviewState.cs"
    $overlayPath = Join-Path $RepoRoot "src\JueMingZ\UI\BlueprintPlacementPreviewOverlay.cs"
    $testPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintPlacementPreviewTests.cs"
    $programPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.cs"
    $auditPath = Join-Path $RepoRoot "scripts\audit-project-health.ps1"
    $csprojPath = Join-Path $RepoRoot "src\JueMingZ\JueMingZ.csproj"
    $runtimePath = Join-Path $RepoRoot "src\JueMingZ\Runtime\JueMingZRuntime.cs"
    $functionDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $diagnosticsDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "AI诊断日志说明.md")
    $updateIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "索引.md")
    $docHistoryIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "索引.md")

    $stateText = Read-TextIfExists -Path $statePath
    $overlayText = Read-TextIfExists -Path $overlayPath
    $testText = Read-TextIfExists -Path $testPath
    $programText = Read-TextIfExists -Path $programPath
    $auditText = Read-TextIfExists -Path $auditPath
    $csprojText = Read-TextIfExists -Path $csprojPath
    $runtimeText = Read-TextIfExists -Path $runtimePath
    $functionDocText = Read-TextIfExists -Path $functionDocPath
    $diagnosticsDocText = Read-TextIfExists -Path $diagnosticsDocPath
    $updateIndexText = Read-TextIfExists -Path $updateIndexPath
    $docHistoryIndexText = Read-TextIfExists -Path $docHistoryIndexPath

    if (Test-BlueprintPlacementVersionMetadata -RuntimeText $runtimeText -CsprojText $csprojText -AllowedRuntimeVersions @("0.971-blueprint-placement-release-gate")) {
        Write-Pass "Blueprint placement release-gate version metadata is synchronized."
    }
    else {
        Write-FailHealth "Blueprint placement release-gate must synchronize RuntimeVersion and project metadata to 0.971-blueprint-placement-release-gate."
    }

    if ($stateText -and
        $stateText.Contains("_awaitInitialLeftRelease") -and
        $stateText.Contains("awaitingInitialLeftRelease") -and
        $stateText.Contains("initialLeftReleased") -and
        $stateText.Contains("held physical button")) {
        Write-Pass "Blueprint placement preview state keeps the initial physical-left release gate."
    }
    else {
        Write-FailHealth "Blueprint placement preview state must require a physical-left release before the first placement confirmation."
    }

    if ($overlayText -and
        $overlayText.Contains("_wasPhysicalLeftDown") -and
        $overlayText.Contains("BuildPointerInputFromPhysicalEdgesForTesting") -and
        $overlayText.Contains("ResolvePhysicalLeftDown") -and
        $overlayText.Contains("resolved world-left only decides")) {
        Write-Pass "Blueprint placement overlay derives press edges from physical-left while preserving strict world-left gating."
    }
    else {
        Write-FailHealth "Blueprint placement overlay must keep physical-left edge derivation and strict world-left placement gating."
    }

    if ($testText -and
        $testText.Contains("BlueprintPlacementPreviewWaitsForPhysicalLeftReleaseBeforeConfirm") -and
        $testText.Contains("awaitingInitialLeftRelease") -and
        $testText.Contains("initialLeftReleased") -and
        $programText -and
        $programText.Contains("blueprint placement preview waits for physical left release before confirm")) {
        Write-Pass "Blueprint placement release-gate console regression is registered."
    }
    else {
        Write-FailHealth "Blueprint placement release-gate console regression must remain registered."
    }

    if ($auditText -and
        $auditText.Contains("Test-BlueprintPlacementReleaseGateGovernance") -and
        $functionDocText -and
        $functionDocText.Contains("0.971-blueprint-placement-release-gate") -and
        $diagnosticsDocText -and
        $diagnosticsDocText.Contains("0.971-blueprint-placement-release-gate") -and
        $updateIndexText -and
        $updateIndexText.Contains("0.971-蓝图放置预览松手门闩") -and
        $docHistoryIndexText -and
        $docHistoryIndexText.Contains("蓝图放置预览松手门闩")) {
        Write-Pass "Blueprint placement release-gate docs and health audit anchors are synchronized."
    }
    else {
        Write-FailHealth "Blueprint placement release-gate must update function docs, diagnostics docs, update index, document-change history, and health audit anchors."
    }
}

function Test-BlueprintFeedbackAutoplacePlanGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $planDirectory = Get-BlueprintFeedbackAutoplacePlanDirectory -RepoRoot $RepoRoot
    $plan00Path = Join-Path $planDirectory "00-基准.md"
    $plan01Path = Join-Path $planDirectory "01-事实冻结与资料矩阵.md"
    $currentPlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "索引.md")
    $functionDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $updateIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "索引.md")
    $updateRecordPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "0.955-蓝图实机反馈自动放置方案-2606240117.md")
    $stage01UpdateRecordPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "0.956-蓝图反馈事实冻结-2606240137.md")
    $docHistoryIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "索引.md")
    $docHistoryRecordPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "蓝图实机反馈自动放置方案-2606240117.md")
    $stage01DocHistoryRecordPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "蓝图反馈事实冻结-2606240137.md")
    $auditPath = Join-Path $RepoRoot "scripts\audit-project-health.ps1"

    $plan00Text = Read-TextIfExists -Path $plan00Path
    $plan01Text = Read-TextIfExists -Path $plan01Path
    $currentPlanIndexText = Read-TextIfExists -Path $currentPlanIndexPath
    $functionDocText = Read-TextIfExists -Path $functionDocPath
    $updateIndexText = Read-TextIfExists -Path $updateIndexPath
    $updateRecordText = Read-TextIfExists -Path $updateRecordPath
    $stage01UpdateRecordText = Read-TextIfExists -Path $stage01UpdateRecordPath
    $docHistoryIndexText = Read-TextIfExists -Path $docHistoryIndexPath
    $docHistoryRecordText = Read-TextIfExists -Path $docHistoryRecordPath
    $stage01DocHistoryRecordText = Read-TextIfExists -Path $stage01DocHistoryRecordPath
    $auditText = Read-TextIfExists -Path $auditPath

    $expectedStageFiles = @(
        "01-事实冻结与资料矩阵.md",
        "02-F5子菜单互斥与蓝图库整理.md",
        "03-已放置列表布局与材料面板.md",
        "04-手持快捷栏点击坐标与状态提示.md",
        "05-投影完成进度与材料需求语义.md",
        "06-移动蓝图交互联动.md",
        "07-区域修改持续态联动.md",
        "08-镜像一次性功能修复.md",
        "09-自动放置事实复核与入口治理.md",
        "10-自动放置与同类替换执行链路.md",
        "11-回归诊断与审计防线.md",
        "12-验证打包与归档收口.md"
    )

    $allStageFilesExist = Test-Path -LiteralPath $plan00Path
    $allStagesCarryBottomLines = $true
    foreach ($leaf in $expectedStageFiles) {
        $stagePath = Join-Path $planDirectory $leaf
        $stageText = Read-TextIfExists -Path $stagePath
        if (-not $stageText) {
            $allStageFilesExist = $false
            $allStagesCarryBottomLines = $false
            continue
        }

        if (-not (
                $stageText.Contains("## 五大底线执行要求") -and
                $stageText.Contains("性能底线") -and
                $stageText.Contains("功能底线") -and
                $stageText.Contains("注释底线") -and
                $stageText.Contains("指令底线") -and
                $stageText.Contains("职责边界底线"))) {
            $allStagesCarryBottomLines = $false
        }
    }

    $planFileCount = 0
    if (Test-Path -LiteralPath $planDirectory) {
        $planFileCount = (Get-ChildItem -LiteralPath $planDirectory -Filter "*.md" -File -ErrorAction SilentlyContinue | Measure-Object).Count
    }

    if ($allStageFilesExist -and
        $planFileCount -eq 13 -and
        $allStagesCarryBottomLines -and
        $plan00Text -and
        $plan00Text.Contains("1：玩家在蓝图库点击放置") -and
        $plan00Text.Contains("28：本次需要实现自动放置") -and
        $plan00Text.Contains("参考小助手") -and
        $plan00Text.Contains("兼容已有同类替换") -and
        $plan00Text.Contains("019ef1d0-d34b-7e21-861e-53ed17944d39") -and
        $plan00Text.Contains("019ef2a8-5dc5-7463-942a-fb826003955b") -and
        $plan00Text.Contains('create_thread') -and
        $plan00Text.Contains('environment.type=local') -and
        $plan00Text.Contains('C:\Users\kongd\Desktop\JueMingZ') -and
        $plan00Text.Contains(".codex\worktrees") -and
        $plan00Text.Contains("禁止新 Git 分支") -and
        $plan00Text.Contains("0.955-blueprint-feedback-autoplace-plan")) {
        Write-Pass "Blueprint 0.955 feedback/autoplace plan keeps all 13 task files, five bottom lines, raw user scope, and local-only relay guardrails."
    }
    else {
        Write-FailHealth "Blueprint 0.955 feedback/autoplace plan must keep 00-12 files, five bottom lines, raw user scope, accident thread ids, and Desktop local-only relay guardrails."
    }

    if ($currentPlanIndexText -and
        $currentPlanIndexText.Contains("蓝图实机反馈与自动放置治理/00-基准.md") -and
        $currentPlanIndexText.Contains("下一唯一入口") -and
        $currentPlanIndexText.Contains("不得 pendingWorktree") -and
        $functionDocText -and
        $functionDocText.Contains("蓝图实机反馈与自动放置治理/00-基准.md") -and
        $functionDocText.Contains("禁止 fork / worktree / pendingWorktreeId") -and
        $updateIndexText -and
        $updateIndexText.Contains("0.955-蓝图实机反馈自动放置方案") -and
        $updateRecordText -and
        $updateRecordText.Contains('RuntimeVersion 推进到 `0.955-blueprint-feedback-autoplace-plan`') -and
        $updateRecordText.Contains("主项目同目录 local 新对话") -and
        $docHistoryIndexText -and
        $docHistoryIndexText.Contains("蓝图实机反馈自动放置方案") -and
        $docHistoryRecordText -and
        $docHistoryRecordText.Contains('不能再误入 `.codex\worktrees`') -and
        $auditText -and
        $auditText.Contains("Test-BlueprintFeedbackAutoplacePlanGovernance") -and
        $auditText.Contains("0.955-blueprint-feedback-autoplace-plan")) {
        Write-Pass "Blueprint 0.955 feedback/autoplace plan indices, function doc, update record, document history, and audit hook are synchronized."
    }
    else {
        Write-FailHealth "Blueprint 0.955 feedback/autoplace plan must synchronize current index, function doc, update record, document history, and audit hook."
    }

    if ($plan00Text -and
        $plan00Text.Contains('`01-事实冻结与资料矩阵`') -and
        $plan00Text.Contains("已完成") -and
        ($plan00Text.Contains('下一唯一入口为 `02-F5子菜单互斥与蓝图库整理.md`') -or
            $plan00Text.Contains('下一唯一入口为 `03-已放置列表布局与材料面板.md`') -or
            $plan00Text.Contains('下一唯一入口为 `04-手持快捷栏点击坐标与状态提示.md`') -or
            $plan00Text.Contains('下一唯一入口为 `05-投影完成进度与材料需求语义.md`') -or
            $plan00Text.Contains('下一唯一入口为 `06-移动蓝图交互联动.md`') -or
            $plan00Text.Contains('下一唯一入口为 `07-区域修改持续态联动.md`') -or
            $plan00Text.Contains('下一唯一入口为 `08-镜像一次性功能修复.md`') -or
            $plan00Text.Contains('下一唯一入口为 `09-自动放置事实复核与入口治理.md`')) -and
        $plan01Text -and
        $plan01Text.Contains("状态：已完成") -and
        $plan01Text.Contains('RuntimeVersion：`0.956-blueprint-feedback-fact-freeze`') -and
        $plan01Text.Contains("019ef1d0-d34b-7e21-861e-53ed17944d39") -and
        $plan01Text.Contains("019ef2a8-5dc5-7463-942a-fb826003955b") -and
        $plan01Text.Contains('反馈 `28`') -and
        $plan01Text.Contains("BlueprintAutoPlaceActionExecutor") -and
        $plan01Text.Contains("BlueprintReplacementRuleService") -and
        $plan01Text.Contains("小助手 / Terraria / 网页资料矩阵") -and
        $plan01Text.Contains("主项目 local 防线")) {
        Write-Pass "Blueprint feedback/autoplace stage 01 fact freeze records user mapping, accident review, references, and local-only guardrails."
    }
    else {
        Write-FailHealth "Blueprint feedback/autoplace stage 01 must mark 01 complete, advance 00 to 02, and freeze user mapping, accident review, references, autoplace/replacement facts, and local-only guardrails."
    }

    if ($currentPlanIndexText -and
        (($currentPlanIndexText.Contains("0.956-blueprint-feedback-fact-freeze") -and
                $currentPlanIndexText.Contains('下一唯一入口为 `02-F5子菜单互斥与蓝图库整理.md`')) -or
            ($currentPlanIndexText.Contains("0.957-blueprint-f5-library-submenus") -and
                $currentPlanIndexText.Contains('下一唯一入口为 `03-已放置列表布局与材料面板.md`')) -or
            ($currentPlanIndexText.Contains("0.958-blueprint-placed-list-layout") -and
                $currentPlanIndexText.Contains('下一唯一入口为 `04-手持快捷栏点击坐标与状态提示.md`')) -or
            ($currentPlanIndexText.Contains("0.959-blueprint-handheld-hit-status") -and
                $currentPlanIndexText.Contains('下一唯一入口为 `05-投影完成进度与材料需求语义.md`')) -or
            ($currentPlanIndexText.Contains("0.960-blueprint-projection-progress-material") -and
                $currentPlanIndexText.Contains('下一唯一入口为 `06-移动蓝图交互联动.md`')) -or
            ($currentPlanIndexText.Contains("0.961-blueprint-move-interaction-linkage") -and
                $currentPlanIndexText.Contains('下一唯一入口为 `07-区域修改持续态联动.md`')) -or
            ($currentPlanIndexText.Contains("0.962-blueprint-region-continuous-linkage") -and
                $currentPlanIndexText.Contains('下一唯一入口为 `08-镜像一次性功能修复.md`')) -or
            ($currentPlanIndexText.Contains("0.963-blueprint-mirror-one-shot") -and
                $currentPlanIndexText.Contains('下一唯一入口为 `09-自动放置事实复核与入口治理.md`'))) -and
        $functionDocText -and
        $functionDocText.Contains("0.956-blueprint-feedback-fact-freeze") -and
        $functionDocText.Contains("01-事实冻结与资料矩阵.md") -and
        $functionDocText.Contains("02-F5子菜单互斥与蓝图库整理.md") -and
        $updateIndexText -and
        $updateIndexText.Contains("0.956-蓝图反馈事实冻结-2606240137.md") -and
        $stage01UpdateRecordText -and
        $stage01UpdateRecordText.Contains('RuntimeVersion 推进到 `0.956-blueprint-feedback-fact-freeze`') -and
        $stage01UpdateRecordText.Contains("未生成测试包") -and
        $docHistoryIndexText -and
        $docHistoryIndexText.Contains("蓝图反馈事实冻结-2606240137.md") -and
        $stage01DocHistoryRecordText -and
        $stage01DocHistoryRecordText.Contains("蓝图实机反馈与自动放置治理/01") -and
        $auditText -and
        $auditText.Contains("0.956-blueprint-feedback-fact-freeze")) {
        Write-Pass "Blueprint feedback/autoplace stage 01 indices, function doc, update record, document history, and audit hook are synchronized."
    }
    else {
        Write-FailHealth "Blueprint feedback/autoplace stage 01 must synchronize current index, function doc, update record, document history, and audit hook for 0.956."
    }
}

function Test-BlueprintFeedbackStage02Governance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $runtimePath = Join-Path $RepoRoot "src\JueMingZ\Runtime\JueMingZRuntime.cs"
    $csprojPath = Join-Path $RepoRoot "src\JueMingZ\JueMingZ.csproj"
    $libraryStatePath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\BlueprintLibraryUiState.cs"
    $entryStatePath = Join-Path $RepoRoot "src\JueMingZ\Automation\Blueprint\BlueprintEntryState.cs"
    $actionPath = Join-Path $RepoRoot "src\JueMingZ\Input\LegacyUiActionService.Blueprint.cs"
    $mainWindowPath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyMainWindow.Blueprint.cs"
    $libraryTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintLibraryTests.cs"
    $programPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.cs"
    $auditPath = Join-Path $RepoRoot "scripts\audit-project-health.ps1"
    $planDirectory = Get-BlueprintFeedbackAutoplacePlanDirectory -RepoRoot $RepoRoot
    $plan00Path = Join-Path $planDirectory "00-基准.md"
    $plan02Path = Join-Path $planDirectory "02-F5子菜单互斥与蓝图库整理.md"
    $currentPlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "索引.md")
    $functionDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $updateIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "索引.md")
    $updateDirectory = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录")
    $docHistoryIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "索引.md")
    $docHistoryDirectory = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史")

    $updateRecordPath = ""
    if (Test-Path -LiteralPath $updateDirectory) {
        $record = Get-ChildItem -LiteralPath $updateDirectory -Filter "0.957-F5子菜单互斥蓝图库整理-*.md" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($record) {
            $updateRecordPath = $record.FullName
        }
    }

    $docHistoryRecordPath = ""
    if (Test-Path -LiteralPath $docHistoryDirectory) {
        $record = Get-ChildItem -LiteralPath $docHistoryDirectory -Filter "蓝图F5子菜单互斥蓝图库整理-*.md" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($record) {
            $docHistoryRecordPath = $record.FullName
        }
    }

    $runtimeText = Read-TextIfExists -Path $runtimePath
    $csprojText = Read-TextIfExists -Path $csprojPath
    $libraryStateText = Read-TextIfExists -Path $libraryStatePath
    $entryStateText = Read-TextIfExists -Path $entryStatePath
    $actionText = Read-TextIfExists -Path $actionPath
    $mainWindowText = Read-TextIfExists -Path $mainWindowPath
    $libraryTestText = Read-TextIfExists -Path $libraryTestPath
    $programText = Read-TextIfExists -Path $programPath
    $auditText = Read-TextIfExists -Path $auditPath
    $plan00Text = Read-TextIfExists -Path $plan00Path
    $plan02Text = Read-TextIfExists -Path $plan02Path
    $currentPlanIndexText = Read-TextIfExists -Path $currentPlanIndexPath
    $functionDocText = Read-TextIfExists -Path $functionDocPath
    $updateIndexText = Read-TextIfExists -Path $updateIndexPath
    $updateRecordText = if ([string]::IsNullOrWhiteSpace($updateRecordPath)) { $null } else { Read-TextIfExists -Path $updateRecordPath }
    $docHistoryIndexText = Read-TextIfExists -Path $docHistoryIndexPath
    $docHistoryRecordText = if ([string]::IsNullOrWhiteSpace($docHistoryRecordPath)) { $null } else { Read-TextIfExists -Path $docHistoryRecordPath }

    if (Test-BlueprintPlacementVersionMetadata -RuntimeText $runtimeText -CsprojText $csprojText -AllowedRuntimeVersions @(
            "0.957-blueprint-f5-library-submenus",
            "0.958-blueprint-placed-list-layout",
            "0.959-blueprint-handheld-hit-status",
            "0.960-blueprint-projection-progress-material")) {
        Write-Pass "Blueprint feedback/autoplace stage 02 evidence is preserved while current version metadata advances."
    }
    else {
        Write-FailHealth "Blueprint feedback/autoplace stage 02 evidence must remain valid while current RuntimeVersion and project metadata advance past 0.957."
    }

    if ($libraryStateText -and
        $libraryStateText.Contains("CloseMaterialList") -and
        $libraryStateText.Contains("BuildTemplateMaterialLines") -and
        $libraryStateText.Contains("SelectTemplateForPlacement") -and
        $libraryStateText.Contains("_isOpen = false") -and
        $libraryStateText.Contains("_expandedMaterialTemplateId = string.Empty")) {
        Write-Pass "Blueprint library state closes the submenu and material modal when placement preview starts."
    }
    else {
        Write-FailHealth "Blueprint library state must close the library/material modal on successful placement preview and expose material-modal close/build helpers."
    }

    if ($entryStateText -and
        $entryStateText.Contains("libraryOpened") -and
        $entryStateText.Contains("BlueprintEntryModes.PlacedManagement") -and
        $actionText -and
        $actionText.Contains("BlueprintLibraryUiState.CloseLibrary()") -and
        $actionText.Contains("CloseBlueprintMainMenuForBlueprintWorldInteraction") -and
        $actionText.Contains("materials-close") -and
        $actionText.Contains("CloseMaterialList") -and
        $mainWindowText -and
        $mainWindowText.Contains("BlueprintLibraryMaterialModalElementId") -and
        $mainWindowText.Contains("RegisterBlueprintLibraryMaterialModalOverlay") -and
        $mainWindowText.Contains("BuildBlueprintLibraryHeaderSummary") -and
        $mainWindowText.Contains("card-material-modal") -and
        $mainWindowText.Contains("summary-only") -and
        $mainWindowText.Contains("no-empty-gap-text") -and
        $mainWindowText.Contains("larger-card-summary") -and
        $mainWindowText.Contains("use-closes-f5") -and
        $mainWindowText.Contains("mutual-submenus") -and
        $mainWindowText.Contains("return string.Empty;") -and
        -not $mainWindowText.Contains('return "缺口：无";')) {
        Write-Pass "Blueprint stage 02 UI/actions enforce submenu mutual exclusion, F5 close-on-use, summary-only header, no empty-gap text, and material modal."
    }
    else {
        Write-FailHealth "Blueprint stage 02 UI/actions must enforce mutual submenus, close F5 when placement starts, trim summary/no-gap copy, enlarge card summary, and move materials to a modal."
    }

    if ($libraryTestText -and
        $libraryTestText.Contains("BlueprintLibraryStage02FileDialogAndMaterialContracts") -and
        $libraryTestText.Contains("BlueprintLibraryStage02MutualSubmenusAndUseCloseF5") -and
        $libraryTestText.Contains("RegisterBlueprintLibraryMaterialModalOverlayForTesting") -and
        $libraryTestText.Contains("BuildTemplateMaterialLines") -and
        $libraryTestText.Contains("materials-close") -and
        $programText -and
        $programText.Contains("blueprint library stage 02 mutual submenus and use close F5")) {
        Write-Pass "Blueprint stage 02 console tests cover material modal, submenu mutual exclusion, and close-F5-on-use contracts."
    }
    else {
        Write-FailHealth "Blueprint stage 02 must register console tests for material modal, submenu mutual exclusion, and close-F5-on-use contracts."
    }

    if ($plan02Text -and
        $plan02Text.Contains("状态：已完成") -and
        $plan02Text.Contains('RuntimeVersion：`0.957-blueprint-f5-library-submenus`') -and
        $plan02Text.Contains("BlueprintLibraryStage02MutualSubmenusAndUseCloseF5") -and
        $plan02Text.Contains('未实现 `03`') -and
        $plan00Text -and
        $plan00Text.Contains("0.957-blueprint-f5-library-submenus") -and
        ($plan00Text.Contains('下一唯一入口为 `03-已放置列表布局与材料面板.md`') -or
            ($plan00Text.Contains("0.958-blueprint-placed-list-layout") -and
                $plan00Text.Contains('下一唯一入口为 `04-手持快捷栏点击坐标与状态提示.md`')) -or
            ($plan00Text.Contains("0.959-blueprint-handheld-hit-status") -and
                $plan00Text.Contains('下一唯一入口为 `05-投影完成进度与材料需求语义.md`')) -or
            ($plan00Text.Contains("0.960-blueprint-projection-progress-material") -and
                $plan00Text.Contains('下一唯一入口为 `06-移动蓝图交互联动.md`')) -or
            ($plan00Text.Contains("0.961-blueprint-move-interaction-linkage") -and
                $plan00Text.Contains('下一唯一入口为 `07-区域修改持续态联动.md`')) -or
            ($plan00Text.Contains("0.962-blueprint-region-continuous-linkage") -and
                $plan00Text.Contains('下一唯一入口为 `08-镜像一次性功能修复.md`')) -or
            ($plan00Text.Contains("0.963-blueprint-mirror-one-shot") -and
                $plan00Text.Contains('下一唯一入口为 `09-自动放置事实复核与入口治理.md`'))) -and
        $currentPlanIndexText -and
        (($currentPlanIndexText.Contains("0.957-blueprint-f5-library-submenus") -and
                $currentPlanIndexText.Contains('下一唯一入口为 `03-已放置列表布局与材料面板.md`')) -or
            ($currentPlanIndexText.Contains("0.958-blueprint-placed-list-layout") -and
                $currentPlanIndexText.Contains('下一唯一入口为 `04-手持快捷栏点击坐标与状态提示.md`')) -or
            ($currentPlanIndexText.Contains("0.959-blueprint-handheld-hit-status") -and
                $currentPlanIndexText.Contains('下一唯一入口为 `05-投影完成进度与材料需求语义.md`')) -or
            ($currentPlanIndexText.Contains("0.960-blueprint-projection-progress-material") -and
                $currentPlanIndexText.Contains('下一唯一入口为 `06-移动蓝图交互联动.md`')) -or
            ($currentPlanIndexText.Contains("0.961-blueprint-move-interaction-linkage") -and
                $currentPlanIndexText.Contains('下一唯一入口为 `07-区域修改持续态联动.md`')) -or
            ($currentPlanIndexText.Contains("0.962-blueprint-region-continuous-linkage") -and
                $currentPlanIndexText.Contains('下一唯一入口为 `08-镜像一次性功能修复.md`')) -or
            ($currentPlanIndexText.Contains("0.963-blueprint-mirror-one-shot") -and
                $currentPlanIndexText.Contains('下一唯一入口为 `09-自动放置事实复核与入口治理.md`')))) {
        Write-Pass "Blueprint feedback/autoplace stage 02 plan files stay complete while the current plan index advances."
    }
    else {
        Write-FailHealth "Blueprint feedback/autoplace stage 02 plan files must mark 02 complete, record 0.957, and allow only the current next staged entry."
    }

    if ($functionDocText -and
        $functionDocText.Contains("0.957-blueprint-f5-library-submenus") -and
        $functionDocText.Contains("蓝图库 / 已放置列表互斥") -and
        $functionDocText.Contains("材料清单改为单独子窗口") -and
        $updateIndexText -and
        $updateIndexText.Contains("0.957-F5子菜单互斥蓝图库整理") -and
        $updateRecordText -and
        $updateRecordText.Contains('RuntimeVersion：`0.957-blueprint-f5-library-submenus`') -and
        $updateRecordText.Contains("未生成测试包") -and
        $docHistoryIndexText -and
        $docHistoryIndexText.Contains("蓝图F5子菜单互斥蓝图库整理") -and
        $docHistoryRecordText -and
        $docHistoryRecordText.Contains("02-F5子菜单互斥与蓝图库整理.md") -and
        $auditText -and
        $auditText.Contains("Test-BlueprintFeedbackStage02Governance") -and
        $auditText.Contains("0.957-blueprint-f5-library-submenus")) {
        Write-Pass "Blueprint feedback/autoplace stage 02 function doc, update record, document history, and audit hook are synchronized."
    }
    else {
        Write-FailHealth "Blueprint feedback/autoplace stage 02 must synchronize function doc, update record, document history, and audit hook."
    }
}

function Test-BlueprintFeedbackStage03Governance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $runtimePath = Join-Path $RepoRoot "src\JueMingZ\Runtime\JueMingZRuntime.cs"
    $csprojPath = Join-Path $RepoRoot "src\JueMingZ\JueMingZ.csproj"
    $mainWindowPath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyMainWindow.Blueprint.cs"
    $placedWindowPath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyMainWindow.Blueprint.Placed.cs"
    $placedStatePath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\BlueprintPlacedInstanceUiState.cs"
    $actionPath = Join-Path $RepoRoot "src\JueMingZ\Input\LegacyUiActionService.Blueprint.cs"
    $materialTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintMaterialTests.cs"
    $diagnosticsTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintDiagnosticsTests.cs"
    $programPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.cs"
    $auditPath = Join-Path $RepoRoot "scripts\audit-project-health.ps1"
    $planDirectory = Get-BlueprintFeedbackAutoplacePlanDirectory -RepoRoot $RepoRoot
    $plan00Path = Join-Path $planDirectory "00-基准.md"
    $plan03Path = Join-Path $planDirectory "03-已放置列表布局与材料面板.md"
    $currentPlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "索引.md")
    $functionDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $updateIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "索引.md")
    $updateDirectory = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录")
    $docHistoryIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "索引.md")
    $docHistoryDirectory = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史")

    $updateRecordPath = ""
    if (Test-Path -LiteralPath $updateDirectory) {
        $record = Get-ChildItem -LiteralPath $updateDirectory -Filter "0.958-已放置列表布局材料面板-*.md" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($record) {
            $updateRecordPath = $record.FullName
        }
    }

    $docHistoryRecordPath = ""
    if (Test-Path -LiteralPath $docHistoryDirectory) {
        $record = Get-ChildItem -LiteralPath $docHistoryDirectory -Filter "蓝图已放置列表布局材料面板-*.md" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($record) {
            $docHistoryRecordPath = $record.FullName
        }
    }

    $runtimeText = Read-TextIfExists -Path $runtimePath
    $csprojText = Read-TextIfExists -Path $csprojPath
    $mainWindowText = Read-TextIfExists -Path $mainWindowPath
    $placedWindowText = Read-TextIfExists -Path $placedWindowPath
    $placedStateText = Read-TextIfExists -Path $placedStatePath
    $actionText = Read-TextIfExists -Path $actionPath
    $materialTestText = Read-TextIfExists -Path $materialTestPath
    $diagnosticsTestText = Read-TextIfExists -Path $diagnosticsTestPath
    $programText = Read-TextIfExists -Path $programPath
    $auditText = Read-TextIfExists -Path $auditPath
    $plan00Text = Read-TextIfExists -Path $plan00Path
    $plan03Text = Read-TextIfExists -Path $plan03Path
    $currentPlanIndexText = Read-TextIfExists -Path $currentPlanIndexPath
    $functionDocText = Read-TextIfExists -Path $functionDocPath
    $updateIndexText = Read-TextIfExists -Path $updateIndexPath
    $updateRecordText = if ([string]::IsNullOrWhiteSpace($updateRecordPath)) { $null } else { Read-TextIfExists -Path $updateRecordPath }
    $docHistoryIndexText = Read-TextIfExists -Path $docHistoryIndexPath
    $docHistoryRecordText = if ([string]::IsNullOrWhiteSpace($docHistoryRecordPath)) { $null } else { Read-TextIfExists -Path $docHistoryRecordPath }

    if (Test-BlueprintPlacementVersionMetadata -RuntimeText $runtimeText -CsprojText $csprojText -AllowedRuntimeVersions @(
            "0.958-blueprint-placed-list-layout",
            "0.959-blueprint-handheld-hit-status",
            "0.960-blueprint-projection-progress-material")) {
        Write-Pass "Blueprint feedback/autoplace stage 03 version metadata is preserved while current version metadata advances."
    }
    else {
        Write-FailHealth "Blueprint feedback/autoplace stage 03 evidence must remain valid while current RuntimeVersion and project metadata advance past 0.958."
    }

    if ($mainWindowText -and
        $mainWindowText.Contains("stage03-title-count") -and
        $mainWindowText.Contains("stage03-header-page-back") -and
        $mainWindowText.Contains("stage03-material-total-all-lines") -and
        $mainWindowText.Contains("stage03-card-preview") -and
        $mainWindowText.Contains("stage03-library-sized-cards") -and
        $mainWindowText.Contains("read-only-name") -and
        $mainWindowText.Contains("hide-show-cancel-place") -and
        $mainWindowText.Contains("card-material-modal") -and
        $mainWindowText.Contains("BlueprintPlacedMaterialLineTextScale = 0.84f") -and
        $mainWindowText.Contains("CalculateBlueprintPlacedMaterialPanelHeight") -and
        $mainWindowText.Contains("BuildBlueprintPlacedHeaderSummary")) {
        Write-Pass "Blueprint stage 03 visual contract records title/count, header controls, all material lines, card preview, library-sized cards, read-only names, and button copy."
    }
    else {
        Write-FailHealth "Blueprint stage 03 visual contract must lock title/count, same-row header controls, material total/all-lines, card preview, library-sized cards, read-only names, and hide/show/cancel-place copy."
    }

    if ($placedWindowText -and
        $placedWindowText.Contains("GetCachedSnapshotForDraw()") -and
        $placedWindowText.Contains("Draw/layout must stay cache-only") -and
        $placedWindowText.Contains("BuildBlueprintPlacedMaterialLines(snapshot, int.MaxValue)") -and
        $placedWindowText.Contains("材料总计") -and
        $placedWindowText.Contains("DrawBlueprintTemplatePreviewGrid") -and
        $placedWindowText.Contains("取消放置") -and
        $placedWindowText.Contains("点击隐藏此蓝图") -and
        $placedWindowText.Contains("点击显示此蓝图") -and
        $placedWindowText.Contains("BuildBlueprintPlacedInstanceMaterialLines") -and
        $placedWindowText.Contains("The per-card modal lists the placed snapshot materials") -and
        $placedWindowText.Contains("BlueprintLibraryUiState.BuildTemplateMaterialLines")) {
        Write-Pass "Blueprint stage 03 placed-list draw path is cache-only, shows all materials, uses preview cards, and reads per-card modal materials from instance snapshots."
    }
    else {
        Write-FailHealth "Blueprint stage 03 placed-list draw path must stay cache-only, show all material lines, draw card previews, use stage-03 button copy, and read modal materials from TemplateSnapshot."
    }

    if ($placedStateText -and
        $placedStateText.Contains("ExpandedMaterialInstanceId") -and
        $placedStateText.Contains("ToggleMaterialList") -and
        $placedStateText.Contains("CloseMaterialList") -and
        $placedStateText.Contains("GetExpandedMaterialInstance") -and
        $actionText -and
        $actionText.Contains('string.Equals(action, "materials", StringComparison.OrdinalIgnoreCase)') -and
        $actionText.Contains("BlueprintPlacedInstanceUiState.ToggleMaterialList") -and
        $actionText.Contains('string.Equals(action, "materials-close", StringComparison.OrdinalIgnoreCase)') -and
        $actionText.Contains("BlueprintPlacedInstanceUiState.CloseMaterialList")) {
        Write-Pass "Blueprint stage 03 placed material modal actions remain UI-only state transitions."
    }
    else {
        Write-FailHealth "Blueprint stage 03 must keep per-card material modal state in BlueprintPlacedInstanceUiState and route materials/materials-close as UI-only commands."
    }

    if ($materialTestText -and
        $materialTestText.Contains("BlueprintPlacedListStage03LayoutMaterialAndCards") -and
        $materialTestText.Contains("当前世界已放置2个蓝图") -and
        $materialTestText.Contains("材料总计") -and
        $materialTestText.Contains("BuildBlueprintPlacedInstanceMaterialListTextForTesting") -and
        $diagnosticsTestText -and
        $diagnosticsTestText.Contains("BlueprintPlacedListStage03LayoutMaterialAndCards();") -and
        $programText -and
        $programText.Contains("blueprint placed list stage 03 layout material and cards")) {
        Write-Pass "Blueprint stage 03 console tests cover layout, material total/all-lines, modal text, and aggregate regression wiring."
    }
    else {
        Write-FailHealth "Blueprint stage 03 must register console tests for placed-list layout/material/card modal and include them in the aggregate regression chain."
    }

    if ($plan03Text -and
        $plan03Text.Contains("状态：已完成") -and
        $plan03Text.Contains('RuntimeVersion：`0.958-blueprint-placed-list-layout`') -and
        $plan03Text.Contains("BlueprintPlacedListStage03LayoutMaterialAndCards") -and
        $plan03Text.Contains("不生成测试包") -and
        $plan03Text.Contains('不修快捷栏 hit-test；留给 `04`') -and
        $plan00Text -and
        (($plan00Text.Contains("0.958-blueprint-placed-list-layout") -and
                $plan00Text.Contains('下一唯一入口为 `04-手持快捷栏点击坐标与状态提示.md`')) -or
            ($plan00Text.Contains("0.959-blueprint-handheld-hit-status") -and
                $plan00Text.Contains('下一唯一入口为 `05-投影完成进度与材料需求语义.md`')) -or
            ($plan00Text.Contains("0.960-blueprint-projection-progress-material") -and
                $plan00Text.Contains('下一唯一入口为 `06-移动蓝图交互联动.md`')) -or
            ($plan00Text.Contains("0.961-blueprint-move-interaction-linkage") -and
                $plan00Text.Contains('下一唯一入口为 `07-区域修改持续态联动.md`')) -or
            ($plan00Text.Contains("0.962-blueprint-region-continuous-linkage") -and
                $plan00Text.Contains('下一唯一入口为 `08-镜像一次性功能修复.md`')) -or
            ($plan00Text.Contains("0.963-blueprint-mirror-one-shot") -and
                $plan00Text.Contains('下一唯一入口为 `09-自动放置事实复核与入口治理.md`'))) -and
        $currentPlanIndexText -and
        (($currentPlanIndexText.Contains("0.958-blueprint-placed-list-layout") -and
                $currentPlanIndexText.Contains('下一唯一入口为 `04-手持快捷栏点击坐标与状态提示.md`')) -or
            ($currentPlanIndexText.Contains("0.959-blueprint-handheld-hit-status") -and
                $currentPlanIndexText.Contains('下一唯一入口为 `05-投影完成进度与材料需求语义.md`')) -or
            ($currentPlanIndexText.Contains("0.960-blueprint-projection-progress-material") -and
                $currentPlanIndexText.Contains('下一唯一入口为 `06-移动蓝图交互联动.md`')) -or
            ($currentPlanIndexText.Contains("0.961-blueprint-move-interaction-linkage") -and
                $currentPlanIndexText.Contains('下一唯一入口为 `07-区域修改持续态联动.md`')) -or
            ($currentPlanIndexText.Contains("0.962-blueprint-region-continuous-linkage") -and
                $currentPlanIndexText.Contains('下一唯一入口为 `08-镜像一次性功能修复.md`')) -or
            ($currentPlanIndexText.Contains("0.963-blueprint-mirror-one-shot") -and
                $currentPlanIndexText.Contains('下一唯一入口为 `09-自动放置事实复核与入口治理.md`')))) {
        Write-Pass "Blueprint feedback/autoplace stage 03 plan files and current plan index advance through the current staged entry."
    }
    else {
        Write-FailHealth "Blueprint feedback/autoplace stage 03 plan files must mark 03 complete, record 0.958, and advance the only next entry to 04."
    }

    if ($functionDocText -and
        $functionDocText.Contains("0.958-blueprint-placed-list-layout") -and
        $functionDocText.Contains("当前世界已放置xx个蓝图") -and
        $functionDocText.Contains("单独子窗口") -and
        $updateIndexText -and
        $updateIndexText.Contains("0.958-已放置列表布局材料面板") -and
        $updateRecordText -and
        $updateRecordText.Contains('RuntimeVersion：`0.958-blueprint-placed-list-layout`') -and
        $updateRecordText.Contains('本轮未实现 `04-手持快捷栏点击坐标与状态提示.md`') -and
        $docHistoryIndexText -and
        $docHistoryIndexText.Contains("蓝图已放置列表布局材料面板") -and
        $docHistoryRecordText -and
        $docHistoryRecordText.Contains("03-已放置列表布局与材料面板.md") -and
        $auditText -and
        $auditText.Contains("Test-BlueprintFeedbackStage03Governance") -and
        $auditText.Contains("0.958-blueprint-placed-list-layout")) {
        Write-Pass "Blueprint feedback/autoplace stage 03 function doc, update record, document history, and audit hook are synchronized."
    }
    else {
        Write-FailHealth "Blueprint feedback/autoplace stage 03 must synchronize function doc, update record, document history, and audit hook."
    }
}

function Test-BlueprintFeedbackStage04HandheldStatusGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $runtimePath = Join-Path $RepoRoot "src\JueMingZ\Runtime\JueMingZRuntime.cs"
    $csprojPath = Join-Path $RepoRoot "src\JueMingZ\JueMingZ.csproj"
    $readerPath = Join-Path $RepoRoot "src\JueMingZ\UI\DiagnosticMouseStateReader.cs"
    $overlayPath = Join-Path $RepoRoot "src\JueMingZ\UI\BlueprintHandheldActionBarOverlay.cs"
    $handheldTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintHandheldActionBarTests.cs"
    $aggregateTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintHandheldUiClickOwnershipTests.cs"
    $programPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.cs"
    $auditPath = Join-Path $RepoRoot "scripts\audit-project-health.ps1"
    $planDirectory = Get-BlueprintFeedbackAutoplacePlanDirectory -RepoRoot $RepoRoot
    $plan00Path = Join-Path $planDirectory "00-基准.md"
    $plan04Path = Join-Path $planDirectory "04-手持快捷栏点击坐标与状态提示.md"
    $currentPlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "索引.md")
    $functionDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $diagnosticsDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "AI诊断日志说明.md")
    $updateIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "索引.md")
    $updateDirectory = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录")
    $docHistoryIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "索引.md")
    $docHistoryDirectory = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史")
    $experienceDirectory = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("AI经验笔记")

    $updateRecordPath = ""
    if (Test-Path -LiteralPath $updateDirectory) {
        $record = Get-ChildItem -LiteralPath $updateDirectory -Filter "0.959-手持快捷栏点击状态提示-*.md" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($record) {
            $updateRecordPath = $record.FullName
        }
    }

    $docHistoryRecordPath = ""
    if (Test-Path -LiteralPath $docHistoryDirectory) {
        $record = Get-ChildItem -LiteralPath $docHistoryDirectory -Filter "蓝图手持快捷栏点击状态提示-*.md" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($record) {
            $docHistoryRecordPath = $record.FullName
        }
    }

    $experienceNotePath = ""
    if (Test-Path -LiteralPath $experienceDirectory) {
        $record = Get-ChildItem -LiteralPath $experienceDirectory -Recurse -Filter "蓝图手持栏前后置鼠标读取缓存分槽-*.md" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($record) {
            $experienceNotePath = $record.FullName
        }
    }

    $runtimeText = Read-TextIfExists -Path $runtimePath
    $csprojText = Read-TextIfExists -Path $csprojPath
    $readerText = Read-TextIfExists -Path $readerPath
    $overlayText = Read-TextIfExists -Path $overlayPath
    $handheldTestText = Read-TextIfExists -Path $handheldTestPath
    $aggregateTestText = Read-TextIfExists -Path $aggregateTestPath
    $programText = Read-TextIfExists -Path $programPath
    $auditText = Read-TextIfExists -Path $auditPath
    $plan00Text = Read-TextIfExists -Path $plan00Path
    $plan04Text = Read-TextIfExists -Path $plan04Path
    $currentPlanIndexText = Read-TextIfExists -Path $currentPlanIndexPath
    $functionDocText = Read-TextIfExists -Path $functionDocPath
    $diagnosticsDocText = Read-TextIfExists -Path $diagnosticsDocPath
    $updateIndexText = Read-TextIfExists -Path $updateIndexPath
    $updateRecordText = if ([string]::IsNullOrWhiteSpace($updateRecordPath)) { $null } else { Read-TextIfExists -Path $updateRecordPath }
    $docHistoryIndexText = Read-TextIfExists -Path $docHistoryIndexPath
    $docHistoryRecordText = if ([string]::IsNullOrWhiteSpace($docHistoryRecordPath)) { $null } else { Read-TextIfExists -Path $docHistoryRecordPath }
    $experienceNoteText = if ([string]::IsNullOrWhiteSpace($experienceNotePath)) { $null } else { Read-TextIfExists -Path $experienceNotePath }

    if (Test-BlueprintPlacementVersionMetadata -RuntimeText $runtimeText -CsprojText $csprojText -AllowedRuntimeVersions @(
            "0.959-blueprint-handheld-hit-status",
            "0.960-blueprint-projection-progress-material")) {
        Write-Pass "Blueprint feedback/autoplace stage 04 version metadata is synchronized to 0.959-blueprint-handheld-hit-status."
    }
    else {
        Write-FailHealth "Blueprint feedback/autoplace stage 04 must synchronize RuntimeVersion and project metadata to 0.959-blueprint-handheld-hit-status."
    }

    if ($readerText -and
        $readerText.Contains("ReadForBlueprintHandheldActionBarOverlayAfterPlayerInput") -and
        $readerText.Contains("BlueprintHandheldActionBarOverlayAfterPlayerInput") -and
        $readerText.Contains("_cachedBlueprintHandheldActionBarAfterPlayerInputReadState") -and
        $readerText.Contains("BlueprintHandheldOverlayGateBypass")) {
        Write-Pass "Blueprint handheld stage 04 mouse reader keeps prefix and after-PlayerInput cache slots separate."
    }
    else {
        Write-FailHealth "Blueprint handheld stage 04 must add a separate after-PlayerInput diagnostic mouse cache slot for the handheld action bar."
    }

    if ($overlayText -and
        $overlayText.Contains("ReadForBlueprintHandheldActionBarOverlayAfterPlayerInput") -and
        $overlayText.Contains("NoticeTextScale = 0.78f") -and
        $overlayText.Contains("ResolveActiveStatusNotice") -and
        $overlayText.Contains("BlueprintPlacedInstanceTransformState.GetSnapshot") -and
        $overlayText.Contains("BlueprintEraseRegionState.GetSnapshot") -and
        $overlayText.Contains("BlueprintCreationMaskState.GetSnapshot") -and
        $overlayText.Contains("stage04-after-player-input-cache") -and
        $overlayText.Contains("stage04-active-status-notice") -and
        -not $overlayText.Contains("return frame == null ? string.Empty : frame.LastNotice;")) {
        Write-Pass "Blueprint handheld stage 04 overlay reads the fresh postfix mouse slot and separates hover tooltips from active status notices."
    }
    else {
        Write-FailHealth "Blueprint handheld stage 04 overlay must use the postfix mouse reader, enlarged notice text, and active-state notice source without persisting one-shot LastNotice."
    }

    if ($handheldTestText -and $aggregateTestText -and $programText -and
        $handheldTestText.Contains("BlueprintHandheldActionBarStage04ButtonHitBoundsMatchVisibleRects") -and
        $handheldTestText.Contains("BlueprintHandheldActionBarStage04NoticeTimingAndScale") -and
        $handheldTestText.Contains("BlueprintHandheldActionBarStage04MouseReaderCachesPrefixAndPostfixSeparately") -and
        $aggregateTestText.Contains("BlueprintHandheldActionBarStage04ButtonHitBoundsMatchVisibleRects") -and
        $aggregateTestText.Contains("BlueprintHandheldActionBarStage04NoticeTimingAndScale") -and
        $aggregateTestText.Contains("BlueprintHandheldActionBarStage04MouseReaderCachesPrefixAndPostfixSeparately") -and
        $programText.Contains("blueprint handheld action bar stage 04 button hit bounds match visible rects") -and
        $programText.Contains("blueprint handheld action bar stage 04 notice timing and scale") -and
        $programText.Contains("blueprint handheld action bar stage 04 mouse reader caches prefix and postfix separately")) {
        Write-Pass "Blueprint handheld stage 04 console tests cover visual hit bounds, notice timing/scale, and mouse cache phase separation."
    }
    else {
        Write-FailHealth "Blueprint handheld stage 04 must register hit-bound, notice timing, and prefix/postfix cache regression tests in the main suite and aggregate chain."
    }

    if ($plan04Text -and
        $plan04Text.Contains("状态：已完成") -and
        $plan04Text.Contains('RuntimeVersion：`0.959-blueprint-handheld-hit-status`') -and
        $plan04Text.Contains("BlueprintHandheldActionBarStage04ButtonHitBoundsMatchVisibleRects") -and
        $plan04Text.Contains("BlueprintHandheldActionBarStage04NoticeTimingAndScale") -and
        $plan04Text.Contains("BlueprintHandheldActionBarStage04MouseReaderCachesPrefixAndPostfixSeparately") -and
        $plan04Text.Contains("Test-BlueprintFeedbackStage04HandheldStatusGovernance") -and
        $plan04Text.Contains("不生成测试包") -and
        $plan04Text.Contains('未实现 `05`') -and
        $plan00Text -and
        (($plan00Text.Contains("0.959-blueprint-handheld-hit-status") -and
                $plan00Text.Contains('下一唯一入口为 `05-投影完成进度与材料需求语义.md`')) -or
            ($plan00Text.Contains("0.960-blueprint-projection-progress-material") -and
                $plan00Text.Contains('下一唯一入口为 `06-移动蓝图交互联动.md`')) -or
            ($plan00Text.Contains("0.961-blueprint-move-interaction-linkage") -and
                $plan00Text.Contains('下一唯一入口为 `07-区域修改持续态联动.md`')) -or
            ($plan00Text.Contains("0.962-blueprint-region-continuous-linkage") -and
                $plan00Text.Contains('下一唯一入口为 `08-镜像一次性功能修复.md`')) -or
            ($plan00Text.Contains("0.963-blueprint-mirror-one-shot") -and
                $plan00Text.Contains('下一唯一入口为 `09-自动放置事实复核与入口治理.md`'))) -and
        $currentPlanIndexText -and
        (($currentPlanIndexText.Contains("0.959-blueprint-handheld-hit-status") -and
                $currentPlanIndexText.Contains('下一唯一入口为 `05-投影完成进度与材料需求语义.md`')) -or
            ($currentPlanIndexText.Contains("0.960-blueprint-projection-progress-material") -and
                $currentPlanIndexText.Contains('下一唯一入口为 `06-移动蓝图交互联动.md`')) -or
            ($currentPlanIndexText.Contains("0.961-blueprint-move-interaction-linkage") -and
                $currentPlanIndexText.Contains('下一唯一入口为 `07-区域修改持续态联动.md`')) -or
            ($currentPlanIndexText.Contains("0.962-blueprint-region-continuous-linkage") -and
                $currentPlanIndexText.Contains('下一唯一入口为 `08-镜像一次性功能修复.md`')) -or
            ($currentPlanIndexText.Contains("0.963-blueprint-mirror-one-shot") -and
                $currentPlanIndexText.Contains('下一唯一入口为 `09-自动放置事实复核与入口治理.md`')))) {
        Write-Pass "Blueprint feedback/autoplace stage 04 plan files and current plan index advance only to stage 05."
    }
    else {
        Write-FailHealth "Blueprint feedback/autoplace stage 04 plan files must mark 04 complete, record tests/audit/no-package scope, and advance the only next entry to 05."
    }

    if ($functionDocText -and
        $functionDocText.Contains("0.959-blueprint-handheld-hit-status") -and
        $functionDocText.Contains("BlueprintHandheldActionBarStage04ButtonHitBoundsMatchVisibleRects") -and
        $functionDocText.Contains("hover 功能说明") -and
        $diagnosticsDocText -and
        $diagnosticsDocText.Contains("0.959-blueprint-handheld-hit-status") -and
        $diagnosticsDocText.Contains("ReadForBlueprintHandheldActionBarOverlayAfterPlayerInput") -and
        $diagnosticsDocText.Contains("Test-BlueprintFeedbackStage04HandheldStatusGovernance") -and
        $updateIndexText -and
        $updateIndexText.Contains("0.959-手持快捷栏点击状态提示") -and
        $updateRecordText -and
        $updateRecordText.Contains('RuntimeVersion：`0.959-blueprint-handheld-hit-status`') -and
        $updateRecordText.Contains("BlueprintHandheldActionBarStage04ButtonHitBoundsMatchVisibleRects") -and
        $updateRecordText.Contains("不生成测试包") -and
        $docHistoryIndexText -and
        $docHistoryIndexText.Contains("蓝图手持快捷栏点击状态提示") -and
        $docHistoryRecordText -and
        $docHistoryRecordText.Contains("04-手持快捷栏点击坐标与状态提示.md") -and
        $auditText -and
        $auditText.Contains("Test-BlueprintFeedbackStage04HandheldStatusGovernance") -and
        $auditText.Contains("0.959-blueprint-handheld-hit-status")) {
        Write-Pass "Blueprint feedback/autoplace stage 04 function doc, diagnostics doc, update record, document history, and audit hook are synchronized."
    }
    else {
        Write-FailHealth "Blueprint feedback/autoplace stage 04 must synchronize function doc, diagnostics doc, update record, document history, and audit hook."
    }

    if ($experienceNoteText -and
        $experienceNoteText.Contains("prefix") -and
        $experienceNoteText.Contains("after-PlayerInput") -and
        $experienceNoteText.Contains("独立缓存")) {
        Write-Pass "Blueprint feedback/autoplace stage 04 adds the prefix/postfix mouse cache experience note."
    }
    else {
        Write-FailHealth "Blueprint feedback/autoplace stage 04 should record the prefix/postfix mouse cache split as an AI experience note."
    }
}

function Test-BlueprintFeedbackStage05ProgressMaterialGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $runtimePath = Join-Path $RepoRoot "src\JueMingZ\Runtime\JueMingZRuntime.cs"
    $csprojPath = Join-Path $RepoRoot "src\JueMingZ\JueMingZ.csproj"
    $modelsPath = Join-Path $RepoRoot "src\JueMingZ\Automation\Blueprint\BlueprintModels.cs"
    $storePath = Join-Path $RepoRoot "src\JueMingZ\Automation\Blueprint\BlueprintWorldInstanceStore.cs"
    $projectionPath = Join-Path $RepoRoot "src\JueMingZ\Automation\Blueprint\BlueprintProjectionService.cs"
    $materialPath = Join-Path $RepoRoot "src\JueMingZ\Automation\Blueprint\BlueprintMaterialService.cs"
    $overlayPath = Join-Path $RepoRoot "src\JueMingZ\UI\BlueprintProjectionOverlay.cs"
    $rendererPath = Join-Path $RepoRoot "src\JueMingZ\UI\BlueprintProjectionGhostRenderer.cs"
    $snapshotPath = Join-Path $RepoRoot "src\JueMingZ\Diagnostics\DiagnosticSnapshot.cs"
    $snapshotWriterPath = Join-Path $RepoRoot "src\JueMingZ\Diagnostics\DiagnosticSnapshotWriter.Json.cs"
    $snapshotBuilderPath = Join-Path $RepoRoot "src\JueMingZ\Runtime\Diagnostics\RuntimeDiagnosticSnapshotBuilder.Blueprint.cs"
    $projectionTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintProjectionTests.cs"
    $materialTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintMaterialTests.cs"
    $storageTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintStorageTests.cs"
    $programPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.cs"
    $auditPath = Join-Path $RepoRoot "scripts\audit-project-health.ps1"
    $planDirectory = Get-BlueprintFeedbackAutoplacePlanDirectory -RepoRoot $RepoRoot
    $plan00Path = Join-Path $planDirectory "00-基准.md"
    $plan05Path = Join-Path $planDirectory "05-投影完成进度与材料需求语义.md"
    $currentPlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "索引.md")
    $functionDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $diagnosticsDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "AI诊断日志说明.md")
    $updateIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "索引.md")
    $updateDirectory = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录")
    $docHistoryIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "索引.md")
    $docHistoryDirectory = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史")

    $updateRecordPath = ""
    if (Test-Path -LiteralPath $updateDirectory) {
        $record = Get-ChildItem -LiteralPath $updateDirectory -Filter "0.960-投影完成进度材料语义-*.md" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($record) {
            $updateRecordPath = $record.FullName
        }
    }

    $docHistoryRecordPath = ""
    if (Test-Path -LiteralPath $docHistoryDirectory) {
        $record = Get-ChildItem -LiteralPath $docHistoryDirectory -Filter "蓝图投影完成进度材料语义-*.md" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($record) {
            $docHistoryRecordPath = $record.FullName
        }
    }

    $runtimeText = Read-TextIfExists -Path $runtimePath
    $csprojText = Read-TextIfExists -Path $csprojPath
    $modelsText = Read-TextIfExists -Path $modelsPath
    $storeText = Read-TextIfExists -Path $storePath
    $projectionText = Read-TextIfExists -Path $projectionPath
    $materialText = Read-TextIfExists -Path $materialPath
    $overlayText = Read-TextIfExists -Path $overlayPath
    $rendererText = Read-TextIfExists -Path $rendererPath
    $snapshotText = Read-TextIfExists -Path $snapshotPath
    $snapshotWriterText = Read-TextIfExists -Path $snapshotWriterPath
    $snapshotBuilderText = Read-TextIfExists -Path $snapshotBuilderPath
    $projectionTestText = Read-TextIfExists -Path $projectionTestPath
    $materialTestText = Read-TextIfExists -Path $materialTestPath
    $storageTestText = Read-TextIfExists -Path $storageTestPath
    $programText = Read-TextIfExists -Path $programPath
    $auditText = Read-TextIfExists -Path $auditPath
    $plan00Text = Read-TextIfExists -Path $plan00Path
    $plan05Text = Read-TextIfExists -Path $plan05Path
    $currentPlanIndexText = Read-TextIfExists -Path $currentPlanIndexPath
    $functionDocText = Read-TextIfExists -Path $functionDocPath
    $diagnosticsDocText = Read-TextIfExists -Path $diagnosticsDocPath
    $updateIndexText = Read-TextIfExists -Path $updateIndexPath
    $updateRecordText = if ([string]::IsNullOrWhiteSpace($updateRecordPath)) { $null } else { Read-TextIfExists -Path $updateRecordPath }
    $docHistoryIndexText = Read-TextIfExists -Path $docHistoryIndexPath
    $docHistoryRecordText = if ([string]::IsNullOrWhiteSpace($docHistoryRecordPath)) { $null } else { Read-TextIfExists -Path $docHistoryRecordPath }

    if (Test-BlueprintPlacementVersionMetadata -RuntimeText $runtimeText -CsprojText $csprojText -AllowedRuntimeVersions @("0.960-blueprint-projection-progress-material")) {
        Write-Pass "Blueprint feedback/autoplace stage 05 version metadata is synchronized to 0.960-blueprint-projection-progress-material."
    }
    else {
        Write-FailHealth "Blueprint feedback/autoplace stage 05 must synchronize RuntimeVersion and project metadata to 0.960-blueprint-projection-progress-material."
    }

    if ($modelsText -and $storeText -and
        $modelsText.Contains("BlueprintCompletedLayerRecord") -and
        $modelsText.Contains("CompletedLayers") -and
        $storeText.Contains("NormalizeCompletedLayers") -and
        $storeText.Contains("CompletedLayers = NormalizeCompletedLayers")) {
        Write-Pass "Blueprint stage 05 persists completed projection layers on placed instances and normalizes them on load/save."
    }
    else {
        Write-FailHealth "Blueprint stage 05 must add instance-local CompletedLayers storage with normalization."
    }

    if ($projectionText -and
        $projectionText.Contains("BlueprintProjectionLayerStatuses.Completed") -and
        $projectionText.Contains("CompletedLayerCount") -and
        $projectionText.Contains("PersistCompletedProgressIfNeeded") -and
        $projectionText.Contains("AppendCompletedLayerSignature") -and
        $projectionText.Contains("BuildCompletionKey") -and
        $projectionText.Contains("Completion progress is an instance-local promise")) {
        Write-Pass "Blueprint stage 05 projection marks completed cells by instance-local progress, signs them, and reports completed counts."
    }
    else {
        Write-FailHealth "Blueprint stage 05 projection must persist fulfilled progress, return completed status on later refreshes, and include completion in signature/counts."
    }

    if ($materialText -and
        $materialText.Contains("BlueprintProjectionLayerStatuses.Completed") -and
        $materialText.Contains("SkippedFulfilledLayerCount++") -and
        $materialText.Contains("projection.CompletedLayerCount")) {
        Write-Pass "Blueprint stage 05 material statistics subtract completed progress from material demand."
    }
    else {
        Write-FailHealth "Blueprint stage 05 materials must skip completed projection layers and include completed count in the material signature."
    }

    if ($overlayText -and $rendererText -and
        $overlayText.Contains("completed-progress") -and
        $overlayText.Contains("no-cell-border") -and
        $overlayText.Contains("ResolveProjectionBorderAlphaForTesting") -and
        $rendererText.Contains("BlueprintProjectionLayerStatuses.Completed") -and
        $rendererText.Contains("borderAlpha > 0")) {
        Write-Pass "Blueprint stage 05 overlay keeps color masks, hides completed cells, and removes per-cell borders."
    }
    else {
        Write-FailHealth "Blueprint stage 05 overlay must expose completed/no-border contracts and avoid drawing completed cells or per-cell borders."
    }

    if ($snapshotText -and $snapshotWriterText -and $snapshotBuilderText -and
        $snapshotText.Contains("BlueprintProjectionCompletedLayerCount") -and
        $snapshotWriterText.Contains('"BlueprintProjectionCompletedLayerCount"') -and
        $snapshotBuilderText.Contains("projection.CompletedLayerCount")) {
        Write-Pass "Blueprint stage 05 diagnostics expose BlueprintProjectionCompletedLayerCount in runtime snapshots."
    }
    else {
        Write-FailHealth "Blueprint stage 05 diagnostics must expose BlueprintProjectionCompletedLayerCount through the snapshot model, writer, and builder."
    }

    if ($projectionTestText -and $materialTestText -and $storageTestText -and $programText -and
        $projectionTestText.Contains("BlueprintProjectionStage05CompletedProgressPersistsAndSkipsDugCells") -and
        $materialTestText.Contains("BlueprintMaterialsStage05SubtractCompletedProgressFromDemand") -and
        $storageTestText.Contains("BlueprintCompletedLayerRecord") -and
        $programText.Contains("blueprint projection stage 05 completed progress persists and skips dug cells") -and
        $programText.Contains("blueprint materials stage 05 subtract completed progress from demand")) {
        Write-Pass "Blueprint stage 05 console tests cover completed progress persistence, dug-cell hiding, material deduction, and storage roundtrip."
    }
    else {
        Write-FailHealth "Blueprint stage 05 must register projection/material/storage regression tests for completed progress and material deduction."
    }

    if ($plan05Text -and
        $plan05Text.Contains("状态：已完成") -and
        $plan05Text.Contains('RuntimeVersion：`0.960-blueprint-projection-progress-material`') -and
        $plan05Text.Contains("BlueprintProjectionStage05CompletedProgressPersistsAndSkipsDugCells") -and
        $plan05Text.Contains("BlueprintMaterialsStage05SubtractCompletedProgressFromDemand") -and
        $plan05Text.Contains("Test-BlueprintFeedbackStage05ProgressMaterialGovernance") -and
        $plan05Text.Contains("不生成测试包") -and
        $plan05Text.Contains('未实现 `06`') -and
        $plan00Text -and
        $plan00Text.Contains("0.960-blueprint-projection-progress-material") -and
        (($plan00Text.Contains('下一唯一入口为 `06-移动蓝图交互联动.md`') -or
                ($plan00Text.Contains("0.961-blueprint-move-interaction-linkage") -and
                    $plan00Text.Contains('下一唯一入口为 `07-区域修改持续态联动.md`')) -or
                ($plan00Text.Contains("0.962-blueprint-region-continuous-linkage") -and
                    $plan00Text.Contains('下一唯一入口为 `08-镜像一次性功能修复.md`')) -or
                ($plan00Text.Contains("0.963-blueprint-mirror-one-shot") -and
                    $plan00Text.Contains('下一唯一入口为 `09-自动放置事实复核与入口治理.md`')))) -and
        $currentPlanIndexText -and
        $currentPlanIndexText.Contains("0.960-blueprint-projection-progress-material") -and
        (($currentPlanIndexText.Contains('下一唯一入口为 `06-移动蓝图交互联动.md`') -or
                ($currentPlanIndexText.Contains("0.961-blueprint-move-interaction-linkage") -and
                    $currentPlanIndexText.Contains('下一唯一入口为 `07-区域修改持续态联动.md`')) -or
                ($currentPlanIndexText.Contains("0.962-blueprint-region-continuous-linkage") -and
                    $currentPlanIndexText.Contains('下一唯一入口为 `08-镜像一次性功能修复.md`')) -or
                ($currentPlanIndexText.Contains("0.963-blueprint-mirror-one-shot") -and
                    $currentPlanIndexText.Contains('下一唯一入口为 `09-自动放置事实复核与入口治理.md`'))))) {
        Write-Pass "Blueprint feedback/autoplace stage 05 plan files and current plan index advance only to stage 06."
    }
    else {
        Write-FailHealth "Blueprint feedback/autoplace stage 05 plan files must mark 05 complete, record tests/audit/no-package scope, and advance the only next entry to 06."
    }

    if ($functionDocText -and
        $functionDocText.Contains("0.960-blueprint-projection-progress-material") -and
        $functionDocText.Contains("CompletedLayers") -and
        $functionDocText.Contains("no-cell-border") -and
        $diagnosticsDocText -and
        $diagnosticsDocText.Contains("0.960-blueprint-projection-progress-material") -and
        $diagnosticsDocText.Contains("BlueprintProjectionCompletedLayerCount") -and
        $updateIndexText -and
        $updateIndexText.Contains("0.960-投影完成进度材料语义") -and
        $updateRecordText -and
        $updateRecordText.Contains('RuntimeVersion：`0.960-blueprint-projection-progress-material`') -and
        $updateRecordText.Contains("BlueprintProjectionStage05CompletedProgressPersistsAndSkipsDugCells") -and
        $updateRecordText.Contains("不生成测试包") -and
        $docHistoryIndexText -and
        $docHistoryIndexText.Contains("蓝图投影完成进度材料语义") -and
        $docHistoryRecordText -and
        $docHistoryRecordText.Contains("05-投影完成进度与材料需求语义.md")) {
        Write-Pass "Blueprint feedback/autoplace stage 05 function doc, diagnostics doc, update record, and document history are synchronized."
    }
    else {
        Write-FailHealth "Blueprint feedback/autoplace stage 05 must synchronize function doc, diagnostics doc, update record, and document-change history."
    }

    if ($auditText -and
        $auditText.Contains("Test-BlueprintFeedbackStage05ProgressMaterialGovernance") -and
        $auditText.Contains("0.960-blueprint-projection-progress-material") -and
        $auditText.Contains("BlueprintProjectionCompletedLayerCount")) {
        Write-Pass "Blueprint scoped health audit includes the stage 05 completed-progress/material governance check."
    }
    else {
        Write-FailHealth "Blueprint scoped health audit must include Test-BlueprintFeedbackStage05ProgressMaterialGovernance and completed-progress diagnostics anchors."
    }
}

function Test-BlueprintFeedbackStage06MoveInteractionGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $runtimePath = Join-Path $RepoRoot "src\JueMingZ\Runtime\JueMingZRuntime.cs"
    $csprojPath = Join-Path $RepoRoot "src\JueMingZ\JueMingZ.csproj"
    $transformPath = Join-Path $RepoRoot "src\JueMingZ\Automation\Blueprint\BlueprintPlacedInstanceTransformState.cs"
    $projectionOverlayPath = Join-Path $RepoRoot "src\JueMingZ\UI\BlueprintProjectionOverlay.cs"
    $handheldStatePath = Join-Path $RepoRoot "src\JueMingZ\Automation\Blueprint\BlueprintHandheldActionBarState.cs"
    $mainWindowPath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyMainWindow.Blueprint.cs"
    $mainWindowHotkeyPath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyMainWindow.Blueprint.Hotkey.cs"
    $actionPath = Join-Path $RepoRoot "src\JueMingZ\Input\LegacyUiActionService.Blueprint.cs"
    $hotkeyServicePath = Join-Path $RepoRoot "src\JueMingZ\Input\BlueprintEntryHotkeyService.cs"
    $featureIdsPath = Join-Path $RepoRoot "src\JueMingZ\Common\FeatureIds.cs"
    $conflictPath = Join-Path $RepoRoot "src\JueMingZ\Config\FeatureToggleHotkeyConflictRegistry.cs"
    $placedTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintPlacedInstanceTests.cs"
    $entryTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintEntryTests.cs"
    $handheldTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintHandheldActionBarTests.cs"
    $programPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.cs"
    $auditPath = Join-Path $RepoRoot "scripts\audit-project-health.ps1"
    $planDirectory = Get-BlueprintFeedbackAutoplacePlanDirectory -RepoRoot $RepoRoot
    $plan00Path = Join-Path $planDirectory "00-基准.md"
    $plan06Path = Join-Path $planDirectory "06-移动蓝图交互联动.md"
    $currentPlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "索引.md")
    $functionDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $diagnosticsDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "AI诊断日志说明.md")
    $updateIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "索引.md")
    $updateDirectory = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录")
    $docHistoryIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "索引.md")
    $docHistoryDirectory = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史")

    $updateRecordPath = ""
    if (Test-Path -LiteralPath $updateDirectory) {
        $record = Get-ChildItem -LiteralPath $updateDirectory -Filter "0.961-移动蓝图交互联动-*.md" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($record) {
            $updateRecordPath = $record.FullName
        }
    }

    $docHistoryRecordPath = ""
    if (Test-Path -LiteralPath $docHistoryDirectory) {
        $record = Get-ChildItem -LiteralPath $docHistoryDirectory -Filter "蓝图移动蓝图交互联动-*.md" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($record) {
            $docHistoryRecordPath = $record.FullName
        }
    }

    $runtimeText = Read-TextIfExists -Path $runtimePath
    $csprojText = Read-TextIfExists -Path $csprojPath
    $transformText = Read-TextIfExists -Path $transformPath
    $projectionOverlayText = Read-TextIfExists -Path $projectionOverlayPath
    $handheldStateText = Read-TextIfExists -Path $handheldStatePath
    $mainWindowText = Read-TextIfExists -Path $mainWindowPath
    $mainWindowHotkeyText = Read-TextIfExists -Path $mainWindowHotkeyPath
    $actionText = Read-TextIfExists -Path $actionPath
    $hotkeyServiceText = Read-TextIfExists -Path $hotkeyServicePath
    $featureIdsText = Read-TextIfExists -Path $featureIdsPath
    $conflictText = Read-TextIfExists -Path $conflictPath
    $placedTestText = Read-TextIfExists -Path $placedTestPath
    $entryTestText = Read-TextIfExists -Path $entryTestPath
    $handheldTestText = Read-TextIfExists -Path $handheldTestPath
    $programText = Read-TextIfExists -Path $programPath
    $auditText = Read-TextIfExists -Path $auditPath
    $plan00Text = Read-TextIfExists -Path $plan00Path
    $plan06Text = Read-TextIfExists -Path $plan06Path
    $currentPlanIndexText = Read-TextIfExists -Path $currentPlanIndexPath
    $functionDocText = Read-TextIfExists -Path $functionDocPath
    $diagnosticsDocText = Read-TextIfExists -Path $diagnosticsDocPath
    $updateIndexText = Read-TextIfExists -Path $updateIndexPath
    $updateRecordText = if ([string]::IsNullOrWhiteSpace($updateRecordPath)) { $null } else { Read-TextIfExists -Path $updateRecordPath }
    $docHistoryIndexText = Read-TextIfExists -Path $docHistoryIndexPath
    $docHistoryRecordText = if ([string]::IsNullOrWhiteSpace($docHistoryRecordPath)) { $null } else { Read-TextIfExists -Path $docHistoryRecordPath }

    if (Test-BlueprintPlacementVersionMetadata -RuntimeText $runtimeText -CsprojText $csprojText -AllowedRuntimeVersions @("0.961-blueprint-move-interaction-linkage")) {
        Write-Pass "Blueprint feedback/autoplace stage 06 version metadata is synchronized to 0.961-blueprint-move-interaction-linkage."
    }
    else {
        Write-FailHealth "Blueprint feedback/autoplace stage 06 must synchronize RuntimeVersion and project metadata to 0.961-blueprint-move-interaction-linkage."
    }

    if ($transformText -and
        $transformText.Contains("GetFloatingProjectionForDraw") -and
        $transformText.Contains("moveFloatingPreview") -and
        $transformText.Contains("UpdateMoveFloatingPreviewLocked") -and
        $transformText.Contains("HasMoveBlockingProgress") -and
        $transformText.Contains("CompletedLayers") -and
        $transformText.Contains("moveBlockedByPlacedProgress") -and
        $transformText.Contains("Stage 05 persists fulfilled layers into CompletedLayers")) {
        Write-Pass "Blueprint stage 06 transform state owns floating move preview and completed-progress fail-closed move gating."
    }
    else {
        Write-FailHealth "Blueprint stage 06 transform state must expose floating move preview and block moving instances with CompletedLayers or auto-placement progress."
    }

    if ($projectionOverlayText -and
        $projectionOverlayText.Contains("move-floating-follow-preview") -and
        $projectionOverlayText.Contains("BlueprintPlacedInstanceTransformState.GetFloatingProjectionForDraw") -and
        $projectionOverlayText.Contains("DrawProjection(spriteBatch, floating)")) {
        Write-Pass "Blueprint stage 06 projection overlay draws the move floating preview from cached transform state only."
    }
    else {
        Write-FailHealth "Blueprint stage 06 projection overlay must draw move-floating-follow-preview without loading world-instance storage in draw."
    }

    if ($handheldStateText -and $mainWindowText -and $mainWindowHotkeyText -and $actionText -and $hotkeyServiceText -and $featureIdsText -and $conflictText -and
        $handheldStateText.Contains("MoveCancelButtonLabel") -and
        $handheldStateText.Contains("MoveButtonTooltip") -and
        ($mainWindowText.Contains("BlueprintActionShortcutRowCount = 4") -or $mainWindowText.Contains("BlueprintActionShortcutRowCount = 5") -or $mainWindowText.Contains("BlueprintActionShortcutRowCount = 6")) -and
        $mainWindowText.Contains("FeatureIds.BlueprintMoveAction") -and
        $mainWindowText.Contains("BlueprintEntryCommands.StartMove") -and
        $mainWindowText.Contains("move-toggle-cancel-label") -and
        $mainWindowHotkeyText.Contains("FeatureIds.BlueprintMoveAction") -and
        $actionText.Contains("StartOrCancelBlueprintMove") -and
        $actionText.Contains("CloseBlueprintMainMenuForBlueprintWorldInteraction(moveStarted") -and
        $hotkeyServiceText.Contains("FeatureIds.BlueprintMoveAction") -and
        $hotkeyServiceText.Contains("BlueprintEntryCommands.StartMove") -and
        $hotkeyServiceText.Contains("FromTransform") -and
        $featureIdsText.Contains('BlueprintMoveAction = "blueprint.move"') -and
        $conflictText.Contains("蓝图移动快捷键")) {
        Write-Pass "Blueprint stage 06 wires F5, handheld, and action hotkey move entry through shared transform state."
    }
    else {
        Write-FailHealth "Blueprint stage 06 must wire blueprint.move, F5 row, handheld cancel label, hotkey capture/conflict, and shared StartOrCancelBlueprintMove routing."
    }

    if ($placedTestText -and $entryTestText -and $handheldTestText -and $programText -and
        $placedTestText.Contains("GetFloatingProjectionForTesting") -and
        $placedTestText.Contains("BlueprintPlacedInstanceMoveBlocksCompletedProgressAndKeepsOriginalPosition") -and
        $entryTestText.Contains("BlueprintMoveActionShortcutAndHotkeyShareTransformState") -and
        $entryTestText.Contains("FeatureIds.BlueprintMoveAction") -and
        $handheldTestText.Contains("取消移动") -and
        $programText.Contains("blueprint move action shortcut and hotkey share transform state") -and
        $programText.Contains("blueprint placed instance move blocks completed progress and keeps original position")) {
        Write-Pass "Blueprint stage 06 console tests cover floating preview, completed-progress gate, F5 move row, hotkey, and handheld cancel label."
    }
    else {
        Write-FailHealth "Blueprint stage 06 must register console tests for move floating preview, completed-progress gate, F5/hotkey shared state, and handheld cancel label."
    }

    if ($plan06Text -and
        $plan06Text.Contains("状态：已完成") -and
        $plan06Text.Contains('RuntimeVersion：`0.961-blueprint-move-interaction-linkage`') -and
        $plan06Text.Contains("BlueprintMoveActionShortcutAndHotkeyShareTransformState") -and
        $plan06Text.Contains("BlueprintPlacedInstanceMoveBlocksCompletedProgressAndKeepsOriginalPosition") -and
        $plan06Text.Contains("Test-BlueprintFeedbackStage06MoveInteractionGovernance") -and
        $plan06Text.Contains("不生成测试包") -and
        $plan06Text.Contains('未实现 `07`') -and
        $plan00Text -and
        (($plan00Text.Contains("0.961-blueprint-move-interaction-linkage") -and
         $plan00Text.Contains('下一唯一入口为 `07-')) -or
         ($plan00Text.Contains("0.962-blueprint-region-continuous-linkage") -and
          $plan00Text.Contains('下一唯一入口为 `08-')) -or
         ($plan00Text.Contains("0.963-blueprint-mirror-one-shot") -and
          $plan00Text.Contains('下一唯一入口为 `09-'))) -and
        $currentPlanIndexText -and
        (($currentPlanIndexText.Contains("0.961-blueprint-move-interaction-linkage") -and
          $currentPlanIndexText.Contains('下一唯一入口为 `07-')) -or
         ($currentPlanIndexText.Contains("0.962-blueprint-region-continuous-linkage") -and
          $currentPlanIndexText.Contains('下一唯一入口为 `08-')) -or
         ($currentPlanIndexText.Contains("0.963-blueprint-mirror-one-shot") -and
          $currentPlanIndexText.Contains('下一唯一入口为 `09-')))) {
        Write-Pass "Blueprint feedback/autoplace stage 06 plan files and current plan index advance only to stage 07."
    }
    else {
        Write-FailHealth "Blueprint feedback/autoplace stage 06 plan files must mark 06 complete, record tests/audit/no-package scope, and advance the only next entry to 07."
    }

    if ($functionDocText -and
        $functionDocText.Contains("0.961-blueprint-move-interaction-linkage") -and
        $functionDocText.Contains("blueprint.move") -and
        $functionDocText.Contains("move-floating-follow-preview") -and
        $functionDocText.Contains("moveBlockedByPlacedProgress") -and
        $diagnosticsDocText -and
        $diagnosticsDocText.Contains("0.961-blueprint-move-interaction-linkage") -and
        $diagnosticsDocText.Contains("moveFloatingPreview") -and
        $diagnosticsDocText.Contains("moveBlockedByPlacedProgress") -and
        $updateIndexText -and
        $updateIndexText.Contains("0.961-移动蓝图交互联动") -and
        $updateRecordText -and
        $updateRecordText.Contains('RuntimeVersion：`0.961-blueprint-move-interaction-linkage`') -and
        $updateRecordText.Contains("BlueprintMoveActionShortcutAndHotkeyShareTransformState") -and
        $updateRecordText.Contains("不生成测试包") -and
        $docHistoryIndexText -and
        $docHistoryIndexText.Contains("蓝图移动蓝图交互联动") -and
        $docHistoryRecordText -and
        $docHistoryRecordText.Contains("06-移动蓝图交互联动.md")) {
        Write-Pass "Blueprint feedback/autoplace stage 06 function doc, diagnostics doc, update record, and document history are synchronized."
    }
    else {
        Write-FailHealth "Blueprint feedback/autoplace stage 06 must synchronize function doc, diagnostics doc, update record, and document-change history."
    }

    if ($auditText -and
        $auditText.Contains("Test-BlueprintFeedbackStage06MoveInteractionGovernance") -and
        $auditText.Contains("0.961-blueprint-move-interaction-linkage") -and
        $auditText.Contains("moveBlockedByPlacedProgress")) {
        Write-Pass "Blueprint scoped health audit includes the stage 06 move interaction governance check."
    }
    else {
        Write-FailHealth "Blueprint scoped health audit must include Test-BlueprintFeedbackStage06MoveInteractionGovernance and move interaction anchors."
    }
}

function Test-BlueprintFeedbackStage07RegionGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $runtimePath = Join-Path $RepoRoot "src\JueMingZ\Runtime\JueMingZRuntime.cs"
    $csprojPath = Join-Path $RepoRoot "src\JueMingZ\JueMingZ.csproj"
    $eraseStatePath = Join-Path $RepoRoot "src\JueMingZ\Automation\Blueprint\BlueprintEraseRegionState.cs"
    $eraseOverlayPath = Join-Path $RepoRoot "src\JueMingZ\UI\BlueprintEraseRegionOverlay.cs"
    $handheldStatePath = Join-Path $RepoRoot "src\JueMingZ\Automation\Blueprint\BlueprintHandheldActionBarState.cs"
    $mainWindowPath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyMainWindow.Blueprint.cs"
    $mainWindowHotkeyPath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyMainWindow.Blueprint.Hotkey.cs"
    $actionPath = Join-Path $RepoRoot "src\JueMingZ\Input\LegacyUiActionService.Blueprint.cs"
    $hotkeyServicePath = Join-Path $RepoRoot "src\JueMingZ\Input\BlueprintEntryHotkeyService.cs"
    $featureIdsPath = Join-Path $RepoRoot "src\JueMingZ\Common\FeatureIds.cs"
    $conflictPath = Join-Path $RepoRoot "src\JueMingZ\Config\FeatureToggleHotkeyConflictRegistry.cs"
    $entryTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintEntryTests.cs"
    $eraseTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintEraseTests.cs"
    $handheldTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintHandheldActionBarTests.cs"
    $programPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.cs"
    $auditPath = Join-Path $RepoRoot "scripts\audit-project-health.ps1"
    $planDirectory = Get-BlueprintFeedbackAutoplacePlanDirectory -RepoRoot $RepoRoot
    $plan00Path = Join-Path $planDirectory "00-基准.md"
    $plan07Path = Join-Path $planDirectory "07-区域修改持续态联动.md"
    $currentPlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "索引.md")
    $functionDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $diagnosticsDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "AI诊断日志说明.md")
    $updateIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "索引.md")
    $updateDirectory = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录")
    $docHistoryIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "索引.md")
    $docHistoryDirectory = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史")

    $updateRecordPath = ""
    if (Test-Path -LiteralPath $updateDirectory) {
        $record = Get-ChildItem -LiteralPath $updateDirectory -Filter "0.962-区域修改持续态联动-*.md" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($record) {
            $updateRecordPath = $record.FullName
        }
    }

    $docHistoryRecordPath = ""
    if (Test-Path -LiteralPath $docHistoryDirectory) {
        $record = Get-ChildItem -LiteralPath $docHistoryDirectory -Filter "蓝图区域修改持续态联动-*.md" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($record) {
            $docHistoryRecordPath = $record.FullName
        }
    }

    $runtimeText = Read-TextIfExists -Path $runtimePath
    $csprojText = Read-TextIfExists -Path $csprojPath
    $eraseStateText = Read-TextIfExists -Path $eraseStatePath
    $eraseOverlayText = Read-TextIfExists -Path $eraseOverlayPath
    $handheldStateText = Read-TextIfExists -Path $handheldStatePath
    $mainWindowText = Read-TextIfExists -Path $mainWindowPath
    $mainWindowHotkeyText = Read-TextIfExists -Path $mainWindowHotkeyPath
    $actionText = Read-TextIfExists -Path $actionPath
    $hotkeyServiceText = Read-TextIfExists -Path $hotkeyServicePath
    $featureIdsText = Read-TextIfExists -Path $featureIdsPath
    $conflictText = Read-TextIfExists -Path $conflictPath
    $entryTestText = Read-TextIfExists -Path $entryTestPath
    $eraseTestText = Read-TextIfExists -Path $eraseTestPath
    $handheldTestText = Read-TextIfExists -Path $handheldTestPath
    $programText = Read-TextIfExists -Path $programPath
    $auditText = Read-TextIfExists -Path $auditPath
    $plan00Text = Read-TextIfExists -Path $plan00Path
    $plan07Text = Read-TextIfExists -Path $plan07Path
    $currentPlanIndexText = Read-TextIfExists -Path $currentPlanIndexPath
    $functionDocText = Read-TextIfExists -Path $functionDocPath
    $diagnosticsDocText = Read-TextIfExists -Path $diagnosticsDocPath
    $updateIndexText = Read-TextIfExists -Path $updateIndexPath
    $updateRecordText = if ([string]::IsNullOrWhiteSpace($updateRecordPath)) { $null } else { Read-TextIfExists -Path $updateRecordPath }
    $docHistoryIndexText = Read-TextIfExists -Path $docHistoryIndexPath
    $docHistoryRecordText = if ([string]::IsNullOrWhiteSpace($docHistoryRecordPath)) { $null } else { Read-TextIfExists -Path $docHistoryRecordPath }

    if (Test-BlueprintPlacementVersionMetadata -RuntimeText $runtimeText -CsprojText $csprojText -AllowedRuntimeVersions @("0.962-blueprint-region-continuous-linkage")) {
        Write-Pass "Blueprint feedback/autoplace stage 07 version metadata is synchronized to 0.962-blueprint-region-continuous-linkage."
    }
    else {
        Write-FailHealth "Blueprint feedback/autoplace stage 07 must synchronize RuntimeVersion and project metadata to 0.962-blueprint-region-continuous-linkage."
    }

    if ($eraseStateText -and $eraseOverlayText -and
        $eraseStateText.Contains("HasHoverTile") -and
        $eraseStateText.Contains("UpdateHoverLocked") -and
        $eraseStateText.Contains("正在修改已放置蓝图区域") -and
        $eraseStateText.Contains("可继续拖选修改") -and
        $eraseStateText.Contains("已取消已放置蓝图区域修改") -and
        $eraseOverlayText.Contains("cursor-red-follow-mask") -and
        $eraseOverlayText.Contains("cancel-only-exit") -and
        $eraseOverlayText.Contains("snapshot.HasHoverTile") -and
        -not $eraseStateText.Contains("InputActionQueue")) {
        Write-Pass "Blueprint stage 07 erase state owns continuous region modify, hover red mask state, and cancel-only exit without ActionQueue/world writes."
    }
    else {
        Write-FailHealth "Blueprint stage 07 erase state/overlay must keep hover red mask, continuous active state, cancel-only exit, and store-mask-only boundary."
    }

    if ($handheldStateText -and $mainWindowText -and $mainWindowHotkeyText -and $actionText -and $hotkeyServiceText -and $featureIdsText -and $conflictText -and
        $featureIdsText.Contains('BlueprintRegionAction = "blueprint.region"') -and
        ($mainWindowText.Contains("BlueprintActionShortcutRowCount = 5") -or $mainWindowText.Contains("BlueprintActionShortcutRowCount = 6")) -and
        $mainWindowText.Contains("FeatureIds.BlueprintRegionAction") -and
        $mainWindowText.Contains("BlueprintEntryCommands.StartRegionModify") -and
        $mainWindowText.Contains("stage07-region-modify-row") -and
        $mainWindowText.Contains("region-toggle-cancel-label") -and
        $mainWindowHotkeyText.Contains("FeatureIds.BlueprintRegionAction") -and
        $handheldStateText.Contains("RegionModifyCancelButtonLabel") -and
        $handheldStateText.Contains("取消修改") -and
        $actionText.Contains("StartOrCancelBlueprintRegionModify") -and
        $actionText.Contains("FeatureIds.BlueprintRegionAction") -and
        $actionText.Contains("CloseBlueprintMainMenuForBlueprintWorldInteraction(regionStarted") -and
        $hotkeyServiceText.Contains("FeatureIds.BlueprintRegionAction") -and
        $hotkeyServiceText.Contains("BlueprintEntryCommands.StartRegionModify") -and
        $conflictText.Contains("蓝图区域修改快捷键")) {
        Write-Pass "Blueprint stage 07 wires F5, handheld, and action hotkey region entry through shared erase state."
    }
    else {
        Write-FailHealth "Blueprint stage 07 must wire blueprint.region, F5 row, handheld cancel label, hotkey capture/conflict, and shared StartOrCancelBlueprintRegionModify routing."
    }

    if ($entryTestText -and $eraseTestText -and $handheldTestText -and $programText -and
        $entryTestText.Contains("BlueprintRegionActionShortcutAndHotkeyShareEraseState") -and
        $entryTestText.Contains("FeatureIds.BlueprintRegionAction") -and
        $eraseTestText.Contains("BlueprintEraseRegionStage07ContinuousHoverAndCancelOnly") -and
        $eraseTestText.Contains("BlueprintEraseRegionPhysicalLeftEdgesIgnoreConsumedWorldLeft") -and
        $eraseTestText.Contains("cursor-red-follow-mask") -and
        $handheldTestText.Contains("取消修改") -and
        $programText.Contains("blueprint region action shortcut and hotkey share erase state") -and
        $programText.Contains("blueprint erase region stage 07 continuous hover and cancel only") -and
        $programText.Contains("blueprint erase region physical left edges ignore consumed world left")) {
        Write-Pass "Blueprint stage 07 console tests cover F5/hotkey shared state, handheld cancel label, hover mask, physical-left consumed frames, and cancel-only exit."
    }
    else {
        Write-FailHealth "Blueprint stage 07 must register console tests for region F5/hotkey/handheld linkage, hover mask, physical-left consumed frames, and cancel-only exit."
    }

    if ($plan07Text -and
        $plan07Text.Contains("状态：已完成") -and
        $plan07Text.Contains('RuntimeVersion：`0.962-blueprint-region-continuous-linkage`') -and
        $plan07Text.Contains("BlueprintRegionActionShortcutAndHotkeyShareEraseState") -and
        $plan07Text.Contains("BlueprintEraseRegionStage07ContinuousHoverAndCancelOnly") -and
        $plan07Text.Contains("Test-BlueprintFeedbackStage07RegionGovernance") -and
        $plan07Text.Contains("不生成测试包") -and
        $plan07Text.Contains('未实现 `08`') -and
        $plan00Text -and
        (($plan00Text.Contains("0.962-blueprint-region-continuous-linkage") -and
          $plan00Text.Contains('下一唯一入口为 `08-')) -or
         ($plan00Text.Contains("0.963-blueprint-mirror-one-shot") -and
          $plan00Text.Contains('下一唯一入口为 `09-'))) -and
        $currentPlanIndexText -and
        (($currentPlanIndexText.Contains("0.962-blueprint-region-continuous-linkage") -and
          $currentPlanIndexText.Contains('下一唯一入口为 `08-')) -or
         ($currentPlanIndexText.Contains("0.963-blueprint-mirror-one-shot") -and
          $currentPlanIndexText.Contains('下一唯一入口为 `09-')))) {
        Write-Pass "Blueprint feedback/autoplace stage 07 plan files and current plan index advance only to stage 08."
    }
    else {
        Write-FailHealth "Blueprint feedback/autoplace stage 07 plan files must mark 07 complete, record tests/audit/no-package scope, and advance the only next entry to 08."
    }

    if ($functionDocText -and
        $functionDocText.Contains("0.962-blueprint-region-continuous-linkage") -and
        $functionDocText.Contains("blueprint.region") -and
        $functionDocText.Contains("cursor-red-follow-mask") -and
        $functionDocText.Contains("cancel-only-exit") -and
        $diagnosticsDocText -and
        $diagnosticsDocText.Contains("0.962-blueprint-region-continuous-linkage") -and
        $diagnosticsDocText.Contains("blueprint.region") -and
        $diagnosticsDocText.Contains("hasHoverTile") -and
        $updateIndexText -and
        $updateIndexText.Contains("0.962-区域修改持续态联动") -and
        $updateRecordText -and
        $updateRecordText.Contains('RuntimeVersion：`0.962-blueprint-region-continuous-linkage`') -and
        $updateRecordText.Contains("BlueprintRegionActionShortcutAndHotkeyShareEraseState") -and
        $updateRecordText.Contains("不生成测试包") -and
        $docHistoryIndexText -and
        $docHistoryIndexText.Contains("蓝图区域修改持续态联动") -and
        $docHistoryRecordText -and
        $docHistoryRecordText.Contains("07-区域修改持续态联动.md")) {
        Write-Pass "Blueprint feedback/autoplace stage 07 function doc, diagnostics doc, update record, and document history are synchronized."
    }
    else {
        Write-FailHealth "Blueprint feedback/autoplace stage 07 must synchronize function doc, diagnostics doc, update record, and document-change history."
    }

    if ($auditText -and
        $auditText.Contains("Test-BlueprintFeedbackStage07RegionGovernance") -and
        $auditText.Contains("0.962-blueprint-region-continuous-linkage") -and
        $auditText.Contains("cursor-red-follow-mask") -and
        $auditText.Contains("blueprint.region")) {
        Write-Pass "Blueprint scoped health audit includes the stage 07 region governance check."
    }
    else {
        Write-FailHealth "Blueprint scoped health audit must include Test-BlueprintFeedbackStage07RegionGovernance and region interaction anchors."
    }
}

function Test-BlueprintFeedbackStage08MirrorGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $runtimePath = Join-Path $RepoRoot "src\JueMingZ\Runtime\JueMingZRuntime.cs"
    $csprojPath = Join-Path $RepoRoot "src\JueMingZ\JueMingZ.csproj"
    $transformStatePath = Join-Path $RepoRoot "src\JueMingZ\Automation\Blueprint\BlueprintPlacedInstanceTransformState.cs"
    $handheldStatePath = Join-Path $RepoRoot "src\JueMingZ\Automation\Blueprint\BlueprintHandheldActionBarState.cs"
    $mainWindowPath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyMainWindow.Blueprint.cs"
    $mainWindowHotkeyPath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyMainWindow.Blueprint.Hotkey.cs"
    $actionPath = Join-Path $RepoRoot "src\JueMingZ\Input\LegacyUiActionService.Blueprint.cs"
    $hotkeyServicePath = Join-Path $RepoRoot "src\JueMingZ\Input\BlueprintEntryHotkeyService.cs"
    $featureIdsPath = Join-Path $RepoRoot "src\JueMingZ\Common\FeatureIds.cs"
    $conflictPath = Join-Path $RepoRoot "src\JueMingZ\Config\FeatureToggleHotkeyConflictRegistry.cs"
    $entryTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintEntryTests.cs"
    $placedTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintPlacedInstanceTests.cs"
    $handheldTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintHandheldActionBarTests.cs"
    $diagnosticsTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintDiagnosticsTests.cs"
    $programPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.cs"
    $auditPath = Join-Path $RepoRoot "scripts\audit-project-health.ps1"
    $planDirectory = Get-BlueprintFeedbackAutoplacePlanDirectory -RepoRoot $RepoRoot
    $plan00Path = Join-Path $planDirectory "00-基准.md"
    $plan08Path = Join-Path $planDirectory "08-镜像一次性功能修复.md"
    $currentPlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "索引.md")
    $functionDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $diagnosticsDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "AI诊断日志说明.md")
    $updateIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "索引.md")
    $updateDirectory = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录")
    $docHistoryIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "索引.md")
    $docHistoryDirectory = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史")

    $updateRecordPath = ""
    if (Test-Path -LiteralPath $updateDirectory) {
        $record = Get-ChildItem -LiteralPath $updateDirectory -Filter "0.963-镜像一次性功能修复-*.md" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($record) {
            $updateRecordPath = $record.FullName
        }
    }

    $docHistoryRecordPath = ""
    if (Test-Path -LiteralPath $docHistoryDirectory) {
        $record = Get-ChildItem -LiteralPath $docHistoryDirectory -Filter "蓝图镜像一次性功能修复-*.md" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($record) {
            $docHistoryRecordPath = $record.FullName
        }
    }

    $runtimeText = Read-TextIfExists -Path $runtimePath
    $csprojText = Read-TextIfExists -Path $csprojPath
    $transformStateText = Read-TextIfExists -Path $transformStatePath
    $handheldStateText = Read-TextIfExists -Path $handheldStatePath
    $mainWindowText = Read-TextIfExists -Path $mainWindowPath
    $mainWindowHotkeyText = Read-TextIfExists -Path $mainWindowHotkeyPath
    $actionText = Read-TextIfExists -Path $actionPath
    $hotkeyServiceText = Read-TextIfExists -Path $hotkeyServicePath
    $featureIdsText = Read-TextIfExists -Path $featureIdsPath
    $conflictText = Read-TextIfExists -Path $conflictPath
    $entryTestText = Read-TextIfExists -Path $entryTestPath
    $placedTestText = Read-TextIfExists -Path $placedTestPath
    $handheldTestText = Read-TextIfExists -Path $handheldTestPath
    $diagnosticsTestText = Read-TextIfExists -Path $diagnosticsTestPath
    $programText = Read-TextIfExists -Path $programPath
    $auditText = Read-TextIfExists -Path $auditPath
    $plan00Text = Read-TextIfExists -Path $plan00Path
    $plan08Text = Read-TextIfExists -Path $plan08Path
    $currentPlanIndexText = Read-TextIfExists -Path $currentPlanIndexPath
    $functionDocText = Read-TextIfExists -Path $functionDocPath
    $diagnosticsDocText = Read-TextIfExists -Path $diagnosticsDocPath
    $updateIndexText = Read-TextIfExists -Path $updateIndexPath
    $updateRecordText = if ([string]::IsNullOrWhiteSpace($updateRecordPath)) { $null } else { Read-TextIfExists -Path $updateRecordPath }
    $docHistoryIndexText = Read-TextIfExists -Path $docHistoryIndexPath
    $docHistoryRecordText = if ([string]::IsNullOrWhiteSpace($docHistoryRecordPath)) { $null } else { Read-TextIfExists -Path $docHistoryRecordPath }

    if (Test-BlueprintPlacementVersionMetadata -RuntimeText $runtimeText -CsprojText $csprojText -AllowedRuntimeVersions @("0.963-blueprint-mirror-one-shot")) {
        Write-Pass "Blueprint feedback/autoplace stage 08 version metadata is synchronized to 0.963-blueprint-mirror-one-shot."
    }
    else {
        Write-FailHealth "Blueprint feedback/autoplace stage 08 must synchronize RuntimeVersion and project metadata to 0.963-blueprint-mirror-one-shot."
    }

    if ($handheldStateText -and $mainWindowText -and $mainWindowHotkeyText -and $actionText -and $hotkeyServiceText -and $featureIdsText -and $conflictText -and
        $featureIdsText.Contains('BlueprintMirrorAction = "blueprint.mirror"') -and
        $mainWindowText.Contains("BlueprintActionShortcutRowCount = 6") -and
        $mainWindowText.Contains("FeatureIds.BlueprintMirrorAction") -and
        $mainWindowText.Contains("BlueprintEntryCommands.StartMirror") -and
        $mainWindowText.Contains("stage08-mirror-row") -and
        $mainWindowText.Contains("real-mirror-entry") -and
        $mainWindowText.Contains("mirror-toggle-cancel-label") -and
        $mainWindowHotkeyText.Contains("FeatureIds.BlueprintMirrorAction") -and
        $handheldStateText.Contains("MirrorCancelButtonLabel") -and
        $handheldStateText.Contains("取消镜像") -and
        $actionText.Contains("StartOrCancelBlueprintMirror") -and
        $actionText.Contains("FeatureIds.BlueprintMirrorAction") -and
        $actionText.Contains("CloseBlueprintMainMenuForBlueprintWorldInteraction(mirrorStarted") -and
        $hotkeyServiceText.Contains("FeatureIds.BlueprintMirrorAction") -and
        $hotkeyServiceText.Contains("BlueprintEntryCommands.StartMirror") -and
        $hotkeyServiceText.Contains("ApplyBlueprintMirrorAction") -and
        $conflictText.Contains("蓝图镜像快捷键")) {
        Write-Pass "Blueprint stage 08 wires F5, handheld, and action hotkey mirror entry through shared transform state."
    }
    else {
        Write-FailHealth "Blueprint stage 08 must wire blueprint.mirror, F5 row, handheld cancel label, hotkey capture/conflict, and shared StartOrCancelBlueprintMirror routing."
    }

    if ($transformStateText -and
        $transformStateText.Contains("BlueprintMirrorService.TryMirrorHorizontal") -and
        $transformStateText.Contains("HasMoveBlockingProgress(target)") -and
        $transformStateText.Contains("CompletedLayers") -and
        $transformStateText.Contains("autoPlacementProgressActive") -and
        $transformStateText.Contains("mirrorBlockedByPlacedProgress")) {
        Write-Pass "Blueprint stage 08 mirror keeps placed-instance snapshot-only semantics and blocks unsafe progress states."
    }
    else {
        Write-FailHealth "Blueprint stage 08 mirror must reuse BlueprintMirrorService, modify only instance snapshots, and block auto-placement or CompletedLayers progress."
    }

    if ($entryTestText -and $placedTestText -and $handheldTestText -and $diagnosticsTestText -and $programText -and
        $entryTestText.Contains("BlueprintMirrorActionShortcutAndHotkeyShareTransformState") -and
        $entryTestText.Contains("FeatureIds.BlueprintMirrorAction") -and
        $placedTestText.Contains("mirrorBlockedByPlacedProgress") -and
        $placedTestText.Contains("CompletedLayers") -and
        $handheldTestText.Contains("取消镜像") -and
        $diagnosticsTestText.Contains("BlueprintMirrorActionShortcutAndHotkeyShareTransformState") -and
        $programText.Contains("blueprint mirror action shortcut and hotkey share transform state") -and
        $programText.Contains("blueprint placed instance mirror uses service and fails closed")) {
        Write-Pass "Blueprint stage 08 console tests cover F5/hotkey shared state, handheld cancel label, and mirror progress fail-closed."
    }
    else {
        Write-FailHealth "Blueprint stage 08 must register console tests for mirror F5/hotkey/handheld linkage and progress fail-closed behavior."
    }

    if ($plan08Text -and
        $plan08Text.Contains("状态：已完成") -and
        $plan08Text.Contains('RuntimeVersion：`0.963-blueprint-mirror-one-shot`') -and
        $plan08Text.Contains("BlueprintMirrorActionShortcutAndHotkeyShareTransformState") -and
        $plan08Text.Contains("BlueprintPlacedInstanceMirrorUsesServiceAndFailsClosed") -and
        $plan08Text.Contains("Test-BlueprintFeedbackStage08MirrorGovernance") -and
        $plan08Text.Contains("不生成测试包") -and
        $plan08Text.Contains('未实现 `09`') -and
        $plan00Text -and
        $plan00Text.Contains("0.963-blueprint-mirror-one-shot") -and
        $plan00Text.Contains('下一唯一入口为 `09-') -and
        $currentPlanIndexText -and
        $currentPlanIndexText.Contains("0.963-blueprint-mirror-one-shot") -and
        $currentPlanIndexText.Contains('下一唯一入口为 `09-')) {
        Write-Pass "Blueprint feedback/autoplace stage 08 plan files and current plan index advance only to stage 09."
    }
    else {
        Write-FailHealth "Blueprint feedback/autoplace stage 08 plan files must mark 08 complete, record tests/audit/no-package scope, and advance the only next entry to 09."
    }

    if ($functionDocText -and
        $functionDocText.Contains("0.963-blueprint-mirror-one-shot") -and
        $functionDocText.Contains("blueprint.mirror") -and
        $functionDocText.Contains("mirrorBlockedByPlacedProgress") -and
        $functionDocText.Contains("mirror-toggle-cancel-label") -and
        $diagnosticsDocText -and
        $diagnosticsDocText.Contains("0.963-blueprint-mirror-one-shot") -and
        $diagnosticsDocText.Contains("blueprint.mirror") -and
        $diagnosticsDocText.Contains("start-mirror") -and
        $diagnosticsDocText.Contains("mirrorBlockedByPlacedProgress") -and
        $diagnosticsDocText.Contains("Test-BlueprintFeedbackStage08MirrorGovernance") -and
        $updateIndexText -and
        $updateIndexText.Contains("0.963-镜像一次性功能修复") -and
        $updateRecordText -and
        $updateRecordText.Contains('RuntimeVersion：`0.963-blueprint-mirror-one-shot`') -and
        $updateRecordText.Contains("BlueprintMirrorActionShortcutAndHotkeyShareTransformState") -and
        $updateRecordText.Contains("不生成测试包") -and
        $docHistoryIndexText -and
        $docHistoryIndexText.Contains("蓝图镜像一次性功能修复") -and
        $docHistoryRecordText -and
        $docHistoryRecordText.Contains("08-镜像一次性功能修复.md")) {
        Write-Pass "Blueprint feedback/autoplace stage 08 function doc, diagnostics doc, update record, and document history are synchronized."
    }
    else {
        Write-FailHealth "Blueprint feedback/autoplace stage 08 must synchronize function doc, diagnostics doc, update record, and document-change history."
    }

    if ($auditText -and
        $auditText.Contains("Test-BlueprintFeedbackStage08MirrorGovernance") -and
        $auditText.Contains("0.963-blueprint-mirror-one-shot") -and
        $auditText.Contains("blueprint.mirror") -and
        $auditText.Contains("mirrorBlockedByPlacedProgress")) {
        Write-Pass "Blueprint scoped health audit includes the stage 08 mirror governance check."
    }
    else {
        Write-FailHealth "Blueprint scoped health audit must include Test-BlueprintFeedbackStage08MirrorGovernance and mirror interaction anchors."
    }
}

function Test-BlueprintFeedbackStage09AutoplaceEntryGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $runtimePath = Join-Path $RepoRoot "src\JueMingZ\Runtime\JueMingZRuntime.cs"
    $csprojPath = Join-Path $RepoRoot "src\JueMingZ\JueMingZ.csproj"
    $featurePath = Join-Path $RepoRoot "src\JueMingZ\Features\Catalog\BlueprintFeatureRegistrar.cs"
    $autoPlacementPath = Join-Path $RepoRoot "src\JueMingZ\Automation\Blueprint\BlueprintAutoPlacementService.cs"
    $executorPath = Join-Path $RepoRoot "src\JueMingZ\Actions\Executors\BlueprintAutoPlaceActionExecutor.cs"
    $replacementPath = Join-Path $RepoRoot "src\JueMingZ\Automation\Blueprint\BlueprintReplacementRuleService.cs"
    $materialPath = Join-Path $RepoRoot "src\JueMingZ\Automation\Blueprint\BlueprintMaterialService.cs"
    $projectionPath = Join-Path $RepoRoot "src\JueMingZ\Automation\Blueprint\BlueprintProjectionService.cs"
    $channelPath = Join-Path $RepoRoot "src\JueMingZ\Actions\Channels\InputActionChannelResolver.cs"
    $dispatcherPath = Join-Path $RepoRoot "src\JueMingZ\Runtime\RuntimeAutomationDispatcher.cs"
    $mainWindowPath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyMainWindow.Blueprint.cs"
    $uiActionPath = Join-Path $RepoRoot "src\JueMingZ\Input\LegacyUiActionService.Blueprint.cs"
    $entryTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintEntryTests.cs"
    $autoTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintAutoPlacementTests.cs"
    $programPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.cs"
    $auditPath = Join-Path $RepoRoot "scripts\audit-project-health.ps1"
    $planDirectory = Get-BlueprintFeedbackAutoplacePlanDirectory -RepoRoot $RepoRoot
    $plan00Path = Join-Path $planDirectory "00-基准.md"
    $plan09Path = Join-Path $planDirectory "09-自动放置事实复核与入口治理.md"
    $currentPlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "索引.md")
    $functionDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $diagnosticsDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "AI诊断日志说明.md")
    $updateIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "索引.md")
    $updateDirectory = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录")
    $docHistoryIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "索引.md")
    $docHistoryDirectory = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史")

    $updateRecordPath = ""
    if (Test-Path -LiteralPath $updateDirectory) {
        $record = Get-ChildItem -LiteralPath $updateDirectory -Filter "0.964-自动放置事实复核入口治理-*.md" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($record) {
            $updateRecordPath = $record.FullName
        }
    }

    $docHistoryRecordPath = ""
    if (Test-Path -LiteralPath $docHistoryDirectory) {
        $record = Get-ChildItem -LiteralPath $docHistoryDirectory -Filter "蓝图自动放置事实复核入口治理-*.md" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($record) {
            $docHistoryRecordPath = $record.FullName
        }
    }

    $runtimeText = Read-TextIfExists -Path $runtimePath
    $csprojText = Read-TextIfExists -Path $csprojPath
    $featureText = Read-TextIfExists -Path $featurePath
    $autoPlacementText = Read-TextIfExists -Path $autoPlacementPath
    $executorText = Read-TextIfExists -Path $executorPath
    $replacementText = Read-TextIfExists -Path $replacementPath
    $materialText = Read-TextIfExists -Path $materialPath
    $projectionText = Read-TextIfExists -Path $projectionPath
    $channelText = Read-TextIfExists -Path $channelPath
    $dispatcherText = Read-TextIfExists -Path $dispatcherPath
    $mainWindowText = Read-TextIfExists -Path $mainWindowPath
    $uiActionText = Read-TextIfExists -Path $uiActionPath
    $entryTestText = Read-TextIfExists -Path $entryTestPath
    $autoTestText = Read-TextIfExists -Path $autoTestPath
    $programText = Read-TextIfExists -Path $programPath
    $auditText = Read-TextIfExists -Path $auditPath
    $plan00Text = Read-TextIfExists -Path $plan00Path
    $plan09Text = Read-TextIfExists -Path $plan09Path
    $currentPlanIndexText = Read-TextIfExists -Path $currentPlanIndexPath
    $functionDocText = Read-TextIfExists -Path $functionDocPath
    $diagnosticsDocText = Read-TextIfExists -Path $diagnosticsDocPath
    $updateIndexText = Read-TextIfExists -Path $updateIndexPath
    $updateRecordText = if ([string]::IsNullOrWhiteSpace($updateRecordPath)) { $null } else { Read-TextIfExists -Path $updateRecordPath }
    $docHistoryIndexText = Read-TextIfExists -Path $docHistoryIndexPath
    $docHistoryRecordText = if ([string]::IsNullOrWhiteSpace($docHistoryRecordPath)) { $null } else { Read-TextIfExists -Path $docHistoryRecordPath }

    if (Test-BlueprintPlacementVersionMetadata -RuntimeText $runtimeText -CsprojText $csprojText -AllowedRuntimeVersions @("0.964-blueprint-autoplace-entry-governance")) {
        Write-Pass "Blueprint feedback/autoplace stage 09 version metadata is synchronized to 0.964-blueprint-autoplace-entry-governance."
    }
    else {
        Write-FailHealth "Blueprint feedback/autoplace stage 09 must synchronize RuntimeVersion and project metadata to 0.964-blueprint-autoplace-entry-governance."
    }

    if ($featureText -and $entryTestText -and
        $featureText.Contains("阶段 15 自动摆放 ActionQueue 契约") -and
        $featureText.Contains("ItemUseBridge 投影复验") -and
        $featureText.Contains("同类替换候选") -and
        $featureText.Contains("fail-closed / 待后续治理") -and
        -not $featureText.Contains("实际 Tile/Wall 自动摆放、替换和镜像仍未实现") -and
        $entryTestText.Contains("ItemUseBridge") -and
        $entryTestText.Contains("Blueprint main feature note must not describe stage-15 auto placement")) {
        Write-Pass "Blueprint stage 09 refreshes feature catalog notes without marking blueprint.main implemented."
    }
    else {
        Write-FailHealth "Blueprint stage 09 must replace stale blueprint.main notes while preserving planned/placeholder lifecycle metadata."
    }

    if ($dispatcherText -and $autoPlacementText -and $executorText -and $replacementText -and $materialText -and $projectionText -and $channelText -and $mainWindowText -and $uiActionText -and
        $dispatcherText.Contains("DispatchBlueprintAutoPlacement") -and
        $dispatcherText.Contains("BlueprintAutoPlacementEnabled") -and
        $autoPlacementText.Contains("ResolveCandidate") -and
        $autoPlacementText.Contains("TryChooseMaterialForAutoPlacement") -and
        $autoPlacementText.Contains("BlueprintContractStage") -and
        $autoPlacementText.Contains("RecordAutoPlacementCandidateScan") -and
        $executorText.Contains("ItemUseBridge.TryEnqueueUseSelectedItem") -and
        $executorText.Contains("TryFindMainInventoryMaterial") -and
        $executorText.Contains("ForceRefreshProjection") -and
        $executorText.Contains("directWorldMutationAttempted") -and
        $replacementText.Contains("BlueprintReplacementCategories.Torch") -and
        $replacementText.Contains("BlueprintReplacementCategories.Platform") -and
        $replacementText.Contains("BlueprintReplacementCategories.WorkBench") -and
        $replacementText.Contains("BlueprintReplacementCategories.Sign") -and
        $materialText.Contains("VoidBagStack") -and
        $projectionText.Contains("CompletedLayers") -and
        $channelText.Contains("InputActionKind.BlueprintAutoPlace") -and
        $channelText.Contains("InputActionChannel.BridgeItemUse") -and
        $mainWindowText.Contains("BlueprintAutoPlacementVisualContract") -and
        $uiActionText.Contains("Ui.Blueprint.AutoPlacement")) {
        Write-Pass "Blueprint stage 09 source fact review anchors runtime, UI, ActionQueue, replacement, materials, progress, and diagnostics paths."
    }
    else {
        Write-FailHealth "Blueprint stage 09 source anchors must cover runtime dispatch, UI toggle, ActionQueue channels, ItemUseBridge executor, replacement categories, material/progress compatibility, and diagnostics."
    }

    if ($autoTestText -and $programText -and
        $autoTestText.Contains("BlueprintAutoPlacementSubmitsActionQueueAndVerifiesPlacement") -and
        $autoTestText.Contains("BlueprintAutoPlacementUsesConfiguredReplacementMaterial") -and
        $autoTestText.Contains("BlueprintAutoPlacementReplacementFailClosedWhenDisabledOrWrongCategory") -and
        $autoTestText.Contains("BlueprintAutoPlacementDiagnosticsWriteRuntimeSnapshotJson") -and
        $programText.Contains("blueprint auto placement submits ActionQueue and verifies placement") -and
        $programText.Contains("blueprint auto placement uses configured replacement material")) {
        Write-Pass "Blueprint stage 09 keeps existing auto-placement and replacement targeted tests registered."
    }
    else {
        Write-FailHealth "Blueprint stage 09 must retain targeted auto-placement/replacement tests and their Program.cs registration."
    }

    if ($plan09Text -and $plan00Text -and $currentPlanIndexText -and
        $plan09Text.Contains("状态：已完成") -and
        $plan09Text.Contains('RuntimeVersion：`0.964-blueprint-autoplace-entry-governance`') -and
        $plan09Text.Contains("自动放置现状矩阵") -and
        $plan09Text.Contains("同类替换兼容矩阵") -and
        $plan09Text.Contains("主背包 0-49") -and
        $plan09Text.Contains("虚空袋") -and
        $plan09Text.Contains("Test-BlueprintFeedbackStage09AutoplaceEntryGovernance") -and
        $plan09Text.Contains('未实现 `10`') -and
        $plan09Text.Contains("不生成测试包") -and
        $plan00Text.Contains("0.964-blueprint-autoplace-entry-governance") -and
        $plan00Text.Contains('下一唯一入口为 `10-') -and
        $currentPlanIndexText.Contains("0.964-blueprint-autoplace-entry-governance") -and
        $currentPlanIndexText.Contains('下一唯一入口为 `10-')) {
        Write-Pass "Blueprint feedback/autoplace stage 09 plan files and current plan index advance only to stage 10."
    }
    else {
        Write-FailHealth "Blueprint feedback/autoplace stage 09 plan files must mark 09 complete, freeze matrices, record no-package/no-10 scope, and advance only to 10."
    }

    if ($functionDocText -and $diagnosticsDocText -and $updateIndexText -and $updateRecordText -and $docHistoryIndexText -and $docHistoryRecordText -and
        $functionDocText.Contains("0.964-blueprint-autoplace-entry-governance") -and
        $functionDocText.Contains("主背包 0-49") -and
        $functionDocText.Contains("虚空袋") -and
        $diagnosticsDocText.Contains("0.964-blueprint-autoplace-entry-governance") -and
        $diagnosticsDocText.Contains("主背包 0-49") -and
        $diagnosticsDocText.Contains("Test-BlueprintFeedbackStage09AutoplaceEntryGovernance") -and
        $updateIndexText.Contains("0.964-自动放置事实复核入口治理") -and
        $updateRecordText.Contains('RuntimeVersion：`0.964-blueprint-autoplace-entry-governance`') -and
        $updateRecordText.Contains('未实现 `10`') -and
        $docHistoryIndexText.Contains("蓝图自动放置事实复核入口治理") -and
        $docHistoryRecordText.Contains("09-自动放置事实复核与入口治理.md")) {
        Write-Pass "Blueprint feedback/autoplace stage 09 function doc, diagnostics doc, update record, and document history are synchronized."
    }
    else {
        Write-FailHealth "Blueprint feedback/autoplace stage 09 must synchronize function doc, diagnostics doc, update record, and document-change history."
    }

    if ($auditText -and
        $auditText.Contains("Test-BlueprintFeedbackStage09AutoplaceEntryGovernance") -and
        $auditText.Contains("0.964-blueprint-autoplace-entry-governance") -and
        $auditText.Contains("TryFindMainInventoryMaterial") -and
        $auditText.Contains("BlueprintAutoPlacementSubmitsActionQueueAndVerifiesPlacement")) {
        Write-Pass "Blueprint scoped health audit includes the stage 09 auto-placement entry governance check."
    }
    else {
        Write-FailHealth "Blueprint scoped health audit must include Test-BlueprintFeedbackStage09AutoplaceEntryGovernance and auto-placement entry anchors."
    }
}

function Test-BlueprintFeedbackStage10AutoplaceExecutionGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $runtimePath = Join-Path $RepoRoot "src\JueMingZ\Runtime\JueMingZRuntime.cs"
    $csprojPath = Join-Path $RepoRoot "src\JueMingZ\JueMingZ.csproj"
    $autoPlacementPath = Join-Path $RepoRoot "src\JueMingZ\Automation\Blueprint\BlueprintAutoPlacementService.cs"
    $executorPath = Join-Path $RepoRoot "src\JueMingZ\Actions\Executors\BlueprintAutoPlaceActionExecutor.cs"
    $metadataPath = Join-Path $RepoRoot "src\JueMingZ\Common\ActionMetadataKeys.cs"
    $runtimeBuilderPath = Join-Path $RepoRoot "src\JueMingZ\Runtime\Diagnostics\RuntimeDiagnosticSnapshotBuilder.Blueprint.cs"
    $diagnosticSnapshotPath = Join-Path $RepoRoot "src\JueMingZ\Diagnostics\DiagnosticSnapshot.cs"
    $diagnosticWriterPath = Join-Path $RepoRoot "src\JueMingZ\Diagnostics\DiagnosticSnapshotWriter.Json.cs"
    $mainWindowPath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyMainWindow.Blueprint.cs"
    $autoTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintAutoPlacementTests.cs"
    $programPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.cs"
    $auditPath = Join-Path $RepoRoot "scripts\audit-project-health.ps1"
    $planDirectory = Get-BlueprintFeedbackAutoplacePlanDirectory -RepoRoot $RepoRoot
    $plan00Path = Join-Path $planDirectory "00-基准.md"
    $plan10Path = Join-Path $planDirectory "10-自动放置与同类替换执行链路.md"
    $currentPlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "索引.md")
    $functionDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $diagnosticsDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "AI诊断日志说明.md")
    $updateIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "索引.md")
    $updateDirectory = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录")
    $docHistoryIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "索引.md")
    $docHistoryDirectory = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史")

    $updateRecordPath = ""
    if (Test-Path -LiteralPath $updateDirectory) {
        $record = Get-ChildItem -LiteralPath $updateDirectory -Filter "0.965-自动放置执行链路治理-*.md" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($record) {
            $updateRecordPath = $record.FullName
        }
    }

    $docHistoryRecordPath = ""
    if (Test-Path -LiteralPath $docHistoryDirectory) {
        $record = Get-ChildItem -LiteralPath $docHistoryDirectory -Filter "蓝图自动放置执行链路治理-*.md" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($record) {
            $docHistoryRecordPath = $record.FullName
        }
    }

    $runtimeText = Read-TextIfExists -Path $runtimePath
    $csprojText = Read-TextIfExists -Path $csprojPath
    $autoPlacementText = Read-TextIfExists -Path $autoPlacementPath
    $executorText = Read-TextIfExists -Path $executorPath
    $metadataText = Read-TextIfExists -Path $metadataPath
    $runtimeBuilderText = Read-TextIfExists -Path $runtimeBuilderPath
    $diagnosticSnapshotText = Read-TextIfExists -Path $diagnosticSnapshotPath
    $diagnosticWriterText = Read-TextIfExists -Path $diagnosticWriterPath
    $mainWindowText = Read-TextIfExists -Path $mainWindowPath
    $autoTestText = Read-TextIfExists -Path $autoTestPath
    $programText = Read-TextIfExists -Path $programPath
    $auditText = Read-TextIfExists -Path $auditPath
    $plan00Text = Read-TextIfExists -Path $plan00Path
    $plan10Text = Read-TextIfExists -Path $plan10Path
    $currentPlanIndexText = Read-TextIfExists -Path $currentPlanIndexPath
    $functionDocText = Read-TextIfExists -Path $functionDocPath
    $diagnosticsDocText = Read-TextIfExists -Path $diagnosticsDocPath
    $updateIndexText = Read-TextIfExists -Path $updateIndexPath
    $updateRecordText = if ([string]::IsNullOrWhiteSpace($updateRecordPath)) { $null } else { Read-TextIfExists -Path $updateRecordPath }
    $docHistoryIndexText = Read-TextIfExists -Path $docHistoryIndexPath
    $docHistoryRecordText = if ([string]::IsNullOrWhiteSpace($docHistoryRecordPath)) { $null } else { Read-TextIfExists -Path $docHistoryRecordPath }

    if (Test-BlueprintPlacementVersionMetadata -RuntimeText $runtimeText -CsprojText $csprojText -AllowedRuntimeVersions @("0.965-blueprint-autoplace-execution-chain")) {
        Write-Pass "Blueprint feedback/autoplace stage 10 version metadata is synchronized to 0.965-blueprint-autoplace-execution-chain."
    }
    else {
        Write-FailHealth "Blueprint feedback/autoplace stage 10 must synchronize RuntimeVersion and project metadata to 0.965-blueprint-autoplace-execution-chain."
    }

    if ($autoPlacementText -and $executorText -and $metadataText -and $runtimeBuilderText -and $diagnosticSnapshotText -and $diagnosticWriterText -and $mainWindowText -and
        $autoPlacementText.Contains("SkippedVoidBagOnlyLayerCount") -and
        $autoPlacementText.Contains("voidBagMaterialNotExecutable") -and
        $autoPlacementText.Contains("BuildMainInventoryAvailabilityMap") -and
        $autoPlacementText.Contains("BuildCombinedInventoryAvailabilityMap") -and
        $autoPlacementText.Contains("BlueprintMaterialExecutionScope") -and
        $executorText.Contains("TryFindMainInventoryMaterial") -and
        $executorText.Contains("主背包 0-49") -and
        $executorText.Contains("materialExecutionScope") -and
        $executorText.Contains("ItemUseBridge.TryEnqueueUseSelectedItem") -and
        $metadataText.Contains("BlueprintMaterialExecutionScope") -and
        $runtimeBuilderText.Contains("BlueprintAutoPlacementSkippedVoidBagOnlyLayerCount") -and
        $diagnosticSnapshotText.Contains("BlueprintAutoPlacementSkippedVoidBagOnlyLayerCount") -and
        $diagnosticWriterText.Contains("BlueprintAutoPlacementSkippedVoidBagOnlyLayerCount") -and
        $mainWindowText.Contains("main-inventory-execution+void-bag-fail-closed") -and
        $mainWindowText.Contains("虚空袋需移入主包")) {
        Write-Pass "Blueprint stage 10 locks auto-placement execution to main inventory 0-49 and exposes void-bag fail-closed diagnostics."
    }
    else {
        Write-FailHealth "Blueprint stage 10 must keep main-inventory auto-placement execution, void-bag fail-closed classification, request metadata, runtime snapshot, JSON writer, and UI summary anchors."
    }

    if ($autoTestText -and $programText -and
        $autoTestText.Contains("BlueprintAutoPlacementVoidBagOnlyMaterialsFailClosedWithReason") -and
        $autoTestText.Contains("voidBagMaterialNotExecutable") -and
        $autoTestText.Contains("BlueprintAutoPlacementSkippedVoidBagOnlyLayerCount") -and
        $autoTestText.Contains("BlueprintMaterialExecutionScope") -and
        $autoTestText.Contains("BlueprintAutoPlacementSubmitsActionQueueAndVerifiesPlacement") -and
        $autoTestText.Contains("BlueprintAutoPlacementUsesConfiguredReplacementMaterial") -and
        $programText.Contains("blueprint auto placement void bag only materials fail closed with reason")) {
        Write-Pass "Blueprint stage 10 targeted tests cover ActionQueue/replacement success paths and void-bag fail-closed path."
    }
    else {
        Write-FailHealth "Blueprint stage 10 must add and register targeted auto-placement execution and void-bag fail-closed tests."
    }

    if ($plan10Text -and $plan00Text -and $currentPlanIndexText -and
        $plan10Text.Contains("状态：已完成") -and
        $plan10Text.Contains('RuntimeVersion：`0.965-blueprint-autoplace-execution-chain`') -and
        $plan10Text.Contains("主背包 0-49") -and
        $plan10Text.Contains("voidBagMaterialNotExecutable") -and
        $plan10Text.Contains("不生成测试包") -and
        $plan10Text.Contains('未实现 `11`') -and
        $plan00Text.Contains("0.965-blueprint-autoplace-execution-chain") -and
        $plan00Text.Contains('下一唯一入口为 `11-') -and
        $currentPlanIndexText.Contains("0.965-blueprint-autoplace-execution-chain") -and
        $currentPlanIndexText.Contains('下一唯一入口为 `11-')) {
        Write-Pass "Blueprint feedback/autoplace stage 10 plan files and current plan index advance only to stage 11."
    }
    else {
        Write-FailHealth "Blueprint feedback/autoplace stage 10 plan files must mark 10 complete, record main-inventory/void-bag execution facts, no-package/no-11 scope, and advance only to 11."
    }

    if ($functionDocText -and $diagnosticsDocText -and $updateIndexText -and $updateRecordText -and $docHistoryIndexText -and $docHistoryRecordText -and
        $functionDocText.Contains("0.965-blueprint-autoplace-execution-chain") -and
        $functionDocText.Contains("mainInventory0-49") -and
        $functionDocText.Contains("voidBagMaterialNotExecutable") -and
        $functionDocText.Contains("BlueprintAutoPlacementSkippedVoidBagOnlyLayerCount") -and
        $diagnosticsDocText.Contains("0.965-blueprint-autoplace-execution-chain") -and
        $diagnosticsDocText.Contains("BlueprintAutoPlacementSkippedVoidBagOnlyLayerCount") -and
        $diagnosticsDocText.Contains("Test-BlueprintFeedbackStage10AutoplaceExecutionGovernance") -and
        $updateIndexText.Contains("0.965-自动放置执行链路治理") -and
        $updateRecordText.Contains('RuntimeVersion：`0.965-blueprint-autoplace-execution-chain`') -and
        $updateRecordText.Contains('未实现 `11`') -and
        $docHistoryIndexText.Contains("蓝图自动放置执行链路治理") -and
        $docHistoryRecordText.Contains("10-自动放置与同类替换执行链路.md")) {
        Write-Pass "Blueprint feedback/autoplace stage 10 function doc, diagnostics doc, update record, and document history are synchronized."
    }
    else {
        Write-FailHealth "Blueprint feedback/autoplace stage 10 must synchronize function doc, diagnostics doc, update record, and document-change history."
    }

    if ($auditText -and
        $auditText.Contains("Test-BlueprintFeedbackStage10AutoplaceExecutionGovernance") -and
        $auditText.Contains("0.965-blueprint-autoplace-execution-chain") -and
        $auditText.Contains("voidBagMaterialNotExecutable") -and
        $auditText.Contains("BlueprintAutoPlacementVoidBagOnlyMaterialsFailClosedWithReason")) {
        Write-Pass "Blueprint scoped health audit includes the stage 10 auto-placement execution governance check."
    }
    else {
        Write-FailHealth "Blueprint scoped health audit must include Test-BlueprintFeedbackStage10AutoplaceExecutionGovernance and execution-chain anchors."
    }
}

function Test-BlueprintFeedbackStage11RegressionDiagnosticsGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $runtimePath = Join-Path $RepoRoot "src\JueMingZ\Runtime\JueMingZRuntime.cs"
    $csprojPath = Join-Path $RepoRoot "src\JueMingZ\JueMingZ.csproj"
    $diagnosticsTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintDiagnosticsTests.cs"
    $autoTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintAutoPlacementTests.cs"
    $programPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.cs"
    $auditPath = Join-Path $RepoRoot "scripts\audit-project-health.ps1"
    $planDirectory = Get-BlueprintFeedbackAutoplacePlanDirectory -RepoRoot $RepoRoot
    $plan00Path = Join-Path $planDirectory "00-基准.md"
    $plan11Path = Join-Path $planDirectory "11-回归诊断与审计防线.md"
    $currentPlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "索引.md")
    $functionDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $diagnosticsDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "AI诊断日志说明.md")
    $updateIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "索引.md")
    $updateDirectory = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录")
    $docHistoryIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "索引.md")
    $docHistoryDirectory = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史")

    $updateRecordPath = ""
    if (Test-Path -LiteralPath $updateDirectory) {
        $record = Get-ChildItem -LiteralPath $updateDirectory -Filter "0.966-蓝图回归诊断审计防线-*.md" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($record) {
            $updateRecordPath = $record.FullName
        }
    }

    $docHistoryRecordPath = ""
    if (Test-Path -LiteralPath $docHistoryDirectory) {
        $record = Get-ChildItem -LiteralPath $docHistoryDirectory -Filter "蓝图回归诊断审计防线-*.md" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($record) {
            $docHistoryRecordPath = $record.FullName
        }
    }

    $runtimeText = Read-TextIfExists -Path $runtimePath
    $csprojText = Read-TextIfExists -Path $csprojPath
    $diagnosticsTestText = Read-TextIfExists -Path $diagnosticsTestPath
    $autoTestText = Read-TextIfExists -Path $autoTestPath
    $programText = Read-TextIfExists -Path $programPath
    $auditText = Read-TextIfExists -Path $auditPath
    $plan00Text = Read-TextIfExists -Path $plan00Path
    $plan11Text = Read-TextIfExists -Path $plan11Path
    $currentPlanIndexText = Read-TextIfExists -Path $currentPlanIndexPath
    $functionDocText = Read-TextIfExists -Path $functionDocPath
    $diagnosticsDocText = Read-TextIfExists -Path $diagnosticsDocPath
    $updateIndexText = Read-TextIfExists -Path $updateIndexPath
    $updateRecordText = if ([string]::IsNullOrWhiteSpace($updateRecordPath)) { $null } else { Read-TextIfExists -Path $updateRecordPath }
    $docHistoryIndexText = Read-TextIfExists -Path $docHistoryIndexPath
    $docHistoryRecordText = if ([string]::IsNullOrWhiteSpace($docHistoryRecordPath)) { $null } else { Read-TextIfExists -Path $docHistoryRecordPath }

    if (Test-BlueprintPlacementVersionMetadata -RuntimeText $runtimeText -CsprojText $csprojText -AllowedRuntimeVersions @("0.966-blueprint-regression-diagnostics-audit")) {
        Write-Pass "Blueprint feedback/autoplace stage 11 version metadata is synchronized to 0.966-blueprint-regression-diagnostics-audit."
    }
    else {
        Write-FailHealth "Blueprint feedback/autoplace stage 11 must synchronize RuntimeVersion and project metadata to 0.966-blueprint-regression-diagnostics-audit."
    }

    if ($diagnosticsTestText -and $autoTestText -and $programText -and
        $diagnosticsTestText.Contains("BlueprintFeedbackStage11RegressionDiagnosticsContractsStayWired") -and
        $diagnosticsTestText.Contains("BlueprintPlacementStage08RegressionDiagnosticsContractsStayWired") -and
        $diagnosticsTestText.Contains("FeatureCatalogExposesBlueprintEntryAsPlannedPlaceholder") -and
        $diagnosticsTestText.Contains("BlueprintHandheldActionBarStage04ButtonHitBoundsMatchVisibleRects") -and
        $diagnosticsTestText.Contains("BlueprintProjectionStage05CompletedProgressPersistsAndSkipsDugCells") -and
        $diagnosticsTestText.Contains("BlueprintMaterialsStage05SubtractCompletedProgressFromDemand") -and
        $diagnosticsTestText.Contains("BlueprintAutoPlacementSubmitsActionQueueAndVerifiesPlacement") -and
        $diagnosticsTestText.Contains("BlueprintAutoPlacementUsesConfiguredReplacementMaterial") -and
        $diagnosticsTestText.Contains("BlueprintAutoPlacementVoidBagOnlyMaterialsFailClosedWithReason") -and
        $diagnosticsTestText.Contains("BlueprintAutoPlacementReplacementFailClosedWhenDisabledOrWrongCategory") -and
        $diagnosticsTestText.Contains("BlueprintAutoPlacementDiagnosticsWriteRuntimeSnapshotJson") -and
        $autoTestText.Contains("BlueprintMaterialExecutionScope") -and
        $autoTestText.Contains("voidBagMaterialNotExecutable") -and
        $programText.Contains("blueprint feedback stage 11 regression diagnostics contracts stay wired")) {
        Write-Pass "Blueprint stage 11 aggregate regression test covers stage 02-10 UI, diagnostics, ActionQueue, replacement, and void-bag contracts."
    }
    else {
        Write-FailHealth "Blueprint stage 11 must add and register an aggregate regression over stage 02-10 UI, diagnostics, ActionQueue, replacement, and void-bag contracts."
    }

    if ($plan11Text -and $plan00Text -and $currentPlanIndexText -and
        $plan11Text.Contains("状态：已完成") -and
        $plan11Text.Contains('RuntimeVersion：`0.966-blueprint-regression-diagnostics-audit`') -and
        $plan11Text.Contains("BlueprintFeedbackStage11RegressionDiagnosticsContractsStayWired") -and
        $plan11Text.Contains("Test-BlueprintFeedbackStage11RegressionDiagnosticsGovernance") -and
        $plan11Text.Contains("不生成测试包") -and
        $plan11Text.Contains('未实现 `12`') -and
        $plan00Text.Contains("0.966-blueprint-regression-diagnostics-audit") -and
        $plan00Text.Contains('下一唯一入口为 `12-') -and
        $currentPlanIndexText.Contains("0.966-blueprint-regression-diagnostics-audit") -and
        $currentPlanIndexText.Contains('下一唯一入口为 `12-')) {
        Write-Pass "Blueprint feedback/autoplace stage 11 plan files and current plan index advance only to stage 12."
    }
    else {
        Write-FailHealth "Blueprint feedback/autoplace stage 11 plan files must mark 11 complete, record aggregate/audit anchors, no-package/no-12 scope, and advance only to 12."
    }

    if ($functionDocText -and $diagnosticsDocText -and $updateIndexText -and $updateRecordText -and $docHistoryIndexText -and $docHistoryRecordText -and
        $functionDocText.Contains("0.966-blueprint-regression-diagnostics-audit") -and
        $functionDocText.Contains("BlueprintFeedbackStage11RegressionDiagnosticsContractsStayWired") -and
        $functionDocText.Contains("Test-BlueprintFeedbackStage11RegressionDiagnosticsGovernance") -and
        $diagnosticsDocText.Contains("0.966-blueprint-regression-diagnostics-audit") -and
        $diagnosticsDocText.Contains("BlueprintFeedbackStage11RegressionDiagnosticsContractsStayWired") -and
        $diagnosticsDocText.Contains("Test-BlueprintFeedbackStage11RegressionDiagnosticsGovernance") -and
        $diagnosticsDocText.Contains("BlueprintAutoPlacementSkippedVoidBagOnlyLayerCount") -and
        $updateIndexText.Contains("0.966-蓝图回归诊断审计防线") -and
        $updateRecordText.Contains('RuntimeVersion：`0.966-blueprint-regression-diagnostics-audit`') -and
        $updateRecordText.Contains("BlueprintFeedbackStage11RegressionDiagnosticsContractsStayWired") -and
        $updateRecordText.Contains("Test-BlueprintFeedbackStage11RegressionDiagnosticsGovernance") -and
        $updateRecordText.Contains('未实现 `12`') -and
        $docHistoryIndexText.Contains("蓝图回归诊断审计防线") -and
        $docHistoryRecordText.Contains("11-回归诊断与审计防线.md")) {
        Write-Pass "Blueprint feedback/autoplace stage 11 function doc, diagnostics doc, update record, and document history are synchronized."
    }
    else {
        Write-FailHealth "Blueprint feedback/autoplace stage 11 must synchronize function doc, diagnostics doc, update record, and document-change history."
    }

    if ($auditText -and
        $auditText.Contains("Test-BlueprintFeedbackStage11RegressionDiagnosticsGovernance") -and
        $auditText.Contains("0.966-blueprint-regression-diagnostics-audit") -and
        $auditText.Contains("BlueprintFeedbackStage11RegressionDiagnosticsContractsStayWired") -and
        $auditText.Contains("Test-BlueprintFeedbackStage10AutoplaceExecutionGovernance")) {
        Write-Pass "Blueprint scoped health audit includes the stage 11 regression diagnostics governance check."
    }
    else {
        Write-FailHealth "Blueprint scoped health audit must include Test-BlueprintFeedbackStage11RegressionDiagnosticsGovernance and regression diagnostics anchors."
    }
}

function Test-BlueprintFeedbackAutoplaceStage12CloseoutGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $auditPath = Join-Path $RepoRoot "scripts\audit-project-health.ps1"
    $csprojPath = Join-Path $RepoRoot "src\JueMingZ\JueMingZ.csproj"
    $runtimePath = Join-Path $RepoRoot "src\JueMingZ\Runtime\JueMingZRuntime.cs"
    $currentPlanDirectory = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "蓝图实机反馈与自动放置治理")
    $archivePlanDirectory = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图实机反馈与自动放置治理")
    $plan00Path = Join-BlueprintFeedbackAutoplacePlanPath -RepoRoot $RepoRoot -Leaf "00-基准.md"
    $plan12Path = Join-BlueprintFeedbackAutoplacePlanPath -RepoRoot $RepoRoot -Leaf "12-验证打包与归档收口.md"
    $currentPlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "索引.md")
    $archivePlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "索引.md")
    $functionDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $diagnosticsDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "AI诊断日志说明.md")
    $updateIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "索引.md")
    $updateRecordPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "0.967-蓝图反馈自动放置验证收口-2606241619.md")
    $docHistoryIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "索引.md")
    $docHistoryRecordPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "蓝图反馈自动放置验证收口-2606241619.md")

    $auditText = Read-TextIfExists -Path $auditPath
    $csprojText = Read-TextIfExists -Path $csprojPath
    $runtimeText = Read-TextIfExists -Path $runtimePath
    $plan00Text = Read-TextIfExists -Path $plan00Path
    $plan12Text = Read-TextIfExists -Path $plan12Path
    $currentPlanIndexText = Read-TextIfExists -Path $currentPlanIndexPath
    $archivePlanIndexText = Read-TextIfExists -Path $archivePlanIndexPath
    $functionDocText = Read-TextIfExists -Path $functionDocPath
    $diagnosticsDocText = Read-TextIfExists -Path $diagnosticsDocPath
    $updateIndexText = Read-TextIfExists -Path $updateIndexPath
    $updateRecordText = Read-TextIfExists -Path $updateRecordPath
    $docHistoryIndexText = Read-TextIfExists -Path $docHistoryIndexPath
    $docHistoryRecordText = Read-TextIfExists -Path $docHistoryRecordPath

    if (Test-BlueprintPlacementVersionMetadata -RuntimeText $runtimeText -CsprojText $csprojText -AllowedRuntimeVersions @("0.967-blueprint-feedback-autoplace-closeout")) {
        Write-Pass "Blueprint feedback/autoplace stage 12 closeout version metadata is synchronized to 0.967-blueprint-feedback-autoplace-closeout."
    }
    else {
        Write-FailHealth "Blueprint feedback/autoplace stage 12 must synchronize RuntimeVersion and project metadata to 0.967-blueprint-feedback-autoplace-closeout."
    }

    if ((Test-Path -LiteralPath $archivePlanDirectory) -and
        -not (Test-Path -LiteralPath $currentPlanDirectory) -and
        $plan00Text -and
        $plan00Text.Contains('状态：`12-验证打包与归档收口` 已完成') -and
        $plan00Text.Contains("0.967-blueprint-feedback-autoplace-closeout") -and
        $plan00Text.Contains("自动接力已终止") -and
        $plan12Text -and
        $plan12Text.Contains("状态：已完成") -and
        $plan12Text.Contains("0.967-blueprint-feedback-autoplace-closeout") -and
        $plan12Text.Contains("JueMingZ-TestPackage") -and
        $plan12Text.Contains("-RequireFreshTestPackage") -and
        $plan12Text.Contains("不生成源码包") -and
        $plan12Text.Contains("不创建后续")) {
        Write-Pass "Blueprint feedback/autoplace plan is archived with stage 12 package delivery, strict freshness audit, and relay termination recorded."
    }
    else {
        Write-FailHealth "Blueprint feedback/autoplace stage 12 must archive the plan and mark 00/12 complete with package, strict freshness audit, no source package, and no-next-thread scope."
    }

    if ($currentPlanIndexText -and
        $currentPlanIndexText.Contains("当前没有正在推进的计划") -and
        $currentPlanIndexText.Contains("文档/归档历史计划/蓝图实机反馈与自动放置治理/") -and
        $currentPlanIndexText.Contains("0.967-blueprint-feedback-autoplace-closeout") -and
        $currentPlanIndexText.Contains("自动接力已终止") -and
        $archivePlanIndexText -and
        $archivePlanIndexText.Contains("文档/归档历史计划/蓝图实机反馈与自动放置治理/") -and
        $archivePlanIndexText.Contains("0.967-blueprint-feedback-autoplace-closeout") -and
        $archivePlanIndexText.Contains("JueMingZ-TestPackage")) {
        Write-Pass "Blueprint feedback/autoplace current and archive plan indexes record the stage 12 closeout and relay termination."
    }
    else {
        Write-FailHealth "Blueprint feedback/autoplace stage 12 must remove the plan from current work and add the 0.967 archived closeout summary."
    }

    if ($functionDocText -and
        $functionDocText.Contains("0.967-blueprint-feedback-autoplace-closeout") -and
        $functionDocText.Contains("文档/归档历史计划/蓝图实机反馈与自动放置治理/00-基准.md") -and
        $functionDocText.Contains("JueMingZ-TestPackage") -and
        $functionDocText.Contains("严格新鲜包健康审计") -and
        $diagnosticsDocText -and
        $diagnosticsDocText.Contains("0.967-blueprint-feedback-autoplace-closeout") -and
        $diagnosticsDocText.Contains("不新增 runtime snapshot 字段") -and
        $diagnosticsDocText.Contains("不新增 trace JSONL")) {
        Write-Pass "Blueprint feature and diagnostics docs record the 0.967 closeout without expanding runtime or diagnostics scope."
    }
    else {
        Write-FailHealth "Blueprint feedback/autoplace stage 12 must update blueprint feature and diagnostics docs with package closeout and no-new-diagnostics scope."
    }

    if ($updateIndexText -and
        $updateIndexText.Contains("0.967-蓝图反馈自动放置验证收口-2606241619.md") -and
        $updateRecordText -and
        $updateRecordText.Contains('RuntimeVersion：`0.967-blueprint-feedback-autoplace-closeout`') -and
        $updateRecordText.Contains("JueMingZ-TestPackage") -and
        $updateRecordText.Contains("-RequireFreshTestPackage") -and
        $updateRecordText.Contains("不生成源码包") -and
        $docHistoryIndexText -and
        $docHistoryIndexText.Contains("蓝图反馈自动放置验证收口-2606241619.md") -and
        $docHistoryRecordText -and
        $docHistoryRecordText.Contains("0.967-blueprint-feedback-autoplace-closeout") -and
        $docHistoryRecordText.Contains("蓝图实机反馈与自动放置治理/12")) {
        Write-Pass "Blueprint feedback/autoplace stage 12 update record and document-change history are synchronized."
    }
    else {
        Write-FailHealth "Blueprint feedback/autoplace stage 12 must synchronize update index/record and document-change history for the 0.967 closeout."
    }

    if ($auditText -and
        $auditText.Contains("Test-BlueprintFeedbackAutoplaceStage12CloseoutGovernance") -and
        $auditText.Contains("0.967-blueprint-feedback-autoplace-closeout") -and
        $auditText.Contains("Join-BlueprintFeedbackAutoplacePlanPath")) {
        Write-Pass "Blueprint scoped health audit includes the stage 12 closeout governance check and archived-plan path resolution."
    }
    else {
        Write-FailHealth "Blueprint scoped health audit must include Test-BlueprintFeedbackAutoplaceStage12CloseoutGovernance and archived-plan path resolution."
    }
}

function Test-BlueprintLibraryStage10Governance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $libraryTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintLibraryTests.cs"
    $programPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.cs"
    $auditPath = Join-Path $RepoRoot "scripts\audit-project-health.ps1"
    $functionDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $diagnosticsDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "AI诊断日志说明.md")
    $plan10Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "蓝图创建与蓝图库完善", "10-诊断测试文档审计.md")
    if (-not (Test-Path -LiteralPath $plan10Path)) {
        $plan10Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图创建与蓝图库完善", "10-诊断测试文档审计.md")
    }
    $plan00Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "蓝图创建与蓝图库完善", "00-基准.md")
    if (-not (Test-Path -LiteralPath $plan00Path)) {
        $plan00Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图创建与蓝图库完善", "00-基准.md")
    }
    $updateIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "索引.md")
    $updateDirectory = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录")
    $docHistoryIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "索引.md")
    $docHistoryDirectory = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史")

    $updateRecordPath = ""
    if (Test-Path -LiteralPath $updateDirectory) {
        $record = Get-ChildItem -LiteralPath $updateDirectory -Filter "0.898-蓝图库诊断测试文档审计-*.md" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($record) {
            $updateRecordPath = $record.FullName
        }
    }

    $docHistoryRecordPath = ""
    if (Test-Path -LiteralPath $docHistoryDirectory) {
        $record = Get-ChildItem -LiteralPath $docHistoryDirectory -Filter "蓝图库诊断测试文档审计-*.md" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($record) {
            $docHistoryRecordPath = $record.FullName
        }
    }

    $libraryTestText = Read-TextIfExists -Path $libraryTestPath
    $programText = Read-TextIfExists -Path $programPath
    $auditText = Read-TextIfExists -Path $auditPath
    $functionDocText = Read-TextIfExists -Path $functionDocPath
    $diagnosticsDocText = Read-TextIfExists -Path $diagnosticsDocPath
    $plan10Text = Read-TextIfExists -Path $plan10Path
    $plan00Text = Read-TextIfExists -Path $plan00Path
    $updateIndexText = Read-TextIfExists -Path $updateIndexPath
    $updateRecordText = if ([string]::IsNullOrWhiteSpace($updateRecordPath)) { $null } else { Read-TextIfExists -Path $updateRecordPath }
    $docHistoryIndexText = Read-TextIfExists -Path $docHistoryIndexPath
    $docHistoryRecordText = if ([string]::IsNullOrWhiteSpace($docHistoryRecordPath)) { $null } else { Read-TextIfExists -Path $docHistoryRecordPath }

    if ($libraryTestText -and
        $libraryTestText.Contains("BlueprintLibraryStage10DiagnosticsAuditContractsStayWired") -and
        $libraryTestText.Contains("BlueprintHandheldActionBarOverlayStaysUiOnlyAndNoScan") -and
        $libraryTestText.Contains("BlueprintHandheldActionBarInputCapturesOnlyInsideBar") -and
        $libraryTestText.Contains("BlueprintHandheldActionBarDynamicButtonMatrix") -and
        $libraryTestText.Contains("BlueprintCreateActionButtonSyncsExitStateWithSharedToggle") -and
        $libraryTestText.Contains("BlueprintLibrarySubmenuAndShortcutRowsOpenSameUiState") -and
        $libraryTestText.Contains("BlueprintLibraryTwoColumnCardsPreviewAndLayoutButtons") -and
        $libraryTestText.Contains("BlueprintLibraryStage07NamingRenameDeleteConfirmKeepsInstances") -and
        $libraryTestText.Contains("BlueprintLibraryStage08ImportExportDiagnostics") -and
        $libraryTestText.Contains("BlueprintLibraryStage09UseSnapshotAndInstanceBoundary") -and
        $programText.Contains("blueprint library stage 10 diagnostics audit contracts stay wired")) {
        Write-Pass "Blueprint library stage 10 keeps an aggregate console regression covering handheld input/no-scan, create-state matrix, library management, import/export, and instance snapshot boundaries."
    }
    else {
        Write-FailHealth "Blueprint library stage 10 must keep the aggregate console regression registered and wired to the 02-09 contract tests."
    }

    if ($auditText -and
        $auditText.Contains("Test-BlueprintLibraryStage10Governance") -and
        $auditText.Contains("0.898-蓝图库诊断测试文档审计") -and
        $auditText.Contains("BlueprintLibraryStage10DiagnosticsAuditContractsStayWired") -and
        $auditText.Contains("layout-use") -and
        $auditText.Contains("previewStarted") -and
        $auditText.Contains("未新增 AI 经验笔记")) {
        Write-Pass "Blueprint library stage 10 health audit locks the aggregate regression, layout-use diagnostics, and no-new-experience-note closeout wording."
    }
    else {
        Write-FailHealth "Blueprint library stage 10 health audit must lock the aggregate regression, layout-use previewStarted diagnostics, and experience-note review wording."
    }

    if ($functionDocText -and
        $functionDocText.Contains("BlueprintLibraryStage10DiagnosticsAuditContractsStayWired") -and
        $functionDocText.Contains("layout-use") -and
        $functionDocText.Contains("previewStarted") -and
        $functionDocText.Contains("不生成测试包") -and
        $diagnosticsDocText -and
        $diagnosticsDocText.Contains("Test-BlueprintLibraryStage10Governance") -and
        $diagnosticsDocText.Contains("BlueprintLibraryStage10DiagnosticsAuditContractsStayWired") -and
        $diagnosticsDocText.Contains("implemented:true") -and
        $diagnosticsDocText.Contains("placeholderOnly:false") -and
        $diagnosticsDocText.Contains('resultCode:"previewStarted"')) {
        Write-Pass "Blueprint function and diagnostics docs describe the stage 10 aggregate audit and current layout-use preview metadata contract."
    }
    else {
        Write-FailHealth "Blueprint function and diagnostics docs must describe the stage 10 aggregate audit, no package status, and layout-use previewStarted metadata."
    }

    $plan00Stage10Ok = $plan00Text -and
        $plan00Text.Contains("10-诊断测试文档审计.md") -and
        $plan00Text.Contains('已完成 `BlueprintLibraryStage10DiagnosticsAuditContractsStayWired`') -and
        (
            ($plan00Text.Contains('等待 `11-验证与收口.md` 接力') -and
                $plan00Text.Contains("0.898-blueprint-library-diagnostics-audit")) -or
            ($plan00Text.Contains('状态：`11-验证与收口` 已完成') -and
                $plan00Text.Contains("0.899-blueprint-library-closeout"))
        )

    if ($plan10Text -and
        $plan10Text.Contains("状态：已完成") -and
        $plan10Text.Contains("BlueprintLibraryStage10DiagnosticsAuditContractsStayWired") -and
        $plan10Text.Contains("Test-BlueprintLibraryStage10Governance") -and
        $plan10Text.Contains("未新增 AI 经验笔记") -and
        $plan00Stage10Ok) {
        Write-Pass "Blueprint library stage 10 plan files record completion, aggregate test, health audit, and no-new-experience-note review."
    }
    else {
        Write-FailHealth "Blueprint library stage 10 plan files must record completion, aggregate regression, health audit, and experience-note review."
    }

    if ($updateIndexText -and
        $updateIndexText.Contains("0.898-蓝图库诊断测试文档审计") -and
        $updateRecordText -and
        $updateRecordText.Contains("0.898-blueprint-library-diagnostics-audit") -and
        $updateRecordText.Contains("BlueprintLibraryStage10DiagnosticsAuditContractsStayWired") -and
        $updateRecordText.Contains("Test-BlueprintLibraryStage10Governance") -and
        $updateRecordText.Contains("未新增 AI 经验笔记") -and
        $docHistoryIndexText -and
        $docHistoryIndexText.Contains("蓝图库诊断测试文档审计") -and
        $docHistoryRecordText -and
        $docHistoryRecordText.Contains("BlueprintLibraryStage10DiagnosticsAuditContractsStayWired") -and
        $docHistoryRecordText.Contains("Test-BlueprintLibraryStage10Governance")) {
        Write-Pass "Blueprint library stage 10 update record and document history are synchronized."
    }
    else {
        Write-FailHealth "Blueprint library stage 10 must synchronize update index/record and document history with the aggregate test and health audit."
    }
}

function Test-BlueprintLibraryStage11CloseoutGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $auditPath = Join-Path $RepoRoot "scripts\audit-project-health.ps1"
    $currentPlanDirPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "蓝图创建与蓝图库完善")
    $archivedPlan00Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图创建与蓝图库完善", "00-基准.md")
    $archivedPlan11Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图创建与蓝图库完善", "11-验证与收口.md")
    $currentPlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "索引.md")
    $archivePlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "索引.md")
    $blueprintDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $diagnosticsDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "AI诊断日志说明.md")
    $updateIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "索引.md")
    $updateRecordPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "0.899-蓝图库验证收口-2606211953.md")
    $docHistoryIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "索引.md")
    $docHistoryRecordPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "蓝图库验证收口-2606211953.md")

    $auditText = Read-TextIfExists -Path $auditPath
    $plan00Text = Read-TextIfExists -Path $archivedPlan00Path
    $plan11Text = Read-TextIfExists -Path $archivedPlan11Path
    $currentPlanIndexText = Read-TextIfExists -Path $currentPlanIndexPath
    $archivePlanIndexText = Read-TextIfExists -Path $archivePlanIndexPath
    $blueprintDocText = Read-TextIfExists -Path $blueprintDocPath
    $diagnosticsDocText = Read-TextIfExists -Path $diagnosticsDocPath
    $updateIndexText = Read-TextIfExists -Path $updateIndexPath
    $updateRecordText = Read-TextIfExists -Path $updateRecordPath
    $docHistoryIndexText = Read-TextIfExists -Path $docHistoryIndexPath
    $docHistoryRecordText = Read-TextIfExists -Path $docHistoryRecordPath

    if ($auditText -and
        $auditText.Contains("Test-BlueprintLibraryStage11CloseoutGovernance") -and
        $auditText.Contains("0.899-blueprint-library-closeout") -and
        $auditText.Contains("0.899-蓝图库验证收口-2606211953.md")) {
        Write-Pass "Blueprint library stage-11 closeout health audit is present and wired to the 0.899 closeout contract."
    }
    else {
        Write-FailHealth "Blueprint library stage-11 closeout health audit must lock the 0.899 closeout contract and update record."
    }

    if (-not (Test-Path -LiteralPath $currentPlanDirPath) -and
        $plan00Text -and
        $plan00Text.Contains('状态：`11-验证与收口` 已完成') -and
        $plan00Text.Contains("0.899-blueprint-library-closeout") -and
        $plan00Text.Contains("自动串行接力终止") -and
        $plan11Text -and
        $plan11Text.Contains("状态：已完成") -and
        $plan11Text.Contains("0.899-blueprint-library-closeout") -and
        $plan11Text.Contains("JueMingZ-TestPackage") -and
        $plan11Text.Contains("严格新鲜包健康审计") -and
        $plan11Text.Contains("不创建后续")) {
        Write-Pass "Blueprint library plan is archived with the 0.899 closeout, package delivery, and no further handoff."
    }
    else {
        Write-FailHealth "Stage-11 closeout must move the blueprint library plan to archive and mark 11 complete with package/fresh-audit scope."
    }

    if ($currentPlanIndexText -and
        $currentPlanIndexText.Contains("当前没有正在推进的计划") -and
        $currentPlanIndexText.Contains("文档/归档历史计划/蓝图创建与蓝图库完善/") -and
        $archivePlanIndexText -and
        $archivePlanIndexText.Contains("文档/归档历史计划/蓝图创建与蓝图库完善/") -and
        $archivePlanIndexText.Contains("0.899-blueprint-library-closeout") -and
        $archivePlanIndexText.Contains("自动接力已终止")) {
        Write-Pass "Current and archived plan indices record the blueprint library closeout and relay termination."
    }
    else {
        Write-FailHealth "Stage-11 closeout must update current and archived plan indices."
    }

    if ($blueprintDocText -and
        $blueprintDocText.Contains("0.899-blueprint-library-closeout") -and
        $blueprintDocText.Contains("文档/归档历史计划/蓝图创建与蓝图库完善/00-基准.md") -and
        $blueprintDocText.Contains("不新增用户可见蓝图运行时行为") -and
        $diagnosticsDocText -and
        $diagnosticsDocText.Contains("0.899-blueprint-library-closeout") -and
        $diagnosticsDocText.Contains("不新增诊断字段")) {
        Write-Pass "Blueprint feature and diagnostics docs record the 0.899 closeout without expanding runtime or diagnostics scope."
    }
    else {
        Write-FailHealth "Stage-11 closeout must update blueprint feature and diagnostics docs with the no-new-runtime/no-new-diagnostics scope."
    }

    if ($updateIndexText -and
        $updateIndexText.Contains("0.899-蓝图库验证收口-2606211953.md") -and
        $updateRecordText -and
        $updateRecordText.Contains('RuntimeVersion：`0.899-blueprint-library-closeout`') -and
        $updateRecordText.Contains("JueMingZ-TestPackage") -and
        $updateRecordText.Contains("-RequireFreshTestPackage") -and
        $docHistoryIndexText -and
        $docHistoryIndexText.Contains("蓝图库验证收口-2606211953.md") -and
        $docHistoryRecordText -and
        $docHistoryRecordText.Contains("0.899-blueprint-library-closeout")) {
        Write-Pass "Stage-11 update record and document-change history are synchronized."
    }
    else {
        Write-FailHealth "Stage-11 update record, update index, and document-change history must reference the 0.899 closeout."
    }
}

function Test-BlueprintFeedbackStage04Governance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $dispatcherPath = Join-Path $RepoRoot "src\JueMingZ\Runtime\RuntimeAutomationDispatcher.cs"
    $entryHotkeyPath = Join-Path $RepoRoot "src\JueMingZ\Input\BlueprintEntryHotkeyService.cs"
    $configServicePath = Join-Path $RepoRoot "src\JueMingZ\Config\ConfigService.cs"
    $conflictPath = Join-Path $RepoRoot "src\JueMingZ\Config\FeatureToggleHotkeyConflictRegistry.cs"
    $entryStatePath = Join-Path $RepoRoot "src\JueMingZ\Automation\Blueprint\BlueprintEntryState.cs"
    $stateApiPath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyMainWindow.StateApi.cs"
    $hotkeyPath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyMainWindow.Blueprint.Hotkey.cs"
    $autoMiningInputPath = Join-Path $RepoRoot "src\JueMingZ\Automation\WorldAutomation\AutoMiningHotkeyInput.cs"
    $autoMiningModelsPath = Join-Path $RepoRoot "src\JueMingZ\Automation\WorldAutomation\AutoMiningModels.cs"
    $autoMiningServicePath = Join-Path $RepoRoot "src\JueMingZ\Automation\WorldAutomation\AutoMiningService.cs"
    $autoCaptureServicePath = Join-Path $RepoRoot "src\JueMingZ\Automation\WorldAutomation\AutoCaptureCritterService.cs"
    $scenarioNamesPath = Join-Path $RepoRoot "src\JueMingZ\Common\ScenarioNames.cs"
    $snapshotPath = Join-Path $RepoRoot "src\JueMingZ\Diagnostics\DiagnosticSnapshot.cs"
    $snapshotWriterPath = Join-Path $RepoRoot "src\JueMingZ\Diagnostics\DiagnosticSnapshotWriter.Json.cs"
    $snapshotBuilderPath = Join-Path $RepoRoot "src\JueMingZ\Runtime\Diagnostics\RuntimeDiagnosticSnapshotBuilder.InventoryInformationFishing.cs"
    $blueprintTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintEntryTests.cs"
    $worldTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.WorldAutomationActionTests.cs"
    $programPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.cs"
    $blueprintDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $autoMiningDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "杂项页", "自动挖矿.md")
    $autoCaptureDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "物品页", "自动捕捉.md")
    $diagnosticsDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "AI诊断日志说明.md")
    $plan04Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "蓝图功能实机反馈修补", "04-入口热键清理与自动挖矿采集键修复.md")
    if (-not (Test-Path -LiteralPath $plan04Path)) {
        $plan04Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图功能实机反馈修补", "04-入口热键清理与自动挖矿采集键修复.md")
    }
    $plan05Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "蓝图功能实机反馈修补", "05-自动挖矿自动模式与自动捕捉背包门禁.md")
    if (-not (Test-Path -LiteralPath $plan05Path)) {
        $plan05Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图功能实机反馈修补", "05-自动挖矿自动模式与自动捕捉背包门禁.md")
    }

    $dispatcherText = Read-TextIfExists -Path $dispatcherPath
    $entryHotkeyText = Read-TextIfExists -Path $entryHotkeyPath
    $configServiceText = Read-TextIfExists -Path $configServicePath
    $conflictText = Read-TextIfExists -Path $conflictPath
    $entryStateText = Read-TextIfExists -Path $entryStatePath
    $stateApiText = Read-TextIfExists -Path $stateApiPath
    $hotkeyText = Read-TextIfExists -Path $hotkeyPath
    $autoMiningInputText = Read-TextIfExists -Path $autoMiningInputPath
    $autoMiningModelsText = Read-TextIfExists -Path $autoMiningModelsPath
    $autoMiningServiceText = Read-TextIfExists -Path $autoMiningServicePath
    $autoCaptureServiceText = Read-TextIfExists -Path $autoCaptureServicePath
    $scenarioNamesText = Read-TextIfExists -Path $scenarioNamesPath
    $snapshotText = Read-TextIfExists -Path $snapshotPath
    $snapshotWriterText = Read-TextIfExists -Path $snapshotWriterPath
    $snapshotBuilderText = Read-TextIfExists -Path $snapshotBuilderPath
    $blueprintTestText = Read-TextIfExists -Path $blueprintTestPath
    $worldTestText = Read-TextIfExists -Path $worldTestPath
    $programText = Read-TextIfExists -Path $programPath
    $blueprintDocText = Read-TextIfExists -Path $blueprintDocPath
    $autoMiningDocText = Read-TextIfExists -Path $autoMiningDocPath
    $autoCaptureDocText = Read-TextIfExists -Path $autoCaptureDocPath
    $diagnosticsDocText = Read-TextIfExists -Path $diagnosticsDocPath
    $plan04Text = Read-TextIfExists -Path $plan04Path
    $plan05Text = Read-TextIfExists -Path $plan05Path

    if ($dispatcherText -and
        -not $dispatcherText.Contains("targeting.blueprint-entry-hotkey") -and
        $dispatcherText.Contains("targeting.blueprint-action-hotkeys") -and
        $dispatcherText.Contains("BlueprintEntryHotkeyService.Tick(") -and
        $entryHotkeyText -and
        $entryHotkeyText.Contains("HasActiveBinding") -and
        $entryHotkeyText.Contains("FeatureIds.BlueprintCreateAction") -and
        $entryHotkeyText.Contains("FeatureIds.BlueprintSaveAction") -and
        $entryHotkeyText.Contains("FeatureIds.BlueprintLibraryAction") -and
        $entryHotkeyText.Contains("BlueprintLibraryUiState.OpenLibrary") -and
        $entryHotkeyText.Contains("directEntryHotkeyDisabled") -and
        -not $entryHotkeyText.Contains("FeatureIds.BlueprintMain") -and
        -not $entryHotkeyText.Contains("OpenEntryHotkey") -and
        $configServiceText -and
        $configServiceText.Contains("RemoveBlueprintEntryHotkey") -and
        $configServiceText.Contains("hotkeys.Remove(FeatureIds.BlueprintMain)") -and
        $entryStateText -and
        $entryStateText.Contains("directEntryHotkeyDisabled")) {
        Write-Pass "Blueprint direct-entry hotkey runtime path stays disabled while action hotkeys use blueprint.create/blueprint.save/blueprint.library only."
    }
    else {
        Write-FailHealth "Blueprint stage-04/05 must keep old targeting.blueprint-entry-hotkey/blueprint.main direct-open disabled while allowing only blueprint action hotkeys."
    }

    if ($conflictText -and
        -not $conflictText.Contains("TryFindBlueprintEntryConflict") -and
        -not $conflictText.Contains("BlueprintEntry,") -and
        $stateApiText.Contains("直接打开蓝图页快捷键已停用") -and
        $hotkeyText.Contains("蓝图页直接打开快捷键已停用") -and
        $blueprintTestText.Contains("BlueprintDirectEntryHotkeyIsDisabled") -and
        $blueprintTestText.Contains("BlueprintDirectEntryHotkeyIsNotAConflictSource") -and
        $programText -and
        $programText.Contains("blueprint direct entry hotkey is disabled")) {
        Write-Pass "Blueprint direct-entry capture/save/conflict surfaces stay disabled and covered by console tests."
    }
    else {
        Write-FailHealth "Blueprint stage-04 must reject direct-entry capture/save attempts and remove the old conflict source, with regression tests registered."
    }

    if ($autoMiningModelsText -and
        $autoMiningModelsText.Contains("AutoMiningHotkeyInputResult") -and
        $autoMiningModelsText.Contains("AutoMiningDiagnostics") -and
        $autoMiningInputText -and
        $autoMiningInputText.Contains("ConsumePressedForTesting") -and
        $autoMiningInputText.Contains("gameInputUnavailable") -and
        $autoMiningInputText.Contains("notForeground") -and
        $autoMiningInputText.Contains("textInputFocused") -and
        $autoMiningServiceText -and
        $autoMiningServiceText.Contains("RecordHotkeyDecision") -and
        $autoMiningServiceText.Contains("RecordRuntimeHotkeyGateSkipped") -and
        $scenarioNamesText.Contains("WorldAutomationAutoMiningHotkey")) {
        Write-Pass "Auto mining Hotkey trigger uses structured input results and records blocked reasons without touching Auto mode."
    }
    else {
        Write-FailHealth "Auto mining stage-04 Hotkey trigger must expose structured results, blocked reasons, and hotkey diagnostics."
    }

    $requiredAutoMiningFields = @(
        "AutoMiningLastDecision",
        "AutoMiningLastDecisionUtc",
        "AutoMiningLastHotkey",
        "AutoMiningLastHotkeyResultCode",
        "AutoMiningLastHotkeyBlockedReason",
        "AutoMiningLastHotkeyDecisionUtc"
    )
    $missingAutoMiningFields = @()
    foreach ($field in $requiredAutoMiningFields) {
        if (-not ($snapshotText.Contains($field) -and $snapshotWriterText.Contains($field) -and $snapshotBuilderText.Contains($field))) {
            $missingAutoMiningFields += $field
        }
    }

    if ($missingAutoMiningFields.Count -eq 0 -and
        $worldTestText.Contains("AutoMiningHotkeyInputTriggersAndDebounces") -and
        $worldTestText.Contains("AutoMiningHotkeyInputReportsBlockedReasons") -and
        $programText -and
        $programText.Contains("auto mining hotkey input reports blocked reasons")) {
        Write-Pass "Auto mining Hotkey blocked-reason snapshot fields and console tests are wired."
    }
    else {
        Write-FailHealth "Auto mining stage-04 diagnostics/tests missing fields or registrations: $($missingAutoMiningFields -join ', ')"
    }

    if ($blueprintDocText -and
        $blueprintDocText.Contains("directEntryHotkeyDisabled") -and
        $blueprintDocText.Contains("blueprint.main") -and
        $autoMiningDocText -and
        $autoMiningDocText.Contains("AutoMiningLastHotkeyBlockedReason") -and
        $diagnosticsDocText -and
        $diagnosticsDocText.Contains("AutoMiningLastHotkeyBlockedReason") -and
        $plan04Text -and
        $plan04Text.Contains("0.875-blueprint-hotkey-automining-hotkey")) {
        Write-Pass "Blueprint/auto-mining function docs, diagnostics rules, and stage-04 plan describe the hotkey cleanup."
    }
    else {
        Write-FailHealth "Stage-04 docs must describe directEntryHotkeyDisabled, auto-mining Hotkey blocked-reason diagnostics, and the 0.875 completion record."
    }

    if ($autoMiningServiceText -and
        $autoMiningServiceText.Contains("ObserveManualTileMined") -and
        $autoMiningServiceText.Contains("TryHandleManualMiningSelection") -and
        $autoMiningServiceText.Contains("ResetForTesting") -and
        $worldTestText.Contains("AutoMiningAutoModeObservationSubmitsSustainedRequest") -and
        $programText.Contains("auto mining auto mode observation submits sustained request")) {
        Write-Pass "Auto mining Auto mode PickTile observation is covered by a sustained-request regression test."
    }
    else {
        Write-FailHealth "Stage-05 must keep Auto mining Auto-mode PickTile observation wired to selection and covered by a sustained-request console test."
    }

    if ($autoCaptureServiceText -and
        $autoCaptureServiceText.Contains("CanRunManualCaptureWithInventoryOpen") -and
        $autoCaptureServiceText.Contains("AutoCaptureCritterModes.Manual") -and
        $autoCaptureServiceText.Contains("bugNet.Slot >= 0 && bugNet.Slot < 10") -and
        $worldTestText.Contains("AutoCaptureCritterManualInventoryOpenRequiresSelectedHotbarBugNet") -and
        $programText.Contains("auto capture critter manual inventory open requires selected hotbar bug net")) {
        Write-Pass "Auto capture inventory-open gate is limited to Manual mode with a selected hotbar bug net and covered by tests."
    }
    else {
        Write-FailHealth "Stage-05 must keep auto capture inventory-open execution limited to Manual mode with a selected hotbar bug net, with Auto mode still blocked."
    }

    if ($autoMiningDocText -and
        $autoMiningDocText.Contains("AutoMiningAutoModeObservationSubmitsSustainedRequest") -and
        $autoCaptureDocText -and
        $autoCaptureDocText.Contains("Manual + 当前选中快捷栏虫网") -and
        $diagnosticsDocText -and
        $diagnosticsDocText.Contains("AutoMiningAutoModeObservationSubmitsSustainedRequest") -and
        $plan05Text -and
        $plan05Text.Contains("0.876-automining-auto-autocapture-inventory-gate")) {
        Write-Pass "Stage-05 auto mining/auto capture docs and plan record the 0.876 completion contract."
    }
    else {
        Write-FailHealth "Stage-05 docs must describe Auto mining Auto-mode regression coverage, Manual selected-hotbar bug-net inventory gate, diagnostics, and the 0.876 completion record."
    }
}

function Test-BlueprintFeedbackStage08Governance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $runtimeDiagnosticsTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.RuntimeDiagnosticsAndDispatchTests.cs"
    $programPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.cs"
    $blueprintDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $autoMiningDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "杂项页", "自动挖矿.md")
    $autoCaptureDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "物品页", "自动捕捉.md")
    $featureToggleDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "F5通用", "功能主开关快捷键.md")
    $diagnosticsDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "AI诊断日志说明.md")
    $plan00Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "蓝图功能实机反馈修补", "00-基准.md")
    if (-not (Test-Path -LiteralPath $plan00Path)) {
        $plan00Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图功能实机反馈修补", "00-基准.md")
    }
    $plan08Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "蓝图功能实机反馈修补", "08-诊断测试文档审计.md")
    if (-not (Test-Path -LiteralPath $plan08Path)) {
        $plan08Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图功能实机反馈修补", "08-诊断测试文档审计.md")
    }
    $updateIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "索引.md")
    $updateRecordPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "0.879-蓝图反馈诊断测试文档审计-2606202359.md")
    $experienceIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("AI经验笔记", "索引.md")
    $experienceNotePath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("AI经验笔记", "自动化与热键", "热键调度Hook静默失效防复发-2606202359.md")
    $docHistoryIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "索引.md")
    $docHistoryRecordPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "蓝图反馈诊断测试文档审计-2606202359.md")

    $runtimeDiagnosticsTestText = Read-TextIfExists -Path $runtimeDiagnosticsTestPath
    $programText = Read-TextIfExists -Path $programPath
    $blueprintDocText = Read-TextIfExists -Path $blueprintDocPath
    $autoMiningDocText = Read-TextIfExists -Path $autoMiningDocPath
    $autoCaptureDocText = Read-TextIfExists -Path $autoCaptureDocPath
    $featureToggleDocText = Read-TextIfExists -Path $featureToggleDocPath
    $diagnosticsDocText = Read-TextIfExists -Path $diagnosticsDocPath
    $plan00Text = Read-TextIfExists -Path $plan00Path
    $plan08Text = Read-TextIfExists -Path $plan08Path
    $updateIndexText = Read-TextIfExists -Path $updateIndexPath
    $updateRecordText = Read-TextIfExists -Path $updateRecordPath
    $experienceIndexText = Read-TextIfExists -Path $experienceIndexPath
    $experienceNoteText = Read-TextIfExists -Path $experienceNotePath
    $docHistoryIndexText = Read-TextIfExists -Path $docHistoryIndexPath
    $docHistoryRecordText = Read-TextIfExists -Path $docHistoryRecordPath

    if ($runtimeDiagnosticsTestText -and
        $runtimeDiagnosticsTestText.Contains("BlueprintFeedbackDiagnosticsAuditFieldsStayWired") -and
        $runtimeDiagnosticsTestText.Contains("ScenarioNames.WorldAutomationAutoMiningHotkey") -and
        $runtimeDiagnosticsTestText.Contains("ScenarioNames.BlueprintActionHotkey") -and
        $runtimeDiagnosticsTestText.Contains("AutoMiningLastHotkeyBlockedReason") -and
        $runtimeDiagnosticsTestText.Contains("BlueprintHandheldActionBarLastResultCode") -and
        $programText.Contains("blueprint feedback diagnostics audit fields stay wired")) {
        Write-Pass "Blueprint feedback stage-08 diagnostic aggregate console test is present and registered."
    }
    else {
        Write-FailHealth "Stage-08 must keep a registered aggregate test for blueprint feedback diagnostic fields and hotkey scenarios."
    }

    if ($blueprintDocText -and
        $blueprintDocText.Contains("0.879-blueprint-feedback-diagnostics-audit") -and
        $blueprintDocText.Contains("BlueprintFeedbackDiagnosticsAuditFieldsStayWired") -and
        $blueprintDocText.Contains("删除 / 移动 / 红图仍 UI-only") -and
        $autoMiningDocText -and
        $autoMiningDocText.Contains("热键 / 调度 / hook 静默整类失效") -and
        $autoMiningDocText.Contains("AutoMiningAutoModeObservationSubmitsSustainedRequest") -and
        $autoCaptureDocText -and
        $autoCaptureDocText.Contains("AutoCaptureCritterManualInventoryOpenRequiresSelectedHotbarBugNet") -and
        $featureToggleDocText -and
        $featureToggleDocText.Contains("WorldAutomation.AutoMining.Hotkey") -and
        $diagnosticsDocText -and
        $diagnosticsDocText.Contains("BlueprintFeedbackDiagnosticsAuditFieldsStayWired") -and
        $diagnosticsDocText.Contains("Test-BlueprintFeedbackStage08Governance")) {
        Write-Pass "Blueprint feedback stage-08 function and diagnostics documents describe the aggregate diagnostic/testing contract."
    }
    else {
        Write-FailHealth "Stage-08 docs must describe 0.879 aggregate diagnostics, auto-mining silent-failure prevention, auto-capture inventory gate, feature-toggle separation, and health audit anchors."
    }

    if ($plan00Text -and
        ($plan00Text.Contains('下一阶段为 `09-验证与收口`') -or $plan00Text.Contains('09-验证与收口` 已完成')) -and
        $plan08Text -and
        $plan08Text.Contains("状态：已完成") -and
        $plan08Text.Contains("0.879-blueprint-feedback-diagnostics-audit") -and
        ($plan08Text.Contains("未生成测试包") -or $plan08Text.Contains("不生成测试包")) -and
        $updateRecordText -and
        $updateRecordText.Contains('RuntimeVersion：`0.879-blueprint-feedback-diagnostics-audit`') -and
        ($updateRecordText.Contains("不生成测试包") -or $updateRecordText.Contains("未生成测试包"))) {
        Write-Pass "Blueprint feedback stage-08 plan and update record remain synchronized after final closeout."
    }
    else {
        Write-FailHealth "Stage-08 plan/update record must mark 08 complete, record RuntimeVersion 0.879, and keep no-package scope."
    }

    if ($experienceNoteText -and
        $experienceNoteText.Contains("WorldAutomation.AutoMining.Hotkey") -and
        $experienceNoteText.Contains("AutoMiningAutoModeObservationSubmitsSustainedRequest") -and
        $experienceNoteText.Contains("AutoCaptureCritterManualInventoryOpenRequiresSelectedHotbarBugNet") -and
        $experienceNoteText.Contains("Test-BlueprintFeedbackStage08Governance") -and
        $experienceIndexText.Contains("热键调度Hook静默失效防复发-2606202359.md")) {
        Write-Pass "Stage-08 auto-mining silent-failure prevention experience note is indexed and points to automated guardrails."
    }
    else {
        Write-FailHealth "Stage-08 must index an experience note summarizing hotkey/dispatcher/hook silent-failure prevention and its automated guardrails."
    }

    if ($updateIndexText -and
        $updateIndexText.Contains("0.879-蓝图反馈诊断测试文档审计-2606202359.md") -and
        $docHistoryRecordText -and
        $docHistoryRecordText.Contains("蓝图功能实机反馈修补/08") -and
        $docHistoryIndexText.Contains("蓝图反馈诊断测试文档审计-2606202359.md")) {
        Write-Pass "Stage-08 update index and document-change history are synchronized."
    }
    else {
        Write-FailHealth "Stage-08 update index and document-change history must reference the 0.879 diagnostic/test/doc audit."
    }
}

function Test-BlueprintFeedbackStage09CloseoutGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $plan00Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图功能实机反馈修补", "00-基准.md")
    $plan09Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图功能实机反馈修补", "09-验证与收口.md")
    $currentPlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "索引.md")
    $archivePlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "索引.md")
    $blueprintDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $functionIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "功能索引.md")
    $autoMiningDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "杂项页", "自动挖矿.md")
    $autoCaptureDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "物品页", "自动捕捉.md")
    $featureToggleDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "F5通用", "功能主开关快捷键.md")
    $diagnosticsDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "AI诊断日志说明.md")
    $updateIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "索引.md")
    $updateRecordPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "0.880-蓝图反馈验证收口-2606210025.md")
    $docHistoryIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "索引.md")
    $docHistoryRecordPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "蓝图反馈验证收口-2606210025.md")

    $plan00Text = Read-TextIfExists -Path $plan00Path
    $plan09Text = Read-TextIfExists -Path $plan09Path
    $currentPlanIndexText = Read-TextIfExists -Path $currentPlanIndexPath
    $archivePlanIndexText = Read-TextIfExists -Path $archivePlanIndexPath
    $blueprintDocText = Read-TextIfExists -Path $blueprintDocPath
    $functionIndexText = Read-TextIfExists -Path $functionIndexPath
    $autoMiningDocText = Read-TextIfExists -Path $autoMiningDocPath
    $autoCaptureDocText = Read-TextIfExists -Path $autoCaptureDocPath
    $featureToggleDocText = Read-TextIfExists -Path $featureToggleDocPath
    $diagnosticsDocText = Read-TextIfExists -Path $diagnosticsDocPath
    $updateIndexText = Read-TextIfExists -Path $updateIndexPath
    $updateRecordText = Read-TextIfExists -Path $updateRecordPath
    $docHistoryIndexText = Read-TextIfExists -Path $docHistoryIndexPath
    $docHistoryRecordText = Read-TextIfExists -Path $docHistoryRecordPath

    if ($plan00Text -and
        $plan00Text.Contains('状态：`09-验证与收口` 已完成') -and
        $plan00Text.Contains("自动接力已终止") -and
        $plan09Text -and
        $plan09Text.Contains("状态：已完成") -and
        $plan09Text.Contains("0.880-blueprint-feedback-closeout") -and
        $plan09Text.Contains("默认测试包") -and
        $plan09Text.Contains("不创建后续")) {
        Write-Pass "Blueprint feedback stage-09 archived plan records final closeout, package delivery, and no further handoff."
    }
    else {
        Write-FailHealth "Stage-09 archived plan must mark final closeout complete, record RuntimeVersion 0.880, default test package delivery, and no further handoff."
    }

    if ($currentPlanIndexText -and
        $currentPlanIndexText.Contains("当前没有正在推进的计划") -and
        -not $currentPlanIndexText.Contains("蓝图功能实机反馈修补/") -and
        $archivePlanIndexText -and
        $archivePlanIndexText.Contains("文档/归档历史计划/蓝图功能实机反馈修补/") -and
        $archivePlanIndexText.Contains("0.880-blueprint-feedback-closeout") -and
        $archivePlanIndexText.Contains("自动接力已终止")) {
        Write-Pass "Blueprint feedback final closeout moved the plan from current index to archived index."
    }
    else {
        Write-FailHealth "Stage-09 must remove the blueprint feedback plan from current index and add the archived closeout summary."
    }

    if ($blueprintDocText -and
        $blueprintDocText.Contains("文档/归档历史计划/蓝图功能实机反馈修补/00-基准.md") -and
        $blueprintDocText.Contains("0.880-blueprint-feedback-closeout") -and
        $blueprintDocText.Contains("删除 / 移动 / 红图仍 UI-only") -and
        $functionIndexText -and
        $functionIndexText.Contains("0.880-blueprint-feedback-closeout") -and
        $functionIndexText.Contains("用户实机验收仍待确认") -and
        $autoMiningDocText -and
        $autoMiningDocText.Contains("0.880-蓝图反馈验证收口-2606210025.md") -and
        $autoCaptureDocText -and
        $autoCaptureDocText.Contains("0.880-蓝图反馈验证收口-2606210025.md") -and
        $featureToggleDocText -and
        $featureToggleDocText.Contains("0.880-蓝图反馈验证收口-2606210025.md") -and
        $diagnosticsDocText -and
        $diagnosticsDocText.Contains("0.880-blueprint-feedback-closeout") -and
        $diagnosticsDocText.Contains("不新增诊断字段")) {
        Write-Pass "Stage-09 function and diagnostics docs describe final closeout without widening feature scope."
    }
    else {
        Write-FailHealth "Stage-09 must synchronize blueprint/auto-mining/auto-capture/feature-toggle docs, feature index, and diagnostics closeout scope."
    }

    if ($updateIndexText -and
        $updateIndexText.Contains("0.880-blueprint-feedback-closeout") -and
        $updateIndexText.Contains("0.880-蓝图反馈验证收口-2606210025.md") -and
        $updateRecordText -and
        $updateRecordText.Contains('RuntimeVersion：`0.880-blueprint-feedback-closeout`') -and
        $updateRecordText.Contains("JueMingZ-TestPackage") -and
        $updateRecordText.Contains("-RequireFreshTestPackage") -and
        $docHistoryRecordText -and
        $docHistoryRecordText.Contains("蓝图功能实机反馈修补/09") -and
        $docHistoryIndexText.Contains("蓝图反馈验证收口-2606210025.md")) {
        Write-Pass "Stage-09 update record and document-change history are synchronized."
    }
    else {
        Write-FailHealth "Stage-09 update record, update index, and document-change history must reference the 0.880 final closeout and package audit."
    }
}

function Test-HotkeyBackspaceClearGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $featureToggleUiPath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyMainWindow.FeatureToggleHotkeys.cs"
    $featureToggleChordPath = Join-Path $RepoRoot "src\JueMingZ\Config\FeatureToggleHotkeyChord.cs"
    $blueprintUiPath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyMainWindow.Blueprint.cs"
    $blueprintHotkeyPath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyMainWindow.Blueprint.Hotkey.cs"
    $quickItemCapturePath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyMainWindow.Misc.QuickItems.Capture.cs"
    $quickItemUiPath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyMainWindow.Misc.QuickItems.cs"
    $quickItemCardPath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyMainWindow.Misc.QuickItems.Cards.cs"
    $quickItemRuntimePath = Join-Path $RepoRoot "src\JueMingZ\Automation\InventoryAndItems\QuickItemHotkeyService.cs"
    $autoMiningUiPath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyMainWindow.Misc.AutoMining.cs"
    $mapInputPath = Join-Path $RepoRoot "src\JueMingZ\Automation\Information\MapQuickAnnouncementHotkeyInput.cs"
    $mapSettingsPath = Join-Path $RepoRoot "src\JueMingZ\Config\MapQuickAnnouncementSettings.cs"
    $mapCapturePath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyMainWindow.MapEnhancement.Capture.cs"
    $mapUiPath = Join-Path $RepoRoot "src\JueMingZ\UI\Legacy\LegacyMainWindow.MapEnhancement.cs"
    $programPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.cs"
    $featureToggleTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.FeatureToggleHotkeyTests.cs"
    $blueprintTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintEntryTests.cs"
    $inventoryTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.InventoryAutomationActionTests.cs"
    $worldTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.WorldAutomationActionTests.cs"
    $mapTestPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.MapQuickAnnouncementTests.cs"
    $plan06Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "蓝图创建交互二次反馈修补", "06-快捷键退格解绑治理.md")
    $archivedPlan06Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图创建交互二次反馈修补", "06-快捷键退格解绑治理.md")
    $blueprintDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $featureToggleDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "F5通用", "功能主开关快捷键.md")
    $quickItemDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "物品页", "快捷物品.md")
    $autoMiningDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "杂项页", "自动挖矿.md")
    $quickAnnouncementDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "地图加强页", "快捷宣告.md")

    if (-not (Test-Path -LiteralPath $plan06Path) -and (Test-Path -LiteralPath $archivedPlan06Path)) {
        $plan06Path = $archivedPlan06Path
    }

    $featureToggleUiText = Read-TextIfExists -Path $featureToggleUiPath
    $featureToggleChordText = Read-TextIfExists -Path $featureToggleChordPath
    $blueprintUiText = Read-TextIfExists -Path $blueprintUiPath
    $blueprintHotkeyText = Read-TextIfExists -Path $blueprintHotkeyPath
    $quickItemCaptureText = Read-TextIfExists -Path $quickItemCapturePath
    $quickItemUiText = Read-TextIfExists -Path $quickItemUiPath
    $quickItemCardText = Read-TextIfExists -Path $quickItemCardPath
    $quickItemRuntimeText = Read-TextIfExists -Path $quickItemRuntimePath
    $autoMiningUiText = Read-TextIfExists -Path $autoMiningUiPath
    $mapInputText = Read-TextIfExists -Path $mapInputPath
    $mapSettingsText = Read-TextIfExists -Path $mapSettingsPath
    $mapCaptureText = Read-TextIfExists -Path $mapCapturePath
    $mapUiText = Read-TextIfExists -Path $mapUiPath
    $programText = Read-TextIfExists -Path $programPath
    $featureToggleTestText = Read-TextIfExists -Path $featureToggleTestPath
    $blueprintTestText = Read-TextIfExists -Path $blueprintTestPath
    $inventoryTestText = Read-TextIfExists -Path $inventoryTestPath
    $worldTestText = Read-TextIfExists -Path $worldTestPath
    $mapTestText = Read-TextIfExists -Path $mapTestPath
    $plan06Text = Read-TextIfExists -Path $plan06Path
    $blueprintDocText = Read-TextIfExists -Path $blueprintDocPath
    $featureToggleDocText = Read-TextIfExists -Path $featureToggleDocPath
    $quickItemDocText = Read-TextIfExists -Path $quickItemDocPath
    $autoMiningDocText = Read-TextIfExists -Path $autoMiningDocPath
    $quickAnnouncementDocText = Read-TextIfExists -Path $quickAnnouncementDocPath

    if ($featureToggleUiText -and
        $featureToggleUiText.Contains("VkBackspace") -and
        $featureToggleUiText.Contains("ClearFeatureToggleHotkeyBinding") -and
        $featureToggleUiText.Contains("TryClearFeatureToggleHotkeyBindingForTesting") -and
        $featureToggleChordText -and
        $featureToggleChordText.Contains("Backspace")) {
        Write-Pass "Feature toggle hotkey capture treats Backspace as clear-only and rejects it as a saved chord."
    }
    else {
        Write-FailHealth "Feature toggle hotkey capture must clear on Backspace and FeatureToggleHotkeyChord must reject Backspace."
    }

    if ($blueprintUiText -and
        $blueprintUiText.Contains("BlueprintActionHotkeyTooltipClear") -and
        $blueprintHotkeyText -and
        $blueprintHotkeyText.Contains("VkBackspace") -and
        $blueprintHotkeyText.Contains("ClearBlueprintHotkeyBinding") -and
        $blueprintHotkeyText.Contains("TryClearBlueprintActionHotkeyForTesting") -and
        $blueprintHotkeyText.Contains("FeatureIds.BlueprintLibraryAction")) {
        Write-Pass "Blueprint create/save/library action hotkey capture clears bindings on Backspace without saving Backspace."
    }
    else {
        Write-FailHealth "Blueprint create/save/library action hotkey capture must expose Backspace clear behavior and clear test hooks."
    }

    if ($quickItemCaptureText -and
        $quickItemCaptureText.Contains("ClearQuickItemHotkeyBinding") -and
        $quickItemCaptureText.Contains("ClearAutoMiningHotkeyBinding") -and
        $quickItemCaptureText.Contains("VkBackspace") -and
        $quickItemUiText -and
        $quickItemUiText.Contains("Backspace 删除") -and
        $quickItemCardText -and
        $quickItemCardText.Contains("Backspace 删除") -and
        $quickItemRuntimeText -and
        $quickItemRuntimeText.Contains("TryNormalizeHotkeyForTesting") -and
        $autoMiningUiText -and
        $autoMiningUiText.Contains("Backspace 删除绑定")) {
        Write-Pass "Quick item and auto-mining capture paths expose Backspace clear behavior while runtime parsing stays explicit."
    }
    else {
        Write-FailHealth "Quick item and auto-mining capture paths must clear on Backspace, update visible hints, and keep runtime parsing testable."
    }

    if ($mapInputText -and
        $mapInputText.Contains("0x08") -and
        $mapInputText.Contains('"Backspace"') -and
        $mapSettingsText -and
        $mapSettingsText.Contains("TryClearHotkeySlot") -and
        $mapCaptureText -and
        $mapCaptureText.Contains("TryClearMapQuickAnnouncementHotkeySlotForTesting") -and
        $mapUiText -and
        $mapUiText.Contains("Backspace 删除")) {
        Write-Pass "Map quick announcement capture recognizes Backspace only for selected-slot clearing."
    }
    else {
        Write-FailHealth "Map quick announcement capture must recognize Backspace for clearing without making it a saved token."
    }

    if ($programText -and
        $programText.Contains("Backspace clears binding") -and
        $featureToggleTestText -and
        $featureToggleTestText.Contains("FeatureToggleHotkeyBackspaceClearContract") -and
        $blueprintTestText -and
        $blueprintTestText.Contains("BlueprintActionHotkeyBackspaceClearContract") -and
        $inventoryTestText -and
        $inventoryTestText.Contains("QuickItemHotkeyBackspaceClearContract") -and
        $worldTestText -and
        $worldTestText.Contains("AutoMiningHotkeyBackspaceClearContract") -and
        $mapTestText -and
        $mapTestText.Contains("MapQuickAnnouncementBackspaceClearContract")) {
        Write-Pass "Console tests cover Backspace clear contracts for blueprint, feature toggle, quick item, auto-mining, and quick announcement hotkeys."
    }
    else {
        Write-FailHealth "Console tests must cover all Backspace clear hotkey capture paths introduced in stage 06."
    }

    if ($plan06Text -and
        $plan06Text.Contains("Backspace") -and
        $plan06Text.Contains("0.885-blueprint-hotkey-backspace-clear") -and
        $blueprintDocText -and
        $blueprintDocText.Contains("Backspace") -and
        $featureToggleDocText -and
        $featureToggleDocText.Contains("Backspace") -and
        $quickItemDocText -and
        $quickItemDocText.Contains("Backspace") -and
        $autoMiningDocText -and
        $autoMiningDocText.Contains("Backspace") -and
        $quickAnnouncementDocText -and
        $quickAnnouncementDocText.Contains("Backspace")) {
        Write-Pass "Stage 06 plan and feature docs describe Backspace clear-only hotkey behavior."
    }
    else {
        Write-FailHealth "Stage 06 plan and feature docs must document Backspace clear-only hotkey behavior for all touched capture paths."
    }
}

function Test-BlueprintSecondFeedbackStage07Governance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $testPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.BlueprintSecondFeedbackTests.cs"
    $programPath = Join-Path $RepoRoot "tests\JueMingZ.Tests\Program.cs"
    $auditPath = Join-Path $RepoRoot "scripts\audit-project-health.ps1"
    $plan00Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "蓝图创建交互二次反馈修补", "00-基准.md")
    $plan07Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "蓝图创建交互二次反馈修补", "07-诊断测试文档审计.md")
    $archivedPlan00Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图创建交互二次反馈修补", "00-基准.md")
    $archivedPlan07Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图创建交互二次反馈修补", "07-诊断测试文档审计.md")
    $currentPlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "索引.md")
    $archivePlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "索引.md")
    $blueprintDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $featureToggleDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "F5通用", "功能主开关快捷键.md")
    $quickItemDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "物品页", "快捷物品.md")
    $autoMiningDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "杂项页", "自动挖矿.md")
    $quickAnnouncementDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "地图加强页", "快捷宣告.md")
    $diagnosticsDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "AI诊断日志说明.md")
    $updateIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "索引.md")
    $updateRecordPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "0.886-蓝图二次反馈诊断审计-2606210411.md")
    $docHistoryIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "索引.md")
    $docHistoryRecordPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "蓝图二次反馈诊断审计-2606210411.md")

    if (-not (Test-Path -LiteralPath $plan00Path) -and (Test-Path -LiteralPath $archivedPlan00Path)) {
        $plan00Path = $archivedPlan00Path
    }
    if (-not (Test-Path -LiteralPath $plan07Path) -and (Test-Path -LiteralPath $archivedPlan07Path)) {
        $plan07Path = $archivedPlan07Path
    }

    $testText = Read-TextIfExists -Path $testPath
    $programText = Read-TextIfExists -Path $programPath
    $auditText = Read-TextIfExists -Path $auditPath
    $plan00Text = Read-TextIfExists -Path $plan00Path
    $plan07Text = Read-TextIfExists -Path $plan07Path
    $currentPlanIndexText = Read-TextIfExists -Path $currentPlanIndexPath
    $blueprintDocText = Read-TextIfExists -Path $blueprintDocPath
    $featureToggleDocText = Read-TextIfExists -Path $featureToggleDocPath
    $quickItemDocText = Read-TextIfExists -Path $quickItemDocPath
    $autoMiningDocText = Read-TextIfExists -Path $autoMiningDocPath
    $quickAnnouncementDocText = Read-TextIfExists -Path $quickAnnouncementDocPath
    $diagnosticsDocText = Read-TextIfExists -Path $diagnosticsDocPath
    $updateIndexText = Read-TextIfExists -Path $updateIndexPath
    $updateRecordText = Read-TextIfExists -Path $updateRecordPath
    $docHistoryIndexText = Read-TextIfExists -Path $docHistoryIndexPath
    $docHistoryRecordText = Read-TextIfExists -Path $docHistoryRecordPath
    $archivePlanIndexText = Read-TextIfExists -Path $archivePlanIndexPath

    if ($testText -and
        $testText.Contains("BlueprintSecondFeedbackStage07AuditContractsStayWired") -and
        $testText.Contains("LegacyMainUiState.Visible") -and
        $testText.Contains("BlueprintEntryCommands.StartCreate") -and
        $testText.Contains("SaveDisabledTooltip") -and
        $testText.Contains("lower-saturation-lower-alpha-no-border") -and
        $testText.Contains("GetLocalPromptContractForTesting") -and
        $testText.Contains("NormalizeKeyboardKey(" + [char]34 + "Backspace" + [char]34 + ")") -and
        $programText -and
        $programText.Contains("blueprint second feedback stage 07 audit contracts stay wired")) {
        Write-Pass "Blueprint second-feedback stage-07 aggregate console test is registered and covers create toggle, handheld matrix, air/visual prompt, and Backspace contracts."
    }
    else {
        Write-FailHealth "Stage-07 must register an aggregate blueprint second-feedback console test covering create toggle, handheld matrix, air/visual prompt, and Backspace contracts."
    }

    if ($auditText -and
        $auditText.Contains("Test-BlueprintSecondFeedbackStage07Governance") -and
        $auditText.Contains("BlueprintSecondFeedbackStage07AuditContractsStayWired") -and
        $auditText.Contains("0.886-blueprint-second-feedback-diagnostics-audit")) {
        Write-Pass "Blueprint second-feedback stage-07 health audit function is present and wired to the 0.886 stage contract."
    }
    else {
        Write-FailHealth "Stage-07 health audit must include Test-BlueprintSecondFeedbackStage07Governance and lock the 0.886 stage contract."
    }

    if ($blueprintDocText -and
        $blueprintDocText.Contains('F5 `开始` 成功进入创建态后会立即关闭 F5 主菜单') -and
        $blueprintDocText.Contains('只显示 `保存蓝图` / `退出创建`') -and
        $blueprintDocText.Contains("空气格也会进入 mask / 选区 bounds") -and
        $blueprintDocText.Contains("本地玩家头顶显示蓝字") -and
        $blueprintDocText.Contains("低饱和、低透明蓝色") -and
        $blueprintDocText.Contains("Backspace") -and
        $blueprintDocText.Contains("0.886-blueprint-second-feedback-diagnostics-audit") -and
        $featureToggleDocText -and
        $featureToggleDocText.Contains("Backspace") -and
        $quickItemDocText -and
        $quickItemDocText.Contains("Backspace") -and
        $autoMiningDocText -and
        $autoMiningDocText.Contains("Backspace") -and
        $quickAnnouncementDocText -and
        $quickAnnouncementDocText.Contains("Backspace")) {
        Write-Pass "Blueprint and related hotkey feature docs describe the second-feedback create/visual/Backspace contracts."
    }
    else {
        Write-FailHealth "Stage-07 docs must describe blueprint create toggle, handheld matrix, air mask, local prompt, visual convergence, and Backspace clear-only behavior."
    }

    if ($diagnosticsDocText -and
        $diagnosticsDocText.Contains("Ui.Blueprint.CreateSaveEntry") -and
        $diagnosticsDocText.Contains("Hotkey.BlueprintAction") -and
        $diagnosticsDocText.Contains("BlueprintHandheldActionBarVisible") -and
        $diagnosticsDocText.Contains("Test-BlueprintSecondFeedbackStage07Governance") -and
        $diagnosticsDocText.Contains("0.886-blueprint-second-feedback-diagnostics-audit")) {
        Write-Pass "Diagnostics docs keep the second-feedback stage-07 blueprint action and handheld audit routing."
    }
    else {
        Write-FailHealth "Stage-07 diagnostics docs must keep Ui.Blueprint.CreateSaveEntry, Hotkey.BlueprintAction, handheld snapshot fields, and the new health audit route."
    }

    $stage07IndexOk =
        ($currentPlanIndexText -and
            $currentPlanIndexText.Contains('`07-诊断测试文档审计.md` 已完成') -and
            $currentPlanIndexText.Contains('后续入口为 `08-验证与收口.md`')) -or
        ($archivePlanIndexText -and
            $archivePlanIndexText.Contains("蓝图创建交互二次反馈修补") -and
            $archivePlanIndexText.Contains("0.887-blueprint-second-feedback-closeout"))

    if ($plan00Text -and
        $plan00Text.Contains('`07` 已完成诊断测试文档审计') -and
        $plan00Text.Contains("0.886-blueprint-second-feedback-diagnostics-audit") -and
        $plan07Text -and
        $plan07Text.Contains("状态：已完成") -and
        $plan07Text.Contains("0.886-blueprint-second-feedback-diagnostics-audit") -and
        $plan07Text.Contains("未生成测试包") -and
        $stage07IndexOk) {
        Write-Pass "Stage-07 plan files and plan indices preserve the 0.886 diagnostic/test/doc audit contract before or after closeout archive."
    }
    else {
        Write-FailHealth "Stage-07 plan files and plan indices must preserve the 0.886 diagnostic/test/doc audit contract before or after closeout archive."
    }

    if ($updateIndexText -and
        $updateIndexText.Contains("0.886-蓝图二次反馈诊断审计-2606210411.md") -and
        $updateRecordText -and
        $updateRecordText.Contains('RuntimeVersion：`0.886-blueprint-second-feedback-diagnostics-audit`') -and
        $updateRecordText.Contains("未生成测试包") -and
        $docHistoryIndexText -and
        $docHistoryIndexText.Contains("蓝图二次反馈诊断审计-2606210411.md") -and
        $docHistoryRecordText -and
        $docHistoryRecordText.Contains("蓝图创建交互二次反馈修补/07")) {
        Write-Pass "Stage-07 update record and document-change history are synchronized."
    }
    else {
        Write-FailHealth "Stage-07 update record, update index, and document-change history must reference the 0.886 diagnostic/test/doc audit."
    }
}

function Test-BlueprintSecondFeedbackStage08CloseoutGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $auditPath = Join-Path $RepoRoot "scripts\audit-project-health.ps1"
    $currentPlanDirPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "蓝图创建交互二次反馈修补")
    $archivedPlan00Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图创建交互二次反馈修补", "00-基准.md")
    $archivedPlan08Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图创建交互二次反馈修补", "08-验证与收口.md")
    $currentPlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "索引.md")
    $archivePlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "索引.md")
    $blueprintDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $diagnosticsDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "AI诊断日志说明.md")
    $updateIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "索引.md")
    $updateRecordPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "0.887-蓝图二次反馈验证收口-2606210430.md")
    $docHistoryIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "索引.md")
    $docHistoryRecordPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "蓝图二次反馈验证收口-2606210430.md")

    $auditText = Read-TextIfExists -Path $auditPath
    $plan00Text = Read-TextIfExists -Path $archivedPlan00Path
    $plan08Text = Read-TextIfExists -Path $archivedPlan08Path
    $currentPlanIndexText = Read-TextIfExists -Path $currentPlanIndexPath
    $archivePlanIndexText = Read-TextIfExists -Path $archivePlanIndexPath
    $blueprintDocText = Read-TextIfExists -Path $blueprintDocPath
    $diagnosticsDocText = Read-TextIfExists -Path $diagnosticsDocPath
    $updateIndexText = Read-TextIfExists -Path $updateIndexPath
    $updateRecordText = Read-TextIfExists -Path $updateRecordPath
    $docHistoryIndexText = Read-TextIfExists -Path $docHistoryIndexPath
    $docHistoryRecordText = Read-TextIfExists -Path $docHistoryRecordPath

    if ($auditText -and
        $auditText.Contains("Test-BlueprintSecondFeedbackStage08CloseoutGovernance") -and
        $auditText.Contains("0.887-blueprint-second-feedback-closeout") -and
        $auditText.Contains("0.887-蓝图二次反馈验证收口-2606210430.md")) {
        Write-Pass "Blueprint second-feedback stage-08 closeout health audit function is present and wired to the 0.887 closeout contract."
    }
    else {
        Write-FailHealth "Stage-08 closeout health audit must lock the 0.887 closeout contract and update record."
    }

    if (-not (Test-Path -LiteralPath $currentPlanDirPath) -and
        $plan00Text -and
        $plan00Text.Contains("0.887-blueprint-second-feedback-closeout") -and
        $plan00Text.Contains("自动串行接力终止") -and
        $plan08Text -and
        $plan08Text.Contains("状态：已完成") -and
        $plan08Text.Contains("JueMingZ-TestPackage") -and
        $plan08Text.Contains("严格新鲜包健康审计")) {
        Write-Pass "Blueprint second-feedback plan is archived with the 0.887 closeout and package/fresh-audit scope."
    }
    else {
        Write-FailHealth "Stage-08 closeout must move the plan to archive and mark 08 complete with package/fresh-audit scope."
    }

    if ($currentPlanIndexText -and
        $currentPlanIndexText.Contains("蓝图创建交互二次反馈修补") -and
        $currentPlanIndexText.Contains("文档/归档历史计划/蓝图创建交互二次反馈修补/") -and
        $currentPlanIndexText.Contains("自动接力已终止") -and
        $archivePlanIndexText -and
        $archivePlanIndexText.Contains("文档/归档历史计划/蓝图创建交互二次反馈修补/") -and
        $archivePlanIndexText.Contains("0.887-blueprint-second-feedback-closeout")) {
        Write-Pass "Current and archived plan indices record the stage-08 closeout and relay termination."
    }
    else {
        Write-FailHealth "Stage-08 closeout must update current and archived plan indices."
    }

    if ($blueprintDocText -and
        $blueprintDocText.Contains("0.887-blueprint-second-feedback-closeout") -and
        $blueprintDocText.Contains("不新增用户可见蓝图运行时行为") -and
        $blueprintDocText.Contains("文档/归档历史计划/蓝图创建交互二次反馈修补/00-基准.md") -and
        $diagnosticsDocText -and
        $diagnosticsDocText.Contains("0.887-blueprint-second-feedback-closeout") -and
        $diagnosticsDocText.Contains("不新增诊断字段")) {
        Write-Pass "Blueprint feature and diagnostics docs record the 0.887 closeout without expanding runtime or diagnostics scope."
    }
    else {
        Write-FailHealth "Stage-08 closeout must update blueprint feature and diagnostics docs with the no-new-runtime/no-new-diagnostics scope."
    }

    if ($updateIndexText -and
        $updateIndexText.Contains("0.887-蓝图二次反馈验证收口-2606210430.md") -and
        $updateRecordText -and
        $updateRecordText.Contains('RuntimeVersion：`0.887-blueprint-second-feedback-closeout`') -and
        $updateRecordText.Contains("JueMingZ-TestPackage") -and
        $updateRecordText.Contains("严格新鲜包健康审计") -and
        $docHistoryIndexText -and
        $docHistoryIndexText.Contains("蓝图二次反馈验证收口-2606210430.md") -and
        $docHistoryRecordText -and
        $docHistoryRecordText.Contains("0.887-blueprint-second-feedback-closeout")) {
        Write-Pass "Stage-08 update record and document-change history are synchronized."
    }
    else {
        Write-FailHealth "Stage-08 update record, update index, and document-change history must reference the 0.887 closeout."
    }
}

function Get-NormalizedAuditScopes {
    param([Parameter(Mandatory = $true)][string[]]$Scopes)

    $selected = @()
    foreach ($scopeName in $Scopes) {
        if ([string]::IsNullOrWhiteSpace($scopeName)) {
            continue
        }

        if ($scopeName -eq "All") {
            return @("All")
        }

        if ($selected -notcontains $scopeName) {
            $selected += $scopeName
        }
    }

    if ($selected.Count -eq 0) {
        return @("All")
    }

    return $selected
}

function Test-AuditScopeSelected {
    param(
        [Parameter(Mandatory = $true)][string[]]$Scopes,
        [Parameter(Mandatory = $true)][string[]]$Candidates
    )

    if ($Scopes -contains "All") {
        return $true
    }

    foreach ($candidate in $Candidates) {
        if ($Scopes -contains $candidate) {
            return $true
        }
    }

    return $false
}

function Test-CurrentContractAnchors {
    param(
        [Parameter(Mandatory = $true)][string]$RepoRoot,
        [Parameter(Mandatory = $true)][string]$RelativePath,
        [Parameter(Mandatory = $true)][string[]]$RequiredAnchors,
        [Parameter(Mandatory = $true)][string]$PassMessage,
        [Parameter(Mandatory = $true)][string]$FailMessage
    )

    $path = Join-Path $RepoRoot $RelativePath
    $text = Read-TextIfExists -Path $path
    if ($null -eq $text) {
        Write-FailHealth "$FailMessage missing file: $RelativePath"
        return
    }

    $missing = @()
    foreach ($anchor in $RequiredAnchors) {
        if (-not [string]::IsNullOrWhiteSpace($anchor) -and -not $text.Contains($anchor)) {
            $missing += $anchor
        }
    }

    if ($missing.Count -gt 0) {
        Write-FailHealth "$FailMessage missing anchor(s) in ${RelativePath}: $($missing -join ', ')"
    }
    else {
        Write-Pass $PassMessage
    }
}

function Test-CurrentContractForbiddenAnchors {
    param(
        [Parameter(Mandatory = $true)][string]$RepoRoot,
        [Parameter(Mandatory = $true)][string]$RelativePath,
        [Parameter(Mandatory = $true)][string[]]$ForbiddenAnchors,
        [Parameter(Mandatory = $true)][string]$PassMessage,
        [Parameter(Mandatory = $true)][string]$FailMessage
    )

    $path = Join-Path $RepoRoot $RelativePath
    $text = Read-TextIfExists -Path $path
    if ($null -eq $text) {
        Write-FailHealth "$FailMessage missing file: $RelativePath"
        return
    }

    $violations = @()
    foreach ($anchor in $ForbiddenAnchors) {
        if (-not [string]::IsNullOrWhiteSpace($anchor) -and $text.Contains($anchor)) {
            $violations += $anchor
        }
    }

    if ($violations.Count -gt 0) {
        Write-FailHealth "$FailMessage contains forbidden anchor(s) in ${RelativePath}: $($violations -join ', ')"
    }
    else {
        Write-Pass $PassMessage
    }
}

function Test-BlueprintCurrentCreationGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "src\JueMingZ\Automation\Blueprint\BlueprintCreationMaskState.cs" -RequiredAnchors @(
        "BeginCreate",
        "ClearSelection",
        "ExitCreatePreservingSelection",
        "FinishCreate",
        "HandlePointer"
    ) -PassMessage "Blueprint creation mask still owns begin/clear/exit/finish/pointer state." -FailMessage "Blueprint creation mask contract drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "src\JueMingZ\Automation\Blueprint\BlueprintCreationPromptService.cs" -RequiredAnchors @(
        "NotifyCreateStarted",
        "NotifyCreateExited",
        "GetLocalPromptContractForTesting",
        "StartEventKind",
        "ExitEventKind",
        "LocalPromptContract"
    ) -PassMessage "Blueprint creation prompt stays local-only and emits start/exit notifications." -FailMessage "Blueprint creation prompt contract drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "src\JueMingZ\Automation\Blueprint\BlueprintEntryState.cs" -RequiredAnchors @(
        "BlueprintCreationMaskState.BeginCreate",
        "BlueprintCreationMaskState.ClearSelection",
        "BlueprintCreationMaskState.ExitCreatePreservingSelection",
        "BlueprintCreationMaskState.FinishCreate",
        "BlueprintCreationPromptService.NotifyCreateStarted",
        "BlueprintCreationPromptService.NotifyCreateExited"
    ) -PassMessage "Blueprint creation entry flow still routes through mask and prompt services." -FailMessage "Blueprint creation entry flow drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "src\JueMingZ\UI\BlueprintCreationOverlay.cs" -RequiredAnchors @(
        "DrawInterfaceLayer",
        "BuildPointerInputForTesting",
        "ShouldBlockCreationForPointerOwnershipForTesting",
        "ShouldConsumeAfterPlayerInputForTesting",
        "GetVisualContractForTesting"
    ) -PassMessage "Blueprint creation overlay keeps the pointer contract and draw entrypoint." -FailMessage "Blueprint creation overlay contract drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "tests\JueMingZ.Tests\Program.BlueprintCreationTests.cs" -RequiredAnchors @(
        "BlueprintCreationOverlayRoutesAndPointerContract",
        "BlueprintCreationLocalPromptEdgesAndVisualContract",
        "BlueprintCreationClearFinishAndCancelContracts"
    ) -PassMessage "Blueprint creation tests still cover overlay routing and prompt edges." -FailMessage "Blueprint creation test coverage drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "tests\JueMingZ.Tests\Program.cs" -RequiredAnchors @(
        "blueprint creation overlay routes and pointer contract",
        "blueprint creation local prompt edges and visual contract",
        "blueprint creation clear finish and cancel contracts"
    ) -PassMessage "Blueprint creation test registration is still wired." -FailMessage "Blueprint creation test registration drifted."
}

function Test-BlueprintCurrentPlacementGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "src\JueMingZ\Automation\Blueprint\BlueprintPlacementPreviewState.cs" -RequiredAnchors @(
        "BlueprintPlacementWorldContext",
        "LeftReleased",
        "ConfirmPlacementLocked",
        "initialLeftReleased",
        "GetSnapshot"
    ) -PassMessage "Blueprint placement preview still gates confirm on release and world context." -FailMessage "Blueprint placement preview contract drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "src\JueMingZ\Automation\Blueprint\BlueprintProjectionService.cs" -RequiredAnchors @(
        "EffectiveLayerCount",
        "BlueprintPlacementWorldContext",
        "RecordProjectionResolve",
        "RefreshAfterWorldIdentityChanged"
    ) -PassMessage "Blueprint projection still feeds effective layer counts and refresh flow." -FailMessage "Blueprint projection contract drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "src\JueMingZ\UI\Legacy\BlueprintPlacedInstanceUiState.cs" -RequiredAnchors @(
        "GetCachedSummary",
        "RefreshForWorldLifecycle",
        "ClearAllCurrentWorld",
        "BlueprintPlacementWorldContext",
        "BuildCommandId"
    ) -PassMessage "Blueprint placed-instance UI still owns the current-world management surface." -FailMessage "Blueprint placed-instance UI contract drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "src\JueMingZ\Automation\Blueprint\BlueprintPlacedInstanceActivity.cs" -RequiredAnchors @(
        "EffectiveLayerCount",
        "HasActionablePlacedBlueprint",
        "ResolveActionableCount"
    ) -PassMessage "Blueprint placed-instance activity still keys off effective projection content." -FailMessage "Blueprint placed-instance activity contract drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "src\JueMingZ\Automation\Blueprint\BlueprintWorldInstanceLifecycleService.cs" -RequiredAnchors @(
        "RefreshForWorldLifecycle",
        "BlueprintPlacedInstanceUiState.RefreshForWorldLifecycle",
        "BlueprintProjectionService.RefreshAfterWorldIdentityChanged"
    ) -PassMessage "Blueprint world lifecycle still refreshes placed and projection state together." -FailMessage "Blueprint world lifecycle contract drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "tests\JueMingZ.Tests\Program.BlueprintPlacementPreviewTests.cs" -RequiredAnchors @(
        "BlueprintPlacementPreviewUsesUpperLeftCenterAnchorForEvenSize",
        "BlueprintPlacementConfirmCreatesWorldInstanceOnly",
        "BlueprintPlacementConfirmRefreshesProjectionAndPlacedList",
        "BlueprintPlacementPreviewWaitsForPhysicalLeftReleaseBeforeConfirm",
        "BlueprintPlacementOverlayRoutesAndPointerContract"
    ) -PassMessage "Blueprint placement entry tests still cover preview and confirm gating." -FailMessage "Blueprint placement entry tests drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "tests\JueMingZ.Tests\Program.BlueprintPlacedInstanceTests.cs" -RequiredAnchors @(
        "BlueprintPlacedInstancesUiStateLoadsCurrentWorldAndKeepsSnapshots",
        "BlueprintPlacedInstanceCommandsToggleRemoveSelectAndLayer",
        "BlueprintPlacedInstanceClearAllCurrentWorldKeepsTemplatesAndRefreshesCaches",
        "BlueprintPlacedInstanceMoveKeepsSnapshotStateAndRefreshesCaches",
        "BlueprintPlacedInstanceMoveBlocksCompletedProgressAndKeepsOriginalPosition",
        "BlueprintPlacedInstanceMirrorUsesServiceAndFailsClosed"
    ) -PassMessage "Blueprint placed-instance tests still cover current-world management and transforms." -FailMessage "Blueprint placed-instance tests drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "tests\JueMingZ.Tests\Program.cs" -RequiredAnchors @(
        "blueprint placement confirm creates world instance only",
        "blueprint placement confirm refreshes projection and placed list",
        "blueprint placement preview waits for physical left release before confirm",
        "blueprint placement overlay routes and pointer contract",
        "blueprint placed instances UI state loads current world and keeps snapshots",
        "blueprint placed instance mirror uses service and fails closed"
    ) -PassMessage "Blueprint placement tests remain registered." -FailMessage "Blueprint placement test registration drifted."
}

function Test-BlueprintCurrentTransformGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "src\JueMingZ\Automation\Blueprint\BlueprintMirrorService.cs" -RequiredAnchors @(
        "TryMirrorHorizontal",
        "MirrorSlopeForTesting",
        "CanMirrorLayerForTesting",
        "ResolveTileObjectDataForDirection",
        "TileObjectData"
    ) -PassMessage "Blueprint mirror service still flips slopes and TileObjectData direction." -FailMessage "Blueprint mirror service contract drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "src\JueMingZ\Automation\Blueprint\BlueprintPlacedInstanceTransformState.cs" -RequiredAnchors @(
        "BeginMove",
        "BeginMirror",
        "mirrorBlockedByPlacedProgress",
        "GetFloatingProjectionForTesting",
        "BlueprintMirrorService.TryMirrorHorizontal"
    ) -PassMessage "Blueprint placed-instance transform state still gates move/mirror through the mirror service." -FailMessage "Blueprint placed-instance transform contract drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "tests\JueMingZ.Tests\Program.BlueprintMirrorTests.cs" -RequiredAnchors @(
        "BlueprintMirrorCompleteMultitileObjectMirrorsAndPartialFailsClosed",
        "BlueprintMirrorSupportMatrixMapsSlopesAndFlipsObjectDirection",
        "BlueprintMirrorProjectionMaterialsAndAutoPlacementUseMirroredSnapshot",
        "BlueprintMirrorDiagnosticsWriteRuntimeSnapshotJson"
    ) -PassMessage "Blueprint mirror tests still cover object support, fail-closed, projection reuse, and diagnostics." -FailMessage "Blueprint mirror test coverage drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "tests\JueMingZ.Tests\Program.BlueprintPlacedInstanceTests.cs" -RequiredAnchors @(
        "BlueprintPlacedInstanceMirrorUsesServiceAndFailsClosed",
        "BlueprintPlacedInstanceMoveBlocksCompletedProgressAndKeepsOriginalPosition"
    ) -PassMessage "Blueprint placed-instance tests still cover transform progress and mirror service reuse." -FailMessage "Blueprint placed-instance transform tests drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "tests\JueMingZ.Tests\Program.cs" -RequiredAnchors @(
        "blueprint mirror complete multitile object mirrors and partial fails closed",
        "blueprint mirror support matrix maps slopes and flips object direction",
        "blueprint mirror projection materials and auto placement use mirrored snapshot",
        "blueprint mirror diagnostics write runtime snapshot json",
        "blueprint placed instance mirror uses service and fails closed"
    ) -PassMessage "Blueprint transform tests remain registered." -FailMessage "Blueprint transform test registration drifted."
}

function Test-BlueprintCurrentAutoPlacementGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "src\JueMingZ\Automation\Blueprint\BlueprintAutoPlacementService.cs" -RequiredAnchors @(
        "voidBagMaterialNotExecutable",
        "BuildMainInventoryAvailabilityMap",
        "BuildCombinedInventoryAvailabilityMap",
        "BlueprintMaterialExecutionScope",
        "RecordAutoPlacementCandidateScan",
        "BuildRequestForTesting"
    ) -PassMessage "Blueprint auto-placement service still resolves main-inventory scope and void-bag fail-closed paths." -FailMessage "Blueprint auto-placement service contract drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "src\JueMingZ\Actions\Executors\BlueprintAutoPlaceActionExecutor.cs" -RequiredAnchors @(
        "ScenarioNames.BlueprintAutoPlace",
        "TryFindMainInventoryMaterial",
        "ItemUseBridge.TryEnqueueUseSelectedItem",
        "materialExecutionScope",
        "directWorldMutationAttempted",
        "inventoryMutationAttempted"
    ) -PassMessage "Blueprint auto-place executor still routes through ItemUseBridge and fail-closed verification." -FailMessage "Blueprint auto-place executor contract drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "src\JueMingZ\Common\ActionMetadataKeys.cs" -RequiredAnchors @(
        "BlueprintMaterialExecutionScope"
    ) -PassMessage "Blueprint material execution metadata key stays centralized." -FailMessage "Blueprint material execution metadata drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "src\JueMingZ\Runtime\RuntimeAutomationDispatcher.cs" -RequiredAnchors @(
        "DispatchBlueprintAutoPlacement",
        "BlueprintAutoPlacementEnabled",
        "BlueprintAutoPlacementService.Tick"
    ) -PassMessage "Blueprint auto-placement stays wired into the runtime dispatcher." -FailMessage "Blueprint auto-placement runtime dispatch drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "src\JueMingZ\Runtime\Diagnostics\RuntimeDiagnosticSnapshotBuilder.Blueprint.cs" -RequiredAnchors @(
        "BlueprintAutoPlacementSkippedVoidBagOnlyLayerCount",
        "BlueprintAutoPlacementLastFailureReason",
        "BlueprintAutoPlacementAverageCandidateScanElapsedMs"
    ) -PassMessage "Blueprint auto-placement diagnostics still feed runtime snapshot fields." -FailMessage "Blueprint auto-placement diagnostics snapshot drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "src\JueMingZ\Diagnostics\DiagnosticSnapshotWriter.Json.cs" -RequiredAnchors @(
        "BlueprintAutoPlacementSkippedVoidBagOnlyLayerCount",
        "BlueprintAutoPlacementLastFailureReason"
    ) -PassMessage "Blueprint auto-placement diagnostics still serialize into runtime JSON." -FailMessage "Blueprint auto-placement JSON snapshot drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "tests\JueMingZ.Tests\Program.BlueprintAutoPlacementTests.cs" -RequiredAnchors @(
        "BlueprintAutoPlacementSubmitsActionQueueAndVerifiesPlacement",
        "BlueprintAutoPlacementUsesConfiguredReplacementMaterial",
        "BlueprintAutoPlacementVoidBagOnlyMaterialsFailClosedWithReason",
        "BlueprintAutoPlacementDiagnosticsWriteRuntimeSnapshotJson"
    ) -PassMessage "Blueprint auto-placement tests still cover execution, replacement, and fail-closed diagnostics." -FailMessage "Blueprint auto-placement tests drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "tests\JueMingZ.Tests\Program.cs" -RequiredAnchors @(
        "blueprint auto placement submits ActionQueue and verifies placement",
        "blueprint auto placement uses configured replacement material",
        "blueprint auto placement void bag only materials fail closed with reason",
        "blueprint auto placement diagnostics write runtime snapshot json"
    ) -PassMessage "Blueprint auto-placement tests remain registered." -FailMessage "Blueprint auto-placement test registration drifted."
}

function Test-BlueprintCurrentHandheldGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "src\JueMingZ\Automation\Blueprint\BlueprintHandheldActionBarState.cs" -RequiredAnchors @(
        "ButtonIdCreate",
        "ButtonIdSave",
        "ButtonIdOpenPlacedList",
        "ButtonIdClearPlaced",
        "ButtonIdMove",
        "ButtonIdRegionModify",
        "ButtonIdMirror",
        "RecordDeferredBusinessClick",
        "BuildPointerOwnerId",
        "BuildDiagnostics"
    ) -PassMessage "Blueprint handheld action bar state still owns the command matrix and pointer diagnostics." -FailMessage "Blueprint handheld action bar state drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "src\JueMingZ\UI\BlueprintHandheldActionBarOverlay.cs" -RequiredAnchors @(
        "PopulateDynamicBlueprintState",
        "ReadForBlueprintHandheldActionBarOverlay",
        "ReadMouseForBlueprintHandheldOverlay",
        "RegisterPointerOwnerForCurrentFrame",
        "UpdateAfterPlayerInputGuard",
        "LegacyUiTheme.DrawButtonClipped"
    ) -PassMessage "Blueprint handheld overlay still stays on the UI/input-only surface." -FailMessage "Blueprint handheld overlay contract drifted."

    Test-CurrentContractForbiddenAnchors -RepoRoot $RepoRoot -RelativePath "src\JueMingZ\UI\BlueprintHandheldActionBarOverlay.cs" -ForbiddenAnchors @(
        "InputActionQueue",
        "ForceRefreshForMaterialWindow",
        "ForceRefreshForAutoPlacement"
    ) -PassMessage "Blueprint handheld overlay avoids ActionQueue and blueprint refresh backflow." -FailMessage "Blueprint handheld overlay backflow guard drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "src\JueMingZ\UI\DiagnosticMouseStateReader.cs" -RequiredAnchors @(
        "ReadForBlueprintHandheldActionBarOverlay",
        "BlueprintHandheldActionBarOverlayAfterPlayerInput",
        "BlueprintHandheldOverlayGateBypass"
    ) -PassMessage "Blueprint handheld mouse reader keeps the dedicated gate-bypass cache paths." -FailMessage "Blueprint handheld mouse reader drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "src\JueMingZ\Hooks\InterfaceLayerHookCallbacks.cs" -RequiredAnchors @(
        "BlueprintHandheldActionBarDispatcherLayerName",
        "DrawBlueprintHandheldActionBarDispatcherLayer",
        'ParseScaleValue(_scaleType, "None")'
    ) -PassMessage "Blueprint handheld interface layer stays on the unscaled dispatcher." -FailMessage "Blueprint handheld interface-layer contract drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "src\JueMingZ\Automation\Blueprint\BlueprintPlacedInstanceActivity.cs" -RequiredAnchors @(
        "EffectiveLayerCount",
        "HasActionablePlacedBlueprint"
    ) -PassMessage "Blueprint handheld action bar still keys off effective placed content." -FailMessage "Blueprint handheld activity contract drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "tests\JueMingZ.Tests\Program.BlueprintHandheldActionBarTests.cs" -RequiredAnchors @(
        "BlueprintHandheldActionBarUsesEffectiveProjectionForPlacedState",
        "BlueprintHandheldActionBarOverlayStaysUiOnlyAndNoScan",
        "BlueprintHandheldActionBarGateClosedMouseKeepsTerrariaClick",
        "BlueprintHandheldActionBarDiagnosticsSnapshotJson"
    ) -PassMessage "Blueprint handheld tests still cover effective projection and UI-only boundaries." -FailMessage "Blueprint handheld tests drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "tests\JueMingZ.Tests\Program.InterfaceLayerHookTests.cs" -RequiredAnchors @(
        "GetBlueprintHandheldActionBarDispatcherRouteNamesForTesting",
        "GetBlueprintHandheldActionBarScaleTypeNameForTesting",
        "blueprint handheld action bar unscaled dispatcher routes"
    ) -PassMessage "Blueprint handheld interface-layer tests still pin the scale domain." -FailMessage "Blueprint handheld interface-layer test coverage drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "tests\JueMingZ.Tests\Program.cs" -RequiredAnchors @(
        "blueprint handheld action bar uses effective projection for placed state",
        "blueprint handheld action bar overlay stays UI-only and no-scan",
        "blueprint handheld action bar gate-closed mouse keeps Terraria click",
        "blueprint handheld action bar diagnostics snapshot json"
    ) -PassMessage "Blueprint handheld tests remain registered." -FailMessage "Blueprint handheld test registration drifted."
}

function Test-BlueprintCurrentDiagnosticsGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "src\JueMingZ\Automation\Blueprint\BlueprintDiagnostics.cs" -RequiredAnchors @(
        "RecordProjectionResolve",
        "RecordMaterialResolve",
        "RecordAutoPlacementCandidateScan",
        "RecordOperationIfNeeded"
    ) -PassMessage "Blueprint diagnostics helper still owns projection/material/auto-placement counters." -FailMessage "Blueprint diagnostics helper drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "src\JueMingZ\Runtime\Diagnostics\RuntimeDiagnosticSnapshotBuilder.Blueprint.cs" -RequiredAnchors @(
        "BlueprintMirrorLastStatus",
        "BlueprintAutoPlacementSkippedVoidBagOnlyLayerCount",
        "BlueprintDiagnosticsTemplateCount",
        "BlueprintCreationSelectedCount",
        "BlueprintPerformanceLastScenario"
    ) -PassMessage "Blueprint runtime snapshot still carries the current blueprint diagnostics fields." -FailMessage "Blueprint runtime snapshot blueprint fields drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "src\JueMingZ\Diagnostics\DiagnosticSnapshot.cs" -RequiredAnchors @(
        "BlueprintMirrorLastStatus",
        "BlueprintDiagnosticsTemplateCount",
        "BlueprintPerformanceLastScenario",
        "BlueprintAutoPlacementSkippedVoidBagOnlyLayerCount",
        "BlueprintAutoPlacementLastFailureReason"
    ) -PassMessage "Blueprint diagnostic snapshot DTO still exposes the current fields." -FailMessage "Blueprint diagnostic snapshot DTO drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "src\JueMingZ\Diagnostics\DiagnosticSnapshotWriter.Json.cs" -RequiredAnchors @(
        "BlueprintMirrorLastStatus",
        "BlueprintDiagnosticsTemplateCount",
        "BlueprintPerformanceLastScenario",
        "BlueprintAutoPlacementSkippedVoidBagOnlyLayerCount",
        "BlueprintAutoPlacementLastFailureReason"
    ) -PassMessage "Blueprint diagnostic JSON writer still serializes the current fields." -FailMessage "Blueprint diagnostic JSON writer drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "tests\JueMingZ.Tests\Program.BlueprintDiagnosticsTests.cs" -RequiredAnchors @(
        "BlueprintDiagnosticsAggregateRuntimeSnapshotJson",
        "BlueprintDiagnosticsPerformanceCountersAverageCosts",
        "BlueprintWallContinuityStage05RegressionDiagnosticsContractsStayWired"
    ) -PassMessage "Blueprint diagnostics tests still cover the aggregate runtime snapshot and averages." -FailMessage "Blueprint diagnostics tests drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "tests\JueMingZ.Tests\Program.cs" -RequiredAnchors @(
        "blueprint diagnostics aggregate runtime snapshot json",
        "blueprint diagnostics performance counters average costs",
        "blueprint wall object stage 06 regression diagnostics contracts stay wired",
        "blueprint wall continuity stage 05 regression diagnostics contracts stay wired"
    ) -PassMessage "Blueprint diagnostics tests remain registered." -FailMessage "Blueprint diagnostics test registration drifted."
}

function Test-BlueprintWallObjectStage06RegressionDiagnosticsGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "src\JueMingZ\Automation\Blueprint\BlueprintWallProjectionFrameResolver.cs" -RequiredAnchors @(
        "ResolveFrameForTesting",
        "FrameSize = 36",
        "layer.FrameX = frameX",
        "layer.FrameY = frameY"
    ) -PassMessage "Blueprint wall projection frame resolver still derives transient wall frame coordinates." -FailMessage "Blueprint wall projection frame resolver contract drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "src\JueMingZ\Automation\Blueprint\BlueprintCaptureService.cs" -RequiredAnchors @(
        "TryBuildCaptureCellRequests",
        "TryAddObjectExpansionRequest",
        "BuildObjectExpansionCell",
        "objectExpansionIncomplete"
    ) -PassMessage "Blueprint capture still expands partial multi-tile objects and fails closed on incomplete expansion." -FailMessage "Blueprint object capture expansion contract drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "src\JueMingZ\Automation\Blueprint\BlueprintMirrorService.cs" -RequiredAnchors @(
        "TryValidateObjectIntegrity",
        "objectIncomplete",
        "objectTileDataUnresolvable",
        "不能保留旧 FrameX/Y 伪装成功"
    ) -PassMessage "Blueprint mirror service still fails closed when object frame or integrity cannot be proven." -FailMessage "Blueprint mirror fail-closed contract drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "tests\JueMingZ.Tests\Program.BlueprintDiagnosticsTests.cs" -RequiredAnchors @(
        "BlueprintWallObjectStage06RegressionDiagnosticsContractsStayWired",
        "BlueprintProjectionWallFramesUseNeighborContinuity",
        "BlueprintCaptureExpandsPartialMultitileObjectWithoutWallsOrWires",
        "BlueprintCaptureFailsClosedWhenExpandedObjectCellIsIncomplete",
        "BlueprintMirrorCompleteMultitileObjectMirrorsAndPartialFailsClosed",
        "BlueprintAutoPlacementDiagnosticsWriteRuntimeSnapshotJson",
        "BlueprintAutoPlacementVoidBagOnlyMaterialsFailClosedWithReason"
    ) -PassMessage "Blueprint stage 06 aggregate regression reuses wall, capture, mirror, and auto-placement evidence contracts." -FailMessage "Blueprint stage 06 aggregate regression drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "tests\JueMingZ.Tests\Program.cs" -RequiredAnchors @(
        "blueprint wall object stage 06 regression diagnostics contracts stay wired"
    ) -PassMessage "Blueprint stage 06 aggregate regression is registered." -FailMessage "Blueprint stage 06 aggregate regression registration drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "文档\功能介绍\蓝图页\蓝图.md" -RequiredAnchors @(
        "0.981-blueprint-wall-object-regression-audit",
        "BlueprintWallObjectStage06RegressionDiagnosticsContractsStayWired",
        "Test-BlueprintWallObjectStage06RegressionDiagnosticsGovernance"
    ) -PassMessage "Blueprint feature doc describes the stage 06 aggregate audit boundary." -FailMessage "Blueprint feature doc must describe the stage 06 aggregate audit boundary."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "文档\项目规则\AI诊断日志说明.md" -RequiredAnchors @(
        "0.981-blueprint-wall-object-regression-audit",
        "BlueprintWallObjectStage06RegressionDiagnosticsContractsStayWired",
        "证据不足"
    ) -PassMessage "Blueprint diagnostics doc keeps the stage 06 evidence boundary visible." -FailMessage "Blueprint diagnostics doc must keep the stage 06 evidence boundary visible."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "文档\归档历史计划\蓝图墙层与多格家具镜像治理作业方法-2606260717\06-回归诊断与审计防线.md" -RequiredAnchors @(
        "状态：已完成",
        "BlueprintWallObjectStage06RegressionDiagnosticsContractsStayWired",
        "Test-BlueprintWallObjectStage06RegressionDiagnosticsGovernance",
        "未新增诊断字段"
    ) -PassMessage "Blueprint stage 06 plan records the aggregate regression and no-new-diagnostics boundary." -FailMessage "Blueprint stage 06 plan must record completion, aggregate audit, and diagnostics boundary."
}

function Test-BlueprintWallObjectStage07CloseoutGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $csprojPath = Join-Path $RepoRoot "src\JueMingZ\JueMingZ.csproj"
    $runtimePath = Join-Path $RepoRoot "src\JueMingZ\Runtime\JueMingZRuntime.cs"
    $currentPlanDirectory = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "蓝图墙层与多格家具镜像治理作业方法-2606260717")
    $archivePlanDirectory = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图墙层与多格家具镜像治理作业方法-2606260717")
    $plan00Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图墙层与多格家具镜像治理作业方法-2606260717", "00-作业基准.md")
    $plan07Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图墙层与多格家具镜像治理作业方法-2606260717", "07-验证打包与归档收口.md")
    $currentPlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "索引.md")
    $archivePlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "索引.md")
    $functionDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $diagnosticsDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "AI诊断日志说明.md")
    $updateIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "索引.md")
    $updateRecordPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "0.982-蓝图墙家具验证收口-2606261634.md")
    $docHistoryIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "索引.md")
    $docHistoryRecordPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "蓝图墙家具验证收口-2606261634.md")

    $csprojText = Read-TextIfExists -Path $csprojPath
    $runtimeText = Read-TextIfExists -Path $runtimePath
    $plan00Text = Read-TextIfExists -Path $plan00Path
    $plan07Text = Read-TextIfExists -Path $plan07Path
    $currentPlanIndexText = Read-TextIfExists -Path $currentPlanIndexPath
    $archivePlanIndexText = Read-TextIfExists -Path $archivePlanIndexPath
    $functionDocText = Read-TextIfExists -Path $functionDocPath
    $diagnosticsDocText = Read-TextIfExists -Path $diagnosticsDocPath
    $updateIndexText = Read-TextIfExists -Path $updateIndexPath
    $updateRecordText = Read-TextIfExists -Path $updateRecordPath
    $docHistoryIndexText = Read-TextIfExists -Path $docHistoryIndexPath
    $docHistoryRecordText = Read-TextIfExists -Path $docHistoryRecordPath

    if ($plan07Text -and
        $plan07Text.Contains("0.982-blueprint-wall-object-closeout") -and
        $updateRecordText -and
        $updateRecordText.Contains('RuntimeVersion：`0.982-blueprint-wall-object-closeout`')) {
        Write-Pass "Blueprint wall/object stage 07 closeout still records the 0.982 package version without pinning the current RuntimeVersion."
    }
    else {
        Write-FailHealth "Blueprint wall/object stage 07 must keep the archived 0.982 RuntimeVersion record while allowing newer current versions."
    }

    if ((Test-Path -LiteralPath $archivePlanDirectory) -and
        -not (Test-Path -LiteralPath $currentPlanDirectory) -and
        $plan00Text -and
        $plan00Text.Contains("07-验证打包与归档收口") -and
        $plan00Text.Contains("已完成：0.982-蓝图墙家具验证收口") -and
        $plan07Text -and
        $plan07Text.Contains("状态：已完成") -and
        $plan07Text.Contains("0.982-blueprint-wall-object-closeout") -and
        $plan07Text.Contains("JueMingZ-TestPackage") -and
        $plan07Text.Contains("-RequireFreshTestPackage") -and
        $plan07Text.Contains("不生成源码包") -and
        $plan07Text.Contains("不新增功能")) {
        Write-Pass "Blueprint wall/object plan is archived with stage 07 package delivery and strict freshness audit recorded."
    }
    else {
        Write-FailHealth "Blueprint wall/object stage 07 must archive the plan and mark 00/07 complete with package, strict freshness audit, no source package, and no-new-feature scope."
    }

    if ($currentPlanIndexText -and
        $currentPlanIndexText.Contains("当前没有正在推进的计划") -and
        $currentPlanIndexText.Contains("文档/归档历史计划/蓝图墙层与多格家具镜像治理作业方法-2606260717/") -and
        $currentPlanIndexText.Contains("0.982-blueprint-wall-object-closeout") -and
        $archivePlanIndexText -and
        $archivePlanIndexText.Contains("文档/归档历史计划/蓝图墙层与多格家具镜像治理作业方法-2606260717/") -and
        $archivePlanIndexText.Contains("0.982-blueprint-wall-object-closeout") -and
        $archivePlanIndexText.Contains("JueMingZ-TestPackage")) {
        Write-Pass "Blueprint wall/object current and archive plan indexes record the stage 07 closeout."
    }
    else {
        Write-FailHealth "Blueprint wall/object stage 07 must remove the plan from current work and add the 0.982 archived closeout summary."
    }

    if ($functionDocText -and
        $functionDocText.Contains("0.982-blueprint-wall-object-closeout") -and
        $functionDocText.Contains("文档/归档历史计划/蓝图墙层与多格家具镜像治理作业方法-2606260717/00-作业基准.md") -and
        $functionDocText.Contains("JueMingZ-TestPackage") -and
        $functionDocText.Contains("严格新鲜包健康审计") -and
        $diagnosticsDocText -and
        $diagnosticsDocText.Contains("0.982-blueprint-wall-object-closeout") -and
        $diagnosticsDocText.Contains("不新增 runtime snapshot 字段") -and
        $diagnosticsDocText.Contains("不新增 trace JSONL")) {
        Write-Pass "Blueprint feature and diagnostics docs record the 0.982 closeout without expanding runtime or diagnostics scope."
    }
    else {
        Write-FailHealth "Blueprint wall/object stage 07 must update blueprint feature and diagnostics docs with package closeout and no-new-diagnostics scope."
    }

    if ($updateIndexText -and
        $updateIndexText.Contains("0.982-蓝图墙家具验证收口-2606261634.md") -and
        $updateRecordText -and
        $updateRecordText.Contains('RuntimeVersion：`0.982-blueprint-wall-object-closeout`') -and
        $updateRecordText.Contains("JueMingZ-TestPackage") -and
        $updateRecordText.Contains("-RequireFreshTestPackage") -and
        $updateRecordText.Contains("不生成源码包") -and
        $docHistoryIndexText -and
        $docHistoryIndexText.Contains("蓝图墙家具验证收口-2606261634.md") -and
        $docHistoryRecordText -and
        $docHistoryRecordText.Contains("0.982-blueprint-wall-object-closeout") -and
        $docHistoryRecordText.Contains("07-验证打包与归档收口")) {
        Write-Pass "Blueprint wall/object stage 07 update record and document-change history are synchronized."
    }
    else {
        Write-FailHealth "Blueprint wall/object stage 07 must synchronize update index/record and document-change history for the 0.982 closeout."
    }
}

function Test-BlueprintWallFrameRefreshStage03Governance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "src\JueMingZ\Compat\TerrariaWallFrameCompat.cs" -RequiredAnchors @(
        "WorldGen.SquareWallFrame",
        "must never be used as a substitute for placing WallType",
        "refreshedCoordinateCount = 9"
    ) -PassMessage "Blueprint wall frame refresh stays in a narrow Compat wrapper." -FailMessage "Blueprint wall frame refresh Compat wrapper drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "src\JueMingZ\Actions\Executors\BlueprintAutoPlaceActionExecutor.cs" -RequiredAnchors @(
        "TryRefreshWallFramesAround",
        "wallFrameRefreshFailed",
        "IsProjectionVerified"
    ) -PassMessage "Blueprint auto-place executor refreshes wall frames only in the post-use verification boundary." -FailMessage "Blueprint auto-place wall frame refresh boundary drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "tests\JueMingZ.Tests\Program.BlueprintAutoPlacementTests.cs" -RequiredAnchors @(
        "BlueprintAutoPlacementRefreshesWallFramesAfterVerifiedWallUse",
        "BlueprintAutoPlacementDoesNotRefreshWallFramesWhenWallTypeMissing",
        "WallFrameRefreshAttemptCount"
    ) -PassMessage "Blueprint wall frame refresh regressions cover verified wall use and missing-wall fail-closed behavior." -FailMessage "Blueprint wall frame refresh regression tests drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "tests\JueMingZ.Tests\Program.cs" -RequiredAnchors @(
        "blueprint auto placement refreshes wall frames after verified wall use",
        "blueprint auto placement skips wall frame refresh when WallType is missing"
    ) -PassMessage "Blueprint wall frame refresh regression tests are registered." -FailMessage "Blueprint wall frame refresh test registration drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "文档\归档历史计划\蓝图墙不连续实机修复-2606281210\03-墙帧刷新边界修复.md" -RequiredAnchors @(
        "状态：已完成",
        "0.984-blueprint-wall-frame-refresh",
        "Test-BlueprintWallFrameRefreshStage03Governance",
        '下一唯一入口为 `04-自动放置节奏与复验修复.md`'
    ) -PassMessage "Blueprint wall continuity stage 03 plan records the frame refresh implementation and next handoff." -FailMessage "Blueprint wall continuity stage 03 plan must record completion, audit anchor, and next handoff."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "文档\功能介绍\蓝图页\蓝图.md" -RequiredAnchors @(
        "0.984-blueprint-wall-frame-refresh",
        "WorldGen.SquareWallFrame",
        '不改变 `Blueprint.AutoPlace` action event schema'
    ) -PassMessage "Blueprint feature doc describes the wall frame refresh boundary." -FailMessage "Blueprint feature doc must describe the 0.984 wall frame refresh boundary."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "文档\项目规则\AI诊断日志说明.md" -RequiredAnchors @(
        "0.984-blueprint-wall-frame-refresh",
        "wallFrameRefreshFailed",
        "BlueprintAutoPlacementRefreshesWallFramesAfterVerifiedWallUse"
    ) -PassMessage "Blueprint diagnostics doc describes wall frame refresh diagnostics and tests." -FailMessage "Blueprint diagnostics doc must describe the 0.984 wall frame refresh diagnostics boundary."
}

function Test-BlueprintWallUnverifiedRetryStage04Governance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "src\JueMingZ\Automation\Blueprint\BlueprintAutoPlacementService.cs" -RequiredAnchors @(
        "MaxWallUnverifiedRetrySubmissions = 1",
        "CanSubmitWallUnverifiedRetryLocked",
        "ArmWallUnverifiedRetryIfNeededLocked",
        "waitingForProjectionChange"
    ) -PassMessage "Blueprint wall auto-placement keeps the unverified retry bounded." -FailMessage "Blueprint wall unverified retry guard drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "tests\JueMingZ.Tests\Program.BlueprintAutoPlacementTests.cs" -RequiredAnchors @(
        "BlueprintAutoPlacementRetriesWallMissingOnceAfterUnverifiedUse",
        "Expected one bounded retry",
        "Expected wall auto placement to stop after one unverified retry"
    ) -PassMessage "Blueprint wall unverified retry regression covers one retry and stop condition." -FailMessage "Blueprint wall unverified retry regression test drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "tests\JueMingZ.Tests\Program.cs" -RequiredAnchors @(
        "blueprint auto placement retries missing wall once after unverified use"
    ) -PassMessage "Blueprint wall unverified retry regression is registered." -FailMessage "Blueprint wall unverified retry test registration drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "文档\归档历史计划\蓝图墙不连续实机修复-2606281210\04-自动放置节奏与复验修复.md" -RequiredAnchors @(
        "状态：已完成",
        "0.985-blueprint-wall-unverified-retry",
        "Test-BlueprintWallUnverifiedRetryStage04Governance",
        '下一唯一入口为 `05-回归诊断与审计防线.md`'
    ) -PassMessage "Blueprint wall continuity stage 04 plan records the bounded retry and next handoff." -FailMessage "Blueprint wall continuity stage 04 plan must record completion, audit anchor, and next handoff."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "文档\功能介绍\蓝图页\蓝图.md" -RequiredAnchors @(
        "0.985-blueprint-wall-unverified-retry",
        "有界重试",
        '不改变 `Blueprint.AutoPlace` action event schema'
    ) -PassMessage "Blueprint feature doc describes the 0.985 bounded wall retry boundary." -FailMessage "Blueprint feature doc must describe the 0.985 bounded wall retry boundary."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "文档\项目规则\AI诊断日志说明.md" -RequiredAnchors @(
        "0.985-blueprint-wall-unverified-retry",
        "BlueprintAutoPlacementRetriesWallMissingOnceAfterUnverifiedUse",
        "waitingForProjectionChange"
    ) -PassMessage "Blueprint diagnostics doc describes the 0.985 bounded wall retry diagnostics." -FailMessage "Blueprint diagnostics doc must describe the 0.985 bounded wall retry diagnostics."
}

function Test-BlueprintWallContinuityStage05RegressionDiagnosticsGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "tests\JueMingZ.Tests\Program.BlueprintDiagnosticsTests.cs" -RequiredAnchors @(
        "BlueprintWallContinuityStage05RegressionDiagnosticsContractsStayWired",
        "BlueprintProjectionWallDiagnosticsSeparateTypePresenceAndFrameMismatch",
        "BlueprintProjectionWallDiagnosticsExposeCompletedCurrentMismatch",
        "BlueprintAutoPlacementRefreshesWallFramesAfterVerifiedWallUse",
        "BlueprintAutoPlacementRetriesWallMissingOnceAfterUnverifiedUse",
        "BlueprintAutoPlacementVoidBagOnlyMaterialsFailClosedWithReason"
    ) -PassMessage "Blueprint wall continuity stage 05 aggregate regression reuses wall diagnostics, frame refresh, retry, and material-boundary contracts." -FailMessage "Blueprint wall continuity stage 05 aggregate regression drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "tests\JueMingZ.Tests\Program.cs" -RequiredAnchors @(
        "blueprint wall continuity stage 05 regression diagnostics contracts stay wired"
    ) -PassMessage "Blueprint wall continuity stage 05 aggregate regression is registered." -FailMessage "Blueprint wall continuity stage 05 aggregate regression registration drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "src\JueMingZ\Automation\Blueprint\BlueprintProjectionService.cs" -RequiredAnchors @(
        "WallTypeMissingLayerCount",
        "WallFrameMismatchLayerCount",
        "WallCompletedCurrentMismatchCount"
    ) -PassMessage "Blueprint projection still distinguishes missing wall, wall frame mismatch, and completed-current mismatch." -FailMessage "Blueprint wall completion diagnostics drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "src\JueMingZ\Actions\Executors\BlueprintAutoPlaceActionExecutor.cs" -RequiredAnchors @(
        "TryRefreshWallFramesAround",
        "wallFrameRefreshFailed",
        "directWorldMutationAttempted"
    ) -PassMessage "Blueprint auto-place executor keeps wall frame refresh in the action verification boundary and mutation guards visible." -FailMessage "Blueprint auto-place wall continuity executor boundary drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "src\JueMingZ\Automation\Blueprint\BlueprintAutoPlacementService.cs" -RequiredAnchors @(
        "MaxWallUnverifiedRetrySubmissions = 1",
        "CanSubmitWallUnverifiedRetryLocked",
        "waitingForProjectionChange"
    ) -PassMessage "Blueprint wall unverified retry remains bounded and stops at projection-change wait." -FailMessage "Blueprint wall retry stop condition drifted."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "文档\归档历史计划\蓝图墙不连续实机修复-2606281210\05-回归诊断与审计防线.md" -RequiredAnchors @(
        "状态：已完成",
        "0.986-blueprint-wall-regression-audit",
        "BlueprintWallContinuityStage05RegressionDiagnosticsContractsStayWired",
        "Test-BlueprintWallContinuityStage05RegressionDiagnosticsGovernance",
        '下一唯一入口为 `06-验证打包与归档收口.md`'
    ) -PassMessage "Blueprint wall continuity stage 05 plan records aggregate regression, audit, and next handoff." -FailMessage "Blueprint wall continuity stage 05 plan must record completion, aggregate audit, and next handoff."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "文档\归档历史计划\蓝图墙不连续实机修复-2606281210\00-基准.md" -RequiredAnchors @(
        "05-回归诊断与审计防线",
        '已完成：`0.986-blueprint-wall-regression-audit`',
        "06-验证打包与归档收口"
    ) -PassMessage "Blueprint wall continuity baseline advances from stage 05 to stage 06." -FailMessage "Blueprint wall continuity baseline must mark stage 05 complete and stage 06 next."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "文档\当前在做计划\索引.md" -RequiredAnchors @(
        "0.986-blueprint-wall-regression-audit",
        "文档/归档历史计划/蓝图墙不连续实机修复-2606281210/",
        "0.986-blueprint-wall-regression-audit"
    ) -PassMessage "Current plan index keeps the archived wall continuity stage 05 context visible." -FailMessage "Current plan index must keep the archived wall continuity stage 05 context visible."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "文档\功能介绍\蓝图页\蓝图.md" -RequiredAnchors @(
        "0.986-blueprint-wall-regression-audit",
        "BlueprintWallContinuityStage05RegressionDiagnosticsContractsStayWired",
        "Test-BlueprintWallContinuityStage05RegressionDiagnosticsGovernance"
    ) -PassMessage "Blueprint feature doc describes the stage 05 wall continuity audit boundary." -FailMessage "Blueprint feature doc must describe the stage 05 wall continuity audit boundary."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "文档\项目规则\AI诊断日志说明.md" -RequiredAnchors @(
        "0.986-blueprint-wall-regression-audit",
        "BlueprintWallContinuityStage05RegressionDiagnosticsContractsStayWired",
        "DiagnosticLifecycle=ActiveInvestigation"
    ) -PassMessage "Blueprint diagnostics doc describes the stage 05 wall continuity diagnostics lifecycle." -FailMessage "Blueprint diagnostics doc must describe the stage 05 wall continuity diagnostics lifecycle."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "文档\更新记录\索引.md" -RequiredAnchors @(
        "0.986-蓝图墙回归审计-2606281352.md",
        "0.986-blueprint-wall-regression-audit"
    ) -PassMessage "Update record index includes the 0.986 wall continuity audit." -FailMessage "Update record index must include the 0.986 wall continuity audit."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "文档\更新记录\0.986-蓝图墙回归审计-2606281352.md" -RequiredAnchors @(
        "0.986-blueprint-wall-regression-audit",
        "BlueprintWallContinuityStage05RegressionDiagnosticsContractsStayWired",
        "Test-BlueprintWallContinuityStage05RegressionDiagnosticsGovernance",
        "不生成测试包"
    ) -PassMessage "Update record describes the stage 05 wall continuity regression audit and no-package boundary." -FailMessage "Update record must describe the stage 05 wall continuity regression audit and no-package boundary."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "文档\文档更改历史\索引.md" -RequiredAnchors @(
        "蓝图墙回归审计-2606281352.md",
        "0.986-blueprint-wall-regression-audit"
    ) -PassMessage "Document-change history index includes the stage 05 wall continuity audit." -FailMessage "Document-change history index must include the stage 05 wall continuity audit."

    Test-CurrentContractAnchors -RepoRoot $RepoRoot -RelativePath "文档\文档更改历史\蓝图墙回归审计-2606281352.md" -RequiredAnchors @(
        "05-回归诊断与审计防线",
        "BlueprintWallContinuityStage05RegressionDiagnosticsContractsStayWired",
        "Test-BlueprintWallContinuityStage05RegressionDiagnosticsGovernance"
    ) -PassMessage "Document-change record describes the stage 05 wall continuity audit synchronization." -FailMessage "Document-change record must describe the stage 05 wall continuity audit synchronization."
}

function Test-BlueprintWallContinuityStage06CloseoutGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $csprojPath = Join-Path $RepoRoot "src\JueMingZ\JueMingZ.csproj"
    $runtimePath = Join-Path $RepoRoot "src\JueMingZ\Runtime\JueMingZRuntime.cs"
    $currentPlanDirectory = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "蓝图墙不连续实机修复-2606281210")
    $archivePlanDirectory = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图墙不连续实机修复-2606281210")
    $plan00Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图墙不连续实机修复-2606281210", "00-基准.md")
    $plan06Path = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "蓝图墙不连续实机修复-2606281210", "06-验证打包与归档收口.md")
    $currentPlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("当前在做计划", "索引.md")
    $archivePlanIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("归档历史计划", "索引.md")
    $functionDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("功能介绍", "蓝图页", "蓝图.md")
    $diagnosticsDocPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("项目规则", "AI诊断日志说明.md")
    $updateIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "索引.md")
    $updateRecordPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("更新记录", "0.987-蓝图墙连续性验证收口-2606281405.md")
    $docHistoryIndexPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "索引.md")
    $docHistoryRecordPath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @("文档更改历史", "蓝图墙连续性验证收口-2606281405.md")

    $csprojText = Read-TextIfExists -Path $csprojPath
    $runtimeText = Read-TextIfExists -Path $runtimePath
    $plan00Text = Read-TextIfExists -Path $plan00Path
    $plan06Text = Read-TextIfExists -Path $plan06Path
    $currentPlanIndexText = Read-TextIfExists -Path $currentPlanIndexPath
    $archivePlanIndexText = Read-TextIfExists -Path $archivePlanIndexPath
    $functionDocText = Read-TextIfExists -Path $functionDocPath
    $diagnosticsDocText = Read-TextIfExists -Path $diagnosticsDocPath
    $updateIndexText = Read-TextIfExists -Path $updateIndexPath
    $updateRecordText = Read-TextIfExists -Path $updateRecordPath
    $docHistoryIndexText = Read-TextIfExists -Path $docHistoryIndexPath
    $docHistoryRecordText = Read-TextIfExists -Path $docHistoryRecordPath

    if ($plan06Text -and
        $plan06Text.Contains("0.987-blueprint-wall-continuity-closeout") -and
        $updateRecordText -and
        $updateRecordText.Contains('RuntimeVersion：`0.987-blueprint-wall-continuity-closeout`')) {
        Write-Pass "Blueprint wall continuity stage 06 still records the 0.987 package version without pinning the current RuntimeVersion."
    }
    else {
        Write-FailHealth "Blueprint wall continuity stage 06 must keep the archived 0.987 RuntimeVersion record while allowing newer current versions."
    }

    if ((Test-Path -LiteralPath $archivePlanDirectory) -and
        -not (Test-Path -LiteralPath $currentPlanDirectory) -and
        $plan00Text -and
        $plan00Text.Contains("06-验证打包与归档收口") -and
        $plan00Text.Contains('已完成：`0.987-blueprint-wall-continuity-closeout`') -and
        $plan06Text -and
        $plan06Text.Contains("状态：已完成") -and
        $plan06Text.Contains("0.987-blueprint-wall-continuity-closeout") -and
        $plan06Text.Contains("JueMingZ-TestPackage") -and
        $plan06Text.Contains("-RequireFreshTestPackage") -and
        $plan06Text.Contains("不生成源码包") -and
        $plan06Text.Contains("不新增运行时功能")) {
        Write-Pass "Blueprint wall continuity plan is archived with stage 06 package delivery and strict freshness audit recorded."
    }
    else {
        Write-FailHealth "Blueprint wall continuity stage 06 must archive the plan and record package, strict freshness audit, no source package, and no-new-runtime-feature scope."
    }

    if ($currentPlanIndexText -and
        $currentPlanIndexText.Contains("当前没有正在推进的计划") -and
        $currentPlanIndexText.Contains("文档/归档历史计划/蓝图墙不连续实机修复-2606281210/") -and
        $currentPlanIndexText.Contains("0.987-blueprint-wall-continuity-closeout") -and
        $archivePlanIndexText -and
        $archivePlanIndexText.Contains("文档/归档历史计划/蓝图墙不连续实机修复-2606281210/") -and
        $archivePlanIndexText.Contains("0.987-blueprint-wall-continuity-closeout") -and
        $archivePlanIndexText.Contains("JueMingZ-TestPackage")) {
        Write-Pass "Blueprint wall continuity current and archive plan indexes record the stage 06 closeout."
    }
    else {
        Write-FailHealth "Blueprint wall continuity stage 06 must remove the plan from current work and add the archived closeout summary."
    }

    if ($functionDocText -and
        $functionDocText.Contains("0.987-blueprint-wall-continuity-closeout") -and
        $functionDocText.Contains("JueMingZ-TestPackage") -and
        $functionDocText.Contains("Test-BlueprintWallContinuityStage06CloseoutGovernance") -and
        $diagnosticsDocText -and
        $diagnosticsDocText.Contains("0.987-blueprint-wall-continuity-closeout") -and
        $diagnosticsDocText.Contains("不新增 runtime snapshot 字段") -and
        $diagnosticsDocText.Contains("不新增 trace JSONL")) {
        Write-Pass "Blueprint feature and diagnostics docs record the wall continuity closeout without expanding runtime or diagnostics scope."
    }
    else {
        Write-FailHealth "Blueprint wall continuity stage 06 must update feature and diagnostics docs with package closeout and no-new-diagnostics scope."
    }

    if ($updateIndexText -and
        $updateIndexText.Contains("0.987-蓝图墙连续性验证收口-2606281405.md") -and
        $updateRecordText -and
        $updateRecordText.Contains('RuntimeVersion：`0.987-blueprint-wall-continuity-closeout`') -and
        $updateRecordText.Contains("JueMingZ-TestPackage") -and
        $updateRecordText.Contains("-RequireFreshTestPackage") -and
        $updateRecordText.Contains("不生成源码包") -and
        $docHistoryIndexText -and
        $docHistoryIndexText.Contains("蓝图墙连续性验证收口-2606281405.md") -and
        $docHistoryRecordText -and
        $docHistoryRecordText.Contains("0.987-blueprint-wall-continuity-closeout") -and
        $docHistoryRecordText.Contains("06-验证打包与归档收口")) {
        Write-Pass "Blueprint wall continuity closeout update record and document-change history are synchronized."
    }
    else {
        Write-FailHealth "Blueprint wall continuity stage 06 must synchronize update index/record and document-change history for the 0.987 closeout."
    }
}

function Invoke-PackageBoundaryAudit {
    param(
        [Parameter(Mandatory = $true)][string]$RepoRoot,
        [string]$RuntimeVersion
    )

    if ($RuntimeVersion) {
        Test-VersionConsistency -RepoRoot $RepoRoot -RuntimeVersion $RuntimeVersion -RequireFreshPackage:$RequireFreshTestPackage
    }

    Test-GitSourceBoundary -RepoRoot $RepoRoot
    if ($IncludeSourcePackageZip) {
        Test-SourcePackageZip -RepoRoot $RepoRoot
    }
    else {
        Write-Pass "Source package zip audit skipped by default; use -IncludeSourcePackageZip only when a source export was explicitly requested."
    }

    if ($RuntimeVersion) {
        Test-TestPackage -RepoRoot $RepoRoot -RuntimeVersion $RuntimeVersion -AllowReadme:$AllowTestPackageReadme -RequireFreshPackage:$RequireFreshTestPackage
    }
    else {
        Test-TestPackage -RepoRoot $RepoRoot -RuntimeVersion "Unknown" -AllowReadme:$AllowTestPackageReadme -RequireFreshPackage:$RequireFreshTestPackage
    }
}

function Invoke-GovernanceAudit {
    param(
        [Parameter(Mandatory = $true)][string]$RepoRoot,
        [Parameter(Mandatory = $true)][string[]]$Scopes,
        [string]$RuntimeVersion
    )

    if ($RuntimeVersion) {
        Test-DocsConsistency -RepoRoot $RepoRoot -RuntimeVersion $RuntimeVersion
    }

    if (Test-AuditScopeSelected -Scopes $Scopes -Candidates @("Information", "Fishing")) {
        Test-InformationFishingFallbackCleanup -RepoRoot $RepoRoot
    }

    if (Test-AuditScopeSelected -Scopes $Scopes -Candidates @("UI")) {
        Test-LegacyUiOverlayGovernance -RepoRoot $RepoRoot
        Test-F5MultiPageUiLayoutGovernance -RepoRoot $RepoRoot
    }

    if (Test-AuditScopeSelected -Scopes $Scopes -Candidates @("Combat")) {
        Test-CombatAimDiagnosticsGovernance -RepoRoot $RepoRoot
        Test-PhasebladeQuickSwitchDiagnosticsGovernance -RepoRoot $RepoRoot
    }

    if (Test-AuditScopeSelected -Scopes $Scopes -Candidates @("Diagnostics")) {
        Test-DiagnosticLifecycleGovernance -RepoRoot $RepoRoot
        Test-IterationLogNumbers -RepoRoot $RepoRoot
    }

    if (Test-AuditScopeSelected -Scopes $Scopes -Candidates @("Map")) {
        Test-MapQuickAnnouncementGovernance -RepoRoot $RepoRoot
        Test-MapCustomMarkerGovernance -RepoRoot $RepoRoot
        Test-MapDirectionHintGovernance -RepoRoot $RepoRoot
        Test-MapFootprintGovernance -RepoRoot $RepoRoot
        Test-PlayerWorldExplorationGovernance -RepoRoot $RepoRoot
    }

    if (Test-AuditScopeSelected -Scopes $Scopes -Candidates @("Hotkey")) {
        Test-FeatureToggleHotkeyGovernance -RepoRoot $RepoRoot
        Test-HotkeyBackspaceClearGovernance -RepoRoot $RepoRoot
    }

    if (Test-AuditScopeSelected -Scopes $Scopes -Candidates @("Blueprint", "BlueprintCreation")) {
        Test-BlueprintCurrentCreationGovernance -RepoRoot $RepoRoot
    }

    if (Test-AuditScopeSelected -Scopes $Scopes -Candidates @("Blueprint", "BlueprintPlacement")) {
        Test-BlueprintCurrentPlacementGovernance -RepoRoot $RepoRoot
    }

    if (Test-AuditScopeSelected -Scopes $Scopes -Candidates @("Blueprint", "BlueprintTransform")) {
        Test-BlueprintCurrentTransformGovernance -RepoRoot $RepoRoot
    }

    if (Test-AuditScopeSelected -Scopes $Scopes -Candidates @("Blueprint", "BlueprintAutoPlacement")) {
        Test-BlueprintCurrentAutoPlacementGovernance -RepoRoot $RepoRoot
    }

    if (Test-AuditScopeSelected -Scopes $Scopes -Candidates @("Blueprint", "BlueprintHandheld")) {
        Test-BlueprintCurrentHandheldGovernance -RepoRoot $RepoRoot
    }

    if (Test-AuditScopeSelected -Scopes $Scopes -Candidates @("Blueprint", "BlueprintDiagnostics")) {
        Test-BlueprintCurrentDiagnosticsGovernance -RepoRoot $RepoRoot
        Test-BlueprintWallObjectStage06RegressionDiagnosticsGovernance -RepoRoot $RepoRoot
        Test-BlueprintWallObjectStage07CloseoutGovernance -RepoRoot $RepoRoot
        Test-BlueprintWallFrameRefreshStage03Governance -RepoRoot $RepoRoot
        Test-BlueprintWallUnverifiedRetryStage04Governance -RepoRoot $RepoRoot
        Test-BlueprintWallContinuityStage05RegressionDiagnosticsGovernance -RepoRoot $RepoRoot
        Test-BlueprintWallContinuityStage06CloseoutGovernance -RepoRoot $RepoRoot
    }

    if (Test-AuditScopeSelected -Scopes $Scopes -Candidates @("Notes")) {
        Test-UserNotesGovernance -RepoRoot $RepoRoot
    }

    if ((Test-AuditScopeSelected -Scopes $Scopes -Candidates @("Exploration")) -and
        -not (Test-AuditScopeSelected -Scopes $Scopes -Candidates @("Map"))) {
        Test-PlayerWorldExplorationGovernance -RepoRoot $RepoRoot
    }

    if (Test-AuditScopeSelected -Scopes $Scopes -Candidates @("ActionQueue")) {
        Test-ActionQueueDirectEnqueueGovernance -RepoRoot $RepoRoot
    }

    if (Test-AuditScopeSelected -Scopes $Scopes -Candidates @("Feature")) {
        Test-NewFeatureBoundaryGovernance -RepoRoot $RepoRoot
    }

    if (Test-AuditScopeSelected -Scopes $Scopes -Candidates @("Structure")) {
        Test-DeepStructureBoundaryGovernance -RepoRoot $RepoRoot
    }

    # New scopes: placeholder blocks for future per-domain audit functions.
    # When dedicated Test-* functions are added, move them here.
    if (Test-AuditScopeSelected -Scopes $Scopes -Candidates @("Movement")) {
        # Movement-specific governance functions go here.
    }
    if (Test-AuditScopeSelected -Scopes $Scopes -Candidates @("Items")) {
        # Items/inventory-specific governance functions go here.
    }
    if (Test-AuditScopeSelected -Scopes $Scopes -Candidates @("Buffs")) {
        # Buff/recovery-specific governance functions go here.
    }
    if (Test-AuditScopeSelected -Scopes $Scopes -Candidates @("World")) {
        # World-automation-specific governance functions go here (Map covers overlap).
    }
    if (Test-AuditScopeSelected -Scopes $Scopes -Candidates @("Npc")) {
        # NPC-services-specific governance functions go here.
    }
}

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$runtimeVersion = Get-RuntimeVersion -RepoRoot $repoRoot
$selectedAuditScopes = @(Get-NormalizedAuditScopes -Scopes $AuditScope)

if (($AuditProfile -eq "Fast") -and ($selectedAuditScopes -notcontains "All")) {
    Write-FailHealth "Fast audit profile cannot be combined with scoped governance checks; use -Profile Fast without -Scope, or use -Scope with the default full profile."
}

Write-Pass "Project health audit profile=$AuditProfile scope=$($selectedAuditScopes -join ',')."
Invoke-PackageBoundaryAudit -RepoRoot $repoRoot -RuntimeVersion $runtimeVersion

if ($AuditProfile -eq "Fast") {
    Write-Pass "Governance audit skipped for Profile=Fast; package, version, and source-boundary checks completed."
}
else {
    Invoke-GovernanceAudit -RepoRoot $repoRoot -Scopes $selectedAuditScopes -RuntimeVersion $runtimeVersion
}

if ($script:FailCount -gt 0) {
    Write-Host "FAIL project health audit completed with $script:FailCount failure(s), $script:WarnCount warning(s)."
    exit 1
}

Write-Host "PASS project health audit completed with 0 failures, $script:WarnCount warning(s)."
exit 0
