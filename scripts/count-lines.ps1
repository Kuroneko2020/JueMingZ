$root = 'C:\Users\kongd\Desktop\JueMingZ'
$dirs = @(
    @{Path = (Join-Path $root 'src\JueMingZ'); Label = 'src/JueMingZ'},
    @{Path = (Join-Path $root 'tests\JueMingZ.Tests'); Label = 'tests/JueMingZ.Tests'}
)
$exts = @('*.cs')

$grandTotal = 0
$grandFiles = 0
$grandEmpty = 0
$grandComment = 0

foreach ($d in $dirs) {
    $files = Get-ChildItem -Path $d.Path -Recurse -File -Include $exts |
        Where-Object { $_.Directory.Name -ne 'bin' -and $_.Directory.Name -ne 'obj' }
    $total = 0
    $fileCount = 0
    $emptyLines = 0
    $commentLines = 0
    foreach ($f in $files) {
        $lines = Get-Content -LiteralPath $f.FullName -Encoding UTF8
        $fileCount++
        foreach ($ln in $lines) {
            $total++
            $trim = $ln.Trim()
            if ($trim -eq '') { $emptyLines++; continue }
            if ($trim.StartsWith('//') -or $trim.StartsWith('/*') -or $trim.StartsWith('*')) { $commentLines++ }
        }
    }
    $codeOnly = $total - $emptyLines - $commentLines
    Write-Host ("[{0}] files={1} total={2} code={3} blank={4} comment={5}" -f $d.Label, $fileCount, $total, $codeOnly, $emptyLines, $commentLines)
    $grandTotal += $total
    $grandFiles += $fileCount
    $grandEmpty += $emptyLines
    $grandComment += $commentLines
}
$grandCode = $grandTotal - $grandEmpty - $grandComment
Write-Host ''
Write-Host ('GRAND TOTAL files={0} lines={1} code={2} blank={3} comment={4}' -f $grandFiles, $grandTotal, $grandCode, $grandEmpty, $grandComment)
