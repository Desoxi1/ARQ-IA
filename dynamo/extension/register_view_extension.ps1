#requires -Version 5.1
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)][string]$AssemblyPath,
    [Parameter(Mandatory = $true)][ValidatePattern('^[0-9a-fA-F]{64}$')][string]$Sha256,
    [string]$ManifestAssemblyPath = '..\BimAiDynamoAssistant.dll',
    [string]$DynamoRevitRoot = (Join-Path $env:ProgramFiles 'Autodesk\Revit 2027\AddIns\DynamoForRevit'),
    [switch]$Force,
    [switch]$StageWhileRevitOpen
)

$ErrorActionPreference = 'Stop'
$AssemblyPath = [IO.Path]::GetFullPath($AssemblyPath)
$DynamoRevitRoot = [IO.Path]::GetFullPath($DynamoRevitRoot)
$expectedRoot = [IO.Path]::GetFullPath((Join-Path $env:ProgramFiles 'Autodesk\Revit 2027\AddIns\DynamoForRevit'))
if (-not [String]::Equals($DynamoRevitRoot, $expectedRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'DynamoRevitRoot debe ser la carpeta oficial de Dynamo for Revit 2027.'
}
if ((Get-Process -Name Revit -ErrorAction SilentlyContinue) -and -not $StageWhileRevitOpen) {
    throw 'Cierra Revit antes de registrar la View Extension.'
}
if ($StageWhileRevitOpen -and [IO.Path]::GetFileName($AssemblyPath) -notmatch '^BimAiDynamoAssistant\.[0-9a-f]{16}\.dll$') {
    throw 'Para preparar la extensión con Revit abierto, el DLL debe estar versionado por SHA-256.'
}
if (-not (Test-Path -LiteralPath $AssemblyPath -PathType Leaf)) { throw 'No se encontró el DLL de Asistente BIM.' }
$actualHash = (Get-FileHash -LiteralPath $AssemblyPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualHash -ne $Sha256.ToLowerInvariant()) { throw 'El DLL no coincide con el SHA-256 esperado.' }
if ((Get-Item -LiteralPath $AssemblyPath).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'El DLL no puede ser un enlace o punto de reanálisis.' }

$viewExtensions = Join-Path $DynamoRevitRoot 'viewExtensions'
$manifestPath = Join-Path $viewExtensions 'BimAiDynamoAssistant_ViewExtensionDefinition.xml'
$resolvedManifestAssemblyPath = [IO.Path]::GetFullPath((Join-Path $viewExtensions $ManifestAssemblyPath))
if (-not [String]::Equals($resolvedManifestAssemblyPath, $AssemblyPath, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'La ruta relativa del manifiesto debe resolver exactamente al DLL verificado.'
}
if ((Test-Path -LiteralPath $viewExtensions) -and ((Get-Item -LiteralPath $viewExtensions).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
    throw 'La carpeta viewExtensions no puede ser un punto de reanálisis.'
}
if (Test-Path -LiteralPath $manifestPath -PathType Leaf) {
    try { [xml]$existing = Get-Content -LiteralPath $manifestPath -Raw } catch { throw 'Ya existe un manifiesto de extensión inválido; se conserva.' }
    if ($existing.ViewExtensionDefinition.TypeName -ne 'BimAiDynamoAssistant.BimAiDynamoViewExtension') {
        throw 'El archivo de destino pertenece a otro tipo de extensión; se conserva.'
    }
    if ($existing.ViewExtensionDefinition.AssemblyPath -ne $ManifestAssemblyPath -and -not $Force) {
        throw 'Ya hay otro DLL registrado para Asistente BIM; inspecciónalo antes de actualizar.'
    }
}

if (-not $PSCmdlet.ShouldProcess($manifestPath, 'Registrar la View Extension Asistente BIM de Dynamo 4.0')) { return }
$null = New-Item -ItemType Directory -Force -Path $viewExtensions
$escapedAssembly = [Security.SecurityElement]::Escape($ManifestAssemblyPath)
$manifest = @"
<?xml version="1.0" encoding="utf-8"?>
<ViewExtensionDefinition>
  <AssemblyPath>$escapedAssembly</AssemblyPath>
  <TypeName>BimAiDynamoAssistant.BimAiDynamoViewExtension</TypeName>
</ViewExtensionDefinition>
"@
$temporaryPath = $manifestPath + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
try {
    [IO.File]::WriteAllText($temporaryPath, $manifest, (New-Object System.Text.UTF8Encoding($false)))
    Move-Item -LiteralPath $temporaryPath -Destination $manifestPath -Force
} finally {
    Remove-Item -LiteralPath $temporaryPath -Force -ErrorAction SilentlyContinue
}
[xml]$verified = Get-Content -LiteralPath $manifestPath -Raw
if ($verified.ViewExtensionDefinition.AssemblyPath -ne $ManifestAssemblyPath -or
    $verified.ViewExtensionDefinition.TypeName -ne 'BimAiDynamoAssistant.BimAiDynamoViewExtension') {
    throw 'La verificación del manifiesto de View Extension no pasó.'
}
if ($StageWhileRevitOpen) {
    Write-Output 'DYNAMO_VIEW_EXTENSION_REGISTRATION=STAGED_RESTART_REQUIRED'
} else {
    Write-Output 'DYNAMO_VIEW_EXTENSION_REGISTRATION=PASS'
}
Write-Output ("MANIFEST_PATH=" + $manifestPath)
Write-Output ("ASSEMBLY_SHA256=" + $actualHash)
