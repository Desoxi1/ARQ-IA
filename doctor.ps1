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

$assistantAddinPackage = Join-Path $projectRoot 'revit\assistant\dist\BimAiRevitAssistant.dll'
$assistantAddinHashFile = Join-Path $projectRoot 'revit\assistant\dist\BimAiRevitAssistant.sha256'
$assistantAddinsRoot = Join-Path $env:APPDATA 'Autodesk\Revit\Addins\2027'
$assistantAddinManifest = Join-Path $assistantAddinsRoot 'BimAiRevitAssistant.addin'
$assistantInstallTime = $null
if ((Test-Path -LiteralPath $assistantAddinPackage -PathType Leaf) -and
    (Test-Path -LiteralPath $assistantAddinHashFile -PathType Leaf)) {
    $expectedAssistantHash = (Get-Content -LiteralPath $assistantAddinHashFile -Raw).Trim().ToLowerInvariant()
    if ($expectedAssistantHash -notmatch '^[0-9a-f]{64}$' -or (Get-PathHash $assistantAddinPackage) -ne $expectedAssistantHash) {
        Write-Check FAIL 'El add-in portable de Revit no coincide con su SHA-256'
    } elseif (Test-Path -LiteralPath $assistantAddinManifest -PathType Leaf) {
        try {
            [xml]$assistantManifestXml = Get-Content -LiteralPath $assistantAddinManifest -Raw
            $assistantEntry = $assistantManifestXml.RevitAddIns.AddIn
            $installedAssistantAssembly = [IO.Path]::GetFullPath([string]$assistantEntry.Assembly)
            if ($assistantEntry.FullClassName -ne 'BimAiRevitAssistant.RevitAssistantApplication' -or
                $assistantEntry.VendorDescription -ne 'Branko Plaza' -or
                -not (Test-Path -LiteralPath $installedAssistantAssembly -PathType Leaf) -or
                (Get-PathHash $installedAssistantAssembly) -ne $expectedAssistantHash) {
                throw 'El registro, fabricante o ensamblado no coincide con el paquete.'
            }
            $assistantInstallTime = (Get-Item -LiteralPath $assistantAddinManifest).LastWriteTime
            Write-Check PASS 'Add-in nativo de Revit instalado; Branko Plaza y SHA-256 verificados'
        } catch {
            Write-Check FAIL 'El manifiesto o el ensamblado de Asistente BIM para Revit no es valido'
        }
    } else {
        Write-Check WAIT 'Add-in nativo listo; instala con revit\assistant\install.ps1'
    }
} else {
    Write-Check FAIL 'Falta el add-in portable de Revit o su manifiesto SHA-256; compila con revit\assistant\build.ps1'
}

if ($assistantInstallTime -and (Test-Path -LiteralPath $assistantAddinManifest -PathType Leaf)) {
    $runningAssistantSession = @(Get-Process -Name Revit -ErrorAction SilentlyContinue)
    $olderAssistantSessions = @()
    foreach ($process in $runningAssistantSession) {
        try { if ($process.StartTime -lt $assistantInstallTime) { $olderAssistantSessions += $process } } catch { }
    }
    if ($olderAssistantSessions.Count -gt 0) {
        Write-Check WAIT 'Guarda el trabajo y reinicia Revit cuando corresponda para cargar el panel BIM-AI en la cinta'
    }
}

