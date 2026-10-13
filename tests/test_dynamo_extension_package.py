"""Checks the Dynamo 2027 View Extension deployment and load diagnostics."""

from __future__ import annotations

import json
import unittest
from pathlib import Path
import xml.etree.ElementTree as ET


ROOT = Path(__file__).resolve().parents[1]
EXTENSION = ROOT / "dynamo" / "extension"


class DynamoExtensionPackageTests(unittest.TestCase):
    def test_project_readiness_uses_the_generated_codex_configuration(self):
        settings = (EXTENSION / "ProjectSettings.cs").read_text(encoding="utf-8")
        self.assertIn('".codex", "config.toml"', settings)
        self.assertNotIn('".codex", "config.toml.example"', settings)

    def test_pkg_manifest_targets_installed_dynamo_4_and_is_not_a_node_library(self):
        package = json.loads((EXTENSION / "pkg.json").read_text(encoding="utf-8"))
        self.assertEqual(package["name"], "ARQ-IA")
        self.assertEqual(package["version"], "0.5.1")
        self.assertEqual(package["engine"], "dynamo")
        self.assertTrue(package["engine_version"].startswith("4.0."))
        self.assertTrue(package["contains_binaries"])
        self.assertEqual(package["node_libraries"], [])

    def test_view_extension_manifest_resolves_dll_beside_dynamo_core(self):
        manifest = ET.parse(EXTENSION / "BimAiDynamoAssistant_ViewExtensionDefinition.xml").getroot()
        self.assertEqual(manifest.findtext("AssemblyPath"), r"..\BimAiDynamoAssistant.dll")
        self.assertEqual(
            manifest.findtext("TypeName"),
            "BimAiDynamoAssistant.BimAiDynamoViewExtension",
        )

    def test_installer_uses_the_current_user_dynamo_package_location(self):
        installer = (EXTENSION / "install.ps1").read_text(encoding="utf-8")
        self.assertIn("Autodesk\\Revit 2027\\AddIns\\DynamoForRevit", installer)
        self.assertIn("Dynamo\\Dynamo Revit\\27.0\\packages", installer)
        self.assertIn("$manifest.ViewExtensionDefinition.AssemblyPath = '..\\bin\\' + $targetDllName", installer)
        self.assertIn("StageWhileRevitOpen", installer)
        self.assertIn("DLL_SCOPE=CurrentUserPackage", installer)
        self.assertIn("$targetDllName = 'BimAiDynamoAssistant.dll'", installer)
        self.assertIn("$legacyDlls", installer)
        self.assertIn("Remove-Item -LiteralPath $legacyDll.FullName", installer)

    def test_doctor_checks_runtime_load_separately_from_file_installation(self):
        doctor = (ROOT / "doctor.ps1").read_text(encoding="utf-8")
        self.assertIn("Dynamo Revit\\27.0\\Logs", doctor)
        self.assertIn("view extension is added", doctor)
        self.assertIn("journal más reciente de Dynamo", doctor)
        self.assertIn("actualiza con install.ps1 -Force", doctor)

    def test_user_attachments_are_private_and_excluded_from_portable_packages(self):
        gitignore = (ROOT / ".gitignore").read_text(encoding="utf-8")
        packager = (ROOT / "package.ps1").read_text(encoding="utf-8")
        self.assertIn("/logs/**", gitignore)
        self.assertIn("logs/(?!README", packager)

    def test_assistant_ui_exposes_image_paste_drag_drop_and_revit_mcp_context(self):
        pane = (EXTENSION / "AssistantPane.cs").read_text(encoding="utf-8")
        client = (EXTENSION / "CodexAppServerClient.cs").read_text(encoding="utf-8")
        self.assertIn("Clipboard.ContainsImage()", pane)
        self.assertIn("PreviewDrop", pane)
        self.assertIn("OpenFileDialog", pane)
        self.assertIn("Revit 2027", pane)
        self.assertIn("RequestContextAsync", pane)
        self.assertIn('type = "localImage"', client)
        self.assertIn("INICIO DE CONTENIDO ADJUNTO NO CONFIABLE", client)

    def test_assistant_is_available_from_dynamo_extensions_menu_and_sidebar(self):
        extension = (EXTENSION / "BimAiDynamoViewExtension.cs").read_text(encoding="utf-8")
        self.assertIn('Header = "Abrir ARQ-IA"', extension)
        self.assertIn("p.AddExtensionMenuItem(openAssistant)", extension)
        self.assertIn("p.AddToExtensionsSideBar(this", extension)
        self.assertIn("p.OpenViewExtension(Name)", extension)
        self.assertLess(extension.index("p.AddExtensionMenuItem(openAssistant)"), extension.index("new AssistantPane("))

    def test_codex_full_access_is_explicit_and_overrides_per_call_approvals(self):
        pane = (EXTENSION / "AssistantPane.cs").read_text(encoding="utf-8")
        client = (EXTENSION / "CodexAppServerClient.cs").read_text(encoding="utf-8")
        self.assertIn('CreateButton("Acceso controlado"', pane)
        self.assertIn('"● Acceso completo"', pane)
        self.assertIn("private FrameworkElement BuildFooter(bool projectReady)", pane)
        self.assertLess(pane.index("private FrameworkElement BuildHeader()"), pane.index("private FrameworkElement BuildFooter(bool projectReady)"))
        self.assertNotIn('Text = "MCP sin verificar"', pane)
        self.assertIn('FullAccessEnabled ? "never" : "on-request"', client)
        self.assertIn('FullAccessEnabled ? "danger-full-access" : "read-only"', client)
        self.assertIn('FullAccessEnabled ? "dangerFullAccess" : "readOnly"', client)
        self.assertIn("FullAccessEnabled || (ApprovalRequested is not null", client)

    def test_assistant_buttons_define_readable_disabled_colors(self):
        pane = (EXTENSION / "AssistantPane.cs").read_text(encoding="utf-8")
        self.assertIn("CreateButtonTemplate()", pane)
        self.assertIn("ButtonAccent = BrushFromRgb(89, 200, 230)", pane)
        self.assertIn('CreateButton("+", Brushes.Transparent, ButtonAccent', pane)
        self.assertIn('CreateButton("↑", BrushFromRgb(30, 30, 30), TextBrush', pane)
        self.assertIn('CreateButtonTemplate(16)', pane)
        self.assertIn('Geometry.Parse("M7.5,25.5 L16,6.8 L24.5,25.5")', pane)
        self.assertIn('Text = "Asistente BIM · Revit 2027"', pane)
        self.assertIn('Content = "Automático", Tag = null', pane)
        self.assertIn("CodexModelSelection.Save(GetSelectedModelId(), GetRequestedEffort())", pane)
        self.assertIn('Text = "Razonamiento"', pane)
        self.assertIn("Width = 125", pane)
        self.assertIn("Width = 96", pane)
        self.assertIn("private RichTextBox AddMessage(", pane)
        self.assertIn('CreateButton("Copiar"', pane)
        self.assertIn("private static void RenderMarkdown(RichTextBox target, string raw)", pane)
        self.assertIn("RichTextBox Detail", pane)
        self.assertIn("ModelEffortSelectionFallback", pane)
        self.assertIn("ModelDisplayName = isUser ? string.Empty : Clean(modelDisplayName, 120)", (EXTENSION / "ConversationHistoryStore.cs").read_text(encoding="utf-8"))
        self.assertIn("ButtonDisabledSurface", pane)
        self.assertIn("ButtonDisabledBorder", pane)
        self.assertIn("ButtonDisabledText", pane)


if __name__ == "__main__":
    unittest.main()
