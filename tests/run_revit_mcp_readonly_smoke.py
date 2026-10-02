"""Live, read-only smoke check for the installed Revit MCP server.

Run only while Revit is open:
    python tests/run_revit_mcp_readonly_smoke.py

The script never prints model names, element ids, parameter values, or tool
payloads. It only reports whether the MCP handshake and two read calls passed.
"""

from __future__ import annotations

import json
import os
import queue
import subprocess
import sys
import threading
import time
from pathlib import Path
from typing import Any


TIMEOUT_SECONDS = 60
READ_TOOLS = ("revit_get_current_view_info", "revit_get_selected_elements")


class McpStdioClient:
    def __init__(self, executable: Path) -> None:
        self.process = subprocess.Popen(
            [str(executable), "--toolsets", "query", "--disable-toolbaker"],
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


def find_server() -> Path:
    local_app_data = os.environ.get("LOCALAPPDATA")
    if not local_app_data:
        raise RuntimeError("LOCALAPPDATA is not set")
    return Path(local_app_data) / "RvtMcp" / "rvt" / "server" / "0.6.2" / "rvt-mcp.exe"


def require_ok(response: dict[str, Any] | None) -> bool:
    if not response or "error" in response:
        return False
    result = response.get("result")
    return isinstance(result, dict) and not result.get("isError")


def read_tool_status(response: dict[str, Any] | None) -> str:
    if not require_ok(response):
        return "tool_error"
    result = response.get("result", {}) if isinstance(response, dict) else {}
    content = result.get("content", []) if isinstance(result, dict) else []
    if not isinstance(content, list) or not content:
        return "empty_response"
    saw_payload = False
    for item in content:
        if not isinstance(item, dict) or not isinstance(item.get("text"), str):
            continue
        text = item["text"].strip()
        if not text:
            continue
        saw_payload = True
        normalized = text.casefold()
        if normalized.startswith(("error", "failed", "exception")):
            if "document" in normalized and "open" in normalized:
                return "no_active_document"
            return "tool_error"
        try:
            payload = json.loads(text)
        except json.JSONDecodeError:
            continue
        if isinstance(payload, dict):
            if payload.get("error"):
                return "tool_error"
            status = str(payload.get("status", "")).casefold()
            if status in {"error", "failed", "failure", "no_active_document", "no_document_open"}:
                return "no_active_document" if "document" in status else "tool_error"
    return "ok" if saw_payload else "empty_response"


def main() -> int:
    try:
        executable = find_server()
        if not executable.is_file():
            print("MCP_SERVER=FAIL (no se encontró rvt-mcp v0.6.2)")
            return 2

        client = McpStdioClient(executable)
        passed = True
        try:
            initialize = client.request(
                "initialize",
                {
                    "protocolVersion": "2024-11-05",
                    "capabilities": {},
                    "clientInfo": {"name": "bim-ai-readonly-smoke", "version": "1.0"},
                },
            )
            init_ok = require_ok(initialize)
            print(f"MCP_INITIALIZE={'PASS' if init_ok else 'FAIL'}")
            passed &= init_ok
            if not init_ok:
                return 1

            client.notify_initialized()
            listing = client.request("tools/list", {})
            tools = listing.get("result", {}).get("tools", []) if require_ok(listing) else []
            available = {tool.get("name") for tool in tools if isinstance(tool, dict)}
            discovery_ok = set(READ_TOOLS).issubset(available)
            print(
                "READ_TOOL_DISCOVERY="
                + ("PASS" if discovery_ok else "FAIL")
                + f" count={len(tools)}"
            )
            passed &= discovery_ok
            if not discovery_ok:
                return 1

            for index, name in enumerate(READ_TOOLS, start=3):
                response = client.request(
                    "tools/call", {"name": name, "arguments": {}}
                )
                status = read_tool_status(response)
                ok = status == "ok"
                suffix = "" if ok else f" status={status}"
                print(f"{name.upper()}={'PASS' if ok else 'FAIL'}{suffix}")
                passed &= ok
            return 0 if passed else 1
        finally:
            client.close()
    except (OSError, RuntimeError, TimeoutError, json.JSONDecodeError) as exc:
        # Avoid printing exception contents because the MCP server may include
        # machine-local paths or model context in diagnostics.
        print(f"MCP_READONLY_SMOKE=FAIL ({type(exc).__name__})")
        return 1


if __name__ == "__main__":
    sys.exit(main())
