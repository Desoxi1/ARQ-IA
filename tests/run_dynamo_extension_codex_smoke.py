"""Verify the installed Codex app-server can create a read-only BIM assistant thread.

By default this sends a short, tool-free response request through the signed-in
Codex CLI. With --mcp it approves only the read-only list_saved_routines request.
With --revit-readonly it approves only active-view and selection queries in Revit.
With --eval-ambiguity it checks that an ambiguous BIM request receives a
clarification before tools or changes. No mode executes Dynamo or changes Revit.
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
READONLY_REVIT_TOOLS = {
    "revit_get_current_view_info",
    "revit_get_selected_elements",
}


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
        serialized_params = json.dumps(params, ensure_ascii=False).casefold()
        matched_read_tools = [name for name in READONLY_REVIT_TOOLS if name in serialized_params]
        if (
            method == "mcpServer/elicitation/request"
            and isinstance(params, dict)
            and params.get("serverName") == "rvt_mcp"
            and params.get("mode") == "form"
            and len(matched_read_tools) == 1
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
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--mcp", action="store_true", help="Also call the read-only routine listing MCP tool.")
    mode.add_argument("--revit-readonly", action="store_true", help="Call only the active-view and selection read tools in Revit.")
    mode.add_argument("--eval-ambiguity", action="store_true", help="Check that an ambiguous BIM request receives a clarification before tools or changes.")
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

        if args.mcp:
            developer_instructions = "Only call bim_ai_routines.list_saved_routines, a read-only tool. Do not call any Revit or write tool."
        elif args.revit_readonly:
            developer_instructions = (
                "Only call rvt_mcp.revit_get_current_view_info and "
                "rvt_mcp.revit_get_selected_elements, exactly once each and in that order. "
                "Wait for the first tool result before calling the second; never call them in parallel. "
                "Do not call any other tool, do not modify the model, and report the active view and selection count."
            )
        elif args.eval_ambiguity:
            developer_instructions = "Follow the project's AGENTS.md and language evaluation cases. For this evaluation, do not call tools or access Revit. If the request has materially different BIM/Revit interpretations, ask one concise clarification in Spanish before planning any change."
        else:
            developer_instructions = "Do not call tools or access Revit. Reply exactly BIM_AI_EXTENSION_CONNECTED."
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
        elif args.revit_readonly:
            smoke_prompt = (
                "Primero consulta una vez la vista activa de Revit y espera el resultado. "
                "Después consulta una vez la selección actual y espera el resultado. "
                "Dime el nombre de la vista activa y cuántos elementos hay seleccionados. "
                "Haz las llamadas una por una, nunca en paralelo. Solo lectura; no cambies nada."
            )
        elif args.eval_ambiguity:
            smoke_prompt = "Haz una planta para el segundo piso."
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
        if args.eval_ambiguity:
            normalized_reply = " ".join(reply.casefold().split())
            asks_clarification = any(marker in normalized_reply for marker in ("¿", "necesito saber", "aclarar", "prefieres", "prefiere"))
            identifies_distinct_bim_intents = (
                "vista" in normalized_reply
                and any(term in normalized_reply for term in ("nivel", "losa", "recinto", "distribuci", "diseñar", "disenar"))
            )
            if status != "completed" or not asks_clarification or not identifies_distinct_bim_intents or tool_calls or client.server_requests:
                print(f"CODEX_APP_SERVER_AMBIGUITY=FAIL status={status} reply={reply[:300]!r} calls={tool_calls} requests={client.server_requests}")
                return 1
            print("CODEX_APP_SERVER_AMBIGUITY=PASS asks_clarification=true tools=0")
            return 0
        if args.revit_readonly:
            completed_tools = [
                entry.split(":", 1)[0].split(".", 1)[-1]
                for entry in tool_calls
                if entry.casefold().endswith(":completed")
            ]
            if (
                status != "completed"
                or sorted(completed_tools) != sorted(READONLY_REVIT_TOOLS)
                or not reply.strip()
                or any("failed" in entry.casefold() for entry in tool_calls)
            ):
                print(f"CODEX_APP_SERVER_REVIT_READONLY=FAIL status={status} reply={reply[:300]!r} calls={tool_calls} requests={client.server_requests}")
                return 1
            print(f"CODEX_APP_SERVER_REVIT_READONLY=PASS tools={','.join(sorted(READONLY_REVIT_TOOLS))}")
            return 0
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
