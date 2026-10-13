using System.Text.Json;

namespace BimAiDynamoAssistant;

internal sealed record McpToolActivity(string Id, string Server, string Tool, string Status, string? Detail);

internal static class Program
{
    private static int Main()
    {
        var root = Path.Combine(Path.GetTempPath(), "ARQ-IA-history-smoke-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new ConversationHistoryStore(root);
            var record = store.Create("gpt-test-model");
            store.AddMessage(record, "Tú", "Resume los niveles y materiales del modelo.", true,
                new[] { @"C:\Users\Example\Private\Plano A.png" });
            store.AddMessage(record, "ARQ-IA", "La ficha se consultó en modo de solo lectura.", false);
            store.UpsertTool(record, new McpToolActivity("tool-1", "rvt_mcp", "revit_get_model_statistics", "completed", "{\"categories\":4}"));

            var reloadedStore = new ConversationHistoryStore(root);
            var listed = reloadedStore.ListRecent();
            var loaded = reloadedStore.Load(record.Id);
            if (reloadedStore.Count != 1 || listed.Count != 1 || loaded is null || loaded.Entries.Count != 3)
                throw new InvalidOperationException("Conversation history did not survive a reload.");
            if (loaded.Title != "Resume los niveles y materiales del modelo.")
                throw new InvalidOperationException("Conversation title was not derived from the first prompt.");

            var jsonPath = Path.Combine(root, ".arq-ia", "conversations", record.Id + ".json");
            var json = File.ReadAllText(jsonPath);
            using var parsed = JsonDocument.Parse(json);
            if (json.Contains("C:\\Users\\Example", StringComparison.Ordinal) ||
                json.Contains("Private\\Plano A.png", StringComparison.Ordinal))
                throw new InvalidOperationException("History stored an attachment path instead of its file name.");
            if (!json.Contains("Plano A.png", StringComparison.Ordinal))
                throw new InvalidOperationException("History did not preserve the attachment name.");

            var continuation = ConversationHistoryStore.BuildContinuationContext(loaded);
            if (!continuation.Contains("Resume", StringComparison.Ordinal) ||
                continuation.Contains("Private", StringComparison.Ordinal))
                throw new InvalidOperationException("Conversation continuation context is incomplete or leaks a path.");
            if (reloadedStore.Load("../outside") is not null)
                throw new InvalidOperationException("History accepted a path-shaped conversation id.");

            Console.WriteLine("ARQ_IA_LOCAL_CONVERSATION_HISTORY=PASS reload=true attachment_content=not_saved path=project_local");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"ARQ_IA_LOCAL_CONVERSATION_HISTORY=FAIL {exception.GetType().Name}: {exception.Message}");
            return 1;
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
