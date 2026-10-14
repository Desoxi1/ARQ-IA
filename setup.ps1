#requires -Version 5.1
[CmdletBinding()]
param([switch]$ConfigureOnly)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$packagePath = Join-Path $repoRoot 'vendor\RvtMcp.Setup-v1.1.0-win-x64.zip'
$packageUri = 'https://github.com/bimwright/rvt-mcp/releases/download/v1.1.0/RvtMcp.Setup-v1.1.0-win-x64.zip'
$expectedHash = '0b3f93df2f993b0cb537426ecbb33330497aadd04360ac68e575663160e4c0bb'
$revitAddins = Join-Path $env:APPDATA 'Autodesk\Revit\Addins\2027'
$currentServerPath = Join-Path $env:LOCALAPPDATA 'Bimwright\rvt-mcp\server\current\rvt-mcp.exe'
$legacyServerPath = Join-Path $env:LOCALAPPDATA 'RvtMcp\rvt\server\0.6.2\rvt-mcp.exe'
$addinManifest = Join-Path $revitAddins 'RvtMcp.R27.addin'
$pluginDll = Join-Path $revitAddins 'RvtMcp\RvtMcp.Plugin.dll'
$codexDir = Join-Path $repoRoot '.codex'
$codexConfig = Join-Path $codexDir 'config.toml'

function Get-Sha256Hex([string]$Path) {
    $stream = [IO.File]::OpenRead($Path)
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-', '').ToLowerInvariant()
    } finally {
        $stream.Dispose()
        $sha.Dispose()
    }
}

if ($ConfigureOnly) {
    if (-not (Test-Path -LiteralPath $currentServerPath -PathType Leaf) -or
        -not (Test-Path -LiteralPath $addinManifest -PathType Leaf) -or
        -not (Test-Path -LiteralPath $pluginDll -PathType Leaf)) {
        throw 'ConfigureOnly requiere rvt-mcp v1.1.0 instalado para Revit 2027. Ejecuta el instalador completo para actualizar.'
    }
    Write-Host 'Se actualizará solamente la configuración local MCP del proyecto.'
} else {
    $runningRevit = Get-Process -Name Revit -ErrorAction SilentlyContinue
    if ($runningRevit) { throw 'Cierra todas las ventanas de Revit 2027 antes de instalar o actualizar add-ins.' }

$alreadyInstalled = (Test-Path -LiteralPath $currentServerPath -PathType Leaf) -and
    (Test-Path -LiteralPath $addinManifest -PathType Leaf) -and (Test-Path -LiteralPath $pluginDll -PathType Leaf)
if (-not $alreadyInstalled -and -not (Test-Path -LiteralPath $packagePath -PathType Leaf)) {
    $vendorDir = Split-Path -Parent $packagePath
    New-Item -ItemType Directory -Force -Path $vendorDir | Out-Null
    $downloadPath = Join-Path $env:TEMP ('RvtMcp-v1.1.0-' + [Guid]::NewGuid().ToString('N') + '.zip')
    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Write-Host 'Descargando el instalador oficial rvt-mcp v1.1.0…'
        Invoke-WebRequest -Uri $packageUri -OutFile $downloadPath -UseBasicParsing
        $downloadHash = Get-Sha256Hex $downloadPath
        if ($downloadHash -ne $expectedHash) { throw "El SHA-256 del instalador descargado no coincide. No se instalará: $downloadHash" }
        Move-Item -LiteralPath $downloadPath -Destination $packagePath
    } finally {
        if (Test-Path -LiteralPath $downloadPath) { Remove-Item -LiteralPath $downloadPath -Force }
    }
}

