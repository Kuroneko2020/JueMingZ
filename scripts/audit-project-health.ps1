# audit-project-health.ps1 - JueMing-Z project health audit entry point.
# AutoDetect maps changed files to code-domain audit scopes, then delegates to
# audit-project-health-full.ps1 for the actual checks.
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
        "Blueprint", "BlueprintCreation", "BlueprintPlacement", "BlueprintTransform", "BlueprintAutoPlacement", "BlueprintHandheld", "BlueprintDiagnostics",
        "ActionQueue", "Input", "Feature", "Diagnostics", "Runtime", "Config", "GameState", "Structure",
        "Combat", "Fishing", "Movement", "Map", "Information", "UI", "Hotkey", "Notes", "Items", "Buffs", "World", "Search", "Exploration", "Npc"
    )]
    [string[]]$AuditScope = @("All"),
    [switch]$AutoDetect
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$fullScript = Join-Path $scriptDir "audit-project-health-full.ps1"

function Normalize-AuditPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    $normalized = ($Path -replace '\\', '/').Trim()
    while ($normalized.StartsWith("./")) {
        $normalized = $normalized.Substring(2)
    }

    return $normalized
}

function Test-AuditPathMatches {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string[]]$Patterns
    )

    foreach ($pattern in $Patterns) {
        if ([string]::IsNullOrWhiteSpace($pattern)) {
            continue
        }

        if ($Path.StartsWith($pattern, [System.StringComparison]::OrdinalIgnoreCase) -or
            $Path.IndexOf($pattern, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
            return $true
        }
    }

    return $false
}

function Test-DocumentationOnlyPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    return $Path.StartsWith("文档/", [System.StringComparison]::OrdinalIgnoreCase) -or
           $Path.StartsWith("docs/", [System.StringComparison]::OrdinalIgnoreCase) -or
           $Path.EndsWith(".md", [System.StringComparison]::OrdinalIgnoreCase)
}

function Add-AuditScope {
    param(
        [System.Collections.Generic.HashSet[string]]$Scopes,
        [Parameter(Mandatory = $true)][string]$Scope
    )

    if ($null -ne $Scopes -and -not [string]::IsNullOrWhiteSpace($Scope)) {
        [void]$Scopes.Add($Scope)
    }
}

function Add-AuditScopeDependencies {
    param([System.Collections.Generic.HashSet[string]]$Scopes)

    if ($null -eq $Scopes) {
        return
    }

    if ($Scopes.Contains("BlueprintAutoPlacement")) {
        Add-AuditScope -Scopes $Scopes -Scope "ActionQueue"
        Add-AuditScope -Scopes $Scopes -Scope "Input"
        Add-AuditScope -Scopes $Scopes -Scope "Feature"
        Add-AuditScope -Scopes $Scopes -Scope "BlueprintDiagnostics"
    }

    if ($Scopes.Contains("BlueprintTransform") -or $Scopes.Contains("BlueprintPlacement")) {
        Add-AuditScope -Scopes $Scopes -Scope "BlueprintDiagnostics"
        Add-AuditScope -Scopes $Scopes -Scope "Feature"
    }

    if ($Scopes.Contains("BlueprintCreation")) {
        Add-AuditScope -Scopes $Scopes -Scope "BlueprintHandheld"
        Add-AuditScope -Scopes $Scopes -Scope "BlueprintDiagnostics"
        Add-AuditScope -Scopes $Scopes -Scope "Feature"
    }

    if ($Scopes.Contains("BlueprintHandheld")) {
        Add-AuditScope -Scopes $Scopes -Scope "UI"
        Add-AuditScope -Scopes $Scopes -Scope "Input"
        Add-AuditScope -Scopes $Scopes -Scope "Feature"
    }

    if ($Scopes.Contains("BlueprintDiagnostics")) {
        Add-AuditScope -Scopes $Scopes -Scope "Diagnostics"
        Add-AuditScope -Scopes $Scopes -Scope "Runtime"
    }

    if ($Scopes.Contains("ActionQueue") -or $Scopes.Contains("Input")) {
        Add-AuditScope -Scopes $Scopes -Scope "Diagnostics"
    }

    if ($Scopes.Contains("Runtime") -or $Scopes.Contains("GameState")) {
        Add-AuditScope -Scopes $Scopes -Scope "Diagnostics"
    }
}

function Expand-AuditScopesWithDependencies {
    param([string[]]$Scopes)

    if ($null -eq $Scopes -or $Scopes.Count -eq 0) {
        return @("All")
    }

    $expanded = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($scope in $Scopes) {
        Add-AuditScope -Scopes $expanded -Scope $scope
    }

    if ($expanded.Contains("All")) {
        return @("All")
    }

    Add-AuditScopeDependencies -Scopes $expanded
    return @($expanded)
}

