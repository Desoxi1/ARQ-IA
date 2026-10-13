"""Checks the clone-and-run installation path and its user-facing guidance."""

from __future__ import annotations

import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


class BootstrapInstallerTests(unittest.TestCase):
    def test_root_installer_is_clone_path_agnostic_and_checks_required_hosts(self):
        installer = (ROOT / "install.ps1").read_text(encoding="utf-8")
        self.assertIn("$projectRoot = [IO.Path]::GetFullPath($PSScriptRoot)", installer)
        self.assertIn("Autodesk\\Revit 2027", installer)
        self.assertIn("DynamoCore.dll", installer)
        self.assertIn("Get-Command 'codex.exe'", installer)
        self.assertNotRegex(installer, r"(?i)[A-Z]:\\Users\\Branko")

    def test_root_installer_runs_the_complete_local_stack_and_links_current_clone(self):
        installer = (ROOT / "install.ps1").read_text(encoding="utf-8")
        for child in (
            "setup.ps1",
            "revit\\bridge\\install.ps1",
            "revit\\assistant\\install.ps1",
            "dynamo\\extension\\install.ps1",
        ):
            self.assertIn(child, installer)
        self.assertIn("'-StageWhileRevitOpen'", installer)
        self.assertIn("'-Force'", installer)
        self.assertIn("bridge.json", installer)
        self.assertIn("Join-Path $projectRoot 'routines'", installer)
        self.assertIn("$settings.routinesRoot", installer)

    def test_installer_has_a_read_only_preflight_and_clear_first_run_boundaries(self):
        installer = (ROOT / "install.ps1").read_text(encoding="utf-8")
        self.assertIn("[switch]$CheckOnly", installer)
        self.assertIn("PREFLIGHT=PASS", installer)
        self.assertIn("Guarda el trabajo y cierra Revit", installer)
        self.assertIn("Abre esta carpeta en Codex", installer)
        self.assertIn("INSTALL=PASS", installer)

    def test_panel_replaces_ambiguous_setup_text_with_one_exact_command(self):
        pane = (ROOT / "dynamo" / "extension" / "AssistantPane.cs").read_text(encoding="utf-8")
        self.assertIn("Instalar-ARQ-IA.cmd", pane)
        self.assertIn("Primera instalación pendiente", pane)
        self.assertNotIn("Proyecto sin configurar", pane)
        self.assertNotIn("Configura BIM-AI-POC", pane)
        self.assertIn("_prompt.IsEnabled = projectRoot is not null", pane)
        self.assertIn("_attachButton.IsEnabled = projectRoot is not null", pane)
        self.assertIn('Text = "Asistente BIM · Revit 2027"', pane)
        self.assertNotIn("Gestor: Branko Plaza", pane)

    def test_docs_and_portable_package_point_to_the_single_entrypoint(self):
        readme = (ROOT / "README.md").read_text(encoding="utf-8")
        other_pc = (ROOT / "docs" / "INSTRUCCIONES_OTRO_PC.md").read_text(encoding="utf-8")
        packager = (ROOT / "package.ps1").read_text(encoding="utf-8")
        self.assertIn(".\\install.ps1", readme)
        self.assertIn(".\\install.ps1", other_pc)
        self.assertIn("ARQ-IA/install.ps1", packager)
        self.assertIn("ARQ-IA/Instalar-ARQ-IA.cmd", packager)


if __name__ == "__main__":
    unittest.main()
