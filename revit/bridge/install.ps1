#requires -Version 5.1
[CmdletBinding(SupportsShouldProcess = $true)]
param([switch]$Force)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$runningRevit = Get-Process -Name Revit -ErrorAction SilentlyContinue
if ($runningRevit) { throw 'Cierra Revit después de guardar el trabajo antes de instalar el puente. No se modificó la instalación.' }

$assemblySource = Join-Path $PSScriptRoot 'dist\BimAiDynamoBridge.dll'
if (Test-Path -LiteralPath $assemblySource -PathType Leaf) {
    $checksumPath = Join-Path $PSScriptRoot 'dist\BimAiDynamoBridge.sha256'
    if (-not (Test-Path -LiteralPath $checksumPath -PathType Leaf)) { throw 'Falta el SHA-256 del puente empaquetado.' }
    $expectedHash = (Get-Content -LiteralPath $checksumPath -Raw).Trim().ToLowerInvariant()
    $actualHash = (Get-FileHash -LiteralPath $assemblySource -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($expectedHash -notmatch '^[0-9a-f]{64}$' -or $actualHash -ne $expectedHash) { throw 'El SHA-256 del puente empaquetado no coincide; no se instalará.' }
} else {
    $buildScript = Join-Path $PSScriptRoot 'build.ps1'
    & (Join-Path $env:windir 'System32\WindowsPowerShell\v1.0\powershell.exe') -NoProfile -ExecutionPolicy Bypass -File $buildScript
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo compilar el puente.' }
    $assemblySource = Join-Path $repoRoot 'artifacts\bridge-build\BimAiDynamoBridge.dll'
}
$assemblyHash = (Get-FileHash -LiteralPath $assemblySource -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Output "BRIDGE_ASSEMBLY_SHA256=$assemblyHash"
$addinsRoot = Join-Path $env:APPDATA 'Autodesk\Revit\Addins\2027'
$installRoot = Join-Path $addinsRoot 'BimAiDynamoBridge'
$assemblyTarget = Join-Path $installRoot 'BimAiDynamoBridge.dll'
$manifestTarget = Join-Path $addinsRoot 'BimAiDynamoBridge.addin'
$settingsRoot = Join-Path $env:APPDATA 'BIM-AI-POC'
$settingsTarget = Join-Path $settingsRoot 'bridge.json'

if (-not $Force -and ((Test-Path -LiteralPath $installRoot) -or (Test-Path -LiteralPath $manifestTarget))) {
    throw 'Ya existe un puente instalado. Revisa sus archivos; usa -Force solo para actualizar esta instalación.'
}
if (-not $Force -and (Test-Path -LiteralPath $settingsTarget)) {
    throw 'Ya existe bridge.json; se conserva para evitar cambiar una ruta local sin revisión.'
}

$targets = @($installRoot, $manifestTarget, $settingsRoot, $settingsTarget)
foreach ($target in $targets) {
    if (-not $PSCmdlet.ShouldProcess($target, 'Instalar puente local BIM-AI para Revit 2027')) { return }
}

New-Item -ItemType Directory -Force -Path $installRoot, $settingsRoot | Out-Null
Copy-Item -LiteralPath $assemblySource -Destination $assemblyTarget -Force:$Force
$escapedAssembly = [Security.SecurityElement]::Escape($assemblyTarget)
$manifest = @"
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>BIM-AI Dynamo Bridge</Name>
    <Assembly>$escapedAssembly</Assembly>
    <AddInId>85279988-3BB1-4200-8DA5-E2FD521EA7A8</AddInId>
    <FullClassName>BimAiDynamoBridge.BridgeApplication</FullClassName>
    <VendorId>BMAI</VendorId>
    <VendorDescription>BIM-AI-POC local read-only Dynamo runner</VendorDescription>
  </AddIn>
</RevitAddIns>
"@
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[IO.File]::WriteAllText($manifestTarget, $manifest, $utf8NoBom)
$settings = [ordered]@{ routinesRoot = (Join-Path $repoRoot 'routines') }
[IO.File]::WriteAllText($settingsTarget, (ConvertTo-Json -InputObject $settings -Depth 4), $utf8NoBom)

Write-Output 'BRIDGE_INSTALL=PASS'
Write-Output 'Reinicia Revit y acepta la carga del add-in BIM-AI Dynamo Bridge para probarlo.'
