using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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
var textReceived = clientType.GetEvent("TextReceived")!;
textReceived.AddEventHandler(client, new Action<string>(delta => text.Append(delta)));

var createApprovalCount = 0;
var listApprovalCount = 0;
var approvalProperty = clientType.GetProperty("ApprovalRequested")!;
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

    var ensureStarted = (Task)clientType.GetMethod("EnsureStartedAsync")!.Invoke(client, new object[] { projectRoot, CancellationToken.None })!;
    await ensureStarted;
    Console.WriteLine("CODEX_APP_SERVER_CSHARP_THREAD=PASS");

    var createPrompt = $"Use bim_ai_routines.create_dynamo_routine exactly once to save a read-only routine named {routineName}. Use description 'Temporary app-server integration smoke test; does not access Revit'. Use python_code exactly `OUT = \"APP_SERVER_GENERATED_PROBE\"`. Set scope to active_view, writes_model to false, and confirm_model_write to false. Do not call rvt_mcp, the Dynamo runner, shell, or any other tool. After saving, respond with the exact routine name.";
    var create = (Task)clientType.GetMethod("SendMessageAsync")!.Invoke(client, new object[] { createPrompt, CancellationToken.None })!;
    await create;

    if (createApprovalCount != 1)
        throw new InvalidOperationException($"Expected one approved routine creation call; got {createApprovalCount}.");
    if (listApprovalCount > 2)
        throw new InvalidOperationException($"Unexpected number of list calls during routine creation: {listApprovalCount}.");
    if (!routinePaths.All(File.Exists))
        throw new FileNotFoundException("Codex completed without creating the complete routine package.");

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

    Console.WriteLine("CODEX_APP_SERVER_CSHARP_MCP_CREATE=PASS routine_saved=true writes_model=false");

    text.Clear();
    var listApprovalBaseline = listApprovalCount;
    const string listPrompt = "Call bim_ai_routines.list_saved_routines exactly once. Report whether codex_probe_ is present and include the exact name of the probe routine from the current conversation. Do not call any other tool.";
    var list = (Task)clientType.GetMethod("SendMessageAsync")!.Invoke(client, new object[] { listPrompt, CancellationToken.None })!;
    await list;
    if (listApprovalCount != listApprovalBaseline + 1 ||
        !text.ToString().Contains(routineName, StringComparison.Ordinal))
        throw new InvalidOperationException("The saved routine was not listed through the app-server MCP connection.");

    Console.WriteLine("CODEX_APP_SERVER_CSHARP_APPROVAL=PASS create_and_list_approved=true");
    Console.WriteLine("CODEX_APP_SERVER_CSHARP_MCP_LIST=PASS saved_routine_returned=true");
}
catch (Exception exception)
{
    Exception failure = (exception as TargetInvocationException)?.InnerException ?? exception;
    Console.WriteLine($"CODEX_APP_SERVER_CSHARP=FAIL error={failure.GetType().Name} detail={failure.Message[..Math.Min(220, failure.Message.Length)]}");
    exitCode = 1;
}
finally
{
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