$extensionPackageRoot = Join-Path $projectRoot 'dynamo\extension\dist'
$extensionPackageDll = Join-Path $extensionPackageRoot 'BimAiDynamoAssistant.dll'
$extensionPackageHash = Join-Path $extensionPackageRoot 'BimAiDynamoAssistant.dll.sha256'
$extensionPackageDefinition = Join-Path $extensionPackageRoot 'viewExtensions\BimAiDynamoAssistant_ViewExtensionDefinition.xml'
$extensionPackageManifest = Join-Path $extensionPackageRoot 'pkg.json'
$dynamoRoot = Join-Path $revitRoot 'AddIns\DynamoForRevit'
$extensionInstalledManifest = Join-Path $dynamoRoot 'viewExtensions\BimAiDynamoAssistant_ViewExtensionDefinition.xml'
$extensionRegistrationReady = $false
if ((Test-Path -LiteralPath $extensionPackageDll -PathType Leaf) -and
    (Test-Path -LiteralPath $extensionPackageHash -PathType Leaf) -and
    (Test-Path -LiteralPath $extensionPackageDefinition -PathType Leaf) -and
    (Test-Path -LiteralPath $extensionPackageManifest -PathType Leaf)) {
    $expectedExtensionHash = (Get-Content -LiteralPath $extensionPackageHash -Raw).Trim().ToLowerInvariant()
    if ($expectedExtensionHash -notmatch '^[0-9a-f]{64}$' -or (Get-PathHash $extensionPackageDll) -ne $expectedExtensionHash) {
        Write-Check FAIL 'El DLL portable de Asistente BIM no coincide con su SHA-256'
    } elseif (Test-Path -LiteralPath $extensionInstalledManifest -PathType Leaf) {
        try {
            [xml]$registeredExtension = Get-Content -LiteralPath $extensionInstalledManifest -Raw
            $registeredAssemblyText = [string]$registeredExtension.ViewExtensionDefinition.AssemblyPath
            if ([IO.Path]::IsPathRooted($registeredAssemblyText)) {
                $registeredAssembly = [IO.Path]::GetFullPath($registeredAssemblyText)
            } else {
                $registeredAssembly = [IO.Path]::GetFullPath([IO.Path]::Combine((Split-Path -Parent $extensionInstalledManifest), $registeredAssemblyText))
            }
            $registeredAssemblyRoot = [IO.Path]::GetFullPath((Split-Path -Parent $registeredAssembly))
            $expectedAssemblyRoot = [IO.Path]::GetFullPath($dynamoRoot)
            $manifestTypeMatches = $registeredExtension.ViewExtensionDefinition.TypeName -eq 'BimAiDynamoAssistant.BimAiDynamoViewExtension'
            $registrationMatches = [String]::Equals($registeredAssemblyRoot, $expectedAssemblyRoot, [StringComparison]::OrdinalIgnoreCase) -and
                [IO.Path]::GetFileName($registeredAssembly) -match '^BimAiDynamoAssistant(?:\.[0-9a-f]{16})?\.dll$' -and
                (Test-Path -LiteralPath $registeredAssembly -PathType Leaf)
        } catch {
            $manifestTypeMatches = $false
            $registrationMatches = $false
        }
        $installedExtensionHash = if ($registrationMatches) { Get-PathHash $registeredAssembly } else { '' }
        if (-not $manifestTypeMatches) {
            Write-Check FAIL 'El manifiesto de Dynamo es inválido o pertenece a otra extensión'
        } elseif (-not $registrationMatches) {
            Write-Check WAIT 'Asistente BIM aún apunta a una ruta antigua; migra el manifiesto con install.ps1 -Force y completa la elevación de Windows'
        } elseif ($installedExtensionHash -eq $expectedExtensionHash) {
            $extensionRegistrationReady = $true
            Write-Check PASS 'Asistente BIM instalado junto a Dynamo 4.0.2; DLL versionada, manifiesto relativo y SHA-256 coinciden'
        } else {
            Write-Check WAIT 'La extensión Dynamo instalada tiene una versión anterior; actualiza con install.ps1 -Force y completa la elevación de Windows'
        }
    } else {
        Write-Check WAIT 'Paquete de Asistente BIM listo; instala con dynamo\extension\install.ps1'
    }
} else {
    Write-Check FAIL 'Falta el DLL, SHA-256, pkg.json o manifiesto portable de Asistente BIM para Dynamo'
}

