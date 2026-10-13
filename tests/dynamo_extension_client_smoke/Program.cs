using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;

if (args.Length != 3)
{
    Console.WriteLine("Usage: DynamoExtensionClientSmoke <extension.dll> <dynamo-root> <project-root>");
    return 2;
}

var extensionPath = Path.GetFullPath(args[0]);
var dynamoRoot = Path.GetFullPath(args[1]);
var projectRoot = Path.GetFullPath(args[2]);
var routinesRoot = Path.Combine(projectRoot, "routines");
var routineName = "codex_probe_" + Guid.NewGuid().ToString("N")[..10];
var routinePaths = new[] { ".py", ".dyn", ".manifest.json" }
    .Select(suffix => Path.Combine(routinesRoot, routineName + suffix))
    .ToArray();
var attachmentProbeDirectory = Path.Combine(Path.GetTempPath(), "BIM-AI-POC-attachment-smoke-" + Guid.NewGuid().ToString("N"));
var attachmentStoreType = (Type?)null;
IDisposable? attachmentStore = null;
var exitCode = 0;

AssemblyLoadContext.Default.Resolving += (context, name) =>
{
    var dependency = Path.Combine(dynamoRoot, name.Name + ".dll");
    return File.Exists(dependency) ? context.LoadFromAssemblyPath(dependency) : null;
};

var extensionAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(extensionPath);
var clientType = extensionAssembly.GetType("BimAiDynamoAssistant.CodexAppServerClient", throwOnError: true)!;
var client = Activator.CreateInstance(clientType, nonPublic: true)!;
var text = new StringBuilder();
var statuses = new StringBuilder();
var textReceived = clientType.GetEvent("TextReceived")!;
textReceived.AddEventHandler(client, new Action<string>(delta => text.Append(delta)));
var statusChanged = clientType.GetEvent("StatusChanged")!;
statusChanged.AddEventHandler(client, new Action<string>(status => statuses.AppendLine(status)));

var createApprovalCount = 0;
var listApprovalCount = 0;
var approvalProperty = clientType.GetProperty("ApprovalRequested")!;
clientType.GetProperty("FullAccessEnabled")!.SetValue(client, true);
var approvalDelegateType = approvalProperty.PropertyType;
var requestType = approvalDelegateType.GenericTypeArguments[0];
var request = Expression.Parameter(requestType, "request");
var command = Expression.Property(request, "Command");
var reason = Expression.Property(request, "Reason");
var exactServer = Expression.Equal(command, Expression.Constant("Servidor MCP: bim_ai_routines"));
var isCreateRoutine = Expression.Call(reason, nameof(string.Contains), Type.EmptyTypes, Expression.Constant("create_dynamo_routine"));
var isListRoutines = Expression.Call(reason, nameof(string.Contains), Type.EmptyTypes, Expression.Constant("list_saved_routines"));
var approvedTool = Expression.OrElse(isCreateRoutine, isListRoutines);
var allowed = Expression.AndAlso(exactServer, approvedTool);
var recordApproval = new Action<string>(toolName =>
{
    if (toolName == "create") createApprovalCount++;
    else if (toolName == "list") listApprovalCount++;
});
var selectedTool = Expression.Condition(
    isCreateRoutine,
    Expression.Constant("create"),
    Expression.Condition(isListRoutines, Expression.Constant("list"), Expression.Constant("other")));
var fromResult = typeof(Task).GetMethods(BindingFlags.Public | BindingFlags.Static)
    .Single(method => method.Name == nameof(Task.FromResult) && method.IsGenericMethodDefinition)
    .MakeGenericMethod(typeof(bool));
var approvalBody = Expression.Block(
    Expression.Invoke(Expression.Constant(recordApproval), selectedTool),
    Expression.Call(fromResult, allowed));
approvalProperty.SetValue(client, Expression.Lambda(approvalDelegateType, approvalBody, request).Compile());

