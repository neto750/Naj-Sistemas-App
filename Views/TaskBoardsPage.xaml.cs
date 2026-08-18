using Microsoft.Maui.Controls.Shapes;
using NajGravador.Models;
using NajGravador.Services;

namespace NajGravador.Views;

public partial class TaskBoardsPage : ContentPage
{
    private const int ItemsPerPage = 5;
    private readonly LegalTaskRepository _repository = new();
    private readonly List<LegalTask> _tasks = [];
    private readonly string[] _statusNames = Enum.GetValues<LegalTaskStatus>()
        .Select(LegalTaskStatusInfo.GetName)
        .ToArray();
    private LegalTask? _editingTask;
    private int _currentPage = 1;
    private int _totalPages = 1;
    private bool _isCompact;
    private bool _hasLoaded;
    private readonly TaskCompletionSource<bool> _visualTreeLoaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private CancellationTokenSource? _toastCancellation;

    public TaskBoardsPage()
    {
        InitializeComponent();
        StatusPicker.ItemsSource = _statusNames;
        StatusPicker.SelectedIndex = 0;
        Loaded += OnPageLoaded;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Opacity = 1;
        TranslationY = 0;
        _currentPage = 1;
        await _visualTreeLoaded.Task;
        await LoadTasksAsync();
        _hasLoaded = true;
        ForceTasksLayout();
    }

