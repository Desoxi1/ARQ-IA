#requires -Version 5.1
$ErrorActionPreference = 'Stop'

$runningRevit = @(Get-Process -Name Revit -ErrorAction SilentlyContinue)
if ($runningRevit.Count -gt 0) {
    throw 'Cierra todas las sesiones de Revit antes de retirar la instalación antigua.'
}

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Esta migración única requiere una consola de PowerShell abierta como administrador para retirar archivos de Program Files.'
}

$legacyRoot = Join-Path $env:ProgramFiles 'Autodesk\Revit 2027\AddIns\DynamoForRevit'
$legacyDefinition = Join-Path $legacyRoot 'viewExtensions\BimAiDynamoAssistant_ViewExtensionDefinition.xml'
if (-not (Test-Path -LiteralPath $legacyDefinition)) {
    Write-Output 'LEGACY_EXTENSION=NOT_INSTALLED'
    exit 0
}

if (-not (Test-Path -LiteralPath $legacyDefinition -PathType Leaf)) {
    throw 'Falta el manifiesto antiguo; no se eliminará ningún archivo sin poder verificar su identidad.'
}
[xml]$definition = Get-Content -LiteralPath $legacyDefinition -Raw
if ($definition.ViewExtensionDefinition.TypeName -ne 'BimAiDynamoAssistant.BimAiDynamoViewExtension' -or
    [string]$definition.ViewExtensionDefinition.AssemblyPath -notmatch '^\.\.\\BimAiDynamoAssistant(?:\.[0-9a-f]{16})?\.dll$') {
    throw 'El manifiesto de Program Files no coincide con el add-in antiguo esperado; no se eliminó nada.'
}
$legacyDll = [IO.Path]::GetFullPath([IO.Path]::Combine((Split-Path -Parent $legacyDefinition), [string]$definition.ViewExtensionDefinition.AssemblyPath))
if ([IO.Path]::GetFullPath((Split-Path -Parent $legacyDll)) -ne [IO.Path]::GetFullPath($legacyRoot)) {
    throw 'La DLL referida por el manifiesto no está dentro de Dynamo for Revit; se conserva.'
}
if (Test-Path -LiteralPath $legacyDll -PathType Leaf) {
    $assemblyName = [Reflection.AssemblyName]::GetAssemblyName($legacyDll)
    if ($assemblyName.Name -ne 'BimAiDynamoAssistant') {
        throw 'El assembly de Program Files no coincide con Asistente BIM; no se eliminó nada.'
    }
}

foreach ($path in @($legacyDefinition, $legacyDll)) {
    if (Test-Path -LiteralPath $path -PathType Leaf) {
        Remove-Item -LiteralPath $path -Force
    }
}

if ((Test-Path -LiteralPath $legacyDll) -or (Test-Path -LiteralPath $legacyDefinition)) {
    throw 'No se retiraron todos los archivos verificados de la extensión antigua.'
}
Write-Output 'LEGACY_EXTENSION=REMOVED'
Write-Output 'SCOPE=Only verified BIM-AI Dynamo DLL and view-extension manifest under the Revit 2027 add-in folder.'
