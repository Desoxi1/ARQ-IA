using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Dynamo.Applications;

namespace BimAiDynamoBridge
{
    public sealed class BridgeApplication : IExternalApplication
    {
        internal const string PipeName = "BIM-AI-POC-Revit-27";
        internal const string RoutineId = "read_active_view_selection";
        internal const string ExpectedPythonHash = "6eac802870573eb2fad211fc30f1422caf0299979bf718c1d506d341c0fae17c";
        internal const string ExpectedGraphHash = "a4312143c1e0f6120fb65cf889873f19516bd3a84907777fe648d4fd536de9b8";
        internal static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        private static readonly ConcurrentQueue<PendingRequest> Requests = new ConcurrentQueue<PendingRequest>();
        private CancellationTokenSource _shutdown;
        private ExternalEvent _externalEvent;
        private string _routinesRoot;

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                _routinesRoot = ReadRoutinesRoot();
                var handler = new BridgeEventHandler(Requests, _routinesRoot);
                _externalEvent = ExternalEvent.Create(handler);
                _shutdown = new CancellationTokenSource();
                Task.Run(() => ListenAsync(_shutdown.Token));
                return Result.Succeeded;
            }
            catch
            {
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            try { _shutdown?.Cancel(); } catch { }
            try { _externalEvent?.Dispose(); } catch { }
            try { _shutdown?.Dispose(); } catch { }
            return Result.Succeeded;
        }

