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
        "LegacyMainWindow.MapEnhancement.cs" = 1
        "LegacyMainWindow.MapEnhancement.RevealedArea.cs" = 1
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
    $mapEnhancementUiText = Read-TextIfExists -Path $mapEnhancementUiPath
    $markerUiText = Read-TextIfExists -Path $markerUiPath
    $mapLayerText = Read-TextIfExists -Path $mapLayerPath
    $fullscreenCompatText = Read-TextIfExists -Path $fullscreenCompatPath
    $testText = Read-TextIfExists -Path $testPath
    $uiInputTestText = Read-TextIfExists -Path $uiInputTestPath
    $interfaceTestText = Read-TextIfExists -Path $interfaceTestPath
    $mapMarkerFeatureDocText = Read-TextIfExists -Path $mapMarkerFeatureDocPath
    $diagnosticRulesText = Read-TextIfExists -Path $diagnosticRulesPath

    if ($null -eq $registrarText -or $null -eq $rootText -or $null -eq $modelsText -or $null -eq $stylesText -or $null -eq $storeText -or $null -eq $cacheText -or $null -eq $diagnosticsText -or $null -eq $traceRecorderText -or $null -eq $snapshotText -or $null -eq $snapshotWriterText -or $null -eq $snapshotBuilderText -or $null -eq $diagnosticUiSnapshotBuilderText -or $null -eq $interactionText -or $null -eq $mapCompatText -or $null -eq $debugHotkeyText -or $null -eq $handlerText -or $null -eq $interfaceLayerText -or $null -eq $fullscreenPickerDrawInstallerText -or $null -eq $stylePickerOverlayText -or $null -eq $legacyWindowStateText -or $null -eq $uiMouseCaptureServiceText -or $null -eq $mapEnhancementUiText -or $null -eq $markerUiText -or $null -eq $mapLayerText -or $null -eq $fullscreenCompatText -or $null -eq $testText -or $null -eq $uiInputTestText -or $null -eq $interfaceTestText -or $null -eq $mapMarkerFeatureDocText -or $null -eq $diagnosticRulesText) {
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
        $mapEnhancementUiText.Contains("LegacyUiMetrics.RowHeight * 5 + LegacyUiMetrics.SettingRowGap * 4") -and
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
        $testText.Contains("directly after the main title row") -and
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
        "PlayerWorldMapMarkersLastWriteUtc",
        "MapMarkerTraceEventsPath",
        "MapMarkerLastTraceEventWrittenAtUtc",
        "MapMarkerLastTraceEventType",
        "MapMarkerLastTraceMarkerId"
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
        Write-Pass "Map marker runtime snapshot covers read/write status, picker state, transform coordinates, jump state, and UI action summaries."
    }
    else {
        Write-FailHealth "Map marker diagnostics must include read/write status, transform coordinates, picker draw/click/close state, last UI action, last jump state, and UI-only action count in runtime snapshot JSON."
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
        $targetServiceText.Contains("MapDirectionHintDiagnostics.RecordTravellingMerchantTarget") -and
        $targetServiceText.Contains("MapDirectionHintDiagnostics.RecordRareCreatureTarget") -and
        $testText.Contains("MapDirectionHintTargetServiceBuildsSnapshotAndHonorsCadence")) {
        Write-Pass "Map direction hint targeting stays a 15-tick read-only stage after game-state-read and before input/action gates."
    }
    else {
        Write-FailHealth "Map direction hint target scan must stay in a 15-tick runtime stage after game-state-read and before input/action gates."
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

    if ($overlayText.Contains("MapDirectionHintTargetService.GetSnapshot") -and
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
    $diagnosticMouseReaderPath = Join-Path $RepoRoot "src\JueMingZ\UI\DiagnosticMouseStateReader.cs"
    $uiMouseCaptureServicePath = Join-Path $RepoRoot "src\JueMingZ\UI\UiMouseCaptureService.cs"
    $hookInstallerPath = Join-Path $RepoRoot "src\JueMingZ\Bootstrap\HookInstaller.cs"
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
    $diagnosticMouseReaderText = Read-TextIfExists -Path $diagnosticMouseReaderPath
    $uiMouseCaptureServiceText = Read-TextIfExists -Path $uiMouseCaptureServicePath
    $hookInstallerText = Read-TextIfExists -Path $hookInstallerPath
    $handlerText = Read-TextIfExists -Path $handlerPath
    $snapshotText = Read-TextIfExists -Path $snapshotPath
    $snapshotWriterText = Read-TextIfExists -Path $snapshotWriterPath
    $snapshotBuilderText = Read-TextIfExists -Path $snapshotBuilderPath
    $testText = Read-TextIfExists -Path $testPath
    $programText = Read-TextIfExists -Path $programPath
    $featureDocText = Read-TextIfExists -Path $featureDocPath
    $diagnosticRulesText = Read-TextIfExists -Path $diagnosticRulesPath

    if ($null -eq $registrarText -or $null -eq $featureIdsText -or $null -eq $appSettingsText -or $null -eq $configServiceText -or $null -eq $settingsSnapshotText -or $null -eq $settingsSnapshotProviderText -or $null -eq $runtimeText -or $null -eq $rootText -or $null -eq $modelsText -or $null -eq $storeText -or $null -eq $cacheText -or $null -eq $serviceText -or $null -eq $diagnosticsText -or $null -eq $renderCacheText -or $null -eq $playbackStateText -or $null -eq $mapLayerText -or $null -eq $mapLayerInstallerText -or $null -eq $overlayInstallerText -or $null -eq $overlayText -or $null -eq $diagnosticMouseReaderText -or $null -eq $uiMouseCaptureServiceText -or $null -eq $hookInstallerText -or $null -eq $handlerText -or $null -eq $snapshotText -or $null -eq $snapshotWriterText -or $null -eq $snapshotBuilderText -or $null -eq $testText -or $null -eq $programText -or $null -eq $featureDocText -or $null -eq $diagnosticRulesText) {
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
        $overlayText.Contains("/FullscreenUi") -and
        -not $overlayText.Contains("LegacyUiInput.ReadMouse()") -and
        $playbackStateText.Contains("var safeWidth = Math.Max(1, screenWidth)") -and
        $playbackStateText.Contains("var safeHeight = Math.Max(1, screenHeight)") -and
        $playbackStateText.Contains("safeHeight - bottomMargin - height") -and
        -not $overlayText.Contains("MapOverlayDrawContext") -and
        -not $playbackStateText.Contains("MapOverlayDrawContext") -and
        $testText.Contains("MapFootprintPlaybackDefaultsToLatestPausedAndScreenSpaceLayout") -and
        $testText.Contains("MapFootprintPlaybackFullscreenUiScaleHitTestCapturesBar")) {
        Write-Pass "Map footprint playback overlay stays fullscreen UI screen-space, UIScale-aware, and bottom-safe-margin based."
    }
    else {
        Write-FailHealth "Map footprint playback UI must use OnPostFullscreenMapDraw plus fullscreen UI-scale screen-size layout, not F5 base-logical or map-content coordinates."
    }

    if ($playbackStateText.Contains("interaction.MouseCaptured = hit.BarHovered || _dragging || (_lastLeftDown && leftReleased)") -and
        $playbackStateText.Contains("interaction.ScrollConsumed = interaction.MouseCaptured") -and
        $playbackStateText.Contains("DisplayTimelineEndTicks") -and
        $overlayText.Contains("ReadForFullscreenMapOverlay") -and
        $overlayText.Contains("TerrariaUiMouseCompat.TryConsumeMouseTriggerInput") -and
        $overlayText.Contains("TerrariaUiMouseCompat.TryMarkUiMouseCapture") -and
        $overlayText.Contains("TerrariaUiMouseCompat.TryConsumeUiScroll") -and
        $overlayText.Contains("raw.GameInputAvailable") -and
        $overlayText.Contains("raw.TerrariaReadAvailable && raw.TerrariaLeftDown") -and
        $diagnosticMouseReaderText.Contains("ReadForFullscreenMapOverlay") -and
        $diagnosticMouseReaderText.Contains("FullscreenOverlayGateBypass") -and
        $overlayText.Contains("CalculateTrackFilledWidth") -and
        $overlayText.Contains("RecordPlaybackPrefixInput") -and
        $overlayText.Contains("RecordPlaybackDrawInput") -and
        $uiMouseCaptureServiceText.Contains("TerrariaUiMouseCompat.TryConsumeMouseTriggerInput") -and
        $testText.Contains("MapFootprintPlaybackHandlesRateDragAndInputHandoff") -and
        $testText.Contains("MapFootprintPlaybackPausedProgressDisplayEndStaysStable") -and
        $testText.Contains("MapFootprintPlaybackFullscreenMouseKeepsReadableClickWhenGlobalGateFalseSpec") -and
        $testText.Contains("MapFootprintPlaybackFullscreenMouseDoesNotUseOsFallbackWhenGlobalGateFalse") -and
        $testText.Contains("MapFootprintPlaybackFullscreenCaptureClearsClickWhenGlobalGateFalse") -and
        -not $programText.Contains('RunExpectedFailure("map footprint playback fullscreen mouse keeps readable click when global gate false"')) {
        Write-Pass "Map footprint playback input consumption remains limited to fullscreen bar/drag hits, gate-false Terraria mouse reads, and paused display progress uses a frozen visible timeline end."
    }
    else {
        Write-FailHealth "Map footprint playback input must only capture fullscreen bar/drag mouse and scroll, keep gate-false Terraria mouse reads without OS fallback, forbid expected-failure rollback, and preserve paused display-end coverage."
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
        "MapFootprintsPlaybackPrefixLeftDown",
        "MapFootprintsPlaybackPrefixLeftPressed",
        "MapFootprintsPlaybackPrefixLeftReleased",
        "MapFootprintsPlaybackPrefixScrollDelta",
        "MapFootprintsPlaybackPrefixGameUpdateCount",
        "MapFootprintsPlaybackPrefixMainMouseLeftBefore",
        "MapFootprintsPlaybackPrefixMainMouseLeftAfter",
        "MapFootprintsPlaybackPrefixMainMouseLeftReleaseBefore",
        "MapFootprintsPlaybackPrefixMainMouseLeftReleaseAfter",
        "MapFootprintsPlaybackPrefixMainMouseInterfaceBefore",
        "MapFootprintsPlaybackPrefixMainMouseInterfaceAfter",
        "MapFootprintsPlaybackPrefixMainBlockMouseBefore",
        "MapFootprintsPlaybackPrefixMainBlockMouseAfter",
        "MapFootprintsPlaybackPrefixPlayerMouseInterfaceBefore",
        "MapFootprintsPlaybackPrefixPlayerMouseInterfaceAfter",
        "MapFootprintsPlaybackPrefixUtc",
        "MapFootprintsPlaybackDrawHitTarget",
        "MapFootprintsPlaybackDrawMouseReadMode",
        "MapFootprintsPlaybackDrawMouseX",
        "MapFootprintsPlaybackDrawMouseY",
        "MapFootprintsPlaybackDrawMouseReadAvailable",
        "MapFootprintsPlaybackDrawBarHovered",
        "MapFootprintsPlaybackDrawMainMouseLeft",
        "MapFootprintsPlaybackDrawMainMouseLeftRelease",
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
        $diagnosticsText.Contains("RecordPlaybackDrawInput") -and
        $missingDiagnosticFields.Count -eq 0 -and
        $testText.Contains("PlayerWorldFootprintsDiagnosticsWrittenToSnapshot") -and
        $testText.Contains("MapFootprintsDrawLongLineThresholdPixels") -and
        $testText.Contains("MapFootprintsDrawLongestStartTileX") -and
        $testText.Contains("MapFootprintsDrawLongestStartScreenX") -and
        $testText.Contains("MapFootprintsPlaybackPrefixMouseReadMode") -and
        $testText.Contains("FullscreenOverlayGateBypass/FullscreenUi") -and
        $testText.Contains("MapFootprintsPlaybackPrefixMouseReadAvailable") -and
        $testText.Contains("MapFootprintsPlaybackPrefixMainMouseLeftAfter") -and
        $testText.Contains("MapFootprintsPlaybackPrefixMainMouseLeftReleaseAfter") -and
        $testText.Contains("MapFootprintsPlaybackDrawMouseReadMode") -and
        $testText.Contains("MapFootprintsPlaybackDrawMouseReadAvailable") -and
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
        $featureDocText.Contains("MapFootprintsPlaybackDrawMouseReadMode") -and
        $featureDocText.Contains("MapFootprintsPlaybackDrawMainMouseLeftRelease") -and
        $featureDocText.Contains("FullscreenOverlayGateBypass") -and
        $featureDocText.Contains("Draw、UI 和 cache 读取路径不得写 JSON") -and
        $diagnosticRulesText.Contains("PlayerWorldFootprintsLastDecision") -and
        $diagnosticRulesText.Contains("MapFootprintsPlaybackProgress") -and
        $diagnosticRulesText.Contains("MapFootprintsDrawMaxLinePixels") -and
        $diagnosticRulesText.Contains("screen 坐标表示最终裁剪后的 draw command 端点") -and
        $diagnosticRulesText.Contains("tile 坐标仍保留原始缓存线段端点") -and
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
Test-MapCustomMarkerGovernance -RepoRoot $repoRoot
Test-MapDirectionHintGovernance -RepoRoot $repoRoot
Test-MapFootprintGovernance -RepoRoot $repoRoot
Test-PlayerWorldExplorationGovernance -RepoRoot $repoRoot
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
