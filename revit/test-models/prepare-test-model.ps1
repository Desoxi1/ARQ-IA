#requires -Version 5.1
[CmdletBinding()]
param(
    [string]$RevitRoot = (Join-Path $env:ProgramFiles 'Autodesk\Revit 2027'),
    [string]$SourceModelPath,
    [string]$DestinationName = 'BIM-AI-Dynamo-Test.rvt'
)

$ErrorActionPreference = 'Stop'
$testModelsRoot = [IO.Path]::GetFullPath($PSScriptRoot)

if ([IO.Path]::GetFileName($DestinationName) -ne $DestinationName -or
    [IO.Path]::GetExtension($DestinationName) -ne '.rvt' -or
    $DestinationName -match '[<>:"/\\|?*]') {
    throw 'DestinationName must be a single safe .rvt filename.'
}
if ((Get-Item -LiteralPath $testModelsRoot).Attributes -band [IO.FileAttributes]::ReparsePoint) {
    throw 'The test-model directory cannot be a reparse point.'
}

if ([string]::IsNullOrWhiteSpace($SourceModelPath)) {
    $SourceModelPath = Join-Path $RevitRoot 'Samples\Snowdon Towers Sample HVAC.rvt'
}
$SourceModelPath = [IO.Path]::GetFullPath($SourceModelPath)
if (-not (Test-Path -LiteralPath $SourceModelPath -PathType Leaf)) {
    throw "No sample RVT found at '$SourceModelPath'. Pass -SourceModelPath with a local disposable RVT."
}
$sourceItem = Get-Item -LiteralPath $SourceModelPath
if (($sourceItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -or
    [IO.Path]::GetExtension($sourceItem.Name) -ne '.rvt') {
    throw 'The source must be a regular RVT file.'
}

$destinationPath = [IO.Path]::GetFullPath((Join-Path $testModelsRoot $DestinationName))
if (-not $destinationPath.StartsWith($testModelsRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Destination path must stay under revit/test-models.'
}
$sourceHash = (Get-FileHash -LiteralPath $SourceModelPath -Algorithm SHA256).Hash.ToLowerInvariant()

if (Test-Path -LiteralPath $destinationPath) {
    $destinationItem = Get-Item -LiteralPath $destinationPath
    if ($destinationItem.Attributes -band [IO.FileAttributes]::ReparsePoint) {
        throw 'The destination cannot be a reparse point.'
    }
    if (-not $destinationItem.PSIsContainer -and
        (Get-FileHash -LiteralPath $destinationPath -Algorithm SHA256).Hash.ToLowerInvariant() -eq $sourceHash) {
        Write-Output 'TEST_MODEL=READY'
        Write-Output 'TEST_MODEL_COPY=ALREADY_PRESENT_HASH_MATCH'
        Write-Output ("TEST_MODEL_SHA256=" + $sourceHash)
        return
    }
    throw 'The destination already exists and is not the same sample. It was not overwritten.'
}

$temporaryPath = $destinationPath + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
try {
    Copy-Item -LiteralPath $SourceModelPath -Destination $temporaryPath
    $copyHash = (Get-FileHash -LiteralPath $temporaryPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($copyHash -ne $sourceHash) { throw 'The copied RVT failed its SHA-256 check.' }
    [IO.File]::Move($temporaryPath, $destinationPath)
} finally {
    Remove-Item -LiteralPath $temporaryPath -Force -ErrorAction SilentlyContinue
}

Write-Output 'TEST_MODEL=READY'
Write-Output ("TEST_MODEL_PATH=" + $destinationPath)
Write-Output ("TEST_MODEL_SHA256=" + $sourceHash)
