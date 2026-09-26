"""MCP contract check; optionally run the hash-pinned read-only graph in Revit.

Offline contract check:
    python tests/run_dynamo_runner_mcp_smoke.py

Live read-only execution (Revit 2027 open, add-in installed, saved/idle model):
    python tests/run_dynamo_runner_mcp_smoke.py --live

The live mode can execute only routines/read_active_view_selection.py after
the C# bridge validates its fixed hashes and checks the model is unchanged.
"""

from __future__ import annotations

import argparse
import json
import queue
import shutil
import subprocess
import sys
import threading
import time
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[1]
TIMEOUT_SECONDS = 105
EXPECTED_TOOLS = {"ping_revit_dynamo_bridge", "execute_saved_readonly_routine", "execute_saved_routine"}


def parse_tool_payload(response: dict[str, Any]) -> dict[str, Any]:
    result = response.get("result", {})
    items = result.get("content", []) if isinstance(result, dict) else []
    for item in items:
        if not isinstance(item, dict) or not isinstance(item.get("text"), str):
            continue
        try:
            payload = json.loads(item["text"])
        except json.JSONDecodeError:
            continue
        if isinstance(payload, dict):
            return payload
    return {}


class McpStdioClient:
    def __init__(self, powershell: str) -> None:
        self.process = subprocess.Popen(
            [
                powershell,
                "-NoLogo",
                "-NoProfile",
                "-ExecutionPolicy",
                "Bypass",
                "-File",
                str(ROOT / "mcp" / "DynamoRunnerMcp.ps1"),
            ],
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
        assert self.process.stdin is not None
        self.process.stdin.write(
            json.dumps({"jsonrpc": "2.0", "method": "notifications/initialized", "params": {}})
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


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--live", action="store_true", help="Run the hash-pinned read-only graph in Revit.")
    parser.add_argument("--live-generic", action="store_true", help="Run the saved generic read-only routine on the disposable test RVT.")
    args = parser.parse_args()

    powershell = shutil.which("powershell.exe")
    if not powershell:
        print("DYNAMO_RUNNER_MCP=FAIL (Windows PowerShell not found)")
        return 2

    client: McpStdioClient | None = None
    try:
        client = McpStdioClient(powershell)
        initialized = client.request(
            "initialize",
            {
                "protocolVersion": "2024-11-05",
                "capabilities": {},
                "clientInfo": {"name": "dynamo-runner-smoke", "version": "1.0"},
            },
        )
        if not initialized.get("result"):
            print("DYNAMO_RUNNER_INITIALIZE=FAIL")
            return 1
        client.notify_initialized()
        listing = client.request("tools/list", {})
        tools = listing.get("result", {}).get("tools", [])
        names = {tool.get("name") for tool in tools if isinstance(tool, dict)}
        if not EXPECTED_TOOLS.issubset(names):
            print("DYNAMO_RUNNER_TOOLS=FAIL")
            return 1
        execute_tool = next(
            (
                tool
                for tool in tools
                if isinstance(tool, dict) and tool.get("name") == "execute_saved_routine"
            ),
            {},
        )
        execute_schema = execute_tool.get("inputSchema", {})
        if (
            "authorize_possible_model_writes" not in execute_schema.get("properties", {})
            or "authorize_possible_model_writes" not in execute_schema.get("required", [])
        ):
            print("DYNAMO_RUNNER_AUTHORIZATION_SCHEMA=FAIL")
            return 1
        print("DYNAMO_RUNNER_AUTHORIZATION_SCHEMA=PASS")
        print("DYNAMO_RUNNER_MCP_CONTRACT=PASS tools=3")

        if not args.live and not args.live_generic:
            print("DYNAMO_RUNNER_LIVE=NOT_REQUESTED")
            return 0

        ping = client.request(
            "tools/call", {"name": "ping_revit_dynamo_bridge", "arguments": {}}
        )
        if ping.get("result", {}).get("isError"):
            status = parse_tool_payload(ping).get("status", "unknown")
            print(f"REVIT_BRIDGE_PING=FAIL status={status}")
            return 1
        print("REVIT_BRIDGE_PING=PASS")

        if args.live:
            run = client.request(
                "tools/call",
                {
                    "name": "execute_saved_readonly_routine",
                    "arguments": {"routine_id": "read_active_view_selection"},
                },
            )
            result = run.get("result", {})
            if result.get("isError"):
                status = parse_tool_payload(run).get("status", "unknown")
                print(f"DYNAMO_READONLY_RUN=FAIL status={status}")
                return 1
            payload = parse_tool_payload(run)
            ok = (
                payload.get("ok") is True
                and payload.get("status") == "executed_and_verified_read_only"
                and payload.get("modelChangeCheck") == "unchanged"
            )
            print(f"DYNAMO_READONLY_RUN={'PASS' if ok else 'FAIL'}")
            if not ok:
                return 1

        if args.live_generic:
            run = client.request(
                "tools/call",
                {
                    "name": "execute_saved_routine",
                    "arguments": {
                        "routine_id": "read_test_model_summary",
                        "authorize_possible_model_writes": True,
                    },
                },
            )
            result = run.get("result", {})
            if result.get("isError"):
                status = parse_tool_payload(run).get("status", "unknown")
                print(f"DYNAMO_GENERIC_READONLY_RUN=FAIL status={status}")
                return 1
            payload = parse_tool_payload(run)
            routine_result = payload.get("routineResult", {})
            result_value = routine_result.get("routine_result", {}) if isinstance(routine_result, dict) else {}
            ok = (
                payload.get("ok") is True
                and payload.get("status") == "executed_and_verified_read_only"
                and payload.get("modelChangeCheck") == "unchanged"
                and result_value.get("status") == "ok"
                and isinstance(result_value.get("element_count"), int)
            )
            print(f"DYNAMO_GENERIC_READONLY_RUN={'PASS' if ok else 'FAIL'}")
            if not ok:
                return 1

        return 0
    except (OSError, RuntimeError, TimeoutError, json.JSONDecodeError) as exc:
        print(f"DYNAMO_RUNNER_MCP=FAIL ({type(exc).__name__})")
        return 1
    finally:
        if client is not None:
            client.close()


if __name__ == "__main__":
    sys.exit(main())
