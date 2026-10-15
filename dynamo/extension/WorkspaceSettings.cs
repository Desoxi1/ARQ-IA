using System.IO;
using System.Text.Json;

namespace BimAiDynamoAssistant;

/// <summary>Stores the selected Codex working folder in the current Windows profile.</summary>
internal static class WorkspaceSettings
{
    private sealed record Settings(int SchemaVersion, string WorkspacePath);

    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ARQ-IA",
        "workspace.json");

    internal static string? Load(string fallbackRoot)
    {
        try
        {
            var path = SettingsPath;
            if (!File.Exists(path) || new FileInfo(path).Length > 16 * 1024) return fallbackRoot;
            var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path));
            if (settings is null || settings.SchemaVersion != 1 || string.IsNullOrWhiteSpace(settings.WorkspacePath)) return fallbackRoot;
            var selected = Path.GetFullPath(settings.WorkspacePath);
            return Directory.Exists(selected) ? selected : fallbackRoot;
        }
        catch
        {
            return fallbackRoot;
        }
    }

    internal static void Save(string workspacePath)
    {
        var selected = Path.GetFullPath(workspacePath);
        if (!Directory.Exists(selected)) throw new DirectoryNotFoundException("La carpeta de trabajo seleccionada no existe.");

        var path = SettingsPath;
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var temporary = path + ".tmp";
        var json = JsonSerializer.Serialize(new Settings(1, selected), new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(temporary, json);
        File.Move(temporary, path, overwrite: true);
    }
}
