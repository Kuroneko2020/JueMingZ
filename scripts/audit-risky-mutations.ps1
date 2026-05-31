$ErrorActionPreference = "Stop"

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

function New-AuditMatch {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [int]$LineNumber,

        [Parameter(Mandatory = $true)]
        [string]$Line
    )

    [PSCustomObject]@{
        Path = $Path
        LineNumber = $LineNumber
        Line = $Line
    }
}

function Invoke-RgSearch {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Pattern,

        [Parameter(Mandatory = $true)]
        [string]$SourceRoot
    )

    $rgCommand = Get-Command rg -ErrorAction SilentlyContinue
    if (-not $rgCommand) {
        return $null
    }

    $output = & $rgCommand.Source -n --no-heading $Pattern $SourceRoot -g "*.cs"
    $exitCode = $LASTEXITCODE
    if ($exitCode -eq 1) {
        return @()
    }

    if ($exitCode -ne 0) {
        throw "ripgrep failed with exit code $exitCode"
    }

    $results = @()
    foreach ($line in $output) {
        $parts = $line -split ":", 3
        if ($parts.Count -lt 3) {
            continue
        }

        $results += New-AuditMatch -Path $parts[0] -LineNumber ([int]$parts[1]) -Line $parts[2]
    }

    return $results
}

function Invoke-PowerShellSearch {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Pattern,

        [Parameter(Mandatory = $true)]
        [string]$SourceRoot
    )

    if (-not (Test-Path $SourceRoot)) {
        return @()
    }

    $results = @()
    $matches = Get-ChildItem -Path $SourceRoot -Recurse -Filter "*.cs" -File |
        Select-String -Pattern $Pattern

    foreach ($match in $matches) {
        $results += New-AuditMatch -Path $match.Path -LineNumber $match.LineNumber -Line $match.Line.Trim()
    }

    return $results
}

function Invoke-AuditSearch {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Pattern,

        [Parameter(Mandatory = $true)]
        [string]$SourceRoot,

        [Parameter(Mandatory = $true)]
        [bool]$UseRipgrep
    )

    if ($UseRipgrep) {
        return Invoke-RgSearch -Pattern $Pattern -SourceRoot $SourceRoot
    }

    return Invoke-PowerShellSearch -Pattern $Pattern -SourceRoot $SourceRoot
}

function Test-IsAllowedControlledInputPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RelativePath
    )

    $normalized = $RelativePath.Replace('/', '\')
    return $normalized.StartsWith("src\JueMingZ\Actions\", [System.StringComparison]::OrdinalIgnoreCase) -or
           $normalized.StartsWith("src\JueMingZ\Compat\", [System.StringComparison]::OrdinalIgnoreCase)
}

function Test-IsAllowedControlledBuffMutationPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RelativePath
    )

    $normalized = $RelativePath.Replace('/', '\')
    return $normalized.Equals("src\JueMingZ\Actions\Executors\BuffPotionDirectUseExecutor.cs", [System.StringComparison]::OrdinalIgnoreCase) -or
           $normalized.Equals("src\JueMingZ\Compat\PlayerBuffCompat.cs", [System.StringComparison]::OrdinalIgnoreCase) -or
           $normalized.Equals("src\JueMingZ\Compat\InventoryMutationCompat.cs", [System.StringComparison]::OrdinalIgnoreCase) -or
           $normalized.Equals("src\JueMingZ\Compat\AutoSellCompat.cs", [System.StringComparison]::OrdinalIgnoreCase) -or
           $normalized.Equals("src\JueMingZ\Compat\FishingAutoEquipmentCompat.cs", [System.StringComparison]::OrdinalIgnoreCase) -or
           $normalized.Equals("src\JueMingZ\Compat\MovementSafeLandingEquipmentCompat.cs", [System.StringComparison]::OrdinalIgnoreCase) -or
           $normalized.StartsWith("src\JueMingZ\Automation\BuffAndRecovery\", [System.StringComparison]::OrdinalIgnoreCase)
}

function Test-IsAllowedDirectLocalBuffPotionLiteralPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RelativePath
    )

    $normalized = $RelativePath.Replace('/', '\')
    return $normalized.Equals("src\JueMingZ\Actions\ActionExecutionModes.cs", [System.StringComparison]::OrdinalIgnoreCase) -or
           $normalized.Equals("src\JueMingZ\Actions\Executors\BuffPotionDirectUseExecutor.cs", [System.StringComparison]::OrdinalIgnoreCase) -or
           $normalized.Equals("src\JueMingZ\Compat\PlayerBuffCompat.cs", [System.StringComparison]::OrdinalIgnoreCase) -or
           $normalized.Equals("src\JueMingZ\Compat\InventoryMutationCompat.cs", [System.StringComparison]::OrdinalIgnoreCase)
}

function Test-IsAllowedForbiddenDataReadPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RelativePath,

        [Parameter(Mandatory = $true)]
        [string]$Line
    )

    $normalized = $RelativePath.Replace('/', '\')
    if (-not $normalized.Equals("src\JueMingZ\Compat\TerrariaMainCompat.cs", [System.StringComparison]::OrdinalIgnoreCase)) {
        return $false
    }

    if ($Line -notmatch 'Main\.chest') {
        return $false
    }

    return $Line -notmatch 'Main\.chest\s*(=|\[[^\]]+\]\s*=)'
}

