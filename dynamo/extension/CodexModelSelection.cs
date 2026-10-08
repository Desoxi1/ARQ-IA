using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BimAiDynamoAssistant;

/// <summary>Nivel de costo elegido por el usuario para las llamadas a Codex.</summary>
internal enum ModelTier
{
    Economico,
    Estandar,
    Potente
}

/// <summary>
/// Persistencia por proyecto del nivel elegido. No usa .codex/config.toml
/// porque ese archivo lo regenera setup.ps1; aquí solo vive preferencia UI.
/// </summary>
internal static class CodexModelSelection
{
    private const ModelTier DefaultTier = ModelTier.Estandar;

    public static string EffortFor(ModelTier tier) => tier switch
    {
        ModelTier.Economico => "low",
        ModelTier.Potente => "high",
        _ => "medium",
    };

    public static string LabelFor(ModelTier tier) => tier switch
    {
        ModelTier.Economico => "Económico · lecturas y resúmenes",
        ModelTier.Potente => "Potente · tareas compuestas o tras un fallo",
        _ => "Estándar · rutinas y cambios (recomendado)",
    };

    public static string HintFor(ModelTier tier) => tier switch
    {
        ModelTier.Economico => "Vista, selección y preguntas usan menos cuota.",
        ModelTier.Potente => "Solo cuando Estándar no resolvió; verifica por etapas.",
        _ => "Crear rutinas y ediciones acotadas con verificación.",
    };

    public static ModelTier Load(string? projectRoot)
    {
        try
        {
            var path = SelectionPath();
            if (path is null || !File.Exists(path)) return DefaultTier;
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var key = ProjectKey(projectRoot);
            if (key is null) return DefaultTier;
            if (!document.RootElement.TryGetProperty(key, out var entry)) return DefaultTier;
            if (entry.TryGetProperty("tier", out var tierValue) &&
                Enum.TryParse<ModelTier>(tierValue.GetString(), out var tier) &&
                Enum.IsDefined(tier))
                return tier;
        }
        catch
        {
            // Preferencia ilegible: usar el valor por defecto sin romper el panel.
        }
        return DefaultTier;
    }

    public static void Save(string? projectRoot, ModelTier tier)
    {
        try
        {
            var path = SelectionPath();
            if (path is null) return;
            var key = ProjectKey(projectRoot);
            if (key is null) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            Dictionary<string, object> all = new(StringComparer.Ordinal);
            if (File.Exists(path))
            {
                try
                {
                    using var document = JsonDocument.Parse(File.ReadAllText(path));
                    foreach (var property in document.RootElement.EnumerateObject())
                        all[property.Name] = property.Value.GetRawText();
                }
                catch
                {
                    // Archivo corrupto: se reescribe solo con la selección actual.
                }
            }
            all[key] = JsonSerializer.Serialize(new { tier = tier.ToString() });
            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
            writer.WriteStartObject();
            foreach (var pair in all)
            {
                writer.WritePropertyName(pair.Key);
                if (pair.Value is string raw)
                {
                    using var fragment = JsonDocument.Parse(raw);
                    fragment.RootElement.WriteTo(writer);
                }
            }
            writer.WriteEndObject();
        }
        catch
        {
            // No bloquear el envío si la preferencia no se pudo guardar.
        }
    }

    /// <summary>
    /// Pin opcional de modelo exacto para usuarios avanzados. Si el archivo de
    /// selección contiene "model" para este proyecto, se envía a app-server;
    /// en caso contrario solo se usa effort por nivel. Nunca se hardcodean IDs.
    /// </summary>
    public static string? PinnedModel(string? projectRoot)
    {
        try
        {
            var path = SelectionPath();
            if (path is null || !File.Exists(path)) return null;
            var key = ProjectKey(projectRoot);
            if (key is null) return null;
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty(key, out var entry)) return null;
            if (entry.TryGetProperty("model", out var modelValue))
            {
                var model = modelValue.GetString();
                return string.IsNullOrWhiteSpace(model) ? null : model.Trim();
            }
        }
        catch
        {
        }
        return null;
    }

    private static string? SelectionPath()
    {
        try
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BIM-AI-POC",
                "model-selection.json");
        }
        catch
        {
            return null;
        }
    }

    private static string? ProjectKey(string? projectRoot)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(projectRoot)) return null;
            var normalized = Path.GetFullPath(projectRoot).TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToUpperInvariant();
            var hash = SHA1.HashData(Encoding.UTF8.GetBytes(normalized));
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
        catch
        {
            return null;
        }
    }
}
