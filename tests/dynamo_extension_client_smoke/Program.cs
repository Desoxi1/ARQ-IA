using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;

if (args.Length != 3)
{
    Console.WriteLine("Usage: DynamoExtensionClientSmoke <extension.dll> <dynamo-root> <project-root>");
    return 2;
}

var extensionPath = Path.GetFullPath(args[0]);
var dynamoRoot = Path.GetFullPath(args[1]);
var projectRoot = Path.GetFullPath(args[2]);
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

var approvalCount = 0;
var approvalProperty = clientType.GetProperty("ApprovalRequested")!;
var approvalDelegateType = approvalProperty.PropertyType;
var requestType = approvalDelegateType.GenericTypeArguments[0];
var request = Expression.Parameter(requestType, "request");
var command = Expression.Property(request, "Command");
var reason = Expression.Property(request, "Reason");
var exactServer = Expression.Equal(command, Expression.Constant("Servidor MCP: bim_ai_routines"));
var listOnly = Expression.Call(reason, nameof(string.Contains), Type.EmptyTypes, Expression.Constant("list_saved_routines"));
var allowed = Expression.AndAlso(exactServer, listOnly);
var recordApproval = new Action(() => approvalCount++);
var fromResult = typeof(Task).GetMethods(BindingFlags.Public | BindingFlags.Static)
    .Single(method => method.Name == nameof(Task.FromResult) && method.IsGenericMethodDefinition)
    .MakeGenericMethod(typeof(bool));
var approvalBody = Expression.Block(
    Expression.Invoke(Expression.Constant(recordApproval)),
    Expression.Call(fromResult, allowed));
approvalProperty.SetValue(client, Expression.Lambda(approvalDelegateType, approvalBody, request).Compile());

try
{
    var ensureStarted = (Task)clientType.GetMethod("EnsureStartedAsync")!.Invoke(client, new object[] { projectRoot, CancellationToken.None })!;
    await ensureStarted;
    Console.WriteLine("CODEX_APP_SERVER_CSHARP_THREAD=PASS");

    const string prompt = "Llama a bim_ai_routines.list_saved_routines. Responde únicamente con RUTINAS=<cantidad>. No uses herramientas que escriban ni accedas a Revit.";
    var send = (Task)clientType.GetMethod("SendMessageAsync")!.Invoke(client, new object[] { prompt, CancellationToken.None })!;
    await send;

    if (approvalCount != 1)
    {
        Console.WriteLine($"CODEX_APP_SERVER_CSHARP_APPROVAL=FAIL count={approvalCount}");
        return 1;
    }
    if (!text.ToString().Replace(" ", "", StringComparison.Ordinal).Contains("RUTINAS=3", StringComparison.Ordinal))
    {
        Console.WriteLine("CODEX_APP_SERVER_CSHARP_MCP=FAIL expected_result_not_received");
        return 1;
    }

    Console.WriteLine("CODEX_APP_SERVER_CSHARP_APPROVAL=PASS read_only_tool_approved_once=true");
    Console.WriteLine("CODEX_APP_SERVER_CSHARP_MCP=PASS routines=3");
    return 0;
}
catch (Exception exception)
{
    Exception failure = (exception as TargetInvocationException)?.InnerException ?? exception;
    Console.WriteLine($"CODEX_APP_SERVER_CSHARP=FAIL error={failure.GetType().Name} detail={failure.Message[..Math.Min(180, failure.Message.Length)]}");
    return 1;
}
finally
{
    if (client is IAsyncDisposable disposable)
        await disposable.DisposeAsync();
}
