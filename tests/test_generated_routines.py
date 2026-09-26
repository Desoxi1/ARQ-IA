"""Integrity checks for reusable routines produced by the local builder."""

from __future__ import annotations

import hashlib
import json
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
WRAPPER = ROOT / "dynamo" / "pythonnet3_result_wrapper.py"


class GeneratedRoutineIntegrityTests(unittest.TestCase):
    def assert_routine_package(self, routine_id: str, writes_model: bool) -> None:
        routines = ROOT / "routines"
        source_path = routines / f"{routine_id}.py"
        graph_path = routines / f"{routine_id}.dyn"
        manifest_path = routines / f"{routine_id}.manifest.json"
        source_bytes = source_path.read_bytes()
        graph_bytes = graph_path.read_bytes()
        wrapper_bytes = WRAPPER.read_bytes()
        source = source_bytes.decode("utf-8")
        wrapper = wrapper_bytes.decode("utf-8")
        graph = json.loads(graph_bytes.decode("utf-8"))
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))

        self.assertEqual(manifest["name"], routine_id)
        self.assertEqual(manifest["python_engine"], "PythonNet3")
        self.assertEqual(manifest["result_contract"], "bim-ai-json-v1")
        self.assertEqual(manifest["writes_model"], writes_model)
        self.assertEqual(manifest["confirm_model_write"], writes_model)
        self.assertEqual(manifest["python_sha256"], hashlib.sha256(source_bytes).hexdigest())
        self.assertEqual(manifest["source_sha256"], hashlib.sha256(source_bytes).hexdigest())
        self.assertEqual(manifest["dynamo_sha256"], hashlib.sha256(graph_bytes).hexdigest())
        self.assertEqual(
            manifest["runtime_wrapper_sha256"], hashlib.sha256(wrapper_bytes).hexdigest()
        )
        self.assertEqual(len(graph["Nodes"]), 1)
        self.assertEqual(graph["Nodes"][0]["Engine"], "PythonNet3")
        self.assertEqual(
            graph["Nodes"][0]["Code"], source.rstrip() + "\n\n" + wrapper.lstrip()
        )
        self.assertEqual(graph["Connectors"], [])
        self.assertEqual(graph["Dependencies"], [])
        self.assertEqual(graph["View"]["Dynamo"]["RunType"], "Manual")
        self.assertNotIn(b"\r\n", graph_bytes)

    def test_generic_read_only_test_model_routine_package(self):
        self.assert_routine_package("read_test_model_summary", writes_model=False)
        source = (ROOT / "routines" / "read_test_model_summary.py").read_text(encoding="utf-8")
        compile(source, "read_test_model_summary.py", "exec")

    def test_comments_writer_is_scoped_and_requires_confirmation(self):
        self.assert_routine_package("set_comments_on_selection", writes_model=True)
        source = (ROOT / "routines" / "set_comments_on_selection.py").read_text(encoding="utf-8")
        self.assertIn("testModelsRoot", source)
        self.assertIn('"affected_ids": updated_ids', source)


if __name__ == "__main__":
    unittest.main()
