#requires -Version 5.1
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [switch]$Force,
    [switch]$StageWhileRevitOpen
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$runningRevit = @(Get-Process -Name Revit -ErrorAction SilentlyContinue)
if ($runningRevit.Count -gt 0 -and -not $StageWhileRevitOpen) {
    throw 'Guarda el modelo y cierra Revit antes de instalar. Usa -StageWhileRevitOpen solo para preparar la próxima sesión sin cambiar la actual.'
}
$requiresRestart = $runningRevit.Count -gt 0

$distributionRoot = Join-Path $PSScriptRoot 'dist'
$assemblySource = Join-Path $distributionRoot 'BimAiRevitAssistant.dll'
$checksumSource = Join-Path $distributionRoot 'BimAiRevitAssistant.sha256'
foreach ($source in @($assemblySource, $checksumSource)) {
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "Falta un archivo compilado del add-in: $source" }
}
$expectedHash = (Get-Content -LiteralPath $checksumSource -Raw).Trim().ToLowerInvariant()
$actualHash = (Get-FileHash -LiteralPath $assemblySource -Algorithm SHA256).Hash.ToLowerInvariant()
if ($expectedHash -notmatch '^[0-9a-f]{64}$' -or $actualHash -ne $expectedHash) {
    throw 'El SHA-256 del add-in no coincide con la distribución.'
}

$settingsPath = Join-Path $env:APPDATA 'BIM-AI-POC\bridge.json'
if (-not (Test-Path -LiteralPath $settingsPath -PathType Leaf)) {
    throw 'Falta bridge.json. Ejecuta Instalar-ARQ-IA.cmd desde la carpeta clonada.'
}
try {
    $settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
    $routinesRoot = [IO.Path]::GetFullPath([string]$settings.routinesRoot)
    $projectRoot = [IO.Directory]::GetParent($routinesRoot).FullName
    if (-not (Test-Path -LiteralPath $routinesRoot -PathType Container) -or
        -not (Test-Path -LiteralPath (Join-Path $projectRoot 'AGENTS.md') -PathType Leaf) -or
        -not (Test-Path -LiteralPath (Join-Path $projectRoot '.codex\config.toml') -PathType Leaf) -or
        -not (Test-Path -LiteralPath (Join-Path $projectRoot 'dynamo\extension\README.md') -PathType Leaf) -or
        -not (Test-Path -LiteralPath (Join-Path $projectRoot 'mcp') -PathType Container)) {
        throw 'No se encontró el proyecto ARQ-IA configurado en bridge.json.'
    }
} catch {
    throw 'bridge.json no tiene una ruta válida al proyecto ARQ-IA.'
}

$addinsRoot = Join-Path $env:APPDATA 'Autodesk\Revit\Addins\2027'
$installRoot = Join-Path $addinsRoot 'BimAiRevitAssistant'
$assemblyName = 'BimAiRevitAssistant.' + $expectedHash.Substring(0, 16) + '.dll'
$assemblyTarget = Join-Path $installRoot $assemblyName
$manifestTarget = Join-Path $addinsRoot 'BimAiRevitAssistant.addin'

if (-not $Force -and ((Test-Path -LiteralPath $installRoot) -or (Test-Path -LiteralPath $manifestTarget))) {
    throw 'Ya existe una instalación de Asistente BIM para Revit. Revisa el destino y usa -Force para actualizarla.'
}
if (Test-Path -LiteralPath $manifestTarget -PathType Leaf) {
    try { [xml]$existingManifest = Get-Content -LiteralPath $manifestTarget -Raw } catch { throw 'El manifiesto .addin existente está inválido; se conserva.' }
    $existingType = [string]$existingManifest.RevitAddIns.AddIn.FullClassName
    if ($existingType -ne 'BimAiRevitAssistant.RevitAssistantApplication') {
        throw 'El manifiesto .addin existente pertenece a otro add-in; se conserva.'
    }
}

$targets = @($addinsRoot, $installRoot, $assemblyTarget, $manifestTarget)
foreach ($target in $targets) {
    if (-not $PSCmdlet.ShouldProcess($target, 'Instalar Asistente BIM para Revit 2027')) { return }
}

New-Item -ItemType Directory -Force -Path $installRoot | Out-Null
if (Test-Path -LiteralPath $assemblyTarget -PathType Leaf) {
    if ((Get-FileHash -LiteralPath $assemblyTarget -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expectedHash) {
        throw 'El DLL versionado por hash ya existe con contenido distinto; se conserva.'
    }
} else {
    $assemblyTemporary = $assemblyTarget + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
    try {
        Copy-Item -LiteralPath $assemblySource -Destination $assemblyTemporary
        if ((Get-FileHash -LiteralPath $assemblyTemporary -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expectedHash) {
            throw 'El DLL copiado no coincide con el SHA-256 esperado.'
        }
        Move-Item -LiteralPath $assemblyTemporary -Destination $assemblyTarget
    } finally {
        Remove-Item -LiteralPath $assemblyTemporary -Force -ErrorAction SilentlyContinue
    }
}

$escapedAssembly = [Security.SecurityElement]::Escape($assemblyTarget)
$manifest = @"
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>ARQ-IA para Revit</Name>
    <Assembly>$escapedAssembly</Assembly>
    <AddInId>4CA1683B-89AB-4F68-9D29-1D2D506C0F51</AddInId>
    <FullClassName>BimAiRevitAssistant.RevitAssistantApplication</FullClassName>
    <VendorId>BMAI</VendorId>
    <VendorDescription>Branko Plaza</VendorDescription>
  </AddIn>
</RevitAddIns>
"@
$encoding = New-Object System.Text.UTF8Encoding($false)
$manifestTemporary = $manifestTarget + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
try {
    [IO.File]::WriteAllText($manifestTemporary, $manifest, $encoding)
    Move-Item -LiteralPath $manifestTemporary -Destination $manifestTarget -Force
} finally {
    Remove-Item -LiteralPath $manifestTemporary -Force -ErrorAction SilentlyContinue
}

[xml]$verified = Get-Content -LiteralPath $manifestTarget -Raw
if ($verified.RevitAddIns.AddIn.FullClassName -ne 'BimAiRevitAssistant.RevitAssistantApplication' -or
    $verified.RevitAddIns.AddIn.AddInId -ne '4CA1683B-89AB-4F68-9D29-1D2D506C0F51' -or
    $verified.RevitAddIns.AddIn.VendorId -ne 'BMAI' -or
    $verified.RevitAddIns.AddIn.VendorDescription -ne 'Branko Plaza' -or
    [IO.Path]::GetFullPath($verified.RevitAddIns.AddIn.Assembly) -ne [IO.Path]::GetFullPath($assemblyTarget) -or
    (Get-FileHash -LiteralPath $assemblyTarget -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expectedHash) {
    throw 'La verificación de la instalación del add-in no pasó.'
}

if ($requiresRestart) {
    Write-Output 'REVIT_ASSISTANT_INSTALL=STAGED_RESTART_REQUIRED'
} else {
    Write-Output 'REVIT_ASSISTANT_INSTALL=PASS'
}
Write-Output 'VENDOR_DESCRIPTION=Branko Plaza'
Write-Output ("ASSEMBLY_PATH=" + $assemblyTarget)
Write-Output ("SHA256=" + $expectedHash)