function Resolve-AutoDetectAuditScopes {
    param([Parameter(Mandatory = $true)][string[]]$ChangedFiles)

    $globalPatterns = @(
        "scripts/",
        "src/JueMingZ/Bootstrap/",
        "src/JueMingZ/Compat/",
        "src/JueMingZ/Hooks/",
        "src/JueMingZ/Common/",
        "src/JueMingZ/Records/",
        "tests/JueMingZ.Tests/Program.cs"
    )

    $rules = @(
        @{ Scope = "BlueprintCreation"; Patterns = @(
                "src/JueMingZ/Automation/Blueprint/BlueprintCreation",
                "src/JueMingZ/Automation/Blueprint/BlueprintCapture",
                "src/JueMingZ/Automation/Blueprint/BlueprintEntryState.cs",
                "src/JueMingZ/UI/BlueprintCreationOverlay.cs",
                "src/JueMingZ/Compat/TerrariaBlueprintCreationPromptCompat.cs",
                "tests/JueMingZ.Tests/Program.BlueprintCreationTests.cs",
                "tests/JueMingZ.Tests/Program.BlueprintCaptureTests.cs"
            ) },
        @{ Scope = "BlueprintPlacement"; Patterns = @(
                "src/JueMingZ/Automation/Blueprint/BlueprintPlacement",
                "src/JueMingZ/Automation/Blueprint/BlueprintPlacedInstance",
                "src/JueMingZ/Automation/Blueprint/BlueprintProjectionService.cs",
                "src/JueMingZ/Automation/Blueprint/BlueprintWorldInstance",
                "src/JueMingZ/UI/BlueprintProjectionOverlay.cs",
                "tests/JueMingZ.Tests/Program.BlueprintPlacementPreviewTests.cs",
                "tests/JueMingZ.Tests/Program.BlueprintCaptureTests.cs",
                "tests/JueMingZ.Tests/Program.BlueprintPlacedInstanceTests.cs"
            ) },
        @{ Scope = "BlueprintTransform"; Patterns = @(
                "src/JueMingZ/Automation/Blueprint/BlueprintMirrorService.cs",
                "src/JueMingZ/Automation/Blueprint/BlueprintPlacedInstanceTransformState.cs",
                "src/JueMingZ/UI/BlueprintPlacedInstanceTransformOverlay.cs",
                "tests/JueMingZ.Tests/Program.BlueprintMirrorTests.cs",
                "tests/JueMingZ.Tests/Program.BlueprintPlacedInstanceTests.cs"
            ) },
        @{ Scope = "BlueprintAutoPlacement"; Patterns = @(
                "src/JueMingZ/Automation/Blueprint/BlueprintAutoPlacementService.cs",
                "src/JueMingZ/Automation/Blueprint/BlueprintMaterialService.cs",
                "src/JueMingZ/Actions/Executors/BlueprintAutoPlaceActionExecutor.cs",
                "tests/JueMingZ.Tests/Program.BlueprintAutoPlacementTests.cs"
            ) },
        @{ Scope = "BlueprintHandheld"; Patterns = @(
                "src/JueMingZ/Automation/Blueprint/BlueprintHandheldActionBarState.cs",
                "src/JueMingZ/UI/BlueprintHandheldActionBarOverlay.cs",
                "src/JueMingZ/UI/DiagnosticMouseStateReader.cs",
                "tests/JueMingZ.Tests/Program.BlueprintHandheldActionBarTests.cs",
                "tests/JueMingZ.Tests/Program.BlueprintHandheldUiClickOwnershipTests.cs"
            ) },
        @{ Scope = "BlueprintDiagnostics"; Patterns = @(
                "src/JueMingZ/Automation/Blueprint/BlueprintDiagnostics.cs",
                "src/JueMingZ/Runtime/Diagnostics/RuntimeDiagnosticSnapshotBuilder.Blueprint.cs",
                "tests/JueMingZ.Tests/Program.BlueprintDiagnosticsTests.cs"
            ) },
        @{ Scope = "Blueprint"; Patterns = @(
                "src/JueMingZ/UI/Legacy/LegacyMainWindow.Blueprint",
                "src/JueMingZ/Input/LegacyUiActionService.Blueprint.cs",
                "src/JueMingZ/Input/BlueprintEntryHotkeyService.cs",
                "src/JueMingZ/UI/Legacy/BlueprintLibraryUiState.cs",
                "tests/JueMingZ.Tests/Program.BlueprintEntryTests.cs"
            ) },
        @{ Scope = "ActionQueue"; Patterns = @(
                "src/JueMingZ/Actions/InputActionQueue",
                "src/JueMingZ/Actions/ItemUseBridge",
                "src/JueMingZ/Actions/ItemCheckWriterArbiter",
                "src/JueMingZ/Actions/Channels/",
                "src/JueMingZ/Actions/Executors/UseSelectedItemActionExecutor.cs",
                "src/JueMingZ/Actions/Executors/UseHotbarItemActionExecutor.cs"
            ) },
        @{ Scope = "Input"; Patterns = @(
                "src/JueMingZ/Input/",
                "src/JueMingZ/UI/UiMouseCaptureService.cs",
                "src/JueMingZ/UI/UiPointerOwnershipService.cs"
            ) },
        @{ Scope = "Diagnostics"; Patterns = @(
                "src/JueMingZ/Diagnostics/",
                "src/JueMingZ/Runtime/Diagnostics/"
            ) },
        @{ Scope = "Feature"; Patterns = @(
                "src/JueMingZ/Features/",
                "src/JueMingZ/Common/FeatureIds.cs"
            ) },
        @{ Scope = "Runtime"; Patterns = @(
                "src/JueMingZ/Runtime/"
            ) },
        @{ Scope = "Config"; Patterns = @(
                "src/JueMingZ/Config/"
            ) },
        @{ Scope = "GameState"; Patterns = @(
                "src/JueMingZ/GameState/"
            ) },
        @{ Scope = "UI"; Patterns = @(
                "src/JueMingZ/UI/"
            ) },
        @{ Scope = "Combat"; Patterns = @(
                "src/JueMingZ/Automation/Combat/"
            ) },
        @{ Scope = "Fishing"; Patterns = @(
                "src/JueMingZ/Automation/Fishing/"
            ) },
        @{ Scope = "Movement"; Patterns = @(
                "src/JueMingZ/Automation/Movement/"
            ) },
        @{ Scope = "Map"; Patterns = @(
                "src/JueMingZ/Automation/MapEnhancement/",
                "src/JueMingZ/Automation/WorldAutomation/",
                "src/JueMingZ/UI/Map"
            ) },
        @{ Scope = "Information"; Patterns = @(
                "src/JueMingZ/Automation/Information/",
                "src/JueMingZ/UI/Information/"
            ) },
        @{ Scope = "Items"; Patterns = @(
                "src/JueMingZ/Automation/InventoryAndItems/"
            ) },
        @{ Scope = "Buffs"; Patterns = @(
                "src/JueMingZ/Automation/BuffAndRecovery/",
                "src/JueMingZ/Automation/AutoRecovery/"
            ) },
        @{ Scope = "Npc"; Patterns = @(
                "src/JueMingZ/Automation/NpcServices/"
            ) }
    )

    $scopes = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    Add-AuditScope -Scopes $scopes -Scope "Base"

    foreach ($rawFile in $ChangedFiles) {
        $file = Normalize-AuditPath -Path $rawFile
        if ([string]::IsNullOrWhiteSpace($file) -or (Test-DocumentationOnlyPath -Path $file)) {
            continue
        }

        if (Test-AuditPathMatches -Path $file -Patterns $globalPatterns) {
            $scopes.Clear()
            Add-AuditScope -Scopes $scopes -Scope "All"
            break
        }

        $matched = $false
        foreach ($rule in $rules) {
            if (Test-AuditPathMatches -Path $file -Patterns ([string[]]$rule.Patterns)) {
                Add-AuditScope -Scopes $scopes -Scope ([string]$rule.Scope)
                $matched = $true
            }
        }

        if (-not $matched) {
            Add-AuditScope -Scopes $scopes -Scope "All"
            break
        }
    }

    if ($scopes.Contains("All")) {
        return @("All")
    }

    Add-AuditScopeDependencies -Scopes $scopes
    return @($scopes)
}

