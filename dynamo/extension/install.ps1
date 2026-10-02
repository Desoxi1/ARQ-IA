#requires -Version 5.1
[CmdletBinding()]
param(
    [switch]$Force,
    [switch]$ElevatedWorker,
    [switch]$StageWhileRevitOpen
)

$ErrorActionPreference = 'Stop'
$dynamoRoot = Join-Path $env:ProgramFiles 'Autodesk\Revit 2027\AddIns\DynamoForRevit'
$viewExtensions = Join-Path $dynamoRoot 'viewExtensions'
$distributionDirectory = Join-Path $PSScriptRoot 'dist'
$sourceDll = Join-Path $distributionDirectory 'BimAiDynamoAssistant.dll'
$sourceHash = Join-Path $distributionDirectory 'BimAiDynamoAssistant.dll.sha256'
$sourceDefinition = Join-Path $distributionDirectory 'viewExtensions\BimAiDynamoAssistant_ViewExtensionDefinition.xml'
$sourcePackageManifest = Join-Path $distributionDirectory 'pkg.json'
$registrationScript = Join-Path $PSScriptRoot 'register_view_extension.ps1'

$runningRevit = @(Get-Process -Name Revit -ErrorAction SilentlyContinue)
if ($runningRevit.Count -gt 0 -and -not $StageWhileRevitOpen) {
    throw 'Guarda el modelo cuando corresponda y usa -StageWhileRevitOpen para preparar una DLL versionada sin tocar la sesión actual.'
}
$requiresRestart = $runningRevit.Count -gt 0

foreach ($path in @($sourceDll, $sourceHash, $sourceDefinition, $sourcePackageManifest, $registrationScript)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Falta un archivo del paquete de extensión: $path" }
}
foreach ($assembly in @('DynamoCore.dll', 'DynamoCoreWpf.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $dynamoRoot $assembly) -PathType Leaf)) {
        throw "No se encontró Dynamo for Revit 2027 en $dynamoRoot"
    }
}

$expectedHash = (Get-Content -LiteralPath $sourceHash -Raw).Trim().ToLowerInvariant()
if ($expectedHash -notmatch '^[0-9a-f]{64}$') { throw 'El manifiesto SHA-256 de la extensión no es válido.' }
$actualHash = (Get-FileHash -LiteralPath $sourceDll -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualHash -ne $expectedHash) { throw 'El DLL no coincide con el SHA-256 del paquete.' }
$targetDllName = 'BimAiDynamoAssistant.' + $expectedHash.Substring(0, 16) + '.dll'
$targetDll = Join-Path $dynamoRoot $targetDllName
$targetDefinition = Join-Path $viewExtensions 'BimAiDynamoAssistant_ViewExtensionDefinition.xml'
$manifestAssemblyPath = '..\' + $targetDllName

[xml]$definition = Get-Content -LiteralPath $sourceDefinition -Raw
if ($definition.ViewExtensionDefinition.AssemblyPath -ne '..\BimAiDynamoAssistant.dll' -or
    $definition.ViewExtensionDefinition.TypeName -ne 'BimAiDynamoAssistant.BimAiDynamoViewExtension') {
    throw 'El manifiesto de Dynamo no apunta al assembly esperado en la carpeta oficial.'
}
$packageInfo = Get-Content -LiteralPath $sourcePackageManifest -Raw | ConvertFrom-Json
if ($packageInfo.name -ne 'BIM-AI-POC' -or $packageInfo.engine -ne 'dynamo' -or
    $packageInfo.engine_version -notlike '4.0.*' -or $packageInfo.node_libraries.Count -ne 0) {
    throw 'pkg.json no cumple el formato esperado para una View Extension de Dynamo 4.0.'
}

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    if ($ElevatedWorker) { throw 'La elevación de Windows no entregó permisos de administrador.' }
    $psExe = Join-Path $env:windir 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $forceArgument = if ($Force) { ' -Force' } else { '' }
    $stageArgument = if ($StageWhileRevitOpen) { ' -StageWhileRevitOpen' } else { '' }
    $arguments = '-NoProfile -ExecutionPolicy Bypass -File "{0}" -ElevatedWorker{1}{2}' -f $PSCommandPath, $forceArgument, $stageArgument
    try {
        $elevated = Start-Process -FilePath $psExe -ArgumentList $arguments -Verb RunAs -Wait -PassThru
    } catch {
        throw 'Se canceló o falló la elevación necesaria para instalar la extensión en Dynamo for Revit.'
    }
    if ($elevated.ExitCode -ne 0) { throw "Falló la instalación elevada del panel (código $($elevated.ExitCode))." }
    exit 0
}

if ((Test-Path -LiteralPath $targetDll -PathType Leaf) -and
    (Get-Item -LiteralPath $targetDll).Attributes -band [IO.FileAttributes]::ReparsePoint) {
    throw 'El DLL versionado de destino es un enlace o punto de reanálisis; se conserva para revisión.'
}
if ((Test-Path -LiteralPath $targetDll -PathType Leaf) -and -not $Force) {
    $installedHash = (Get-FileHash -LiteralPath $targetDll -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($installedHash -ne $expectedHash) {
        throw 'Ya existe un DLL versionado distinto de Asistente BIM en Dynamo for Revit; se conserva.'
    }
}
if ((Test-Path -LiteralPath $targetDefinition -PathType Leaf) -and -not $Force) {
    try { [xml]$existing = Get-Content -LiteralPath $targetDefinition -Raw } catch { throw 'Hay un manifiesto Dynamo inválido; se conserva.' }
    if ($existing.ViewExtensionDefinition.TypeName -ne 'BimAiDynamoAssistant.BimAiDynamoViewExtension') {
        throw 'El manifiesto existente pertenece a otro tipo de extensión; se conserva.'
    }
    if ($existing.ViewExtensionDefinition.AssemblyPath -ne $manifestAssemblyPath) {
        throw 'Ya existe otra versión de Asistente BIM registrada; inspecciónala y usa -Force para actualizar.'
    }
}

if (-not (Test-Path -LiteralPath $targetDll -PathType Leaf)) {
    Copy-Item -LiteralPath $sourceDll -Destination $targetDll
}
$installedHash = (Get-FileHash -LiteralPath $targetDll -Algorithm SHA256).Hash.ToLowerInvariant()
if ($installedHash -ne $expectedHash) { throw 'El DLL instalado no coincide con el SHA-256 del paquete.' }

& $registrationScript -AssemblyPath $targetDll -Sha256 $expectedHash `
    -ManifestAssemblyPath $manifestAssemblyPath -DynamoRevitRoot $dynamoRoot -Force `
    -StageWhileRevitOpen:$StageWhileRevitOpen

if ($requiresRestart) {
    Write-Output 'DYNAMO_EXTENSION_INSTALL=STAGED_RESTART_REQUIRED'
} else {
    Write-Output 'DYNAMO_EXTENSION_INSTALL=PASS'
}
Write-Output ("DYNAMO_ROOT=" + $dynamoRoot)
Write-Output ("SHA256=" + $installedHash)
Write-Output ("VIEW_EXTENSION_MANIFEST=" + $targetDefinition)
Write-Output ("MANIFEST_ASSEMBLY_PATH=" + $manifestAssemblyPath)
Write-Output 'DLL_SCOPE=Revit2027Shared'
Write-Output 'NEXT=Reinicia Revit 2027 y abre Dynamo para cargar Asistente BIM.'
