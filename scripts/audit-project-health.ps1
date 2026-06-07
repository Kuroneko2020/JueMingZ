param(
    [switch]$IncludeSourcePackageZip
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

function Read-TextIfExists {
    param([Parameter(Mandatory = $true)][string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) {
        return $null
    }

    return [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8)
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

function Test-TestPackage {
    param(
        [Parameter(Mandatory = $true)][string]$RepoRoot,
        [Parameter(Mandatory = $true)][string]$RuntimeVersion
    )
    # Audit the user-facing package payload only; absence before packaging is
    # allowed, but stale templates or extra runtime DLLs are failures afterward.
    $packageDir = Join-Path $RepoRoot "JueMingZ-TestPackage"
    if (-not (Test-Path -LiteralPath $packageDir)) {
        Write-WarnHealth "JueMingZ-TestPackage is absent; root absence is allowed before packaging."
        return
    }

    foreach ($name in @("JueMingZ.dll", "Terraria.exe.config", "VERSION.txt")) {
        if (Test-Path -LiteralPath (Join-Path $packageDir $name)) {
            Write-Pass "Test package contains $name"
        }
        else {
            Write-FailHealth "Test package missing first-level file: $name"
        }
    }

    $harmonyPath = Join-Path $packageDir "0Harmony.dll"
    if (Test-Path -LiteralPath $harmonyPath) {
        Write-FailHealth "Test package should not contain external 0Harmony.dll; Harmony is embedded in JueMingZ.dll."
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
            Write-FailHealth "Test package should not contain compile-only Terraria/XNA/ReLogic dependency: $name"
        }
        else {
            Write-Pass "Test package excludes compile-only dependency: $name"
        }
    }

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

    # The Chinese README filename is a Windows-facing delivery contract; non-
    # Windows encoded names require Windows validation before becoming fatal.
    if ($hasChineseReadme) {
        Write-Pass "Test package contains Chinese README file."
    }
    elseif ($hasEncodedReadme -and -not $isWindows) {
        Write-WarnHealth "Chinese README file is absent, but README_#U*.txt was observed outside Windows; require Windows validation before treating this as user-facing failure."
    }
    else {
        Write-FailHealth "Test package missing Chinese README file."
    }

    if ($encoded -and $encoded.Count -gt 0) {
        if ($isWindows) {
            Write-FailHealth "Encoded README_#U*.txt exists in Windows test package."
        }
        else {
            Write-WarnHealth "README_#U*.txt observed outside Windows; do not treat as user-facing failure without Windows validation."
        }
    }
    else {
        Write-Pass "No encoded README_#U*.txt file found in test package."
    }

    if ($hasChineseReadme) {
        $readmeText = Read-TextIfExists -Path $readmePath
        if ($null -eq $readmeText) {
            Write-FailHealth "Test package README could not be read."
        }
        elseif ($readmeText.Contains($RuntimeVersion)) {
            Write-Pass "Test package README contains RuntimeVersion $RuntimeVersion"
        }
        else {
            Write-FailHealth "Test package README does not contain RuntimeVersion $RuntimeVersion"
        }

        $projectRulesDir = ConvertFrom-CodePoints @(0x9879, 0x76ee, 0x89c4, 0x5219)
        $testPackageReadmeTemplateFile = (ConvertFrom-CodePoints @(0x6d4b, 0x8bd5, 0x5305)) + "README" + (ConvertFrom-CodePoints @(0x6a21, 0x677f)) + ".zh-CN.txt"
        $templatePath = Join-LocalDocsPath -RepoRoot $RepoRoot -Segments @($projectRulesDir, $testPackageReadmeTemplateFile)
        $templateText = Read-TextIfExists -Path $templatePath
        if ($null -eq $templateText) {
            Write-FailHealth "Test package README template missing."
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
                Write-FailHealth "Test package README is stale versus template; missing line(s): $($missingTemplateLines -join ' | ')"
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
            Write-FailHealth "Test package README contains stale testing wording: $($staleHits -join ', ')"
        }
        else {
            Write-Pass "Test package README has no known stale focused-test wording."
        }
    }
}

function Test-VersionConsistency {
    param(
        [Parameter(Mandatory = $true)][string]$RepoRoot,
        [Parameter(Mandatory = $true)][string]$RuntimeVersion
    )

    $versionPath = Join-Path $RepoRoot "JueMingZ-TestPackage\VERSION.txt"
    $versionText = Read-TextIfExists -Path $versionPath
    if ($null -ne $versionText) {
        if ($versionText.Contains($RuntimeVersion)) {
            Write-Pass "VERSION.txt contains RuntimeVersion $RuntimeVersion"
        }
        else {
            Write-FailHealth "VERSION.txt does not contain RuntimeVersion $RuntimeVersion"
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

    $currentStatusFile = (ConvertFrom-CodePoints @(0x5f53, 0x524d, 0x4ed3, 0x5e93, 0x72b6, 0x6001)) + ".md"
    $coldStartFile = (ConvertFrom-CodePoints @(0x51b7, 0x542f, 0x52a8, 0x8bf4, 0x660e)) + ".md"
    $directoryFile = (ConvertFrom-CodePoints @(0x76ee, 0x5f55)) + ".md"
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
    elseif ($documentationGuide.Contains($currentStatusFile) -and
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
    $currentStatus = Read-TextIfExists -Path $currentStatusPath
    if ($null -eq $currentStatus) {
        Write-FailHealth "New current status document missing."
    }
    else {
        if ($currentStatus.Contains($RuntimeVersion)) {
            Write-Pass "New current status contains RuntimeVersion $RuntimeVersion"
        }
        else {
            Write-FailHealth "New current status does not contain RuntimeVersion $RuntimeVersion"
        }

        if ($currentStatus.Contains($directoryFile) -and
            $currentStatus.Contains($documentationGuideFile) -and
            $currentStatus.Contains($migrationMapFile)) {
            Write-Pass "New current status references the new documentation rules instead of duplicating them."
        }
        else {
            Write-FailHealth "New current status lacks expected documentation rule references."
        }

        $length = (Get-Item -LiteralPath $currentStatusPath).Length
        $currentStatusGuard = Get-DocumentationSizeGuard -RepoRoot $RepoRoot -Kind "CurrentStatus"
        if ($length -gt $currentStatusGuard.Limit) {
            Write-FailHealth "New current status is over adaptive size guard ($length > $($currentStatusGuard.Limit) bytes; registeredFeatures=$($currentStatusGuard.FeatureCount)); move historical details to update records or diagnostics docs."
        }
        else {
            Write-Pass "New current status stays below adaptive size guard ($length <= $($currentStatusGuard.Limit) bytes; registeredFeatures=$($currentStatusGuard.FeatureCount))."
        }
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
    Test-VersionConsistency -RepoRoot $repoRoot -RuntimeVersion $runtimeVersion
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
    Test-TestPackage -RepoRoot $repoRoot -RuntimeVersion $runtimeVersion
}
else {
    Test-TestPackage -RepoRoot $repoRoot -RuntimeVersion "Unknown"
}
Test-InformationFishingFallbackCleanup -RepoRoot $repoRoot
Test-IterationLogNumbers -RepoRoot $repoRoot

if ($script:FailCount -gt 0) {
    Write-Host "FAIL project health audit completed with $script:FailCount failure(s), $script:WarnCount warning(s)."
    exit 1
}

Write-Host "PASS project health audit completed with 0 failures, $script:WarnCount warning(s)."
exit 0
