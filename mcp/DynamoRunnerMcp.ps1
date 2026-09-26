#requires -Version 5.1
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[Console]::InputEncoding = $utf8NoBom
[Console]::OutputEncoding = $utf8NoBom

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$routinesRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'routines'))
$wrapperPath = [IO.Path]::GetFullPath((Join-Path $repoRoot 'dynamo\pythonnet3_result_wrapper.py'))
$pipeName = 'BIM-AI-POC-Revit-27'

function Write-JsonLine([object]$Value) {
    [Console]::Out.WriteLine((ConvertTo-Json -InputObject $Value -Depth 20 -Compress))
    [Console]::Out.Flush()
}

function New-ToolReply([object]$RequestId, [string]$Text, [bool]$IsError = $false) {
    Write-JsonLine ([ordered]@{
        jsonrpc = '2.0'
        id = $RequestId
        result = [ordered]@{ content = @([ordered]@{ type = 'text'; text = $Text }); isError = $IsError }
    })
}

function Get-ToolDefinitions {
    return @(
        [ordered]@{
            name = 'ping_revit_dynamo_bridge'
            description = 'Check whether the per-user Dynamo bridge is running inside Revit 2027. This makes no model changes.'
            inputSchema = [ordered]@{ type = 'object'; properties = [ordered]@{}; additionalProperties = $false }
        },
        [ordered]@{
            name = 'execute_saved_readonly_routine'
            description = 'Run only the exact hash-pinned read_active_view_selection PythonNet3 graph inside Revit 2027/Dynamo. It refuses dirty or modifiable documents, validates the .py/.dyn/manifest and verifies unchanged selection/view/document state. It cannot execute user-supplied code or write routines.'
            inputSchema = [ordered]@{
                type = 'object'
                properties = [ordered]@{
                    routine_id = [ordered]@{ type = 'string'; enum = @('read_active_view_selection'); description = 'The single allowlisted routine.' }
                }
                required = @('routine_id')
                additionalProperties = $false
            }
        },
        [ordered]@{
            name = 'execute_saved_routine'
            description = 'Execute one hash-validated, saved PythonNet3 Manual graph through Revit/Dynamo. Generic routines run only in a saved, idle, non-workshared RVT under revit/test-models. Every generic execution requires authorize_possible_model_writes=true because any PythonNet3 source may write the model regardless of its manifest. Review the source; PythonNet3 has Revit process user permissions and the model path gate is not an operating-system sandbox. If status is revit_execution_timeout_unknown, query the model before retrying. Tool approval is required.'
            inputSchema = [ordered]@{
                type = 'object'
                properties = [ordered]@{
                    routine_id = [ordered]@{ type = 'string'; pattern = '^[a-z][a-z0-9_-]{1,47}$'; description = 'The slug of a saved routine under routines/.' }
                    authorize_possible_model_writes = [ordered]@{ type = 'boolean'; description = 'Must be true for every generic PythonNet3 routine execution. The routine may write the model even if its manifest says read-only; review its source and explicitly authorize possible model changes.' }
                }
                required = @('routine_id', 'authorize_possible_model_writes')
                additionalProperties = $false
            }
        }
    )
}

function Get-Sha256Hex([string]$Path) {
    $stream = [IO.File]::OpenRead($Path)
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-', '').ToLowerInvariant() }
    finally { $stream.Dispose(); $sha.Dispose() }
}

