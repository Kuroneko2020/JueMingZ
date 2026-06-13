param(
    [switch]$IncludeSourcePackageZip,
    [switch]$AllowTestPackageReadme,
    [switch]$RequireFreshTestPackage
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
        "LegacyMainWindow.Misc.cs" = 1
        "LegacyMainWindow.Movement.cs" = 1
        "LegacyMainWindow.Rows.Recovery.cs" = 1
    }
    $expectedAddUiBlockerUses = @{
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
        "MapQuickAnnouncementLastFallbackReason"
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
        "src/JueMingZ/Automation/Information/MapQuickAnnouncementRuntimeService.cs",
        "src/JueMingZ/Automation/Search/SearchItemPickRuntimeService.cs",
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
        Write-Pass "Controlled UI mouse input consumption remains centralized in approved runtime services and Terraria UI compat."
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

function Test-ActionQueueDirectEnqueueGovernance {
    param([Parameter(Mandatory = $true)][string]$RepoRoot)

    $srcRoot = Join-Path $RepoRoot "src\JueMingZ"
    if (-not (Test-Path -LiteralPath $srcRoot)) {
        Write-FailHealth "JueMingZ source root missing while auditing ActionQueue direct enqueue governance."
        return
    }

    $expectedExceptionCounts = @{
        "src/JueMingZ/Input/DiagnosticActionDispatcher.cs" = 1
    }
    $expectedDirectCallCounts = @{
        "src/JueMingZ/Input/DiagnosticActionDispatcher.cs" = 1
    }

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
        Write-FailHealth "ACTION_QUEUE_DIRECT_ENQUEUE_EXCEPTION allowlist changed: $($unexpectedTags -join ', ')"
    }
    else {
        Write-Pass "ActionQueue direct enqueue exception comments remain frozen to the diagnostics allowlist."
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
        Write-FailHealth "InputActionQueue direct Enqueue call allowlist changed: $($unexpectedDirectCalls -join ', ')"
    }
    else {
        Write-Pass "InputActionQueue direct Enqueue calls remain frozen to the diagnostics button path."
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

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$runtimeVersion = Get-RuntimeVersion -RepoRoot $repoRoot
if ($runtimeVersion) {
    Test-VersionConsistency -RepoRoot $repoRoot -RuntimeVersion $runtimeVersion -RequireFreshPackage:$RequireFreshTestPackage
    Test-DocsConsistency -RepoRoot $repoRoot -RuntimeVersion $runtimeVersion
}

Test-GitSourceBoundary -RepoRoot $repoRoot
if ($IncludeSourcePackageZip) {
    Test-SourcePackageZip -RepoRoot $repoRoot
}
else {
    Write-Pass "Source package zip audit skipped by default; use -IncludeSourcePackageZip only when a source export was explicitly requested."
}
if ($runtimeVersion) {
    Test-TestPackage -RepoRoot $repoRoot -RuntimeVersion $runtimeVersion -AllowReadme:$AllowTestPackageReadme -RequireFreshPackage:$RequireFreshTestPackage
}
else {
    Test-TestPackage -RepoRoot $repoRoot -RuntimeVersion "Unknown" -AllowReadme:$AllowTestPackageReadme -RequireFreshPackage:$RequireFreshTestPackage
}
Test-InformationFishingFallbackCleanup -RepoRoot $repoRoot
Test-LegacyUiOverlayGovernance -RepoRoot $repoRoot
Test-CombatAimDiagnosticsGovernance -RepoRoot $repoRoot
Test-PhasebladeQuickSwitchDiagnosticsGovernance -RepoRoot $repoRoot
Test-MapQuickAnnouncementGovernance -RepoRoot $repoRoot
Test-ActionQueueDirectEnqueueGovernance -RepoRoot $repoRoot
Test-NewFeatureBoundaryGovernance -RepoRoot $repoRoot
Test-DeepStructureBoundaryGovernance -RepoRoot $repoRoot
Test-IterationLogNumbers -RepoRoot $repoRoot

if ($script:FailCount -gt 0) {
    Write-Host "FAIL project health audit completed with $script:FailCount failure(s), $script:WarnCount warning(s)."
    exit 1
}

Write-Host "PASS project health audit completed with 0 failures, $script:WarnCount warning(s)."
exit 0
