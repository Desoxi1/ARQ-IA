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
    if (-not ((Test-Path -LiteralPath $currentServerPath -PathType Leaf) -or (Test-Path -LiteralPath $legacyServerPath -PathType Leaf)) -or
        -not (Test-Path -LiteralPath $addinManifest -PathType Leaf) -or
        -not (Test-Path -LiteralPath $pluginDll -PathType Leaf)) {
        throw 'ConfigureOnly requiere rvt-mcp instalado para Revit 2027.'
    }
    Write-Host 'Se actualizará solamente la configuración local MCP del proyecto.'
} else {
    $runningRevit = Get-Process -Name Revit -ErrorAction SilentlyContinue
    if ($runningRevit) { throw 'Cierra todas las ventanas de Revit 2027 antes de instalar o actualizar add-ins.' }

$alreadyInstalled = ((Test-Path -LiteralPath $currentServerPath -PathType Leaf) -or (Test-Path -LiteralPath $legacyServerPath -PathType Leaf)) -and
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

$serverPath = if (Test-Path -LiteralPath $currentServerPath -PathType Leaf) { $currentServerPath } elseif (Test-Path -LiteralPath $legacyServerPath -PathType Leaf) { $legacyServerPath } else { $null }
if (-not $serverPath) { throw 'No se encontró el servidor rvt-mcp instalado.' }

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
$configText = @"
# ARQ-IA managed config generated by setup.ps1. No secrets are stored here.
[mcp_servers.rvt_mcp]
command = "$revitServerEscaped"
args = ["--toolsets", "query,create,modify,view,annotation,graphics,materials", "--disable-toolbaker"]
enabled_tools = [
  "revit_get_current_view_info", "revit_get_selected_elements", "revit_get_element_details",
  "revit_get_element_parameters", "revit_get_type_parameters", "revit_get_available_family_types",
  "revit_get_element_relationships", "revit_ai_element_filter", "revit_list_project_parameters",
  "revit_list_worksets", "revit_list_materials", "revit_get_material_properties",
  "revit_get_material_quantities", "revit_analyze_model_statistics",
  "revit_create_level", "revit_create_grid", "revit_create_line_based_element",
  "revit_create_surface_based_element", "revit_create_room", "revit_create_point_based_element",
  "revit_set_element_parameter_values", "revit_change_element_type", "revit_override_element_graphics", "revit_create_text_note",
  "revit_create_dimensions", "revit_tag_elements"
]
default_tools_approval_mode = "prompt"
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
