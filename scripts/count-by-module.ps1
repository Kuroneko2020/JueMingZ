$root = 'C:\Users\kongd\Desktop\JueMingZ'
$srcRoot = (Join-Path $root 'src\JueMingZ')

$dirs = Get-ChildItem -Path $srcRoot -Directory | Where-Object { $_.Name -ne 'bin' -and $_.Name -ne 'obj' }
$dirs += Get-Item (Join-Path $root 'tests\JueMingZ.Tests')

$grandTotal = 0
$grandFiles = 0

foreach ($d in $dirs) {
    $files = Get-ChildItem -Path $d.FullName -Recurse -File -Include '*.cs' |
        Where-Object { $_.Directory.Name -ne 'bin' -and $_.Directory.Name -ne 'obj' }
    $total = 0
    $fileCount = $files.Count
    foreach ($f in $files) {
        $lines = Get-Content -LiteralPath $f.FullName -Encoding UTF8
        $total += $lines.Count
    }
    Write-Host ("[{0,-32}] files={1,4}  lines={2,7}" -f $d.Name, $fileCount, $total)
    $grandTotal += $total
    $grandFiles += $fileCount
}
Write-Host ''
Write-Host ('GRAND TOTAL: files={0}  lines={1}' -f $grandFiles, $grandTotal)
