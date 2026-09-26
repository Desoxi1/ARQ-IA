"""Exercise Builder MCP -> saved routine -> Runner MCP against a disposable fake bridge.

This validates the offline MCP/PowerShell routine lifecycle and fake named-pipe
contract. It does not start Revit or Dynamo, or claim that the add-in works live.
"""

from __future__ import annotations

import json
import hashlib
import queue
import shutil
import subprocess
import sys
import tempfile
import threading
import time
import uuid
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[1]
TIMEOUT_SECONDS = 15


class McpStdioClient:
    def __init__(self, powershell: str, runner_path: Path) -> None:
        creationflags = getattr(subprocess, "CREATE_NO_WINDOW", 0)
        self.process = subprocess.Popen(
            [
                powershell,
                "-NoLogo",
                "-NoProfile",
                "-ExecutionPolicy",
                "Bypass",
                "-File",
                str(runner_path),
            ],
            cwd=ROOT,
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.DEVNULL,
            text=True,
            encoding="utf-8",
            bufsize=1,
            creationflags=creationflags,
        )
        self.lines: queue.Queue[str] = queue.Queue()
        self.next_id = 1
        threading.Thread(target=self._read_stdout, daemon=True).start()

    def _read_stdout(self) -> None:
        assert self.process.stdout is not None
        for line in self.process.stdout:
            self.lines.put(line)

    def request(self, method: str, params: dict[str, Any] | None = None) -> dict[str, Any]:
        if self.process.stdin is None:
            raise RuntimeError("MCP stdin is unavailable")
        request_id = self.next_id
        self.next_id += 1
        message: dict[str, Any] = {"jsonrpc": "2.0", "id": request_id, "method": method}
        if params is not None:
            message["params"] = params
        self.process.stdin.write(json.dumps(message, separators=(",", ":")) + "\n")
        self.process.stdin.flush()

        deadline = time.monotonic() + TIMEOUT_SECONDS
        while time.monotonic() < deadline:
            try:
                line = self.lines.get(timeout=max(0.01, deadline - time.monotonic()))
            except queue.Empty:
                break
            try:
                response = json.loads(line)
            except json.JSONDecodeError:
                continue
            if isinstance(response, dict) and response.get("id") == request_id:
                return response
        raise TimeoutError(f"MCP request timed out: {method}")

    def notify_initialized(self) -> None:
        assert self.process.stdin is not None
        self.process.stdin.write(
            json.dumps(
                {"jsonrpc": "2.0", "method": "notifications/initialized", "params": {}}
            )
            + "\n"
        )
        self.process.stdin.flush()

    def close(self) -> None:
        if self.process.stdin is not None:
            self.process.stdin.close()
        try:
            self.process.wait(timeout=5)
        except subprocess.TimeoutExpired:
            self.process.kill()
            self.process.wait(timeout=2)


