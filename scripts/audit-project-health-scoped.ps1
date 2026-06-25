param(
    [switch]$IncludeSourcePackageZip,
    [switch]$AllowTestPackageReadme,
    [switch]$RequireFreshTestPackage,
    [Alias("Profile")]
    [ValidateSet("Full", "Fast")]
    [string]$AuditProfile = "Full",
    [Alias("Scope")]
    [ValidateSet("All", "Base", "Information", "UI", "Combat", "Diagnostics", "Map", "Hotkey", "Blueprint", "Notes", "Exploration", "ActionQueue", "Feature", "Structure", "Fishing", "Movement", "Items", "Buffs", "World")]
    [string[]]$AuditScope = @("All"),
    [switch]$AutoDetect
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$originalScript = Join-Path $scriptDir "audit-project-health-full.ps1"

# ---- AutoDetect: git diff to infer scope ----
if ($AutoDetect) {
    $repoRoot = Split-Path -Parent $scriptDir
    Push-Location $repoRoot
    try {
        $changedFiles = @(git diff --name-only HEAD 2>$null)
        if ($LASTEXITCODE -ne 0 -or $changedFiles.Count -eq 0) {
            $changedFiles = @(git diff --name-only --cached 2>$null)
        }
        if ($changedFiles.Count -eq 0) {
            # No changes detected, run only base checks
            $AuditScope = @("Base")
            Write-Host "AutoDetect: no source changes detected; running base checks only."
        } else {
            # Map file paths to scopes
            $scopes = [System.Collections.Generic.HashSet[string]]::new()
            $scopes.Add("Base") | Out-Null  # Base always runs

            # Shared base: any change to these → All (full audit)
            $sharedPatterns = @(
                "Compat/", "Hooks/", "Runtime/", "Actions/", "Bootstrap/",
                "Diagnostics/", "Config/", "GameState/", "Features/", "Input/",
                "Common/", "Records/", "UI/Legacy/LegacyMainWindow.cs",
                "UI/UiTextRenderer.cs", "UI/UiPrimitiveRenderer.cs"
            )
            $functionPatterns = @{
                "Blueprint" = @("Automation/Blueprint/", "UI/*Blueprint*", "UI/Legacy/*Blueprint*")
                "Combat"    = @("Automation/Combat/", "UI/*Combat*")
                "Fishing"   = @("Automation/Fishing/", "UI/*Fishing*")
                "Movement"  = @("Automation/Movement/", "UI/*Movement*", "UI/*SafeLanding*")
                "Map"       = @("Automation/MapEnhancement/", "Automation/WorldAutomation/", "UI/*Map*", "UI/*Footprint*", "UI/*Marker*")
                "Information" = @("Automation/Information/", "UI/Information/", "UI/*Chest*", "UI/*Npc*")
                "Items"     = @("Automation/InventoryAndItems/")
                "Buffs"     = @("Automation/BuffAndRecovery/", "Automation/AutoRecovery/")
                "World"     = @("Automation/WorldAutomation/")
                "Search"    = @("Automation/Search/", "UI/*Search*", "UI/*Chest*")
                "UI"        = @("UI/", "UI/*Overlay*", "UI/*Panel*", "UI/*Button*", "UI/*Control*")
                "Hotkey"    = @("Input/FeatureToggleHotkey*", "UI/*Hotkey*")
                "Diagnostics" = @("Diagnostics/", "UI/Diagnostics*")
                "Notes"     = @("Automation/Information/Notes/", "UI/*Notes*")
            }

            foreach ($file in $changedFiles) {
                $matched = $false
                foreach ($pattern in $sharedPatterns) {
                    if ($file -like "*$pattern*") {
                        $scopes.Clear()
                        $scopes.Add("All") | Out-Null
                        $matched = $true
                        break
                    }
                }
                if ($matched) { break }

                foreach ($scope in $functionPatterns.Keys) {
                    foreach ($pat in $functionPatterns[$scope]) {
                        if ($file -like "*$pat*") {
                            $scopes.Add($scope) | Out-Null
                            $matched = $true
                            break
                        }
                    }
                }

                if (-not $matched) {
                    # Only doc/readme changes → just base
                }
            }

            if ($scopes.Contains("All")) {
                $AuditScope = @("All")
                Write-Host "AutoDetect: shared base changed; running full audit."
            } else {
                $AuditScope = @($scopes)
                Write-Host "AutoDetect: detected scopes: $($AuditScope -join ', ')"
            }
        }
    } finally {
        Pop-Location
    }
}

# If All scope, delegate directly to the full original script (backward compatible)
if ($AuditScope -contains "All") {
    $args = @("-File", $originalScript)
    if ($IncludeSourcePackageZip) { $args += "-IncludeSourcePackageZip" }
    if ($AllowTestPackageReadme) { $args += "-AllowTestPackageReadme" }
    if ($RequireFreshTestPackage) { $args += "-RequireFreshTestPackage" }
    $args += "-AuditProfile", $AuditProfile
    $args += "-AuditScope", "All"
    & powershell -ExecutionPolicy Bypass @args
    exit $LASTEXITCODE
}

# ---- Scoped execution: generate lightweight temp script ----
$scopeNames = ($AuditScope | ForEach-Object { $_ }) -join ','
Write-Host "Scoped audit: $scopeNames"

# Read original script
$lines = Get-Content -Encoding UTF8 $originalScript
$functionStarts = @{}
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match '^function (Test-[A-Za-z0-9-]+)') {
        $functionStarts[$matches[1]] = $i
    }
}

