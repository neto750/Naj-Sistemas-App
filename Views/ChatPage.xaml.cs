using Microsoft.Maui.Controls.Shapes;
using NajGravador.Models;
using NajGravador.Services;

namespace NajGravador.Views;

public partial class ChatPage : ContentPage
{
    private readonly LocalAccountRepository _accounts = new();
    private readonly ChatRepository _chat = new();
    private LocalUser? _currentUser;
    private ChatStore _store = new();
    private List<LocalUser> _users = [];
    private string _selectedFilter = "all";

    public ChatPage() => InitializeComponent();

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _currentUser = await _accounts.GetCurrentAsync();
        if (_currentUser == null)
        {
            await Shell.Current.GoToAsync(nameof(LoginPage));
            return;
        }

        _users = (await _accounts.GetAllAsync()).ToList();
        _store = await _chat.GetSnapshotAsync();
        BuildFilters();
        RenderConversations();
    }

    private void BuildFilters()
    {
        FiltersHost.Children.Clear();
        FiltersHost.Children.Add(CreateFilterButton("Todos", "all"));
        FiltersHost.Children.Add(CreateFilterButton("Não lidas", "unread"));
        FiltersHost.Children.Add(CreateFilterButton("Grupos", "groups"));
        if (_currentUser != null)
        {
            foreach (var filter in _store.Filters.Where(item => item.OwnerUserId == _currentUser.Id))
                FiltersHost.Children.Add(CreateFilterButton(filter.Name, $"custom:{filter.Id}"));
        }

        var addButton = new Button
        {
            Text = "+",
            FontSize = 21,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1E66C2"),
            BackgroundColor = Color.FromArgb("#EAF3FE"),
            CornerRadius = 17,
            Padding = 0,
            WidthRequest = 34,
            HeightRequest = 34,
            MinimumWidthRequest = 34,
            MinimumHeightRequest = 34
        };
        addButton.Clicked += OnAddFilterClicked;
        FiltersHost.Children.Add(addButton);
    }

    private Button CreateFilterButton(string text, string key)
    {
        var selected = _selectedFilter == key;
        var button = new Button
        {
            Text = text,
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = selected ? Colors.White : Color.FromArgb("#526273"),
            BackgroundColor = selected ? Color.FromArgb("#1E66C2") : Colors.White,
            BorderColor = selected ? Color.FromArgb("#1E66C2") : Color.FromArgb("#D5DEE8"),
            BorderWidth = 1,
            CornerRadius = 16,
            Padding = new Thickness(13, 5),
            HeightRequest = 34,
            MinimumHeightRequest = 34,
            CommandParameter = key
        };
        button.Clicked += OnFilterClicked;
        return button;
    }

    private void RenderConversations()
    {
        ConversationsHost.Children.Clear();
        if (_currentUser == null) return;

        var search = ConversationSearchBar.Text?.Trim() ?? string.Empty;
        var customContactIds = new HashSet<string>();
        if (_selectedFilter.StartsWith("custom:", StringComparison.Ordinal))
        {
            var filterId = _selectedFilter[7..];
            customContactIds = _store.Filters.FirstOrDefault(item => item.Id == filterId)?.ContactUserIds.ToHashSet()
                               ?? [];
        }

        var items = _store.Threads
            .Where(thread => thread.ParticipantIds.Contains(_currentUser.Id))
            .Select(thread =>
            {
                var messages = _store.Messages.Where(message => message.ThreadId == thread.Id).OrderBy(message => message.SentAt).ToList();
                var last = messages.LastOrDefault();
                var unread = messages.Count(message =>
                    message.SenderUserId != _currentUser.Id && !message.ReadByUserIds.Contains(_currentUser.Id));
                return new ConversationItem(thread, last, unread, GetThreadDisplayName(thread));
            })
            .Where(item => string.IsNullOrWhiteSpace(search) ||
                           item.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                           (item.LastMessage?.Text.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false))
            .Where(item => _selectedFilter switch
            {
                "unread" => item.UnreadCount > 0,
                "groups" => item.Thread.IsGroup,
                _ when _selectedFilter.StartsWith("custom:", StringComparison.Ordinal) =>
                    item.Thread.ParticipantIds.Any(id => id != _currentUser.Id && customContactIds.Contains(id)),
                _ => true
            })
            .OrderByDescending(item => item.LastMessage?.SentAt ?? item.Thread.CreatedAt)
            .ToList();

        foreach (var item in items) ConversationsHost.Children.Add(CreateConversationRow(item));
        EmptyState.IsVisible = items.Count == 0;
        EmptyStateLabel.Text = _selectedFilter == "unread"
            ? "Nenhuma conversa não lida"
            : _selectedFilter == "groups"
                ? "Nenhum grupo encontrado"
                : "Nenhuma conversa encontrada";
    }

    private View CreateConversationRow(ConversationItem item)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(55)),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) },
            ColumnSpacing = 9,
            RowSpacing = 3
        };

        var otherUser = item.Thread.IsGroup ? null : GetOtherUser(item.Thread);
        var avatar = ChatUi.CreateAvatar(otherUser?.PhotoPath, item.Name, 50, item.Thread.IsGroup);
        Grid.SetRowSpan(avatar, 2);
        grid.Children.Add(avatar);

        var name = new Label
        {
            Text = item.Name,
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#20354B"),
            LineBreakMode = LineBreakMode.TailTruncation
        };
        Grid.SetColumn(name, 1);
        grid.Children.Add(name);

        var time = new Label
        {
            Text = item.LastMessage == null ? string.Empty : ChatUi.FormatElapsed(item.LastMessage.SentAt),
            FontSize = 10,
            TextColor = item.UnreadCount > 0 ? Color.FromArgb("#1E66C2") : Color.FromArgb("#8894A1"),
            VerticalTextAlignment = TextAlignment.Center
        };
        Grid.SetColumn(time, 2);
        grid.Children.Add(time);

        var previewHost = new HorizontalStackLayout { Spacing = 3 };
        if (item.LastMessage != null)
        {
            var isMine = item.LastMessage.SenderUserId == _currentUser!.Id;
            if (isMine)
            {
                var everyoneRead = item.Thread.ParticipantIds
                    .Where(id => id != _currentUser.Id)
                    .All(id =>
                        item.LastMessage.ReadByUserIds.Contains(id) &&
                        (_users.FirstOrDefault(user => user.Id == id)?.ReadReceiptsEnabled ?? true));
                previewHost.Children.Add(new Label
                {
                    Text = "✓✓",
                    FontSize = 12,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb(everyoneRead ? "#1689D4" : "#8A96A3")
                });
            }

            var prefix = item.Thread.IsGroup ? $"{GetSenderName(item.LastMessage.SenderUserId)}: " : string.Empty;
            var previewText = item.LastMessage.Text + (!isMine && item.UnreadCount == 0 ? "." : string.Empty);
            previewHost.Children.Add(new Label
            {
                Text = prefix + previewText,
                FontSize = 12,
                FontAttributes = item.UnreadCount > 0 ? FontAttributes.Bold : FontAttributes.None,
                TextColor = Color.FromArgb(item.UnreadCount > 0 ? "#1E3A5F" : "#788695"),
                LineBreakMode = LineBreakMode.TailTruncation,
                MaxLines = 1
            });
        }
        else
        {
            previewHost.Children.Add(new Label { Text = "Conversa iniciada", FontSize = 12, TextColor = Color.FromArgb("#8A96A3") });
        }
        Grid.SetRow(previewHost, 1);
        Grid.SetColumn(previewHost, 1);
        grid.Children.Add(previewHost);

        if (item.UnreadCount > 0)
        {
            var badge = new Border
            {
                WidthRequest = item.UnreadCount > 9 ? 27 : 23,
                HeightRequest = 23,
                Padding = 1,
                BackgroundColor = Color.FromArgb("#1E66C2"),
                Stroke = Colors.Transparent,
                StrokeShape = new RoundRectangle { CornerRadius = 12 },
                Content = new Label
                {
                    Text = item.UnreadCount > 99 ? "99+" : item.UnreadCount.ToString(),
                    FontSize = item.UnreadCount > 99 ? 8 : 10,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.White,
                    HorizontalTextAlignment = TextAlignment.Center,
                    VerticalTextAlignment = TextAlignment.Center
                }
            };
            Grid.SetRow(badge, 1);
            Grid.SetColumn(badge, 2);
            grid.Children.Add(badge);
        }

        var row = new Border
        {
            Padding = new Thickness(11, 10),
            BackgroundColor = item.UnreadCount > 0 ? Color.FromArgb("#F1F7FE") : Colors.White,
            Stroke = Color.FromArgb("#E5EAF0"),
            StrokeThickness = 0.7,
            StrokeShape = new RoundRectangle { CornerRadius = 13 },
            Content = grid
        };
        var tap = new TapGestureRecognizer { CommandParameter = item.Thread.Id };
        tap.Tapped += OnConversationTapped;
        row.GestureRecognizers.Add(tap);
        return row;
    }

    private string GetThreadDisplayName(ChatThread thread)
    {
        if (thread.IsGroup) return string.IsNullOrWhiteSpace(thread.Name) ? "Grupo" : thread.Name;
        var other = GetOtherUser(thread);
        if (other == null) return "Contato";
        var saved = _store.Contacts.FirstOrDefault(contact =>
            contact.OwnerUserId == _currentUser?.Id && contact.ContactUserId == other.Id);
        return string.IsNullOrWhiteSpace(saved?.SavedName) ? other.DisplayName : saved.SavedName;
    }

    private LocalUser? GetOtherUser(ChatThread thread) =>
        _users.FirstOrDefault(user => thread.ParticipantIds.Contains(user.Id) && user.Id != _currentUser?.Id);

    private string GetSenderName(string senderId)
    {
        if (senderId == _currentUser?.Id) return "Você";
        var saved = _store.Contacts.FirstOrDefault(contact =>
            contact.OwnerUserId == _currentUser?.Id && contact.ContactUserId == senderId);
        return string.IsNullOrWhiteSpace(saved?.SavedName)
            ? _users.FirstOrDefault(user => user.Id == senderId)?.DisplayName ?? "Contato"
            : saved.SavedName;
    }

    private async void OnMenuClicked(object? sender, EventArgs e)
    {
        var action = await DisplayActionSheetAsync(
            "Chat interno", "Cancelar", null,
            "Novo grupo", "Marcar tudo como lido", "Configurações", "Favoritas");
        switch (action)
        {
            case "Novo grupo": await Shell.Current.GoToAsync(nameof(NewGroupPage)); break;
            case "Marcar tudo como lido":
                if (_currentUser != null)
                {
                    await _chat.MarkAllReadAsync(_currentUser.Id);
                    _store = await _chat.GetSnapshotAsync();
                    RenderConversations();
                    await DisplayAlertAsync("Chat interno", "Todas as mensagens foram marcadas como lidas.", "OK");
                }
                break;
            case "Configurações": await Shell.Current.GoToAsync(nameof(ChatSettingsPage)); break;
            case "Favoritas": await Shell.Current.GoToAsync(nameof(FavoriteMessagesPage)); break;
        }
    }

    private void OnFilterClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: string key }) return;
        _selectedFilter = key;
        BuildFilters();
        RenderConversations();
    }

    private void OnSearchChanged(object? sender, EventArgs e) => RenderConversations();
    private async void OnAddFilterClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync(nameof(NewChatListPage));
    private async void OnNewChatTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync(nameof(ChatContactsPage));
    private async void OnBackClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");

    private async void OnConversationTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is string threadId)
            await Shell.Current.GoToAsync($"{nameof(ChatConversationPage)}?threadId={Uri.EscapeDataString(threadId)}");
    }

    private sealed record ConversationItem(ChatThread Thread, ChatMessage? LastMessage, int UnreadCount, string Name);
}