$needsInstall = -not $alreadyInstalled
if ($needsInstall) {
    $actualHash = Get-Sha256Hex $packagePath
    if ($actualHash -ne $expectedHash) { throw "El hash del paquete no coincide. No se instalará. SHA-256 encontrado: $actualHash" }
    $tempRoot = [IO.Path]::GetFullPath($env:TEMP)
    $stageRoot = [IO.Path]::GetFullPath((Join-Path $tempRoot ('ARQ-IA-RvtMcp-v1.1.0-' + [Guid]::NewGuid().ToString('N'))))
    if (-not $stageRoot.StartsWith($tempRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'El directorio temporal calculado está fuera de TEMP.' }
    New-Item -ItemType Directory -Path $stageRoot | Out-Null
    try {
        Expand-Archive -LiteralPath $packagePath -DestinationPath $stageRoot
        $installer = Join-Path $stageRoot 'install.ps1'
        if (-not (Test-Path -LiteralPath $installer -PathType Leaf)) { throw 'El paquete no contiene install.ps1.' }
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $installer -Years 2027 -Client none
        if ($LASTEXITCODE -ne 0) { throw "El instalador rvt-mcp terminó con código $LASTEXITCODE." }
    } finally {
        if (Test-Path -LiteralPath $stageRoot) { Remove-Item -LiteralPath $stageRoot -Recurse -Force }
    }
}

if (-not (Test-Path -LiteralPath $addinManifest -PathType Leaf) -or -not (Test-Path -LiteralPath $pluginDll -PathType Leaf)) { throw 'No se encontró el add-in de Revit 2027 después de instalar.' }
}

$serverPath = if (Test-Path -LiteralPath $currentServerPath -PathType Leaf) { $currentServerPath } else { $null }
if (-not $serverPath) { throw 'No se encontró rvt-mcp v1.1.0. La configuración nueva no es compatible con 0.6.2; ejecuta la instalación completa.' }

New-Item -ItemType Directory -Force -Path $codexDir | Out-Null
if (Test-Path -LiteralPath $codexConfig -PathType Leaf) {
    $existing = Get-Content -LiteralPath $codexConfig -Raw
    $managedHeader = $existing.Contains('# ARQ-IA managed config') -or $existing.Contains('# BIM-AI-POC managed config') -or $existing.Contains('# Generated by setup.ps1 for the local machine. Absolute paths are machine-specific.') -or $existing.Contains('# Project-local BIM-AI-POC MCP configuration. No secrets are stored here.')
    $serverSections = [regex]::Matches($existing, '(?m)^\[mcp_servers\.([^\]]+)\]\s*$')
    $allowedServerNames = @('rvt_mcp', 'bim_ai_routines', 'bim_ai_dynamo_runner')
    $sectionsAreManaged = $serverSections.Count -ge 2 -and $serverSections.Count -le 3
    foreach ($section in $serverSections) {
        if ($section.Groups[1].Value -notin $allowedServerNames) { $sectionsAreManaged = $false }
    }
    if ($managedHeader -and $existing.Contains('[mcp_servers.rvt_mcp]') -and $existing.Contains('[mcp_servers.bim_ai_routines]') -and $sectionsAreManaged) {
        Write-Host 'Se actualizará la configuración MCP generada para este paquete.'
    } else {
        throw "$codexConfig ya existe y no es una configuración ARQ-IA generada sin otros servidores. Se conservó sin cambios; revisa docs\INSTRUCCIONES_OTRO_PC.md para fusionar la configuración manualmente."
    }
}