# Determine which functions to keep based on scope
$scopeFunctionPatterns = @{
    "Base"        = @("Test-Version", "Test-Git", "Test-SourcePackage", "Test-TestPackage", "Test-Zip", "Test-Docs", "Get-")
    "Blueprint"   = @("Test-Blueprint")
    "Combat"      = @("Test-Combat", "Test-Phaseblade")
    "Fishing"     = @("Test-InformationFishing", "Test-Fishing")
    "Movement"    = @("Test-Movement", "Test-SafeLanding")
    "Map"         = @("Test-Map", "Test-Footprint", "Test-Marker", "Test-Exploration", "Test-QuickAnnouncement", "Test-DirectionHint", "Test-PlayerWorldExploration")
    "Information" = @("Test-Information")
    "UI"          = @("Test-LegacyUi", "Test-F5", "Test-UserNotes")
    "Hotkey"      = @("Test-FeatureToggle", "Test-Hotkey", "Test-Backspace")
    "Diagnostics" = @("Test-Diagnostic", "Test-IterationLog")
    "Notes"       = @("Test-UserNotes")
    "ActionQueue" = @("Test-ActionQueue")
    "Feature"     = @("Test-NewFeature")
    "Structure"   = @("Test-DeepStructure")
    "Items"       = @()
    "Buffs"       = @()
    "World"       = @()
    "Search"      = @()
    "Exploration" = @("Test-PlayerWorldExploration")
}

$keepFunctions = [System.Collections.Generic.HashSet[string]]::new()
foreach ($scope in $AuditScope) {
    if ($scopeFunctionPatterns.ContainsKey($scope)) {
        foreach ($pat in $scopeFunctionPatterns[$scope]) {
            foreach ($fname in $functionStarts.Keys) {
                if ($fname -like "*$pat*") {
                    $keepFunctions.Add($fname) | Out-Null
                }
            }
        }
    }
}

# Base always included
if (-not $AuditScope.Contains("Base")) {
    foreach ($pat in $scopeFunctionPatterns["Base"]) {
        foreach ($fname in $functionStarts.Keys) {
            if ($fname -like "*$pat*") {
                $keepFunctions.Add($fname) | Out-Null
            }
        }
    }
}

# Find function end lines
$functionEnds = @{}
$sortedNames = @($functionStarts.Keys | Sort-Object { $functionStarts[$_] })
for ($idx = 0; $idx -lt $sortedNames.Count; $idx++) {
    $fname = $sortedNames[$idx]
    $start = $functionStarts[$fname]
    if ($idx + 1 -lt $sortedNames.Count) {
        $nextStart = $functionStarts[$sortedNames[$idx + 1]]
        $end = $nextStart - 1
    } else {
        # Last defined function ends at line before Invoke-GovernanceAudit or main exec
        $end = $lines.Count - 1
        for ($j = $start; $j -lt $lines.Count; $j++) {
            if ($lines[$j] -match '^\$repoRoot = ') {
                $end = $j - 1
                break
            }
        }
    }
    $functionEnds[$fname] = $end
}

# Build output: include everything up to first function definition, then only kept functions
$firstFuncLine = ($functionStarts.Values | Measure-Object -Minimum).Minimum
$output = [System.Collections.Generic.List[string]]::new()

# Lines before first function (param + utility functions): always include
for ($i = 0; $i -lt $firstFuncLine; $i++) {
    $output.Add($lines[$i])
}

# Include only kept function definitions + the dispatch block
foreach ($fname in $sortedNames) {
    if ($keepFunctions.Contains($fname)) {
        for ($i = $functionStarts[$fname]; $i -le $functionEnds[$fname]; $i++) {
            $output.Add($lines[$i])
        }
    }
}

# Add scope helpers and main execution block
$inMainExec = $false
for ($i = $functionEnds[$sortedNames[-1]] + 1; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    if ($line -match '^\$repoRoot = ') {
        $inMainExec = $true
    }
    # Replace "All" in audit scope for dispatch
    if ($inMainExec -and ($line -match 'Get-NormalizedAuditScopes') -and ($line -match '"All"')) {
        # Keep the line as-is since we already normalized scopes
    }
    
    # For Invoke-GovernanceAudit, we need to keep the function but it's already being built from kept functions
    # Skip the original Invoke-GovernanceAudit - we'll use a custom one
    if ($line -match '^function Invoke-GovernanceAudit') {
        # Skip until end of this function
        for ($j = $i; $j -lt $lines.Count; $j++) {
            if ($lines[$j] -match '^\}\s*$' -and $j -gt $i + 5) {
                $i = $j
                break
            }
        }
        # Insert custom lightweight governance dispatch
        $output.Add("function Invoke-GovernanceAudit {")
        $output.Add("    param([Parameter(Mandatory = `$true)][string]`$RepoRoot, [Parameter(Mandatory = `$true)][string[]]`$Scopes, [string]`$RuntimeVersion)")
        foreach ($fname in $sortedNames) {
            if ($keepFunctions.Contains($fname)) {
                $output.Add("    $fname -RepoRoot `$RepoRoot")
            }
        }
        $output.Add("}")
        continue
    }
    
    $output.Add($line)
}

# Write temp file and run
$tempFile = Join-Path ([System.IO.Path]::GetTempPath()) "audit-scoped-$(Get-Random).ps1"
[System.IO.File]::WriteAllLines($tempFile, $output, [System.Text.UTF8Encoding]::new($false))

try {
    $args = @("-File", $tempFile)
    if ($IncludeSourcePackageZip) { $args += "-IncludeSourcePackageZip" }
    if ($AllowTestPackageReadme) { $args += "-AllowTestPackageReadme" }
    if ($RequireFreshTestPackage) { $args += "-RequireFreshTestPackage" }
    $args += "-AuditProfile", "Full"
    $args += "-AuditScope", "All"
    & powershell -ExecutionPolicy Bypass @args
    exit $LASTEXITCODE
} finally {
    Remove-Item $tempFile -ErrorAction SilentlyContinue
}