function Assert-SavedRoutine([string]$RoutineId, [bool]$ConfirmModelWrite) {
    if ($RoutineId -notmatch '^[a-z][a-z0-9_-]{1,47}$') { throw 'routine_id_invalid' }
    $paths = @{
        py = [IO.Path]::GetFullPath((Join-Path $routinesRoot ($RoutineId + '.py')))
        dyn = [IO.Path]::GetFullPath((Join-Path $routinesRoot ($RoutineId + '.dyn')))
        manifest = [IO.Path]::GetFullPath((Join-Path $routinesRoot ($RoutineId + '.manifest.json')))
    }
    foreach ($path in $paths.Values) {
        if (-not $path.StartsWith($routinesRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'routine_path_invalid' }
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw 'routine_files_missing' }
        if (([IO.File]::GetAttributes($path) -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'routine_path_invalid' }
    }
    if ((Get-Item -LiteralPath $routinesRoot).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'routine_path_invalid' }
    if ((Get-Item -LiteralPath $wrapperPath).Length -gt 32768 -or (Get-Item -LiteralPath $paths.py).Length -gt 32768 -or
        (Get-Item -LiteralPath $paths.dyn).Length -gt 524288 -or (Get-Item -LiteralPath $paths.manifest).Length -gt 16384) { throw 'routine_package_too_large' }
    $manifest = Get-Content -LiteralPath $paths.manifest -Raw | ConvertFrom-Json
    $isWriter = [bool]$manifest.writes_model
    if (-not $ConfirmModelWrite) { throw 'generic_execution_confirmation_required' }
    if ([string]$manifest.name -ne $RoutineId -or [string]$manifest.python_engine -ne 'PythonNet3' -or
        [string]$manifest.result_contract -ne 'bim-ai-json-v1' -or
        [bool]$manifest.confirm_model_write -ne $isWriter) { throw 'routine_manifest_invalid' }
    if ((Get-Sha256Hex $paths.py) -ne ([string]$manifest.python_sha256).ToLowerInvariant() -or
        (Get-Sha256Hex $paths.dyn) -ne ([string]$manifest.dynamo_sha256).ToLowerInvariant() -or
        (Get-Sha256Hex $wrapperPath) -ne ([string]$manifest.runtime_wrapper_sha256).ToLowerInvariant()) { throw 'routine_integrity_check_failed' }
    $code = [IO.File]::ReadAllText($paths.py, $utf8NoBom)
    $wrapper = [IO.File]::ReadAllText($wrapperPath, $utf8NoBom).Replace("`r`n", "`n").Replace("`r", "`n")
    $expectedCode = $code.TrimEnd() + "`n`n" + $wrapper.TrimStart()
    $graph = Get-Content -LiteralPath $paths.dyn -Raw | ConvertFrom-Json
    if ($graph.Nodes.Count -ne 1 -or $graph.Nodes[0].Engine -ne 'PythonNet3' -or
        $graph.Nodes[0].Code -ne $expectedCode -or $graph.Connectors.Count -ne 0 -or
        $graph.Dependencies.Count -ne 0 -or $graph.View.Dynamo.RunType -ne 'Manual') { throw 'routine_graph_invalid' }
    return $manifest
}

function Invoke-RevitBridge([string]$Operation, [string]$RoutineId, [bool]$ConfirmModelWrite = $false) {
    $client = $null
    $reader = $null
    $writer = $null
    try {
        $client = New-Object System.IO.Pipes.NamedPipeClientStream('.', $pipeName, [System.IO.Pipes.PipeDirection]::InOut, [System.IO.Pipes.PipeOptions]::None)
        try { $client.Connect(2500) } catch [System.TimeoutException] { throw 'bridge_connect_timeout' }
        $reader = New-Object System.IO.StreamReader($client, $utf8NoBom, $false, 1024, $true)
        $writer = New-Object System.IO.StreamWriter($client, $utf8NoBom, 1024, $true)
        $writer.AutoFlush = $true
        $request = [ordered]@{ operation = $Operation }
        if ($RoutineId) { $request.routineId = $RoutineId }
        if ($Operation -eq 'run_saved_routine') { $request.confirmModelWrite = $ConfirmModelWrite }
        $writeTask = $writer.WriteLineAsync((ConvertTo-Json -InputObject $request -Depth 4 -Compress))
        if (-not $writeTask.Wait(5000)) { throw 'bridge_write_timeout' }
        $readTask = $reader.ReadLineAsync()
        if (-not $readTask.Wait(95000)) { throw 'bridge_response_timeout' }
        $line = $readTask.Result
        if ([string]::IsNullOrWhiteSpace($line) -or $line.Length -gt 65536) { throw 'Invalid bridge response.' }
        $response = $line | ConvertFrom-Json
        return ,$response
    } finally {
        if ($writer) { $writer.Dispose() }
        if ($reader) { $reader.Dispose() }
        if ($client) { $client.Dispose() }
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
                    result = [ordered]@{ protocolVersion = $version; capabilities = [ordered]@{ tools = [ordered]@{} }; serverInfo = [ordered]@{ name = 'bim-ai-dynamo-runner'; version = '0.1.0' } }
                })
            }
            'notifications/initialized' { }
            'ping' { Write-JsonLine ([ordered]@{ jsonrpc = '2.0'; id = $requestId; result = [ordered]@{} }) }
            'tools/list' { Write-JsonLine ([ordered]@{ jsonrpc = '2.0'; id = $requestId; result = [ordered]@{ tools = (Get-ToolDefinitions) } }) }
            'tools/call' {
                $toolName = [string]$request.params.name
                try {
                    switch ($toolName) {
                        'ping_revit_dynamo_bridge' {
                            $bridge = Invoke-RevitBridge -Operation 'ping' -RoutineId $null
                        }
                        'execute_saved_readonly_routine' {
                            $routineId = [string]$request.params.arguments.routine_id
                            if ($routineId -ne 'read_active_view_selection') { throw 'routine_not_allowed' }
                            $bridge = Invoke-RevitBridge -Operation 'run_saved_readonly_routine' -RoutineId $routineId
                        }
                        'execute_saved_routine' {
                            $routineId = [string]$request.params.arguments.routine_id
                            $confirmModelWrite = [bool]$request.params.arguments.authorize_possible_model_writes
                            $manifest = Assert-SavedRoutine -RoutineId $routineId -ConfirmModelWrite $confirmModelWrite
                            $manifestPath = Join-Path $routinesRoot ($routineId + '.manifest.json')
                            $bridge = Invoke-RevitBridge -Operation 'run_saved_routine' -RoutineId $routineId -ConfirmModelWrite $confirmModelWrite
                            if ($bridge.ok -and $bridge.status -in @('executed_and_verified_change', 'executed_and_verified_no_change', 'executed_and_verified_read_only')) {
                                if ($bridge.status -eq 'executed_and_verified_change' -or
                                    ($bridge.status -eq 'executed_and_verified_read_only' -and -not [bool]$manifest.writes_model)) {
                                    $manifest.status = 'validated'
                                    $manifest.execution = 'mcp_runner_verified_in_test_model'
                                }
                                $manifest | Add-Member -MemberType NoteProperty -Name last_validated_utc -Value ([DateTime]::UtcNow.ToString('o')) -Force
                                $manifest | Add-Member -MemberType NoteProperty -Name last_validation_status -Value ([string]$bridge.status) -Force
                                $tempManifest = $manifestPath + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
                                try {
                                    [IO.File]::WriteAllText($tempManifest, (ConvertTo-Json -InputObject $manifest -Depth 20), $utf8NoBom)
                                    Move-Item -LiteralPath $tempManifest -Destination $manifestPath -Force
                                } finally {
                                    Remove-Item -LiteralPath $tempManifest -Force -ErrorAction SilentlyContinue
                                }
                            }
                        }
                        default { throw 'tool_not_allowed' }
                    }
                    if (-not $bridge.ok) { throw ([string]$bridge.status) }
                    New-ToolReply -RequestId $requestId -Text (ConvertTo-Json -InputObject $bridge -Depth 10 -Compress)
                } catch {
                    $status = 'bridge_unavailable_or_rejected'
                    if ($_.Exception.Message -match '^[a-z0-9_]{1,80}$') { $status = $_.Exception.Message }
                    New-ToolReply -RequestId $requestId -Text (@{ ok = $false; status = $status } | ConvertTo-Json -Compress) -IsError $true
                }
            }
            default {
                if ($null -ne $requestId) {
                    Write-JsonLine ([ordered]@{ jsonrpc = '2.0'; id = $requestId; error = [ordered]@{ code = -32601; message = 'Method not supported.' } })
                }
            }
        }
    } catch {
        if ($null -ne $requestId) {
            Write-JsonLine ([ordered]@{ jsonrpc = '2.0'; id = $requestId; error = [ordered]@{ code = -32700; message = 'Invalid JSON-RPC request.' } })
        }
    }
}