try
{
    if (routinePaths.Any(File.Exists))
        throw new IOException("Generated smoke-test routine path unexpectedly already exists.");

    var ensureStarted = (Task)clientType.GetMethod("EnsureStartedAsync")!.Invoke(client, new object?[] { projectRoot, null, null, CancellationToken.None })!;
    await ensureStarted;
    Console.WriteLine("CODEX_APP_SERVER_CSHARP_THREAD=PASS access=dangerFullAccess");

    var createPrompt = $"Use bim_ai_routines.create_dynamo_routine exactly once to save a read-only routine named {routineName}. Use description 'Temporary app-server integration smoke test; does not access Revit'. Use python_code exactly `OUT = \"APP_SERVER_GENERATED_PROBE\"`. Set scope to active_view, writes_model to false, and confirm_model_write to false. Do not call rvt_mcp, the Dynamo runner, shell, or any other tool. After saving, respond with the exact routine name.";
    var create = (Task)clientType.GetMethod("SendMessageAsync")!.Invoke(client, new object[] { createPrompt, CancellationToken.None })!;
    await create;

    if (createApprovalCount != 0)
    {
        var reply = text.ToString();
        Console.WriteLine($"CODEX_APP_SERVER_CSHARP_CREATE_DIAGNOSTICS statuses={statuses.ToString().Trim()}; reply={(reply.Length > 800 ? reply[^800..] : reply)}");
        throw new InvalidOperationException($"Full access should complete routine creation without approval prompts; saw {createApprovalCount}.");
    }
    if (listApprovalCount > 2)
        throw new InvalidOperationException($"Unexpected number of list calls during routine creation: {listApprovalCount}.");
    if (!routinePaths.All(File.Exists))
    {
        var assistantReply = text.ToString();
        var excerpt = assistantReply.Length > 400 ? assistantReply[^400..] : assistantReply;
        Console.WriteLine($"CODEX_APP_SERVER_CSHARP_CREATE_DIAGNOSTICS approvals=create:{createApprovalCount},list:{listApprovalCount}; statuses={statuses.ToString().Trim()}; reply={excerpt}");
        throw new FileNotFoundException("Codex completed without creating the complete routine package.");
    }

    var python = File.ReadAllText(routinePaths[0]);
    if (!python.Contains("OUT = \"APP_SERVER_GENERATED_PROBE\"", StringComparison.Ordinal))
        throw new InvalidDataException("Generated Python source did not contain the requested probe result.");
    using (var manifestDocument = JsonDocument.Parse(File.ReadAllText(routinePaths[2])))
    {
        var manifest = manifestDocument.RootElement;
        if (manifest.GetProperty("name").GetString() != routineName ||
            manifest.GetProperty("status").GetString() != "draft_not_validated" ||
            manifest.GetProperty("writes_model").GetBoolean())
            throw new InvalidDataException("Generated manifest does not match the requested read-only routine.");
        var sourceHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(routinePaths[0]))).ToLowerInvariant();
        if (manifest.GetProperty("python_sha256").GetString() != sourceHash)
            throw new InvalidDataException("Generated Python source does not match its manifest hash.");
    }
    using (var graphDocument = JsonDocument.Parse(File.ReadAllText(routinePaths[1])))
    {
        var graph = graphDocument.RootElement;
        var node = graph.GetProperty("Nodes")[0];
        if (node.GetProperty("Engine").GetString() != "PythonNet3" ||
            graph.GetProperty("View").GetProperty("Dynamo").GetProperty("RunType").GetString() != "Manual")
            throw new InvalidDataException("Generated Dynamo graph is not a Manual PythonNet3 graph.");
    }

    Console.WriteLine("CODEX_APP_SERVER_CSHARP_MCP_CREATE=PASS routine_saved=true writes_model=false approvals=0");

    text.Clear();
    var listApprovalBaseline = listApprovalCount;
    const string listPrompt = "Call bim_ai_routines.list_saved_routines exactly once. Report whether codex_probe_ is present and include the exact name of the probe routine from the current conversation. Do not call any other tool.";
    var list = (Task)clientType.GetMethod("SendMessageAsync")!.Invoke(client, new object[] { listPrompt, CancellationToken.None })!;
    await list;
    if (listApprovalCount != listApprovalBaseline ||
        !text.ToString().Contains(routineName, StringComparison.Ordinal))
        throw new InvalidOperationException("The saved routine was not listed through the full access app-server MCP connection without approval prompts.");

    Console.WriteLine("CODEX_APP_SERVER_CSHARP_APPROVAL=PASS approval_prompts=0");
    Console.WriteLine("CODEX_APP_SERVER_CSHARP_MCP_LIST=PASS saved_routine_returned=true approvals=0");

    Directory.CreateDirectory(attachmentProbeDirectory);
    var probeToken = "BIM_ATTACHMENT_PROBE_" + Guid.NewGuid().ToString("N")[..12];
    var imagePath = Path.Combine(attachmentProbeDirectory, "red-square.png");
    var textPath = Path.Combine(attachmentProbeDirectory, "reference.txt");
    var pixels = new byte[32 * 32 * 4];
    for (var index = 0; index < pixels.Length; index += 4)
    {
        pixels[index] = 0;
        pixels[index + 1] = 0;
        pixels[index + 2] = 255;
        pixels[index + 3] = 255;
    }
    var bitmap = BitmapSource.Create(32, 32, 96, 96, PixelFormats.Bgra32, null, pixels, 32 * 4);
    var encoder = new PngBitmapEncoder();
    encoder.Frames.Add(BitmapFrame.Create(bitmap));
    using (var imageStream = File.Create(imagePath)) encoder.Save(imageStream);
    File.WriteAllText(textPath, $"Reference code: {probeToken}", new UTF8Encoding(false));

    attachmentStoreType = extensionAssembly.GetType("BimAiDynamoAssistant.LocalAttachmentStore", throwOnError: true)!;
    attachmentStore = (IDisposable)Activator.CreateInstance(attachmentStoreType, new object[] { projectRoot })!;
    var addFile = attachmentStoreType.GetMethod("AddFile")!;
    var imageAttachment = addFile.Invoke(attachmentStore, new object[] { imagePath })!;
    var textAttachment = addFile.Invoke(attachmentStore, new object[] { textPath })!;
    var attachmentType = extensionAssembly.GetType("BimAiDynamoAssistant.LocalAttachment", throwOnError: true)!;
    var stagedAttachments = Array.CreateInstance(attachmentType, 2);
    stagedAttachments.SetValue(imageAttachment, 0);
    stagedAttachments.SetValue(textAttachment, 1);

    text.Clear();
    var sendWithAttachments = clientType.GetMethod("SendMessageWithAttachmentsAsync")!;
    var attachmentTurn = (Task)sendWithAttachments.Invoke(client, new object?[]
    {
        $"Inspect the attached image and text file. Reply with the square's color and the exact reference code. Do not call MCP tools or inspect other files.",
        stagedAttachments,
        null,
        null,
        CancellationToken.None
    })!;
    await attachmentTurn;
    var attachmentReply = text.ToString();
    if (!attachmentReply.Contains(probeToken, StringComparison.Ordinal) ||
        !(attachmentReply.Contains("red", StringComparison.OrdinalIgnoreCase) || attachmentReply.Contains("rojo", StringComparison.OrdinalIgnoreCase)))
    {
        var excerpt = attachmentReply.Length > 500 ? attachmentReply[^500..] : attachmentReply;
        throw new InvalidOperationException($"Codex did not correctly use both staged attachments: {excerpt}");
    }
    Console.WriteLine("CODEX_APP_SERVER_CSHARP_IMAGE_TEXT_ATTACHMENTS=PASS image_and_reference_read=true");
}
catch (Exception exception)
{
    Exception failure = (exception as TargetInvocationException)?.InnerException ?? exception;
    Console.WriteLine($"CODEX_APP_SERVER_CSHARP=FAIL error={failure.GetType().Name} detail={failure.Message[..Math.Min(700, failure.Message.Length)]}");
    exitCode = 1;
}
finally
{
    attachmentStore?.Dispose();
    if (Directory.Exists(attachmentProbeDirectory)) Directory.Delete(attachmentProbeDirectory, recursive: true);
    if (client is IAsyncDisposable disposable)
        await disposable.DisposeAsync();

    try
    {
        foreach (var path in routinePaths)
            if (File.Exists(path)) File.Delete(path);
        if (routinePaths.Any(File.Exists)) throw new IOException("Temporary smoke-test routine cleanup was incomplete.");
        Console.WriteLine("CODEX_APP_SERVER_CSHARP_CLEANUP=PASS temporary_routine_removed=true");
    }
    catch (Exception cleanupFailure)
    {
        Console.WriteLine($"CODEX_APP_SERVER_CSHARP_CLEANUP=FAIL error={cleanupFailure.GetType().Name}");
        exitCode = 1;
    }
}

return exitCode;
