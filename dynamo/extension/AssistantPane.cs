using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Documents;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace BimAiDynamoAssistant;

/// <summary>A focused chat surface embedded in Dynamo's side panel.</summary>
internal sealed class AssistantPane : UserControl, IDisposable
{
    private static readonly Brush Surface = BrushFromRgb(47, 47, 47);
    private static readonly Brush SurfaceRaised = BrushFromRgb(53, 53, 53);
    private static readonly Brush SurfaceInput = BrushFromRgb(46, 46, 46);
    private static readonly Brush PanelBorder = BrushFromRgb(77, 77, 77);
    private static readonly Brush Muted = BrushFromRgb(177, 181, 187);
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
    private ComboBox? _modelBox;
    private IReadOnlyList<CodexModelInfo> _availableModels = Array.Empty<CodexModelInfo>();
    private bool _loadingModels;
    private readonly List<LocalAttachment> _pendingAttachments = new();
    private LocalAttachmentStore? _attachmentStore;
    private CodexAppServerClient? _client;
    private TextBlock? _streamingText;
    private string _streamingRaw = string.Empty;
    private TextBlock? _connectionLabel;
    private Border? _connectionBadge;
    private readonly Dictionary<string, (TextBlock Status, TextBlock Detail)> _toolCards = new();
    private bool _disposed;
    private bool _turnInProgress;
    private bool _turnCancellationRequested;

    public AssistantPane(string? projectRoot)
    {
        _projectRoot = projectRoot;
        Background = Surface;
        Focusable = true;

        _layout = new Grid { Margin = new Thickness(12) };
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

        var attachmentHeader = new DockPanel { Margin = new Thickness(1, 0, 1, 5), LastChildFill = false, Visibility = Visibility.Collapsed };
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
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(11, 10, 9, 8)
        };
        var inputLayout = new Grid();
        inputLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        inputLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var inputArea = new Grid { MinHeight = 42 };
        _prompt = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MinHeight = 42,
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
                : "Pregunta sobre tu modelo, adjunta archivos o solicita una rutina Dynamo…",
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
        _attachButton = CreateButton("⊕", Brushes.Transparent, TextBrush, 18);
        _attachButton.Padding = new Thickness(5, 1, 5, 1);
        _attachButton.ToolTip = "Adjunta imágenes, documentos, archivos de Dynamo o referencias BIM.";
        _attachButton.IsEnabled = projectRoot is not null;
        _attachButton.Click += (_, _) => ChooseAttachments();
        Grid.SetColumn(_attachButton, 0);
        actions.Children.Add(_attachButton);

        var modelRow = BuildModelRow();
        Grid.SetColumn(modelRow, 1);
        actions.Children.Add(modelRow);

        _sendButton = CreateButton("↑", BrushFromRgb(30, 30, 30), TextBrush, 17);
        _sendButton.FontWeight = FontWeights.SemiBold;
        _sendButton.Padding = new Thickness(4, 1, 4, 1);
        _sendButton.Width = 31;
        _sendButton.Height = 31;
        _sendButton.Template = CreateButtonTemplate(16);
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

        if (projectRoot is null)
            AddMessage("ARQ-IA", "Instala ARQ-IA una sola vez desde la carpeta del proyecto. El panel se activará automáticamente al terminar.", isUser: false);

