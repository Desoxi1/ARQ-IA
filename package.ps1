#requires -Version 5.1
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts'))
$prefix = 'BIM-AI-POC/'

if (-not (Test-Path -LiteralPath (Join-Path $projectRoot '.git') -PathType Container)) {
    throw 'Ejecuta este empaquetador desde un checkout Git del proyecto.'
}
$status = @(& git -C $projectRoot status --porcelain)
if ($LASTEXITCODE -ne 0) { throw 'No se pudo consultar el estado de Git.' }
if ($status.Count -gt 0) { throw 'Guarda los cambios en un commit antes de empaquetar.' }

$revision = (& git -C $projectRoot rev-parse --short HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $revision -notmatch '^[0-9a-f]{7,40}$') { throw 'No se pudo resolver el commit actual.' }
New-Item -ItemType Directory -Force -Path $artifactsRoot | Out-Null
$archivePath = Join-Path $artifactsRoot ('BIM-AI-POC-' + $revision + '.zip')
$tempArchive = Join-Path $artifactsRoot ('.BIM-AI-POC-' + [Guid]::NewGuid().ToString('N') + '.tmp.zip')
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
            'BIM-AI-POC/README.md',
            'BIM-AI-POC/install.ps1',
            'BIM-AI-POC/setup.ps1',
            'BIM-AI-POC/doctor.ps1',
            'BIM-AI-POC/.codex/config.toml.example',
            'BIM-AI-POC/dynamo/extension/README.md',
            'BIM-AI-POC/dynamo/extension/install.ps1',
            'BIM-AI-POC/dynamo/extension/uninstall_legacy_programfiles.ps1',
            'BIM-AI-POC/dynamo/extension/dist/BimAiDynamoAssistant.dll',
            'BIM-AI-POC/dynamo/extension/dist/BimAiDynamoAssistant.dll.sha256',
            'BIM-AI-POC/dynamo/extension/dist/pkg.json',
            'BIM-AI-POC/dynamo/extension/dist/viewExtensions/BimAiDynamoAssistant_ViewExtensionDefinition.xml',
            'BIM-AI-POC/revit/assistant/BimAiRevitAssistant.csproj',
            'BIM-AI-POC/revit/assistant/README.md',
            'BIM-AI-POC/revit/assistant/build.ps1',
            'BIM-AI-POC/revit/assistant/install.ps1',
            'BIM-AI-POC/revit/assistant/dist/BimAiRevitAssistant.dll',
            'BIM-AI-POC/revit/assistant/dist/BimAiRevitAssistant.sha256'
        )
        $entryNames = @($entries | ForEach-Object { $_.FullName })
        foreach ($requiredFile in $requiredFiles) {
            if ($requiredFile -notin $entryNames) { throw "Falta un archivo requerido: $requiredFile" }
        }

        $extensionDllEntry = $zip.GetEntry('BIM-AI-POC/dynamo/extension/dist/BimAiDynamoAssistant.dll')
        $extensionHashEntry = $zip.GetEntry('BIM-AI-POC/dynamo/extension/dist/BimAiDynamoAssistant.dll.sha256')
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

        $revitAssemblyEntry = $zip.GetEntry('BIM-AI-POC/revit/assistant/dist/BimAiRevitAssistant.dll')
        $revitHashEntry = $zip.GetEntry('BIM-AI-POC/revit/assistant/dist/BimAiRevitAssistant.sha256')
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

        $tokenPattern = '(?i)\b(?:gh[pousr]_[A-Za-z0-9]{20,}|sk-[A-Za-z0-9]{20,})\b'
        $privatePathPattern = '(?i)[A-Z]:[\\/]+Users[\\/]+Branko(?:[\\/]|$)'
        foreach ($entry in $entries) {
            $name = $entry.FullName
            $extension = [IO.Path]::GetExtension($name).ToLowerInvariant()
            if ($extension -in @('.rvt', '.rfa', '.rte') -or
                $name -eq 'BIM-AI-POC/.codex/config.toml' -or
                $name -match '(?i)(^|/)\.env(\.(?!example$)[^/]+)?$' -or
                $name -match '(?i)(^|/)logs/(runner-results|attachments)/') {
                throw "El paquete incluye un artefacto local no permitido: $name"
            }

            $stream = $entry.Open()
            $reader = New-Object System.IO.StreamReader($stream, [Text.Encoding]::UTF8, $true)
            try { $content = $reader.ReadToEnd() } finally { $reader.Dispose() }
            if ($content -match $tokenPattern) { throw "Se detecto un patron de token en: $name" }
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
    Write-Output 'SECRETS_AND_LOCAL_ARTIFACTS=PASS'
    Write-Output ("ARCHIVE=" + $archivePath)
    Write-Output ("SHA256=" + $hash)
} finally {
    Remove-Item -LiteralPath $tempArchive -Force -ErrorAction SilentlyContinue
}
