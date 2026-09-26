"""End-to-end MCP smoke test for saving a disposable Dynamo routine.

Run from the project root:
    python tests/run_routine_builder_mcp_smoke.py

The test only writes a temporary read-only routine under routines/ and removes
the three files it created. It does not launch Revit or execute Dynamo code.
"""

from __future__ import annotations

import hashlib
import json
import os
import queue
import shutil
import subprocess
import sys
import threading
import time
import uuid
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[1]
ROUTINES = ROOT / "routines"
TIMEOUT_SECONDS = 30


class McpStdioClient:
    def __init__(self, command: list[str]) -> None:
        self.process = subprocess.Popen(
            command,
            cwd=ROOT,
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.DEVNULL,
            text=True,
            encoding="utf-8",
            bufsize=1,
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
        if self.process.stdin is None:
            raise RuntimeError("MCP stdin is unavailable")
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


def require_ok(response: dict[str, Any] | None) -> bool:
    if not response or "error" in response:
        return False
    result = response.get("result")
    return isinstance(result, dict) and not result.get("isError")


def main() -> int:
    powershell = shutil.which("powershell.exe")
    if not powershell:
        print("ROUTINE_MCP_SMOKE=FAIL (Windows PowerShell not found)")
        return 2

    slug = "smoke_" + uuid.uuid4().hex[:12]
    denied_slug = "reject_" + uuid.uuid4().hex[:12]
    suffixes = (".py", ".dyn", ".manifest.json")
    files = [ROUTINES / f"{slug}{suffix}" for suffix in suffixes]
    denied_files = [ROUTINES / f"{denied_slug}{suffix}" for suffix in suffixes]
    cleanup_files = files + denied_files
    client: McpStdioClient | None = None
    passed = False
    try:
        client = McpStdioClient(
            [
                powershell,
                "-NoLogo",
                "-NoProfile",
                "-ExecutionPolicy",
                "Bypass",
                "-File",
                str(ROOT / "mcp" / "RoutineBuilderMcp.ps1"),
            ]
        )
        initialize = client.request(
            "initialize",
            {
                "protocolVersion": "2024-11-05",
                "capabilities": {},
                "clientInfo": {"name": "routine-builder-smoke", "version": "1.0"},
            },
        )
        init_ok = require_ok(initialize)
        print(f"ROUTINE_MCP_INITIALIZE={'PASS' if init_ok else 'FAIL'}")
        if not init_ok:
            return 1

        client.notify_initialized()
        listing = client.request("tools/list", {})
        tools = listing.get("result", {}).get("tools", []) if require_ok(listing) else []
        tool_names = {tool.get("name") for tool in tools if isinstance(tool, dict)}
        discovery_ok = {"list_saved_routines", "create_dynamo_routine"}.issubset(tool_names)
        print(f"ROUTINE_MCP_TOOLS={'PASS' if discovery_ok else 'FAIL'} count={len(tools)}")
        if not discovery_ok:
            return 1

        denied_response = client.request(
            "tools/call",
            {
                "name": "create_dynamo_routine",
                "arguments": {
                    "name": denied_slug,
                    "description": "Negative probe: unconfirmed model write.",
                    "python_code": "OUT = {'status': 'must_not_be_saved'}",
                    "scope": "active_selection",
                    "writes_model": True,
                    "confirm_model_write": False,
                },
            },
        )
        denied_result = denied_response.get("result", {})
        write_rejected = (
            isinstance(denied_result, dict)
            and denied_result.get("isError") is True
            and not any(path.exists() for path in denied_files)
        )
        print(f"UNCONFIRMED_MODEL_WRITE_REJECTED={'PASS' if write_rejected else 'FAIL'}")
        if not write_rejected:
            return 1

        response = client.request(
            "tools/call",
            {
                "name": "create_dynamo_routine",
                "arguments": {
                    "name": slug,
                    "description": "Temporary read-only MCP integration probe.",
                    "python_code": "OUT = {'status': 'ok', 'source': 'mcp_test'}",
                    "scope": "active_view",
                    "writes_model": False,
                    "confirm_model_write": False,
                },
            },
        )
        created = all(path.is_file() for path in files)
        create_ok = require_ok(response) and created
        print(f"CREATE_ROUTINE={'PASS' if create_ok else 'FAIL'}")
        if not create_ok:
            return 1

        manifest = json.loads(files[2].read_text(encoding="utf-8"))
        graph = json.loads(files[1].read_text(encoding="utf-8"))
        wrapper_path = ROOT / "dynamo" / "pythonnet3_result_wrapper.py"
        source = files[0].read_text(encoding="utf-8")
        wrapper = wrapper_path.read_text(encoding="utf-8")
        integrity_ok = (
            manifest.get("status") == "draft_not_validated"
            and manifest.get("writes_model") is False
            and manifest.get("confirm_model_write") is False
            and manifest.get("result_contract") == "bim-ai-json-v1"
            and manifest.get("runtime_wrapper_sha256")
            == hashlib.sha256(wrapper_path.read_bytes()).hexdigest()
            and manifest.get("source_sha256") == hashlib.sha256(files[0].read_bytes()).hexdigest()
            and hashlib.sha256(files[0].read_bytes()).hexdigest()
            == manifest.get("python_sha256")
            and hashlib.sha256(files[1].read_bytes()).hexdigest()
            == manifest.get("dynamo_sha256")
            and b"\r\n" not in files[1].read_bytes()
            and len(graph.get("Nodes", [])) == 1
            and graph["Nodes"][0].get("Engine") == "PythonNet3"
            and graph.get("View", {}).get("Dynamo", {}).get("RunType") == "Manual"
            and graph["Nodes"][0].get("Code") == source.rstrip() + "\n\n" + wrapper.lstrip()
        )
        print(f"GENERATED_PACKAGE_INTEGRITY={'PASS' if integrity_ok else 'FAIL'}")
        passed = integrity_ok
        return 0 if passed else 1
    except (OSError, RuntimeError, TimeoutError, json.JSONDecodeError) as exc:
        print(f"ROUTINE_MCP_SMOKE=FAIL ({type(exc).__name__})")
        return 1
    finally:
        if client is not None:
            client.close()
        for path in cleanup_files:
            if path.exists() and path.parent.resolve() == ROUTINES.resolve():
                path.unlink()
        cleanup_ok = not any(path.exists() for path in cleanup_files)
        print(f"TEMP_ROUTINE_CLEANUP={'PASS' if cleanup_ok else 'FAIL'}")


if __name__ == "__main__":
    sys.exit(main())