if ($extensionRegistrationReady) {
    $dynamoLogRoot = Join-Path $env:APPDATA 'Dynamo\Dynamo Revit\27.0\Logs'
    $latestDynamoLog = Get-ChildItem -LiteralPath $dynamoLogRoot -Filter 'dynamoLog_*.txt' -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    $manifestWriteTime = (Get-Item -LiteralPath $extensionInstalledManifest).LastWriteTime
    if (-not $latestDynamoLog -or $latestDynamoLog.LastWriteTime -lt $manifestWriteTime) {
        Write-Check WAIT 'Instalación lista; reinicia Revit/Dynamo para comprobar que cargue el panel'
    } elseif (Select-String -LiteralPath $latestDynamoLog.FullName -Pattern 'Asistente BIM \(id: a6b0d0a4-0d48-4d74-9a8f-9e0e3f3d1a27\) view extension is added' -Quiet) {
        Write-Check PASS 'El journal más reciente de Dynamo confirma que cargó Asistente BIM'
    } else {
        Write-Check WARN 'El journal más reciente de Dynamo no registra Asistente BIM; revisa la ruta del manifiesto y los errores de carga'
    }
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
        if ($configText -match '"open_dynamo_ui"') {
            Write-Check PASS 'Configuracion MCP local contiene los tres servidores y la herramienta de apertura de Dynamo'
        } else {
            Write-Check WAIT 'La configuración MCP no expone open_dynamo_ui; ejecuta setup.ps1 -ConfigureOnly y reinicia el panel de Codex'
        }
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
$bridgeInstallTime = $null
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
        $bridgeInstallTime = (Get-Item -LiteralPath $bridgeManifest).LastWriteTime
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
$freshRevitProcess = @()
if ($null -ne $bridgeInstallTime -and $revitProcesses.Count -gt 0) {
    try { $freshRevitProcess = @($revitProcesses | Where-Object { $_.StartTime -ge $bridgeInstallTime }) } catch { }
}
if ($testProcess.Count -gt 0) {
    Write-Check PASS 'Revit muestra abierto el modelo de prueba'
} elseif ($revitProcesses.Count -gt 0 -and $null -ne $bridgeInstallTime -and $freshRevitProcess.Count -eq 0) {
    Write-Check WAIT 'La sesion Revit activa precede la instalacion del bridge; guarda el trabajo cuando corresponda e inicia una sesion nueva para cargarlo'
} elseif ($revitProcesses.Count -gt 0) {
    Write-Check WAIT 'Revit esta abierto, pero no se detecta el RVT descartable en su titulo'
} else {
    Write-Check WAIT 'Revit no esta abierto; inicia Revit 2027 con el RVT descartable'
}

$bridgePipe = Get-ChildItem -LiteralPath '\\.\pipe\' -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -eq 'BIM-AI-POC-Revit-27' } | Select-Object -First 1
if ($bridgePipe) {
    Write-Check PASS 'Named pipe del bridge disponible'
    $pingClient = $null
    $pingReader = $null
    $pingWriter = $null
    try {
        $pipeName = 'BIM-AI-POC-Revit-27'
        $pipeEncoding = New-Object System.Text.UTF8Encoding($false)
        $pingClient = New-Object System.IO.Pipes.NamedPipeClientStream('.', $pipeName, [System.IO.Pipes.PipeDirection]::InOut, [System.IO.Pipes.PipeOptions]::None)
        $pingClient.Connect(2500)
        $pingReader = New-Object System.IO.StreamReader($pingClient, $pipeEncoding, $false, 1024, $true)
        $pingWriter = New-Object System.IO.StreamWriter($pingClient, $pipeEncoding, 1024, $true)
        $pingWriter.AutoFlush = $true
        $pingWriter.WriteLine('{"operation":"ping"}')
        $pingTask = $pingReader.ReadLineAsync()
        if (-not $pingTask.Wait(10000)) { throw 'bridge_ping_timeout' }
        $ping = $pingTask.Result | ConvertFrom-Json
        if ($ping.ok -ne $true -or [string]$ping.revitMajor -ne '2027') {
            Write-Check WARN 'El bridge respondió con un estado inesperado o una versión distinta de Revit'
        } elseif (-not $ping.PSObject.Properties['dynamoReadyForActiveDocument']) {
            Write-Check WAIT 'El bridge cargado es anterior al gate de Dynamo UI; reinicia Revit para cargar la versión preparada'
        } elseif ([bool]$ping.dynamoReadyForActiveDocument) {
            Write-Check PASS 'Dynamo UI está abierto y ligado al modelo Revit activo'
        } else {
            Write-Check WAIT 'El bridge actualizado responde; abre Dynamo UI con el RVT descartable activo'
        }
    } catch {
        Write-Check WAIT 'El pipe existe, pero el ping de estado no respondió dentro del límite'
    } finally {
        if ($pingWriter) { $pingWriter.Dispose() }
        if ($pingReader) { $pingReader.Dispose() }
        if ($pingClient) { $pingClient.Dispose() }
    }
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
