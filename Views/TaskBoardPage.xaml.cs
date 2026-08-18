using System.Globalization;
using System.Text.Json;
using Microsoft.Maui.Controls.Shapes;
using NajGravador.Models;
using NajGravador.Services;

namespace NajGravador.Views;

[QueryProperty(nameof(BoardId), nameof(BoardId))]
public partial class TaskBoardPage : ContentPage
{
    private enum BoardFilter { All, Open, Completed, DueToday, Overdue, Assigned }

    private static readonly CultureInfo PtBr = new("pt-BR");
    private static readonly string[] LabelColors = ["#E2483D", "#E56910", "#F5CD47", "#4BCE97", "#579DFF", "#9F8FEF", "#E774BB", "#8590A2"];
    private static readonly string[] CardColors = ["#FFFFFF", "#FFF7D6", "#FFECEB", "#E3FCEF", "#E9F2FF", "#F3F0FF", "#FFF0F7", "#F1F2F4"];

    private readonly KanbanRepository _repository = new();
    private KanbanBoard? _board;
    private KanbanList? _editingList;
    private KanbanCard? _editingCard;
    private BoardFilter _filter;
    private string _editorLabelColor = "#579DFF";
    private string _editorCardColor = "#FFFFFF";
    private CancellationTokenSource? _toastCancellation;
    private bool _isLoading;

    public string BoardId { get; set; } = string.Empty;

