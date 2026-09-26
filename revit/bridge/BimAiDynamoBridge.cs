using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
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
        internal const string ExpectedResultWrapperHash = "c4de58438c86d9987ca21e55e6e837cac42621d52dda24befc29d2e5b2582a43";
        internal static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        private static readonly ConcurrentQueue<PendingRequest> Requests = new ConcurrentQueue<PendingRequest>();
        private CancellationTokenSource _shutdown;
        private ExternalEvent _externalEvent;
        private string _routinesRoot;
        private string _testModelsRoot;

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                var settings = ReadBridgeSettings();
                _routinesRoot = settings.RoutinesRoot;
                _testModelsRoot = settings.TestModelsRoot;
                var handler = new BridgeEventHandler(Requests, _routinesRoot, _testModelsRoot);
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

        private static BridgeSettings ReadBridgeSettings()
        {
            var settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BIM-AI-POC",
                "bridge.json");
            using (var settings = JsonDocument.Parse(File.ReadAllText(settingsPath), new JsonDocumentOptions { MaxDepth = 4 }))
            {
                var root = Path.GetFullPath(settings.RootElement.GetProperty("routinesRoot").GetString());
                var expectedTestRoot = Path.GetFullPath(Path.Combine(Directory.GetParent(root).FullName, "revit", "test-models"));
                var testRoot = Path.GetFullPath(settings.RootElement.GetProperty("testModelsRoot").GetString());
                if (!String.Equals(Path.GetFileName(root), "routines", StringComparison.OrdinalIgnoreCase) || !Directory.Exists(root) ||
                    !String.Equals(testRoot, expectedTestRoot, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(testRoot) ||
                    BridgeEventHandler.ContainsReparsePoint(root) || BridgeEventHandler.ContainsReparsePoint(testRoot))
                    throw new InvalidDataException("Invalid routines root");
                return new BridgeSettings(root, testRoot);
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

                            var pending = new PendingRequest(request.Operation, request.RoutineId, request.ConfirmModelWrite);
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
                                var cancelledBeforeStart = pending.TryCancelBeforeStart();
                                response = BridgeResponse.Fail(cancelledBeforeStart ? "revit_dispatch_timeout_before_execution" : "revit_execution_timeout_unknown");
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
                    var confirmModelWrite = false;
                    if (op == "run_saved_routine")
                    {
                        if (!root.TryGetProperty("confirmModelWrite", out var confirmation) ||
                            (confirmation.ValueKind != JsonValueKind.True && confirmation.ValueKind != JsonValueKind.False)) return null;
                        confirmModelWrite = confirmation.GetBoolean();
                    }
                    if (op != "ping" && op != "run_saved_readonly_routine" && op != "run_saved_routine") return null;
                    return new BridgeRequest(op, routineId, confirmModelWrite);
                }
            }
            catch { return null; }
        }
    }

    internal sealed class BridgeSettings
    {
        internal string RoutinesRoot { get; }
        internal string TestModelsRoot { get; }
        internal BridgeSettings(string routinesRoot, string testModelsRoot) { RoutinesRoot = routinesRoot; TestModelsRoot = testModelsRoot; }
    }

    internal sealed class PendingRequest
    {
        internal string Operation { get; }
        internal string RoutineId { get; }
        internal bool ConfirmModelWrite { get; }
        internal TaskCompletionSource<BridgeResponse> Completion { get; }
        private int _state;

        internal PendingRequest(string operation, string routineId, bool confirmModelWrite)
        {
            Operation = operation;
            RoutineId = routineId;
            ConfirmModelWrite = confirmModelWrite;
            Completion = new TaskCompletionSource<BridgeResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        internal bool TryStart() => Interlocked.CompareExchange(ref _state, 1, 0) == 0;
        internal bool TryCancelBeforeStart() => Interlocked.CompareExchange(ref _state, 2, 0) == 0;
        internal void MarkComplete() => Interlocked.CompareExchange(ref _state, 3, 1);
    }

    internal sealed class BridgeRequest
    {
        internal string Operation { get; }
        internal string RoutineId { get; }
        internal bool ConfirmModelWrite { get; }
        internal BridgeRequest(string operation, string routineId, bool confirmModelWrite) { Operation = operation; RoutineId = routineId; ConfirmModelWrite = confirmModelWrite; }
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
        public long[] ChangedElementIds { get; set; }
        public object RoutineResult { get; set; }

        internal static BridgeResponse Fail(string status) => new BridgeResponse { Ok = false, Status = status };
    }

    internal sealed class BridgeEventHandler : IExternalEventHandler
    {
        private readonly ConcurrentQueue<PendingRequest> _requests;
        private readonly string _routinesRoot;
        private readonly string _testModelsRoot;

        internal BridgeEventHandler(ConcurrentQueue<PendingRequest> requests, string routinesRoot, string testModelsRoot)
        {
            _requests = requests;
            _routinesRoot = routinesRoot;
            _testModelsRoot = testModelsRoot;
        }

        public void Execute(UIApplication application)
        {
            while (_requests.TryDequeue(out var request))
            {
                if (!request.TryStart())
                {
                    request.Completion.TrySetResult(BridgeResponse.Fail("revit_dispatch_timeout_before_execution"));
                    continue;
                }
                try
                {
                    request.Completion.TrySetResult(Handle(application, request));
                }
                catch
                {
                    request.Completion.TrySetResult(BridgeResponse.Fail("execution_failed"));
                }
                finally
                {
                    request.MarkComplete();
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

            if (request.Operation == "run_saved_routine") return HandleSavedRoutine(application, request);

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

                if (new FileInfo(resultPath).Length > 65536) return BridgeResponse.Fail("routine_result_invalid");
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

        private BridgeResponse HandleSavedRoutine(UIApplication application, PendingRequest request)
        {
            if (String.IsNullOrWhiteSpace(request.RoutineId) || !Regex.IsMatch(request.RoutineId, "^[a-z][a-z0-9_-]{1,47}$", RegexOptions.CultureInvariant))
                return BridgeResponse.Fail("routine_id_invalid");

            var uiDocument = application.ActiveUIDocument;
            if (uiDocument == null || uiDocument.Document == null) return BridgeResponse.Fail("no_active_document");
            var document = uiDocument.Document;
            if (document.IsModified || document.IsModifiable) return BridgeResponse.Fail("document_must_be_saved_and_idle");
            if (!IsTestModel(document, _testModelsRoot)) return BridgeResponse.Fail("test_model_required");

            string pythonPath;
            string graphPath;
            string manifestPath;
            bool writesModel;
            try
            {
                pythonPath = InRoutinesRoot(request.RoutineId + ".py");
                graphPath = InRoutinesRoot(request.RoutineId + ".dyn");
                manifestPath = InRoutinesRoot(request.RoutineId + ".manifest.json");
                writesModel = ValidateGeneratedRoutine(request.RoutineId, pythonPath, graphPath, manifestPath);
            }
            catch
            {
                return BridgeResponse.Fail("routine_integrity_check_failed");
            }
            // PythonNet3 is full trust; manifest metadata cannot prove that a script is read-only.
            if (!request.ConfirmModelWrite) return BridgeResponse.Fail("generic_execution_confirmation_required");
            if (document.IsWorkshared) return BridgeResponse.Fail("model_must_be_local_non_workshared");

            var beforeViewId = uiDocument.ActiveView.Id.Value;
            var beforeSelection = uiDocument.Selection.GetElementIds().Select(id => id.Value).OrderBy(id => id).ToArray();
            var projectRoot = Directory.GetParent(_routinesRoot).FullName;
            var resultRoot = Path.GetFullPath(Path.Combine(projectRoot, "logs", "runner-results"));
            if (!IsWithin(projectRoot, resultRoot)) return BridgeResponse.Fail("invalid_result_root");
            Directory.CreateDirectory(resultRoot);
            var runId = Guid.NewGuid().ToString("N");
            var resultPath = Path.GetFullPath(Path.Combine(resultRoot, runId + ".json"));
            if (!IsWithin(resultRoot, resultPath)) return BridgeResponse.Fail("invalid_result_path");

            var changedIds = new SortedSet<long>();
            EventHandler<DocumentChangedEventArgs> changedHandler = (sender, args) =>
            {
                AddChangedIds(changedIds, args.GetAddedElementIds());
                AddChangedIds(changedIds, args.GetModifiedElementIds());
                AddChangedIds(changedIds, args.GetDeletedElementIds());
            };
            var oldRunId = Environment.GetEnvironmentVariable("BIM_AI_RUN_ID", EnvironmentVariableTarget.Process);
            var oldResultPath = Environment.GetEnvironmentVariable("BIM_AI_RESULT_PATH", EnvironmentVariableTarget.Process);
            application.Application.DocumentChanged += changedHandler;
            try
            {
                Environment.SetEnvironmentVariable("BIM_AI_RUN_ID", runId, EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable("BIM_AI_RESULT_PATH", resultPath, EnvironmentVariableTarget.Process);
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
                if (new FileInfo(resultPath).Length > 65536) return BridgeResponse.Fail("routine_result_invalid");

                using (var resultDocument = JsonDocument.Parse(File.ReadAllText(resultPath), new JsonDocumentOptions { MaxDepth = 20 }))
                {
                    var root = resultDocument.RootElement;
                    if (root.GetProperty("status").GetString() != "ok") return BridgeResponse.Fail("routine_reported_failure");
                    if (!root.TryGetProperty("routine_result", out var routineResult)) return BridgeResponse.Fail("routine_result_invalid");
                    var selectionAfter = uiDocument.Selection.GetElementIds().Select(id => id.Value).OrderBy(id => id).ToArray();
                    if (uiDocument.ActiveView.Id.Value != beforeViewId || !beforeSelection.SequenceEqual(selectionAfter) || document.IsModifiable)
                        return BridgeResponse.Fail("post_run_verification_failed");

                    if (writesModel)
                    {
                        if (!TryGetDeclaredAffectedIds(routineResult, out var declaredIds)) return BridgeResponse.Fail("routine_result_invalid");
                        if (!changedIds.SetEquals(declaredIds)) return BridgeResponse.Fail("post_run_change_scope_verification_failed");
                        if (changedIds.Count > 0 && !document.IsModified) return BridgeResponse.Fail("model_write_not_detected");
                        return new BridgeResponse
                        {
                            Ok = true,
                            Status = changedIds.Count == 0 ? "executed_and_verified_no_change" : "executed_and_verified_change",
                            RoutineId = request.RoutineId,
                            ModelChangeCheck = "document_changed_ids_match_routine_result",
                            ChangedElementIds = changedIds.ToArray(),
                            RoutineResult = routineResult.Clone(),
                            RevitMajor = application.Application.VersionNumber,
                            DynamoAssemblyVersion = typeof(DynamoRevitCommandData).Assembly.GetName().Version?.ToString()
                        };
                    }

                    if (document.IsModified || changedIds.Count != 0) return BridgeResponse.Fail("post_run_verification_failed");
                    return new BridgeResponse
                    {
                        Ok = true,
                        Status = "executed_and_verified_read_only",
                        RoutineId = request.RoutineId,
                        ModelChangeCheck = "unchanged",
                        ChangedElementIds = Array.Empty<long>(),
                        RoutineResult = routineResult.Clone(),
                        RevitMajor = application.Application.VersionNumber,
                        DynamoAssemblyVersion = typeof(DynamoRevitCommandData).Assembly.GetName().Version?.ToString()
                    };
                }
            }
            catch
            {
                return BridgeResponse.Fail("routine_result_invalid");
            }
            finally
            {
                application.Application.DocumentChanged -= changedHandler;
                Environment.SetEnvironmentVariable("BIM_AI_RUN_ID", oldRunId, EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable("BIM_AI_RESULT_PATH", oldResultPath, EnvironmentVariableTarget.Process);
                try { if (File.Exists(resultPath)) File.Delete(resultPath); } catch { }
                try { if (File.Exists(resultPath + ".tmp")) File.Delete(resultPath + ".tmp"); } catch { }
            }
        }

        private static bool IsTestModel(Document document, string testModelsRoot)
        {
            try
            {
                if (String.IsNullOrWhiteSpace(document.PathName) || !File.Exists(document.PathName) ||
                    !String.Equals(Path.GetExtension(document.PathName), ".rvt", StringComparison.OrdinalIgnoreCase)) return false;
                if (ContainsReparsePoint(document.PathName) || ContainsReparsePoint(testModelsRoot)) return false;
                return IsWithin(testModelsRoot, Path.GetFullPath(document.PathName));
            }
            catch { return false; }
        }

        private static void AddChangedIds(ISet<long> destination, ICollection<ElementId> source)
        {
            foreach (var id in source) destination.Add(id.Value);
        }

        internal static bool ContainsReparsePoint(string path)
        {
            try
            {
                var fullPath = Path.GetFullPath(path);
                if ((File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0) return true;
                var current = new DirectoryInfo(Path.GetDirectoryName(fullPath));
                while (current != null)
                {
                    if ((current.Attributes & FileAttributes.ReparsePoint) != 0) return true;
                    current = current.Parent;
                }
                return false;
            }
            catch { return true; }
        }

        private static bool TryGetDeclaredAffectedIds(JsonElement routineResult, out HashSet<long> ids)
        {
            ids = new HashSet<long>();
            if (routineResult.ValueKind != JsonValueKind.Object || !routineResult.TryGetProperty("affected_ids", out var values) || values.ValueKind != JsonValueKind.Array)
                return false;
            foreach (var value in values.EnumerateArray())
            {
                long id;
                if (value.ValueKind == JsonValueKind.String)
                {
                    if (!Int64.TryParse(value.GetString(), out id)) return false;
                }
                else if (value.ValueKind == JsonValueKind.Number)
                {
                    if (!value.TryGetInt64(out id)) return false;
                }
                else return false;
                if (!ids.Add(id)) return false;
            }
            return true;
        }

        private bool ValidateGeneratedRoutine(string routineId, string pythonPath, string graphPath, string manifestPath)
        {
            var wrapperPath = Path.Combine(Directory.GetParent(_routinesRoot).FullName, "dynamo", "pythonnet3_result_wrapper.py");
            if (new FileInfo(pythonPath).Length > 32768 || new FileInfo(graphPath).Length > 524288 ||
                new FileInfo(manifestPath).Length > 16384 || new FileInfo(wrapperPath).Length > 32768)
                throw new InvalidDataException("Routine package exceeds size limits");
            var pythonBytes = File.ReadAllBytes(pythonPath);
            var graphBytes = File.ReadAllBytes(graphPath);
            var wrapperBytes = File.ReadAllBytes(wrapperPath);
            var pythonHash = ToHex(SHA256.HashData(pythonBytes));
            var graphHash = ToHex(SHA256.HashData(graphBytes));
            var wrapperHash = ToHex(SHA256.HashData(wrapperBytes));
            if (wrapperHash != BridgeApplication.ExpectedResultWrapperHash) throw new InvalidDataException("Unexpected result wrapper");

            using (var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath), new JsonDocumentOptions { MaxDepth = 12 }))
            {
                var root = manifest.RootElement;
                var writesModel = root.GetProperty("writes_model").GetBoolean();
                if (root.GetProperty("name").GetString() != routineId ||
                    root.GetProperty("python_engine").GetString() != "PythonNet3" ||
                    root.GetProperty("result_contract").GetString() != "bim-ai-json-v1" ||
                    root.GetProperty("confirm_model_write").GetBoolean() != writesModel ||
                    root.GetProperty("runtime_wrapper_sha256").GetString() != wrapperHash ||
                    root.GetProperty("python_sha256").GetString() != pythonHash ||
                    root.GetProperty("dynamo_sha256").GetString() != graphHash)
                    throw new InvalidDataException("Routine manifest integrity failed");

                var pythonText = new UTF8Encoding(false, true).GetString(pythonBytes).Replace("\r\n", "\n").Replace("\r", "\n");
                var wrapperText = new UTF8Encoding(false, true).GetString(wrapperBytes);
                var expectedCode = pythonText.TrimEnd() + "\n\n" + wrapperText.TrimStart();
                using (var graph = JsonDocument.Parse(graphBytes, new JsonDocumentOptions { MaxDepth = 32 }))
                {
                    var graphRoot = graph.RootElement;
                    var nodes = graphRoot.GetProperty("Nodes");
                    if (nodes.GetArrayLength() != 1 || graphRoot.GetProperty("Connectors").GetArrayLength() != 0 ||
                        graphRoot.GetProperty("Dependencies").GetArrayLength() != 0 ||
                        graphRoot.GetProperty("View").GetProperty("Dynamo").GetProperty("RunType").GetString() != "Manual")
                        throw new InvalidDataException("Unexpected graph shape");
                    var node = nodes[0];
                    if (node.GetProperty("Engine").GetString() != "PythonNet3" || node.GetProperty("Code").GetString() != expectedCode)
                        throw new InvalidDataException("Unexpected graph source");
                }
                return writesModel;
            }
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
