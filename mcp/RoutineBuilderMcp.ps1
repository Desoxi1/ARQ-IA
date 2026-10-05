#requires -Version 5.1
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[Console]::InputEncoding = $utf8NoBom
[Console]::OutputEncoding = $utf8NoBom

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$routinesRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'routines'))
$templatePath = [IO.Path]::GetFullPath((Join-Path $repoRoot 'dynamo\pythonnet3_template.dyn'))
$resultWrapperPath = [IO.Path]::GetFullPath((Join-Path $repoRoot 'dynamo\pythonnet3_result_wrapper.py'))

function Write-JsonLine([object]$Value) {
    $json = ConvertTo-Json -InputObject $Value -Depth 80 -Compress
    [Console]::Out.WriteLine($json)
    [Console]::Out.Flush()
}

function New-ToolReply([object]$RequestId, [string]$Text, [bool]$IsError = $false) {
    $content = @([ordered]@{ type = 'text'; text = $Text })
    $result = [ordered]@{ content = $content; isError = $IsError }
    Write-JsonLine ([ordered]@{ jsonrpc = '2.0'; id = $RequestId; result = $result })
}

function New-JsonRpcError([object]$RequestId, [int]$Code, [string]$Message) {
    Write-JsonLine ([ordered]@{ jsonrpc = '2.0'; id = $RequestId; error = [ordered]@{ code = $Code; message = $Message } })
}

function Get-ToolDefinitions {
    $emptyProperties = [ordered]@{}
    return @(
        [ordered]@{
            name = 'list_saved_routines'
            description = 'List Dynamo PythonNet3 routines saved in this ARQ-IA folder. This does not connect to or change Revit.'
            inputSchema = [ordered]@{ type = 'object'; properties = $emptyProperties; additionalProperties = $false }
        },
        [ordered]@{
            name = 'create_dynamo_routine'
            description = 'Save a PythonNet3 script and Manual-run Dynamo graph under routines/. Saving does not execute it. The result wrapper is appended. Model-writing routines can run only through the approved runner on a disposable local RVT under revit/test-models; writes_model and confirm_model_write must both be true.'
            inputSchema = [ordered]@{
                type = 'object'
                properties = [ordered]@{
                    name = [ordered]@{ type = 'string'; description = 'Lowercase routine slug, 2–48 characters: letters, digits, underscore, hyphen.' }
                    description = [ordered]@{ type = 'string'; description = 'What the routine does and its expected result.' }
                    python_code = [ordered]@{ type = 'string'; description = 'Complete PythonNet3 script for the Dynamo Python node. Assign the result to OUT.' }
                    scope = [ordered]@{ type = 'string'; enum = @('active_selection', 'active_view', 'document'); description = 'Revit context the script is intended to inspect or modify.' }
                    writes_model = [ordered]@{ type = 'boolean'; description = 'True if the script can modify the Revit model.' }
                    confirm_model_write = [ordered]@{ type = 'boolean'; description = 'Must be true only when the user explicitly requested a model-writing routine.' }
                }
                required = @('name', 'description', 'python_code', 'scope', 'writes_model', 'confirm_model_write')
                additionalProperties = $false
            }
        }
    )
}

function Get-SavedRoutines {
    $items = @()
    Get-ChildItem -LiteralPath $routinesRoot -Filter '*.manifest.json' -File | Sort-Object Name | ForEach-Object {
        try {
            $manifest = Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json
            $baseName = $_.Name.Substring(0, $_.Name.Length - '.manifest.json'.Length)
            $items += [ordered]@{
                name = [string]$manifest.name
                description = [string]$manifest.description
                scope = [string]$manifest.scope
                writes_model = [bool]$manifest.writes_model
                status = [string]$manifest.status
                last_validated_utc = [string]$manifest.last_validated_utc
                last_validation_status = [string]$manifest.last_validation_status
                python_file = ($baseName + '.py')
                dynamo_file = ($baseName + '.dyn')
                created_utc = [string]$manifest.created_utc
            }
        } catch {
            $items += [ordered]@{ manifest = $_.Name; status = 'invalid_manifest' }
        }
    }
    return [ordered]@{ count = $items.Count; routines = $items }
}

function Write-Utf8NoBom([string]$Path, [string]$Content) {
    $encoding = New-Object System.Text.UTF8Encoding($false)
    $normalizedContent = $Content.Replace("`r`n", "`n").Replace("`r", "`n")
    [IO.File]::WriteAllText($Path, $normalizedContent, $encoding)
}

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

