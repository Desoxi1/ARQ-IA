"""Checks that the shipped bridge and its pinned routine stay in sync."""

from __future__ import annotations

import hashlib
import json
import re
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
BRIDGE_SOURCE = ROOT / "revit" / "bridge" / "BimAiDynamoBridge.cs"
BRIDGE_INSTALLER = ROOT / "revit" / "bridge" / "install.ps1"
BRIDGE_DLL = ROOT / "revit" / "bridge" / "dist" / "BimAiDynamoBridge.dll"
BRIDGE_CHECKSUM = ROOT / "revit" / "bridge" / "dist" / "BimAiDynamoBridge.sha256"
READ_ONLY_MANIFEST = ROOT / "routines" / "read_active_view_selection.manifest.json"
READ_ONLY_GRAPH = ROOT / "routines" / "read_active_view_selection.dyn"
READ_ONLY_SCRIPT = ROOT / "routines" / "read_active_view_selection.py"


class BridgePackageIntegrityTests(unittest.TestCase):
    def test_distributed_dll_matches_its_sha256_file(self):
        expected = BRIDGE_CHECKSUM.read_text(encoding="utf-8").strip().lower()
        actual = hashlib.sha256(BRIDGE_DLL.read_bytes()).hexdigest()
        self.assertRegex(expected, r"^[0-9a-f]{64}$")
        self.assertEqual(expected, actual)

    def test_bridge_hash_pins_match_the_read_only_routine_manifest(self):
        source = BRIDGE_SOURCE.read_text(encoding="utf-8")
        manifest = json.loads(READ_ONLY_MANIFEST.read_text(encoding="utf-8"))
        expected_python = re.search(r'ExpectedPythonHash = "([0-9a-f]{64})"', source)
        expected_graph = re.search(r'ExpectedGraphHash = "([0-9a-f]{64})"', source)
        self.assertIsNotNone(expected_python)
        self.assertIsNotNone(expected_graph)
        self.assertEqual(expected_python.group(1), manifest["python_sha256"])
        self.assertEqual(expected_graph.group(1), manifest["dynamo_sha256"])
        self.assertEqual(
            manifest["python_sha256"], hashlib.sha256(READ_ONLY_SCRIPT.read_bytes()).hexdigest()
        )
        self.assertEqual(
            manifest["dynamo_sha256"], hashlib.sha256(READ_ONLY_GRAPH.read_bytes()).hexdigest()
        )

    def test_pinned_graph_has_canonical_lf_line_endings(self):
        self.assertNotIn(b"\r\n", READ_ONLY_GRAPH.read_bytes())

    def test_generated_result_wrapper_hash_matches_bridge_pin(self):
        source = BRIDGE_SOURCE.read_text(encoding="utf-8")
        wrapper = ROOT / "dynamo" / "pythonnet3_result_wrapper.py"
        expected = re.search(r'ExpectedResultWrapperHash = "([0-9a-f]{64})"', source)
        self.assertIsNotNone(expected)
        self.assertEqual(expected.group(1), hashlib.sha256(wrapper.read_bytes()).hexdigest())

    def test_bridge_distinguishes_expired_queue_from_unknown_in_progress_timeout(self):
        source = BRIDGE_SOURCE.read_text(encoding="utf-8")
        self.assertIn("revit_dispatch_timeout_before_execution", source)
        self.assertIn("revit_execution_timeout_unknown", source)
        self.assertIn("TryCancelBeforeStart", source)

    def test_generic_pythonnet3_execution_requires_confirmation_even_if_manifest_says_readonly(
        self,
    ):
        source = BRIDGE_SOURCE.read_text(encoding="utf-8")
        self.assertIn(
            'if (!request.ConfirmModelWrite) return BridgeResponse.Fail("generic_execution_confirmation_required");',
            source,
        )
        self.assertIn(
            'if (document.IsWorkshared) return BridgeResponse.Fail("model_must_be_local_non_workshared");',
            source,
        )

    def test_runtime_graph_injects_result_context_without_process_environment(self):
        source = BRIDGE_SOURCE.read_text(encoding="utf-8")
        self.assertIn("CreateRuntimeGraphCopy(validatedGraphBytes, runId, resultPath)", source)
        self.assertIn('"_BIM_AI_RUN_ID = " + JsonSerializer.Serialize(runId)', source)
        self.assertIn('"_BIM_AI_RESULT_PATH = " + JsonSerializer.Serialize(absoluteResultPath)', source)
        self.assertIn("FileMode.CreateNew", source)
        self.assertIn("Delete(runtimeGraphPath)", source)
        self.assertIn("_BIM_AI_PYTHON_STARTED_PATH", source)
        self.assertIn("dynamo_python_node_started_without_result", source)
        self.assertIn("dynamo_python_node_not_started", source)

    def test_bridge_initializes_ui_less_dynamo_before_opening_the_graph(self):
        source = BRIDGE_SOURCE.read_text(encoding="utf-8")
        self.assertIn("ExecuteDynamoWorkspace(application, journal, runtimeGraphPath, resultPath, disableTransactions: true)", source)
        self.assertIn("disableTransactions: !writesModel", source)
        self.assertIn("EnsureDynamoInitialized(application)", source)
        self.assertIn('return modelState == "StartedUIless" || modelState == "StartedUI" ? Result.Succeeded : Result.Failed;', source)
        self.assertIn("initializationJournal", source)
        self.assertIn('GetProperty("RevitDynamoModel", BindingFlags.Public | BindingFlags.Static)', source)
        self.assertIn('GetProperty("State", BindingFlags.Public | BindingFlags.Instance)', source)
        self.assertIn('GetProperty("CurrentWorkspace", BindingFlags.Public | BindingFlags.Instance)', source)
        self.assertIn('GetMethod("Run", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null)', source)
        self.assertIn('GetType("RevitServices.Transactions.TransactionManager", false)', source)
        self.assertIn('GetProperty("DisableTransactions", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)', source)
        self.assertIn('post_run_document_changed_" + changedIds.Count', source)
        self.assertIn("post_run_document_modified_without_element_changes", source)

    def test_installer_uses_hash_named_assemblies_for_open_revit_sessions(self):
        installer = BRIDGE_INSTALLER.read_text(encoding="utf-8")
        self.assertIn("BimAiDynamoBridge.' + $assemblyHash.Substring(0, 16) + '.dll", installer)
        self.assertIn("$stagedHash -ne $assemblyHash", installer)
        self.assertIn("Move-Item -LiteralPath $manifestTemp -Destination $manifestTarget -Force", installer)


if __name__ == "__main__":
    unittest.main()
