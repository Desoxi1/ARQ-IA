#requires -Version 5.1
[CmdletBinding()]
param([switch]$CheckOnly)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
$projectRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$revitRoot = Join-Path $env:ProgramFiles 'Autodesk\Revit 2027'
$revitExe = Join-Path $revitRoot 'Revit.exe'
$dynamoRoot = Join-Path $revitRoot 'AddIns\DynamoForRevit'
$revitAddins = Join-Path $env:APPDATA 'Autodesk\Revit\Addins\2027'
$mcpExe = Join-Path $env:LOCALAPPDATA 'Bimwright\rvt-mcp\server\current\rvt-mcp.exe'
$legacyMcpExe = Join-Path $env:LOCALAPPDATA 'RvtMcp\rvt\server\0.6.2\rvt-mcp.exe'
$mcpManifest = Join-Path $revitAddins 'RvtMcp.R27.addin'
$mcpPlugin = Join-Path $revitAddins 'RvtMcp\RvtMcp.Plugin.dll'
$runningRevit = @(Get-Process -Name Revit -ErrorAction SilentlyContinue)

function Assert-RequiredFile([string]$Path, [string]$Description) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "No se encontró $Description en: $Path" }
}

function Invoke-ProjectStep([string]$Name, [string]$ScriptPath, [string[]]$Arguments) {
    Assert-RequiredFile $ScriptPath $Name
    Write-Output "PASO=$Name"
    $powershellExe = Join-Path $env:windir 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $global:LASTEXITCODE = 0
    & $powershellExe -NoProfile -ExecutionPolicy Bypass -File $ScriptPath @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Name terminó con código $LASTEXITCODE." }
}

Write-Output 'ARQ-IA · instalación automática para Revit 2027 y Dynamo'
Assert-RequiredFile $revitExe 'Revit 2027'
Assert-RequiredFile (Join-Path $dynamoRoot 'DynamoCore.dll') 'Dynamo for Revit 2027'
Assert-RequiredFile (Join-Path $dynamoRoot 'DynamoCoreWpf.dll') 'Dynamo for Revit 2027'
foreach ($path in @(
    (Join-Path $projectRoot 'setup.ps1'),
    (Join-Path $projectRoot 'revit\bridge\install.ps1'),
    (Join-Path $projectRoot 'revit\assistant\install.ps1'),
    (Join-Path $projectRoot 'dynamo\extension\install.ps1'),
    (Join-Path $projectRoot 'materials\base.csv'),
    (Join-Path $projectRoot 'materials\local-materials.template.csv')
)) { Assert-RequiredFile $path 'un instalador del paquete' }

function Resolve-CodexExecutable {
    $onPath = Get-Command 'codex.exe' -CommandType Application -ErrorAction SilentlyContinue
    if ($onPath) { return $onPath.Source }
    $desktopBin = Join-Path $env:LOCALAPPDATA 'OpenAI\Codex\bin'
    if (Test-Path -LiteralPath $desktopBin -PathType Container) {
        foreach ($directory in @(Get-ChildItem -LiteralPath $desktopBin -Directory | Sort-Object LastWriteTime -Descending)) {
            $candidate = Join-Path $directory.FullName 'codex.exe'
            if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
        }
    }
    return $null
}

$codexExecutable = Resolve-CodexExecutable
if (-not $codexExecutable) {
    throw 'No se encontró Codex CLI. Instala Codex Desktop en este PC e inicia sesión antes de ejecutar Instalar-ARQ-IA.cmd.'
}
# codex.exe informa el estado por stderr incluso cuando el login es válido.
# Con $ErrorActionPreference='Stop' eso abortaría el instalador, así que se
# aísla la comprobación y solo manda el código de salida.
$codexLoginExit = 1
$prevErrorAction = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
try { & $codexExecutable login status *> $null; $codexLoginExit = $LASTEXITCODE } catch { $codexLoginExit = 1 } finally { $ErrorActionPreference = $prevErrorAction }
if ($codexLoginExit -ne 0) {
    throw 'Codex está instalado, pero falta iniciar sesión. Abre Codex Desktop e inicia sesión antes de instalar ARQ-IA.'
}
Write-Output ("CODEX_CLI=" + $codexExecutable)