function Save-DynamoRoutine([object]$Payload) {
    $name = [string]$Payload.name
    $description = ([string]$Payload.description).Trim()
    $code = ([string]$Payload.python_code).Replace("`r`n", "`n").Replace("`r", "`n")
    $wrapper = (Get-Content -LiteralPath $resultWrapperPath -Raw).Replace("`r`n", "`n").Replace("`r", "`n")
    $runtimeCode = $code.TrimEnd() + "`n`n" + $wrapper.TrimStart()
    $scope = [string]$Payload.scope
    $writesModel = [bool]$Payload.writes_model
    $confirmModelWrite = [bool]$Payload.confirm_model_write

    if ($name -notmatch '^[a-z][a-z0-9_-]{1,47}$') { throw 'name debe cumplir ^[a-z][a-z0-9_-]{1,47}$.' }
    if ([string]::IsNullOrWhiteSpace($description) -or $description.Length -gt 500) { throw 'description debe tener entre 1 y 500 caracteres.' }
    if ([string]::IsNullOrWhiteSpace($code) -or [Text.Encoding]::UTF8.GetByteCount($code) -gt 32768) { throw 'python_code es obligatorio y no puede superar 32 KiB UTF-8.' }
    if ($scope -notin @('active_selection', 'active_view', 'document')) { throw 'scope debe ser active_selection, active_view o document.' }
    if ($writesModel -and -not $confirmModelWrite) { throw 'La rutina modifica el modelo. Requiere confirm_model_write=true porque el usuario debe haber pedido explícitamente ese efecto.' }
    if (-not $writesModel -and $confirmModelWrite) { throw 'confirm_model_write debe ser false para una rutina declarada de solo lectura.' }

    foreach ($suffix in @('.py', '.dyn', '.manifest.json')) {
        if (Test-Path -LiteralPath (Join-Path $routinesRoot ($name + $suffix))) { throw "Ya existe $name$suffix. Elige otro name para no sobrescribir una rutina guardada." }
    }
    if (-not (Test-Path -LiteralPath $templatePath -PathType Leaf)) { throw 'No existe dynamo/pythonnet3_template.dyn; ejecuta setup.ps1 o restaura la plantilla.' }
    if (-not (Test-Path -LiteralPath $resultWrapperPath -PathType Leaf)) { throw 'No existe dynamo/pythonnet3_result_wrapper.py; restaura el wrapper de resultados.' }

    if ($code -notmatch '(?m)^\s*OUT\s*=') { throw 'El script debe asignar el resultado del nodo Dynamo a OUT.' }

    $graph = Get-Content -LiteralPath $templatePath -Raw | ConvertFrom-Json
    if ($graph.Nodes.Count -ne 1 -or $graph.Nodes[0].Engine -ne 'PythonNet3') { throw 'La plantilla Dynamo no cumple el formato esperado de un único nodo PythonNet3.' }
    $node = $graph.Nodes[0]
    $nodeId = [Guid]::NewGuid().ToString('N')
    $node.Id = $nodeId
    $node.Code = $runtimeCode
    $node.Description = $description
    $node.Engine = 'PythonNet3'
    $node.EngineName = 'PythonNet3'
    $node.VariableInputPorts = $false
    $node.Inputs = [object[]]@()
    $node.Outputs[0].Id = [Guid]::NewGuid().ToString('N')
    $graph.Uuid = [Guid]::NewGuid().ToString()
    $graph.Name = $name
    $graph.Description = $description
    $graph.Inputs = [object[]]@()
    $graph.Outputs = [object[]]@()
    $graph.Connectors = [object[]]@()
    $graph.Dependencies = [object[]]@()
    $graph.View.Dynamo.Version = '4.0.2.3852'
    $graph.View.Dynamo.RunType = 'Manual'
    $graph.View.Dynamo.HasRunWithoutCrash = $false
    $graph.View.NodeViews = @([ordered]@{
        Id = $nodeId
        Name = 'Python Script'
        IsSetAsInput = $false
        IsSetAsOutput = $false
        Excluded = $false
        ShowGeometry = $false
        X = 300.0
        Y = 250.0
    })

    $pyPath = Join-Path $routinesRoot ($name + '.py')
    $dynPath = Join-Path $routinesRoot ($name + '.dyn')
    $manifestPath = Join-Path $routinesRoot ($name + '.manifest.json')
    $tempToken = [Guid]::NewGuid().ToString('N')
    $tempPy = $pyPath + '.' + $tempToken + '.tmp'
    $tempSource = $pyPath + '.source.' + $tempToken + '.tmp'
    $tempDyn = $dynPath + '.' + $tempToken + '.tmp'
    $tempManifest = $manifestPath + '.' + $tempToken + '.tmp'
    $created = New-Object 'System.Collections.Generic.List[string]'

    try {
        Write-Utf8NoBom -Path $tempPy -Content $code
        Write-Utf8NoBom -Path $tempSource -Content $code
        Write-Utf8NoBom -Path $tempDyn -Content (ConvertTo-Json -InputObject $graph -Depth 80)
        $manifest = [ordered]@{
            schema_version = 1
            name = $name
            description = $description
            scope = $scope
            writes_model = $writesModel
            confirm_model_write = $confirmModelWrite
            status = 'draft_not_validated'
            execution = 'mcp_runner_test_model_only'
            revit_version = '2027'
            dynamo_version = '4.0.2.3852'
            python_engine = 'PythonNet3'
            result_contract = 'bim-ai-json-v1'
            runtime_wrapper_sha256 = Get-Sha256Hex $resultWrapperPath
            source_sha256 = Get-Sha256Hex $tempSource
            created_utc = [DateTime]::UtcNow.ToString('o')
            python_sha256 = Get-Sha256Hex $tempPy
            dynamo_sha256 = Get-Sha256Hex $tempDyn
        }
        Write-Utf8NoBom -Path $tempManifest -Content (ConvertTo-Json -InputObject $manifest -Depth 20)
        Move-Item -LiteralPath $tempPy -Destination $pyPath
        $created.Add($pyPath)
        Move-Item -LiteralPath $tempDyn -Destination $dynPath
        $created.Add($dynPath)
        Move-Item -LiteralPath $tempManifest -Destination $manifestPath
        $created.Add($manifestPath)
    } catch {
        foreach ($file in $created) { Remove-Item -LiteralPath $file -Force -ErrorAction SilentlyContinue }
        throw
    } finally {
        foreach ($file in @($tempPy, $tempSource, $tempDyn, $tempManifest)) { Remove-Item -LiteralPath $file -Force -ErrorAction SilentlyContinue }
    }

    return [ordered]@{
        saved = $true
        name = $name
        files = @("routines/$name.py", "routines/$name.dyn", "routines/$name.manifest.json")
        writes_model = $writesModel
        status = 'draft_not_validated'
        next_step = 'Review the complete Python source including the result wrapper. The graph is Manual-run; execute_saved_routine is restricted to a disposable RVT under revit/test-models and requests approval.'
    }
}

