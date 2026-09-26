# Appended verbatim after each generated routine's main script.
# Routines must return a JSON-compatible OUT value; the bridge reads this
# bounded result after Dynamo finishes and removes the per-run file.
import json as _bim_ai_json
import math as _bim_ai_math
import os as _bim_ai_os
import re as _bim_ai_re


def _bim_ai_normalize(value, depth=0):
    if depth > 16:
        raise ValueError("Routine result exceeds the maximum nesting depth")
    if value is None or isinstance(value, (str, bool, int)):
        return value
    if isinstance(value, float):
        if not _bim_ai_math.isfinite(value):
            raise ValueError("Routine result contains a non-finite number")
        return value
    if isinstance(value, dict):
        normalized = {}
        for key, item in value.items():
            if not isinstance(key, str):
                raise TypeError("Routine result object keys must be strings")
            normalized[key] = _bim_ai_normalize(item, depth + 1)
        return normalized
    if isinstance(value, (list, tuple)):
        return [_bim_ai_normalize(item, depth + 1) for item in value]
    raise TypeError("OUT must contain only JSON-compatible values")


_bim_ai_result_path = _bim_ai_os.environ.get("BIM_AI_RESULT_PATH", "")
if not _bim_ai_re.fullmatch(r"[0-9a-f]{32}", _bim_ai_os.environ.get("BIM_AI_RUN_ID", "")):
    raise ValueError("Missing or invalid runner correlation id")
if not _bim_ai_os.path.isabs(_bim_ai_result_path) or not _bim_ai_result_path.lower().endswith(".json"):
    raise ValueError("Missing or invalid runner result path")

_bim_ai_value = _bim_ai_normalize(OUT)
_bim_ai_status = "ok"
if isinstance(_bim_ai_value, dict):
    _bim_ai_status = _bim_ai_value.get("status", "ok")
    if not isinstance(_bim_ai_status, str) or len(_bim_ai_status) > 80:
        raise ValueError("Routine result status must be a short string")
_bim_ai_payload = _bim_ai_json.dumps(
    {"status": _bim_ai_status, "routine_result": _bim_ai_value},
    ensure_ascii=False,
    allow_nan=False,
    separators=(",", ":"),
)
if len(_bim_ai_payload.encode("utf-8")) > 65536:
    raise ValueError("Routine result exceeds 64 KiB")

_bim_ai_temp_path = _bim_ai_result_path + ".tmp"
with open(_bim_ai_temp_path, "w", encoding="utf-8", newline="\n") as _bim_ai_file:
    _bim_ai_file.write(_bim_ai_payload)
_bim_ai_os.replace(_bim_ai_temp_path, _bim_ai_result_path)