if ($CheckOnly) {
    Write-Output 'PREFLIGHT=PASS'
    if ($runningRevit.Count -gt 0) {
        Write-Output 'REVIT_SESSION=OPEN; se prepararán los add-ins para la siguiente sesión.'
    } else {
        Write-Output 'REVIT_SESSION=CLOSED; instalación lista para la sesión siguiente.'
    }
    return
}

# rvt-mcp instala su add-in de Revit y solo se actualiza con Revit cerrado.
# Si ya existe, se puede regenerar la configuración local sin reiniciar la sesión.
$mcpInstalled = ((Test-Path -LiteralPath $mcpExe -PathType Leaf) -or (Test-Path -LiteralPath $legacyMcpExe -PathType Leaf)) -and
    (Test-Path -LiteralPath $mcpManifest -PathType Leaf) -and
    (Test-Path -LiteralPath $mcpPlugin -PathType Leaf)
if ($runningRevit.Count -gt 0 -and -not $mcpInstalled) {
    throw 'Guarda el trabajo y cierra Revit para la primera instalación de rvt-mcp. Después ejecuta una sola vez: .\install.ps1'
}

if ($runningRevit.Count -gt 0) {
    Invoke-ProjectStep 'MCP del proyecto Codex' (Join-Path $projectRoot 'setup.ps1') @('-ConfigureOnly')
} else {
    Invoke-ProjectStep 'rvt-mcp y configuración local Codex' (Join-Path $projectRoot 'setup.ps1') @()
}

$stageWhileRevitOpen = $runningRevit.Count -gt 0
$sharedArgs = @('-Force')
if ($stageWhileRevitOpen) { $sharedArgs += '-StageWhileRevitOpen' }
Invoke-ProjectStep 'bridge Revit-Dynamo' (Join-Path $projectRoot 'revit\bridge\install.ps1') $sharedArgs
Invoke-ProjectStep 'chat BIM dentro de Revit' (Join-Path $projectRoot 'revit\assistant\install.ps1') $sharedArgs
Invoke-ProjectStep 'panel ARQ-IA dentro de Dynamo' (Join-Path $projectRoot 'dynamo\extension\install.ps1') $sharedArgs

$bridgeSettings = Join-Path $env:APPDATA 'BIM-AI-POC\bridge.json'
$codexConfig = Join-Path $projectRoot '.codex\config.toml'
$dynamoManifest = Join-Path $env:APPDATA 'Dynamo\Dynamo Revit\27.0\packages\ARQ-IA\extra\BimAiDynamoAssistant_ViewExtensionDefinition.xml'
$revitAssistantManifest = Join-Path $revitAddins 'BimAiRevitAssistant.addin'
foreach ($path in @($bridgeSettings, $codexConfig, $dynamoManifest, $revitAssistantManifest)) {
    Assert-RequiredFile $path 'un archivo de instalación verificado'
}
$settings = Get-Content -LiteralPath $bridgeSettings -Raw | ConvertFrom-Json
if ([IO.Path]::GetFullPath([string]$settings.routinesRoot) -ne [IO.Path]::GetFullPath((Join-Path $projectRoot 'routines'))) {
    throw 'La extensión quedó apuntando a otra carpeta de rutinas; revisa bridge.json antes de usarla.'
}

# Create a private, per-clone materials overlay once; preserve any existing user additions.
$localMaterialsDirectory = Join-Path $projectRoot '.arq-ia\materials'
$localMaterialsCatalog = Join-Path $localMaterialsDirectory 'local.csv'
$localMaterialsTemplate = Join-Path $projectRoot 'materials\local-materials.template.csv'
New-Item -ItemType Directory -Force -Path $localMaterialsDirectory | Out-Null
if (-not (Test-Path -LiteralPath $localMaterialsCatalog -PathType Leaf)) {
    Copy-Item -LiteralPath $localMaterialsTemplate -Destination $localMaterialsCatalog
}

Write-Output 'INSTALL=PASS'
Write-Output 'MATERIALS_BASE=materials/base.csv'
Write-Output 'MATERIALS_LOCAL=.arq-ia/materials/local.csv (privado; no se sobrescribe)'
Write-Output 'CODEX_PROJECT=Abre esta carpeta en Codex y confirma Confiar si aparece el aviso para cargar los MCP locales.'
Write-Output 'REVIT_DYNAMO=Abre Revit y Dynamo en una sesión nueva para cargar ARQ-IA.'
Write-Output 'NEXT_TEST=En Dynamo, consulta la vista activa y la selección; no modifica el modelo.'
