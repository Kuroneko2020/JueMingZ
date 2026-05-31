param(
    [Parameter(Mandatory = $true)]
    [string]$Source,

    [Parameter(Mandatory = $true)]
    [string]$Destination
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $Source)) {
    throw "Source dependency does not exist: $Source"
}

$destinationDirectory = Split-Path -Parent $Destination
if (-not [string]::IsNullOrWhiteSpace($destinationDirectory)) {
    New-Item -ItemType Directory -Force -Path $destinationDirectory | Out-Null
}

$sourceBytes = [System.IO.File]::ReadAllBytes($Source)

$compressedMemory = New-Object System.IO.MemoryStream
try {
    $deflate = New-Object System.IO.Compression.DeflateStream -ArgumentList @(
        $compressedMemory,
        [System.IO.Compression.CompressionLevel]::Optimal,
        $true)
    try {
        $deflate.Write($sourceBytes, 0, $sourceBytes.Length)
    }
    finally {
        $deflate.Dispose()
    }

    $compressedBytes = $compressedMemory.ToArray()
}
finally {
    $compressedMemory.Dispose()
}

$verifyMemory = New-Object System.IO.MemoryStream -ArgumentList (,$compressedBytes)
$expandedMemory = New-Object System.IO.MemoryStream
try {
    $inflate = New-Object System.IO.Compression.DeflateStream -ArgumentList @(
        $verifyMemory,
        [System.IO.Compression.CompressionMode]::Decompress)
    try {
        $inflate.CopyTo($expandedMemory)
    }
    finally {
        $inflate.Dispose()
    }

    $expandedBytes = $expandedMemory.ToArray()
}
finally {
    $expandedMemory.Dispose()
    $verifyMemory.Dispose()
}

$sha256 = [System.Security.Cryptography.SHA256]::Create()
try {
    $sourceHash = [System.BitConverter]::ToString($sha256.ComputeHash($sourceBytes))
    $expandedHash = [System.BitConverter]::ToString($sha256.ComputeHash($expandedBytes))
}
finally {
    $sha256.Dispose()
}

if ($sourceBytes.Length -ne $expandedBytes.Length -or $sourceHash -ne $expandedHash) {
    throw "Compressed dependency verification failed for $Source"
}

$shouldWrite = $true
if (Test-Path -LiteralPath $Destination) {
    $existingBytes = [System.IO.File]::ReadAllBytes($Destination)
    if ($existingBytes.Length -eq $compressedBytes.Length) {
        $shouldWrite = $false
        for ($index = 0; $index -lt $existingBytes.Length; $index++) {
            if ($existingBytes[$index] -ne $compressedBytes[$index]) {
                $shouldWrite = $true
                break
            }
        }
    }
}

if ($shouldWrite) {
    [System.IO.File]::WriteAllBytes($Destination, $compressedBytes)
}

Write-Host "Compressed dependency: $Source -> $Destination ($($sourceBytes.Length) -> $($compressedBytes.Length) bytes)"