if ($AutoDetect) {
    $repoRoot = Split-Path -Parent $scriptDir
    Push-Location $repoRoot
    $prevErrorAction = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $changedFiles = @(git -c core.safecrlf=false diff --name-only HEAD 2>&1 | Where-Object { $_ -notmatch '^warning:' })
        if ($changedFiles.Count -eq 0) {
            $changedFiles = @(git -c core.safecrlf=false diff --name-only --cached 2>&1 | Where-Object { $_ -notmatch '^warning:' })
        }

        if ($changedFiles.Count -eq 0) {
            $AuditScope = @("Base")
            Write-Host "AutoDetect: no source changes -> base checks only."
        }
        else {
            $AuditScope = @(Resolve-AutoDetectAuditScopes -ChangedFiles $changedFiles)
            Write-Host "AutoDetect: $($AuditScope -join ', ')"
        }
    }
    finally {
        $ErrorActionPreference = $prevErrorAction
        Pop-Location
    }
}

$AuditScope = @(Expand-AuditScopesWithDependencies -Scopes $AuditScope)

$fullArgs = @{
    IncludeSourcePackageZip = $IncludeSourcePackageZip
    AllowTestPackageReadme = $AllowTestPackageReadme
    RequireFreshTestPackage = $RequireFreshTestPackage
    AuditProfile = $AuditProfile
    AuditScope = $AuditScope
}

& $fullScript @fullArgs
exit $LASTEXITCODE
