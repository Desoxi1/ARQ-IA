using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
        internal const string ExpectedPythonHash = "c19f52b3796031c6b42526269c5dd057efdfc6e5362d330c93702144a53d2837";
        internal const string ExpectedGraphHash = "62032ba04a950f6d0b09e57364c0f55426187439b521cd3a9863e927175fae85";
        internal const string ExpectedResultWrapperHash = "e2b3098215d0082798d4ade7f962f956c8d7902a5f736cf8d81ee940d5d1d8e4";
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
                var handler = new BridgeEventHandler(Requests, _routinesRoot, _testModelsRoot, () => _externalEvent?.Raise());
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
                                response = await pending.Completion.Task.WaitAsync(
                                    TimeSpan.FromSeconds(request.Operation == "run_saved_routine_in_active_model" ? 300 : 90),
                                    cancellationToken).ConfigureAwait(false);
                            }
                            catch (TimeoutException)
                            {
                                var cancelledBeforeStart = request.Operation == "run_saved_routine_in_active_model"
                                    ? pending.TryCancelBeforeExecution()
                                    : pending.TryCancelBeforeStart();
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
                    if (op == "run_saved_routine" || op == "run_saved_routine_in_active_model")
                    {
                        if (!root.TryGetProperty("confirmModelWrite", out var confirmation) ||
                            (confirmation.ValueKind != JsonValueKind.True && confirmation.ValueKind != JsonValueKind.False)) return null;
                        confirmModelWrite = confirmation.GetBoolean();
                    }
                    if (op != "ping" && op != "open_dynamo_ui" && op != "prepare_saved_routine_in_dynamo" &&
                        op != "run_saved_readonly_routine" && op != "run_saved_routine" &&
                        op != "run_saved_routine_in_active_model") return null;
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
        internal DeferredRun Deferred { get; set; }
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
        internal bool TryCancelBeforeExecution() => TryCancelBeforeStart() || Interlocked.CompareExchange(ref _state, 2, 1) == 1;
        internal bool TryCommitExecution() => Interlocked.CompareExchange(ref _state, 3, 1) == 1;
        internal void MarkComplete()
        {
            if (Interlocked.CompareExchange(ref _state, 4, 1) != 1)
                Interlocked.CompareExchange(ref _state, 4, 3);
        }
    }

    internal sealed class DeferredRun
    {
        internal string ResultPath { get; }
        internal Func<UIApplication, BridgeResponse> Verify { get; }
        internal Action Cleanup { get; }

        internal DeferredRun(string resultPath, Func<UIApplication, BridgeResponse> verify, Action cleanup)
        {
            ResultPath = resultPath;
            Verify = verify;
            Cleanup = cleanup;
        }
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
        public bool? DynamoReadyForActiveDocument { get; set; }
        public long[] ChangedElementIds { get; set; }
        public object RoutineResult { get; set; }
        public string[] VerificationFailures { get; set; }
        public int? ChangedElementCount { get; set; }
        public string[] ChangedElementTypes { get; set; }
        public bool? RoutineLoaded { get; set; }
        public string RoutineRunModeRequested { get; set; }
        public bool? RoutineExecutionRequested { get; set; }
        public bool? DocumentModifiedOnOpen { get; set; }

        internal static BridgeResponse Fail(string status) => new BridgeResponse { Ok = false, Status = status };
    }

    internal sealed class BridgeEventHandler : IExternalEventHandler
    {
        private readonly ConcurrentQueue<PendingRequest> _requests;
        private readonly string _routinesRoot;
        private readonly string _testModelsRoot;
        private readonly Action _raise;

        internal BridgeEventHandler(ConcurrentQueue<PendingRequest> requests, string routinesRoot, string testModelsRoot, Action raise)
        {
            _requests = requests;
            _routinesRoot = routinesRoot;
            _testModelsRoot = testModelsRoot;
            _raise = raise;
        }

        public void Execute(UIApplication application)
        {
            while (_requests.TryDequeue(out var request))
            {
                if (request.Deferred != null)
                {
                    BridgeResponse response;
                    try { response = request.Deferred.Verify(application); }
                    catch { response = BridgeResponse.Fail("routine_result_invalid"); }
                    finally
                    {
                        try { request.Deferred.Cleanup(); } catch { }
                        request.MarkComplete();
                    }
                    request.Completion.TrySetResult(response);
                    continue;
                }
                if (!request.TryStart())
                {
                    request.Completion.TrySetResult(BridgeResponse.Fail("revit_dispatch_timeout_before_execution"));
                    continue;
                }
                try
                {
                    var response = Handle(application, request);
                    if (request.Deferred != null) ScheduleDeferredCompletion(request);
                    else request.Completion.TrySetResult(response);
                }
                catch
                {
                    request.Completion.TrySetResult(BridgeResponse.Fail("execution_failed"));
                }
                finally
                {
                    if (request.Deferred == null) request.MarkComplete();
                }
            }
        }

        private void ScheduleDeferredCompletion(PendingRequest request)
        {
            var resultPath = request.Deferred.ResultPath;
            Task.Run(async () =>
            {
                var deadline = DateTime.UtcNow.AddSeconds(30);
                while (!File.Exists(resultPath) && DateTime.UtcNow < deadline)
                    await Task.Delay(100).ConfigureAwait(false);
                _requests.Enqueue(request);
                _raise();
            });
        }

        public string GetName() => "ARQ-IA Dynamo routine bridge";

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
                    DynamoReadyForActiveDocument = application.ActiveUIDocument?.Document != null &&
                        IsDynamoReadyForDocument(application, application.ActiveUIDocument.Document),
                    RoutineId = BridgeApplication.RoutineId
                };
            }

            if (request.Operation == "open_dynamo_ui") return OpenDynamoUi(application);

            if (request.Operation == "prepare_saved_routine_in_dynamo") return PrepareSavedRoutineInDynamo(application, request.RoutineId);

            if (request.Operation == "run_saved_routine") return HandleSavedRoutine(application, request, false);
            if (request.Operation == "run_saved_routine_in_active_model") return HandleSavedRoutine(application, request, true);

            if (request.Operation != "run_saved_readonly_routine" || request.RoutineId != BridgeApplication.RoutineId)
                return BridgeResponse.Fail("routine_not_allowed");

            var uiDocument = application.ActiveUIDocument;
            if (uiDocument == null || uiDocument.Document == null) return BridgeResponse.Fail("no_active_document");
            var document = uiDocument.Document;
            // Opening Dynamo can dirty a disposable test document (for example while
            // initializing preview resources). The allowlisted graph remains safe to
            // run against that baseline if Revit is idle; verify no further changes.
            if (document.IsModifiable) return BridgeResponse.Fail("document_must_be_idle");
            if (!IsTestModel(document, _testModelsRoot)) return BridgeResponse.Fail("test_model_required");
            if (!IsDynamoReadyForDocument(application, document)) return BridgeResponse.Fail("dynamo_ui_required_for_active_document");

            string pythonPath;
            string graphPath;
            byte[] validatedGraphBytes;
            try
            {
                pythonPath = InRoutinesRoot(BridgeApplication.RoutineId + ".py");
                graphPath = InRoutinesRoot(BridgeApplication.RoutineId + ".dyn");
                var manifestPath = InRoutinesRoot(BridgeApplication.RoutineId + ".manifest.json");
                validatedGraphBytes = ValidateSavedRoutine(pythonPath, graphPath, manifestPath);
            }
            catch
            {
                return BridgeResponse.Fail("routine_integrity_check_failed");
            }

            var beforeViewId = uiDocument.ActiveView.Id.Value;
            var beforeViewType = uiDocument.ActiveView.ViewType.ToString();
            var beforeSelectionCount = uiDocument.Selection.GetElementIds().Count;
            var beforeModified = document.IsModified;
            var beforeDocumentPath = Path.GetFullPath(document.PathName);
            var changedIds = new HashSet<long>();
            var changedElementTypes = new SortedSet<string>(StringComparer.Ordinal);
            EventHandler<DocumentChangedEventArgs> changedHandler = (sender, args) =>
            {
                var eventIds = args.GetAddedElementIds()
                    .Concat(args.GetModifiedElementIds())
                    .Concat(args.GetDeletedElementIds())
                    .Distinct()
                    .ToArray();
                AddChangedIds(changedIds, eventIds);
                foreach (var elementId in eventIds)
                {
                    var elementType = document.GetElement(elementId)?.GetType().Name ?? "DeletedElement";
                    if (Regex.IsMatch(elementType, "^[A-Za-z][A-Za-z0-9_]{0,63}$", RegexOptions.CultureInvariant))
                        changedElementTypes.Add(elementType);
                }
            };
            var runId = Guid.NewGuid().ToString("N");
            var projectRoot = Directory.GetParent(_routinesRoot).FullName;
            var resultRoot = Path.GetFullPath(Path.Combine(projectRoot, "logs", "runner-results"));
            if (!IsWithin(projectRoot, resultRoot)) return BridgeResponse.Fail("invalid_result_root");
            Directory.CreateDirectory(resultRoot);
            if (ContainsReparsePoint(resultRoot)) return BridgeResponse.Fail("invalid_result_root");
            var resultPath = Path.GetFullPath(Path.Combine(resultRoot, runId + ".json"));
            if (!IsWithin(resultRoot, resultPath)) return BridgeResponse.Fail("invalid_result_path");

            string runtimeGraphPath = null;
            void Cleanup()
            {
                application.Application.DocumentChanged -= changedHandler;
                try { if (runtimeGraphPath != null && File.Exists(runtimeGraphPath) && !IsCurrentDynamoWorkspace(runtimeGraphPath)) File.Delete(runtimeGraphPath); } catch { }
                try { if (File.Exists(resultPath)) File.Delete(resultPath); } catch { }
                try { if (File.Exists(resultPath + ".started")) File.Delete(resultPath + ".started"); } catch { }
            }

            BridgeResponse Verify()
            {
                if (!File.Exists(resultPath)) return BridgeResponse.Fail(File.Exists(resultPath + ".started") ? "dynamo_python_node_started_without_result" : "dynamo_python_node_not_started");

                if (new FileInfo(resultPath).Length > 65536) return BridgeResponse.Fail("routine_result_invalid");
                using (var resultDocument = JsonDocument.Parse(File.ReadAllText(resultPath), new JsonDocumentOptions { MaxDepth = 4 }))
                {
                    var result = resultDocument.RootElement;
                    var status = result.GetProperty("status").GetString();
                    var viewType = result.GetProperty("view_type").GetString();
                    var selectionCount = result.GetProperty("selection_count").GetInt32();
                    var verificationFailures = new List<string>();
                    var activeUIDocument = application.ActiveUIDocument;
                    var afterDocumentPath = activeUIDocument?.Document?.PathName;
                    var sameActiveDocument = !String.IsNullOrWhiteSpace(afterDocumentPath) &&
                        String.Equals(Path.GetFullPath(afterDocumentPath), beforeDocumentPath, StringComparison.OrdinalIgnoreCase);
                    if (status != "ok") verificationFailures.Add("routine_status_not_ok");
                    if (beforeModified != document.IsModified) verificationFailures.Add("document_modified_state_changed");
                    if (document.IsModifiable) verificationFailures.Add("document_modifiable");
                    if (!sameActiveDocument) verificationFailures.Add("active_document_changed");
                    if (activeUIDocument == null || activeUIDocument.ActiveView.Id.Value != beforeViewId)
                        verificationFailures.Add("active_view_changed");
                    if (activeUIDocument == null || activeUIDocument.Selection.GetElementIds().Count != beforeSelectionCount)
                        verificationFailures.Add("selection_changed");
                    if (!String.Equals(beforeViewType, viewType, StringComparison.Ordinal))
                        verificationFailures.Add("routine_view_type_mismatch");
                    if (beforeSelectionCount != selectionCount)
                        verificationFailures.Add("routine_selection_count_mismatch");
                    if (changedIds.Count != 0) verificationFailures.Add("document_elements_changed");
                    if (verificationFailures.Count != 0)
                    {
                        return new BridgeResponse
                        {
                            Ok = false,
                            Status = "post_run_verification_failed",
                            VerificationFailures = verificationFailures.ToArray(),
                            ChangedElementCount = changedIds.Count,
                            ChangedElementTypes = changedElementTypes.Take(8).ToArray()
                        };
                    }
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

            var cleanupNow = true;
            application.Application.DocumentChanged += changedHandler;
            try
            {
                runtimeGraphPath = CreateRuntimeGraphCopy(validatedGraphBytes, runId, resultPath);
                var journal = new Dictionary<string, string>
                {
                    [JournalKeys.AutomationModeKey] = "false",
                    [JournalKeys.ShowUiKey] = "false",
                    [JournalKeys.DynPathKey] = runtimeGraphPath,
                    [JournalKeys.DynPathExecuteKey] = "true"
                };
                var dynamoResult = ExecuteDynamoWorkspace(application, journal, runtimeGraphPath, resultPath, disableTransactions: true);
                if (dynamoResult != Result.Succeeded) return BridgeResponse.Fail("dynamo_command_failed");
                if (File.Exists(resultPath)) return Verify();
                request.Deferred = new DeferredRun(resultPath, _ => Verify(), Cleanup);
                cleanupNow = false;
                return null;
            }
            finally
            {
                if (cleanupNow) Cleanup();
            }
        }


        private BridgeResponse OpenDynamoUi(UIApplication application)
        {
            var uiDocument = application.ActiveUIDocument;
            if (uiDocument?.Document == null) return BridgeResponse.Fail("no_active_document");
            var document = uiDocument.Document;
            if (!IsTestModel(document, _testModelsRoot)) return BridgeResponse.Fail("test_model_required");
            if (IsDynamoReadyForDocument(application, document))
            {
                return new BridgeResponse
                {
                    Ok = true,
                    Status = "dynamo_ui_already_open",
                    RevitMajor = application.Application.VersionNumber,
                    DynamoAssemblyVersion = typeof(DynamoRevitCommandData).Assembly.GetName().Version?.ToString(),
                    DynamoReadyForActiveDocument = true
                };
            }
            if (document.IsModified || document.IsModifiable) return BridgeResponse.Fail("document_must_be_saved_and_idle");

            var commandId = RevitCommandId.LookupPostableCommandId(PostableCommand.Dynamo);
            if (commandId == null || !application.CanPostCommand(commandId))
                return BridgeResponse.Fail("dynamo_command_unavailable");

            application.PostCommand(commandId);
            return new BridgeResponse
            {
                Ok = true,
                Status = "dynamo_ui_open_requested",
                RevitMajor = application.Application.VersionNumber,
                DynamoAssemblyVersion = typeof(DynamoRevitCommandData).Assembly.GetName().Version?.ToString(),
                DynamoReadyForActiveDocument = false
            };
        }

        private BridgeResponse PrepareSavedRoutineInDynamo(UIApplication application, string routineId)
        {
            if (String.IsNullOrWhiteSpace(routineId) || !Regex.IsMatch(routineId, "^[a-z][a-z0-9_-]{1,47}$", RegexOptions.CultureInvariant))
                return BridgeResponse.Fail("routine_id_invalid");

            var uiDocument = application.ActiveUIDocument;
            if (uiDocument?.Document == null) return BridgeResponse.Fail("no_active_document");
            var document = uiDocument.Document;
            if (document.IsModifiable) return BridgeResponse.Fail("document_must_be_idle");

            string pythonPath;
            string graphPath;
            string manifestPath;
            byte[] validatedGraphBytes;
            try
            {
                pythonPath = InRoutinesRoot(routineId + ".py");
                graphPath = InRoutinesRoot(routineId + ".dyn");
                manifestPath = InRoutinesRoot(routineId + ".manifest.json");
                if (ContainsReparsePoint(pythonPath) || ContainsReparsePoint(graphPath) || ContainsReparsePoint(manifestPath))
                    return BridgeResponse.Fail("routine_path_invalid");
                ValidateGeneratedRoutine(routineId, pythonPath, graphPath, manifestPath, out validatedGraphBytes);
            }
            catch
            {
                return BridgeResponse.Fail("routine_integrity_check_failed");
            }

            var dynamoModel = GetDynamoModel();
            var dynamoState = GetDynamoModelState(dynamoModel);
            if (dynamoState == "StartedUI")
            {
                if (!IsDynamoBoundToDocument(application, document))
                    return BridgeResponse.Fail("dynamo_ui_bound_to_other_document");
                if (IsCurrentDynamoWorkspace(graphPath))
                {
                    return new BridgeResponse
                    {
                        Ok = true,
                        Status = "routine_already_loaded_mode_unverified",
                        RoutineId = routineId,
                        RoutineLoaded = true,
                        RoutineExecutionRequested = false,
                        DynamoReadyForActiveDocument = true,
                        RevitMajor = application.Application.VersionNumber,
                        DynamoAssemblyVersion = typeof(DynamoRevitCommandData).Assembly.GetName().Version?.ToString()
                    };
                }
                if (!TryGetDynamoWorkspaceContentState(dynamoModel, out var workspaceHasContent))
                    return BridgeResponse.Fail("dynamo_workspace_state_unavailable");
                if (workspaceHasContent)
                    return BridgeResponse.Fail("dynamo_workspace_not_empty");
            }

            if (!IsTestModel(document, _testModelsRoot))
            {
                var confirmation = new TaskDialog("ARQ-IA · abrir rutina")
                {
                    MainInstruction = "¿Permitir abrir código PythonNet3 en este modelo?",
                    MainContent = "Modelo: " + document.Title + "\nRutina: " + routineId +
                        "\nDynamo solicita abrir el grafo en modo Manual, pero aún no se ha comprobado en vivo que esta versión nunca lo ejecute durante la apertura. " +
                        "Pulsa No si solo quieres leer el script; puedes revisarlo en la carpeta routines sin cargarlo en Revit.",
                    ExpandedContent = "RVT: " + document.PathName + "\nScript: " + pythonPath +
                        "\nSHA-256 del grafo: " + ToHex(SHA256.HashData(validatedGraphBytes)),
                    CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
                    DefaultButton = TaskDialogResult.No
                };
                if (confirmation.Show() != TaskDialogResult.Yes)
                    return BridgeResponse.Fail("user_declined_graph_load_in_active_model");
                if (!IsApplicationActiveDocument(application, document) || document.IsModifiable)
                    return BridgeResponse.Fail("active_document_changed_before_open");
            }

            if (!IsApplicationActiveDocument(application, document)) return BridgeResponse.Fail("active_document_changed_before_open");
            var wasModified = document.IsModified;
            var journal = new Dictionary<string, string>
            {
                [JournalKeys.AutomationModeKey] = "false",
                [JournalKeys.ShowUiKey] = "true",
                [JournalKeys.DynPathKey] = graphPath,
                [JournalKeys.DynPathExecuteKey] = "false",
                [JournalKeys.ForceManualRunKey] = "true",
                [JournalKeys.DynPathCheckExisting] = "false"
            };

            Result result;
            try
            {
                result = new DynamoRevit().ExecuteCommand(new DynamoRevitCommandData
                {
                    Application = application,
                    JournalData = journal
                });
            }
            catch
            {
                return BridgeResponse.Fail("dynamo_open_failed");
            }
            if (result != Result.Succeeded) return BridgeResponse.Fail("dynamo_open_failed");
            if (!IsApplicationActiveDocument(application, document)) return BridgeResponse.Fail("active_document_changed_during_open");
            if (!IsDynamoBoundToDocument(application, document)) return BridgeResponse.Fail("dynamo_ui_not_bound_to_active_document");
            if (!IsCurrentDynamoWorkspace(graphPath)) return BridgeResponse.Fail("dynamo_graph_not_loaded");

            return new BridgeResponse
            {
                Ok = true,
                Status = "routine_loaded_manual_mode_requested",
                RoutineId = routineId,
                RoutineLoaded = true,
                RoutineRunModeRequested = "Manual",
                RoutineExecutionRequested = false,
                DocumentModifiedOnOpen = !wasModified && document.IsModified,
                DynamoReadyForActiveDocument = true,
                RevitMajor = application.Application.VersionNumber,
                DynamoAssemblyVersion = typeof(DynamoRevitCommandData).Assembly.GetName().Version?.ToString()
            };
        }

        private string InRoutinesRoot(string fileName)
        {
            var path = Path.GetFullPath(Path.Combine(_routinesRoot, fileName));
            if (!IsWithin(_routinesRoot, path)) throw new InvalidDataException("Routine path outside allowlist");
            return path;
        }

        private BridgeResponse HandleSavedRoutine(UIApplication application, PendingRequest request, bool activeModelExecution)
        {
            if (String.IsNullOrWhiteSpace(request.RoutineId) || !Regex.IsMatch(request.RoutineId, "^[a-z][a-z0-9_-]{1,47}$", RegexOptions.CultureInvariant))
                return BridgeResponse.Fail("routine_id_invalid");

            var uiDocument = application.ActiveUIDocument;
            if (uiDocument == null || uiDocument.Document == null) return BridgeResponse.Fail("no_active_document");
            var document = uiDocument.Document;
            // The saved path identifies the disposable RVT on disk. Dynamo may have
            // already marked that in-memory copy modified during UI startup; routines
            // still require an idle document and verify their own DocumentChanged delta.
            if (document.IsModifiable) return BridgeResponse.Fail("document_must_be_idle");
            if (activeModelExecution)
            {
                if (String.IsNullOrWhiteSpace(document.PathName) ||
                    !String.Equals(Path.GetExtension(document.PathName), ".rvt", StringComparison.OrdinalIgnoreCase) ||
                    ContainsReparsePoint(document.PathName))
                    return BridgeResponse.Fail("saved_local_rvt_required");
            }
            else if (!IsTestModel(document, _testModelsRoot)) return BridgeResponse.Fail("test_model_required");
            if (!IsDynamoReadyForDocument(application, document)) return BridgeResponse.Fail("dynamo_ui_required_for_active_document");

            string pythonPath;
            string graphPath;
            string manifestPath;
            bool writesModel;
            byte[] validatedGraphBytes;
            try
            {
                pythonPath = InRoutinesRoot(request.RoutineId + ".py");
                graphPath = InRoutinesRoot(request.RoutineId + ".dyn");
                manifestPath = InRoutinesRoot(request.RoutineId + ".manifest.json");
                if (ContainsReparsePoint(pythonPath) || ContainsReparsePoint(graphPath) || ContainsReparsePoint(manifestPath))
                    return BridgeResponse.Fail("routine_path_invalid");
                writesModel = ValidateGeneratedRoutine(request.RoutineId, pythonPath, graphPath, manifestPath, out validatedGraphBytes);
            }
            catch
            {
                return BridgeResponse.Fail("routine_integrity_check_failed");
            }
            // PythonNet3 is full trust; manifest metadata cannot prove that a script is read-only.
            if (!request.ConfirmModelWrite) return BridgeResponse.Fail("generic_execution_confirmation_required");
            if (!activeModelExecution && document.IsWorkshared) return BridgeResponse.Fail("model_must_be_local_non_workshared");
            if (activeModelExecution)
            {
                if (!TryGetDynamoWorkspaceContentState(GetDynamoModel(), out var workspaceHasContent))
                    return BridgeResponse.Fail("dynamo_workspace_state_unavailable");
                if (workspaceHasContent)
                    return BridgeResponse.Fail("dynamo_workspace_not_empty");

                var targetPath = Path.GetFullPath(document.PathName);
                var graphHash = ToHex(SHA256.HashData(validatedGraphBytes));
                var confirmation = new TaskDialog("ARQ-IA · confirmar ejecución")
                {
                    MainInstruction = "¿Ejecutar esta rutina en el modelo abierto?",
                    MainContent = "Modelo: " + document.Title + "\nRutina: " + request.RoutineId +
                        "\nAcción: ejecutar PythonNet3 en Dynamo; puede modificar el modelo.\n" +
                        "El grafo se cargará y ejecutará solo después de Sí. El RVT no se guardará automáticamente. " +
                        "Revisa el archivo .py antes de continuar.",
                    ExpandedContent = "RVT: " + targetPath + "\nGrafo: " + graphPath +
                        "\nScript: " + pythonPath +
                        "\nSHA-256 del grafo: " + graphHash +
                        "\nPythonNet3 se ejecuta con tus permisos de Revit y Windows.",
                    CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
                    DefaultButton = TaskDialogResult.No
                };
                if (confirmation.Show() != TaskDialogResult.Yes)
                    return BridgeResponse.Fail("user_declined_active_model_execution");
                if (!request.TryCommitExecution())
                    return BridgeResponse.Fail("execution_approval_expired");
                if (!IsApplicationActiveDocument(application, document) ||
                    !String.Equals(Path.GetFullPath(document.PathName), targetPath, StringComparison.OrdinalIgnoreCase) ||
                    document.IsModifiable || !IsDynamoReadyForDocument(application, document) ||
                    !TryGetDynamoWorkspaceContentState(GetDynamoModel(), out var stillHasContent) || stillHasContent)
                    return BridgeResponse.Fail("active_document_changed_before_execution");
            }

            var beforeModified = document.IsModified;
            var beforeViewId = uiDocument.ActiveView.Id.Value;
            var beforeSelection = uiDocument.Selection.GetElementIds().Select(id => id.Value).OrderBy(id => id).ToArray();
            var projectRoot = Directory.GetParent(_routinesRoot).FullName;
            var resultRoot = Path.GetFullPath(Path.Combine(projectRoot, "logs", "runner-results"));
            if (!IsWithin(projectRoot, resultRoot)) return BridgeResponse.Fail("invalid_result_root");
            Directory.CreateDirectory(resultRoot);
            if (ContainsReparsePoint(resultRoot)) return BridgeResponse.Fail("invalid_result_root");
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
            string runtimeGraphPath = null;
            void Cleanup()
            {
                application.Application.DocumentChanged -= changedHandler;
                try { if (runtimeGraphPath != null && File.Exists(runtimeGraphPath) && !IsCurrentDynamoWorkspace(runtimeGraphPath)) File.Delete(runtimeGraphPath); } catch { }
                try { if (File.Exists(resultPath)) File.Delete(resultPath); } catch { }
                try { if (File.Exists(resultPath + ".tmp")) File.Delete(resultPath + ".tmp"); } catch { }
                try { if (File.Exists(resultPath + ".started")) File.Delete(resultPath + ".started"); } catch { }
            }

            BridgeResponse Verify()
            {
                if (!File.Exists(resultPath)) return BridgeResponse.Fail(File.Exists(resultPath + ".started") ? "dynamo_python_node_started_without_result" : "dynamo_python_node_not_started");
                if (new FileInfo(resultPath).Length > 65536) return BridgeResponse.Fail("routine_result_invalid");

                using (var resultDocument = JsonDocument.Parse(File.ReadAllText(resultPath), new JsonDocumentOptions { MaxDepth = 20 }))
                {
                    var root = resultDocument.RootElement;
                    if (root.GetProperty("status").GetString() != "ok") return BridgeResponse.Fail("routine_reported_failure");
                    if (!root.TryGetProperty("routine_result", out var routineResult)) return BridgeResponse.Fail("routine_result_invalid");
                    var selectionAfter = uiDocument.Selection.GetElementIds().Select(id => id.Value).OrderBy(id => id).ToArray();
                    if (uiDocument.ActiveView.Id.Value != beforeViewId) return BridgeResponse.Fail("post_run_active_view_changed");
                    if (!beforeSelection.SequenceEqual(selectionAfter)) return BridgeResponse.Fail("post_run_selection_changed");
                    if (document.IsModifiable) return BridgeResponse.Fail("post_run_document_still_modifiable");

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

                    if (changedIds.Count != 0) return BridgeResponse.Fail("post_run_document_changed_" + changedIds.Count);
                    if (beforeModified != document.IsModified) return BridgeResponse.Fail("post_run_document_modified_state_changed");
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

            var cleanupNow = true;
            application.Application.DocumentChanged += changedHandler;
            try
            {
                runtimeGraphPath = CreateRuntimeGraphCopy(validatedGraphBytes, runId, resultPath);
                var journal = new Dictionary<string, string>
                {
                    [JournalKeys.AutomationModeKey] = "false",
                    [JournalKeys.ShowUiKey] = "false",
                    [JournalKeys.DynPathKey] = runtimeGraphPath,
                    [JournalKeys.DynPathExecuteKey] = "true"
                };
                var dynamoResult = ExecuteDynamoWorkspace(application, journal, runtimeGraphPath, resultPath, disableTransactions: !writesModel);
                if (dynamoResult != Result.Succeeded) return BridgeResponse.Fail("dynamo_command_failed");
                if (File.Exists(resultPath)) return Verify();
                request.Deferred = new DeferredRun(resultPath, _ => Verify(), Cleanup);
                cleanupNow = false;
                return null;
            }
            catch
            {
                return BridgeResponse.Fail("routine_result_invalid");
            }
            finally
            {
                if (cleanupNow) Cleanup();
            }
        }


        private string CreateRuntimeGraphCopy(byte[] validatedGraphBytes, string runId, string resultPath)
        {
            if (validatedGraphBytes == null || validatedGraphBytes.Length == 0 ||
                !Regex.IsMatch(runId ?? String.Empty, "^[0-9a-f]{32}$", RegexOptions.CultureInvariant))
                throw new InvalidDataException("Invalid runtime graph context");

            var absoluteResultPath = Path.GetFullPath(resultPath);
            var resultRoot = Path.GetDirectoryName(absoluteResultPath);
            if (String.IsNullOrWhiteSpace(resultRoot)) throw new InvalidDataException("Invalid result directory");
            var markerPath = absoluteResultPath + ".started";
            if (!IsWithin(resultRoot, markerPath)) throw new InvalidDataException("Invalid Python node marker path");
            var runtimeGraphPath = Path.GetFullPath(Path.Combine(resultRoot, runId + ".dyn"));
            if (!IsWithin(resultRoot, runtimeGraphPath) || ContainsReparsePoint(resultRoot))
                throw new InvalidDataException("Invalid runtime graph path");

            var graphText = new UTF8Encoding(false, true).GetString(validatedGraphBytes);
            var graph = JsonNode.Parse(graphText, documentOptions: new JsonDocumentOptions { MaxDepth = 32 }) as JsonObject;
            var nodes = graph?["Nodes"] as JsonArray;
            if (graph == null || nodes == null || nodes.Count != 1 || nodes[0] is not JsonObject node)
                throw new InvalidDataException("Unexpected runtime graph shape");
            var code = node["Code"]?.GetValue<string>();
            if (code == null) throw new InvalidDataException("Runtime graph has no Python source");

            var runtimeContext = "_BIM_AI_RUN_ID = " + JsonSerializer.Serialize(runId) + "\n" +
                "_BIM_AI_RESULT_PATH = " + JsonSerializer.Serialize(absoluteResultPath) + "\n" +
                "_BIM_AI_RESULT_DIR = " + JsonSerializer.Serialize(resultRoot) + "\n" +
                "_BIM_AI_PYTHON_STARTED_PATH = " + JsonSerializer.Serialize(markerPath) + "\n" +
                "with open(_BIM_AI_PYTHON_STARTED_PATH, 'w', encoding='utf-8') as _bim_ai_marker:\n" +
                "    _bim_ai_marker.write('1')\n";
            node["Code"] = runtimeContext + code;
            var runtimeGraphJson = graph.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
            using (var stream = new FileStream(runtimeGraphPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(runtimeGraphJson);
            }
            return runtimeGraphPath;
        }

        private static Result ExecuteDynamoWorkspace(UIApplication application, Dictionary<string, string> graphJournal, string expectedGraphPath, string resultPath, bool disableTransactions)
        {
            var activeDocument = application.ActiveUIDocument?.Document;
            if (activeDocument == null || !IsDynamoReadyForDocument(application, activeDocument)) return Result.Failed;

            object transactionManager = null;
            PropertyInfo disableTransactionsProperty = null;
            object previousDisableTransactions = null;
            if (disableTransactions && !TrySetDynamoTransactionsDisabled(true, out transactionManager, out disableTransactionsProperty, out previousDisableTransactions))
                return Result.Failed;

            Result result;
            try
            {
                result = ExecuteDynamoWorkspaceCore(application, graphJournal, expectedGraphPath, resultPath);
            }
            catch
            {
                result = Result.Failed;
            }

            if (disableTransactionsProperty != null)
            {
                try { disableTransactionsProperty.SetValue(transactionManager, previousDisableTransactions); }
                catch { return Result.Failed; }
            }
            return result;
        }

        private static Result ExecuteDynamoWorkspaceCore(UIApplication application, Dictionary<string, string> graphJournal, string expectedGraphPath, string resultPath)
        {
            var graphData = new DynamoRevitCommandData
            {
                Application = application,
                JournalData = graphJournal
            };
            var graphResult = new DynamoRevit().ExecuteCommand(graphData);
            if (graphResult != Result.Succeeded)
                return graphResult;

            var model = GetDynamoModel();
            var workspaceProperty = model?.GetType().GetProperty("CurrentWorkspace", BindingFlags.Public | BindingFlags.Instance);
            var workspace = workspaceProperty?.GetValue(model);
            if (workspace == null) return Result.Failed;

            var fileNameProperty = workspace.GetType().GetProperty("FileName", BindingFlags.Public | BindingFlags.Instance);
            var openedPath = fileNameProperty?.GetValue(workspace) as string;
            if (String.IsNullOrWhiteSpace(openedPath) ||
                !String.Equals(Path.GetFullPath(openedPath), Path.GetFullPath(expectedGraphPath), StringComparison.OrdinalIgnoreCase))
                return Result.Failed;

            // DynPathExecuteKey already calls HomeWorkspaceModel.Run() in DynamoRevit.
            // A second Run() here could execute an approved write twice.
            return Result.Succeeded;
        }

        private static bool IsDynamoReadyForDocument(UIApplication application, Document document)
        {
            try
            {
                if (application?.ActiveUIDocument?.Document == null || document == null) return false;
                if (GetDynamoModelState(GetDynamoModel()) != "StartedUI") return false;

                var managerType = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType("RevitServices.Persistence.DocumentManager", false))
                    .FirstOrDefault(type => type != null);
                var instanceProperty = managerType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                var manager = instanceProperty?.GetValue(null);
                var currentDocumentProperty = managerType?.GetProperty("CurrentUIDocument", BindingFlags.Public | BindingFlags.Instance);
                var dynamoUiDocument = currentDocumentProperty?.GetValue(manager) as UIDocument;
                var targetPath = application.ActiveUIDocument.Document.PathName;
                var dynamoPath = dynamoUiDocument?.Document?.PathName;
                return !String.IsNullOrWhiteSpace(targetPath) && !String.IsNullOrWhiteSpace(dynamoPath) &&
                    String.Equals(Path.GetFullPath(targetPath), Path.GetFullPath(dynamoPath), StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static bool TrySetDynamoTransactionsDisabled(bool disabled, out object transactionManager, out PropertyInfo disableTransactionsProperty, out object previousValue)
        {
            transactionManager = null;
            disableTransactionsProperty = null;
            previousValue = null;
            var managerType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("RevitServices.Transactions.TransactionManager", false))
                .FirstOrDefault(type => type != null);
            var instanceProperty = managerType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            transactionManager = instanceProperty?.GetValue(null);
            if (transactionManager == null) return false;
            disableTransactionsProperty = managerType.GetProperty("DisableTransactions", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (disableTransactionsProperty == null || !disableTransactionsProperty.CanRead || !disableTransactionsProperty.CanWrite) return false;
            previousValue = disableTransactionsProperty.GetValue(transactionManager);
            if (!(previousValue is bool)) return false;
            disableTransactionsProperty.SetValue(transactionManager, disabled);
            return (bool)disableTransactionsProperty.GetValue(transactionManager) == disabled;
        }

        private static object GetDynamoModel()
        {
            var modelProperty = typeof(DynamoRevit).GetProperty("RevitDynamoModel", BindingFlags.Public | BindingFlags.Static);
            return modelProperty?.GetValue(null);
        }

        private static bool IsDynamoBoundToDocument(UIApplication application, Document document)
        {
            try
            {
                if (!IsApplicationActiveDocument(application, document)) return false;
                if (GetDynamoModelState(GetDynamoModel()) != "StartedUI") return false;
                var managerType = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType("RevitServices.Persistence.DocumentManager", false))
                    .FirstOrDefault(type => type != null);
                var instanceProperty = managerType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                var manager = instanceProperty?.GetValue(null);
                var currentDocumentProperty = managerType?.GetProperty("CurrentUIDocument", BindingFlags.Public | BindingFlags.Instance);
                var dynamoDocument = (currentDocumentProperty?.GetValue(manager) as UIDocument)?.Document;
                return SameRevitDocument(dynamoDocument, document);
            }
            catch { return false; }
        }

        private static bool IsApplicationActiveDocument(UIApplication application, Document document)
        {
            try
            {
                return SameRevitDocument(application?.ActiveUIDocument?.Document, document);
            }
            catch { return false; }
        }

        private static bool SameRevitDocument(Document left, Document right)
        {
            if (left == null || right == null) return false;
            if (ReferenceEquals(left, right)) return true;
            try
            {
                if (String.IsNullOrWhiteSpace(left.PathName) || String.IsNullOrWhiteSpace(right.PathName)) return false;
                return String.Equals(Path.GetFullPath(left.PathName), Path.GetFullPath(right.PathName), StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static bool TryGetDynamoWorkspaceContentState(object model, out bool hasContent)
        {
            hasContent = false;
            try
            {
                var workspace = model?.GetType().GetProperty("CurrentWorkspace", BindingFlags.Public | BindingFlags.Instance)?.GetValue(model);
                if (workspace == null) return false;
                foreach (var propertyName in new[] { "Nodes", "Notes" })
                {
                    var property = workspace.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                    if (property == null || !property.CanRead) return false;
                    var entries = property.GetValue(workspace) as IEnumerable;
                    if (entries == null) return false;
                    foreach (var entry in entries)
                    {
                        if (entry == null) continue;
                        hasContent = true;
                        return true;
                    }
                }
                return true;
            }
            catch { return false; }
        }

        private static bool IsCurrentDynamoWorkspace(string path)
        {
            try
            {
                var model = GetDynamoModel();
                var workspace = model?.GetType().GetProperty("CurrentWorkspace", BindingFlags.Public | BindingFlags.Instance)?.GetValue(model);
                var fileName = workspace?.GetType().GetProperty("FileName", BindingFlags.Public | BindingFlags.Instance)?.GetValue(workspace) as string;
                return !String.IsNullOrWhiteSpace(fileName) &&
                    String.Equals(Path.GetFullPath(fileName), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static string GetDynamoModelState(object model)
        {
            if (model == null) return null;
            var stateProperty = model.GetType().GetProperty("State", BindingFlags.Public | BindingFlags.Instance);
            return stateProperty?.GetValue(model)?.ToString();
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

        private bool ValidateGeneratedRoutine(string routineId, string pythonPath, string graphPath, string manifestPath, out byte[] validatedGraphBytes)
        {
            validatedGraphBytes = Array.Empty<byte>();
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
                validatedGraphBytes = graphBytes;
                return writesModel;
            }
        }

        private static bool IsWithin(string root, string path)
        {
            var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var normalizedPath = Path.GetFullPath(path);
            return normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
        }

        private static byte[] ValidateSavedRoutine(string pythonPath, string graphPath, string manifestPath)
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
            return graphBytes;
        }

        private static string ToHex(byte[] bytes)
        {
            var value = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) value.Append(b.ToString("x2"));
            return value.ToString();
        }
    }
}