def write_routine_fixture(
    directory: Path, *, include_writer: bool = True, include_readonly: bool = True
) -> None:
    routines = directory / "routines"
    dynamo = directory / "dynamo"
    routines.mkdir(parents=True, exist_ok=True)
    dynamo.mkdir(parents=True, exist_ok=True)
    wrapper = ROOT / "dynamo" / "pythonnet3_result_wrapper.py"
    wrapper_copy = dynamo / wrapper.name
    wrapper_copy.write_bytes(wrapper.read_bytes())
    wrapper_text = wrapper.read_text(encoding="utf-8")
    fixtures = []
    if include_writer:
        fixtures.append(("pipe_smoke_writer", True, 'OUT = {"status": "ok", "affected_ids": ["42"]}\n'))
    if include_readonly:
        fixtures.append(("pipe_smoke_readonly", False, 'OUT = {"status": "ok", "affected_ids": []}\n'))
    for name, writes_model, source in fixtures:
        graph = {
            "Nodes": [
                {
                    "Engine": "PythonNet3",
                    "Code": source.rstrip() + "\n\n" + wrapper_text.lstrip(),
                }
            ],
            "Connectors": [],
            "Dependencies": [],
            "View": {"Dynamo": {"RunType": "Manual"}},
        }
        python_path = routines / f"{name}.py"
        graph_path = routines / f"{name}.dyn"
        manifest_path = routines / f"{name}.manifest.json"
        python_bytes = source.encode("utf-8")
        graph_bytes = (json.dumps(graph, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
        python_path.write_bytes(python_bytes)
        graph_path.write_bytes(graph_bytes)
        manifest_path.write_text(
            json.dumps(
                {
                "schema_version": 1,
                "name": name,
                "description": "Disposable pipe smoke writer",
                "scope": "active_selection",
                "writes_model": writes_model,
                "confirm_model_write": writes_model,
                "status": "draft_not_validated",
                "execution": "mcp_runner_test_model_only",
                "python_engine": "PythonNet3",
                "result_contract": "bim-ai-json-v1",
                "runtime_wrapper_sha256": hashlib.sha256(wrapper_copy.read_bytes()).hexdigest(),
                "source_sha256": hashlib.sha256(python_bytes).hexdigest(),
                "python_sha256": hashlib.sha256(python_bytes).hexdigest(),
                "dynamo_sha256": hashlib.sha256(graph_bytes).hexdigest(),
                },
                indent=2,
            )
            + "\n",
            encoding="utf-8",
        )


def write_fake_bridge(directory: Path, pipe_name: str) -> Path:
    server_path = directory / "fake_bridge.ps1"
    source = rf"""$ErrorActionPreference = 'Stop'
$pipeName = '{pipe_name}'
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
for ($index = 0; $index -lt 4; $index++) {{
    $pipe = [System.IO.Pipes.NamedPipeServerStream]::new(
        $pipeName,
        [System.IO.Pipes.PipeDirection]::InOut,
        1,
        [System.IO.Pipes.PipeTransmissionMode]::Byte,
        [System.IO.Pipes.PipeOptions]::None
    )
    $pipe.WaitForConnection()
    $reader = [System.IO.StreamReader]::new($pipe, $utf8NoBom, $false, 1024, $true)
    $writer = [System.IO.StreamWriter]::new($pipe, $utf8NoBom, 1024, $true)
    $writer.AutoFlush = $true
    $request = $reader.ReadLine() | ConvertFrom-Json
    if ($request.operation -eq 'ping') {{
        $response = [ordered]@{{ ok = $true; status = 'ready'; revitMajor = '2027'; routineId = 'read_active_view_selection' }}
    }} elseif ($request.operation -eq 'run_saved_readonly_routine' -and $request.routineId -eq 'read_active_view_selection') {{
        $response = [ordered]@{{ ok = $true; status = 'executed_and_verified_read_only'; routineId = 'read_active_view_selection'; viewType = 'FloorPlan'; selectionCount = 0; modelChangeCheck = 'unchanged'; revitMajor = '2027'; dynamoAssemblyVersion = '4.0.2.3852' }}
    }} elseif ($request.operation -eq 'run_saved_routine' -and $request.routineId -eq 'pipe_smoke_writer' -and $request.confirmModelWrite -eq $true) {{
        $response = [ordered]@{{ ok = $true; status = 'executed_and_verified_change'; routineId = $request.routineId; modelChangeCheck = 'document_changed_ids_match_routine_result'; changedElementIds = @(42); routineResult = [ordered]@{{ status = 'ok'; affected_ids = @('42') }}; revitMajor = '2027'; dynamoAssemblyVersion = '4.0.2.3852' }}
    }} elseif ($request.operation -eq 'run_saved_routine' -and $request.routineId -eq 'pipe_smoke_readonly' -and $request.confirmModelWrite -eq $true) {{
        $response = [ordered]@{{ ok = $true; status = 'executed_and_verified_read_only'; routineId = $request.routineId; modelChangeCheck = 'unchanged'; changedElementIds = @(); routineResult = [ordered]@{{ status = 'ok'; affected_ids = @() }}; revitMajor = '2027'; dynamoAssemblyVersion = '4.0.2.3852' }}
    }} else {{
        $response = [ordered]@{{ ok = $false; status = 'routine_not_allowed' }}
    }}
    $writer.WriteLine((ConvertTo-Json -InputObject $response -Depth 8 -Compress))
    $writer.Dispose()
    $reader.Dispose()
    $pipe.Dispose()
}}
"""
    server_path.write_text(source, encoding="utf-8")
    return server_path


def main() -> int:
    powershell = shutil.which("powershell.exe")
    if not powershell:
        print("RUNNER_PIPE_SMOKE=FAIL (Windows PowerShell not found)")
        return 2

    runner_source = ROOT / "mcp" / "DynamoRunnerMcp.ps1"
    if not runner_source.is_file():
        print("RUNNER_PIPE_SMOKE=FAIL (runner script missing)")
        return 2

    pipe_name = "BIM-AI-TEST-" + uuid.uuid4().hex
    client: McpStdioClient | None = None
    builder_client: McpStdioClient | None = None
    fake_bridge: subprocess.Popen[str] | None = None
    try:
        with tempfile.TemporaryDirectory(prefix="bim-ai-pipe-smoke-") as temp_name:
            directory = Path(temp_name).resolve()
            (directory / "mcp").mkdir()
            runner_copy = directory / "mcp" / "DynamoRunnerMcp.ps1"
            runner_text = runner_source.read_text(encoding="utf-8")
            old_pipe_line = "$pipeName = 'BIM-AI-POC-Revit-27'"
            if runner_text.count(old_pipe_line) != 1:
                print("RUNNER_PIPE_SMOKE=FAIL (pipe declaration changed)")
                return 1
            runner_copy.write_text(
                runner_text.replace(old_pipe_line, f"$pipeName = '{pipe_name}'"),
                encoding="utf-8",
            )
            builder_copy = directory / "mcp" / "RoutineBuilderMcp.ps1"
            shutil.copy2(ROOT / "mcp" / "RoutineBuilderMcp.ps1", builder_copy)
            write_routine_fixture(directory, include_writer=False)
            shutil.copy2(ROOT / "dynamo" / "pythonnet3_template.dyn", directory / "dynamo" / "pythonnet3_template.dyn")

            builder_client = McpStdioClient(powershell, builder_copy)
            builder_initialized = builder_client.request(
                "initialize",
                {
                    "protocolVersion": "2024-11-05",
                    "capabilities": {},
                    "clientInfo": {"name": "routine-builder-pipe-smoke", "version": "1.0"},
                },
            )
            if not builder_initialized.get("result"):
                print("FULL_LOOP_BUILDER_INITIALIZE=FAIL")
                return 1
            builder_client.notify_initialized()
            generated = builder_client.request(
                "tools/call",
                {
                    "name": "create_dynamo_routine",
                    "arguments": {
                        "name": "pipe_smoke_writer",
                        "description": "Temporary full-loop writer for the fake Revit bridge.",
                        "python_code": 'OUT = {"status": "ok", "affected_ids": ["42"]}',
                        "scope": "active_selection",
                        "writes_model": True,
                        "confirm_model_write": True,
                    },
                },
            )
            generated_result = generated.get("result", {})
            generated_manifest_path = directory / "routines" / "pipe_smoke_writer.manifest.json"
            generated_manifest = json.loads(generated_manifest_path.read_text(encoding="utf-8")) if generated_manifest_path.is_file() else {}
            generated_ok = (
                not generated_result.get("isError")
                and generated_manifest.get("status") == "draft_not_validated"
                and generated_manifest.get("writes_model") is True
            )
            if not generated_ok:
                print("FULL_LOOP_ROUTINE_GENERATION=FAIL")
                return 1
            print("FULL_LOOP_ROUTINE_GENERATION=PASS")
            fake_script = write_fake_bridge(directory, pipe_name)
            fake_bridge = subprocess.Popen(
                [powershell, "-NoLogo", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(fake_script)],
                stdin=subprocess.DEVNULL,
                stdout=subprocess.DEVNULL,
                stderr=subprocess.DEVNULL,
                creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
            )
            client = McpStdioClient(powershell, runner_copy)

            initialized = client.request(
                "initialize",
                {
                    "protocolVersion": "2024-11-05",
                    "capabilities": {},
                    "clientInfo": {"name": "runner-pipe-smoke", "version": "1.0"},
                },
            )
            if not initialized.get("result"):
                print("RUNNER_PIPE_INITIALIZE=FAIL")
                return 1
            client.notify_initialized()

            tool_list = client.request("tools/list")
            available_tools = [tool.get("name") for tool in tool_list.get("result", {}).get("tools", [])]
            if "execute_saved_routine" not in available_tools:
                print("RUNNER_GENERIC_TOOL=FAIL missing tool definition")
                return 1
            print("RUNNER_GENERIC_TOOL=PASS")

            denied = client.request(
                "tools/call",
                {
                    "name": "execute_saved_routine",
                    "arguments": {"routine_id": "pipe_smoke_writer", "authorize_possible_model_writes": False},
                },
            )
            denied_items = denied.get("result", {}).get("content", [])
            denied_payload = json.loads(denied_items[0]["text"]) if denied_items else {}
            if not denied.get("result", {}).get("isError") or denied_payload.get("status") != "generic_execution_confirmation_required":
                print("RUNNER_GENERIC_CONFIRM=FAIL missing write gate")
                return 1
            print("RUNNER_GENERIC_CONFIRM=PASS")

            denied_readonly = client.request(
                "tools/call",
                {
                    "name": "execute_saved_routine",
                    "arguments": {"routine_id": "pipe_smoke_readonly", "authorize_possible_model_writes": False},
                },
            )
            denied_readonly_items = denied_readonly.get("result", {}).get("content", [])
            denied_readonly_payload = json.loads(denied_readonly_items[0]["text"]) if denied_readonly_items else {}
            if not denied_readonly.get("result", {}).get("isError") or denied_readonly_payload.get("status") != "generic_execution_confirmation_required":
                print("RUNNER_GENERIC_READONLY_CONFIRM=FAIL missing universal execution gate")
                return 1
            print("RUNNER_GENERIC_READONLY_CONFIRM=PASS")

            ping = client.request(
                "tools/call", {"name": "ping_revit_dynamo_bridge", "arguments": {}}
            )
            ping_items = ping.get("result", {}).get("content", [])
            ping_payload = json.loads(ping_items[0]["text"]) if ping_items else {}
            if ping.get("result", {}).get("isError") or ping_payload.get("status") != "ready":
                print(f"RUNNER_PIPE_PING=FAIL status={ping_payload.get('status', 'unknown')}")
                print(f"FAKE_BRIDGE_EXITED={fake_bridge.poll() is not None}")
                return 1
            print("RUNNER_PIPE_PING=PASS")

            run = client.request(
                "tools/call",
                {
                    "name": "execute_saved_readonly_routine",
                    "arguments": {"routine_id": "read_active_view_selection"},
                },
            )
            run_items = run.get("result", {}).get("content", [])
            run_payload = json.loads(run_items[0]["text"]) if run_items else {}
            if (
                run.get("result", {}).get("isError")
                or run_payload.get("status") != "executed_and_verified_read_only"
                or run_payload.get("modelChangeCheck") != "unchanged"
            ):
                print("RUNNER_PIPE_ROUTINE=FAIL")
                return 1
            print("RUNNER_PIPE_ROUTINE=PASS")

            generic = client.request(
                "tools/call",
                {
                    "name": "execute_saved_routine",
                    "arguments": {"routine_id": "pipe_smoke_writer", "authorize_possible_model_writes": True},
                },
            )
            generic_items = generic.get("result", {}).get("content", [])
            generic_payload = json.loads(generic_items[0]["text"]) if generic_items else {}
            manifest = json.loads((directory / "routines" / "pipe_smoke_writer.manifest.json").read_text(encoding="utf-8"))
            if (
                generic.get("result", {}).get("isError")
                or generic_payload.get("status") != "executed_and_verified_change"
                or generic_payload.get("changedElementIds") != [42]
                or manifest.get("status") != "validated"
            ):
                print(f"RUNNER_GENERIC_EXECUTION=FAIL payload={generic_payload} manifest_status={manifest.get('status')}")
                return 1
            print("RUNNER_GENERIC_EXECUTION=PASS manifest_promoted=true")

            generic_readonly = client.request(
                "tools/call",
                {
                    "name": "execute_saved_routine",
                    "arguments": {"routine_id": "pipe_smoke_readonly", "authorize_possible_model_writes": True},
                },
            )
            generic_readonly_items = generic_readonly.get("result", {}).get("content", [])
            generic_readonly_payload = json.loads(generic_readonly_items[0]["text"]) if generic_readonly_items else {}
            readonly_manifest = json.loads((directory / "routines" / "pipe_smoke_readonly.manifest.json").read_text(encoding="utf-8"))
            if (
                generic_readonly.get("result", {}).get("isError")
                or generic_readonly_payload.get("status") != "executed_and_verified_read_only"
                or readonly_manifest.get("status") != "validated"
            ):
                print("RUNNER_GENERIC_READONLY=FAIL")
                return 1
            print("RUNNER_GENERIC_READONLY=PASS manifest_promoted=true")

            saved_listing = builder_client.request(
                "tools/call", {"name": "list_saved_routines", "arguments": {}}
            )
            saved_items = saved_listing.get("result", {}).get("content", [])
            saved_payload = json.loads(saved_items[0]["text"]) if saved_items else {}
            saved_routines = saved_payload.get("routines", []) if isinstance(saved_payload, dict) else []
            saved_writer = next(
                (item for item in saved_routines if item.get("name") == "pipe_smoke_writer"),
                {},
            )
            if saved_writer.get("status") != "validated":
                print("FULL_LOOP_ROUTINE_REUSE=FAIL")
                return 1
            print("FULL_LOOP_ROUTINE_REUSE=PASS status=validated")

            fake_bridge.wait(timeout=5)
            fake_bridge = None
            builder_client.close()
            builder_client = None
            client.close()
            client = None

            no_server_client = McpStdioClient(powershell, runner_copy)
            try:
                no_server_init = no_server_client.request(
                    "initialize",
                    {
                        "protocolVersion": "2024-11-05",
                        "capabilities": {},
                        "clientInfo": {"name": "runner-pipe-timeout-smoke", "version": "1.0"},
                    },
                )
                if not no_server_init.get("result"):
                    print("RUNNER_PIPE_TIMEOUT=FAIL initialize")
                    return 1
                no_server_client.notify_initialized()
                started = time.monotonic()
                unavailable = no_server_client.request(
                    "tools/call", {"name": "ping_revit_dynamo_bridge", "arguments": {}}
                )
                elapsed = time.monotonic() - started
                items = unavailable.get("result", {}).get("content", [])
                failure = json.loads(items[0]["text"]) if items else {}
                if (
                    not unavailable.get("result", {}).get("isError")
                    or failure.get("status") != "bridge_connect_timeout"
                    or elapsed > 10
                ):
                    print("RUNNER_PIPE_TIMEOUT=FAIL")
                    return 1
                print("RUNNER_PIPE_TIMEOUT=PASS absent_bridge_fails_closed=true")
            finally:
                no_server_client.close()

            print("RUNNER_PIPE_TRANSPORT=PASS fake_bridge=true revit_started=false")
            return 0
    except (OSError, RuntimeError, TimeoutError, json.JSONDecodeError) as exc:
        print(f"RUNNER_PIPE_SMOKE=FAIL ({type(exc).__name__})")
        return 1
    finally:
        if builder_client is not None:
            builder_client.close()
        if client is not None:
            client.close()
        if fake_bridge is not None:
            try:
                fake_bridge.wait(timeout=2)
            except subprocess.TimeoutExpired:
                fake_bridge.kill()
                fake_bridge.wait(timeout=2)


if __name__ == "__main__":
    sys.exit(main())