        private static string ReadRoutinesRoot()
        {
            var settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BIM-AI-POC",
                "bridge.json");
            using (var settings = JsonDocument.Parse(File.ReadAllText(settingsPath), new JsonDocumentOptions { MaxDepth = 4 }))
            {
                var root = Path.GetFullPath(settings.RootElement.GetProperty("routinesRoot").GetString());
                if (!String.Equals(Path.GetFileName(root), "routines", StringComparison.OrdinalIgnoreCase) || !Directory.Exists(root))
                    throw new InvalidDataException("Invalid routines root");
                return root;
            }
        }

        private async Task ListenAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using (var pipe = new NamedPipeServerStream(
                        PipeName,
                        PipeDirection.InOut,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly))
                    {
                        await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                        using (var reader = new StreamReader(pipe, new UTF8Encoding(false), false, 1024, true))
                        using (var writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, true) { AutoFlush = true })
                        {
                            var line = await ReadBoundedLineAsync(reader, cancellationToken).ConfigureAwait(false);
                            var request = ParseRequest(line);
                            if (request == null)
                            {
                                await writer.WriteLineAsync(JsonSerializer.Serialize(BridgeResponse.Fail("invalid_request"), JsonOptions)).ConfigureAwait(false);
                                continue;
                            }

                            var pending = new PendingRequest(request.Operation, request.RoutineId);
                            Requests.Enqueue(pending);
                            var raise = _externalEvent.Raise();
                            if (raise != ExternalEventRequest.Accepted && raise != ExternalEventRequest.Pending)
                            {
                                pending.Completion.TrySetResult(BridgeResponse.Fail("revit_dispatch_denied"));
                            }

                            BridgeResponse response;
                            try
                            {
                                response = await pending.Completion.Task.WaitAsync(TimeSpan.FromSeconds(90), cancellationToken).ConfigureAwait(false);
                            }
                            catch (TimeoutException)
                            {
                                response = BridgeResponse.Fail("revit_dispatch_timeout");
                            }
                            await writer.WriteLineAsync(JsonSerializer.Serialize(response, JsonOptions)).ConfigureAwait(false);
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch
                {
                    await Task.Delay(500, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private static async Task<string> ReadBoundedLineAsync(StreamReader reader, CancellationToken cancellationToken)
        {
            var value = new StringBuilder();
            var buffer = new char[1];
            while (value.Length <= 65536)
            {
                var count = await reader.ReadAsync(buffer, 0, 1).WaitAsync(cancellationToken).ConfigureAwait(false);
                if (count == 0) return value.Length == 0 ? null : value.ToString();
                if (buffer[0] == '\n') return value.ToString();
                if (buffer[0] != '\r') value.Append(buffer[0]);
            }
            throw new InvalidDataException("Request too large");
        }

        private static BridgeRequest ParseRequest(string json)
        {
            if (String.IsNullOrWhiteSpace(json)) return null;
            try
            {
                using (var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 4 }))
                {
                    var root = document.RootElement;
                    if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("operation", out var operation)) return null;
                    var op = operation.GetString();
                    var routineId = root.TryGetProperty("routineId", out var id) ? id.GetString() : null;
                    if (op != "ping" && op != "run_saved_readonly_routine") return null;
                    return new BridgeRequest(op, routineId);
                }
            }
            catch { return null; }
        }
    }

    internal sealed class PendingRequest
    {
        internal string Operation { get; }
        internal string RoutineId { get; }
        internal TaskCompletionSource<BridgeResponse> Completion { get; }

        internal PendingRequest(string operation, string routineId)
        {
            Operation = operation;
            RoutineId = routineId;
            Completion = new TaskCompletionSource<BridgeResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    internal sealed class BridgeRequest
    {
        internal string Operation { get; }
        internal string RoutineId { get; }
        internal BridgeRequest(string operation, string routineId) { Operation = operation; RoutineId = routineId; }
    }

    internal sealed class BridgeResponse
    {
        public bool Ok { get; set; }
        public string Status { get; set; }
        public string RoutineId { get; set; }
        public string ViewType { get; set; }
        public int? SelectionCount { get; set; }
        public string ModelChangeCheck { get; set; }
        public string RevitMajor { get; set; }
        public string DynamoAssemblyVersion { get; set; }

        internal static BridgeResponse Fail(string status) => new BridgeResponse { Ok = false, Status = status };
    }

    internal sealed class BridgeEventHandler : IExternalEventHandler
    {
        private readonly ConcurrentQueue<PendingRequest> _requests;
        private readonly string _routinesRoot;

        internal BridgeEventHandler(ConcurrentQueue<PendingRequest> requests, string routinesRoot)
        {
            _requests = requests;
            _routinesRoot = routinesRoot;
        }

        public void Execute(UIApplication application)
        {
            while (_requests.TryDequeue(out var request))
            {
                try
                {
                    request.Completion.TrySetResult(Handle(application, request));
                }
                catch
                {
                    request.Completion.TrySetResult(BridgeResponse.Fail("execution_failed"));
                }
            }
        }

        public string GetName() => "BIM-AI Dynamo read-only routine bridge";

        private BridgeResponse Handle(UIApplication application, PendingRequest request)
        {
            if (!String.Equals(application.Application.VersionNumber, "2027", StringComparison.Ordinal))
                return BridgeResponse.Fail("unsupported_revit_version");

            if (request.Operation == "ping")
            {
                return new BridgeResponse
                {
                    Ok = true,
                    Status = "ready",
                    RevitMajor = application.Application.VersionNumber,
                    DynamoAssemblyVersion = typeof(DynamoRevitCommandData).Assembly.GetName().Version?.ToString(),
                    RoutineId = BridgeApplication.RoutineId
                };
            }

            if (request.Operation != "run_saved_readonly_routine" || request.RoutineId != BridgeApplication.RoutineId)
                return BridgeResponse.Fail("routine_not_allowed");

            var uiDocument = application.ActiveUIDocument;
            if (uiDocument == null || uiDocument.Document == null) return BridgeResponse.Fail("no_active_document");
            var document = uiDocument.Document;
            if (document.IsModified || document.IsModifiable) return BridgeResponse.Fail("document_must_be_saved_and_idle");

            string pythonPath;
            string graphPath;
            try
            {
                pythonPath = InRoutinesRoot(BridgeApplication.RoutineId + ".py");
                graphPath = InRoutinesRoot(BridgeApplication.RoutineId + ".dyn");
                var manifestPath = InRoutinesRoot(BridgeApplication.RoutineId + ".manifest.json");
                ValidateSavedRoutine(pythonPath, graphPath, manifestPath);
            }
            catch
            {
                return BridgeResponse.Fail("routine_integrity_check_failed");
            }

            var beforeViewId = uiDocument.ActiveView.Id.Value;
            var beforeViewType = uiDocument.ActiveView.ViewType.ToString();
            var beforeSelectionCount = uiDocument.Selection.GetElementIds().Count;
            var beforeModified = document.IsModified;
            var runId = Guid.NewGuid().ToString("N");
            var projectRoot = Directory.GetParent(_routinesRoot).FullName;
            var resultRoot = Path.GetFullPath(Path.Combine(projectRoot, "logs", "runner-results"));
            if (!IsWithin(projectRoot, resultRoot)) return BridgeResponse.Fail("invalid_result_root");
            Directory.CreateDirectory(resultRoot);
            var resultPath = Path.GetFullPath(Path.Combine(resultRoot, runId + ".json"));
            if (!IsWithin(resultRoot, resultPath)) return BridgeResponse.Fail("invalid_result_path");

            var oldRunId = Environment.GetEnvironmentVariable("BIM_AI_RUN_ID", EnvironmentVariableTarget.Process);
            var oldResultRoot = Environment.GetEnvironmentVariable("BIM_AI_RESULT_DIR", EnvironmentVariableTarget.Process);
            try
            {
                Environment.SetEnvironmentVariable("BIM_AI_RUN_ID", runId, EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable("BIM_AI_RESULT_DIR", resultRoot, EnvironmentVariableTarget.Process);

                var journal = new Dictionary<string, string>
                {
                    [JournalKeys.AutomationModeKey] = "true",
                    [JournalKeys.ShowUiKey] = "false",
                    [JournalKeys.DynPathKey] = graphPath,
                    [JournalKeys.DynPathExecuteKey] = "true"
                };
                var commandData = new DynamoRevitCommandData { Application = application, JournalData = journal };
                var dynamoResult = new DynamoRevit().ExecuteCommand(commandData);
                if (dynamoResult != Result.Succeeded) return BridgeResponse.Fail("dynamo_command_failed");
                if (!File.Exists(resultPath)) return BridgeResponse.Fail("dynamo_result_missing");

                using (var resultDocument = JsonDocument.Parse(File.ReadAllText(resultPath), new JsonDocumentOptions { MaxDepth = 4 }))
                {
                    var result = resultDocument.RootElement;
                    var status = result.GetProperty("status").GetString();
                    var viewType = result.GetProperty("view_type").GetString();
                    var selectionCount = result.GetProperty("selection_count").GetInt32();
                    var unchanged = !document.IsModified && !document.IsModifiable &&
                        ReferenceEquals(application.ActiveUIDocument?.Document, document) &&
                        uiDocument.ActiveView.Id.Value == beforeViewId &&
                        uiDocument.Selection.GetElementIds().Count == beforeSelectionCount &&
                        beforeModified == document.IsModified &&
                        String.Equals(beforeViewType, viewType, StringComparison.Ordinal) &&
                        beforeSelectionCount == selectionCount;
                    if (status != "ok" || !unchanged) return BridgeResponse.Fail("post_run_verification_failed");
                    return new BridgeResponse
                    {
                        Ok = true,
                        Status = "executed_and_verified_read_only",
                        RoutineId = BridgeApplication.RoutineId,
                        ViewType = viewType,
                        SelectionCount = selectionCount,
                        ModelChangeCheck = "unchanged",
                        RevitMajor = application.Application.VersionNumber,
                        DynamoAssemblyVersion = typeof(DynamoRevitCommandData).Assembly.GetName().Version?.ToString()
                    };
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("BIM_AI_RUN_ID", oldRunId, EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable("BIM_AI_RESULT_DIR", oldResultRoot, EnvironmentVariableTarget.Process);
                try { if (File.Exists(resultPath)) File.Delete(resultPath); } catch { }
            }
        }

        private string InRoutinesRoot(string fileName)
        {
            var path = Path.GetFullPath(Path.Combine(_routinesRoot, fileName));
            if (!IsWithin(_routinesRoot, path)) throw new InvalidDataException("Routine path outside allowlist");
            return path;
        }

        private static bool IsWithin(string root, string path)
        {
            var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var normalizedPath = Path.GetFullPath(path);
            return normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
        }

        private static void ValidateSavedRoutine(string pythonPath, string graphPath, string manifestPath)
        {
            var pythonBytes = File.ReadAllBytes(pythonPath);
            var graphBytes = File.ReadAllBytes(graphPath);
            if (!String.Equals(ToHex(SHA256.HashData(pythonBytes)), BridgeApplication.ExpectedPythonHash, StringComparison.Ordinal) ||
                !String.Equals(ToHex(SHA256.HashData(graphBytes)), BridgeApplication.ExpectedGraphHash, StringComparison.Ordinal))
                throw new InvalidDataException("Unexpected routine hash");

            using (var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath), new JsonDocumentOptions { MaxDepth = 8 }))
            {
                var root = manifest.RootElement;
                if (root.GetProperty("name").GetString() != BridgeApplication.RoutineId ||
                    root.GetProperty("writes_model").GetBoolean() ||
                    root.GetProperty("python_sha256").GetString() != BridgeApplication.ExpectedPythonHash ||
                    root.GetProperty("dynamo_sha256").GetString() != BridgeApplication.ExpectedGraphHash)
                    throw new InvalidDataException("Unexpected routine manifest");
            }

            var pythonText = File.ReadAllText(pythonPath, Encoding.UTF8);
            using (var graph = JsonDocument.Parse(graphBytes, new JsonDocumentOptions { MaxDepth = 32 }))
            {
                var root = graph.RootElement;
                var nodes = root.GetProperty("Nodes");
                if (nodes.GetArrayLength() != 1 || root.GetProperty("Connectors").GetArrayLength() != 0 ||
                    root.GetProperty("Dependencies").GetArrayLength() != 0 ||
                    root.GetProperty("View").GetProperty("Dynamo").GetProperty("RunType").GetString() != "Manual")
                    throw new InvalidDataException("Unexpected graph shape");
                var node = nodes[0];
                if (node.GetProperty("Engine").GetString() != "PythonNet3" ||
                    node.GetProperty("Code").GetString() != pythonText)
                    throw new InvalidDataException("Unexpected graph code");
            }
        }

        private static string ToHex(byte[] bytes)
        {
            var value = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) value.Append(b.ToString("x2"));
            return value.ToString();
        }
    }
}
