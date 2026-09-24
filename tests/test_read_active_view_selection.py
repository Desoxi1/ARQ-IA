"""Offline contract tests for the read-only Revit/Dynamo example routine."""

from __future__ import annotations

import hashlib
import json
import sys
import types
import unittest
from pathlib import Path
from unittest.mock import patch


ROOT = Path(__file__).resolve().parents[1]
ROUTINE = ROOT / "routines" / "read_active_view_selection.py"
GRAPH = ROOT / "routines" / "read_active_view_selection.dyn"
MANIFEST = ROOT / "routines" / "read_active_view_selection.manifest.json"
SOURCE = ROUTINE.read_text(encoding="utf-8")


def execute_with_fake_revit(uidoc):
    clr = types.ModuleType("clr")
    clr.AddReference = lambda _name: None

    services = types.ModuleType("RevitServices")
    services.__path__ = []
    persistence = types.ModuleType("RevitServices.Persistence")
    persistence.DocumentManager = types.SimpleNamespace(
        Instance=types.SimpleNamespace(
            CurrentUIApplication=types.SimpleNamespace(ActiveUIDocument=uidoc)
            if uidoc is not None
            else None
        )
    )
    services.Persistence = persistence

    modules = {
        "clr": clr,
        "RevitServices": services,
        "RevitServices.Persistence": persistence,
    }
    with patch.dict(sys.modules, modules):
        namespace = {}
        exec(compile(SOURCE, str(ROUTINE), "exec"), namespace)
    return namespace["OUT"]


class ReadOnlyRoutineTests(unittest.TestCase):
    def test_missing_active_document_returns_stable_empty_result(self):
        self.assertEqual(
            execute_with_fake_revit(None),
            {
                "status": "no_active_document",
                "view_name": None,
                "view_type": None,
                "selection_count": 0,
                "selected_elements": [],
            },
        )

    def test_empty_selection_reports_active_view(self):
        uidoc = types.SimpleNamespace(
            Document=object(),
            ActiveView=types.SimpleNamespace(Name="Level 1", ViewType="FloorPlan"),
            Selection=types.SimpleNamespace(GetElementIds=lambda: []),
        )
        self.assertEqual(
            execute_with_fake_revit(uidoc),
            {
                "status": "ok",
                "view_name": "Level 1",
                "view_type": "FloorPlan",
                "selection_count": 0,
                "selected_elements": [],
            },
        )

    def test_selected_elements_include_ids_and_nullable_categories(self):
        class ElementId:
            def __init__(self, value):
                self.Value = value

        ids = [ElementId(101), ElementId(102)]
        elements_by_id = {
            ids[0]: types.SimpleNamespace(Id=ids[0], Category=types.SimpleNamespace(Name="Walls")),
            ids[1]: types.SimpleNamespace(Id=ids[1], Category=None),
        }
        uidoc = types.SimpleNamespace(
            Document=types.SimpleNamespace(GetElement=lambda element_id: elements_by_id.get(element_id)),
            ActiveView=types.SimpleNamespace(Name="3D", ViewType="ThreeD"),
            Selection=types.SimpleNamespace(GetElementIds=lambda: ids),
        )
        self.assertEqual(
            execute_with_fake_revit(uidoc),
            {
                "status": "ok",
                "view_name": "3D",
                "view_type": "ThreeD",
                "selection_count": 2,
                "selected_elements": [
                    {"id": "101", "category": "Walls"},
                    {"id": "102", "category": None},
                ],
            },
        )

    def test_graph_manifest_and_hashes_match_the_pythonnet3_source(self):
        graph = json.loads(GRAPH.read_text(encoding="utf-8"))
        manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
        self.assertEqual(len(graph["Nodes"]), 1)
        self.assertEqual(graph["Nodes"][0]["Engine"], "PythonNet3")
        self.assertEqual(graph["Nodes"][0]["Code"], SOURCE)
        self.assertEqual(graph["View"]["Dynamo"]["RunType"], "Manual")
        self.assertEqual(graph["Connectors"], [])
        self.assertFalse(manifest["writes_model"])
        self.assertEqual(manifest["status"], "draft_not_validated")
        self.assertEqual(
            manifest["python_sha256"],
            hashlib.sha256(ROUTINE.read_bytes()).hexdigest(),
        )
        self.assertEqual(
            manifest["dynamo_sha256"],
            hashlib.sha256(GRAPH.read_bytes()).hexdigest(),
        )


if __name__ == "__main__":
    unittest.main()