        _readinessTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _readinessTimer.Tick += (_, _) => TryActivateProject();
        if (projectRoot is null) _readinessTimer.Start();
        else _ = LoadModelChoicesAsync();
    }

    private FrameworkElement BuildHeader()
    {
        var outer = new DockPanel { Margin = new Thickness(1, 1, 1, 12), LastChildFill = true };
        var newChat = CreateButton("▢", Brushes.Transparent, TextBrush, 16);
        newChat.ToolTip = "Nueva conversación de ARQ-IA";
        newChat.Padding = new Thickness(4, 2, 4, 2);
        newChat.Click += async (_, _) => await NewConversationAsync();
        DockPanel.SetDock(newChat, Dock.Right);
        outer.Children.Add(newChat);
        _contextButton = CreateButton("↻", Brushes.Transparent, TextBrush, 17);
        _contextButton.ToolTip = "Volver a consultar la vista y selección de Revit (solo lectura)";
        _contextButton.Padding = new Thickness(4, 2, 4, 2);
        _contextButton.IsEnabled = _projectRoot is not null;
        _contextButton.Click += async (_, _) => await RequestContextAsync();
        DockPanel.SetDock(_contextButton, Dock.Right);
        outer.Children.Add(_contextButton);
        _connectionLabel = new TextBlock
        {
            Text = "MCP sin verificar",
            FontSize = 10,
            Foreground = Muted
        };
        _connectionBadge = new Border
        {
            Child = _connectionLabel,
            Background = BrushFromRgb(65, 65, 65),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(7, 2, 7, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0)
        };
        DockPanel.SetDock(_connectionBadge, Dock.Right);
        outer.Children.Add(_connectionBadge);
        var markCanvas = new Canvas { Width = 32, Height = 32 };
        foreach (var (x, y) in new[] { (9d, 9d), (17d, 9d), (9d, 17d) })
        {
            var square = new System.Windows.Shapes.Rectangle
            {
                Width = 6, Height = 6, RadiusX = 1, RadiusY = 1,
                Stroke = TextBrush, StrokeThickness = 1.2
            };
            Canvas.SetLeft(square, x);
            Canvas.SetTop(square, y);
            markCanvas.Children.Add(square);
        }
        var lastSquare = new System.Windows.Shapes.Rectangle
        {
            Width = 6, Height = 6, RadiusX = 1, RadiusY = 1,
            Fill = TextBrush
        };
        Canvas.SetLeft(lastSquare, 17);
        Canvas.SetTop(lastSquare, 17);
        markCanvas.Children.Add(lastSquare);
        var mark = new Border
        {
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(7),
            Background = BrushFromRgb(35, 35, 35),
            Margin = new Thickness(0, 0, 8, 0),
            ToolTip = "ARQ-IA · Gestor: Branko Plaza",
            Child = markCanvas
        };
        DockPanel.SetDock(mark, Dock.Left);
        outer.Children.Add(mark);
        var title = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        title.Children.Add(new TextBlock
        {
            Text = "ARQ-IA",
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White
        });
        title.Children.Add(new TextBlock
        {
            Text = "Asistente BIM · Revit 2027",
            FontSize = 10,
            Foreground = Muted
        });
        outer.Children.Add(title);
        var header = new Grid();
        header.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        header.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(outer, 0);
        header.Children.Add(outer);
        var divider = new Border { Height = 1, Background = BrushFromRgb(62, 62, 62), Margin = new Thickness(0, 0, 0, 6) };
        Grid.SetRow(divider, 1);
        header.Children.Add(divider);
        return header;
    }

    private FrameworkElement BuildContextCard(bool projectReady)
    {
        if (projectReady) return new Border { Visibility = Visibility.Collapsed };
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
        var retry = CreateButton("Revisar", SurfaceRaised, TextBrush, 10);
        retry.Padding = new Thickness(8, 3, 8, 3);
        retry.ToolTip = "Comprobar de nuevo la instalación de ARQ-IA.";
        retry.Click += (_, _) => TryActivateProject();
        DockPanel.SetDock(retry, Dock.Right);
        topRow.Children.Add(retry);
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

    private FrameworkElement BuildModelRow()
    {
        var row = new DockPanel { Margin = new Thickness(5, 0, 8, 0), LastChildFill = false, VerticalAlignment = VerticalAlignment.Center };
        var label = new TextBlock
        {
            Text = "Codex · Revit MCP",
            Foreground = Muted,
            FontSize = 10,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 7, 0)
        };
        DockPanel.SetDock(label, Dock.Left);
        row.Children.Add(label);

        _modelBox = new ComboBox
        {
            FontSize = 10,
            Width = 112,
            MaxDropDownHeight = 280,
            Background = SurfaceRaised,
            Foreground = TextBrush,
            BorderBrush = PanelBorder,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4, 2, 4, 2),
            Template = CreateDarkComboBoxTemplate(),
            ToolTip = "Modelos reales que ofrece Codex en este equipo. Automático usa Luna para consultas breves y el modelo predeterminado para tareas generales."
        };
        var itemStyle = new Style(typeof(ComboBoxItem));
        itemStyle.Setters.Add(new Setter(Control.BackgroundProperty, SurfaceRaised));
        itemStyle.Setters.Add(new Setter(Control.ForegroundProperty, TextBrush));
        itemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(6, 4, 6, 4)));
        _modelBox.ItemContainerStyle = itemStyle;
        _modelBox.Items.Add(new ComboBoxItem { Content = "Automático", Tag = null });
        _modelBox.SelectedIndex = 0;
        _modelBox.IsEnabled = _projectRoot is not null;
        _modelBox.SelectionChanged += (_, _) =>
        {
            if (_loadingModels || _modelBox?.SelectedItem is not ComboBoxItem selected) return;
            CodexModelSelection.Save(selected.Tag as string);
            SetStatus($"Modelo {selected.Content} · se aplicará al próximo envío.");
        };
        DockPanel.SetDock(_modelBox, Dock.Right);
        row.Children.Add(_modelBox);

        return row;
    }

    private async Task LoadModelChoicesAsync()
    {
        if (_disposed || _projectRoot is null || _modelBox is null) return;
        try
        {
            _client ??= CreateClient();
            await _client.EnsureInitializedAsync(_projectRoot);
            var models = await _client.ListModelsAsync();
            if (_disposed || _modelBox is null) return;
            if (_connectionLabel is not null) _connectionLabel.Text = "MCP sin verificar";
            _availableModels = models;
            _loadingModels = true;
            var saved = CodexModelSelection.Load();
            _modelBox.Items.Clear();
            _modelBox.Items.Add(new ComboBoxItem { Content = "Automático", Tag = null });
            foreach (var model in models)
                _modelBox.Items.Add(new ComboBoxItem { Content = model.DisplayName, Tag = model.Id });
            _modelBox.SelectedItem = _modelBox.Items.OfType<ComboBoxItem>()
                .FirstOrDefault(item => item.Tag is string id && id == saved) ?? _modelBox.Items[0];
            _loadingModels = false;
            SetStatus(models.Count > 0
                ? $"{models.Count} modelos disponibles en el catálogo de Codex."
                : "No se pudo listar modelos; Codex usará el predeterminado.");
        }
        catch (Exception ex)
        {
            _loadingModels = false;
            SetStatus($"Modelos no disponibles ({ex.GetType().Name}); se usará el predeterminado.");
            var client = _client;
            _client = null;
            if (client is not null) await client.DisposeAsync();
        }
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
        _placeholder.Text = "Pregunta sobre tu modelo, adjunta archivos o solicita una rutina Dynamo…";
        if (_contextButton is not null) _contextButton.IsEnabled = true;
        if (_modelBox is not null) _modelBox.IsEnabled = true;
        _status.Text = "Listo · Codex se conecta al enviar";
        _layout.Children.Remove(_contextCard);
        _contextCard = BuildContextCard(projectReady: true);
        Grid.SetRow(_contextCard, 1);
        _layout.Children.Add(_contextCard);
        AddMessage("ARQ-IA", "Instalación detectada. Ya puedes enviar mensajes y adjuntar archivos.", isUser: false);
        _ = LoadModelChoicesAsync();
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

    private async Task SendPromptAsync(string? promptOverride = null, bool includeAttachments = true, bool clearComposer = true, bool preferLightweight = false)
    {
        if (_disposed || _projectRoot is null || _turnInProgress || !_sendButton.IsEnabled) return;
        var prompt = promptOverride ?? _prompt.Text.Trim();
        if (prompt.Length == 0 && includeAttachments && _pendingAttachments.Count > 0)
            prompt = "Analiza los adjuntos y relaciónalos con el contexto actual de Revit.";
        if (prompt.Length == 0) return;

        var selectedModel = ResolveModel(prompt, preferLightweight);
        var modelInfo = _availableModels.FirstOrDefault(candidate => candidate.Id == selectedModel);
        var effort = modelInfo?.DefaultEffort ?? "medium";
        var attachments = includeAttachments ? _pendingAttachments.ToArray() : Array.Empty<LocalAttachment>();
        var estimateTokens = (prompt.Length + attachments.Where(a => a.TextContent is not null).Sum(a => a.TextContent!.Length)) / 4;
        AddMessage("Tú", prompt, isUser: true, attachments);
        _streamingText = null;
        _streamingRaw = string.Empty;
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
            await _client.EnsureStartedAsync(_projectRoot, selectedModel, effort);
            SetStatus($"Consultando Codex ({modelInfo?.DisplayName ?? "predeterminado"} · ≈{estimateTokens} tokens de entrada)…");
            await _client.SendMessageWithAttachmentsAsync(prompt, attachments, selectedModel, effort);
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
                _sendButton.Content = "↑";
                _sendButton.Background = BrushFromRgb(30, 30, 30);
                _sendButton.Foreground = TextBrush;
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
            clearComposer: false,
            preferLightweight: true);
    }

    private string? ResolveModel(string prompt, bool preferLightweight)
    {
        if (_modelBox?.SelectedItem is ComboBoxItem { Tag: string chosen }) return chosen;
        var briefRead = preferLightweight || (prompt.Length < 180 &&
            (prompt.Contains("vista", StringComparison.OrdinalIgnoreCase) ||
             prompt.Contains("selecci", StringComparison.OrdinalIgnoreCase) ||
             prompt.Contains("lista", StringComparison.OrdinalIgnoreCase)) &&
            !prompt.Contains("modifica", StringComparison.OrdinalIgnoreCase) &&
            !prompt.Contains("crea", StringComparison.OrdinalIgnoreCase));
        if (briefRead)
            return _availableModels.FirstOrDefault(model => model.Id.EndsWith("-luna", StringComparison.OrdinalIgnoreCase))?.Id;
        return _availableModels.FirstOrDefault(model => model.IsDefault)?.Id;
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
            _streamingRaw += value;
            RenderMarkdown(_streamingText, _streamingRaw);
            _conversationScroll.ScrollToEnd();
        });
        client.ToolActivity += activity => Dispatcher.Invoke(() => UpdateToolCard(activity));
        client.TurnFailed += value => Dispatcher.Invoke(() => AddMessage("Codex", value, isUser: false));
        client.ApprovalRequested = request => Dispatcher.InvokeAsync(() => ConfirmRequest(request)).Task;
        return client;
    }

    private async Task NewConversationAsync()
    {
        if (_disposed || _turnInProgress) return;
        var old = _client;
        _client = null;
        _streamingText = null;
        _streamingRaw = string.Empty;
        _toolCards.Clear();
        _messages.Children.Clear();
        AddMessage("ARQ-IA", "Nueva conversación. El modelo Revit permanece sin cambios.", isUser: false);
        if (_connectionLabel is not null)
        {
            _connectionLabel.Text = "MCP sin verificar";
            _connectionLabel.Foreground = Muted;
            if (_connectionBadge is not null) _connectionBadge.Background = BrushFromRgb(65, 65, 65);
        }
        if (old is not null) await old.DisposeAsync();
        if (_projectRoot is not null) await LoadModelChoicesAsync();
    }

    private void UpdateToolCard(McpToolActivity activity)
    {
        var key = string.IsNullOrWhiteSpace(activity.Id) ? activity.Server + "." + activity.Tool : activity.Id;
        if (!_toolCards.TryGetValue(key, out var fields))
        {
            var card = new Border
            {
                Background = BrushFromRgb(49, 49, 49),
                BorderBrush = PanelBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(11, 9, 11, 9),
                Margin = new Thickness(0, 0, 0, 12)
            };
            var stack = new StackPanel();
            var top = new DockPanel();
            var badge = new Border
            {
                Background = BrushFromRgb(66, 66, 66),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(6, 1, 6, 1),
                Child = new TextBlock { Text = "MCP", Foreground = TextBrush, FontSize = 9 }
            };
            DockPanel.SetDock(badge, Dock.Right);
            top.Children.Add(badge);
            top.Children.Add(new TextBlock
            {
                Text = "⚙  " + FriendlyToolLabel(activity),
                Foreground = TextBrush,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap
            });
            stack.Children.Add(top);
            var state = new TextBlock { Foreground = Muted, FontSize = 10, Margin = new Thickness(0, 4, 0, 0) };
            stack.Children.Add(state);
            var detail = new TextBlock { Foreground = Muted, FontSize = 10, TextWrapping = TextWrapping.Wrap };
            stack.Children.Add(new Expander
            {
                Header = "Detalles técnicos",
                Content = detail,
                FontSize = 10,
                Foreground = Muted,
                Margin = new Thickness(0, 4, 0, 0),
                Visibility = Visibility.Collapsed
            });
            card.Child = stack;
            _messages.Children.Add(card);
            fields = (state, detail);
            _toolCards[key] = fields;
        }
        fields.Status.Text = activity.Status switch
        {
            "inProgress" => "Consultando herramienta…",
            "completed" => "Completada · respuesta recibida",
            "failed" => "Error de herramienta",
            _ => activity.Status
        };
        fields.Status.Foreground = activity.Status == "completed" ? BrushFromRgb(136, 220, 184) :
            activity.Status == "failed" ? BrushFromRgb(255, 153, 153) : Muted;
        if (!string.IsNullOrWhiteSpace(activity.Detail))
        {
            fields.Detail.Text = activity.Detail.Length > 3000 ? activity.Detail[..3000] + "…" : activity.Detail;
            if (fields.Detail.Parent is Expander expander) expander.Visibility = Visibility.Visible;
        }
        if (activity.Status == "completed" && _connectionLabel is not null)
        {
            _connectionLabel.Text = "MCP conectado";
            _connectionLabel.Foreground = BrushFromRgb(85, 219, 110);
            if (_connectionBadge is not null) _connectionBadge.Background = BrushFromRgb(39, 65, 42);
        }
        _conversationScroll.ScrollToEnd();
    }

    private static string FriendlyToolLabel(McpToolActivity activity)
    {
        if (activity.Server == "rvt_mcp")
        {
            var name = activity.Tool switch
            {
                "revit_get_selected_elements" => "Get Selection",
                "revit_get_current_view_info" => "Get Active View",
                "revit_list_materials" => "List Materials",
                "revit_override_element_graphics" => "Override Graphics",
                _ => activity.Tool.Replace("revit_", string.Empty, StringComparison.Ordinal).Replace('_', ' ')
            };
            return "Revit MCP · " + name;
        }
        if (activity.Server == "bim_ai_routines") return "Rutinas Dynamo · " + activity.Tool.Replace('_', ' ');
        if (activity.Server == "bim_ai_dynamo_runner") return "Dynamo Runner · " + activity.Tool.Replace('_', ' ');
        return activity.Server + " · " + activity.Tool;
    }

    private TextBlock AddMessage(string speaker, string message, bool isUser, IReadOnlyList<LocalAttachment>? attachments = null)
    {
        var messageStack = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
        if (!isUser)
        {
            var heading = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(3, 0, 3, 6) };
            heading.Children.Add(new TextBlock { Text = "◉", Foreground = TextBrush, FontSize = 11, Margin = new Thickness(0, 0, 7, 0) });
            heading.Children.Add(new TextBlock { Text = speaker, Foreground = TextBrush, FontSize = 12, FontWeight = FontWeights.SemiBold });
            heading.Children.Add(new TextBlock { Text = "Respuesta del asistente", Foreground = Muted, FontSize = 9, Margin = new Thickness(7, 2, 0, 0) });
            messageStack.Children.Add(heading);
        }

        var bubble = new Border
        {
            Background = isUser ? BrushFromRgb(31, 31, 31) : Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(12),
            Padding = isUser ? new Thickness(12, 9, 12, 9) : new Thickness(3, 3, 3, 4),
            HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            MaxWidth = double.PositiveInfinity
        };
        var contents = new StackPanel();
        var body = new TextBlock
        {
            Foreground = TextBrush,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap
        };
        if (isUser) body.Text = message;
        else RenderMarkdown(body, message);
        contents.Children.Add(body);
        if (attachments is { Count: > 0 })
        {
            var files = new TextBlock
            {
                Text = string.Join("\n", attachments.Select(item => item.IsImage ? "▣ " : "▤ ").Zip(attachments, (symbol, item) => symbol + item.Name)),
                Foreground = Muted,
                FontSize = 10,
                Margin = new Thickness(0, 6, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            contents.Children.Add(files);
        }
        bubble.Child = contents;
        messageStack.Children.Add(bubble);
        if (!isUser)
        {
            var copy = CreateButton("▢", Brushes.Transparent, Muted, 13);
            copy.Padding = new Thickness(3, 1, 3, 1);
            copy.Margin = new Thickness(5, 3, 0, 0);
            copy.HorizontalAlignment = HorizontalAlignment.Left;
            copy.ToolTip = "Copiar esta respuesta";
            copy.Click += (_, _) =>
            {
                if (body.Tag is string content && content.Length > 0)
                    Clipboard.SetText(content);
            };
            messageStack.Children.Add(copy);
        }
        _messages.Children.Add(messageStack);
        _conversationScroll.ScrollToEnd();
        return body;
    }

    private static void RenderMarkdown(TextBlock target, string raw)
    {
        target.Tag = raw;
        target.Inlines.Clear();
        var lines = raw.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var inCode = false;
        var fence = new string((char)96, 3);
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            if (line.TrimStart().StartsWith(fence, StringComparison.Ordinal))
            {
                inCode = !inCode;
                continue;
            }
            if (index > 0) target.Inlines.Add(new LineBreak());
            if (inCode)
            {
                target.Inlines.Add(new Run(line)
                {
                    FontFamily = new FontFamily("Consolas"),
                    Foreground = BrushFromRgb(184, 213, 231)
                });
                continue;
            }
            var heading = line.StartsWith("### ", StringComparison.Ordinal) ? 3 :
                line.StartsWith("## ", StringComparison.Ordinal) ? 2 :
                line.StartsWith("# ", StringComparison.Ordinal) ? 1 : 0;
            if (heading > 0)
            {
                target.Inlines.Add(new Run(line[(heading + 1)..])
                {
                    FontWeight = FontWeights.SemiBold,
                    FontSize = heading == 1 ? 15 : heading == 2 ? 14 : 13
                });
                continue;
            }
            if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                target.Inlines.Add(new Run("• "));
                line = line[2..];
            }
            AppendInlineFormatting(target, line);
        }
    }

    private static void AppendInlineFormatting(TextBlock target, string line)
    {
        // Render only a small, safe subset. Tool output stays plain text and never becomes active markup.
        var marker = new string((char)96, 1);
        var parts = Regex.Split(line, "(\\*\\*[^*]+\\*\\*|\\*[^*]+\\*|" + marker + "[^" + marker + "]+" + marker + ")");
        foreach (var part in parts)
        {
            if (part.Length == 0) continue;
            if (part.StartsWith("**", StringComparison.Ordinal) && part.EndsWith("**", StringComparison.Ordinal) && part.Length > 4)
                target.Inlines.Add(new Run(part[2..^2]) { FontWeight = FontWeights.SemiBold });
            else if (part.StartsWith("*", StringComparison.Ordinal) && part.EndsWith("*", StringComparison.Ordinal) && part.Length > 2)
                target.Inlines.Add(new Run(part[1..^1]) { FontStyle = FontStyles.Italic });
            else if (part.StartsWith(marker, StringComparison.Ordinal) && part.EndsWith(marker, StringComparison.Ordinal) && part.Length > 2)
                target.Inlines.Add(new Run(part[1..^1]) { FontFamily = new FontFamily("Consolas"), Foreground = BrushFromRgb(154, 220, 234) });
            else target.Inlines.Add(new Run(part));
        }
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

    private static ControlTemplate CreateDarkComboBoxTemplate()
    {
        var toggleTemplate = new ControlTemplate(typeof(ToggleButton));
        var toggleBorder = new FrameworkElementFactory(typeof(Border));
        toggleBorder.SetValue(Border.BackgroundProperty, SurfaceRaised);
        toggleBorder.SetValue(Border.BorderBrushProperty, PanelBorder);
        toggleBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        toggleBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(5));
        toggleBorder.SetValue(Border.PaddingProperty, new Thickness(6, 4, 6, 4));
        var toggleContent = new FrameworkElementFactory(typeof(DockPanel));
        var arrow = new FrameworkElementFactory(typeof(TextBlock));
        arrow.SetValue(TextBlock.TextProperty, "⌄");
        arrow.SetValue(TextBlock.ForegroundProperty, Muted);
        arrow.SetValue(DockPanel.DockProperty, Dock.Right);
        toggleContent.AppendChild(arrow);
        var selected = new FrameworkElementFactory(typeof(TextBlock));
        selected.SetBinding(TextBlock.TextProperty, new Binding("SelectedItem.Content")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(ComboBox), 1)
        });
        selected.SetValue(TextBlock.ForegroundProperty, TextBrush);
        selected.SetValue(TextBlock.FontSizeProperty, 10d);
        selected.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        toggleContent.AppendChild(selected);
        toggleBorder.AppendChild(toggleContent);
        toggleTemplate.VisualTree = toggleBorder;

        var template = new ControlTemplate(typeof(ComboBox));
        var root = new FrameworkElementFactory(typeof(Grid));
        var toggle = new FrameworkElementFactory(typeof(ToggleButton));
        toggle.SetValue(ToggleButton.TemplateProperty, toggleTemplate);
        toggle.SetValue(ToggleButton.FocusableProperty, false);
        toggle.SetBinding(ToggleButton.IsCheckedProperty, new Binding("IsDropDownOpen")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent),
            Mode = BindingMode.TwoWay
        });
        root.AppendChild(toggle);
        var popup = new FrameworkElementFactory(typeof(Popup), "PART_Popup");
        popup.SetValue(Popup.PlacementProperty, PlacementMode.Bottom);
        popup.SetValue(Popup.AllowsTransparencyProperty, true);
        popup.SetValue(Popup.FocusableProperty, false);
        popup.SetBinding(Popup.IsOpenProperty, new Binding("IsDropDownOpen")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
        });
        var popupBorder = new FrameworkElementFactory(typeof(Border));
        popupBorder.SetValue(Border.BackgroundProperty, SurfaceRaised);
        popupBorder.SetValue(Border.BorderBrushProperty, PanelBorder);
        popupBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        popupBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        popupBorder.SetValue(Border.MinWidthProperty, 180d);
        var scroll = new FrameworkElementFactory(typeof(ScrollViewer));
        scroll.SetValue(ScrollViewer.CanContentScrollProperty, true);
        scroll.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
        scroll.AppendChild(new FrameworkElementFactory(typeof(ItemsPresenter)));
        popupBorder.AppendChild(scroll);
        popup.AppendChild(popupBorder);
        root.AppendChild(popup);
        template.VisualTree = root;
        return template;
    }

    private static ControlTemplate CreateButtonTemplate(double cornerRadius = 6)
    {
        var template = new ControlTemplate(typeof(Button));
        var chrome = new FrameworkElementFactory(typeof(Border), "ButtonChrome");
        chrome.SetValue(Border.CornerRadiusProperty, new CornerRadius(cornerRadius));
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
