"""Contract tests for the small Dynamo result transport wrapper."""

from __future__ import annotations

import json
import os
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch


ROOT = Path(__file__).resolve().parents[1]
WRAPPER = ROOT / "dynamo" / "pythonnet3_result_wrapper.py"
SOURCE = WRAPPER.read_text(encoding="utf-8")


def execute_wrapper(result, run_id="a" * 32, injected_context=False):
    with tempfile.TemporaryDirectory(prefix="bim-ai-result-test-") as temp_name:
        result_path = Path(temp_name) / "result.json"
        environment = {
            "BIM_AI_RUN_ID": "" if injected_context else run_id,
            "BIM_AI_RESULT_PATH": "" if injected_context else str(result_path),
        }
        with patch.dict(os.environ, environment):
            namespace = {"OUT": result, "__name__": "__main__"}
            if injected_context:
                namespace["_BIM_AI_RUN_ID"] = run_id
                namespace["_BIM_AI_RESULT_PATH"] = str(result_path)
            exec(compile(SOURCE, str(WRAPPER), "exec"), namespace)
        return json.loads(result_path.read_text(encoding="utf-8"))


class ResultWrapperTests(unittest.TestCase):
    def test_writes_bounded_json_result_with_status_and_payload(self):
        payload = execute_wrapper(
            {"status": "ok", "affected_ids": ["21"], "value": "PRUEBA_IA"}
        )
        self.assertEqual(payload["status"], "ok")
        self.assertEqual(payload["routine_result"]["affected_ids"], ["21"])

    def test_runtime_graph_context_globals_work_without_environment_variables(self):
        payload = execute_wrapper({"status": "ok", "value": 27}, injected_context=True)
        self.assertEqual(payload["status"], "ok")
        self.assertEqual(payload["routine_result"]["value"], 27)

    def test_non_json_values_are_rejected(self):
        with self.assertRaises(TypeError):
            execute_wrapper({"status": "ok", "value": object()})

    def test_oversized_result_is_rejected(self):
        with self.assertRaises(ValueError):
            execute_wrapper({"status": "ok", "value": "x" * 70000})

    def test_invalid_run_correlation_id_is_rejected(self):
        with self.assertRaises(ValueError):
            execute_wrapper({"status": "ok"}, run_id="bad")


if __name__ == "__main__":
    unittest.main()
