using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BimAiDynamoAssistant;

internal sealed class AssistantPane : UserControl, IDisposable
{
    private readonly string? _projectRoot;
    private readonly TextBox _transcript;
    private readonly TextBox _prompt;
    private readonly Button _sendButton;
    private readonly TextBlock _status;
    private CodexAppServerClient? _client;
    private bool _disposed;
    private bool _assistantMessageOpen;

    public AssistantPane(string? projectRoot)
    {
        _projectRoot = projectRoot;
        Background = new SolidColorBrush(Color.FromRgb(29, 34, 42));

        var layout = new Grid { Margin = new Thickness(12) };
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        header.Children.Add(new TextBlock
        {
            Text = "Asistente BIM",
            FontSize = 19,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White
        });
        header.Children.Add(new TextBlock
        {
            Text = "Describe la tarea para Revit en lenguaje natural",
            Margin = new Thickness(0, 3, 0, 0),
            Foreground = new SolidColorBrush(Color.FromRgb(176, 188, 204)),
            TextWrapping = TextWrapping.Wrap
        });
        Grid.SetRow(header, 0);
        layout.Children.Add(header);

        _transcript = new TextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = new SolidColorBrush(Color.FromRgb(21, 25, 31)),
            Foreground = new SolidColorBrush(Color.FromRgb(232, 237, 243)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(61, 72, 86)),
            Padding = new Thickness(9),
            FontSize = 13
        };
        Grid.SetRow(_transcript, 1);
        layout.Children.Add(_transcript);

        _prompt = new TextBox
        {
            AcceptsReturn = true,
            MinHeight = 74,
            MaxHeight = 150,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(0, 10, 0, 8),
            Padding = new Thickness(9),
            FontSize = 13,
            Background = new SolidColorBrush(Color.FromRgb(38, 45, 55)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(75, 92, 112))
        };
        _prompt.AcceptsTab = false;
        _prompt.ToolTip = "Ejemplo: consulta la vista activa y dime qué elementos tengo seleccionados. No cambies el modelo.";
        Grid.SetRow(_prompt, 2);
        layout.Children.Add(_prompt);

        var footer = new DockPanel { LastChildFill = false };
        _status = new TextBlock
        {
            Text = projectRoot is null ? "Falta configurar la carpeta BIM-AI-POC" : "Codex aún no conectado",
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromRgb(176, 188, 204)),
            TextWrapping = TextWrapping.Wrap
        };
        DockPanel.SetDock(_status, Dock.Left);
        footer.Children.Add(_status);

        _sendButton = new Button
        {
            Content = "Enviar",
            MinWidth = 84,
            Padding = new Thickness(14, 7, 14, 7),
            Margin = new Thickness(8, 0, 0, 0),
            Background = new SolidColorBrush(Color.FromRgb(60, 130, 246)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            IsEnabled = projectRoot is not null
        };
        _sendButton.Click += async (_, _) => await SendPromptAsync();
        DockPanel.SetDock(_sendButton, Dock.Right);
        footer.Children.Add(_sendButton);
        Grid.SetRow(footer, 3);
        layout.Children.Add(footer);

        Content = layout;
        _prompt.KeyDown += async (_, args) =>
        {
            if (args.Key == System.Windows.Input.Key.Enter &&
                !System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift))
            {
                args.Handled = true;
                await SendPromptAsync();
            }
        };

        if (projectRoot is not null)
            Append("Asistente", "Conectado al proyecto local. Revisa el plan y autoriza cada operación que pueda cambiar el modelo.");
    }

    private async Task SendPromptAsync()
    {
        if (_disposed || _projectRoot is null || _sendButton.IsEnabled is false) return;
        var prompt = _prompt.Text.Trim();
        if (prompt.Length == 0) return;

        _prompt.Clear();
        Append("Tú", prompt);
        _assistantMessageOpen = false;
        _sendButton.IsEnabled = false;
        _status.Text = "Conectando con Codex…";

        try
        {
            _client ??= CreateClient();
            await _client.EnsureStartedAsync(_projectRoot);
            _status.Text = "Codex está preparando la respuesta…";
            await _client.SendMessageAsync(prompt);
            _transcript.AppendText(Environment.NewLine + Environment.NewLine);
            _status.Text = "Listo";
        }
        catch (Exception ex)
        {
            Append("Sistema", $"No se pudo completar la solicitud ({ex.GetType().Name}). Comprueba Codex, su inicio de sesión y la configuración MCP del proyecto.");
            _status.Text = "Codex no disponible";
            var failedClient = _client;
            _client = null;
            if (failedClient is not null) _ = failedClient.DisposeAsync();
        }
        finally
        {
            if (!_disposed) _sendButton.IsEnabled = true;
        }
    }

    private CodexAppServerClient CreateClient()
    {
        var client = new CodexAppServerClient();
        client.StatusChanged += value => Dispatcher.Invoke(() => _status.Text = value);
        client.TextReceived += value => Dispatcher.Invoke(() =>
        {
            if (!_assistantMessageOpen)
            {
                _transcript.AppendText("Asistente: ");
                _assistantMessageOpen = true;
            }
            AppendDelta(value);
        });
        client.TurnFailed += value => Dispatcher.Invoke(() => Append("Sistema", value));
        client.ApprovalRequested = request => Dispatcher.InvokeAsync(() => ConfirmRequest(request)).Task;
        return client;
    }

    private bool ConfirmRequest(AgentApprovalRequest request)
    {
        var details = string.Join(Environment.NewLine, new[] { request.Reason, request.Command, request.WorkingDirectory, request.RequestedPermissions }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        var answer = MessageBox.Show(
            $"Codex solicita aprobación.{Environment.NewLine}{Environment.NewLine}{details}{Environment.NewLine}{Environment.NewLine}¿Permitir? Los permisos de archivos o red aprobados pueden durar este turno.",
            "Aprobación de BIM AI",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        return answer == MessageBoxResult.Yes;
    }

    private void Append(string speaker, string message)
    {
        _transcript.AppendText($"{speaker}: {message}{Environment.NewLine}{Environment.NewLine}");
        _transcript.ScrollToEnd();
    }

    private void AppendDelta(string delta)
    {
        _transcript.AppendText(delta);
        _transcript.ScrollToEnd();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_client is not null) _ = _client.DisposeAsync();
        _client = null;
    }
}
