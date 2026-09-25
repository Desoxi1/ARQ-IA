#requires -Version 5.1
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[Console]::InputEncoding = $utf8NoBom
[Console]::OutputEncoding = $utf8NoBom

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
            description = 'Check whether the per-user read-only Dynamo bridge is running inside Revit 2027. This makes no model changes.'
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
        }
    )
}

function Invoke-RevitBridge([string]$Operation, [string]$RoutineId) {
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
                        default { throw 'tool_not_allowed' }
                    }
                    if (-not $bridge.ok) { throw ([string]$bridge.status) }
                    New-ToolReply -RequestId $requestId -Text (ConvertTo-Json -InputObject $bridge -Depth 10 -Compress)
                } catch {
                    $status = 'bridge_unavailable_or_rejected'
                    if ($_.Exception.Message -in @('bridge_connect_timeout','bridge_write_timeout','bridge_response_timeout','no_active_document','document_must_be_saved_and_idle','routine_not_allowed','routine_integrity_check_failed','unsupported_revit_version','dynamo_command_failed','dynamo_result_missing','post_run_verification_failed')) {
                        $status = $_.Exception.Message
                    }
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
