"""Verify the installed Codex app-server can create a read-only BIM assistant thread.

By default this sends a short, tool-free response request through the signed-in
Codex CLI. With --mcp it approves only the read-only list_saved_routines request.
Neither mode calls Revit or Dynamo.
"""

from __future__ import annotations

import json
import argparse
import queue
import shutil
import subprocess
import sys
import threading
import time
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[1]
TIMEOUT_SECONDS = 90
EXPECTED_REPLY = "BIM_AI_EXTENSION_CONNECTED"


class AppServer:
    def __init__(self, executable: str) -> None:
        flags = getattr(subprocess, "CREATE_NO_WINDOW", 0)
        self.process = subprocess.Popen(
            [executable, "app-server", "--listen", "stdio://"],
            cwd=ROOT,
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.DEVNULL,
            text=True,
            encoding="utf-8",
            bufsize=1,
            creationflags=flags,
        )
        self.lines: queue.Queue[str] = queue.Queue()
        self.events: list[dict[str, Any]] = []
        self.server_requests: list[str] = []
        self.next_id = 1
        threading.Thread(target=self._read_stdout, daemon=True).start()

    def _read_stdout(self) -> None:
        assert self.process.stdout is not None
        for line in self.process.stdout:
            self.lines.put(line)

    def _send(self, payload: dict[str, Any]) -> None:
        if self.process.stdin is None:
            raise RuntimeError("app-server stdin unavailable")
        self.process.stdin.write(json.dumps(payload, separators=(",", ":")) + "\n")
        self.process.stdin.flush()

    def _next_message(self, deadline: float) -> dict[str, Any]:
        while time.monotonic() < deadline:
            try:
                line = self.lines.get(timeout=max(0.01, deadline - time.monotonic()))
            except queue.Empty as exc:
                raise TimeoutError("app-server response timed out") from exc
            try:
                value = json.loads(line)
            except json.JSONDecodeError:
                continue
            if isinstance(value, dict):
                return value
        raise TimeoutError("app-server response timed out")

    def request(self, method: str, params: dict[str, Any]) -> dict[str, Any]:
        request_id = self.next_id
        self.next_id += 1
        self._send({"jsonrpc": "2.0", "id": request_id, "method": method, "params": params})
        deadline = time.monotonic() + TIMEOUT_SECONDS
        while True:
            message = self._next_message(deadline)
            if message.get("id") == request_id:
                if "error" in message:
                    error = message.get("error", {})
                    code = error.get("code", "unknown") if isinstance(error, dict) else "unknown"
                    detail = error.get("message", "request failed") if isinstance(error, dict) else "request failed"
                    raise RuntimeError(f"app-server request failed code={code}: {str(detail)[:240]}")
                return message
            self._handle_server_request(message)
            self.events.append(message)

    def notify(self, method: str, params: dict[str, Any]) -> None:
        self._send({"jsonrpc": "2.0", "method": method, "params": params})

    def _handle_server_request(self, message: dict[str, Any]) -> None:
        if "id" not in message or "method" not in message:
            return
        method = str(message.get("method", "unknown"))
        params = message.get("params", {})
        if method == "mcpServer/elicitation/request" and isinstance(params, dict):
            schema = params.get("requestedSchema", {})
            properties = schema.get("properties", {}) if isinstance(schema, dict) else {}
            self.server_requests.append(
                f"{method}:{params.get('serverName', '?')}:{params.get('mode', '?')}:{str(params.get('message', params.get('description', ''))[:180])}:fields={list(properties) if isinstance(properties, dict) else []}:required={schema.get('required', []) if isinstance(schema, dict) else []}"
            )
        else:
            self.server_requests.append(method)
        schema = params.get("requestedSchema", {}) if isinstance(params, dict) else {}
        if (
            method == "mcpServer/elicitation/request"
            and isinstance(params, dict)
            and params.get("serverName") == "bim_ai_routines"
            and params.get("mode") == "form"
            and "list_saved_routines" in str(params.get("message", ""))
            and isinstance(schema, dict)
            and not (schema.get("properties") or schema.get("required"))
        ):
            self._send({
                "jsonrpc": "2.0",
                "id": message["id"],
                "result": {"action": "accept", "content": {}, "_meta": None},
            })
            return
        self._send({
            "jsonrpc": "2.0",
            "id": message["id"],
            "error": {"code": -32001, "message": "Smoke test denies all tool and permission requests."},
        })

    def wait_for_turn(self, expected_turn_id: str) -> tuple[str, str, list[str]]:
        deadline = time.monotonic() + TIMEOUT_SECONDS
        text_parts: list[str] = []
        tool_calls: list[str] = []
        while True:
            message = self.events.pop(0) if self.events else self._next_message(deadline)
            self._handle_server_request(message)
            if message.get("method") in {"item/started", "item/completed"}:
                params = message.get("params", {})
                item = params.get("item", {}) if isinstance(params, dict) else {}
                if isinstance(item, dict) and item.get("type") == "mcpToolCall":
                    tool_calls.append(f"{item.get('server', '?')}.{item.get('tool', '?')}:{item.get('status', '?')}")
            if message.get("method") == "item/agentMessage/delta":
                params = message.get("params", {})
                delta = params.get("delta") if isinstance(params, dict) else None
                if isinstance(delta, str) and params.get("turnId") == expected_turn_id:
                    text_parts.append(delta)
            if message.get("method") == "turn/completed":
                params = message.get("params", {})
                turn = params.get("turn", {}) if isinstance(params, dict) else {}
                status = turn.get("status", "unknown") if isinstance(turn, dict) else "unknown"
                if isinstance(turn, dict) and turn.get("id") == expected_turn_id:
                    return "".join(text_parts), str(status), tool_calls

    def close(self) -> None:
        if self.process.stdin is not None:
            self.process.stdin.close()
        try:
            self.process.wait(timeout=10)
        except subprocess.TimeoutExpired:
            self.process.kill()
            self.process.wait(timeout=3)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--mcp", action="store_true", help="Also call the read-only routine listing MCP tool.")
    args = parser.parse_args()
    executable = shutil.which("codex.exe") or shutil.which("codex")
    if not executable:
        print("CODEX_APP_SERVER=FAIL codex_cli_not_found")
        return 2

    client: AppServer | None = None
    try:
        client = AppServer(executable)
        initialized = client.request(
            "initialize",
            {
                "clientInfo": {"name": "bim_ai_dynamo_smoke", "title": "BIM AI Dynamo Smoke", "version": "0.1.0"},
                "capabilities": None,
            },
        )
        if not initialized.get("result"):
            print("CODEX_APP_SERVER_INITIALIZE=FAIL")
            return 1
        client.notify("initialized", {})

        developer_instructions = (
            "Only call bim_ai_routines.list_saved_routines, a read-only tool. Do not call any Revit or write tool."
            if args.mcp else "Do not call tools or access Revit. Reply exactly BIM_AI_EXTENSION_CONNECTED."
        )
        thread = client.request(
            "thread/start",
            {
                "cwd": str(ROOT),
                "approvalPolicy": "on-request",
                "sandbox": "read-only",
                "ephemeral": True,
                "developerInstructions": developer_instructions,
            },
        )
        result = thread.get("result", {})
        thread_id = result.get("thread", {}).get("id") if isinstance(result, dict) else None
        if not isinstance(thread_id, str) or not thread_id:
            print("CODEX_APP_SERVER_THREAD=FAIL")
            return 1
        print("CODEX_APP_SERVER_THREAD=PASS")

        if args.mcp:
            smoke_prompt = "Llama a bim_ai_routines.list_saved_routines. Responde únicamente con RUTINAS=<cantidad>. No uses herramientas que escriban ni accedas a Revit."
        else:
            smoke_prompt = "Responde exactamente BIM_AI_EXTENSION_CONNECTED. No uses herramientas."

        started = client.request(
            "turn/start",
            {
                "threadId": thread_id,
                "input": [{"type": "text", "text": smoke_prompt, "text_elements": []}],
                "approvalPolicy": "on-request",
            },
        )
        if not started.get("result"):
            print("CODEX_APP_SERVER_TURN_START=FAIL")
            return 1
        turn_id = started["result"].get("turn", {}).get("id")
        if not isinstance(turn_id, str) or not turn_id:
            print("CODEX_APP_SERVER_TURN_START=FAIL missing_turn_id")
            return 1
        reply, status, tool_calls = client.wait_for_turn(turn_id)
        expected = "RUTINAS=3" if args.mcp else EXPECTED_REPLY
        if status != "completed" or expected not in reply.replace(" ", ""):
            print(f"CODEX_APP_SERVER_{'MCP' if args.mcp else 'TURN'}=FAIL status={status} reply={reply[:300]!r} calls={tool_calls} requests={client.server_requests}")
            return 1
        if args.mcp:
            print("CODEX_APP_SERVER_MCP=PASS read_only_tool=true")
        else:
            print("CODEX_APP_SERVER_TURN=PASS tool_free=true")
        return 0
    except (OSError, RuntimeError, TimeoutError, json.JSONDecodeError) as exc:
        print(f"CODEX_APP_SERVER=FAIL error={type(exc).__name__}")
        return 1
    finally:
        if client is not None:
            client.close()


if __name__ == "__main__":
    sys.exit(main())
