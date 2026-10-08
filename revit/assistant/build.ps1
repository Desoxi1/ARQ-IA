#requires -Version 5.1
[CmdletBinding()]
param(
    [string]$RevitRoot = (Join-Path $env:ProgramFiles 'Autodesk\Revit 2027'),
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$projectPath = Join-Path $PSScriptRoot 'BimAiRevitAssistant.csproj'
$sourcePath = Join-Path $PSScriptRoot 'BimAiRevitAssistant.cs'
$localSdkDirectory = Join-Path $env:LOCALAPPDATA 'BIM-AI-POC\dotnet-sdk'
$localDotnet = Join-Path $localSdkDirectory 'dotnet.exe'

if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repoRoot 'artifacts\revit-assistant' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)

foreach ($required in @(
    $projectPath,
    $sourcePath,
    (Join-Path $RevitRoot 'RevitAPI.dll'),
    (Join-Path $RevitRoot 'RevitAPIUI.dll'),
    (Join-Path $repoRoot 'dynamo\extension\AssistantPane.cs'),
    (Join-Path $repoRoot 'dynamo\extension\CodexAppServerClient.cs'),
    (Join-Path $repoRoot 'dynamo\extension\CodexModelSelection.cs'),
    (Join-Path $repoRoot 'dynamo\extension\LocalAttachmentStore.cs'),
    (Join-Path $repoRoot 'dynamo\extension\ProjectSettings.cs'),
    (Join-Path $repoRoot 'dynamo\extension\TaskRouter.cs')
)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw "No se encontró un archivo requerido: $required" }
}

if (Test-Path -LiteralPath $localDotnet -PathType Leaf) {
    $dotnet = $localDotnet
    $env:DOTNET_ROOT = $localSdkDirectory
    $env:PATH = "$localSdkDirectory;$env:PATH"
} else {
    $dotnetCommand = Get-Command dotnet.exe -ErrorAction Stop
    $dotnet = $dotnetCommand.Source
}

$null = New-Item -ItemType Directory -Force -Path $OutputDirectory
& $dotnet build $projectPath `
    --configuration Release `
    --output $OutputDirectory `
    "-p:RevitRoot=$RevitRoot"
if ($LASTEXITCODE -ne 0) { throw "Falló la compilación del add-in de Revit (código $LASTEXITCODE)." }

$assemblyPath = Join-Path $OutputDirectory 'BimAiRevitAssistant.dll'
if (-not (Test-Path -LiteralPath $assemblyPath -PathType Leaf)) { throw 'La compilación no produjo BimAiRevitAssistant.dll.' }
foreach ($forbiddenAssembly in @('DynamoCore.dll', 'DynamoCoreWpf.dll', 'RevitAPI.dll', 'RevitAPIUI.dll')) {
    if (Test-Path -LiteralPath (Join-Path $OutputDirectory $forbiddenAssembly)) {
        throw "La salida contiene una dependencia host que no debe distribuirse: $forbiddenAssembly"
    }
}

$distributionDirectory = Join-Path $PSScriptRoot 'dist'
$null = New-Item -ItemType Directory -Force -Path $distributionDirectory
$distributionDll = Join-Path $distributionDirectory 'BimAiRevitAssistant.dll'
Copy-Item -LiteralPath $assemblyPath -Destination $distributionDll -Force
$dllHash = (Get-FileHash -LiteralPath $distributionDll -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText(
    (Join-Path $distributionDirectory 'BimAiRevitAssistant.sha256'),
    $dllHash + [Environment]::NewLine,
    (New-Object System.Text.UTF8Encoding($false)))

Write-Output 'REVIT_ASSISTANT_BUILD=PASS'
Write-Output 'SHARED_ASSISTANT_UI=PASS'
Write-Output 'DYNAMO_ASSEMBLY_DEPENDENCIES=NONE'
Write-Output ("OUTPUT=" + $OutputDirectory)
Write-Output ("DISTRIBUTION=" + $distributionDirectory)
Write-Output ("SHA256=" + $dllHash)
