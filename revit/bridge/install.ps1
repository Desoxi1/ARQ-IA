#requires -Version 5.1
[CmdletBinding(SupportsShouldProcess = $true)]
param([switch]$Force, [switch]$StageWhileRevitOpen)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$runningRevit = Get-Process -Name Revit -ErrorAction SilentlyContinue
if ($runningRevit -and -not $StageWhileRevitOpen) { throw 'Cierra Revit después de guardar el trabajo antes de instalar el puente. O usa -StageWhileRevitOpen para preparar archivos que solo cargará una sesión nueva.' }
$requiresRestart = [bool]$runningRevit

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
$assemblyFileName = 'BimAiDynamoBridge.' + $assemblyHash.Substring(0, 16) + '.dll'
$assemblyTarget = Join-Path $installRoot $assemblyFileName
$manifestTarget = Join-Path $addinsRoot 'BimAiDynamoBridge.addin'
$settingsRoot = Join-Path $env:APPDATA 'BIM-AI-POC'
$settingsTarget = Join-Path $settingsRoot 'bridge.json'
$testModelsRoot = Join-Path $repoRoot 'revit\test-models'

if (-not $Force -and ((Test-Path -LiteralPath $installRoot) -or (Test-Path -LiteralPath $manifestTarget))) {
    throw 'Ya existe un puente instalado. Revisa sus archivos; usa -Force solo para actualizar esta instalación.'
}
if (-not $Force -and (Test-Path -LiteralPath $settingsTarget)) {
    throw 'Ya existe bridge.json; se conserva para evitar cambiar una ruta local sin revisión.'
}

$targets = @($installRoot, $assemblyTarget, $manifestTarget, $settingsRoot, $settingsTarget, $testModelsRoot)
foreach ($target in $targets) {
    if (-not $PSCmdlet.ShouldProcess($target, 'Instalar puente local BIM-AI para Revit 2027')) { return }
}

New-Item -ItemType Directory -Force -Path $installRoot, $settingsRoot, $testModelsRoot | Out-Null
if (Test-Path -LiteralPath $assemblyTarget -PathType Leaf) {
    $installedHash = (Get-FileHash -LiteralPath $assemblyTarget -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($installedHash -ne $assemblyHash) {
        throw "El DLL de destino $assemblyFileName no coincide con su hash de nombre. Se conserva para revisión manual."
    }
} else {
    $assemblyTemp = $assemblyTarget + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
    try {
        Copy-Item -LiteralPath $assemblySource -Destination $assemblyTemp
        $stagedHash = (Get-FileHash -LiteralPath $assemblyTemp -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($stagedHash -ne $assemblyHash) { throw 'El DLL copiado no coincide con el hash del paquete.' }
        Move-Item -LiteralPath $assemblyTemp -Destination $assemblyTarget
    } finally {
        Remove-Item -LiteralPath $assemblyTemp -Force -ErrorAction SilentlyContinue
    }
}
$escapedAssembly = [Security.SecurityElement]::Escape($assemblyTarget)
$manifest = @"
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>ARQ-IA Dynamo Bridge</Name>
    <Assembly>$escapedAssembly</Assembly>
    <AddInId>85279988-3BB1-4200-8DA5-E2FD521EA7A8</AddInId>
    <FullClassName>BimAiDynamoBridge.BridgeApplication</FullClassName>
    <VendorId>BMAI</VendorId>
    <VendorDescription>Branko Plaza</VendorDescription>
  </AddIn>
</RevitAddIns>
"@
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$manifestTemp = $manifestTarget + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
try {
    [IO.File]::WriteAllText($manifestTemp, $manifest, $utf8NoBom)
    Move-Item -LiteralPath $manifestTemp -Destination $manifestTarget -Force
} finally {
    Remove-Item -LiteralPath $manifestTemp -Force -ErrorAction SilentlyContinue
}
$settings = [ordered]@{ routinesRoot = (Join-Path $repoRoot 'routines'); testModelsRoot = $testModelsRoot }
[IO.File]::WriteAllText($settingsTarget, (ConvertTo-Json -InputObject $settings -Depth 4), $utf8NoBom)

if ($requiresRestart) {
    Write-Output 'BRIDGE_INSTALL=STAGED_RESTART_REQUIRED'
    Write-Output 'El manifiesto apunta a un DLL con nombre basado en su hash para que la siguiente sesión cargue esta versión. Las sesiones abiertas conservan su ensamblado cargado.'
} else {
    Write-Output 'BRIDGE_INSTALL=PASS'
    Write-Output 'Reinicia Revit y acepta la carga del add-in BIM-AI Dynamo Bridge para probarlo.'
}
Write-Output ("BRIDGE_ASSEMBLY_PATH=" + $assemblyTarget)
