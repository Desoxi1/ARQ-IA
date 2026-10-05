using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace BimAiDynamoAssistant;

/// <summary>A focused chat surface embedded in Dynamo's side panel.</summary>
internal sealed class AssistantPane : UserControl, IDisposable
{
    private static readonly Brush Surface = BrushFromRgb(28, 34, 43);
    private static readonly Brush SurfaceRaised = BrushFromRgb(37, 45, 57);
    private static readonly Brush SurfaceInput = BrushFromRgb(21, 26, 34);
    private static readonly Brush PanelBorder = BrushFromRgb(56, 68, 84);
    private static readonly Brush Muted = BrushFromRgb(159, 174, 193);
    private static readonly Brush Accent = BrushFromRgb(71, 137, 247);
    private static readonly Brush ButtonAccent = BrushFromRgb(89, 200, 230);
    private static readonly Brush ButtonAccentText = BrushFromRgb(9, 37, 47);
    private static readonly Brush ButtonHoverBorder = BrushFromRgb(155, 233, 245);
    private static readonly Brush ButtonPressed = BrushFromRgb(55, 157, 183);
    private static readonly Brush ButtonBusy = BrushFromRgb(40, 124, 145);
    private static readonly Brush ButtonBusyText = BrushFromRgb(249, 253, 254);
    private static readonly Brush ButtonDisabledSurface = BrushFromRgb(100, 172, 192);
    private static readonly Brush ButtonDisabledBorder = BrushFromRgb(58, 132, 151);
    private static readonly Brush ButtonDisabledText = BrushFromRgb(18, 49, 58);
    private static readonly Brush TextBrush = BrushFromRgb(235, 240, 247);

    private const int MaxAttachmentsPerTurn = 8;
    private const long MaxPendingBytes = 40L * 1024 * 1024;

    private string? _projectRoot;
    private readonly Grid _layout;
    private FrameworkElement _contextCard;
    private readonly DispatcherTimer _readinessTimer;
    private readonly StackPanel _messages;
    private readonly ScrollViewer _conversationScroll;
    private readonly WrapPanel _attachmentChips;
    private readonly TextBlock _attachmentCount;
    private readonly TextBox _prompt;
    private readonly TextBlock _placeholder;
    private readonly Button _attachButton;
    private readonly Button _sendButton;
    private readonly TextBlock _status;
    private Button? _contextButton;
    private readonly List<LocalAttachment> _pendingAttachments = new();
    private LocalAttachmentStore? _attachmentStore;
    private CodexAppServerClient? _client;
    private TextBlock? _streamingText;
    private bool _disposed;
    private bool _turnInProgress;
    private bool _turnCancellationRequested;

