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
            name = 'open_dynamo_ui'
            description = 'Open the official Dynamo window in Revit for the active saved, idle, unmodified disposable RVT under revit/test-models. Uses Revit PostableCommand.Dynamo and does not run a graph or routine. Dynamo preview initialization can touch the active document; use only the disposable test model and close without saving if Revit marks it modified.'
            inputSchema = [ordered]@{ type = 'object'; properties = [ordered]@{}; additionalProperties = $false }
        },
        [ordered]@{
            name = 'ping_revit_dynamo_bridge'
            description = 'Check whether the per-user bridge is running inside Revit 2027 and whether the Dynamo UI is open on the active document. This makes no model changes.'
            inputSchema = [ordered]@{ type = 'object'; properties = [ordered]@{}; additionalProperties = $false }
        },
        [ordered]@{
            name = 'prepare_saved_routine_in_dynamo'
            description = 'Request Dynamo to load one hash-validated ARQ-IA PythonNet3 graph for the currently active Revit document, including a working project, with Manual mode and no execution requested. This tool does not call the routine executor or save the RVT. It refuses to replace a Dynamo workspace containing nodes or notes and refuses a session bound to another document. Dynamo startup may mark the RVT modified. The response confirms the loaded path, not runtime proof that this installed Dynamo build did not execute a node while opening; that behavior remains unverified until a live check on the installed version. Use only after the user asks to prepare a routine for review in the already-open model.'
            inputSchema = [ordered]@{
                type = 'object'
                properties = [ordered]@{
                    routine_id = [ordered]@{ type = 'string'; pattern = '^[a-z][a-z0-9_-]{1,47}$'; description = 'The slug returned by create_dynamo_routine.' }
                }
                required = @('routine_id')
                additionalProperties = $false
            }
        },
        [ordered]@{
            name = 'execute_saved_readonly_routine'
            description = 'Run only the exact hash-pinned read_active_view_selection PythonNet3 graph inside Revit 2027/Dynamo. Dynamo UI must already be open and bound to the active document; the bridge will not initialize Dynamo silently. It refuses dirty or modifiable documents, validates the .py/.dyn/manifest and verifies unchanged selection/view/document state. It cannot execute user-supplied code or write routines.'
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
            description = 'Execute one hash-validated, saved PythonNet3 Manual graph through Revit/Dynamo. Dynamo UI must already be open and bound to the active document; the bridge will not initialize Dynamo silently. Generic routines run only in a saved, idle, non-workshared RVT under revit/test-models. Every generic execution requires authorize_possible_model_writes=true because any PythonNet3 source may write the model regardless of its manifest. Review the source; PythonNet3 has Revit process user permissions and the model path gate is not an operating-system sandbox. If status is revit_execution_timeout_unknown, query the model before retrying. Tool approval is required.'
            inputSchema = [ordered]@{
                type = 'object'
                properties = [ordered]@{
                    routine_id = [ordered]@{ type = 'string'; pattern = '^[a-z][a-z0-9_-]{1,47}$'; description = 'The slug of a saved routine under routines/.' }
                    authorize_possible_model_writes = [ordered]@{ type = 'boolean'; description = 'Must be true for every generic PythonNet3 routine execution. The routine may write the model even if its manifest says read-only; review its source and explicitly authorize possible model changes.' }
                }
                required = @('routine_id', 'authorize_possible_model_writes')
                additionalProperties = $false
            }
        },
        [ordered]@{
            name = 'execute_saved_routine_in_active_model'
            description = 'Execute a hash-validated PythonNet3 routine in the currently open, saved RVT only after the user explicitly requests execution. First inspect the .py without loading it and explain the exact model, routine, intended elements and effects in chat. Dynamo must be open on an empty workspace. Revit then shows a separate Yes/No confirmation naming the active model and graph hash before loading and executing. No response or No cancels; Full Access never bypasses this dialog. The bridge does not save or synchronize the RVT. PythonNet3 has the permissions of Revit and Windows. If execution times out, inspect the model before considering a retry.'
            inputSchema = [ordered]@{
                type = 'object'
                properties = [ordered]@{
                    routine_id = [ordered]@{ type = 'string'; pattern = '^[a-z][a-z0-9_-]{1,47}$'; description = 'The saved routine to load and run after Revit confirmation; the current Dynamo workspace must be empty.' }
                    authorize_possible_model_writes = [ordered]@{ type = 'boolean'; description = 'Must be true; a separate confirmation inside Revit is always required.' }
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

function Assert-SavedRoutine([string]$RoutineId, [bool]$ConfirmModelWrite, [switch]$AllowWithoutWriteAuthorization) {
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
    if (-not $ConfirmModelWrite -and -not $AllowWithoutWriteAuthorization) { throw 'generic_execution_confirmation_required' }
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
        if ($Operation -in @('run_saved_routine', 'run_saved_routine_in_active_model')) { $request.confirmModelWrite = $ConfirmModelWrite }
        $writeTask = $writer.WriteLineAsync((ConvertTo-Json -InputObject $request -Depth 4 -Compress))
        if (-not $writeTask.Wait(5000)) { throw 'bridge_write_timeout' }
        $readTask = $reader.ReadLineAsync()
        $readTimeout = if ($Operation -eq 'run_saved_routine_in_active_model') { 305000 } else { 95000 }
        if (-not $readTask.Wait($readTimeout)) { throw 'bridge_response_timeout' }
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
                        'open_dynamo_ui' {
                            $bridge = Invoke-RevitBridge -Operation 'open_dynamo_ui' -RoutineId $null
                        }
                        'ping_revit_dynamo_bridge' {
                            $bridge = Invoke-RevitBridge -Operation 'ping' -RoutineId $null
                        }
                        'prepare_saved_routine_in_dynamo' {
                            $routineId = [string]$request.params.arguments.routine_id
                            $manifestPath = Join-Path $routinesRoot ($routineId + '.manifest.json')
                            if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw 'routine_files_missing' }
                            $savedManifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
                            [void](Assert-SavedRoutine -RoutineId $routineId -ConfirmModelWrite ([bool]$savedManifest.confirm_model_write) -AllowWithoutWriteAuthorization)
                            $bridge = Invoke-RevitBridge -Operation 'prepare_saved_routine_in_dynamo' -RoutineId $routineId
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
                        'execute_saved_routine_in_active_model' {
                            $routineId = [string]$request.params.arguments.routine_id
                            $confirmModelWrite = [bool]$request.params.arguments.authorize_possible_model_writes
                            [void](Assert-SavedRoutine -RoutineId $routineId -ConfirmModelWrite $confirmModelWrite)
                            $bridge = Invoke-RevitBridge -Operation 'run_saved_routine_in_active_model' -RoutineId $routineId -ConfirmModelWrite $confirmModelWrite
                        }
                        default { throw 'tool_not_allowed' }
                    }
                    if (-not $bridge.ok) { throw ([string]$bridge.status) }
                    New-ToolReply -RequestId $requestId -Text (ConvertTo-Json -InputObject $bridge -Depth 10 -Compress)
                } catch {
                    $status = 'bridge_unavailable_or_rejected'
                    if ($_.Exception.Message -match '^[a-z0-9_]{1,80}$') { $status = $_.Exception.Message }
                    $reply = [ordered]@{ ok = $false; status = $status }
                    $allowedVerificationFailures = @(
                        'routine_status_not_ok', 'document_modified_state_changed', 'document_modified',
                        'document_modifiable', 'active_document_changed', 'active_view_changed',
                        'selection_changed', 'routine_view_type_mismatch', 'routine_selection_count_mismatch',
                        'document_elements_changed', 'document_modified_without_element_changes'
                    )
                    if ($null -ne $bridge -and $bridge.verificationFailures) {
                        $safeFailures = @($bridge.verificationFailures | Where-Object { [string]$_ -in $allowedVerificationFailures })
                        if ($safeFailures.Count -gt 0) { $reply.verificationFailures = $safeFailures }
                    }
                    if ($null -ne $bridge -and $null -ne $bridge.changedElementCount) {
                        $reply.changedElementCount = [int]$bridge.changedElementCount
                    }
                    if ($null -ne $bridge -and $bridge.changedElementTypes) {
                        $safeElementTypes = @($bridge.changedElementTypes | Where-Object { [string]$_ -match '^[A-Za-z][A-Za-z0-9_]{0,63}$' } | Select-Object -First 8)
                        if ($safeElementTypes.Count -gt 0) { $reply.changedElementTypes = $safeElementTypes }
                    }
                    New-ToolReply -RequestId $requestId -Text ($reply | ConvertTo-Json -Compress) -IsError $true
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
