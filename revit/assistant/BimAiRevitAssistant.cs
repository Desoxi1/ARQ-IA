using System;
using System.Reflection;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using BimAiDynamoAssistant;

namespace BimAiRevitAssistant
{
    public sealed class RevitAssistantApplication : IExternalApplication
    {
        internal static readonly DockablePaneId PaneId = new DockablePaneId(
            new Guid("c2fd201a-6ca4-457b-8d62-3aa7b27ac559"));

        private const string RibbonTabName = "BIM-AI";
        private const string RibbonPanelName = "Asistente BIM";
        private AssistantDockPaneProvider? _paneProvider;

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                _paneProvider = new AssistantDockPaneProvider();
                application.RegisterDockablePane(PaneId, "Asistente BIM", _paneProvider);

                try
                {
                    application.CreateRibbonTab(RibbonTabName);
                }
                catch (Autodesk.Revit.Exceptions.ArgumentException)
                {
                    // Reuse the tab if another BIM-AI component created it first.
                }

                var panel = application.CreateRibbonPanel(RibbonTabName, RibbonPanelName);
                var buttonData = new PushButtonData(
                    "BimAiOpenAssistant",
                    "Abrir chat",
                    Assembly.GetExecutingAssembly().Location,
                    typeof(ShowAssistantCommand).FullName);
                if (panel.AddItem(buttonData) is PushButton button)
                {
                    button.ToolTip = "Abrir el asistente local de Codex para Revit y Dynamo.";
                    button.LongDescription = "Consulta el modelo, adjunta imágenes y archivos, y prepara rutinas reutilizables. Las operaciones de escritura requieren aprobación.";
                }

                return Result.Succeeded;
            }
            catch
            {
                _paneProvider?.Dispose();
                _paneProvider = null;
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            _paneProvider?.Dispose();
            _paneProvider = null;
            return Result.Succeeded;
        }
    }

    public sealed class AssistantDockPaneProvider : IDockablePaneProvider, IDisposable
    {
        private AssistantPane? _pane;

        public void SetupDockablePane(DockablePaneProviderData data)
        {
            _pane ??= new AssistantPane(ProjectSettings.TryGetProjectRoot());
            data.FrameworkElement = _pane;
            data.InitialState = new DockablePaneState { DockPosition = DockPosition.Right };
            data.VisibleByDefault = false;
        }

        public void Dispose()
        {
            _pane?.Dispose();
            _pane = null;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public sealed class ShowAssistantCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, Autodesk.Revit.DB.ElementSet elements)
        {
            try
            {
                commandData.Application.GetDockablePane(RevitAssistantApplication.PaneId).Show();
                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                message = "No se pudo abrir el panel Asistente BIM: " + exception.Message;
                return Result.Failed;
            }
        }
    }
}
