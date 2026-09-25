"""Exercise the runner's named-pipe transport against a disposable fake bridge.

This validates only the MCP/PowerShell wire contract. It does not start Revit,
Dynamo, or claim that the add-in works inside Revit.
"""

from __future__ import annotations

import json
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


def write_fake_bridge(directory: Path, pipe_name: str) -> Path:
    server_path = directory / "fake_bridge.ps1"
    source = rf"""$ErrorActionPreference = 'Stop'
$pipeName = '{pipe_name}'
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
for ($index = 0; $index -lt 2; $index++) {{
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
    fake_bridge: subprocess.Popen[str] | None = None
    try:
        with tempfile.TemporaryDirectory(prefix="bim-ai-pipe-smoke-") as temp_name:
            directory = Path(temp_name).resolve()
            runner_copy = directory / "DynamoRunnerMcp.ps1"
            runner_text = runner_source.read_text(encoding="utf-8")
            old_pipe_line = "$pipeName = 'BIM-AI-POC-Revit-27'"
            if runner_text.count(old_pipe_line) != 1:
                print("RUNNER_PIPE_SMOKE=FAIL (pipe declaration changed)")
                return 1
            runner_copy.write_text(
                runner_text.replace(old_pipe_line, f"$pipeName = '{pipe_name}'"),
                encoding="utf-8",
            )
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
            fake_bridge.wait(timeout=5)
            fake_bridge = None
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
