#requires -Version 5.1
[CmdletBinding()]
param(
    [string]$RevitRoot = (Join-Path $env:ProgramFiles 'Autodesk\Revit 2027'),
    [string]$SourceModelPath,
    [string]$DestinationName = 'BIM-AI-Dynamo-Test.rvt'
)

$ErrorActionPreference = 'Stop'
$testModelsRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$sampleLinks = @(
    'Snowdon Towers Sample Architectural.rvt',
    'Snowdon Towers Sample Structural.rvt',
    'Snowdon Towers Sample Plumbing.rvt',
    'Snowdon Towers Sample Electrical.rvt',
    'Snowdon Towers Sample Site.rvt'
)

function Copy-VerifiedSampleLinks([string]$SampleRoot, [string]$DestinationRoot, [string[]]$Names) {
    $copied = 0
    $ready = 0
    $missing = New-Object 'System.Collections.Generic.List[string]'

    foreach ($name in $Names) {
        $source = Join-Path $SampleRoot $name
        $destination = Join-Path $DestinationRoot $name
        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
            $missing.Add($name)
            continue
        }
        $sourceItem = Get-Item -LiteralPath $source
        if ($sourceItem.Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw "Sample link source cannot be a reparse point: $name"
        }
        $sourceHash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant()

        if (Test-Path -LiteralPath $destination) {
            $destinationItem = Get-Item -LiteralPath $destination
            if ($destinationItem.PSIsContainer -or ($destinationItem.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
                throw "Sample link destination is not a regular file: $name"
            }
            if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant() -ne $sourceHash) {
                throw "Sample link destination exists with a different SHA-256 and was not overwritten: $name"
            }
            $ready++
            continue
        }

        $temporaryPath = $destination + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
        try {
            Copy-Item -LiteralPath $source -Destination $temporaryPath | Out-Null
            $copyHash = (Get-FileHash -LiteralPath $temporaryPath -Algorithm SHA256).Hash.ToLowerInvariant()
            if ($copyHash -ne $sourceHash) { throw "Sample link copy failed its SHA-256 check: $name" }
            [IO.File]::Move($temporaryPath, $destination)
            $copied++
            $ready++
        } finally {
            Remove-Item -LiteralPath $temporaryPath -Force -ErrorAction SilentlyContinue
        }
    }

    return [pscustomobject]@{ Copied = $copied; Ready = $ready; Missing = @($missing) }
}

if ([IO.Path]::GetFileName($DestinationName) -ne $DestinationName -or
    [IO.Path]::GetExtension($DestinationName) -ne '.rvt' -or
    $DestinationName -match '[<>:"/\\|?*]') {
    throw 'DestinationName must be a single safe .rvt filename.'
}
if ((Get-Item -LiteralPath $testModelsRoot).Attributes -band [IO.FileAttributes]::ReparsePoint) {
    throw 'The test-model directory cannot be a reparse point.'
}

$sampleRoot = Join-Path $RevitRoot 'Samples'
$defaultSnowdonSamplePath = [IO.Path]::GetFullPath((Join-Path $sampleRoot 'Snowdon Towers Sample HVAC.rvt'))
if ([string]::IsNullOrWhiteSpace($SourceModelPath)) { $SourceModelPath = $defaultSnowdonSamplePath }
$SourceModelPath = [IO.Path]::GetFullPath($SourceModelPath)
$copySnowdonSampleLinks = [string]::Equals($SourceModelPath, $defaultSnowdonSamplePath, [StringComparison]::OrdinalIgnoreCase)
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
        $modelCopyStatus = 'ALREADY_PRESENT_HASH_MATCH'
    } else {
        throw 'The destination already exists and is not the same sample. It was not overwritten.'
    }
} else {
    $temporaryPath = $destinationPath + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
    try {
        Copy-Item -LiteralPath $SourceModelPath -Destination $temporaryPath | Out-Null
        $copyHash = (Get-FileHash -LiteralPath $temporaryPath -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($copyHash -ne $sourceHash) { throw 'The copied RVT failed its SHA-256 check.' }
        [IO.File]::Move($temporaryPath, $destinationPath)
        $modelCopyStatus = 'COPIED'
    } finally {
        Remove-Item -LiteralPath $temporaryPath -Force -ErrorAction SilentlyContinue
    }
}

$destinationItem = Get-Item -LiteralPath $destinationPath
if ($destinationItem.IsReadOnly) {
    $destinationItem.IsReadOnly = $false
    $destinationItem.Refresh()
}
if ($destinationItem.IsReadOnly) { throw 'The disposable test model could not be made writable.' }

Write-Output 'TEST_MODEL=READY'
Write-Output ("TEST_MODEL_COPY=" + $modelCopyStatus)
Write-Output 'TEST_MODEL_READ_ONLY=FALSE'
if ($copySnowdonSampleLinks) {
    $linkResult = Copy-VerifiedSampleLinks -SampleRoot $sampleRoot -DestinationRoot $testModelsRoot -Names $sampleLinks
    Write-Output ("SAMPLE_LINKS_COPIED=" + $linkResult.Copied)
    Write-Output ("SAMPLE_LINKS_READY=" + $linkResult.Ready)
    if ($linkResult.Missing.Count -gt 0) {
        Write-Output ("SAMPLE_LINKS_MISSING=" + ($linkResult.Missing -join ';'))
    }
} else {
    Write-Output 'SAMPLE_LINKS=SKIPPED_CUSTOM_SOURCE'
}
Write-Output ("TEST_MODEL_PATH=" + $destinationPath)
Write-Output ("TEST_MODEL_SHA256=" + $sourceHash)
