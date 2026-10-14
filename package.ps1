#requires -Version 5.1
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts'))
$prefix = 'ARQ-IA/'

if (-not (Test-Path -LiteralPath (Join-Path $projectRoot '.git') -PathType Container)) {
    throw 'Ejecuta este empaquetador desde un checkout Git del proyecto.'
}
$status = @(& git -C $projectRoot status --porcelain)
if ($LASTEXITCODE -ne 0) { throw 'No se pudo consultar el estado de Git.' }
if ($status.Count -gt 0) { throw 'Guarda los cambios en un commit antes de empaquetar.' }

$revision = (& git -C $projectRoot rev-parse --short HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $revision -notmatch '^[0-9a-f]{7,40}$') { throw 'No se pudo resolver el commit actual.' }
New-Item -ItemType Directory -Force -Path $artifactsRoot | Out-Null
$archivePath = Join-Path $artifactsRoot ('ARQ-IA-' + $revision + '.zip')
$tempArchive = Join-Path $artifactsRoot ('.ARQ-IA-' + [Guid]::NewGuid().ToString('N') + '.tmp.zip')
if (Test-Path -LiteralPath $archivePath) { throw 'Ya existe un paquete para este commit; no se sobrescribio.' }

try {
    & git -C $projectRoot archive --format=zip ('--prefix=' + $prefix) --output=$tempArchive HEAD
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $tempArchive -PathType Leaf)) {
        throw 'git archive no pudo crear el paquete.'
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead($tempArchive)
    try {
        $entries = @($zip.Entries | Where-Object { -not [string]::IsNullOrEmpty($_.Name) })
        $requiredFiles = @(
            'ARQ-IA/README.md',
            'ARQ-IA/Instalar-ARQ-IA.cmd',
            'ARQ-IA/install.ps1',
            'ARQ-IA/setup.ps1',
            'ARQ-IA/doctor.ps1',
            'ARQ-IA/docs/COLOR_MUROS_PRIMER_PISO.md',
            'ARQ-IA/docs/ESTRUCTURA_DEL_REPOSITORIO.md',
            'ARQ-IA/governance/assistant-policy.md',
            'ARQ-IA/governance/README.md',
            'ARQ-IA/config/README.md',
            'ARQ-IA/config/rvt-mcp-tools.json',
            'ARQ-IA/knowledge/README.md',
            'ARQ-IA/knowledge/standards/operacion-y-validacion.md',
            'ARQ-IA/knowledge/references/REPOSITORIOS_Y_CONTENIDO.md',
            'ARQ-IA/knowledge/playbooks/INDEX.md',
            'ARQ-IA/knowledge/playbooks/arquitectura-chile.md',
            'ARQ-IA/knowledge/playbooks/ciclos-imagen.md',
            'ARQ-IA/knowledge/playbooks/diagnostico-modelo.md',
            'ARQ-IA/knowledge/playbooks/estructura-copia.md',
            'ARQ-IA/knowledge/playbooks/fichas-materiales.md',
            'ARQ-IA/knowledge/playbooks/muros-cotas.md',
            'ARQ-IA/knowledge/progressive-learning.md',
            'ARQ-IA/knowledge/templates/project-memory.template.md',
            'ARQ-IA/materials/base.csv',
            'ARQ-IA/materials/TAXONOMIA.md',
            'ARQ-IA/materials/local-materials.template.csv',
            'ARQ-IA/materials/README.md',
            'ARQ-IA/routines/README.md',
            'ARQ-IA/vendor/README.md',
            'ARQ-IA/mcp/RoutineBuilderMcp.ps1',
            'ARQ-IA/mcp/DynamoRunnerMcp.ps1',
            'ARQ-IA/dynamo/extension/README.md',
            'ARQ-IA/dynamo/extension/install.ps1',
            'ARQ-IA/dynamo/extension/dist/BimAiDynamoAssistant.dll',
            'ARQ-IA/dynamo/extension/dist/BimAiDynamoAssistant.dll.sha256',
            'ARQ-IA/dynamo/extension/dist/pkg.json',
            'ARQ-IA/dynamo/extension/dist/viewExtensions/BimAiDynamoAssistant_ViewExtensionDefinition.xml',
            'ARQ-IA/revit/assistant/BimAiRevitAssistant.csproj',
            'ARQ-IA/revit/assistant/README.md',
            'ARQ-IA/revit/assistant/build.ps1',
            'ARQ-IA/revit/assistant/install.ps1',
            'ARQ-IA/revit/assistant/dist/BimAiRevitAssistant.dll',
            'ARQ-IA/revit/assistant/dist/BimAiRevitAssistant.sha256',
            'ARQ-IA/revit/bridge/dist/BimAiDynamoBridge.dll',
            'ARQ-IA/revit/bridge/dist/BimAiDynamoBridge.sha256'
        )
        $entryNames = @($entries | ForEach-Object { $_.FullName })
        foreach ($requiredFile in $requiredFiles) {
            if ($requiredFile -notin $entryNames) { throw "Falta un archivo requerido: $requiredFile" }
        }

        $extensionDllEntry = $zip.GetEntry('ARQ-IA/dynamo/extension/dist/BimAiDynamoAssistant.dll')
        $extensionHashEntry = $zip.GetEntry('ARQ-IA/dynamo/extension/dist/BimAiDynamoAssistant.dll.sha256')
        $dllStream = $extensionDllEntry.Open()
        $sha256 = [Security.Cryptography.SHA256]::Create()
        try { $packagedExtensionHash = [BitConverter]::ToString($sha256.ComputeHash($dllStream)).Replace('-', '').ToLowerInvariant() }
        finally { $dllStream.Dispose() }
        $sha256.Dispose()
        $hashStream = $extensionHashEntry.Open()
        $hashReader = New-Object System.IO.StreamReader($hashStream, [Text.Encoding]::UTF8, $true)
        try { $declaredExtensionHash = $hashReader.ReadToEnd().Trim().ToLowerInvariant() }
        finally { $hashReader.Dispose() }
        if ($declaredExtensionHash -notmatch '^[0-9a-f]{64}$' -or $declaredExtensionHash -ne $packagedExtensionHash) {
            throw 'El DLL de la extension Dynamo no coincide con su manifiesto SHA-256.'
        }

        $revitAssemblyEntry = $zip.GetEntry('ARQ-IA/revit/assistant/dist/BimAiRevitAssistant.dll')
        $revitHashEntry = $zip.GetEntry('ARQ-IA/revit/assistant/dist/BimAiRevitAssistant.sha256')
        $revitDllStream = $revitAssemblyEntry.Open()
        $revitSha256 = [Security.Cryptography.SHA256]::Create()
        try { $packagedRevitHash = [BitConverter]::ToString($revitSha256.ComputeHash($revitDllStream)).Replace('-', '').ToLowerInvariant() }
        finally { $revitDllStream.Dispose() }
        $revitSha256.Dispose()
        $revitHashStream = $revitHashEntry.Open()
        $revitHashReader = New-Object System.IO.StreamReader($revitHashStream, [Text.Encoding]::UTF8, $true)
        try { $declaredRevitHash = $revitHashReader.ReadToEnd().Trim().ToLowerInvariant() }
        finally { $revitHashReader.Dispose() }
        if ($declaredRevitHash -notmatch '^[0-9a-f]{64}$' -or $declaredRevitHash -ne $packagedRevitHash) {
            throw 'El DLL del add-in Revit no coincide con su manifiesto SHA-256.'
        }

        $bridgeDllEntry = $zip.GetEntry('ARQ-IA/revit/bridge/dist/BimAiDynamoBridge.dll')
        $bridgeHashEntry = $zip.GetEntry('ARQ-IA/revit/bridge/dist/BimAiDynamoBridge.sha256')
        $bridgeStream = $bridgeDllEntry.Open()
        $bridgeSha256 = [Security.Cryptography.SHA256]::Create()
        try { $packagedBridgeHash = [BitConverter]::ToString($bridgeSha256.ComputeHash($bridgeStream)).Replace('-', '').ToLowerInvariant() }
        finally { $bridgeStream.Dispose() }
        $bridgeSha256.Dispose()
        $bridgeHashStream = $bridgeHashEntry.Open()
        $bridgeHashReader = New-Object System.IO.StreamReader($bridgeHashStream, [Text.Encoding]::UTF8, $true)
        try { $declaredBridgeHash = $bridgeHashReader.ReadToEnd().Trim().ToLowerInvariant() }
        finally { $bridgeHashReader.Dispose() }
        if ($declaredBridgeHash -notmatch '^[0-9a-f]{64}$' -or $declaredBridgeHash -ne $packagedBridgeHash) {
            throw 'El DLL del bridge Revit-Dynamo no coincide con su manifiesto SHA-256.'
        }

        $tokenPatterns = @(
            '(?i)\b(?:gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}|sk-(?:proj-)?[A-Za-z0-9_-]{20,})\b',
            '(?i)\b(?:AKIA|ASIA)[0-9A-Z]{16}\b',
            '(?i)\bxox[baprs]-[A-Za-z0-9-]{10,}\b',
            '(?i)\bAIza[0-9A-Za-z_-]{35}\b',
            '(?im)-----BEGIN (?:RSA |EC |OPENSSH |DSA )?PRIVATE KEY-----'
        )
        $privatePathPattern = '(?i)\b[A-Z]:[\\/]+Users[\\/]+[^\\/:\s"'']+'
        foreach ($entry in $entries) {
            $name = $entry.FullName
            $extension = [IO.Path]::GetExtension($name).ToLowerInvariant()
            if ($extension -in @('.rvt', '.rfa', '.rte', '.ifc', '.nwc', '.nwd', '.dwg', '.dxf', '.pfx', '.p12', '.pem', '.key') -or
                $name -match '(?i)(^|/)(?:id_rsa|id_ed25519|credentials(?:\.[^/]*)?|secrets(?:\.[^/]*)?)$' -or
                $name -match '(?i)(^|/)revit/test-models/(?!README\.md$|prepare-test-model\.ps1$)' -or
                $name -match '(?i)(^|/)\.codex/' -or
                $name -match '(?i)(^|/)\.arq-ia/' -or
                $name -match '(?i)(^|/)\.env(\.(?!example$)[^/]+)?$' -or
                $name -match '(?i)(^|/)logs/(?!README\.md$)' -or
                $name -match '(?i)(^|/)artifacts/' -or
                $name -match '(?i)(^|/)vendor/[^/]+\.zip$') {
                throw "El paquete incluye un artefacto local no permitido: $name"
            }

            $stream = $entry.Open()
            $reader = New-Object System.IO.StreamReader($stream, [Text.Encoding]::UTF8, $true)
            try { $content = $reader.ReadToEnd() } finally { $reader.Dispose() }
            $matchedSecretPattern = $false
            foreach ($tokenPattern in $tokenPatterns) {
                if ($content -match $tokenPattern) { $matchedSecretPattern = $true; break }
            }
            if ($matchedSecretPattern) { throw "Se detecto un patron conocido de secreto en: $name" }
            if ($content -match $privatePathPattern) { throw "Se detecto una ruta privada local en: $name" }
        }

    } finally {
        $zip.Dispose()
    }

    Move-Item -LiteralPath $tempArchive -Destination $archivePath
    $hash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    Write-Output 'PACKAGE=PASS'
    Write-Output ("REVISION=" + $revision)
    Write-Output ("FILES=" + $entries.Count)
    Write-Output 'TARGETED_SECRET_PATTERNS_AND_LOCAL_ARTIFACTS=PASS'
    Write-Output ("ARCHIVE=" + $archivePath)
    Write-Output ("SHA256=" + $hash)
} finally {
    Remove-Item -LiteralPath $tempArchive -Force -ErrorAction SilentlyContinue
}
