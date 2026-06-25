$lines = Get-Content 'C:\Users\kongd\Desktop\JueMingZ\scripts\audit-project-health.ps1'
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match '^function (Test-[A-Za-z0-9-]+)') {
        Write-Output ('{0}:{1}' -f ($i+1), $matches[1])
    }
}
