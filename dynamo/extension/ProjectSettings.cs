using System.IO;
using System.Text.Json;

namespace BimAiDynamoAssistant;

internal static class ProjectSettings
{
    public static string? TryGetProjectRoot()
    {
        try
        {
            var settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BIM-AI-POC",
                "bridge.json");

            using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
            var routinesRoot = document.RootElement.GetProperty("routinesRoot").GetString();
            if (string.IsNullOrWhiteSpace(routinesRoot)) return null;

            var fullRoutinesRoot = Path.GetFullPath(routinesRoot);
            if (!Directory.Exists(fullRoutinesRoot) ||
                !string.Equals(Path.GetFileName(fullRoutinesRoot), "routines", StringComparison.OrdinalIgnoreCase))
                return null;

            var projectRoot = Directory.GetParent(fullRoutinesRoot)?.FullName;
            return projectRoot is not null &&
                File.Exists(Path.Combine(projectRoot, "AGENTS.md")) &&
                File.Exists(Path.Combine(projectRoot, ".codex", "config.toml.example")) &&
                File.Exists(Path.Combine(projectRoot, "dynamo", "extension", "README.md")) &&
                Directory.Exists(Path.Combine(projectRoot, "mcp"))
                ? projectRoot
                : null;
        }
        catch
        {
            return null;
        }
    }
}
