using System;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using BimAiDynamoAssistant;

namespace BimAiRevitAssistant
{
    public sealed class RevitAssistantApplication : IExternalApplication
    {
        internal static readonly DockablePaneId PaneId = new DockablePaneId(
            new Guid("c2fd201a-6ca4-457b-8d62-3aa7b27ac559"));

        private const string RibbonTabName = "ARQ-IA";
        private const string RibbonPanelName = "ARQ-IA";
        private AssistantDockPaneProvider? _paneProvider;

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                _paneProvider = new AssistantDockPaneProvider();
                application.RegisterDockablePane(PaneId, "ARQ-IA", _paneProvider);

                try
                {
                    application.CreateRibbonTab(RibbonTabName);
                }
                catch (Autodesk.Revit.Exceptions.ArgumentException)
                {
                    // Reuse the tab if another ARQ-IA component created it first.
                }

                var panel = application.CreateRibbonPanel(RibbonTabName, RibbonPanelName);
                var buttonData = new PushButtonData(
                    "BimAiOpenAssistant",
                    "Abrir chat",
                    Assembly.GetExecutingAssembly().Location,
                    typeof(ShowAssistantCommand).FullName);
                if (panel.AddItem(buttonData) is PushButton button)
                {
                    button.LargeImage = ArqIaRibbonIcon.Create(32);
                    button.Image = ArqIaRibbonIcon.Create(16);
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

    internal static class ArqIaRibbonIcon
    {
        public static BitmapSource Create(int size)
        {
            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
            {
                context.PushTransform(new ScaleTransform(size / 32.0, size / 32.0));
                var background = new LinearGradientBrush(
                    Color.FromRgb(20, 107, 167), Color.FromRgb(16, 57, 111), 70);
                context.DrawRoundedRectangle(background, null, new Rect(1, 1, 30, 30), 7, 7);
                var edge = new Pen(new SolidColorBrush(Color.FromRgb(95, 213, 239)), 1);
                context.DrawRoundedRectangle(null, edge, new Rect(1.5, 1.5, 29, 29), 6.5, 6.5);

                var white = new Pen(Brushes.White, 3.4)
                {
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round,
                    LineJoin = PenLineJoin.Round
                };
                context.DrawLine(white, new Point(7.5, 25.5), new Point(16, 6.8));
                context.DrawLine(white, new Point(16, 6.8), new Point(24.5, 25.5));
                var crossbar = new Pen(new SolidColorBrush(Color.FromRgb(113, 229, 244)), 2.8)
                {
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round
                };
                context.DrawLine(crossbar, new Point(11.5, 19.4), new Point(20.5, 19.4));
                context.Pop();
            }

            var image = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            image.Render(visual);
            image.Freeze();
            return image;
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
                message = "No se pudo abrir el panel ARQ-IA: " + exception.Message;
                return Result.Failed;
            }
        }
    }
}