    public AssistantPane(string? projectRoot)
    {
        _projectRoot = projectRoot;
        Background = Surface;
        Focusable = true;

        _layout = new Grid { Margin = new Thickness(10) };
        var layout = _layout;
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = BuildHeader();
        Grid.SetRow(header, 0);
        layout.Children.Add(header);

        _contextCard = BuildContextCard(projectRoot is not null);
        var contextCard = _contextCard;
        Grid.SetRow(contextCard, 1);
        layout.Children.Add(contextCard);

        _messages = new StackPanel { Margin = new Thickness(0, 2, 0, 8) };
        _conversationScroll = new ScrollViewer
        {
            Content = _messages,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            CanContentScroll = false,
            Margin = new Thickness(0, 8, 0, 8)
        };
        Grid.SetRow(_conversationScroll, 2);
        layout.Children.Add(_conversationScroll);

        var composer = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        composer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        composer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        composer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var attachmentHeader = new DockPanel { Margin = new Thickness(1, 0, 1, 5), LastChildFill = false };
        _attachmentCount = new TextBlock
        {
            Text = "Adjuntos",
            Foreground = Muted,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(_attachmentCount, Dock.Left);
        attachmentHeader.Children.Add(_attachmentCount);
        var clipboardHint = new TextBlock
        {
            Text = "Pega o arrastra archivos",
            Foreground = BrushFromRgb(125, 143, 164),
            FontSize = 10,
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(clipboardHint, Dock.Right);
        attachmentHeader.Children.Add(clipboardHint);
        Grid.SetRow(attachmentHeader, 0);
        composer.Children.Add(attachmentHeader);

        _attachmentChips = new WrapPanel { Orientation = Orientation.Horizontal };
        var chipScroll = new ScrollViewer
        {
            Content = _attachmentChips,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = 78,
            Margin = new Thickness(0, 0, 0, 7)
        };
        Grid.SetRow(chipScroll, 1);
        composer.Children.Add(chipScroll);

        var inputCard = new Border
        {
            Background = SurfaceInput,
            BorderBrush = PanelBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(8, 6, 8, 5)
        };
        var inputLayout = new Grid();
        inputLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        inputLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var inputArea = new Grid { MinHeight = 60 };
        _prompt = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MinHeight = 60,
            MaxHeight = 132,
            Padding = new Thickness(2),
            FontSize = 13,
            Background = Brushes.Transparent,
            Foreground = TextBrush,
            BorderThickness = new Thickness(0),
            CaretBrush = Brushes.White,
            AcceptsTab = false,
            ToolTip = "Describe lo que necesitas en Revit. Enter envía; Shift+Enter crea otra línea."
        };
        _prompt.IsEnabled = projectRoot is not null;
        _placeholder = new TextBlock
        {
            Text = projectRoot is null
                ? "El chat estará disponible al terminar la instalación inicial."
                : "Describe lo que necesitas…",
            Foreground = BrushFromRgb(123, 139, 159),
            Margin = new Thickness(4, 4, 4, 4),
            IsHitTestVisible = false,
            TextWrapping = TextWrapping.Wrap
        };
        inputArea.Children.Add(_prompt);
        inputArea.Children.Add(_placeholder);
        Grid.SetRow(inputArea, 0);
        inputLayout.Children.Add(inputArea);

        var actions = new Grid { Margin = new Thickness(0, 6, 0, 0) };
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _attachButton = CreateButton("＋ Adjuntar", ButtonAccent, ButtonAccentText, 11);
        _attachButton.Padding = new Thickness(8, 5, 8, 5);
        _attachButton.BorderBrush = ButtonAccent;
        _attachButton.BorderThickness = new Thickness(1);
        _attachButton.ToolTip = "Adjunta imágenes, documentos, archivos de Dynamo o referencias BIM.";
        _attachButton.IsEnabled = projectRoot is not null;
        _attachButton.Click += (_, _) => ChooseAttachments();
        Grid.SetColumn(_attachButton, 0);
        actions.Children.Add(_attachButton);

        _sendButton = CreateButton("Enviar  ↑", ButtonAccent, ButtonAccentText, 12);
        _sendButton.FontWeight = FontWeights.SemiBold;
        _sendButton.Padding = new Thickness(12, 7, 12, 7);
        _sendButton.BorderBrush = ButtonAccent;
        _sendButton.BorderThickness = new Thickness(1);
        _sendButton.MinWidth = 75;
        _sendButton.IsEnabled = projectRoot is not null;
        _sendButton.Click += async (_, _) =>
        {
            if (_turnInProgress) await StopCurrentTurnAsync();
            else await SendPromptAsync();
        };
        Grid.SetColumn(_sendButton, 2);
        actions.Children.Add(_sendButton);
        Grid.SetRow(actions, 1);
        inputLayout.Children.Add(actions);
        inputCard.Child = inputLayout;
        Grid.SetRow(inputCard, 2);
        composer.Children.Add(inputCard);
        Grid.SetRow(composer, 3);
        layout.Children.Add(composer);

        _status = new TextBlock
        {
            Text = projectRoot is null
                ? "Primera instalación pendiente"
                : "Listo · Codex se conecta al enviar",
            Foreground = Muted,
            FontSize = 10,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(2, 0, 2, 0)
        };
        Grid.SetRow(_status, 4);
        layout.Children.Add(_status);

        Content = layout;
        AllowDrop = true;
        PreviewDragOver += OnPreviewDragOver;
        PreviewDrop += OnPreviewDrop;
        _prompt.TextChanged += (_, _) => _placeholder.Visibility = string.IsNullOrEmpty(_prompt.Text) ? Visibility.Visible : Visibility.Collapsed;
        _prompt.PreviewKeyDown += OnPromptPreviewKeyDown;

        AddMessage("ARQ-IA", projectRoot is null
            ? "Instala ARQ-IA una sola vez desde la carpeta del proyecto. El panel se activará automáticamente al terminar."
            : "Describe lo que quieres resolver en Revit o Dynamo. Puedo usar la vista y la selección como contexto, revisar adjuntos y preparar rutinas PythonNet3 reutilizables. Antes de ejecutar cambios en el modelo, revisarás la propuesta.", isUser: false);

        _readinessTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _readinessTimer.Tick += (_, _) => TryActivateProject();
        if (projectRoot is null) _readinessTimer.Start();
    }

    private FrameworkElement BuildHeader()
    {
        var outer = new DockPanel { Margin = new Thickness(1, 1, 1, 8), LastChildFill = true };
        var markCanvas = new Canvas { Width = 32, Height = 32 };
        var markStroke = new SolidColorBrush(Color.FromRgb(248, 253, 255));
        var markCrossbar = new SolidColorBrush(Color.FromRgb(113, 229, 244));
        markCanvas.Children.Add(new System.Windows.Shapes.Line
        {
            X1 = 8, Y1 = 26, X2 = 16, Y2 = 7,
            Stroke = markStroke, StrokeThickness = 3.5,
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
        });
        markCanvas.Children.Add(new System.Windows.Shapes.Line
        {
            X1 = 16, Y1 = 7, X2 = 24, Y2 = 26,
            Stroke = markStroke, StrokeThickness = 3.5,
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
        });
        markCanvas.Children.Add(new System.Windows.Shapes.Line
        {
            X1 = 12, Y1 = 20, X2 = 20, Y2 = 20,
            Stroke = markCrossbar, StrokeThickness = 2.7,
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
        });
        var mark = new Border
        {
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(9),
            Background = new LinearGradientBrush(Color.FromRgb(20, 107, 167), Color.FromRgb(16, 57, 111), 70),
            BorderBrush = ButtonAccent,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 8, 0),
            Child = markCanvas
        };
        DockPanel.SetDock(mark, Dock.Left);
        outer.Children.Add(mark);
        var title = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        title.Children.Add(new TextBlock
        {
            Text = "ARQ-IA",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White
        });
        title.Children.Add(new TextBlock
        {
            Text = "Codex · Dynamo · Revit 2027",
            FontSize = 10,
            Margin = new Thickness(0, 2, 0, 0),
            Foreground = Muted
        });
        title.Children.Add(new TextBlock
        {
            Text = "Gestor: Branko Plaza",
            FontSize = 9,
            Margin = new Thickness(0, 1, 0, 0),
            Foreground = BrushFromRgb(125, 143, 164)
        });
        outer.Children.Add(title);
        return outer;
    }

