using System.IO;
using System.Text.Json;

namespace BimAiDynamoAssistant;

/// <summary>Persistencia local del ID real de modelo elegido en el panel.</summary>
internal static class CodexModelSelection
{
    private static string SelectionPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ARQ-IA", "model-selection.json");

    public static string? Load()
    {
        try
        {
            var path = SelectionPath();
            if (!File.Exists(path)) return null;
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("model", out var value)) return null;
            return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        }
        catch
        {
            return null;
        }
    }

    public static string? LoadEffort(string? model)
    {
        try
        {
            var path = SelectionPath();
            if (!File.Exists(path)) return null;
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            if (!root.TryGetProperty("effortModel", out var storedModel) ||
                storedModel.ValueKind != JsonValueKind.String ||
                !string.Equals(storedModel.GetString(), model ?? "auto", StringComparison.Ordinal)) return null;
            if (!root.TryGetProperty("effort", out var value)) return null;
            return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        }
        catch
        {
            return null;
        }
    }

    public static void Save(string? model, string? effort)
    {
        try
        {
            var path = SelectionPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(new { model, effort, effortModel = model ?? "auto" },
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // La preferencia es opcional; no bloquea el chat.
        }
    }
}
