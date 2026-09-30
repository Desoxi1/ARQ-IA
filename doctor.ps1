#requires -Version 5.1
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
$script:CheckCounts = @{ PASS = 0; WAIT = 0; WARN = 0; FAIL = 0 }
$projectRoot = [IO.Path]::GetFullPath($PSScriptRoot)

function Write-Check {
    param(
        [Parameter(Mandatory = $true)][ValidateSet('PASS', 'WAIT', 'WARN', 'FAIL')][string]$State,
        [Parameter(Mandatory = $true)][string]$Message
    )
    $script:CheckCounts[$State]++
    Write-Output "[$State] $Message"
}

function Get-PathHash([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

Write-Output 'Diagnostico BIM-AI-POC (solo lectura; no modifica Revit ni instala archivos)'

$revitRoot = Join-Path $env:ProgramFiles 'Autodesk\Revit 2027'
$revitExe = Join-Path $revitRoot 'Revit.exe'
$dynamoAssembly = Join-Path $revitRoot 'AddIns\DynamoForRevit\Revit\DynamoRevitDS.dll'
if (Test-Path -LiteralPath $revitExe -PathType Leaf) {
    Write-Check PASS 'Revit 2027 instalado'
} else {
    Write-Check FAIL 'No se encontro Revit 2027'
}
if (Test-Path -LiteralPath $dynamoAssembly -PathType Leaf) {
    Write-Check PASS 'Dynamo for Revit detectado'
} else {
    Write-Check FAIL 'No se encontro DynamoRevitDS.dll para Revit 2027'
}

foreach ($toolName in @('git', 'python', 'codex')) {
    if (Get-Command $toolName -ErrorAction SilentlyContinue) {
        Write-Check PASS "$toolName disponible en PATH"
    } else {
        Write-Check WARN "$toolName no esta en PATH (no bloquea la consulta local del diagnostico)"
    }
}

$codexConfig = Join-Path $projectRoot '.codex\config.toml'
$requiredServers = @('rvt_mcp', 'bim_ai_routines', 'bim_ai_dynamo_runner')
if (Test-Path -LiteralPath $codexConfig -PathType Leaf) {
    $configText = Get-Content -LiteralPath $codexConfig -Raw
    $missingServers = @($requiredServers | Where-Object {
        $configText -notmatch ('(?m)^\[mcp_servers\.' + [regex]::Escape($_) + '\]$')
    })
    if ($missingServers.Count -eq 0) {
        Write-Check PASS 'Configuracion MCP local contiene los tres servidores del proyecto'
    } else {
        Write-Check FAIL ('Faltan servidores MCP en la configuracion local: ' + ($missingServers -join ', '))
    }
} else {
    Write-Check WAIT 'Falta .codex/config.toml; ejecuta setup.ps1 y abre la carpeta como proyecto confiable en Codex'
}

$mcpRoot = Join-Path $env:LOCALAPPDATA 'RvtMcp\rvt\server\0.6.2'
$mcpExe = Join-Path $mcpRoot 'rvt-mcp.exe'
$revitAddinsRoot = Join-Path $env:APPDATA 'Autodesk\Revit\Addins\2027'
$rvtMcpManifest = Join-Path $revitAddinsRoot 'RvtMcp.R27.addin'
$rvtMcpPlugin = Join-Path $revitAddinsRoot 'RvtMcp\RvtMcp.Plugin.dll'
if ((Test-Path -LiteralPath $mcpExe -PathType Leaf) -and
    (Test-Path -LiteralPath $rvtMcpManifest -PathType Leaf) -and
    (Test-Path -LiteralPath $rvtMcpPlugin -PathType Leaf)) {
    Write-Check PASS 'rvt-mcp v0.6.2 instalado para Revit 2027'
} else {
    Write-Check WAIT 'rvt-mcp no esta completo; ejecuta setup.ps1'
}

$bridgeManifest = Join-Path $revitAddinsRoot 'BimAiDynamoBridge.addin'
$bridgeSource = Join-Path $projectRoot 'revit\bridge\dist\BimAiDynamoBridge.dll'
$bridgeHashFile = Join-Path $projectRoot 'revit\bridge\dist\BimAiDynamoBridge.sha256'
$bridgeSettings = Join-Path $env:APPDATA 'BIM-AI-POC\bridge.json'
if ((Test-Path -LiteralPath $bridgeManifest -PathType Leaf) -and
    (Test-Path -LiteralPath $bridgeSource -PathType Leaf) -and
    (Test-Path -LiteralPath $bridgeHashFile -PathType Leaf)) {
    try {
        [xml]$manifestXml = Get-Content -LiteralPath $bridgeManifest -Raw
        $installedAssembly = [string]$manifestXml.RevitAddIns.AddIn.Assembly
        if (-not (Test-Path -LiteralPath $installedAssembly -PathType Leaf)) {
            throw 'El ensamblado referenciado por el manifiesto no existe.'
        }
        $expectedHash = (Get-Content -LiteralPath $bridgeHashFile -Raw).Trim().ToLowerInvariant()
        if ($expectedHash -notmatch '^[0-9a-f]{64}$' -or
            (Get-PathHash $bridgeSource) -ne $expectedHash -or
            (Get-PathHash $installedAssembly) -ne $expectedHash) {
            throw 'El DLL instalado no coincide con el hash del paquete.'
        }
        Write-Check PASS 'Add-in BIM-AI instalado y su DLL coincide con el paquete'
    } catch {
        Write-Check FAIL 'El manifiesto o el hash del add-in BIM-AI no es valido'
    }
} else {
    Write-Check WAIT 'Add-in BIM-AI aun no esta instalado; ejecuta revit\bridge\install.ps1'
}

if (Test-Path -LiteralPath $bridgeSettings -PathType Leaf) {
    try {
        $settings = Get-Content -LiteralPath $bridgeSettings -Raw | ConvertFrom-Json
        $expectedRoutines = [IO.Path]::GetFullPath((Join-Path $projectRoot 'routines'))
        $expectedTestModels = [IO.Path]::GetFullPath((Join-Path $projectRoot 'revit\test-models'))
        $configuredRoutines = [IO.Path]::GetFullPath([string]$settings.routinesRoot)
        $configuredTestModels = [IO.Path]::GetFullPath([string]$settings.testModelsRoot)
        if ($configuredRoutines -ne $expectedRoutines -or $configuredTestModels -ne $expectedTestModels) {
            throw 'Las rutas configuradas no coinciden con esta carpeta del proyecto.'
        }
        Write-Check PASS 'bridge.json apunta a las rutinas y modelos de prueba de esta carpeta'
    } catch {
        Write-Check FAIL 'bridge.json no es valido para esta carpeta del proyecto'
    }
} else {
    Write-Check WAIT 'Falta bridge.json; ejecuta revit\bridge\install.ps1'
}

$testRoot = Join-Path $projectRoot 'revit\test-models'
$testModels = @(
    (Join-Path $testRoot 'BIM-AI-Dynamo-Test-9205.rvt'),
    (Join-Path $testRoot 'BIM-AI-Dynamo-Test.rvt')
)
$testModel = $testModels | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
if ($testModel) {
    Write-Check PASS 'RVT descartable disponible en revit\test-models'
    $testModelItem = Get-Item -LiteralPath $testModel
    if ($testModelItem.IsReadOnly) {
        Write-Check WAIT 'El RVT descartable esta en solo lectura; ejecuta prepare-test-model.ps1 para habilitar escrituras de prueba'
    } else {
        Write-Check PASS 'El RVT descartable permite escrituras locales'
    }
    if ([IO.Path]::GetFileName($testModel) -like 'BIM-AI-Dynamo-Test*.rvt') {
        $requiredSampleLinks = @(
            'Snowdon Towers Sample Architectural.rvt',
            'Snowdon Towers Sample Structural.rvt',
            'Snowdon Towers Sample Plumbing.rvt',
            'Snowdon Towers Sample Electrical.rvt',
            'Snowdon Towers Sample Site.rvt'
        )
        $missingSampleLinks = @($requiredSampleLinks | Where-Object { -not (Test-Path -LiteralPath (Join-Path $testRoot $_) -PathType Leaf) })
        if ($missingSampleLinks.Count -eq 0) {
            Write-Check PASS 'Los cinco RVT vinculados de Snowdon estan junto al modelo descartable'
        } else {
            Write-Check WAIT ("Faltan RVT vinculados de Snowdon: " + ($missingSampleLinks -join ', ') + '; prepara la muestra para copiar sus dependencias locales')
        }
    }
} else {
    Write-Check WAIT 'Falta el RVT descartable; prepara una copia local con revit\test-models\prepare-test-model.ps1'
}

$revitProcesses = @(Get-Process -Name Revit -ErrorAction SilentlyContinue)
$testProcess = @($revitProcesses | Where-Object { $_.MainWindowTitle -match 'BIM-AI-Dynamo-Test' })
if ($testProcess.Count -gt 0) {
    Write-Check PASS 'Revit muestra abierto el modelo de prueba'
} elseif ($revitProcesses.Count -gt 0) {
    Write-Check WAIT 'Revit esta abierto, pero no se detecta el RVT descartable en su titulo'
} else {
    Write-Check WAIT 'Revit no esta abierto; inicia Revit 2027 con el RVT descartable'
}

$bridgePipe = Get-ChildItem -LiteralPath '\\.\pipe\' -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -eq 'BIM-AI-POC-Revit-27' } | Select-Object -First 1
if ($bridgePipe) {
    Write-Check PASS 'Named pipe del bridge disponible'
} else {
    Write-Check WAIT 'Pipe del bridge no disponible; inicia una sesion de prueba Revit 2027 y acepta la carga del add-in'
}

Write-Output ''
Write-Output ("RESUMEN PASS={0} WAIT={1} WARN={2} FAIL={3}" -f `
    $script:CheckCounts.PASS, $script:CheckCounts.WAIT, $script:CheckCounts.WARN, $script:CheckCounts.FAIL)
if ($script:CheckCounts.FAIL -gt 0) {
    Write-Output 'RESULT=SETUP_INCOMPLETE'
    exit 1
}
if ($script:CheckCounts.WAIT -gt 0) {
    Write-Output 'RESULT=READY_FOR_NEXT_SETUP_STEP'
} else {
    Write-Output 'RESULT=READY_FOR_READONLY_LIVE_TEST'
}
