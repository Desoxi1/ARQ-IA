#requires -Version 5.1
[CmdletBinding()]
param(
    [string]$RevitRoot = (Join-Path $env:ProgramFiles 'Autodesk\Revit 2027'),
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$sourcePath = Join-Path $PSScriptRoot 'BimAiDynamoBridge.cs'
$dynamoRoot = Join-Path $RevitRoot 'AddIns\DynamoForRevit\Revit'
$runtimeBase = Join-Path $env:ProgramFiles 'dotnet\shared\Microsoft.NETCore.App'

if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repoRoot 'artifacts\bridge-build' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)

foreach ($required in @(
    $sourcePath,
    (Join-Path $RevitRoot 'RevitAPI.dll'),
    (Join-Path $RevitRoot 'RevitAPIUI.dll'),
    (Join-Path $dynamoRoot 'DynamoRevitDS.dll')
)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw "No se encontró una referencia requerida: $required" }
}

$runtime = Get-ChildItem -LiteralPath $runtimeBase -Directory -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -match '^10\.0\.\d+$' } |
    Sort-Object { [version]$_.Name } -Descending |
    Select-Object -First 1
if (-not $runtime) { throw 'Se requiere .NET Runtime 10 para compilar el add-in de Revit 2027.' }

$compilerCandidates = New-Object 'System.Collections.Generic.List[string]'
$vswhere = Join-Path ([Environment]::GetFolderPath('ProgramFilesX86')) 'Microsoft Visual Studio\Installer\vswhere.exe'
if (Test-Path -LiteralPath $vswhere -PathType Leaf) {
    $installations = & $vswhere -products '*' -property installationPath 2>$null
    foreach ($installation in $installations) {
        $compilerCandidates.Add((Join-Path $installation 'MSBuild\Current\Bin\Roslyn\csc.exe'))
    }
}
$compilerCandidates.Add((Join-Path ([Environment]::GetFolderPath('ProgramFilesX86')) 'Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe'))
$compilerCandidates.Add((Join-Path ([Environment]::GetFolderPath('ProgramFilesX86')) 'Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe'))
$compiler = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
if (-not $compiler) { throw 'No se encontró Roslyn csc.exe. Instala Visual Studio Build Tools o usa un SDK .NET 10.' }

$referenceNames = @(
    'System.Private.CoreLib.dll', 'System.Runtime.dll', 'System.Console.dll',
    'System.Collections.dll', 'System.Collections.Concurrent.dll', 'System.IO.FileSystem.dll',
    'System.IO.Pipes.dll', 'System.Security.Cryptography.dll', 'System.Text.Json.dll',
    'System.Text.Encoding.Extensions.dll', 'System.Threading.dll', 'System.Threading.Tasks.dll',
    'System.Runtime.Extensions.dll', 'System.Linq.dll', 'System.Memory.dll'
)
$references = New-Object 'System.Collections.Generic.List[string]'
foreach ($name in $referenceNames) {
    $path = Join-Path $runtime.FullName $name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Falta una referencia de .NET: $name" }
    $references.Add($path)
}
$references.Add((Join-Path $RevitRoot 'RevitAPI.dll'))
$references.Add((Join-Path $RevitRoot 'RevitAPIUI.dll'))
$references.Add((Join-Path $dynamoRoot 'DynamoRevitDS.dll'))

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$assemblyPath = Join-Path $OutputDirectory 'BimAiDynamoBridge.dll'
$arguments = @('/nologo', '/noconfig', '/nostdlib+', '/deterministic+', '/langversion:latest', '/target:library', "/out:$assemblyPath")
foreach ($reference in $references) { $arguments += "/reference:$reference" }
$arguments += $sourcePath
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw "Falló la compilación del puente (código $LASTEXITCODE)." }

Write-Output 'BRIDGE_BUILD=PASS'
Write-Output ("ASSEMBLY_BYTES=" + (Get-Item -LiteralPath $assemblyPath).Length)
