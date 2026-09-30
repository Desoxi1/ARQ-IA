param(
    [string]$DynamoRoot = 'C:\Program Files\Autodesk\Revit 2027\AddIns\DynamoForRevit'
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$DynamoRoot = [IO.Path]::GetFullPath($DynamoRoot)
$extensionDll = Join-Path $repoRoot 'artifacts\dynamo-extension\BimAiDynamoAssistant.dll'
$smokeProject = Join-Path $PSScriptRoot 'dynamo_extension_client_smoke\DynamoExtensionClientSmoke.csproj'
$localSdkDirectory = Join-Path $env:LOCALAPPDATA 'BIM-AI-POC\dotnet-sdk'
$localDotnet = Join-Path $localSdkDirectory 'dotnet.exe'

if (-not (Test-Path -LiteralPath $extensionDll -PathType Leaf)) {
    throw 'Build the Dynamo extension first with dynamo\extension\build.ps1.'
}
if (-not (Test-Path -LiteralPath (Join-Path $DynamoRoot 'DynamoCoreWpf.dll') -PathType Leaf)) {
    throw 'DynamoCoreWpf.dll was not found. Pass the Dynamo 4.x installation directory.'
}

if (Test-Path -LiteralPath $localDotnet -PathType Leaf) {
    $dotnet = $localDotnet
    $env:DOTNET_ROOT = $localSdkDirectory
    $env:PATH = "$localSdkDirectory;$env:PATH"
}
else {
    $dotnet = (Get-Command dotnet.exe -ErrorAction Stop).Source
}

& $dotnet run --project $smokeProject --configuration Release -- $extensionDll $DynamoRoot $repoRoot
if ($LASTEXITCODE -ne 0) { throw 'Dynamo extension Codex client smoke failed.' }
