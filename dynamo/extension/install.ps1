#requires -Version 5.1
[CmdletBinding()]
param(
    [switch]$Force,
    [switch]$StageWhileRevitOpen
)

$ErrorActionPreference = 'Stop'
$dynamoRoot = Join-Path $env:ProgramFiles 'Autodesk\Revit 2027\AddIns\DynamoForRevit'
$packagesRoot = Join-Path $env:APPDATA 'Dynamo\Dynamo Revit\27.0\packages'
$packageRoot = Join-Path $packagesRoot 'ARQ-IA'
$packageBin = Join-Path $packageRoot 'bin'
$packageExtra = Join-Path $packageRoot 'extra'
$sourceDll = Join-Path $PSScriptRoot 'dist\BimAiDynamoAssistant.dll'
$sourceHash = Join-Path $PSScriptRoot 'dist\BimAiDynamoAssistant.dll.sha256'
$sourceManifest = Join-Path $PSScriptRoot 'dist\viewExtensions\BimAiDynamoAssistant_ViewExtensionDefinition.xml'
$sourcePackage = Join-Path $PSScriptRoot 'dist\pkg.json'
$targetManifest = Join-Path $packageExtra 'BimAiDynamoAssistant_ViewExtensionDefinition.xml'
$targetPackage = Join-Path $packageRoot 'pkg.json'

foreach ($path in @($sourceDll, $sourceHash, $sourceManifest, $sourcePackage)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Falta un archivo de ARQ-IA: $path" }
}
foreach ($assembly in @('DynamoCore.dll', 'DynamoCoreWpf.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $dynamoRoot $assembly) -PathType Leaf)) {
        throw 'No se encontró Dynamo for Revit 2027.'
    }
}
$expectedHash = (Get-Content -LiteralPath $sourceHash -Raw).Trim().ToLowerInvariant()
if ($expectedHash -notmatch '^[0-9a-f]{64}$' -or
    (Get-FileHash -LiteralPath $sourceDll -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expectedHash) {
    throw 'La DLL de ARQ-IA no coincide con su SHA-256.'
}
$targetDllName = 'BimAiDynamoAssistant.' + $expectedHash.Substring(0, 16) + '.dll'
$targetDll = Join-Path $packageBin $targetDllName
$packageInfo = Get-Content -LiteralPath $sourcePackage -Raw | ConvertFrom-Json
if ($packageInfo.name -ne 'ARQ-IA' -or $packageInfo.engine -ne 'dynamo' -or
    $packageInfo.engine_version -notlike '4.0.*' -or $packageInfo.node_libraries.Count -ne 0) {
    throw 'pkg.json no corresponde a ARQ-IA para Dynamo 4.0.'
}

$revitRunning = @(Get-Process -Name Revit -ErrorAction SilentlyContinue).Count -gt 0
if ($revitRunning -and -not $StageWhileRevitOpen) {
    throw 'Cierra Revit antes de actualizar la extensión o usa -StageWhileRevitOpen para la próxima sesión.'
}
if ((Test-Path -LiteralPath $packageRoot) -and -not (Test-Path -LiteralPath $targetPackage)) {
    throw 'La carpeta de paquete ARQ-IA existente no tiene pkg.json; se conserva para revisión.'
}
if (Test-Path -LiteralPath $targetPackage) {
    $existingInfo = Get-Content -LiteralPath $targetPackage -Raw | ConvertFrom-Json
    if ($existingInfo.name -ne 'ARQ-IA') { throw 'La carpeta ARQ-IA pertenece a otro paquete; se conserva.' }
    if (-not $Force -and (Test-Path -LiteralPath $targetDll)) {
        $installedHash = (Get-FileHash -LiteralPath $targetDll -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($installedHash -ne $expectedHash) { throw 'Ya existe otra versión de ARQ-IA; usa -Force para actualizar.' }
    }
}

New-Item -ItemType Directory -Path $packageBin,$packageExtra -Force | Out-Null
if (-not (Test-Path -LiteralPath $targetDll -PathType Leaf)) {
    Copy-Item -LiteralPath $sourceDll -Destination $targetDll
}
Copy-Item -LiteralPath $sourcePackage -Destination $targetPackage -Force
[xml]$manifest = Get-Content -LiteralPath $sourceManifest -Raw
if ($manifest.ViewExtensionDefinition.TypeName -ne 'BimAiDynamoAssistant.BimAiDynamoViewExtension') {
    throw 'La definición de la extensión no corresponde a ARQ-IA.'
}
$manifest.ViewExtensionDefinition.AssemblyPath = '..\bin\' + $targetDllName
$manifest.Save($targetManifest)

$installedHash = (Get-FileHash -LiteralPath $targetDll -Algorithm SHA256).Hash.ToLowerInvariant()
if ($installedHash -ne $expectedHash) { throw 'La DLL instalada no coincide con su SHA-256.' }
Write-Output ('DYNAMO_EXTENSION_INSTALL=' + $(if ($revitRunning) { 'STAGED_RESTART_REQUIRED' } else { 'PASS' }))
Write-Output 'DLL_SCOPE=CurrentUserPackage'
Write-Output ('PACKAGE_PATH=' + $packageRoot)
Write-Output ('SHA256=' + $installedHash)
Write-Output 'NEXT=Abre Revit 2027 y Dynamo en una sesión nueva para cargar ARQ-IA.'
