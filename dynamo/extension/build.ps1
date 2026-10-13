param(
    [string]$DynamoRoot = 'C:\Program Files\Autodesk\Revit 2027\AddIns\DynamoForRevit',
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\..\artifacts\dynamo-extension')
)

$ErrorActionPreference = 'Stop'
$DynamoRoot = [IO.Path]::GetFullPath($DynamoRoot)
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$distributionDirectory = Join-Path $PSScriptRoot 'dist'
$localSdkDirectory = Join-Path $env:LOCALAPPDATA 'BIM-AI-POC\dotnet-sdk'
$localDotnet = Join-Path $localSdkDirectory 'dotnet.exe'

if (Test-Path -LiteralPath $localDotnet -PathType Leaf) {
    $dotnet = $localDotnet
    $env:DOTNET_ROOT = $localSdkDirectory
    $env:PATH = "$localSdkDirectory;$env:PATH"
}
else {
    $dotnetCommand = Get-Command dotnet.exe -ErrorAction Stop
    $dotnet = $dotnetCommand.Source
}

foreach ($assembly in @('DynamoCore.dll', 'DynamoCoreWpf.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $DynamoRoot $assembly) -PathType Leaf)) {
        throw "Dynamo 4.x assembly not found: $assembly under $DynamoRoot"
    }
}

$null = New-Item -ItemType Directory -Force -Path $OutputDirectory
& $dotnet build (Join-Path $PSScriptRoot 'BimAiDynamoAssistant.csproj') `
    --configuration Release `
    --output $OutputDirectory `
    -p:DynamoRoot="$DynamoRoot"
if ($LASTEXITCODE -ne 0) { throw 'Dynamo extension build failed.' }

$viewExtensionsDirectory = Join-Path $OutputDirectory 'viewExtensions'
$null = New-Item -ItemType Directory -Force -Path $viewExtensionsDirectory
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'BimAiDynamoAssistant_ViewExtensionDefinition.xml') `
    -Destination $viewExtensionsDirectory -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'pkg.json') `
    -Destination (Join-Path $OutputDirectory 'pkg.json') -Force

$distributionViewExtensions = Join-Path $distributionDirectory 'viewExtensions'
$null = New-Item -ItemType Directory -Force -Path $distributionViewExtensions
Copy-Item -LiteralPath (Join-Path $OutputDirectory 'BimAiDynamoAssistant.dll') `
    -Destination (Join-Path $distributionDirectory 'BimAiDynamoAssistant.dll') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'BimAiDynamoAssistant_ViewExtensionDefinition.xml') `
    -Destination $distributionViewExtensions -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'pkg.json') `
    -Destination (Join-Path $distributionDirectory 'pkg.json') -Force
$dllHash = (Get-FileHash -LiteralPath (Join-Path $distributionDirectory 'BimAiDynamoAssistant.dll') -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText(
    (Join-Path $distributionDirectory 'BimAiDynamoAssistant.dll.sha256'),
    $dllHash,
    (New-Object System.Text.UTF8Encoding($false)))
Write-Host "DYNAMO_EXTENSION_BUILD=PASS"
Write-Host "OUTPUT=$OutputDirectory"
Write-Host "DISTRIBUTION=$distributionDirectory"
Write-Host "SHA256=$dllHash"
