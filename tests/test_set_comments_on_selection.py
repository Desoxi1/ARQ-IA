"""Offline checks for the disposable-model Comments write routine."""

from __future__ import annotations

import hashlib
import json
import os
import tempfile
import sys
import types
import unittest
from pathlib import Path
from unittest.mock import patch


ROOT = Path(__file__).resolve().parents[1]
ROUTINE = ROOT / "routines" / "set_comments_on_selection.py"
GRAPH = ROOT / "routines" / "set_comments_on_selection.dyn"
MANIFEST = ROOT / "routines" / "set_comments_on_selection.manifest.json"
SOURCE = ROUTINE.read_text(encoding="utf-8")
WRAPPER = (ROOT / "dynamo" / "pythonnet3_result_wrapper.py").read_text(encoding="utf-8")


class FakeTransactionManager:
    def __init__(self):
        self.started = 0
        self.finished = 0

    def EnsureInTransaction(self, _doc):
        self.started += 1

    def TransactionTaskDone(self):
        self.finished += 1


class FakeElementId:
    def __init__(self, value):
        self.Value = value


class FakeParameter:
    def __init__(self, value="", read_only=False, storage_type="String"):
        self.value = value
        self.IsReadOnly = read_only
        self.StorageType = storage_type

    def AsString(self):
        return self.value

    def Set(self, value):
        self.value = value
        return True


def execute_routine(uidoc=None, transaction_manager=None):
    transaction_manager = transaction_manager or FakeTransactionManager()
    uiapp = types.SimpleNamespace(ActiveUIDocument=uidoc) if uidoc is not None else None
    clr = types.ModuleType("clr")
    clr.AddReference = lambda _name: None

    persistence = types.ModuleType("RevitServices.Persistence")
    persistence.DocumentManager = types.SimpleNamespace(
        Instance=types.SimpleNamespace(CurrentUIApplication=uiapp)
    )
    transactions = types.ModuleType("RevitServices.Transactions")
    transactions.TransactionManager = types.SimpleNamespace(Instance=transaction_manager)

    services = types.ModuleType("RevitServices")
    services.__path__ = []
    services.Persistence = persistence
    services.Transactions = transactions

    revit_db = types.ModuleType("Autodesk.Revit.DB")
    revit_db.BuiltInParameter = types.SimpleNamespace(ALL_MODEL_INSTANCE_COMMENTS="comments")
    revit_db.StorageType = types.SimpleNamespace(String="String")
    revit = types.ModuleType("Autodesk.Revit")
    revit.__path__ = []
    revit.DB = revit_db
    autodesk = types.ModuleType("Autodesk")
    autodesk.__path__ = []
    autodesk.Revit = revit

    modules = {
        "clr": clr,
        "RevitServices": services,
        "RevitServices.Persistence": persistence,
        "RevitServices.Transactions": transactions,
        "Autodesk": autodesk,
        "Autodesk.Revit": revit,
        "Autodesk.Revit.DB": revit_db,
    }
    with tempfile.TemporaryDirectory(prefix="bim-ai-comments-test-") as temp_name:
        temp_root = Path(temp_name)
        test_models_root = temp_root / "revit" / "test-models"
        test_models_root.mkdir(parents=True)
        settings_root = temp_root / "appdata" / "BIM-AI-POC"
        settings_root.mkdir(parents=True)
        (settings_root / "bridge.json").write_text(
            json.dumps({"testModelsRoot": str(test_models_root)}), encoding="utf-8"
        )
        if uidoc is not None:
            doc = uidoc.Document
            if getattr(doc, "OutsideTestModels", False):
                model_path = temp_root / "production" / "architectural-model.rvt"
            else:
                model_path = Path(doc.PathName) if getattr(doc, "PathName", "") else test_models_root / "test.rvt"
            model_path.parent.mkdir(parents=True, exist_ok=True)
            model_path.touch(exist_ok=True)
            doc.PathName = str(model_path)
            if not hasattr(doc, "IsWorkshared"):
                doc.IsWorkshared = False
            if not hasattr(doc, "IsModified"):
                doc.IsModified = False
            if not hasattr(doc, "IsModifiable"):
                doc.IsModifiable = False
        with patch.dict(sys.modules, modules), patch.dict(
            os.environ, {"APPDATA": str(temp_root / "appdata")}
        ):
            namespace = {"__name__": "__main__"}
            exec(compile(SOURCE, str(ROUTINE), "exec"), namespace)
        return namespace["OUT"], transaction_manager