try {
    $repoRoot = Split-Path -Parent $PSScriptRoot
    Set-Location $repoRoot

    $sourceRoot = "src\JueMingZ"
    $useRipgrep = [bool](Get-Command rg -ErrorAction SilentlyContinue)

    if ($useRipgrep) {
        Write-Host "Audit mode: ripgrep"
    }
    else {
        Write-Host "Audit mode: PowerShell fallback"
    }

    $forbiddenPattern = 'player\.inventory\[|player\.armor\[|Main\.chest|chest\.item|NetMessage\.SendData|player\.Teleport|statLife\s*(=|\+=|-=)|statMana\s*(=|\+=|-=)'
    $controlledBuffMutationPattern = 'AddBuff|TurnToAir|item\.stack\s*(--|=)|TrySetInt\s*\([^)]*"stack"'
    $directLocalBuffPotionLiteralPattern = 'DirectLocalBuffPotion'
    $controlledInputPattern = 'Main\.mouseX|Main\.mouseY|controlUseItem|releaseUseItem|controlUseTile|releaseUseTile|controlJump|releaseJump|controlUp|releaseUp|rocketRelease|controlMount|releaseMount|controlHook|releaseHook|mouseInterface|blockMouse|\.selectedItem\s*=|SetMember\s*\([^)]*"selectedItem"|selectedItem\s*=\s*target|SetMember\s*\([^)]*"controlDash"|SetMember\s*\([^)]*"releaseDash"|SetMember\s*\([^)]*"dashType"'

    $forbiddenMatches = Invoke-AuditSearch -Pattern $forbiddenPattern -SourceRoot $sourceRoot -UseRipgrep $useRipgrep
    $controlledBuffMatches = Invoke-AuditSearch -Pattern $controlledBuffMutationPattern -SourceRoot $sourceRoot -UseRipgrep $useRipgrep
    $directLocalBuffPotionLiteralMatches = @(Invoke-AuditSearch -Pattern $directLocalBuffPotionLiteralPattern -SourceRoot $sourceRoot -UseRipgrep $useRipgrep |
        Where-Object { $_.Line -match '"[^"]*DirectLocalBuffPotion[^"]*"' })
    $controlledMatches = Invoke-AuditSearch -Pattern $controlledInputPattern -SourceRoot $sourceRoot -UseRipgrep $useRipgrep

    $invalidForbiddenMatches = @()
    if ($forbiddenMatches -and $forbiddenMatches.Count -gt 0) {
        foreach ($match in $forbiddenMatches) {
            $relativePath = Get-RelativePath -Root $repoRoot -Path $match.Path
            if (-not (Test-IsAllowedForbiddenDataReadPath -RelativePath $relativePath -Line $match.Line)) {
                $invalidForbiddenMatches += [PSCustomObject]@{
                    RelativePath = $relativePath
                    LineNumber = $match.LineNumber
                    Line = $match.Line
                }
            }
        }
    }

    if ($invalidForbiddenMatches.Count -gt 0) {
        Write-Host "Forbidden data mutation: found"
        foreach ($match in $invalidForbiddenMatches) {
            Write-Host ("{0}:{1}: {2}" -f $match.RelativePath, $match.LineNumber, $match.Line)
        }

        exit 1
    }

    Write-Host "Forbidden data mutation: none"

    $invalidControlledBuffMatches = @()
    if ($controlledBuffMatches -and $controlledBuffMatches.Count -gt 0) {
        foreach ($match in $controlledBuffMatches) {
            $relativePath = Get-RelativePath -Root $repoRoot -Path $match.Path
            if (-not (Test-IsAllowedControlledBuffMutationPath -RelativePath $relativePath)) {
                $invalidControlledBuffMatches += [PSCustomObject]@{
                    RelativePath = $relativePath
                    LineNumber = $match.LineNumber
                    Line = $match.Line
                }
            }
        }
    }

    if ($invalidControlledBuffMatches.Count -gt 0) {
        Write-Host "Controlled local buff mutation: found outside allowed paths"
        foreach ($match in $invalidControlledBuffMatches) {
            Write-Host ("{0}:{1}: {2}" -f $match.RelativePath, $match.LineNumber, $match.Line)
        }

        exit 1
    }

    if ($controlledBuffMatches -and $controlledBuffMatches.Count -gt 0) {
        Write-Host "Controlled local buff mutation: found only in allowed paths"
        foreach ($match in $controlledBuffMatches) {
            $relativePath = Get-RelativePath -Root $repoRoot -Path $match.Path
            Write-Host ("  allowed: {0}:{1}: {2}" -f $relativePath, $match.LineNumber, $match.Line)
        }
    }
    else {
        Write-Host "Controlled local buff mutation: none"
    }

    $invalidDirectLocalBuffPotionLiteralMatches = @()
    if ($directLocalBuffPotionLiteralMatches -and $directLocalBuffPotionLiteralMatches.Count -gt 0) {
        foreach ($match in $directLocalBuffPotionLiteralMatches) {
            $relativePath = Get-RelativePath -Root $repoRoot -Path $match.Path
            if (-not (Test-IsAllowedDirectLocalBuffPotionLiteralPath -RelativePath $relativePath)) {
                $invalidDirectLocalBuffPotionLiteralMatches += [PSCustomObject]@{
                    RelativePath = $relativePath
                    LineNumber = $match.LineNumber
                    Line = $match.Line
                }
            }
        }
    }

    if ($invalidDirectLocalBuffPotionLiteralMatches.Count -gt 0) {
        Write-Host "DirectLocalBuffPotion literal: found outside allowed paths"
        foreach ($match in $invalidDirectLocalBuffPotionLiteralMatches) {
            Write-Host ("{0}:{1}: {2}" -f $match.RelativePath, $match.LineNumber, $match.Line)
        }

        exit 1
    }

    if ($directLocalBuffPotionLiteralMatches -and $directLocalBuffPotionLiteralMatches.Count -gt 0) {
        Write-Host "DirectLocalBuffPotion literal: found only in allowed paths"
        foreach ($match in $directLocalBuffPotionLiteralMatches) {
            $relativePath = Get-RelativePath -Root $repoRoot -Path $match.Path
            Write-Host ("  allowed: {0}:{1}: {2}" -f $relativePath, $match.LineNumber, $match.Line)
        }
    }
    else {
        Write-Host "DirectLocalBuffPotion literal: none"
    }

    $invalidControlledMatches = @()
    if ($controlledMatches -and $controlledMatches.Count -gt 0) {
        foreach ($match in $controlledMatches) {
            $relativePath = Get-RelativePath -Root $repoRoot -Path $match.Path
            if (-not (Test-IsAllowedControlledInputPath -RelativePath $relativePath)) {
                $invalidControlledMatches += [PSCustomObject]@{
                    RelativePath = $relativePath
                    LineNumber = $match.LineNumber
                    Line = $match.Line
                }
            }
        }
    }

    if ($invalidControlledMatches.Count -gt 0) {
        Write-Host "Controlled input writes: found outside allowed paths"
        foreach ($match in $invalidControlledMatches) {
            Write-Host ("{0}:{1}: {2}" -f $match.RelativePath, $match.LineNumber, $match.Line)
        }

        exit 1
    }

    if ($controlledMatches -and $controlledMatches.Count -gt 0) {
        Write-Host "Controlled input writes: found only in allowed paths"
        foreach ($match in $controlledMatches) {
            $relativePath = Get-RelativePath -Root $repoRoot -Path $match.Path
            Write-Host ("  allowed: {0}:{1}: {2}" -f $relativePath, $match.LineNumber, $match.Line)
        }
    }
    else {
        Write-Host "Controlled input writes: none"
    }

    Write-Host "Audit passed"
    exit 0
}
catch {
    Write-Host ("Risk audit script failed: {0}" -f $_.Exception.Message)
    exit 2
}