function Escape-TomlString([string]$Value) {
    return $Value.Replace('\', '\\').Replace('"', '\"')
}

$psExe = Join-Path $env:windir 'System32\WindowsPowerShell\v1.0\powershell.exe'
$revitServerEscaped = Escape-TomlString $serverPath
$powershellEscaped = Escape-TomlString $psExe
$builderScriptEscaped = Escape-TomlString (Join-Path $repoRoot 'mcp\RoutineBuilderMcp.ps1')
$dynamoRunnerScriptEscaped = Escape-TomlString (Join-Path $repoRoot 'mcp\DynamoRunnerMcp.ps1')
$repoRootEscaped = Escape-TomlString $repoRoot
$toolConfigPath = Join-Path $repoRoot 'config\rvt-mcp-tools.json'
if (-not (Test-Path -LiteralPath $toolConfigPath -PathType Leaf)) { throw 'Falta config\rvt-mcp-tools.json.' }
$toolConfig = Get-Content -LiteralPath $toolConfigPath -Raw | ConvertFrom-Json
$supportedToolsets = @('query', 'create', 'modify', 'view', 'annotation', 'graphics', 'materials', 'geometry', 'rooms', 'sheets', 'parameters', 'organization', 'structural', 'schedule', 'families', 'links', 'lint', 'mep')
$requiredBlockedToolsets = @('meta', 'delete', 'toolbaker', 'export', 'workflows')
$requiredDisabledTools = @('revit_send_code_to_revit', 'revit_run_baked_tool', 'arbitrary_code')
$readOnlyTools = @($toolConfig.clasificacion_herramientas_es.solo_lectura)
$writeTools = @($toolConfig.clasificacion_herramientas_es.requieren_aprobacion)
$classifiedTools = @($readOnlyTools + $writeTools)
$unclassifiedTools = @($toolConfig.enabled_tools | Where-Object { $_ -notin $classifiedTools })
$classificationMismatch = $classifiedTools.Count -ne $toolConfig.enabled_tools.Count -or
    @($classifiedTools | Select-Object -Unique).Count -ne $classifiedTools.Count -or
    @($toolConfig.enabled_tools | Where-Object { $_ -notin $classifiedTools }).Count -gt 0
$unsafeToolsetEnabled = @($toolConfig.toolsets | Where-Object { $_ -notin $supportedToolsets -or $_ -in $requiredBlockedToolsets }).Count -gt 0 -or
    @($toolConfig.toolsets | Where-Object { $_ -notin $toolConfig.toolsets_es.PSObject.Properties.Name }).Count -gt 0
$requiredProtectionMissing = @($requiredBlockedToolsets | Where-Object { $_ -notin $toolConfig.toolsets_bloqueados }).Count -gt 0 -or
    @($requiredDisabledTools | Where-Object { $_ -notin $toolConfig.always_disabled }).Count -gt 0 -or
    $toolConfig.protecciones.deshabilitar_send_code -ne $true -or
    $toolConfig.protecciones.deshabilitar_toolbaker -ne $true -or
    $toolConfig.protecciones.requiere_aprobacion -ne $true
if ($toolConfig.schema_version -ne 1 -or $toolConfig.idioma -ne 'es-CL' -or $toolConfig.version_rvt_mcp -ne '1.1.0' -or $toolConfig.approval_mode -ne 'prompt' -or
    @($toolConfig.toolsets).Count -eq 0 -or @($toolConfig.toolsets | Select-Object -Unique).Count -ne @($toolConfig.toolsets).Count -or
    @($toolConfig.enabled_tools).Count -eq 0 -or @($toolConfig.enabled_tools | Select-Object -Unique).Count -ne @($toolConfig.enabled_tools).Count -or
    $classificationMismatch -or $unclassifiedTools.Count -gt 0 -or $unsafeToolsetEnabled -or $requiredProtectionMissing -or
    @($toolConfig.always_disabled | Where-Object { $_ -in $toolConfig.enabled_tools }).Count -gt 0) {
    throw 'config\rvt-mcp-tools.json no cumple la versión, clasificación o protecciones requeridas.'
}
foreach ($tool in $toolConfig.enabled_tools) {
    if ($tool -notmatch '^revit_[a-z0-9_]+$' -or $tool -match '(?i)delete|send_code|bake|arbitrary') {
        throw "Herramienta fuera de la lista permitida de ARQ-IA: $tool"
    }
}
$toolsetList = $toolConfig.toolsets -join ','
$enabledToolsToml = ($toolConfig.enabled_tools | ForEach-Object { '"' + $_ + '"' }) -join ', '
$configText = @"
# ARQ-IA managed config generated by setup.ps1. No secrets are stored here.
[mcp_servers.rvt_mcp]
command = "$revitServerEscaped"
args = ["--toolsets", "$toolsetList", "--disable-toolbaker", "--disable-send-code"]
enabled_tools = [$enabledToolsToml]
default_tools_approval_mode = "$($toolConfig.approval_mode)"
startup_timeout_sec = 30
tool_timeout_sec = 60

[mcp_servers.bim_ai_routines]
command = "$powershellEscaped"
args = ["-NoLogo", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "$builderScriptEscaped"]
cwd = "$repoRootEscaped"
enabled_tools = ["list_saved_routines", "create_dynamo_routine"]
default_tools_approval_mode = "prompt"
startup_timeout_sec = 20
tool_timeout_sec = 60

[mcp_servers.bim_ai_dynamo_runner]
command = "$powershellEscaped"
args = ["-NoLogo", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "$dynamoRunnerScriptEscaped"]
cwd = "$repoRootEscaped"
enabled_tools = ["open_dynamo_ui", "ping_revit_dynamo_bridge", "prepare_saved_routine_in_dynamo", "execute_saved_readonly_routine", "execute_saved_routine", "execute_saved_routine_in_active_model"]
default_tools_approval_mode = "prompt"
startup_timeout_sec = 20
tool_timeout_sec = 120
"@
[IO.File]::WriteAllText($codexConfig, $configText, (New-Object System.Text.UTF8Encoding($false)))

# Codex loads project-local MCP configuration only for a trusted project root.
# The user explicitly started this installer from this clone, so register only this exact path.
$userCodexDir = Join-Path $env:USERPROFILE '.codex'
$userCodexConfig = Join-Path $userCodexDir 'config.toml'
New-Item -ItemType Directory -Path $userCodexDir -Force | Out-Null
$userConfigText = if (Test-Path -LiteralPath $userCodexConfig -PathType Leaf) {
    [IO.File]::ReadAllText($userCodexConfig)
} else { '' }
$projectHeaders = [regex]::Matches($userConfigText, '(?m)^\[projects\.(?:"((?:[^"\\]|\\.)*)"|''([^'']*)'')\]\s*$')
$projectAlreadyRegistered = $false
foreach ($header in $projectHeaders) {
    $headerPath = if ($header.Groups[1].Success) {
        $header.Groups[1].Value.Replace('\\', '\').Replace('\"', '"')
    } else { $header.Groups[2].Value }
    if ([string]::Equals($headerPath, $repoRoot, [StringComparison]::OrdinalIgnoreCase)) {
        $projectAlreadyRegistered = $true
        $sectionEnd = $userConfigText.IndexOf("`n[", $header.Index + $header.Length)
        if ($sectionEnd -lt 0) { $sectionEnd = $userConfigText.Length }
        $section = $userConfigText.Substring($header.Index, $sectionEnd - $header.Index)
        if ($section -notmatch '(?m)^trust_level\s*=\s*"trusted"\s*$') {
            throw 'Este proyecto ya está registrado en Codex con otro nivel de confianza; cámbialo manualmente antes de usar los MCP.'
        }
        break
    }
}
if (-not $projectAlreadyRegistered) {
    $headerPath = Escape-TomlString $repoRoot
    $prefix = if ($userConfigText.Length -gt 0 -and -not $userConfigText.EndsWith("`n")) { "`r`n" } else { '' }
    $trustSection = "$prefix`r`n[projects.`"$headerPath`"]`r`ntrust_level = `"trusted`"`r`n"
    [IO.File]::AppendAllText($userCodexConfig, $trustSection, (New-Object System.Text.UTF8Encoding($false)))
    Write-Host 'Se registró esta carpeta exacta como proyecto confiable de Codex para activar sus MCP locales.'
}

if ($ConfigureOnly) {
    Write-Host 'Configuración MCP del proyecto actualizada; no se modificaron add-ins ni Revit.'
} else {
    Write-Host 'Instalación completada para Revit 2027 en el perfil de usuario.'
}
Write-Host 'Codex pedirá aprobación para cada herramienta Revit tipada permitida y para el generador de rutinas.'
Write-Host "Proyecto Codex activo para esta ruta: $repoRoot"
