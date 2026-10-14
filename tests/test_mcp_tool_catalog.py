"""Keep the published Revit MCP catalog broad, classified and fail-closed."""

from __future__ import annotations

import json
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


class McpToolCatalogTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.catalog = json.loads((ROOT / "config" / "rvt-mcp-tools.json").read_text(encoding="utf-8"))

    def test_every_enabled_tool_has_one_effect_classification(self):
        enabled = set(self.catalog["enabled_tools"])
        classified = self.catalog["clasificacion_herramientas_es"]
        read_only = set(classified["solo_lectura"])
        writes = set(classified["requieren_aprobacion"])
        self.assertFalse(read_only & writes)
        self.assertEqual(enabled, read_only | writes)

    def test_catalog_is_localized_and_matches_the_pinned_rvt_mcp(self):
        self.assertEqual(self.catalog["idioma"], "es-CL")
        self.assertEqual(self.catalog["version_rvt_mcp"], "1.1.0")
        self.assertEqual(set(self.catalog["toolsets"]), set(self.catalog["toolsets_es"]))
        self.assertTrue(all(self.catalog["toolsets_es"].values()))

    def test_powerful_and_file_export_toolsets_are_not_enabled(self):
        blocked = set(self.catalog["toolsets_bloqueados"])
        self.assertTrue({"meta", "delete", "toolbaker", "export", "workflows"} <= blocked)
        self.assertFalse(set(self.catalog["toolsets"]) & blocked)
        self.assertTrue(self.catalog["protecciones"]["deshabilitar_send_code"])
        self.assertTrue(self.catalog["protecciones"]["deshabilitar_toolbaker"])
        self.assertTrue(self.catalog["protecciones"]["requiere_aprobacion"])
        self.assertTrue({"revit_send_code_to_revit", "revit_run_baked_tool", "arbitrary_code"} <= set(self.catalog["always_disabled"]))

    def test_enabled_names_are_valid_and_exclude_blocked_capabilities(self):
        enabled = self.catalog["enabled_tools"]
        self.assertEqual(len(enabled), len(set(enabled)))
        for tool in enabled:
            self.assertRegex(tool, r"^revit_[a-z0-9_]+$")
            self.assertNotRegex(tool, r"(?i)send_code|delete|bake|export|arbitrary")

    def test_setup_turns_off_code_execution_on_the_server(self):
        setup = (ROOT / "setup.ps1").read_text(encoding="utf-8")
        self.assertIn('"--disable-send-code"', setup)
        self.assertIn('"--disable-toolbaker"', setup)
        self.assertIn("$classificationMismatch", setup)
        self.assertIn("$requiredProtectionMissing", setup)


if __name__ == "__main__":
    unittest.main()
