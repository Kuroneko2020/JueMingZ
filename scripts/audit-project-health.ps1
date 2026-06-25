# audit-project-health.ps1 — JueMing-Z project health audit
# This is the main entry point. It supports -Scope, -AutoDetect, and delegates to
# audit-project-health-full.ps1 for actual checks.
param(
    [switch]$IncludeSourcePackageZip,
    [switch]$AllowTestPackageReadme,
    [switch]$RequireFreshTestPackage,
    [Alias("Profile")]
    [ValidateSet("Full", "Fast")]
    [string]$AuditProfile = "Full",
    [Alias("Scope")]
    [ValidateSet("All", "Base", "Blueprint", "Combat", "Fishing", "Movement", "Map", "Information", "UI",
                 "Hotkey", "Diagnostics", "Notes", "ActionQueue", "Feature", "Structure",
                 "Items", "Buffs", "World", "Search", "Exploration", "Npc")]
    [string[]]$AuditScope = @("All"),
    [switch]$AutoDetect
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$fullScript = Join-Path $scriptDir "audit-project-health-full.ps1"

# ---- AutoDetect: infer scope from git diff ----
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
            Write-Host "AutoDetect: no source changes → base checks only."
        } else {
            # Shared base — any change triggers full audit
            $sharedPatterns = @(
                "Compat/", "Hooks/", "Runtime/", "Actions/", "Bootstrap/",
                "Diagnostics/", "Config/", "GameState/", "Features/", "Input/",
                "Common/", "Records/"
            )
            # Function domain → scope mapping
            $domainMap = @{
                "Blueprint" = @("Automation/Blueprint/", "UI/Blueprint", "UI/Legacy/LegacyMainWindow.Blueprint")
                "Combat"    = @("Automation/Combat/")
                "Fishing"   = @("Automation/Fishing/", "UI/Legacy/LegacyMainWindow.Fishing")
                "Movement"  = @("Automation/Movement/")
                "Map"       = @("Automation/MapEnhancement/", "Automation/WorldAutomation/", "UI/Map", "UI/Legacy/LegacyMainWindow.MapEnhancement")
                "Information" = @("Automation/Information/", "UI/Information/", "UI/Legacy/LegacyMainWindow.Information")
                "Items"     = @("Automation/InventoryAndItems/", "UI/Legacy/LegacyMainWindow.Items")
                "Buffs"     = @("Automation/BuffAndRecovery/", "Automation/AutoRecovery/", "UI/Legacy/LegacyMainWindow.Rows.Buff")
                "World"     = @("Automation/WorldAutomation/", "UI/Legacy/LegacyMainWindow.Misc.Auto")
                "Search"    = @("Automation/Search/", "UI/Legacy/LegacyMainWindow.Search")
                "UI"        = @("UI/Legacy/LegacyMainWindow.cs", "UI/Legacy/LegacyMainWindow.Pages", "UI/Legacy/LegacyMainWindow.Shared", "UI/Legacy/LegacyMainWindow.Shell", "UI/Legacy/LegacyMainWindow.StateApi", "UI/Legacy/LegacyMainWindow.Rows", "UI/UiTextRenderer.cs", "UI/UiPrimitiveRenderer.cs", "UI/UiMouseCaptureService.cs", "UI/UiPointerOwnershipService.cs")
                "Hotkey"    = @("Input/FeatureToggleHotkey", "Input/LegacyUiActionService.FeatureToggleHotkeys")
                "Notes"     = @("Automation/Information/Notes/", "UI/Legacy/LegacyMainWindow.Notes", "UI/UserNotes")
                "Npc"       = @("Automation/NpcServices/")
                "Diagnostics" = @("UI/DiagnosticsOverlay.cs", "UI/Legacy/LegacyMainWindow.Diagnostics")
            }

            $scopes = [System.Collections.Generic.HashSet[string]]::new()
            $scopes.Add("Base") | Out-Null

            foreach ($file in $changedFiles) {
                $isShared = $false
                foreach ($pat in $sharedPatterns) {
                    if ($file.StartsWith($pat)) {
                        $scopes.Clear(); $scopes.Add("All") | Out-Null
                        $isShared = $true
                        break
                    }
                }
                if ($isShared) { break }

                foreach ($domain in $domainMap.Keys) {
                    foreach ($pat in $domainMap[$domain]) {
                        if ($file.StartsWith($pat) -or $file.Contains($pat)) {
                            $scopes.Add($domain) | Out-Null
                            break
                        }
                    }
                }
            }

            if ($scopes.Contains("All")) {
                $AuditScope = @("All")
                Write-Host "AutoDetect: shared base changed → full audit."
            } else {
                # Also add dependent scopes: Blueprint depends on ActionQueue+Feature
                if ($scopes.Contains("Blueprint")) { $scopes.Add("ActionQueue") | Out-Null; $scopes.Add("Feature") | Out-Null }
                if ($scopes.Contains("Combat") -or $scopes.Contains("Movement") -or $scopes.Contains("Fishing")) { $scopes.Add("ActionQueue") | Out-Null }
                if ($scopes.Contains("Map") -or $scopes.Contains("Information")) { $scopes.Add("Diagnostics") | Out-Null }

                $AuditScope = @($scopes)
                Write-Host "AutoDetect: $($AuditScope -join ', ')"
            }
        }
    } finally {
        $ErrorActionPreference = $prevErrorAction
        Pop-Location
    }
}

# ---- Run full script with resolved scope ----
$args = @("-File", $fullScript)
if ($IncludeSourcePackageZip)    { $args += "-IncludeSourcePackageZip" }
if ($AllowTestPackageReadme)     { $args += "-AllowTestPackageReadme" }
if ($RequireFreshTestPackage)    { $args += "-RequireFreshTestPackage" }
$args += "-AuditProfile", $AuditProfile
$args += "-AuditScope"
$args += $AuditScope

& powershell -ExecutionPolicy Bypass @args
exit $LASTEXITCODE