class SetCommentsRoutineTests(unittest.TestCase):
    def make_document(self, elements, selected, read_only=False):
        doc = types.SimpleNamespace(
            IsReadOnly=read_only,
            IsWorkshared=False,
            IsModified=False,
            IsModifiable=False,
            PathName="",
            GetElement=lambda element_id: elements.get(element_id),
        )
        uidoc = types.SimpleNamespace(
            Document=doc,
            Selection=types.SimpleNamespace(GetElementIds=lambda: selected),
        )
        return doc, uidoc

    def test_missing_document_returns_without_transaction(self):
        result, manager = execute_routine(None)
        self.assertEqual(result["status"], "no_active_document")
        self.assertEqual(manager.started, 0)
        self.assertEqual(manager.finished, 0)

    def test_updates_selected_comments_and_leaves_unselected_elements_alone(self):
        selected_to_update = FakeElementId(21)
        selected_unchanged = FakeElementId(22)
        unselected = FakeElementId(23)
        p_update = FakeParameter("old")
        p_unchanged = FakeParameter("PRUEBA_IA")
        p_unselected = FakeParameter("old")
        elements = {
            selected_to_update: types.SimpleNamespace(
                get_Parameter=lambda _name: p_update
            ),
            selected_unchanged: types.SimpleNamespace(
                get_Parameter=lambda _name: p_unchanged
            ),
            unselected: types.SimpleNamespace(get_Parameter=lambda _name: p_unselected),
        }
        _doc, uidoc = self.make_document(
            elements, [selected_to_update, selected_unchanged]
        )

        result, manager = execute_routine(uidoc)

        self.assertEqual(result["status"], "ok")
        self.assertEqual(result["selected_count"], 2)
        self.assertEqual(result["updated_ids"], ["21"])
        self.assertEqual(result["affected_ids"], ["21"])
        self.assertEqual(result["unchanged_ids"], ["22"])
        self.assertEqual(result["skipped"], [])
        self.assertEqual(p_update.value, "PRUEBA_IA")
        self.assertEqual(p_unchanged.value, "PRUEBA_IA")
        self.assertEqual(p_unselected.value, "old")
        self.assertEqual((manager.started, manager.finished), (1, 1))

    def test_read_only_and_missing_parameters_are_skipped(self):
        read_only_id = FakeElementId(31)
        missing_id = FakeElementId(32)
        read_only_parameter = FakeParameter("old", read_only=True)
        elements = {
            read_only_id: types.SimpleNamespace(
                get_Parameter=lambda _name: read_only_parameter
            ),
            missing_id: types.SimpleNamespace(get_Parameter=lambda _name: None),
        }
        _doc, uidoc = self.make_document(elements, [read_only_id, missing_id])

        result, manager = execute_routine(uidoc)

        self.assertEqual(result["status"], "ok")
        self.assertEqual(result["updated_ids"], [])
        self.assertEqual(
            result["skipped"],
            [
                {"id": "31", "reason": "comments_not_writable_string"},
                {"id": "32", "reason": "comments_unavailable"},
            ],
        )
        self.assertEqual(read_only_parameter.value, "old")
        self.assertEqual((manager.started, manager.finished), (0, 0))

    def test_read_only_document_does_not_start_transaction(self):
        doc, uidoc = self.make_document({}, [], read_only=True)
        result, manager = execute_routine(uidoc)
        self.assertIsNotNone(doc)
        self.assertEqual(result["status"], "document_read_only")
        self.assertEqual((manager.started, manager.finished), (0, 0))

    def test_document_outside_disposable_test_models_is_rejected(self):
        doc, uidoc = self.make_document({}, [])
        doc.OutsideTestModels = True

        result, manager = execute_routine(uidoc)

        self.assertEqual(result["status"], "test_model_required")
        self.assertEqual((manager.started, manager.finished), (0, 0))

    def test_package_hashes_and_manual_pythonnet3_graph_match(self):
        graph = json.loads(GRAPH.read_text(encoding="utf-8"))
        manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
        self.assertEqual(len(graph["Nodes"]), 1)
        self.assertEqual(graph["Nodes"][0]["Engine"], "PythonNet3")
        self.assertEqual(graph["Nodes"][0]["Code"], SOURCE.rstrip() + "\n\n" + WRAPPER.lstrip())
        self.assertEqual(graph["View"]["Dynamo"]["RunType"], "Manual")
        self.assertEqual(graph["Connectors"], [])
        self.assertNotIn(b"\r\n", GRAPH.read_bytes())
        self.assertTrue(manifest["writes_model"])
        self.assertTrue(manifest["confirm_model_write"])
        self.assertEqual(manifest["status"], "draft_not_validated")
        self.assertEqual(manifest["result_contract"], "bim-ai-json-v1")
        self.assertEqual(
            manifest["runtime_wrapper_sha256"],
            hashlib.sha256((ROOT / "dynamo" / "pythonnet3_result_wrapper.py").read_bytes()).hexdigest(),
        )
        self.assertEqual(
            manifest["python_sha256"], hashlib.sha256(ROUTINE.read_bytes()).hexdigest()
        )
        self.assertEqual(
            manifest["dynamo_sha256"], hashlib.sha256(GRAPH.read_bytes()).hexdigest()
        )


if __name__ == "__main__":
    unittest.main()