    private void OnPageLoaded(object? sender, EventArgs e) =>
        _visualTreeLoaded.TrySetResult(true);

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width <= 0) return;

        EditorPanel.WidthRequest = Math.Min(620, Math.Max(300, width - 24));
        EditorPanel.MaximumHeightRequest = Math.Max(420, height - 28);
        var compact = width < 720;
        if (_isCompact == compact) return;
        _isCompact = compact;
        ConfigureFormGrid(DeadlineGrid, compact);
        ConfigureFormGrid(ResponsibleSupervisorGrid, compact);
        PageSummaryLabel.IsVisible = ItemsPerPageLabel.IsVisible = !compact;
        PaginationControls.Spacing = compact ? 3 : 5;
        PaginationNumbersHost.Spacing = compact ? 3 : 5;
        PreviousTenButton.Text = "«";
        NextTenButton.Text = "»";
        PreviousTenButton.Padding = NextTenButton.Padding = compact ? new Thickness(5, 2) : new Thickness(8, 2);
        PreviousPageButton.Padding = NextPageButton.Padding = compact ? new Thickness(5, 2) : new Thickness(8, 2);
        if (_hasLoaded) RenderTasks();
    }

    private static void ConfigureFormGrid(Grid grid, bool compact)
    {
        grid.RowDefinitions.Clear();
        if (compact)
        {
            grid.ColumnDefinitions[0].Width = GridLength.Star;
            grid.ColumnDefinitions[1].Width = new GridLength(0);
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            grid.RowSpacing = 10;
            grid.SetColumn(grid.Children[0], 0);
            grid.SetRow(grid.Children[0], 0);
            grid.SetColumn(grid.Children[1], 0);
            grid.SetRow(grid.Children[1], 1);
        }
        else
        {
            grid.ColumnDefinitions[0].Width = GridLength.Star;
            grid.ColumnDefinitions[1].Width = GridLength.Star;
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            grid.RowSpacing = 0;
            grid.SetColumn(grid.Children[0], 0);
            grid.SetRow(grid.Children[0], 0);
            grid.SetColumn(grid.Children[1], 1);
            grid.SetRow(grid.Children[1], 0);
        }
    }

    private async Task LoadTasksAsync()
    {
        var loadedTasks = await _repository.GetAllAsync();
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            _tasks.Clear();
            _tasks.AddRange(loadedTasks);
            RenderTasks();
        });
    }

    private void RenderTasks()
    {
        var filtered = GetFilteredTasks();
        _totalPages = Math.Max(1, (int)Math.Ceiling(filtered.Count / (double)ItemsPerPage));
        _currentPage = Math.Clamp(_currentPage, 1, _totalPages);
        var currentItems = filtered
            .Skip((_currentPage - 1) * ItemsPerPage)
            .Take(ItemsPerPage)
            .ToList();

        TasksHost.IsVisible = false;
        TasksHost.Children.Clear();
        for (var index = 0; index < currentItems.Count; index++)
        {
            TasksHost.Children.Add(CreateTaskCard(currentItems[index]));
        }
        TasksHost.IsVisible = currentItems.Count > 0;

        var overdue = filtered.Count(IsOverdue);
        ResultCountLabel.Text = filtered.Count == 1 ? "1 tarefa encontrada" : $"{filtered.Count} tarefas encontradas";
        OverdueCountLabel.Text = overdue == 1 ? "1 atrasada" : $"{overdue} atrasadas";
        EmptyState.IsVisible = filtered.Count == 0;
        EmptyTitleLabel.Text = string.IsNullOrWhiteSpace(SearchEntry.Text)
            ? "Nenhuma tarefa cadastrada"
            : "Nenhuma tarefa encontrada";
        RenderPagination();
        Dispatcher.Dispatch(ForceTasksLayout);
    }

    private void ForceTasksLayout()
    {
        TasksHost.InvalidateMeasure();
        TasksScrollView.InvalidateMeasure();
    }

    private List<LegalTask> GetFilteredTasks()
    {
        var query = SearchEntry.Text?.Trim() ?? string.Empty;
        return _tasks
            .Where(task => string.IsNullOrWhiteSpace(query) ||
                           task.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                           task.Client.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                           (!string.IsNullOrWhiteSpace(task.ProcessNumber) &&
                            task.Counterparty.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                           task.ProcessNumber.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                           task.Responsible.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                           task.Supervisor.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                           LegalTaskStatusInfo.GetName(task.Status).Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(task => task.Status == LegalTaskStatus.Completed)
            .ThenBy(task => task.FinalDeadline)
            .ThenBy(task => task.InternalDeadline)
            .ToList();
    }

    private View CreateTaskCard(LegalTask task)
    {
        var statusColor = Color.FromArgb(LegalTaskStatusInfo.GetColor(task.Status));
        var border = new Border
        {
            Padding = 0,
            BackgroundColor = Colors.White,
            Stroke = Color.FromArgb("#DDE4EC"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Shadow = new Shadow
            {
                Brush = Color.FromArgb("#240B1726"),
                Offset = new Point(0, 3),
                Radius = 9,
                Opacity = 0.18f
            }
        };

        var outer = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(5)),
                new ColumnDefinition(GridLength.Star)
            }
        };
        outer.Children.Add(new BoxView { BackgroundColor = statusColor });

        var content = new VerticalStackLayout
        {
            Padding = new Thickness(_isCompact ? 13 : 18, 13),
            Spacing = 11
        };
        Grid.SetColumn(content, 1);

        content.Children.Add(new Label
        {
            Text = task.Description,
            FontSize = _isCompact ? 13.5 : 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1E3A5F"),
            LineBreakMode = LineBreakMode.WordWrap,
            VerticalTextAlignment = TextAlignment.Start
        });

        var actionRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8
        };

        actionRow.Children.Add(new Border
        {
            Padding = new Thickness(9, 5),
            BackgroundColor = Color.FromArgb(LegalTaskStatusInfo.GetBackground(task.Status)),
            Stroke = Colors.Transparent,
            StrokeShape = new RoundRectangle { CornerRadius = 7 },
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Center,
            Content = new HorizontalStackLayout
            {
                Spacing = 5,
                Children =
                {
                    new Label { Text = "●", FontSize = 9, TextColor = statusColor, VerticalTextAlignment = TextAlignment.Center },
                    new Label { Text = $"Situação: {LegalTaskStatusInfo.GetName(task.Status)}", FontSize = 10.5, FontAttributes = FontAttributes.Bold, TextColor = statusColor, VerticalTextAlignment = TextAlignment.Center }
                }
            }
        });

        var menu = new Button
        {
            Text = "•••",
            FontSize = 15,
            TextColor = Color.FromArgb("#52657A"),
            BackgroundColor = Color.FromArgb("#F1F4F8"),
            CornerRadius = 7,
            Padding = 5,
            MinimumWidthRequest = 36,
            MinimumHeightRequest = 34,
            CommandParameter = task
        };
        menu.Clicked += OnTaskMenuClicked;
        Grid.SetColumn(menu, 1);
        actionRow.Children.Add(menu);
        content.Children.Add(actionRow);
        content.Children.Add(new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#EDF1F5") });

        var hasProcess = !string.IsNullOrWhiteSpace(task.ProcessNumber);
        var hasCounterparty = hasProcess && !string.IsNullOrWhiteSpace(task.Counterparty);
        content.Children.Add(CreateDetail("Cliente", task.Client));
        if (hasCounterparty)
            content.Children.Add(CreateDetail("Parte oposta", task.Counterparty));
        if (!string.IsNullOrWhiteSpace(task.ProcessNumber))
            content.Children.Add(CreateDetail("Processo", task.ProcessNumber));

        var deadlines = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 9
        };
        deadlines.Children.Add(CreateDeadlineDetail("Prazo interno", task.InternalDeadline, false, task.Status));
        var finalDeadline = CreateDeadlineDetail("Prazo fatal", task.FinalDeadline, true, task.Status);
        Grid.SetColumn(finalDeadline, 1);
        deadlines.Children.Add(finalDeadline);
        content.Children.Add(deadlines);

        var people = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 9
        };
        people.Children.Add(CreateDetail("Responsável", task.Responsible));
        var supervisor = CreateDetail("Supervisor", string.IsNullOrWhiteSpace(task.Supervisor) ? "Não informado" : task.Supervisor);
        Grid.SetColumn(supervisor, 1);
        people.Children.Add(supervisor);
        content.Children.Add(people);
        outer.Children.Add(content);
        border.Content = outer;

        var tap = new TapGestureRecognizer { CommandParameter = new TaskCardTarget(task, border) };
        tap.Tapped += OnTaskTapped;
        border.GestureRecognizers.Add(tap);
        return border;
    }

    private static View CreateDetail(string title, string value)
    {
        var text = new VerticalStackLayout { Spacing = 1 };
        text.Children.Add(new Label
        {
            Text = title,
            FontSize = 9.5,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#8795A5")
        });
        text.Children.Add(new Label
        {
            Text = value,
            FontSize = 11.5,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#334E68"),
            LineBreakMode = LineBreakMode.WordWrap
        });
        return new Border
        {
            Padding = new Thickness(10, 7),
            BackgroundColor = Color.FromArgb("#F7F9FC"),
            Stroke = Color.FromArgb("#E8EDF3"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 7 },
            Content = text
        };
    }

    private static View CreateDeadlineDetail(string title, DateTime date, bool isFinal, LegalTaskStatus status)
    {
        var isCompleted = status == LegalTaskStatus.Completed;
        var isLate = !isCompleted && date.Date < DateTime.Today;
        var isNear = !isCompleted && !isLate && date.Date <= DateTime.Today.AddDays(isFinal ? 2 : 1);
        var color = isLate ? "#AE2E24" : isNear ? "#B54708" : isCompleted ? "#1F845A" : "#334E68";
        var detail = CreateDetail(title, date.ToString("dd/MM/yyyy"));
        if (detail is Border { Content: VerticalStackLayout text } && text.Children[1] is Label value)
            value.TextColor = Color.FromArgb(color);
        return detail;
    }

    private void RenderPagination()
    {
        PageSummaryLabel.Text = $"Página {_currentPage} de {_totalPages}";
        PreviousPageButton.IsEnabled = PreviousTenButton.IsEnabled = _currentPage > 1;
        NextPageButton.IsEnabled = NextTenButton.IsEnabled = _currentPage < _totalPages;
        PreviousPageButton.Opacity = PreviousTenButton.Opacity = _currentPage > 1 ? 1 : 0.38;
        NextPageButton.Opacity = NextTenButton.Opacity = _currentPage < _totalPages ? 1 : 0.38;

        PaginationNumbersHost.Children.Clear();
        var firstPage = Math.Max(1, _currentPage - 4);
        if (firstPage + 4 > _totalPages) firstPage = Math.Max(1, _totalPages - 4);
        var lastPage = Math.Min(_totalPages, firstPage + 4);
        for (var page = firstPage; page <= lastPage; page++)
        {
            var isCurrent = page == _currentPage;
            var button = new Button
            {
                Text = page.ToString(),
                FontSize = 11,
                FontAttributes = isCurrent ? FontAttributes.Bold : FontAttributes.None,
                TextColor = isCurrent ? Colors.White : Color.FromArgb("#1E3A5F"),
                BackgroundColor = isCurrent ? Color.FromArgb("#1E66C2") : Color.FromArgb("#F1F4F8"),
                CornerRadius = 7,
                Padding = _isCompact ? new Thickness(6, 4) : new Thickness(9, 4),
                MinimumWidthRequest = _isCompact ? 29 : 34,
                CommandParameter = page
            };
            button.Clicked += OnPageNumberClicked;
            PaginationNumbersHost.Children.Add(button);
        }
    }

    private async void OnTaskTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not TaskCardTarget target) return;
        await target.Card.ScaleToAsync(0.992, 60, Easing.CubicOut);
        await target.Card.ScaleToAsync(1, 90, Easing.CubicOut);
        await OpenEditorAsync(target.Task);
    }

    private async void OnTaskMenuClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: LegalTask task }) return;
        var action = await DisplayActionSheetAsync("Opções da tarefa", "Cancelar", null,
            "Editar", "Alterar situação", "Duplicar", "Excluir");
        switch (action)
        {
            case "Editar":
                await OpenEditorAsync(task);
                break;
            case "Alterar situação":
                await ChangeStatusAsync(task);
                break;
            case "Duplicar":
                await DuplicateTaskAsync(task);
                break;
            case "Excluir":
                await DeleteTaskAsync(task);
                break;
        }
    }

    private async Task ChangeStatusAsync(LegalTask task)
    {
        var selected = await DisplayActionSheetAsync("Nova situação", "Cancelar", null, _statusNames);
        var index = Array.IndexOf(_statusNames, selected);
        if (index < 0) return;
        task.Status = Enum.GetValues<LegalTaskStatus>()[index];
        await _repository.SaveAsync(task);
        await LoadTasksAsync();
        await ShowToastAsync($"Situação alterada para {selected}");
    }

    private async Task DuplicateTaskAsync(LegalTask source)
    {
        var copy = new LegalTask
        {
            Description = $"{source.Description} — cópia",
            Client = source.Client,
            Counterparty = source.Counterparty,
            ProcessNumber = source.ProcessNumber,
            InternalDeadline = source.InternalDeadline,
            FinalDeadline = source.FinalDeadline,
            Responsible = source.Responsible,
            Supervisor = source.Supervisor,
            Status = LegalTaskStatus.Pending
        };
        await _repository.SaveAsync(copy);
        _currentPage = 1;
        await LoadTasksAsync();
        await ShowToastAsync("Tarefa duplicada");
    }

    private async Task DeleteTaskAsync(LegalTask task)
    {
        if (!await DisplayAlertAsync("Excluir tarefa?", "A tarefa será removida permanentemente.", "Excluir", "Cancelar")) return;
        await _repository.DeleteAsync(task.Id);
        await LoadTasksAsync();
        await ShowToastAsync("Tarefa excluída");
    }

    private async void OnOpenCreateClicked(object? sender, EventArgs e) => await OpenEditorAsync(null);

    private async Task OpenEditorAsync(LegalTask? task)
    {
        _editingTask = task;
        EditorTitleLabel.Text = task is null ? "Nova tarefa" : "Editar tarefa";
        SaveTaskButton.Text = task is null ? "Criar tarefa" : "Salvar alterações";
        DescriptionEditor.Text = task?.Description ?? string.Empty;
        ClientEntry.Text = task?.Client ?? string.Empty;
        ProcessEntry.Text = task?.ProcessNumber ?? string.Empty;
        CounterpartyEntry.Text = task?.Counterparty ?? string.Empty;
        InternalDeadlinePicker.Date = task?.InternalDeadline ?? DateTime.Today.AddDays(2);
        FinalDeadlinePicker.Date = task?.FinalDeadline ?? DateTime.Today.AddDays(5);
        ResponsibleEntry.Text = task?.Responsible ?? string.Empty;
        SupervisorEntry.Text = task?.Supervisor ?? string.Empty;
        StatusPicker.SelectedIndex = task is null ? 0 : Array.IndexOf(Enum.GetValues<LegalTaskStatus>(), task.Status);
        ValidationBox.IsVisible = false;

        ModalScrim.IsVisible = EditorPanel.IsVisible = true;
        ModalScrim.Opacity = EditorPanel.Opacity = 0;
        EditorPanel.Scale = 0.97;
        await Task.WhenAll(
            ModalScrim.FadeToAsync(1, 140),
            EditorPanel.FadeToAsync(1, 170),
            EditorPanel.ScaleToAsync(1, 190, Easing.CubicOut));
        DescriptionEditor.Focus();
    }

    private async void OnCloseEditorClicked(object? sender, EventArgs e) => await CloseEditorAsync();

    private async Task CloseEditorAsync()
    {
        await Task.WhenAll(
            ModalScrim.FadeToAsync(0, 110),
            EditorPanel.FadeToAsync(0, 130),
            EditorPanel.ScaleToAsync(0.98, 130, Easing.CubicIn));
        ModalScrim.IsVisible = EditorPanel.IsVisible = false;
        _editingTask = null;
    }

    private async void OnSaveTaskClicked(object? sender, EventArgs e)
    {
        var description = DescriptionEditor.Text?.Trim();
        var client = ClientEntry.Text?.Trim();
        var counterparty = CounterpartyEntry.Text?.Trim();
        var process = ProcessEntry.Text?.Trim();
        var responsible = ResponsibleEntry.Text?.Trim();
        var supervisor = SupervisorEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(description) || string.IsNullOrWhiteSpace(client) ||
            string.IsNullOrWhiteSpace(responsible) || StatusPicker.SelectedIndex < 0)
        {
            ShowValidation("Preencha todos os campos obrigatórios.");
            return;
        }
        if (string.IsNullOrWhiteSpace(process) && !string.IsNullOrWhiteSpace(counterparty))
        {
            ShowValidation("Informe o número do processo para adicionar uma parte oposta.");
            return;
        }

        var internalDeadline = InternalDeadlinePicker.Date.GetValueOrDefault(DateTime.Today).Date;
        var finalDeadline = FinalDeadlinePicker.Date.GetValueOrDefault(DateTime.Today).Date;
        if (finalDeadline < internalDeadline)
        {
            ShowValidation("O prazo fatal não pode ser anterior ao prazo interno.");
            return;
        }

        var isNew = _editingTask is null;
        var task = _editingTask ?? new LegalTask();
        task.Description = description;
        task.Client = client;
        task.Counterparty = string.IsNullOrWhiteSpace(process) ? string.Empty : counterparty ?? string.Empty;
        task.ProcessNumber = process ?? string.Empty;
        task.InternalDeadline = internalDeadline;
        task.FinalDeadline = finalDeadline;
        task.Responsible = responsible;
        task.Supervisor = supervisor ?? string.Empty;
        task.Status = Enum.GetValues<LegalTaskStatus>()[StatusPicker.SelectedIndex];
        await _repository.SaveAsync(task);
        _currentPage = 1;
        await CloseEditorAsync();
        await LoadTasksAsync();
        await ShowToastAsync(isNew ? "Tarefa criada com sucesso" : "Alterações salvas");
    }

    private void ShowValidation(string message)
    {
        ValidationLabel.Text = message;
        ValidationBox.IsVisible = true;
        _ = ShakeValidationAsync();
    }

    private async Task ShakeValidationAsync()
    {
        await ValidationBox.TranslateToAsync(-5, 0, 45);
        await ValidationBox.TranslateToAsync(5, 0, 45);
        await ValidationBox.TranslateToAsync(0, 0, 45);
    }

    private void OnSearchChanged(object? sender, TextChangedEventArgs e)
    {
        _currentPage = 1;
        RenderTasks();
    }

    private void OnPreviousPageClicked(object? sender, EventArgs e) => GoToPage(_currentPage - 1);
    private void OnNextPageClicked(object? sender, EventArgs e) => GoToPage(_currentPage + 1);
    private void OnPreviousTenClicked(object? sender, EventArgs e) => GoToPage(_currentPage - 10);
    private void OnNextTenClicked(object? sender, EventArgs e) => GoToPage(_currentPage + 10);

    private void OnPageNumberClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: int page }) GoToPage(page);
    }

    private void GoToPage(int page)
    {
        var target = Math.Clamp(page, 1, _totalPages);
        if (target == _currentPage) return;
        _currentPage = target;
        RenderTasks();
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");

    private async Task ShowToastAsync(string message)
    {
        _toastCancellation?.Cancel();
        _toastCancellation = new CancellationTokenSource();
        var token = _toastCancellation.Token;
        ToastLabel.Text = message;
        Toast.IsVisible = true;
        Toast.Opacity = 0;
        Toast.TranslationY = 8;
        await Task.WhenAll(
            Toast.FadeToAsync(1, 130),
            Toast.TranslateToAsync(0, 0, 150, Easing.CubicOut));
        try { await Task.Delay(1700, token); }
        catch (TaskCanceledException) { return; }
        await Toast.FadeToAsync(0, 130);
        Toast.IsVisible = false;
    }

    private static bool IsOverdue(LegalTask task) =>
        task.Status != LegalTaskStatus.Completed && task.FinalDeadline.Date < DateTime.Today;

    private sealed record TaskCardTarget(LegalTask Task, VisualElement Card);
}
