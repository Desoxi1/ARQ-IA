using System.Windows.Controls;
using Dynamo.Wpf.Extensions;

namespace BimAiDynamoAssistant;

public sealed class BimAiDynamoViewExtension : ViewExtensionBase
{
    private AssistantPane? _pane;

    public override string UniqueId => "a6b0d0a4-0d48-4d74-9a8f-9e0e3f3d1a27";
    public override string Name => "Asistente BIM";

    public override void Startup(ViewStartupParams p)
    {
    }

    public override void Loaded(ViewLoadedParams p)
    {
        _pane = new AssistantPane(ProjectSettings.TryGetProjectRoot());
        p.AddToExtensionsSideBar(this, new ContentControl { Content = _pane });
        p.OpenViewExtension(Name);
    }

    public override void Shutdown()
    {
        _pane?.Dispose();
        _pane = null;
    }

    public override void Dispose()
    {
        _pane?.Dispose();
        _pane = null;
    }
}
