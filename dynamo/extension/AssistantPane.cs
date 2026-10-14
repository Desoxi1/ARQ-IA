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
    private TextBlock _status = null!;
    private Button? _contextButton;
    private Button? _accessButton;
    private Button? _historyButton;
    private Popup? _historyPopup;
    private bool _fullAccessEnabled;
    private ComboBox? _modelBox;
    private ComboBox? _effortBox;
    private IReadOnlyList<CodexModelInfo> _availableModels = Array.Empty<CodexModelInfo>();
    private bool _loadingModels;
    private string _activeTurnModelName = "Codex predeterminado";
    private string _activeTurnEffort = "medium";
    private readonly List<LocalAttachment> _pendingAttachments = new();
    private LocalAttachmentStore? _attachmentStore;
    private CodexAppServerClient? _client;
    private ConversationHistoryStore? _conversationStore;
    private ConversationHistoryRecord? _activeConversation;
    private bool _continueLocalConversation;
    private RichTextBox? _streamingText;
    private StackPanel? _streamingMessageContainer;
    private string _streamingRaw = string.Empty;
    private readonly Dictionary<string, (TextBlock Status, RichTextBox Detail)> _toolCards = new();
    private bool _disposed;
    private bool _turnInProgress;
    private bool _turnCancellationRequested;

    public AssistantPane(string? projectRoot)
    {
        _projectRoot = projectRoot;
        if (projectRoot is not null) _conversationStore = new ConversationHistoryStore(projectRoot);
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
        _attachButton = CreateButton("+", Brushes.Transparent, ButtonAccent, 18);
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

        var footer = BuildFooter(projectRoot is not null);
        Grid.SetRow(footer, 4);
        layout.Children.Add(footer);

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
        var historyButton = CreateButton("◷", Brushes.Transparent, TextBrush, 16);
        _historyButton = historyButton;
        historyButton.Padding = new Thickness(4, 2, 4, 2);
        historyButton.ToolTip = "Historial de conversaciones guardado localmente en este proyecto";
        historyButton.IsEnabled = _projectRoot is not null;
        historyButton.Click += (_, _) => ToggleHistoryPopup(historyButton);
        DockPanel.SetDock(_historyButton, Dock.Right);
        outer.Children.Add(_historyButton);
        UpdateHistoryButton();
        var markCanvas = new Canvas { Width = 32, Height = 32 };
        markCanvas.Children.Add(new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M7.5,25.5 L16,6.8 L24.5,25.5"),
            Stroke = Brushes.White,
            StrokeThickness = 2.5,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round
        });
        markCanvas.Children.Add(new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M11.5,19.4 L20.5,19.4"),
            Stroke = BrushFromRgb(177, 245, 250),
            StrokeThickness = 2.2,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        });
        var mark = new Border
        {
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(7),
            Background = new LinearGradientBrush(Color.FromRgb(20, 107, 167), Color.FromRgb(16, 57, 111), 70),
            BorderBrush = BrushFromRgb(89, 200, 230),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 8, 0),
            ToolTip = "ARQ-IA · Asistente BIM",
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

    private void ToggleHistoryPopup(Button anchor)
    {
        if (_historyPopup is { IsOpen: true })
        {
            _historyPopup.IsOpen = false;
            return;
        }

        var records = _conversationStore?.ListRecent(500) ?? Array.Empty<ConversationHistoryRecord>();
        var items = new StackPanel();
        items.Children.Add(new TextBlock
        {
            Text = $"Historial local · {records.Count} conversaciones",
            Foreground = TextBrush,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(8, 7, 8, 8)
        });

        Button? firstItem = null;
        if (records.Count == 0)
        {
            items.Children.Add(new TextBlock
            {
                Text = "Tus conversaciones aparecerán aquí y se guardan solo en este PC, dentro de la carpeta del proyecto.",
                Foreground = Muted,
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(8, 0, 8, 10)
            });
        }
        else
        {
            foreach (var record in records)
            {
                var item = CreateButton(string.Empty, Brushes.Transparent, TextBrush, 11);
                item.HorizontalContentAlignment = HorizontalAlignment.Left;
                item.Padding = new Thickness(8, 7, 8, 7);
                item.Margin = new Thickness(2, 1, 2, 1);
                item.ToolTip = $"{record.Title}\n{ConversationHistoryStore.Preview(record)}";
                var content = new StackPanel();
                var titleRow = new DockPanel { LastChildFill = true };
                var date = new TextBlock
                {
                    Text = record.UpdatedUtc.ToLocalTime().ToString("dd MMM · HH:mm"),
                    Foreground = Muted,
                    FontSize = 9,
                    Margin = new Thickness(8, 1, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                DockPanel.SetDock(date, Dock.Right);
                titleRow.Children.Add(date);
                titleRow.Children.Add(new TextBlock
                {
                    Text = record.Title,
                    Foreground = TextBrush,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
                content.Children.Add(titleRow);
                content.Children.Add(new TextBlock
                {
                    Text = ConversationHistoryStore.Preview(record),
                    Foreground = Muted,
                    FontSize = 9,
                    TextWrapping = TextWrapping.Wrap,
                    MaxHeight = 34,
                    Margin = new Thickness(0, 3, 0, 0)
                });
                item.Content = content;
                item.Click += async (_, _) =>
                {
                    if (_historyPopup is not null) _historyPopup.IsOpen = false;
                    await OpenConversationAsync(record.Id);
                };
                firstItem ??= item;
                items.Children.Add(item);
            }
        }

        var scroll = new ScrollViewer
        {
            Content = items,
            Width = 310,
            MaxHeight = 390,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            CanContentScroll = true
        };
        var surface = new Border
        {
            Background = SurfaceRaised,
            BorderBrush = PanelBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(4),
            Child = scroll
        };
        var popup = new Popup
        {
            PlacementTarget = anchor,
            Placement = PlacementMode.Bottom,
            HorizontalOffset = -282,
            AllowsTransparency = true,
            StaysOpen = false,
            Focusable = true,
            Child = surface
        };
        surface.PreviewKeyDown += (_, args) =>
        {
            if (args.Key != Key.Escape) return;
            popup.IsOpen = false;
            anchor.Focus();
            args.Handled = true;
        };
        popup.Opened += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            if (firstItem is not null) firstItem.Focus();
            else anchor.Focus();
        }));
        popup.Closed += (_, _) =>
        {
            if (ReferenceEquals(_historyPopup, popup)) _historyPopup = null;
        };
        _historyPopup = popup;
        popup.IsOpen = true;
    }

    private void UpdateHistoryButton()
    {
        if (_historyButton is null) return;
        var count = _conversationStore?.Count ?? 0;
        _historyButton.ToolTip = count == 0
            ? "Historial local de conversaciones (vacío)"
            : $"Historial local · {count} conversaciones";
    }

    private FrameworkElement BuildFooter(bool projectReady)
    {
        var footer = new Grid { Margin = new Thickness(2, 2, 2, 0), VerticalAlignment = VerticalAlignment.Center };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _status = new TextBlock
        {
            Text = projectReady ? "Listo · Codex se conecta al enviar" : "Primera instalación pendiente",
            Foreground = Muted,
            FontSize = 10,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        Grid.SetColumn(_status, 0);
        footer.Children.Add(_status);

        _accessButton = CreateButton("Acceso controlado", SurfaceRaised, Muted, 10);
        _accessButton.ToolTip = "Acceso completo habilita archivos, comandos y red sin aprobaciones repetidas. Se confirma una vez y vuelve a Acceso controlado al abrir otra conversación.";
        _accessButton.Padding = new Thickness(9, 4, 9, 4);
        _accessButton.MinHeight = 25;
        _accessButton.Template = CreateButtonTemplate(12);
        _accessButton.IsEnabled = projectReady;
        _accessButton.Click += (_, _) => ToggleAccessMode();
        Grid.SetColumn(_accessButton, 1);
        footer.Children.Add(_accessButton);
        return footer;
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
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(5, 0, 8, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        row.Children.Add(new TextBlock
        {
            Text = "Modelo",
            Foreground = Muted,
            FontSize = 9,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 5, 0)
        });
        _modelBox = new ComboBox
        {
            FontSize = 10,
            Width = 125,
            MaxDropDownHeight = 280,
            Background = SurfaceRaised,
            Foreground = TextBrush,
            BorderBrush = PanelBorder,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4, 2, 4, 2),
            Template = CreateDarkComboBoxTemplate(),
            ToolTip = "Elige Automático o selecciona a mano uno de los modelos disponibles de Codex. La elección se aplica al próximo mensaje."
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
            var model = selected.Tag as string;
            RefreshEffortChoices(CodexModelSelection.LoadEffort(model));
            SaveModelPreferences();
            SetStatus($"Modelo {selected.Content} · razonamiento {GetSelectedEffortLabel(model)} · próximo envío.");
        };
        row.Children.Add(_modelBox);

        row.Children.Add(new TextBlock
        {
            Text = "Razonamiento",
            Foreground = Muted,
            FontSize = 9,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(7, 0, 5, 0)
        });
        _effortBox = new ComboBox
        {
            FontSize = 10,
            Width = 96,
            MaxDropDownHeight = 240,
            Background = SurfaceRaised,
            Foreground = TextBrush,
            BorderBrush = PanelBorder,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4, 2, 4, 2),
            Template = CreateDarkComboBoxTemplate(),
            ToolTip = "Nivel de razonamiento enviado a Codex: predeterminado, mínimo, bajo, medio, alto o máximo; las opciones dependen del modelo."
        };
        _effortBox.ItemContainerStyle = itemStyle;
        _effortBox.IsEnabled = _projectRoot is not null;
        _effortBox.SelectionChanged += (_, _) =>
        {
            if (_loadingModels) return;
            SaveModelPreferences();
            var model = GetSelectedModelId();
            SetStatus($"Razonamiento {GetSelectedEffortLabel(model)} · se aplicará al próximo envío.");
        };
        row.Children.Add(_effortBox);
        RefreshEffortChoices(CodexModelSelection.LoadEffort(null));

        return row;
    }

    private void RefreshEffortChoices(string? preferredEffort)
    {
        if (_effortBox is null) return;
        var restoreLoading = _loadingModels;
        _loadingModels = true;
        try
        {
            _effortBox.Items.Clear();
            var modelId = GetSelectedModelId();
            var model = modelId is null ? null : _availableModels.FirstOrDefault(item => item.Id == modelId);
            var defaultEffort = model?.DefaultEffort ?? _availableModels.FirstOrDefault(item => item.IsDefault)?.DefaultEffort ?? "medium";
            var defaultLabel = modelId is null ? "Predeterminado" : $"Predeterminado · {EffortDisplayName(defaultEffort)}";
            _effortBox.Items.Add(new ComboBoxItem { Content = defaultLabel, Tag = null });

            IEnumerable<string> supported;
            if (modelId is not null)
                supported = model?.SupportedEfforts ?? Array.Empty<string>();
            else
            {
                var modelEfforts = _availableModels
                    .Where(item => item.SupportedEfforts.Count > 0)
                    .Select(item => item.SupportedEfforts.ToHashSet(StringComparer.OrdinalIgnoreCase))
                    .ToArray();
                supported = modelEfforts.Length == 0
                    ? Array.Empty<string>()
                    : modelEfforts.Skip(1).Aggregate(modelEfforts[0], (common, next) =>
                    {
                        common.IntersectWith(next);
                        return common;
                    });
            }

            foreach (var effort in supported.Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(EffortOrder).ThenBy(value => value, StringComparer.OrdinalIgnoreCase))
            {
                _effortBox.Items.Add(new ComboBoxItem { Content = EffortDisplayName(effort), Tag = effort });
            }

            var chosen = _effortBox.Items.OfType<ComboBoxItem>()
                .FirstOrDefault(item => item.Tag is string value && string.Equals(value, preferredEffort, StringComparison.OrdinalIgnoreCase));
            _effortBox.SelectedItem = chosen ?? _effortBox.Items[0];
        }
        finally
        {
            _loadingModels = restoreLoading;
        }
    }

    private string? GetSelectedModelId() => (_modelBox?.SelectedItem as ComboBoxItem)?.Tag as string;

    private string GetSelectedEffortLabel(string? modelId)
    {
        if (_effortBox?.SelectedItem is ComboBoxItem { Tag: string effort }) return EffortDisplayName(effort);
        var model = modelId is null ? _availableModels.FirstOrDefault(item => item.IsDefault) :
            _availableModels.FirstOrDefault(item => item.Id == modelId);
        return EffortDisplayName(model?.DefaultEffort ?? "medium");
    }

    private string? GetRequestedEffort() => (_effortBox?.SelectedItem as ComboBoxItem)?.Tag as string;

    private void SaveModelPreferences()
    {
        if (_modelBox is null) return;
        CodexModelSelection.Save(GetSelectedModelId(), GetRequestedEffort());
    }

    private static string EffortDisplayName(string effort) => effort.ToLowerInvariant() switch
    {
        "none" => "Sin razonamiento",
        "minimal" => "Mínimo",
        "low" => "Bajo",
        "medium" => "Medio",
        "high" => "Alto",
        "xhigh" => "Muy alto",
        "max" => "Máximo",
        "ultra" => "Ultra",
        _ => string.IsNullOrWhiteSpace(effort) ? "Predeterminado" : char.ToUpperInvariant(effort[0]) + effort[1..]
    };

    private static int EffortOrder(string effort) => effort.ToLowerInvariant() switch
    {
        "none" => 0,
        "minimal" => 1,
        "low" => 2,
        "medium" => 3,
        "high" => 4,
        "xhigh" => 5,
        "max" => 6,
        "ultra" => 7,
        _ => 8
    };

    private async Task LoadModelChoicesAsync()
    {
        if (_disposed || _projectRoot is null || _modelBox is null) return;
        try
        {
            _client ??= CreateClient();
            await _client.EnsureInitializedAsync(_projectRoot);
            var models = await _client.ListModelsAsync();
            if (_disposed || _modelBox is null) return;
            _availableModels = models;
            _loadingModels = true;
            var saved = CodexModelSelection.Load();
            _modelBox.Items.Clear();
            _modelBox.Items.Add(new ComboBoxItem { Content = "Automático", Tag = null });
            foreach (var model in models)
                _modelBox.Items.Add(new ComboBoxItem { Content = model.DisplayName, Tag = model.Id });
            _modelBox.SelectedItem = _modelBox.Items.OfType<ComboBoxItem>()
                .FirstOrDefault(item => item.Tag is string id && id == saved) ?? _modelBox.Items[0];
            RefreshEffortChoices(CodexModelSelection.LoadEffort(GetSelectedModelId()));
            _loadingModels = false;
            SetStatus(models.Count > 0
                ? $"{models.Count} modelos disponibles en el catálogo de Codex."
                : "No se pudo listar modelos; Codex usará el predeterminado.");
        }
        catch (Exception ex)
        {
            _loadingModels = false;
            RefreshEffortChoices(CodexModelSelection.LoadEffort(GetSelectedModelId()));
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
        _conversationStore = new ConversationHistoryStore(projectRoot);
        _readinessTimer.Stop();
        _prompt.IsEnabled = true;
        _attachButton.IsEnabled = true;
        _sendButton.IsEnabled = true;
        if (_accessButton is not null) _accessButton.IsEnabled = true;
        _placeholder.Text = "Pregunta sobre tu modelo, adjunta archivos o solicita una rutina Dynamo…";
        if (_contextButton is not null) _contextButton.IsEnabled = true;
        if (_historyButton is not null) _historyButton.IsEnabled = true;
        UpdateHistoryButton();
        if (_modelBox is not null) _modelBox.IsEnabled = true;
        if (_effortBox is not null) _effortBox.IsEnabled = true;
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
        var requestedEffort = GetRequestedEffort();
        var effort = requestedEffort ?? modelInfo?.DefaultEffort ?? "medium";
        var unsupportedEffort = requestedEffort is not null && modelInfo is not null &&
            modelInfo.SupportedEfforts.Count > 0 &&
            !modelInfo.SupportedEfforts.Contains(requestedEffort, StringComparer.OrdinalIgnoreCase);
        if (unsupportedEffort) effort = modelInfo!.DefaultEffort;
        _activeTurnModelName = modelInfo?.DisplayName ?? selectedModel ?? "Codex predeterminado";
        _activeTurnEffort = effort;
        var attachments = includeAttachments ? _pendingAttachments.ToArray() : Array.Empty<LocalAttachment>();
        var estimateTokens = (prompt.Length + attachments.Where(a => a.TextContent is not null).Sum(a => a.TextContent!.Length)) / 4;
        var continuationContext = _continueLocalConversation && _activeConversation is not null
            ? ConversationHistoryStore.BuildContinuationContext(_activeConversation)
            : null;
        var initializeProjectProfile = NeedsProjectProfile();
        if (_activeConversation is null && _conversationStore is not null)
        {
            try { _activeConversation = _conversationStore.Create(selectedModel); }
            catch (Exception ex) { SetStatus($"No se pudo iniciar el historial local ({ex.GetType().Name}); el chat seguirá disponible."); }
        }
        AddMessage("Tú", prompt, isUser: true, attachments);
        PersistMessage("Tú", prompt, isUser: true, attachments.Select(item => item.Name));
        _streamingText = null;
        _streamingMessageContainer = null;
        _streamingRaw = string.Empty;
        _toolCards.Clear();
        _turnInProgress = true;
        _turnCancellationRequested = false;
        _prompt.IsEnabled = false;
        _attachButton.IsEnabled = false;
        if (_modelBox is not null) _modelBox.IsEnabled = false;
        if (_effortBox is not null) _effortBox.IsEnabled = false;
        if (_contextButton is not null) _contextButton.IsEnabled = false;
        _sendButton.Content = "Detener";
        _sendButton.Background = ButtonBusy;
        _sendButton.Foreground = ButtonBusyText;
        _sendButton.IsEnabled = true;
        SetStatus(initializeProjectProfile
            ? "Primera consulta · preparando inventario local de Revit en solo lectura…"
            : "Conectando con Codex…");

        try
        {
            _client ??= CreateClient();
            _client.FullAccessEnabled = _fullAccessEnabled;
            await _client.EnsureStartedAsync(_projectRoot, selectedModel, effort);
            var modelAndEffort = $"{_activeTurnModelName} · {EffortDisplayName(effort)}";
            SetStatus(unsupportedEffort
                ? $"{EffortDisplayName(requestedEffort!)} no está disponible en {_activeTurnModelName}; se usará {EffortDisplayName(effort)} · ≈{estimateTokens} tokens…"
                : $"Consultando Codex ({modelAndEffort} · ≈{estimateTokens} tokens de entrada)…");
            await _client.SendMessageWithContextAsync(
                prompt,
                attachments,
                selectedModel,
                effort,
                continuationContext,
                initializeProjectProfile);
            _continueLocalConversation = false;
            if (clearComposer)
            {
                _prompt.Clear();
                if (includeAttachments)
                {
                    _pendingAttachments.Clear();
                    RefreshAttachmentChips();
                }
            }
            _conversationScroll.ScrollToEnd();
            SetStatus(initializeProjectProfile && NeedsProjectProfile()
                ? "Respuesta lista · la ficha local de Revit sigue pendiente."
                : "Respuesta lista · el modelo sigue bajo tu control.");
        }
        catch (Exception ex)
        {
            if (_turnCancellationRequested)
                SetStatus("Turno detenido.");
            else
            {
                AddMessage("Conexión", $"No se pudo completar el envío ({ex.GetType().Name}). Revisa que Codex esté instalado, iniciado y que el MCP esté configurado.", isUser: false);
                PersistMessage("Conexión", $"No se pudo completar el envío ({ex.GetType().Name}). Revisa que Codex esté instalado, iniciado y que el MCP esté configurado.", isUser: false);
                SetStatus("No se pudo conectar con Codex.");
            }
            _continueLocalConversation = _activeConversation is not null;
            var failedClient = _client;
            _client = null;
            if (failedClient is not null) _ = failedClient.DisposeAsync();
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(_streamingRaw))
                PersistMessage("ARQ-IA", _streamingRaw, isUser: false,
                    modelDisplayName: _activeTurnModelName,
                    reasoningEffort: _activeTurnEffort);
            _turnInProgress = false;
            _turnCancellationRequested = false;
            _streamingText = null;
            if (!_disposed)
            {
                _prompt.IsEnabled = true;
                _attachButton.IsEnabled = true;
                if (_modelBox is not null) _modelBox.IsEnabled = true;
                if (_effortBox is not null) _effortBox.IsEnabled = true;
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
        var client = new CodexAppServerClient { FullAccessEnabled = _fullAccessEnabled };
        client.StatusChanged += value => Dispatcher.Invoke(() => SetStatus(value));
        client.ModelEffortSelectionFallback += () => Dispatcher.Invoke(() =>
        {
            _activeTurnModelName = "Codex predeterminado";
            _activeTurnEffort = "Predeterminado";
            SetStatus("Este servidor Codex no admite fijar modelo o razonamiento; se aplicarán sus valores predeterminados.");
        });
        client.TextReceived += value => Dispatcher.Invoke(() =>
        {
            if (_streamingText is null)
            {
                _streamingText = AddMessage("ARQ-IA", string.Empty, isUser: false,
                    modelDisplayName: _activeTurnModelName,
                    reasoningEffort: _activeTurnEffort);
                _streamingMessageContainer = _messages.Children[_messages.Children.Count - 1] as StackPanel;
            }
            _streamingRaw += value;
            RenderMarkdown(_streamingText, _streamingRaw);
            _conversationScroll.ScrollToEnd();
        });
        client.ToolActivity += activity => Dispatcher.Invoke(() => UpdateToolCard(activity));
        client.TurnFailed += value => Dispatcher.Invoke(() =>
        {
            AddMessage("Codex", value, isUser: false,
                modelDisplayName: _activeTurnModelName,
                reasoningEffort: _activeTurnEffort);
            PersistMessage("Codex", value, isUser: false,
                modelDisplayName: _activeTurnModelName,
                reasoningEffort: _activeTurnEffort);
        });
        client.ApprovalRequested = request => Dispatcher.InvokeAsync(() => ConfirmRequest(request)).Task;
        return client;
    }

    private void ToggleAccessMode()
    {
        if (_turnInProgress || _accessButton is null) return;
        var enable = !_fullAccessEnabled;
        if (enable)
        {
            var answer = MessageBox.Show(
                "Acceso completo permite a Codex usar archivos, comandos y red sin pedir aprobación por cada acción en esta conversación. Las rutinas PythonNet3 se ejecutan dentro de Revit con los permisos de Windows del usuario; la restricción del RVT de prueba no aísla el resto del equipo. Actívalo solo para una tarea y un código que hayas revisado.\n\n¿Activar Acceso completo para esta conversación?",
                "Acceso completo · ARQ-IA",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);
            if (answer != MessageBoxResult.Yes)
            {
                SetStatus("Acceso controlado activo.");
                return;
            }
        }
        SetAccessMode(enable);
    }

    private void SetAccessMode(bool enabled)
    {
        _fullAccessEnabled = enabled;
        if (_accessButton is not null)
        {
            _accessButton.Content = _fullAccessEnabled ? "● Acceso completo" : "Acceso controlado";
            _accessButton.Background = _fullAccessEnabled ? BrushFromRgb(76, 50, 28) : SurfaceRaised;
            _accessButton.Foreground = _fullAccessEnabled ? BrushFromRgb(255, 175, 91) : Muted;
        }
        if (_client is not null) _client.FullAccessEnabled = _fullAccessEnabled;
        SetStatus(_fullAccessEnabled
            ? "Acceso completo activo en este chat: Codex ejecutará acciones sin pedir aprobación por llamada."
            : "Acceso controlado activo: Codex volverá a pedir aprobación cuando corresponda.");
    }

    private async Task NewConversationAsync()
    {
        if (_disposed || _turnInProgress) return;
        SetAccessMode(false);
        var old = _client;
        _client = null;
        _activeConversation = null;
        _continueLocalConversation = false;
        _streamingText = null;
        _streamingMessageContainer = null;
        _streamingRaw = string.Empty;
        _toolCards.Clear();
        _messages.Children.Clear();
        _prompt.Clear();
        _pendingAttachments.Clear();
        RefreshAttachmentChips();
        AddMessage("ARQ-IA", "Nueva conversación. El modelo Revit permanece sin cambios.", isUser: false);
        if (old is not null) await old.DisposeAsync();
        if (_projectRoot is not null) await LoadModelChoicesAsync();
    }

    private async Task OpenConversationAsync(string id)
    {
        if (_disposed || _turnInProgress || _conversationStore is null) return;
        ConversationHistoryRecord? record;
        try { record = _conversationStore.Load(id); }
        catch (Exception ex)
        {
            SetStatus($"No se pudo abrir el historial local ({ex.GetType().Name}).");
            return;
        }
        if (record is null)
        {
            SetStatus("Esta conversación ya no está disponible en el historial local.");
            return;
        }

        SetAccessMode(false);
        var old = _client;
        _client = null;
        _activeConversation = record;
        _continueLocalConversation = true;
        _streamingText = null;
        _streamingMessageContainer = null;
        _streamingRaw = string.Empty;
        _toolCards.Clear();
        _messages.Children.Clear();
        _prompt.Clear();
        _pendingAttachments.Clear();
        RefreshAttachmentChips();

        var turnEntries = new List<ConversationHistoryEntry>();
        void RenderEntry(ConversationHistoryEntry entry)
        {
            if (entry.Kind == "message")
                AddMessage(entry.Speaker, entry.Text, entry.IsUser,
                    attachmentNames: entry.AttachmentNames,
                    modelDisplayName: entry.ModelDisplayName,
                    reasoningEffort: entry.ReasoningEffort);
            else if (entry.Kind == "tool")
                UpdateToolCard(new McpToolActivity(entry.Id, entry.Server, entry.Tool, entry.Status, entry.Detail), persistHistory: false, updateStatus: false);
        }
        void RenderTurn()
        {
            foreach (var entry in turnEntries.Where(entry => entry.Kind == "tool")) RenderEntry(entry);
            foreach (var entry in turnEntries.Where(entry => entry.Kind == "message")) RenderEntry(entry);
            turnEntries.Clear();
        }
        foreach (var entry in record.Entries)
        {
            if (entry.Kind == "message" && entry.IsUser)
            {
                RenderTurn();
                _toolCards.Clear();
                RenderEntry(entry);
            }
            else turnEntries.Add(entry);
        }
        RenderTurn();
        SetStatus($"Historial local · {record.UpdatedUtc.ToLocalTime():dd MMM yyyy HH:mm} · vuelve a adjuntar archivos si hacen falta.");
        if (old is not null) await old.DisposeAsync();
        _prompt.Focus();
    }

    private void UpdateToolCard(McpToolActivity activity, bool persistHistory = true, bool updateStatus = true)
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
                TextWrapping = TextWrapping.Wrap,
            });
            stack.Children.Add(top);
            var state = new TextBlock { Foreground = Muted, FontSize = 10, Margin = new Thickness(0, 4, 0, 0) };
            stack.Children.Add(state);
            var detail = new RichTextBox
            {
                Foreground = Muted,
                FontSize = 10,
                IsReadOnly = true,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Cursor = Cursors.IBeam
            };
            detail.Document.PagePadding = new Thickness(0);
            detail.Document.Background = Brushes.Transparent;
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
            var responseIndex = _streamingMessageContainer is null
                ? -1
                : _messages.Children.IndexOf(_streamingMessageContainer);
            if (responseIndex >= 0) _messages.Children.Insert(responseIndex, card);
            else _messages.Children.Add(card);
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
            var detailText = activity.Detail.Length > 3000 ? activity.Detail[..3000] + "…" : activity.Detail;
            RenderPlainText(fields.Detail, detailText);
            if (fields.Detail.Parent is Expander expander) expander.Visibility = Visibility.Visible;
        }
        if (persistHistory && _activeConversation is not null && _conversationStore is not null)
        {
            try
            {
                _conversationStore.UpsertTool(_activeConversation, activity);
                UpdateHistoryButton();
            }
            catch (Exception ex) { SetStatus($"No se pudo guardar el resultado MCP en el historial ({ex.GetType().Name})."); }
        }
        if (updateStatus && activity.Status == "completed") SetStatus("MCP conectado · respuesta recibida");
        else if (updateStatus && activity.Status == "failed") SetStatus("No se pudo completar la consulta MCP");
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

    private RichTextBox AddMessage(
        string speaker,
        string message,
        bool isUser,
        IReadOnlyList<LocalAttachment>? attachments = null,
        IReadOnlyList<string>? attachmentNames = null,
        string? modelDisplayName = null,
        string? reasoningEffort = null)
    {
        var messageStack = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
        if (!isUser)
        {
            var heading = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(3, 0, 3, 6) };
            heading.Children.Add(new TextBlock { Text = "◉", Foreground = TextBrush, FontSize = 11, Margin = new Thickness(0, 0, 7, 0) });
            heading.Children.Add(new TextBlock { Text = speaker, Foreground = TextBrush, FontSize = 12, FontWeight = FontWeights.SemiBold });
            heading.Children.Add(new TextBlock { Text = "Respuesta del asistente", Foreground = Muted, FontSize = 9, Margin = new Thickness(7, 2, 0, 0) });
            if (!string.IsNullOrWhiteSpace(modelDisplayName))
            {
                var modelBadge = new Border
                {
                    Background = SurfaceRaised,
                    BorderBrush = PanelBorder,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(6, 1, 6, 1),
                    Margin = new Thickness(8, 0, 0, 0),
                    Child = new TextBlock
                    {
                        Text = $"{modelDisplayName} · {EffortDisplayName(reasoningEffort ?? "medium")}",
                        Foreground = BrushFromRgb(177, 213, 225),
                        FontSize = 9
                    }
                };
                heading.Children.Add(modelBadge);
            }
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
        var body = new RichTextBox
        {
            Foreground = TextBrush,
            FontSize = 13,
            IsReadOnly = true,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Cursor = Cursors.IBeam,
            Tag = message
        };
        body.Document.PagePadding = new Thickness(0);
        body.Document.Background = Brushes.Transparent;
        if (isUser) RenderPlainText(body, message);
        else RenderMarkdown(body, message);
        contents.Children.Add(body);
        var displayedAttachmentNames = attachments is { Count: > 0 }
            ? attachments.Select(item => item.Name).ToArray()
            : attachmentNames?.ToArray() ?? Array.Empty<string>();
        if (displayedAttachmentNames.Length > 0)
        {
            var files = new TextBlock
            {
                Text = string.Join("\n", displayedAttachmentNames.Select(name => "▤ " + name)),
                Foreground = Muted,
                FontSize = 10,
                Margin = new Thickness(0, 6, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                Cursor = Cursors.IBeam
            };
            contents.Children.Add(files);
        }
        bubble.Child = contents;
        messageStack.Children.Add(bubble);
        var copy = CreateButton("Copiar", Brushes.Transparent, Muted, 10);
        copy.Padding = new Thickness(5, 2, 5, 2);
        copy.Margin = new Thickness(4, 3, 0, 0);
        copy.HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        copy.ToolTip = "Copiar el texto completo del mensaje";
        copy.Click += (_, _) => CopyMessageToClipboard(body.Tag as string);
        messageStack.Children.Add(copy);
        _messages.Children.Add(messageStack);
        _conversationScroll.ScrollToEnd();
        return body;
    }

    private static void RenderMarkdown(RichTextBox target, string raw)
    {
        target.Tag = raw;
        target.Document.Blocks.Clear();
        var lines = raw.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var fence = new string((char)96, 3);
        var index = 0;
        while (index < lines.Length)
        {
            var line = lines[index];
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith(fence, StringComparison.Ordinal))
            {
                var codeLines = new List<string>();
                index++;
                while (index < lines.Length && !lines[index].TrimStart().StartsWith(fence, StringComparison.Ordinal))
                    codeLines.Add(lines[index++]);
                if (index < lines.Length) index++;
                AddCodeBlock(target, codeLines);
                continue;
            }
            if (string.IsNullOrWhiteSpace(line))
            {
                index++;
                continue;
            }

            if (index + 1 < lines.Length && TryParseMarkdownTableRow(line, out var headerCells) &&
                TryParseMarkdownTableRow(lines[index + 1], out var separatorCells) &&
                IsMarkdownTableSeparator(separatorCells) && separatorCells.Count == headerCells.Count)
            {
                var tableRows = new List<IReadOnlyList<string>> { headerCells };
                index += 2;
                while (index < lines.Length && TryParseMarkdownTableRow(lines[index], out var rowCells) && rowCells.Count == headerCells.Count)
                {
                    tableRows.Add(rowCells);
                    index++;
                }
                AddMarkdownTable(target, tableRows);
                continue;
            }

            var heading = trimmed.StartsWith("### ", StringComparison.Ordinal) ? 3 :
                trimmed.StartsWith("## ", StringComparison.Ordinal) ? 2 :
                trimmed.StartsWith("# ", StringComparison.Ordinal) ? 1 : 0;
            if (heading > 0)
            {
                var paragraph = new Paragraph { Margin = new Thickness(0, heading == 1 ? 10 : 8, 0, 5), LineHeight = 22 };
                paragraph.Inlines.Add(new Run(trimmed[(heading + 1)..])
                {
                    FontWeight = FontWeights.SemiBold,
                    FontSize = heading == 1 ? 16 : heading == 2 ? 14 : 13,
                    Foreground = BrushFromRgb(202, 232, 241)
                });
                target.Document.Blocks.Add(paragraph);
                index++;
                continue;
            }
            if (trimmed.StartsWith("> ", StringComparison.Ordinal))
            {
                var paragraph = new Paragraph { Margin = new Thickness(4, 2, 0, 7), Padding = new Thickness(7, 3, 4, 3), Background = SurfaceRaised, LineHeight = 19 };
                paragraph.Inlines.Add(new Run("│ ") { Foreground = ButtonAccent, FontWeight = FontWeights.Bold });
                paragraph.Inlines.Add(new Run(trimmed[2..]) { FontStyle = FontStyles.Italic, Foreground = BrushFromRgb(205, 211, 220) });
                target.Document.Blocks.Add(paragraph);
                index++;
                continue;
            }

            var listItem = Regex.Match(line, @"^(\s*)([-*+]|\d+[.)])\s+(.+)$");
            if (listItem.Success)
            {
                var indentation = Math.Min(listItem.Groups[1].Value.Length, 4);
                var paragraph = new Paragraph { Margin = new Thickness(8 + indentation * 8, 1, 0, 3), LineHeight = 19 };
                var marker = listItem.Groups[2].Value;
                paragraph.Inlines.Add(new Run(char.IsDigit(marker[0]) ? marker + "  " : "•  ")
                {
                    Foreground = ButtonAccent,
                    FontWeight = FontWeights.SemiBold
                });
                AppendInlineFormatting(paragraph, listItem.Groups[3].Value);
                target.Document.Blocks.Add(paragraph);
                index++;
                continue;
            }

            var body = new Paragraph { Margin = new Thickness(0, 0, 0, 7), LineHeight = 19 };
            AppendInlineFormatting(body, line);
            index++;
            while (index < lines.Length && !string.IsNullOrWhiteSpace(lines[index]) && !IsMarkdownBlockStart(lines, index, fence))
            {
                body.Inlines.Add(new Run(" "));
                AppendInlineFormatting(body, lines[index].Trim());
                index++;
            }
            target.Document.Blocks.Add(body);
        }
    }

    private static bool IsMarkdownBlockStart(string[] lines, int index, string fence)
    {
        var trimmed = lines[index].TrimStart();
        if (trimmed.StartsWith(fence, StringComparison.Ordinal) || trimmed.StartsWith("# ", StringComparison.Ordinal) ||
            trimmed.StartsWith("## ", StringComparison.Ordinal) || trimmed.StartsWith("### ", StringComparison.Ordinal) ||
            trimmed.StartsWith("> ", StringComparison.Ordinal) || Regex.IsMatch(lines[index], @"^\s*([-*+]|\d+[.)])\s+.+")) return true;
        return index + 1 < lines.Length && TryParseMarkdownTableRow(lines[index], out _) &&
               TryParseMarkdownTableRow(lines[index + 1], out var separator) && IsMarkdownTableSeparator(separator);
    }

    private static bool TryParseMarkdownTableRow(string line, out List<string> cells)
    {
        cells = new List<string>();
        var trimmed = line.Trim();
        if (!trimmed.Contains('|')) return false;
        var start = trimmed.StartsWith('|') ? 1 : 0;
        var end = trimmed.EndsWith('|') ? trimmed.Length - 1 : trimmed.Length;
        var cell = new System.Text.StringBuilder();
        var escaped = false;
        var inCode = false;
        for (var position = start; position < end; position++)
        {
            var character = trimmed[position];
            if (escaped)
            {
                cell.Append(character);
                escaped = false;
                continue;
            }
            if (character == '\\')
            {
                escaped = true;
                continue;
            }
            if (character == '`') inCode = !inCode;
            if (character == '|' && !inCode)
            {
                cells.Add(cell.ToString().Trim());
                cell.Clear();
            }
            else cell.Append(character);
        }
        if (escaped) cell.Append('\\');
        cells.Add(cell.ToString().Trim());
        return cells.Count >= 2;
    }

    private static bool IsMarkdownTableSeparator(IReadOnlyList<string> cells) =>
        cells.Count >= 2 && cells.All(cell => Regex.IsMatch(cell.Replace(":", string.Empty, StringComparison.Ordinal), @"^-{3,}$"));

    private static void AddMarkdownTable(RichTextBox target, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        if (rows.Count == 0 || rows[0].Count == 0) return;
        var table = new Table
        {
            CellSpacing = 0,
            BorderBrush = PanelBorder,
            BorderThickness = new Thickness(1),
            Background = Surface,
            Margin = new Thickness(0, 2, 0, 10)
        };
        var columnCount = rows[0].Count;
        var weights = Enumerable.Range(0, columnCount)
            .Select(column => Math.Clamp(rows.Max(row => row[column].Length), 5, 28))
            .ToArray();
        foreach (var weight in weights) table.Columns.Add(new TableColumn { Width = new GridLength(weight, GridUnitType.Star) });

        var group = new TableRowGroup();
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = new TableRow();
            for (var column = 0; column < columnCount; column++)
            {
                var cell = new TableCell
                {
                    BorderBrush = PanelBorder,
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Padding = new Thickness(6, 4, 6, 4),
                    Background = rowIndex == 0 ? SurfaceRaised : (rowIndex % 2 == 0 ? SurfaceRaised : Surface)
                };
                var paragraph = new Paragraph { Margin = new Thickness(0), LineHeight = 17, FontSize = 11, Foreground = TextBrush };
                AppendInlineFormatting(paragraph, rows[rowIndex][column]);
                if (rowIndex == 0)
                {
                    foreach (var inline in paragraph.Inlines)
                    {
                        inline.FontWeight = FontWeights.SemiBold;
                        inline.Foreground = BrushFromRgb(202, 232, 241);
                    }
                }
                cell.Blocks.Add(paragraph);
                row.Cells.Add(cell);
            }
            group.Rows.Add(row);
        }
        table.RowGroups.Add(group);
        target.Document.Blocks.Add(table);
    }

    private static void AddCodeBlock(RichTextBox target, IReadOnlyList<string> lines)
    {
        var paragraph = new Paragraph
        {
            Margin = new Thickness(0, 2, 0, 9),
            Padding = new Thickness(8, 6, 8, 6),
            Background = BrushFromRgb(38, 45, 53),
            LineHeight = 18
        };
        for (var index = 0; index < lines.Count; index++)
        {
            if (index > 0) paragraph.Inlines.Add(new LineBreak());
            paragraph.Inlines.Add(new Run(string.IsNullOrEmpty(lines[index]) ? " " : lines[index])
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Foreground = BrushFromRgb(190, 222, 235)
            });
        }
        target.Document.Blocks.Add(paragraph);
    }

    private static void RenderPlainText(RichTextBox target, string raw)
    {
        target.Tag = raw;
        target.Document.Blocks.Clear();
        var paragraph = new Paragraph { Margin = new Thickness(0), LineHeight = 19 };
        var lines = raw.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            if (index > 0) paragraph.Inlines.Add(new LineBreak());
            paragraph.Inlines.Add(new Run(lines[index]));
        }
        target.Document.Blocks.Add(paragraph);
    }

    private void CopyMessageToClipboard(string? content)
    {
        if (string.IsNullOrEmpty(content)) return;
        try
        {
            Clipboard.SetText(content);
            SetStatus("Mensaje copiado al portapapeles.");
        }
        catch (Exception exception)
        {
            SetStatus($"No se pudo copiar el mensaje ({exception.GetType().Name}). Selecciónalo y usa Ctrl+C.");
        }
    }

    private static void AppendInlineFormatting(Paragraph target, string line)
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

    private bool NeedsProjectProfile()
    {
        if (_projectRoot is null) return false;
        var path = Path.Combine(_projectRoot, ".arq-ia", "model-profile.md");
        try { return !File.Exists(path) || new FileInfo(path).Length < 80; }
        catch { return true; }
    }

    private void PersistMessage(
        string speaker,
        string message,
        bool isUser,
        IEnumerable<string>? attachmentNames = null,
        string? modelDisplayName = null,
        string? reasoningEffort = null)
    {
        if (_activeConversation is null || _conversationStore is null) return;
        try
        {
            _conversationStore.AddMessage(_activeConversation, speaker, message, isUser, attachmentNames,
                modelDisplayName, reasoningEffort);
            UpdateHistoryButton();
        }
        catch (Exception ex)
        {
            SetStatus($"El chat sigue disponible, pero no se pudo guardar el historial local ({ex.GetType().Name}).");
        }
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