    private FrameworkElement BuildContextCard(bool projectReady)
    {
        var card = new Border
        {
            Background = SurfaceRaised,
            BorderBrush = PanelBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 7, 8, 7),
            Margin = new Thickness(0, 0, 0, 4)
        };
        var stack = new StackPanel();
        var topRow = new DockPanel { LastChildFill = true };
        var title = new TextBlock
        {
            Text = projectReady ? "Proyecto local conectado" : "Preparación inicial",
            Foreground = projectReady ? BrushFromRgb(153, 220, 185) : BrushFromRgb(153, 196, 255),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold
        };
        if (projectReady)
        {
            _contextButton = CreateButton("Ver", BrushFromRgb(49, 69, 96), TextBrush, 10);
            _contextButton.Padding = new Thickness(8, 3, 8, 3);
            _contextButton.ToolTip = "Consultar ahora la vista activa y la selección mediante MCP, solo lectura.";
            _contextButton.Click += async (_, _) => await RequestContextAsync();
            DockPanel.SetDock(_contextButton, Dock.Right);
            topRow.Children.Add(_contextButton);
        }
        else
        {
            var retry = CreateButton("Revisar", SurfaceRaised, TextBrush, 10);
            retry.Padding = new Thickness(8, 3, 8, 3);
            retry.ToolTip = "Comprobar de nuevo la instalación de ARQ-IA.";
            retry.Click += (_, _) => TryActivateProject();
            DockPanel.SetDock(retry, Dock.Right);
            topRow.Children.Add(retry);
        }
        topRow.Children.Add(title);
        stack.Children.Add(topRow);
        stack.Children.Add(new TextBlock
        {
            Text = projectReady
                ? "Codex local · MCP Revit · rutinas Dynamo"
                : "Haz doble clic en Instalar-ARQ-IA.cmd dentro de la carpeta clonada.",
            Foreground = Muted,
            FontSize = 10,
            Margin = new Thickness(0, 3, 0, 0),
            TextWrapping = TextWrapping.Wrap
        });
        card.Child = stack;
        return card;
    }

    private void TryActivateProject()
    {
        if (_disposed || _projectRoot is not null) return;
        var projectRoot = ProjectSettings.TryGetProjectRoot();
        if (projectRoot is null) return;

        _projectRoot = projectRoot;
        _readinessTimer.Stop();
        _prompt.IsEnabled = true;
        _attachButton.IsEnabled = true;
        _sendButton.IsEnabled = true;
        _placeholder.Text = "Describe lo que necesitas…";
        _status.Text = "Listo · Codex se conecta al enviar";
        _layout.Children.Remove(_contextCard);
        _contextCard = BuildContextCard(projectReady: true);
        Grid.SetRow(_contextCard, 1);
        _layout.Children.Add(_contextCard);
        AddMessage("ARQ-IA", "Instalación detectada. Ya puedes enviar mensajes y adjuntar archivos.", isUser: false);
    }

    private async void OnPromptPreviewKeyDown(object sender, KeyEventArgs args)
    {
        if (args.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            args.Handled = true;
            await SendPromptAsync();
            return;
        }

        if (_projectRoot is not null && args.Key == Key.V && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            try
            {
                if (Clipboard.ContainsImage())
                {
                    args.Handled = true;
                    var image = Clipboard.GetImage();
                    if (image is not null) AddClipboardImage(image);
                }
            }
            catch (Exception ex) { ShowAttachmentError(ex); }
        }
    }

    private void OnPreviewDragOver(object sender, DragEventArgs args)
    {
        args.Effects = _projectRoot is not null && args.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        args.Handled = true;
    }

    private void OnPreviewDrop(object sender, DragEventArgs args)
    {
        if (_projectRoot is null) return;
        if (args.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;
        AddFiles(paths);
        args.Handled = true;
    }

    private void ChooseAttachments()
    {
        if (_disposed || _turnInProgress || _projectRoot is null) return;
        var dialog = new OpenFileDialog
        {
            Title = "Adjuntar archivos a ARQ-IA",
            Multiselect = true,
            CheckFileExists = true,
            Filter = "Archivos compatibles|*.png;*.jpg;*.jpeg;*.gif;*.webp;*.pdf;*.docx;*.xlsx;*.pptx;*.txt;*.md;*.csv;*.json;*.xml;*.yaml;*.yml;*.py;*.dyn;*.ifc;*.log;*.cs;*.js;*.ts;*.html;*.css;*.sql;*.rvt;*.rfa;*.rte;*.dwg;*.dxf;*.nwc;*.nwd|Todos los archivos|*.*"
        };
        if (dialog.ShowDialog() == true) AddFiles(dialog.FileNames);
    }

    private void AddFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (_pendingAttachments.Count >= MaxAttachmentsPerTurn)
            {
                SetStatus($"Máximo {MaxAttachmentsPerTurn} adjuntos por envío.");
                break;
            }

            try
            {
                if (_pendingAttachments.Any(item => string.Equals(item.Name, Path.GetFileName(path), StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(path) && string.Equals(item.LocalPath, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase)))
                    continue;

                var info = new FileInfo(path);
                if (_pendingAttachments.Sum(item => item.SizeBytes) + info.Length > MaxPendingBytes)
                    throw new InvalidDataException("El límite total por envío es 40 MB.");

                _attachmentStore ??= new LocalAttachmentStore(_projectRoot ?? throw new InvalidOperationException("Ejecuta Instalar-ARQ-IA.cmd desde la carpeta clonada para activar el chat."));
                _pendingAttachments.Add(_attachmentStore.AddFile(path));
                SetStatus("Adjunto listo · se enviará con tu siguiente mensaje.");
            }
            catch (Exception ex) { ShowAttachmentError(ex); }
        }
        RefreshAttachmentChips();
    }

    private void AddClipboardImage(BitmapSource image)
    {
        if (_pendingAttachments.Count >= MaxAttachmentsPerTurn)
            throw new InvalidDataException($"Máximo {MaxAttachmentsPerTurn} adjuntos por envío.");
        _attachmentStore ??= new LocalAttachmentStore(_projectRoot ?? throw new InvalidOperationException("Ejecuta Instalar-ARQ-IA.cmd desde la carpeta clonada para activar el chat."));
        var staged = _attachmentStore.AddClipboardImage(image);
        if (_pendingAttachments.Sum(item => item.SizeBytes) + staged.SizeBytes > MaxPendingBytes)
            throw new InvalidDataException("El límite total por envío es 40 MB.");
        _pendingAttachments.Add(staged);
        RefreshAttachmentChips();
        SetStatus("Imagen pegada · se enviará con tu siguiente mensaje.");
    }

    private void RefreshAttachmentChips()
    {
        _attachmentChips.Children.Clear();
        _attachmentCount.Text = _pendingAttachments.Count == 0
            ? "Adjuntos"
            : $"Adjuntos · {_pendingAttachments.Count}/{MaxAttachmentsPerTurn}";

        foreach (var attachment in _pendingAttachments)
        {
            var chip = new Border
            {
                Background = SurfaceRaised,
                BorderBrush = PanelBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(5, 4, 4, 4),
                Margin = new Thickness(0, 0, 5, 5),
                ToolTip = $"{attachment.Name} · {FormatBytes(attachment.SizeBytes)}"
            };
            var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            if (attachment.IsImage)
            {
                try
                {
                    var preview = new BitmapImage();
                    preview.BeginInit();
                    preview.UriSource = new Uri(attachment.LocalPath);
                    preview.DecodePixelWidth = 48;
                    preview.CacheOption = BitmapCacheOption.OnLoad;
                    preview.EndInit();
                    preview.Freeze();
                    row.Children.Add(new Image
                    {
                        Source = preview,
                        Width = 28,
                        Height = 28,
                        Stretch = Stretch.UniformToFill,
                        Margin = new Thickness(0, 0, 5, 0)
                    });
                }
                catch { }
            }
            else
                row.Children.Add(new TextBlock { Text = "▧", Foreground = Accent, FontSize = 15, Margin = new Thickness(1, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center });

            row.Children.Add(new TextBlock
            {
                Text = Shorten(Path.GetFileNameWithoutExtension(attachment.Name), 14) + attachment.Extension,
                Foreground = TextBrush,
                FontSize = 10,
                MaxWidth = 110,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            });
            var remove = CreateButton("×", Brushes.Transparent, Muted, 11);
            remove.Padding = new Thickness(4, 0, 4, 0);
            remove.Margin = new Thickness(2, 0, 0, 0);
            remove.ToolTip = "Quitar adjunto";
            remove.Click += (_, _) =>
            {
                _pendingAttachments.Remove(attachment);
                RefreshAttachmentChips();
            };
            row.Children.Add(remove);
            chip.Child = row;
            _attachmentChips.Children.Add(chip);
        }
    }

    private async Task SendPromptAsync(string? promptOverride = null, bool includeAttachments = true, bool clearComposer = true)
    {
        if (_disposed || _projectRoot is null || _turnInProgress || !_sendButton.IsEnabled) return;
        var prompt = promptOverride ?? _prompt.Text.Trim();
        if (prompt.Length == 0 && includeAttachments && _pendingAttachments.Count > 0)
            prompt = "Analiza los adjuntos y relaciónalos con el contexto actual de Revit.";
        if (prompt.Length == 0) return;

        var attachments = includeAttachments ? _pendingAttachments.ToArray() : Array.Empty<LocalAttachment>();
        AddMessage("Tú", prompt, isUser: true, attachments);
        _streamingText = null;
        _turnInProgress = true;
        _turnCancellationRequested = false;
        _prompt.IsEnabled = false;
        _attachButton.IsEnabled = false;
        if (_contextButton is not null) _contextButton.IsEnabled = false;
        _sendButton.Content = "Detener";
        _sendButton.Background = ButtonBusy;
        _sendButton.Foreground = ButtonBusyText;
        _sendButton.IsEnabled = true;
        SetStatus("Conectando con Codex…");

        try
        {
            _client ??= CreateClient();
            await _client.EnsureStartedAsync(_projectRoot);
            SetStatus("Consultando Codex y los MCP locales…");
            await _client.SendMessageWithAttachmentsAsync(prompt, attachments);
            if (clearComposer)
            {
                _prompt.Clear();
                if (includeAttachments)
                {
                    _pendingAttachments.Clear();
                    RefreshAttachmentChips();
                }
            }
            _messages.Children.Add(new Border { Height = 7, Opacity = 0 });
            SetStatus("Respuesta lista · el modelo sigue bajo tu control.");
        }
        catch (Exception ex)
        {
            if (_turnCancellationRequested)
                SetStatus("Turno detenido.");
            else
            {
                AddMessage("Conexión", $"No se pudo completar el envío ({ex.GetType().Name}). Revisa que Codex esté instalado, iniciado y que el MCP esté configurado.", isUser: false);
                SetStatus("No se pudo conectar con Codex.");
            }
            var failedClient = _client;
            _client = null;
            if (failedClient is not null) _ = failedClient.DisposeAsync();
        }
        finally
        {
            _turnInProgress = false;
            _turnCancellationRequested = false;
            _streamingText = null;
            if (!_disposed)
            {
                _prompt.IsEnabled = true;
                _attachButton.IsEnabled = true;
                if (_contextButton is not null) _contextButton.IsEnabled = true;
                _sendButton.Content = "Enviar  ↑";
                _sendButton.Background = ButtonAccent;
                _sendButton.Foreground = ButtonAccentText;
                _sendButton.IsEnabled = true;
                _prompt.Focus();
            }
        }
    }

    private async Task RequestContextAsync()
    {
        if (_disposed || _turnInProgress || _projectRoot is null) return;
        await SendPromptAsync(
            "Consulta mediante MCP, en modo de solo lectura, la vista activa y la selección de Revit. Resume el nombre/tipo de vista y, para la selección, cantidad, categorías e IDs. Si no hay un documento activo, indícalo. No cambies el modelo.",
            includeAttachments: false,
            clearComposer: false);
    }

    private async Task StopCurrentTurnAsync()
    {
        if (_disposed || !_turnInProgress || _turnCancellationRequested) return;
        _turnCancellationRequested = true;
        _sendButton.IsEnabled = false;
        SetStatus("Deteniendo la respuesta…");
        var client = _client;
        _client = null;
        if (client is not null) await client.DisposeAsync();
    }

    private CodexAppServerClient CreateClient()
    {
        var client = new CodexAppServerClient();
        client.StatusChanged += value => Dispatcher.Invoke(() => SetStatus(value));
        client.TextReceived += value => Dispatcher.Invoke(() =>
        {
            if (_streamingText is null)
                _streamingText = AddMessage("ARQ-IA", string.Empty, isUser: false);
            _streamingText.Text += value;
            _conversationScroll.ScrollToEnd();
        });
        client.TurnFailed += value => Dispatcher.Invoke(() => AddMessage("Codex", value, isUser: false));
        client.ApprovalRequested = request => Dispatcher.InvokeAsync(() => ConfirmRequest(request)).Task;
        return client;
    }

    private TextBlock AddMessage(string speaker, string message, bool isUser, IReadOnlyList<LocalAttachment>? attachments = null)
    {
        var messageStack = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        var heading = new TextBlock
        {
            Text = speaker,
            Foreground = isUser ? BrushFromRgb(145, 184, 255) : BrushFromRgb(136, 220, 184),
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(3, 0, 3, 4)
        };
        messageStack.Children.Add(heading);

        var bubble = new Border
        {
            Background = isUser ? BrushFromRgb(43, 63, 94) : SurfaceRaised,
            BorderBrush = isUser ? BrushFromRgb(61, 91, 133) : PanelBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(9, 7, 9, 8),
            HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            MaxWidth = double.PositiveInfinity
        };
        var contents = new StackPanel();
        var body = new TextBlock
        {
            Text = message,
            Foreground = TextBrush,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        };
        contents.Children.Add(body);
        if (attachments is { Count: > 0 })
        {
            var files = new TextBlock
            {
                Text = string.Join("\n", attachments.Select(item => item.IsImage ? "▣ " : "▤ ").Zip(attachments, (symbol, item) => symbol + item.Name)),
                Foreground = BrushFromRgb(190, 207, 231),
                FontSize = 10,
                Margin = new Thickness(0, 6, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            contents.Children.Add(files);
        }
        bubble.Child = contents;
        messageStack.Children.Add(bubble);
        _messages.Children.Add(messageStack);
        _conversationScroll.ScrollToEnd();
        return body;
    }

    private bool ConfirmRequest(AgentApprovalRequest request)
    {
        var details = string.Join(Environment.NewLine, new[] { request.Reason, request.Command, request.WorkingDirectory, request.RequestedPermissions }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        var answer = MessageBox.Show(
            $"Codex solicita aprobación.{Environment.NewLine}{Environment.NewLine}{details}{Environment.NewLine}{Environment.NewLine}¿Permitir? Los permisos de archivos o red aprobados pueden durar este turno.",
            "Aprobación de ARQ-IA",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        return answer == MessageBoxResult.Yes;
    }

    private void ShowAttachmentError(Exception exception)
    {
        SetStatus(exception.Message);
        AddMessage("Adjunto", exception.Message, isUser: false);
    }

    private void SetStatus(string value)
    {
        if (!_disposed) _status.Text = value;
    }

    private static Button CreateButton(string content, Brush background, Brush foreground, double fontSize) => new()
    {
        Content = content,
        FontSize = fontSize,
        Padding = new Thickness(8, 5, 8, 5),
        Background = background,
        Foreground = foreground,
        BorderBrush = Brushes.Transparent,
        BorderThickness = new Thickness(0),
        Template = CreateButtonTemplate(),
        Cursor = Cursors.Hand,
        FocusVisualStyle = null
    };

    private static ControlTemplate CreateButtonTemplate()
    {
        var template = new ControlTemplate(typeof(Button));
        var chrome = new FrameworkElementFactory(typeof(Border), "ButtonChrome");
        chrome.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        chrome.SetBinding(Border.BackgroundProperty, TemplateParentBinding(nameof(Control.Background)));
        chrome.SetBinding(Border.BorderBrushProperty, TemplateParentBinding(nameof(Control.BorderBrush)));
        chrome.SetBinding(Border.BorderThicknessProperty, TemplateParentBinding(nameof(Control.BorderThickness)));
        chrome.SetBinding(Border.PaddingProperty, TemplateParentBinding(nameof(Control.Padding)));

        var content = new FrameworkElementFactory(typeof(ContentPresenter), "ButtonContent");
        content.SetBinding(ContentPresenter.ContentProperty, TemplateParentBinding(nameof(ContentControl.Content)));
        content.SetBinding(ContentPresenter.ContentTemplateProperty, TemplateParentBinding(nameof(ContentControl.ContentTemplate)));
        content.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
        content.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        content.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
        chrome.AppendChild(content);
        template.VisualTree = chrome;

        var hover = new MultiTrigger();
        hover.Conditions.Add(new Condition(UIElement.IsMouseOverProperty, true));
        hover.Conditions.Add(new Condition(UIElement.IsEnabledProperty, true));
        hover.Setters.Add(new Setter(Border.BorderBrushProperty, ButtonHoverBorder, "ButtonChrome"));
        template.Triggers.Add(hover);

        var pressed = new MultiTrigger();
        pressed.Conditions.Add(new Condition(ButtonBase.IsPressedProperty, true));
        pressed.Conditions.Add(new Condition(UIElement.IsEnabledProperty, true));
        pressed.Setters.Add(new Setter(Border.BackgroundProperty, ButtonPressed, "ButtonChrome"));
        template.Triggers.Add(pressed);

        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(Border.BackgroundProperty, ButtonDisabledSurface, "ButtonChrome"));
        disabled.Setters.Add(new Setter(Border.BorderBrushProperty, ButtonDisabledBorder, "ButtonChrome"));
        disabled.Setters.Add(new Setter(Control.ForegroundProperty, ButtonDisabledText));
        template.Triggers.Add(disabled);

        return template;
    }

    private static Binding TemplateParentBinding(string path) => new(path)
    {
        RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
    };

    private static Brush BrushFromRgb(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }

    private static string Shorten(string value, int maxLength) => value.Length <= maxLength ? value : value[..Math.Max(1, maxLength - 1)] + "…";

    private static string FormatBytes(long bytes) => bytes >= 1024 * 1024
        ? $"{bytes / (1024d * 1024d):0.#} MB"
        : $"{Math.Max(1, bytes / 1024)} KB";

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _readinessTimer.Stop();
        if (_client is not null) _ = _client.DisposeAsync();
        _client = null;
        _attachmentStore?.Dispose();
        _attachmentStore = null;
    }
}