    public TaskBoardPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_isLoading || string.IsNullOrWhiteSpace(BoardId)) return;
        _isLoading = true;
        try
        {
            _board = await _repository.GetAsync(BoardId);
            if (_board is null)
            {
                await DisplayAlertAsync("Quadro não encontrado", "Este quadro não existe mais.", "Voltar");
                await Shell.Current.GoToAsync("..");
                return;
            }
            ApplyTheme();
            RenderBoard();
            Opacity = 0;
            await this.FadeToAsync(1, 180, Easing.CubicOut);
        }
        finally { _isLoading = false; }
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width <= 0) return;
        CardEditorPanel.WidthRequest = Math.Min(520, width);
        FilterButton.Text = width < 620 ? "⌕" : "⌕ Filtrar";
        ThemeButton.Text = width < 620 ? "▧" : "▧ Fundo";
        FilterButton.Padding = ThemeButton.Padding = width < 620 ? new Thickness(8, 5) : new Thickness(10, 5);
    }

    private void ApplyTheme()
    {
        if (_board is null) return;
        var theme = KanbanThemes.Find(_board.ThemeKey);
        BoardBackground.Background = new LinearGradientBrush(
            new GradientStopCollection
            {
                new() { Color = Color.FromArgb(theme.StartHex), Offset = 0 },
                new() { Color = Color.FromArgb(theme.EndHex), Offset = 1 }
            }, new Point(0, 0), new Point(1, 1));
    }

    private void RenderBoard()
    {
        if (_board is null) return;
        BoardNameLabel.Text = _board.Name;
        FavoriteButton.Text = _board.IsFavorite ? "★" : "☆";
        var allCards = _board.Lists.SelectMany(list => list.Cards).Where(card => !card.IsArchived).ToList();
        var completed = allCards.Count(card => card.IsCompleted);
        BoardProgressLabel.Text = $"{completed} de {allCards.Count} tarefas concluídas";

        ListsHost.Children.Clear();
        foreach (var list in _board.Lists)
            ListsHost.Children.Add(CreateListView(list));
        ListsHost.Children.Add(CreateAddListView());
    }

    private View CreateListView(KanbanList list)
    {
        var activeCards = list.Cards.Where(card => !card.IsArchived).ToList();
        var visibleCards = activeCards.Where(CardMatchesFilter).ToList();
        var border = new Border
        {
            WidthRequest = list.IsCollapsed ? 64 : 292,
            Padding = list.IsCollapsed ? new Thickness(7, 10) : new Thickness(10),
            BackgroundColor = Color.FromArgb("#F1F2F4"),
            Stroke = Color.FromArgb("#33091720"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Shadow = new Shadow { Brush = Color.FromArgb("#44091720"), Offset = new Point(0, 4), Radius = 10, Opacity = 0.22f },
            VerticalOptions = LayoutOptions.Start,
            MaximumHeightRequest = 720
        };

        if (list.IsCollapsed)
        {
            var collapsed = new VerticalStackLayout { Spacing = 8, HorizontalOptions = LayoutOptions.Center };
            var expand = new Button { Text = "›", FontSize = 24, TextColor = Color.FromArgb("#44546F"), BackgroundColor = Colors.Transparent, Padding = 0, CommandParameter = list };
            expand.Clicked += OnToggleListCollapsed;
            collapsed.Children.Add(expand);
            collapsed.Children.Add(new Label { Text = list.Name, FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#172B4D"), Rotation = 90, WidthRequest = 155, Margin = new Thickness(0, 58, 0, 58), HorizontalTextAlignment = TextAlignment.Center, LineBreakMode = LineBreakMode.TailTruncation });
            collapsed.Children.Add(new Border
            {
                WidthRequest = 28, HeightRequest = 28, Padding = 0, BackgroundColor = Color.FromArgb("#DCDFE4"), Stroke = Colors.Transparent,
                StrokeShape = new RoundRectangle { CornerRadius = 14 },
                Content = new Label { Text = activeCards.Count.ToString(PtBr), FontSize = 11, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#44546F"), HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center }
            });
            border.Content = collapsed;
            return border;
        }

        var root = new Grid { RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) }, RowSpacing = 8 };
        var header = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 2 };
        header.Children.Add(new Label { Text = list.Name, FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#172B4D"), Margin = new Thickness(5, 0), VerticalTextAlignment = TextAlignment.Center, LineBreakMode = LineBreakMode.TailTruncation });
        var count = new Border
        {
            WidthRequest = 27, HeightRequest = 25, Padding = 0, BackgroundColor = Color.FromArgb("#DCDFE4"), Stroke = Colors.Transparent,
            StrokeShape = new RoundRectangle { CornerRadius = 13 },
            Content = new Label { Text = activeCards.Count.ToString(PtBr), FontSize = 10, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#44546F"), HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center }
        };
        Grid.SetColumn(count, 1); header.Children.Add(count);
        var menu = new Button { Text = "•••", FontSize = 14, TextColor = Color.FromArgb("#44546F"), BackgroundColor = Colors.Transparent, Padding = 3, MinimumWidthRequest = 34, MinimumHeightRequest = 32, CommandParameter = list };
        menu.Clicked += OnListMenuClicked;
        Grid.SetColumn(menu, 2); header.Children.Add(menu);
        var listDrag = new DragGestureRecognizer { CanDrag = true };
        listDrag.DragStarting += (_, e) => e.Data.Properties.Add("KanbanListId", list.Id);
        header.GestureRecognizers.Add(listDrag);
        root.Children.Add(header);

        var cardsHost = new VerticalStackLayout { Spacing = 8 };
        foreach (var card in visibleCards)
            cardsHost.Children.Add(CreateCardView(list, card));
        if (visibleCards.Count == 0 && _filter != BoardFilter.All)
            cardsHost.Children.Add(new Label { Text = "Nenhum cartão neste filtro", FontSize = 11, TextColor = Color.FromArgb("#8590A2"), HorizontalTextAlignment = TextAlignment.Center, Padding = new Thickness(6, 15) });
        var cardScroll = new ScrollView { Content = cardsHost, MaximumHeightRequest = 560, VerticalScrollBarVisibility = ScrollBarVisibility.Always };
        Grid.SetRow(cardScroll, 1); root.Children.Add(cardScroll);

        var add = new Button { Text = "＋ Adicionar cartão", FontSize = 12, TextColor = Color.FromArgb("#44546F"), BackgroundColor = Colors.Transparent, HorizontalOptions = LayoutOptions.Fill, Padding = new Thickness(8, 6), CommandParameter = list };
        add.Clicked += OnAddCardClicked;
        Grid.SetRow(add, 2); root.Children.Add(add);
        border.Content = root;

        var drop = new DropGestureRecognizer { AllowDrop = true };
        drop.DragOver += (_, _) => { _ = border.ScaleToAsync(1.015, 80, Easing.CubicOut); };
        drop.DragLeave += (_, _) => border.ScaleToAsync(1, 80, Easing.CubicOut);
        drop.Drop += async (_, e) => { await border.ScaleToAsync(1, 80); await HandleDropAsync(list, e); };
        border.GestureRecognizers.Add(drop);
        return border;
    }

    private View CreateCardView(KanbanList list, KanbanCard card)
    {
        var border = new Border
        {
            Padding = new Thickness(11, 9),
            BackgroundColor = SafeColor(card.ColorHex, "#FFFFFF"),
            Stroke = Color.FromArgb("#26091720"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 9 },
            Shadow = new Shadow { Brush = Color.FromArgb("#35091720"), Offset = new Point(0, 2), Radius = 5, Opacity = 0.2f }
        };
        var content = new VerticalStackLayout { Spacing = 7 };
        if (!string.IsNullOrWhiteSpace(card.LabelName))
        {
            content.Children.Add(new Border
            {
                Padding = new Thickness(8, 3), HorizontalOptions = LayoutOptions.Start,
                BackgroundColor = SafeColor(card.LabelColorHex, "#579DFF"), Stroke = Colors.Transparent,
                StrokeShape = new RoundRectangle { CornerRadius = 5 },
                Content = new Label { Text = card.LabelName, FontSize = 9, FontAttributes = FontAttributes.Bold, TextColor = Colors.White }
            });
        }

        var titleRow = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) }, ColumnSpacing = 8 };
        var doneCircle = new Border
        {
            WidthRequest = 23, HeightRequest = 23, Padding = 0,
            BackgroundColor = card.IsCompleted ? Color.FromArgb("#22A06B") : Colors.White,
            Stroke = card.IsCompleted ? Color.FromArgb("#22A06B") : Color.FromArgb("#8590A2"), StrokeThickness = 1.5,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Content = new Label { Text = card.IsCompleted ? "✓" : string.Empty, FontSize = 12, FontAttributes = FontAttributes.Bold, TextColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center }
        };
        var doneTap = new TapGestureRecognizer { CommandParameter = new CardLocation(list, card, doneCircle) };
        doneTap.Tapped += OnCardDoneTapped;
        doneCircle.GestureRecognizers.Add(doneTap);
        titleRow.Children.Add(doneCircle);
        var title = new Label { Text = card.Title, FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb(card.IsCompleted ? "#626F86" : "#172B4D"), TextDecorations = card.IsCompleted ? TextDecorations.Strikethrough : TextDecorations.None, LineBreakMode = LineBreakMode.WordWrap };
        Grid.SetColumn(title, 1); titleRow.Children.Add(title);
        content.Children.Add(titleRow);

        var metadata = new HorizontalStackLayout { Spacing = 7 };
        if (card.DueDate.HasValue)
        {
            var dueColor = card.IsCompleted ? "#22A06B" : card.DueDate.Value < DateTime.Now ? "#AE2E24" : card.DueDate.Value.Date == DateTime.Today ? "#E56910" : "#44546F";
            metadata.Children.Add(CreateMetadataBadge($"◷ {card.DueDate.Value:dd/MM HH:mm}", dueColor, card.IsCompleted ? "#E3FCEF" : "#F1F2F4"));
        }
        if (card.Checklist.Count > 0)
            metadata.Children.Add(CreateMetadataBadge($"☑ {card.Checklist.Count(item => item.IsCompleted)}/{card.Checklist.Count}", "#44546F", "#F1F2F4"));
        if (card.Attachments.Count > 0)
            metadata.Children.Add(CreateMetadataBadge($"⌕ {card.Attachments.Count}", "#44546F", "#F1F2F4"));
        if (!string.IsNullOrWhiteSpace(card.Assignee))
            metadata.Children.Add(CreateMetadataBadge(GetInitials(card.Assignee), "#FFFFFF", "#0C66E4"));
        if (metadata.Children.Count > 0) content.Children.Add(metadata);

        var tap = new TapGestureRecognizer { CommandParameter = new CardLocation(list, card, border) };
        tap.Tapped += OnCardTapped;
        border.GestureRecognizers.Add(tap);
        var drag = new DragGestureRecognizer { CanDrag = true };
        drag.DragStarting += (_, e) =>
        {
            e.Data.Properties.Add("KanbanCardId", card.Id);
            e.Data.Properties.Add("KanbanSourceListId", list.Id);
        };
        border.GestureRecognizers.Add(drag);
        var cardDrop = new DropGestureRecognizer { AllowDrop = true };
        cardDrop.DragOver += (_, _) => { _ = border.ScaleToAsync(1.025, 70, Easing.CubicOut); };
        cardDrop.DragLeave += (_, _) => { _ = border.ScaleToAsync(1, 70, Easing.CubicOut); };
        cardDrop.Drop += async (_, e) =>
        {
            await border.ScaleToAsync(1, 70, Easing.CubicOut);
            await HandleCardDropAsync(list, card, e);
        };
        border.GestureRecognizers.Add(cardDrop);
        border.Content = content;
        return border;
    }

    private static View CreateMetadataBadge(string text, string textHex, string backgroundHex) => new Border
    {
        Padding = new Thickness(6, 3), BackgroundColor = Color.FromArgb(backgroundHex), Stroke = Colors.Transparent,
        StrokeShape = new RoundRectangle { CornerRadius = 5 },
        Content = new Label { Text = text, FontSize = 9.5, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb(textHex) }
    };

    private View CreateAddListView()
    {
        var border = new Border
        {
            WidthRequest = 292, Padding = new Thickness(10), BackgroundColor = Color.FromArgb("#DFFFFFFF"), Stroke = Colors.Transparent,
            StrokeShape = new RoundRectangle { CornerRadius = 10 }, VerticalOptions = LayoutOptions.Start
        };
        var button = new Button { Text = "＋ Adicionar outra lista", FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#172B4D"), BackgroundColor = Colors.Transparent, Padding = new Thickness(8, 7) };
        button.Clicked += OnAddListClicked;
        border.Content = button;
        return border;
    }

    private bool CardMatchesFilter(KanbanCard card) => _filter switch
    {
        BoardFilter.Open => !card.IsCompleted,
        BoardFilter.Completed => card.IsCompleted,
        BoardFilter.DueToday => card.DueDate?.Date == DateTime.Today,
        BoardFilter.Overdue => !card.IsCompleted && card.DueDate < DateTime.Now,
        BoardFilter.Assigned => !string.IsNullOrWhiteSpace(card.Assignee),
        _ => true
    };

    private async Task HandleDropAsync(KanbanList targetList, DropEventArgs e)
    {
        if (_board is null) return;
        if (e.Data.Properties.TryGetValue("KanbanCardId", out var cardValue) && cardValue is string cardId)
        {
            var source = _board.Lists.FirstOrDefault(item => item.Cards.Any(card => card.Id == cardId));
            var card = source?.Cards.FirstOrDefault(item => item.Id == cardId);
            if (source is null || card is null || source == targetList) return;
            source.Cards.Remove(card);
            targetList.Cards.Add(card);
            card.UpdatedAt = DateTime.Now;
            AddActivity($"Cartão “{card.Title}” movido de {source.Name} para {targetList.Name}");
            await SaveAndRenderAsync("Cartão movido");
            return;
        }

        if (e.Data.Properties.TryGetValue("KanbanListId", out var listValue) && listValue is string listId)
        {
            var sourceIndex = _board.Lists.FindIndex(item => item.Id == listId);
            var targetIndex = _board.Lists.IndexOf(targetList);
            if (sourceIndex < 0 || targetIndex < 0 || sourceIndex == targetIndex) return;
            var moved = _board.Lists[sourceIndex];
            _board.Lists.RemoveAt(sourceIndex);
            targetIndex = _board.Lists.IndexOf(targetList);
            _board.Lists.Insert(targetIndex, moved);
            AddActivity($"Lista “{moved.Name}” movida");
            await SaveAndRenderAsync("Lista movida");
        }
    }

    private async Task HandleCardDropAsync(KanbanList targetList, KanbanCard targetCard, DropEventArgs e)
    {
        if (_board is null || !e.Data.Properties.TryGetValue("KanbanCardId", out var value) || value is not string cardId || cardId == targetCard.Id)
            return;

        var sourceList = _board.Lists.FirstOrDefault(list => list.Cards.Any(card => card.Id == cardId));
        var movedCard = sourceList?.Cards.FirstOrDefault(card => card.Id == cardId);
        if (sourceList is null || movedCard is null) return;

        sourceList.Cards.Remove(movedCard);
        var targetIndex = targetList.Cards.IndexOf(targetCard);
        if (targetIndex < 0) targetIndex = targetList.Cards.Count;
        targetList.Cards.Insert(targetIndex, movedCard);
        movedCard.UpdatedAt = DateTime.Now;
        AddActivity(sourceList == targetList
            ? $"Cartão “{movedCard.Title}” reordenado em {targetList.Name}"
            : $"Cartão “{movedCard.Title}” movido de {sourceList.Name} para {targetList.Name}");
        await SaveAndRenderAsync(sourceList == targetList ? "Cartão reordenado" : "Cartão movido");
    }

    private async void OnAddListClicked(object? sender, EventArgs e)
    {
        if (_board is null) return;
        var name = await DisplayPromptAsync("Adicionar lista", "Nome da lista", placeholder: "Ex.: Em revisão", maxLength: 60);
        if (string.IsNullOrWhiteSpace(name)) return;
        _board.Lists.Add(new KanbanList { Name = name.Trim() });
        AddActivity($"Lista “{name.Trim()}” adicionada");
        await SaveAndRenderAsync("Lista adicionada");
    }

    private async void OnAddCardClicked(object? sender, EventArgs e)
    {
        if (_board is null || sender is not Button { CommandParameter: KanbanList list }) return;
        var title = await DisplayPromptAsync("Adicionar cartão", $"Na lista “{list.Name}”", placeholder: "Digite um título", maxLength: 120);
        if (string.IsNullOrWhiteSpace(title)) return;
        var card = new KanbanCard { Title = title.Trim() };
        list.Cards.Add(card);
        AddActivity($"Cartão “{card.Title}” adicionado a {list.Name}");
        await _repository.SaveAsync(_board);
        RenderBoard();
        OpenCardEditor(list, card);
    }

    private async void OnListMenuClicked(object? sender, EventArgs e)
    {
        if (_board is null || sender is not Button { CommandParameter: KanbanList list }) return;
        var action = await DisplayActionSheetAsync(list.Name, "Cancelar", null,
            "Adicionar cartão", "Renomear lista", list.IsCollapsed ? "Expandir lista" : "Recolher lista", "Copiar lista", "Mover para a esquerda", "Mover para a direita", "Ordenar cartões", "Arquivar concluídos", "Excluir lista");
        switch (action)
        {
            case "Adicionar cartão": OnAddCardClicked(new Button { CommandParameter = list }, EventArgs.Empty); break;
            case "Renomear lista":
                var name = await DisplayPromptAsync("Renomear lista", "Novo nome", initialValue: list.Name, maxLength: 60);
                if (!string.IsNullOrWhiteSpace(name)) { list.Name = name.Trim(); AddActivity($"Lista renomeada para “{list.Name}”"); await SaveAndRenderAsync("Lista renomeada"); }
                break;
            case "Recolher lista": case "Expandir lista":
                list.IsCollapsed = !list.IsCollapsed; await SaveAndRenderAsync(list.IsCollapsed ? "Lista recolhida" : "Lista expandida"); break;
            case "Copiar lista":
                var copy = Clone(list); copy.Id = Guid.NewGuid().ToString("N"); copy.Name += " — cópia"; RegenerateCardIds(copy);
                _board.Lists.Insert(_board.Lists.IndexOf(list) + 1, copy); AddActivity($"Lista “{list.Name}” copiada"); await SaveAndRenderAsync("Lista copiada"); break;
            case "Mover para a esquerda": await MoveListAsync(list, -1); break;
            case "Mover para a direita": await MoveListAsync(list, 1); break;
            case "Ordenar cartões": await SortListAsync(list); break;
            case "Arquivar concluídos":
                var cardsToArchive = list.Cards.Where(card => card.IsCompleted && !card.IsArchived).ToList();
                foreach (var card in cardsToArchive) card.IsArchived = true;
                if (cardsToArchive.Count > 0) { AddActivity($"{cardsToArchive.Count} cartões concluídos arquivados de {list.Name}"); await SaveAndRenderAsync($"{cardsToArchive.Count} cartões arquivados"); }
                break;
            case "Excluir lista":
                if (await DisplayAlertAsync("Excluir lista?", $"“{list.Name}” contém {list.Cards.Count} cartões. A exclusão não pode ser desfeita.", "Excluir", "Cancelar"))
                { _board.Lists.Remove(list); AddActivity($"Lista “{list.Name}” excluída"); await SaveAndRenderAsync("Lista excluída"); }
                break;
        }
    }

    private async Task MoveListAsync(KanbanList list, int offset)
    {
        if (_board is null) return;
        var oldIndex = _board.Lists.IndexOf(list);
        var newIndex = Math.Clamp(oldIndex + offset, 0, _board.Lists.Count - 1);
        if (newIndex == oldIndex) { await ShowToastAsync("A lista já está no limite"); return; }
        _board.Lists.RemoveAt(oldIndex); _board.Lists.Insert(newIndex, list);
        AddActivity($"Lista “{list.Name}” movida");
        await SaveAndRenderAsync("Lista movida");
    }

    private async Task SortListAsync(KanbanList list)
    {
        var action = await DisplayActionSheetAsync("Ordenar cartões", "Cancelar", null, "Data de entrega", "Nome (A–Z)", "Mais recentes", "Concluídos por último");
        list.Cards = action switch
        {
            "Data de entrega" => list.Cards.OrderBy(card => card.DueDate ?? DateTime.MaxValue).ToList(),
            "Nome (A–Z)" => list.Cards.OrderBy(card => card.Title, StringComparer.Create(PtBr, true)).ToList(),
            "Mais recentes" => list.Cards.OrderByDescending(card => card.UpdatedAt).ToList(),
            "Concluídos por último" => list.Cards.OrderBy(card => card.IsCompleted).ToList(),
            _ => list.Cards
        };
        if (action != "Cancelar" && action is not null) { AddActivity($"Cartões de {list.Name} ordenados por {action.ToLowerInvariant()}"); await SaveAndRenderAsync("Cartões ordenados"); }
    }

    private async void OnToggleListCollapsed(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: KanbanList list }) { list.IsCollapsed = false; await SaveAndRenderAsync("Lista expandida"); }
    }

    private async void OnCardDoneTapped(object? sender, TappedEventArgs e)
    {
        if (_board is null || e.Parameter is not CardLocation location) return;
        location.Card.IsCompleted = !location.Card.IsCompleted;
        location.Card.UpdatedAt = DateTime.Now;
        if (location.Visual is Border circle)
        {
            await circle.ScaleToAsync(0.72, 80, Easing.CubicIn);
            circle.BackgroundColor = location.Card.IsCompleted ? Color.FromArgb("#22A06B") : Colors.White;
            circle.Stroke = location.Card.IsCompleted ? Color.FromArgb("#22A06B") : Color.FromArgb("#8590A2");
            if (circle.Content is Label glyph) glyph.Text = location.Card.IsCompleted ? "✓" : string.Empty;
            await circle.ScaleToAsync(1.15, 120, Easing.CubicOut);
            await circle.ScaleToAsync(1, 90, Easing.CubicInOut);
        }
        AddActivity(location.Card.IsCompleted ? $"Cartão “{location.Card.Title}” concluído" : $"Cartão “{location.Card.Title}” reaberto");
        await _repository.SaveAsync(_board);
        RenderBoard();
    }

    private async void OnCardTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not CardLocation location) return;
        await location.Visual.ScaleToAsync(0.985, 60, Easing.CubicOut);
        await location.Visual.ScaleToAsync(1, 90, Easing.CubicOut);
        OpenCardEditor(location.List, location.Card);
    }

    private async void OpenCardEditor(KanbanList list, KanbanCard card)
    {
        _editingList = list; _editingCard = card;
        CardTitleEntry.Text = card.Title;
        CardDescriptionEditor.Text = card.Description;
        DueDateCheckBox.IsChecked = card.DueDate.HasValue;
        DueDatePicker.Date = card.DueDate?.Date ?? DateTime.Today;
        DueTimePicker.Time = card.DueDate?.TimeOfDay ?? new TimeSpan(17, 0, 0);
        AssigneeEntry.Text = card.Assignee;
        LabelNameEntry.Text = card.LabelName;
        _editorLabelColor = card.LabelColorHex;
        _editorCardColor = card.ColorHex;
        EditorListLabel.Text = $"na lista {list.Name}";
        UpdateEditorDoneVisual();
        UpdateDueDateControls();
        BuildColorChoices();
        RenderChecklist();
        RenderAttachments();
        RenderComments();
        EditorScrim.IsVisible = CardEditorPanel.IsVisible = true;
        CardEditorPanel.TranslationX = 480;
        await Task.WhenAll(EditorScrim.FadeToAsync(1, 140), CardEditorPanel.FadeToAsync(1, 160), CardEditorPanel.TranslateToAsync(0, 0, 220, Easing.CubicOut));
    }

    private async void OnCloseEditorClicked(object? sender, EventArgs e) => await CloseEditorAsync();

    private async Task CloseEditorAsync()
    {
        await Task.WhenAll(EditorScrim.FadeToAsync(0, 120), CardEditorPanel.FadeToAsync(0, 140), CardEditorPanel.TranslateToAsync(480, 0, 180, Easing.CubicIn));
        EditorScrim.IsVisible = CardEditorPanel.IsVisible = false;
        _editingCard = null; _editingList = null;
        RenderBoard();
    }

    private async void OnSaveCardClicked(object? sender, EventArgs e)
    {
        if (_board is null || _editingCard is null) return;
        var title = CardTitleEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(title)) { await DisplayAlertAsync("Título obrigatório", "Digite um título para o cartão.", "OK"); return; }
        _editingCard.Title = title;
        _editingCard.Description = CardDescriptionEditor.Text?.Trim() ?? string.Empty;
        _editingCard.Assignee = AssigneeEntry.Text?.Trim() ?? string.Empty;
        _editingCard.LabelName = LabelNameEntry.Text?.Trim() ?? string.Empty;
        _editingCard.LabelColorHex = _editorLabelColor;
        _editingCard.ColorHex = _editorCardColor;
        _editingCard.DueDate = DueDateCheckBox.IsChecked
            ? DueDatePicker.Date.GetValueOrDefault(DateTime.Today).Date + DueTimePicker.Time.GetValueOrDefault(new TimeSpan(17, 0, 0))
            : null;
        _editingCard.UpdatedAt = DateTime.Now;
        AddActivity($"Cartão “{_editingCard.Title}” atualizado");
        await _repository.SaveAsync(_board);
        await CloseEditorAsync();
        RenderBoard();
        await ShowToastAsync("Alterações salvas");
    }

    private async void OnEditorDoneTapped(object? sender, TappedEventArgs e)
    {
        if (_board is null || _editingCard is null) return;
        _editingCard.IsCompleted = !_editingCard.IsCompleted;
        await EditorDoneCircle.ScaleToAsync(0.65, 80, Easing.CubicIn);
        UpdateEditorDoneVisual();
        await EditorDoneCircle.ScaleToAsync(1.18, 120, Easing.CubicOut);
        await EditorDoneCircle.ScaleToAsync(1, 100, Easing.CubicInOut);
        AddActivity(_editingCard.IsCompleted ? $"Cartão “{_editingCard.Title}” concluído" : $"Cartão “{_editingCard.Title}” reaberto");
        await _repository.SaveAsync(_board);
    }

    private void UpdateEditorDoneVisual()
    {
        if (_editingCard is null) return;
        EditorDoneCircle.BackgroundColor = _editingCard.IsCompleted ? Color.FromArgb("#22A06B") : Colors.White;
        EditorDoneCircle.Stroke = _editingCard.IsCompleted ? Color.FromArgb("#22A06B") : Color.FromArgb("#8590A2");
        EditorDoneGlyph.Text = _editingCard.IsCompleted ? "✓" : string.Empty;
    }

    private void OnDueDateEnabledChanged(object? sender, CheckedChangedEventArgs e) => UpdateDueDateControls();
    private void UpdateDueDateControls() { DueDatePicker.IsEnabled = DueTimePicker.IsEnabled = DueDateCheckBox.IsChecked; DueDatePicker.Opacity = DueTimePicker.Opacity = DueDateCheckBox.IsChecked ? 1 : 0.45; }

    private void BuildColorChoices()
    {
        BuildColorHost(LabelColorsHost, LabelColors, _editorLabelColor, true);
        BuildColorHost(CardColorsHost, CardColors, _editorCardColor, false);
    }

    private void BuildColorHost(FlexLayout host, IEnumerable<string> colors, string selected, bool isLabel)
    {
        host.Children.Clear();
        foreach (var hex in colors)
        {
            var choice = new Border
            {
                WidthRequest = 43, HeightRequest = 33, Margin = new Thickness(0, 0, 7, 5), Padding = 0,
                BackgroundColor = Color.FromArgb(hex), Stroke = hex == selected ? Color.FromArgb("#172B4D") : Color.FromArgb("#22091720"), StrokeThickness = hex == selected ? 3 : 1,
                StrokeShape = new RoundRectangle { CornerRadius = 7 }
            };
            var tap = new TapGestureRecognizer { CommandParameter = new ColorChoice(hex, isLabel) };
            tap.Tapped += OnColorChoiceTapped;
            choice.GestureRecognizers.Add(tap);
            host.Children.Add(choice);
        }
    }

    private void OnColorChoiceTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not ColorChoice choice) return;
        if (choice.IsLabel) _editorLabelColor = choice.Hex; else _editorCardColor = choice.Hex;
        BuildColorChoices();
    }

    private void RenderChecklist()
    {
        ChecklistHost.Children.Clear();
        if (_editingCard is null) return;
        var completed = _editingCard.Checklist.Count(item => item.IsCompleted);
        var progress = _editingCard.Checklist.Count == 0 ? 0 : completed / (double)_editingCard.Checklist.Count;
        ChecklistProgress.Progress = progress;
        ChecklistPercentLabel.Text = $"{progress:P0}";
        foreach (var item in _editingCard.Checklist)
        {
            var row = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 6 };
            var check = new CheckBox { IsChecked = item.IsCompleted, Color = Color.FromArgb("#22A06B"), CommandParameter = item };
            check.CheckedChanged += OnChecklistItemChanged;
            row.Children.Add(check);
            var label = new Label { Text = item.Text, FontSize = 12, TextColor = Color.FromArgb(item.IsCompleted ? "#626F86" : "#172B4D"), TextDecorations = item.IsCompleted ? TextDecorations.Strikethrough : TextDecorations.None, VerticalTextAlignment = TextAlignment.Center };
            Grid.SetColumn(label, 1); row.Children.Add(label);
            var remove = new Button { Text = "✕", FontSize = 12, TextColor = Color.FromArgb("#AE2E24"), BackgroundColor = Colors.Transparent, Padding = 5, MinimumHeightRequest = 32, MinimumWidthRequest = 32, CommandParameter = item };
            remove.Clicked += OnRemoveChecklistClicked;
            Grid.SetColumn(remove, 2); row.Children.Add(remove);
            ChecklistHost.Children.Add(row);
        }
    }

    private async void OnAddChecklistClicked(object? sender, EventArgs e)
    {
        if (_board is null || _editingCard is null || string.IsNullOrWhiteSpace(NewChecklistEntry.Text)) return;
        _editingCard.Checklist.Add(new KanbanChecklistItem { Text = NewChecklistEntry.Text.Trim() });
        NewChecklistEntry.Text = string.Empty;
        await _repository.SaveAsync(_board);
        RenderChecklist();
    }

    private async void OnChecklistItemChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (_board is null || _editingCard is null || sender is not CheckBox { CommandParameter: KanbanChecklistItem item }) return;
        item.IsCompleted = e.Value;
        await _repository.SaveAsync(_board);
        RenderChecklist();
    }

    private async void OnRemoveChecklistClicked(object? sender, EventArgs e)
    {
        if (_board is null || _editingCard is null || sender is not Button { CommandParameter: KanbanChecklistItem item }) return;
        _editingCard.Checklist.Remove(item);
        await _repository.SaveAsync(_board);
        RenderChecklist();
    }

    private void RenderAttachments()
    {
        AttachmentsHost.Children.Clear();
        if (_editingCard is null) return;
        NoAttachmentsLabel.IsVisible = _editingCard.Attachments.Count == 0;
        foreach (var attachment in _editingCard.Attachments)
        {
            var row = new Grid { ColumnDefinitions = { new ColumnDefinition(new GridLength(58)), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 9 };
            View preview;
            if (IsImage(attachment.FileName) && File.Exists(attachment.LocalPath))
                preview = new Image { Source = ImageSource.FromFile(attachment.LocalPath), WidthRequest = 58, HeightRequest = 45, Aspect = Aspect.AspectFill };
            else
                preview = new Border { WidthRequest = 58, HeightRequest = 45, BackgroundColor = Color.FromArgb("#E9F2FF"), Stroke = Colors.Transparent, StrokeShape = new RoundRectangle { CornerRadius = 6 }, Content = new Label { Text = "▤", FontSize = 22, TextColor = Color.FromArgb("#0C66E4"), HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center } };
            row.Children.Add(preview);
            var info = new VerticalStackLayout { Spacing = 1, VerticalOptions = LayoutOptions.Center };
            info.Children.Add(new Label { Text = attachment.FileName, FontSize = 11, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#172B4D"), LineBreakMode = LineBreakMode.TailTruncation });
            info.Children.Add(new Label { Text = $"Adicionado em {attachment.AddedAt:dd/MM/yyyy HH:mm}", FontSize = 9, TextColor = Color.FromArgb("#626F86") });
            Grid.SetColumn(info, 1); row.Children.Add(info);
            var remove = new Button { Text = "🗑", FontSize = 14, TextColor = Color.FromArgb("#AE2E24"), BackgroundColor = Color.FromArgb("#FFECEB"), CornerRadius = 6, Padding = 7, MinimumHeightRequest = 34, MinimumWidthRequest = 34, CommandParameter = attachment };
            remove.Clicked += OnRemoveAttachmentClicked;
            Grid.SetColumn(remove, 2); row.Children.Add(remove);
            AttachmentsHost.Children.Add(row);
        }
    }

    private async void OnAddAttachmentClicked(object? sender, EventArgs e)
    {
        if (_board is null || _editingCard is null) return;
        try
        {
            var files = await FilePicker.Default.PickMultipleAsync(new PickOptions { PickerTitle = "Anexar imagens ou arquivos" });
            foreach (var file in files.OfType<FileResult>())
            {
                var path = await _repository.CopyAttachmentAsync(file);
                _editingCard.Attachments.Add(new KanbanAttachment { FileName = file.FileName, LocalPath = path, ContentType = file.ContentType ?? string.Empty });
            }
            if (_editingCard.Attachments.Count > 0)
            {
                AddActivity($"Anexo adicionado ao cartão “{_editingCard.Title}”");
                await _repository.SaveAsync(_board);
                RenderAttachments();
            }
        }
        catch (Exception ex) { await DisplayAlertAsync("Não foi possível anexar", ex.Message, "OK"); }
    }

    private async void OnRemoveAttachmentClicked(object? sender, EventArgs e)
    {
        if (_board is null || _editingCard is null || sender is not Button { CommandParameter: KanbanAttachment attachment }) return;
        if (!await DisplayAlertAsync("Remover anexo?", attachment.FileName, "Remover", "Cancelar")) return;
        _editingCard.Attachments.Remove(attachment);
        await _repository.SaveAsync(_board);
        RenderAttachments();
    }

    private void RenderComments()
    {
        CommentsHost.Children.Clear();
        if (_editingCard is null) return;
        foreach (var comment in _editingCard.Comments.OrderByDescending(item => item.CreatedAt))
        {
            var row = new Grid { ColumnDefinitions = { new ColumnDefinition(new GridLength(30)), new ColumnDefinition(GridLength.Star) }, ColumnSpacing = 8 };
            row.Children.Add(new Border { WidthRequest = 30, HeightRequest = 30, BackgroundColor = Color.FromArgb("#0C66E4"), Stroke = Colors.Transparent, StrokeShape = new RoundRectangle { CornerRadius = 15 }, Content = new Label { Text = "EU", FontSize = 9, FontAttributes = FontAttributes.Bold, TextColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center } });
            var bubble = new Border { Padding = new Thickness(9, 7), BackgroundColor = Color.FromArgb("#F1F2F4"), Stroke = Colors.Transparent, StrokeShape = new RoundRectangle { CornerRadius = 7 } };
            var stack = new VerticalStackLayout { Spacing = 2 };
            stack.Children.Add(new Label { Text = comment.Text, FontSize = 11, TextColor = Color.FromArgb("#172B4D") });
            stack.Children.Add(new Label { Text = comment.CreatedAt.ToString("dd/MM/yyyy HH:mm"), FontSize = 8.5, TextColor = Color.FromArgb("#8590A2") });
            bubble.Content = stack; Grid.SetColumn(bubble, 1); row.Children.Add(bubble); CommentsHost.Children.Add(row);
        }
    }

    private async void OnAddCommentClicked(object? sender, EventArgs e)
    {
        if (_board is null || _editingCard is null || string.IsNullOrWhiteSpace(NewCommentEntry.Text)) return;
        _editingCard.Comments.Add(new KanbanComment { Text = NewCommentEntry.Text.Trim() });
        AddActivity($"Comentário adicionado a “{_editingCard.Title}”");
        NewCommentEntry.Text = string.Empty;
        await _repository.SaveAsync(_board);
        RenderComments();
    }

    private async void OnMoveCopyCardClicked(object? sender, EventArgs e)
    {
        if (_board is null || _editingCard is null || _editingList is null) return;
        var action = await DisplayActionSheetAsync("Mover ou copiar cartão", "Cancelar", null, "Mover para outra lista", "Copiar para outra lista");
        if (action is not ("Mover para outra lista" or "Copiar para outra lista")) return;
        var targetName = await DisplayActionSheetAsync("Escolha a lista", "Cancelar", null, _board.Lists.Select(list => list.Name).ToArray());
        var target = _board.Lists.FirstOrDefault(list => list.Name == targetName);
        if (target is null) return;
        if (action == "Mover para outra lista")
        {
            _editingList.Cards.Remove(_editingCard); target.Cards.Add(_editingCard);
            AddActivity($"Cartão “{_editingCard.Title}” movido para {target.Name}");
        }
        else
        {
            var copy = Clone(_editingCard); RegenerateCardId(copy); copy.Title += " — cópia"; target.Cards.Add(copy);
            AddActivity($"Cartão “{_editingCard.Title}” copiado para {target.Name}");
        }
        await _repository.SaveAsync(_board);
        await CloseEditorAsync(); RenderBoard(); await ShowToastAsync(action.StartsWith("Mover") ? "Cartão movido" : "Cartão copiado");
    }

    private async void OnDeleteCardClicked(object? sender, EventArgs e)
    {
        if (_board is null || _editingCard is null || _editingList is null) return;
        if (!await DisplayAlertAsync("Excluir cartão?", $"“{_editingCard.Title}” será excluído permanentemente.", "Excluir", "Cancelar")) return;
        var title = _editingCard.Title;
        _editingList.Cards.Remove(_editingCard); AddActivity($"Cartão “{title}” excluído");
        await _repository.SaveAsync(_board); await CloseEditorAsync(); RenderBoard(); await ShowToastAsync("Cartão excluído");
    }

    private async void OnFilterClicked(object? sender, EventArgs e)
    {
        var selected = await DisplayActionSheetAsync("Filtrar cartões", "Cancelar", null, "Todos", "Pendentes", "Concluídos", "Vencem hoje", "Atrasados", "Com responsável");
        _filter = selected switch { "Pendentes" => BoardFilter.Open, "Concluídos" => BoardFilter.Completed, "Vencem hoje" => BoardFilter.DueToday, "Atrasados" => BoardFilter.Overdue, "Com responsável" => BoardFilter.Assigned, _ => BoardFilter.All };
        FilterBanner.IsVisible = _filter != BoardFilter.All;
        FilterLabel.Text = $"Filtro ativo: {selected}";
        RenderBoard();
    }

    private void OnClearFilterClicked(object? sender, EventArgs e) { _filter = BoardFilter.All; FilterBanner.IsVisible = false; RenderBoard(); }

    private async void OnThemeClicked(object? sender, EventArgs e)
    {
        if (_board is null) return;
        var selected = await DisplayActionSheetAsync("Plano de fundo", "Cancelar", null, KanbanThemes.All.Select(theme => theme.Name).ToArray());
        var theme = KanbanThemes.All.FirstOrDefault(item => item.Name == selected);
        if (theme is null) return;
        _board.ThemeKey = theme.Key; AddActivity($"Fundo alterado para {theme.Name}"); await _repository.SaveAsync(_board); ApplyTheme();
    }

    private async void OnFavoriteClicked(object? sender, EventArgs e)
    {
        if (_board is null) return;
        _board.IsFavorite = !_board.IsFavorite; FavoriteButton.Text = _board.IsFavorite ? "★" : "☆";
        await FavoriteButton.ScaleToAsync(1.25, 100, Easing.CubicOut); await FavoriteButton.ScaleToAsync(1, 100, Easing.CubicIn);
        await _repository.SaveAsync(_board);
    }

    private async void OnBoardMenuClicked(object? sender, EventArgs e)
    {
        if (_board is null) return;
        var action = await DisplayActionSheetAsync(_board.Name, "Cancelar", null, "Renomear quadro", "Adicionar lista", "Copiar quadro", "Ver atividade", "Ver cartões arquivados", "Arquivar todos os concluídos", "Excluir quadro");
        switch (action)
        {
            case "Renomear quadro":
                var name = await DisplayPromptAsync("Renomear quadro", "Novo nome", initialValue: _board.Name, maxLength: 80);
                if (!string.IsNullOrWhiteSpace(name)) { _board.Name = name.Trim(); AddActivity($"Quadro renomeado para “{_board.Name}”"); await SaveAndRenderAsync("Quadro renomeado"); }
                break;
            case "Adicionar lista": OnAddListClicked(sender, e); break;
            case "Copiar quadro": await CopyCurrentBoardAsync(); break;
            case "Ver atividade":
                var activity = _board.Activity.Count == 0 ? "Ainda não há atividade." : string.Join("\n\n", _board.Activity.Take(20).Select(item => $"{item.CreatedAt:dd/MM HH:mm}  •  {item.Description}"));
                await DisplayAlertAsync("Atividade recente", activity, "Fechar"); break;
            case "Ver cartões arquivados": await ShowArchivedCardsAsync(); break;
            case "Arquivar todos os concluídos":
                var archived = _board.Lists.SelectMany(list => list.Cards).Where(card => card.IsCompleted && !card.IsArchived).ToList();
                foreach (var card in archived) card.IsArchived = true;
                if (archived.Count > 0) { AddActivity($"{archived.Count} cartões concluídos arquivados"); await SaveAndRenderAsync($"{archived.Count} cartões arquivados"); } else await ShowToastAsync("Não há cartões concluídos");
                break;
            case "Excluir quadro":
                if (await DisplayAlertAsync("Excluir quadro?", "Todo o conteúdo será excluído permanentemente.", "Excluir", "Cancelar")) { await _repository.DeleteAsync(_board.Id); await Shell.Current.GoToAsync(".."); }
                break;
        }
    }

    private async Task ShowArchivedCardsAsync()
    {
        if (_board is null) return;
        var archived = _board.Lists
            .SelectMany(list => list.Cards.Where(card => card.IsArchived).Select(card => (List: list, Card: card)))
            .ToList();
        if (archived.Count == 0) { await ShowToastAsync("Não há cartões arquivados"); return; }
        var options = archived.Select((item, index) => $"{index + 1}. {item.Card.Title} — {item.List.Name}").ToArray();
        var selected = await DisplayActionSheetAsync("Restaurar cartão arquivado", "Cancelar", null, options);
        var selectedIndex = Array.IndexOf(options, selected);
        if (selectedIndex < 0) return;
        var restored = archived[selectedIndex].Card;
        restored.IsArchived = false;
        restored.UpdatedAt = DateTime.Now;
        AddActivity($"Cartão “{restored.Title}” restaurado");
        await SaveAndRenderAsync("Cartão restaurado");
    }

    private async Task CopyCurrentBoardAsync()
    {
        if (_board is null) return;
        var copy = Clone(_board); copy.Id = Guid.NewGuid().ToString("N"); copy.Name += " — cópia"; copy.IsFavorite = false; copy.CreatedAt = copy.UpdatedAt = DateTime.Now;
        foreach (var list in copy.Lists) { list.Id = Guid.NewGuid().ToString("N"); RegenerateCardIds(list); }
        copy.Activity = [new KanbanActivity { Description = $"Quadro copiado de “{_board.Name}”" }];
        await _repository.SaveAsync(copy); await ShowToastAsync("Cópia criada em Meus quadros");
    }

    private async Task SaveAndRenderAsync(string toast)
    {
        if (_board is null) return;
        await _repository.SaveAsync(_board); RenderBoard(); await ShowToastAsync(toast);
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");

    private async Task ShowToastAsync(string message)
    {
        _toastCancellation?.Cancel(); _toastCancellation = new CancellationTokenSource(); var token = _toastCancellation.Token;
        ToastLabel.Text = message; Toast.IsVisible = true; Toast.Opacity = 0; Toast.TranslationY = 10;
        await Task.WhenAll(Toast.FadeToAsync(1, 130), Toast.TranslateToAsync(0, 0, 150, Easing.CubicOut));
        try { await Task.Delay(1600, token); } catch (TaskCanceledException) { return; }
        await Toast.FadeToAsync(0, 130); Toast.IsVisible = false;
    }

    private void AddActivity(string description)
    {
        if (_board is null) return;
        _board.Activity.Insert(0, new KanbanActivity { Description = description });
        if (_board.Activity.Count > 100) _board.Activity.RemoveRange(100, _board.Activity.Count - 100);
    }

    private static T Clone<T>(T source) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(source))!;
    private static void RegenerateCardIds(KanbanList list) { foreach (var card in list.Cards) RegenerateCardId(card); }
    private static void RegenerateCardId(KanbanCard card)
    {
        card.Id = Guid.NewGuid().ToString("N");
        foreach (var item in card.Checklist) item.Id = Guid.NewGuid().ToString("N");
        foreach (var attachment in card.Attachments) attachment.Id = Guid.NewGuid().ToString("N");
        foreach (var comment in card.Comments) comment.Id = Guid.NewGuid().ToString("N");
    }

    private static bool IsImage(string fileName) => new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" }.Contains(System.IO.Path.GetExtension(fileName).ToLowerInvariant());
    private static string GetInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(parts.Take(2).Select(part => char.ToUpperInvariant(part[0])));
    }
    private static Color SafeColor(string? hex, string fallback) { try { return Color.FromArgb(string.IsNullOrWhiteSpace(hex) ? fallback : hex); } catch { return Color.FromArgb(fallback); } }

    private sealed record CardLocation(KanbanList List, KanbanCard Card, VisualElement Visual);
    private sealed record ColorChoice(string Hex, bool IsLabel);
}
