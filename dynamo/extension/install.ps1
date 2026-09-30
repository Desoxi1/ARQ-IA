param(
    [string]$DynamoRoot = 'C:\Program Files\Autodesk\Revit 2027\AddIns\DynamoForRevit',
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$DynamoRoot = [IO.Path]::GetFullPath($DynamoRoot)
$distributionDirectory = Join-Path $PSScriptRoot 'dist'
$sourceDll = Join-Path $distributionDirectory 'BimAiDynamoAssistant.dll'
$sourceHash = Join-Path $distributionDirectory 'BimAiDynamoAssistant.dll.sha256'
$sourceDefinition = Join-Path $distributionDirectory 'viewExtensions\BimAiDynamoAssistant_ViewExtensionDefinition.xml'
$targetDll = Join-Path $DynamoRoot 'BimAiDynamoAssistant.dll'
$targetExtensions = Join-Path $DynamoRoot 'viewExtensions'
$targetDefinition = Join-Path $targetExtensions 'BimAiDynamoAssistant_ViewExtensionDefinition.xml'

foreach ($path in @($sourceDll, $sourceHash, $sourceDefinition)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing extension package file: $path" }
}
foreach ($assembly in @('DynamoCore.dll', 'DynamoCoreWpf.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $DynamoRoot $assembly) -PathType Leaf)) {
        throw "Dynamo 4.x installation not found at $DynamoRoot"
    }
}

$runningRevit = @(Get-Process -Name Revit -ErrorAction SilentlyContinue)
if ($runningRevit.Count -gt 0) {
    throw 'Close every Revit window before installing the Dynamo extension, then run this script again.'
}

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Open PowerShell as Administrator to install under Program Files.'
}

$expectedHash = (Get-Content -LiteralPath $sourceHash -Raw).Trim().ToLowerInvariant()
if ($expectedHash -notmatch '^[0-9a-f]{64}$') { throw 'Invalid extension SHA-256 manifest.' }
$actualHash = (Get-FileHash -LiteralPath $sourceDll -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualHash -ne $expectedHash) { throw 'Extension DLL does not match its SHA-256 manifest.' }

if ((Test-Path -LiteralPath $targetDll) -or (Test-Path -LiteralPath $targetDefinition)) {
    $sameDll = (Test-Path -LiteralPath $targetDll) -and
        ((Get-FileHash -LiteralPath $targetDll -Algorithm SHA256).Hash.ToLowerInvariant() -eq $expectedHash)
    $sameDefinition = (Test-Path -LiteralPath $targetDefinition) -and
        ((Get-FileHash -LiteralPath $targetDefinition -Algorithm SHA256).Hash.ToLowerInvariant() -eq
         (Get-FileHash -LiteralPath $sourceDefinition -Algorithm SHA256).Hash.ToLowerInvariant())
    if ($sameDll -and $sameDefinition) {
        Write-Output 'DYNAMO_EXTENSION_INSTALL=ALREADY_INSTALLED'
        Write-Output ("SHA256=" + $expectedHash)
        exit 0
    }
    if (-not $Force) { throw 'A different extension file is already installed. Review it, then rerun with -Force to replace it.' }
}

$null = New-Item -ItemType Directory -Force -Path $targetExtensions
Copy-Item -LiteralPath $sourceDll -Destination $targetDll -Force
Copy-Item -LiteralPath $sourceDefinition -Destination $targetDefinition -Force
$installedHash = (Get-FileHash -LiteralPath $targetDll -Algorithm SHA256).Hash.ToLowerInvariant()
if ($installedHash -ne $expectedHash) { throw 'Installed extension hash verification failed.' }

Write-Output 'DYNAMO_EXTENSION_INSTALL=PASS'
Write-Output "DYNAMO_ROOT=$DynamoRoot"
Write-Output "SHA256=$installedHash"
Write-Output 'NEXT=Start Revit 2027, open Dynamo, then select the Asistente BIM tab.'