while ($null -ne ($line = [Console]::In.ReadLine())) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    $requestId = $null
    try {
        $request = $line | ConvertFrom-Json
        $requestId = $request.id
        $method = [string]$request.method
        switch ($method) {
            'initialize' {
                $version = [string]$request.params.protocolVersion
                if ([string]::IsNullOrWhiteSpace($version)) { $version = '2025-03-26' }
                Write-JsonLine ([ordered]@{
                    jsonrpc = '2.0'; id = $requestId
                    result = [ordered]@{ protocolVersion = $version; capabilities = [ordered]@{ tools = [ordered]@{} }; serverInfo = [ordered]@{ name = 'bim-ai-routine-builder'; version = '0.1.0' } }
                })
            }
            'notifications/initialized' { }
            'ping' { Write-JsonLine ([ordered]@{ jsonrpc = '2.0'; id = $requestId; result = [ordered]@{} }) }
            'tools/list' { Write-JsonLine ([ordered]@{ jsonrpc = '2.0'; id = $requestId; result = [ordered]@{ tools = (Get-ToolDefinitions) } }) }
            'tools/call' {
                $toolName = [string]$request.params.name
                try {
                    switch ($toolName) {
                        'list_saved_routines' { $payload = Get-SavedRoutines }
                        'create_dynamo_routine' { $payload = Save-DynamoRoutine -Payload $request.params.arguments }
                        default { throw "Herramienta desconocida: $toolName" }
                    }
                    New-ToolReply -RequestId $requestId -Text (ConvertTo-Json -InputObject $payload -Depth 50 -Compress)
                } catch {
                    New-ToolReply -RequestId $requestId -Text $_.Exception.Message -IsError $true
                }
            }
            default {
                if ($null -ne $requestId) { New-JsonRpcError -RequestId $requestId -Code -32601 -Message "Método MCP no soportado: $method" }
            }
        }
    } catch {
        if ($null -ne $requestId) { New-JsonRpcError -RequestId $requestId -Code -32700 -Message 'JSON-RPC parse error.' }
    }
}
