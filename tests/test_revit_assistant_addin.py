"""Checks the native Revit chat add-in and its portable registration contract."""

from __future__ import annotations

import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
ADDIN = ROOT / "revit" / "assistant"
SHARED = ROOT / "dynamo" / "extension"


class RevitAssistantAddinTests(unittest.TestCase):
    def test_revit_host_reuses_chat_and_attachment_sources_without_dynamo_references(self):
        project = (ADDIN / "BimAiRevitAssistant.csproj").read_text(encoding="utf-8")
        self.assertIn("net10.0-windows", project)
        self.assertIn("RevitAPI.dll", project)
        self.assertIn("RevitAPIUI.dll", project)
        self.assertIn("AssistantPane.cs", project)
        self.assertIn("CodexAppServerClient.cs", project)
        self.assertIn("LocalAttachmentStore.cs", project)
        self.assertNotIn("DynamoCore.dll", project)
        self.assertNotIn("DynamoCoreWpf.dll", project)
        for source in ("AssistantPane.cs", "CodexAppServerClient.cs", "LocalAttachmentStore.cs", "ProjectSettings.cs"):
            self.assertTrue((SHARED / source).is_file())

    def test_addin_registers_a_right_dockable_pane_and_revit_ribbon_command(self):
        source = (ADDIN / "BimAiRevitAssistant.cs").read_text(encoding="utf-8")
        self.assertIn("RegisterDockablePane", source)
        self.assertIn("DockPosition.Right", source)
        self.assertIn('"BIM-AI"', source)
        self.assertIn('"Abrir chat"', source)
        self.assertIn("GetDockablePane", source)
        self.assertIn("Gestor: Branko Plaza", (SHARED / "AssistantPane.cs").read_text(encoding="utf-8"))

    def test_installer_is_per_user_hash_checked_and_identifies_branko_plaza_as_vendor(self):
        installer = (ADDIN / "install.ps1").read_text(encoding="utf-8")
        self.assertIn("Autodesk\\Revit\\Addins\\2027", installer)
        self.assertIn("Get-FileHash", installer)
        self.assertIn("VendorDescription>Branko Plaza", installer)
        self.assertIn("StageWhileRevitOpen", installer)
        self.assertIn("BimAiRevitAssistant.RevitAssistantApplication", installer)
        self.assertIn(".codex\\config.toml.example", installer)
        self.assertIn("$verified.RevitAddIns.AddIn.VendorDescription -ne 'Branko Plaza'", installer)
        self.assertNotIn("Remove-Item -Recurse", installer)

    def test_build_publishes_only_the_host_addin_assembly(self):
        build = (ADDIN / "build.ps1").read_text(encoding="utf-8")
        self.assertIn("dotnet build", build)
        self.assertIn("DYNAMO_ASSEMBLY_DEPENDENCIES=NONE", build)
        self.assertIn("RevitAPI.dll", build)
        self.assertIn("BimAiRevitAssistant.sha256", build)

    def test_doctor_and_portable_packager_cover_the_revit_addin(self):
        doctor = (ROOT / "doctor.ps1").read_text(encoding="utf-8")
        packager = (ROOT / "package.ps1").read_text(encoding="utf-8")
        self.assertIn("BimAiRevitAssistant.addin", doctor)
        self.assertIn("VendorDescription -ne 'Branko Plaza'", doctor)
        self.assertIn("revit\\assistant\\dist\\BimAiRevitAssistant.dll", doctor)
        self.assertIn("BIM-AI-POC/revit/assistant/dist/BimAiRevitAssistant.dll", packager)
        self.assertIn("El DLL del add-in Revit no coincide", packager)
        settings = (SHARED / "ProjectSettings.cs").read_text(encoding="utf-8")
        self.assertIn('"dynamo", "extension", "README.md"', settings)
        self.assertIn('Directory.Exists(Path.Combine(projectRoot, "mcp"))', settings)


if __name__ == "__main__":
    unittest.main()
