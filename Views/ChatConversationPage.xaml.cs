using Microsoft.Maui.Controls.Shapes;
using NajGravador.Models;
using NajGravador.Services;

namespace NajGravador.Views;

[QueryProperty(nameof(ThreadId), "threadId")]
public partial class ChatConversationPage : ContentPage
{
    private readonly LocalAccountRepository _accounts = new();
    private readonly ChatRepository _chat = new();
    private LocalUser? _currentUser;
    private List<LocalUser> _users = [];
    private ChatStore _store = new();
    private ChatThread? _thread;
    private bool _isSending;

    public string ThreadId { get; set; } = string.Empty;

    public ChatConversationPage() => InitializeComponent();

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _currentUser = await _accounts.GetCurrentAsync();
        if (_currentUser == null) return;
        _users = (await _accounts.GetAllAsync()).ToList();
        _thread = await _chat.GetThreadAsync(ThreadId);
        if (_thread == null || !_thread.ParticipantIds.Contains(_currentUser.Id))
        {
            await DisplayAlertAsync("Conversa", "Esta conversa não está disponível.", "OK");
            await Shell.Current.GoToAsync("..");
            return;
        }

        await _chat.MarkThreadReadAsync(_thread.Id, _currentUser.Id);
        _store = await _chat.GetSnapshotAsync();
        UpdateHeader();
        RenderMessages();
        await ScrollToEndAsync();
    }

    private void UpdateHeader()
    {
        if (_thread == null || _currentUser == null) return;
        if (_thread.IsGroup)
        {
            TitleLabel.Text = _thread.Name;
            SubtitleLabel.Text = $"{_thread.ParticipantIds.Count} participantes";
            AvatarHost.Content = ChatUi.CreateAvatar(null, _thread.Name, 40, true);
            return;
        }

        var other = _users.FirstOrDefault(user =>
            user.Id != _currentUser.Id && _thread.ParticipantIds.Contains(user.Id));
        TitleLabel.Text = GetUserName(other?.Id ?? string.Empty);
        SubtitleLabel.Text = other?.Email ?? "Chat interno NAJ";
        AvatarHost.Content = ChatUi.CreateAvatar(other?.PhotoPath, TitleLabel.Text, 40);
    }

    private void RenderMessages()
    {
        MessagesHost.Children.Clear();
        if (_thread == null || _currentUser == null) return;
        var messages = _store.Messages
            .Where(message => message.ThreadId == _thread.Id)
            .OrderBy(message => message.SentAt)
            .ToList();
        if (messages.Count == 0)
        {
            MessagesHost.Children.Add(new Label
            {
                Text = "Nenhuma mensagem ainda. Envie a primeira mensagem!",
                FontSize = 12,
                TextColor = Color.FromArgb("#758392"),
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(20, 50)
            });
            return;
        }

        DateTime? lastDate = null;
        foreach (var message in messages)
        {
            if (lastDate != message.SentAt.Date)
            {
                MessagesHost.Children.Add(CreateDateDivider(message.SentAt.Date));
                lastDate = message.SentAt.Date;
            }
            MessagesHost.Children.Add(CreateMessageBubble(message));
        }
    }

    private View CreateDateDivider(DateTime date)
    {
        var text = date == DateTime.Today ? "Hoje" : date == DateTime.Today.AddDays(-1) ? "Ontem" : date.ToString("dd/MM/yyyy");
        return new Border
        {
            Padding = new Thickness(9, 4),
            Margin = new Thickness(0, 4),
            HorizontalOptions = LayoutOptions.Center,
            BackgroundColor = Color.FromArgb("#DCE7F2"),
            Stroke = Colors.Transparent,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Content = new Label { Text = text, FontSize = 10, TextColor = Color.FromArgb("#546779") }
        };
    }

    private View CreateMessageBubble(ChatMessage message)
    {
        var isMine = message.SenderUserId == _currentUser!.Id;
        var favorite = message.FavoriteByUserIds.Contains(_currentUser.Id);
        var content = new VerticalStackLayout { Spacing = 2 };
        if (_thread!.IsGroup && !isMine)
        {
            content.Children.Add(new Label
            {
                Text = GetUserName(message.SenderUserId),
                FontSize = 10,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#1E66C2")
            });
        }
        content.Children.Add(new Label
        {
            Text = message.Text,
            FontSize = 14,
            TextColor = Color.FromArgb("#233548"),
            LineBreakMode = LineBreakMode.WordWrap
        });

        var meta = new HorizontalStackLayout { Spacing = 4, HorizontalOptions = LayoutOptions.End };
        var favoriteButton = new Button
        {
            Text = favorite ? "★" : "☆",
            FontSize = 12,
            TextColor = Color.FromArgb(favorite ? "#E0A100" : "#7B8997"),
            BackgroundColor = Colors.Transparent,
            Padding = 0,
            WidthRequest = 24,
            HeightRequest = 20,
            MinimumWidthRequest = 24,
            MinimumHeightRequest = 20,
            CommandParameter = message.Id
        };
        favoriteButton.Clicked += OnFavoriteClicked;
        meta.Children.Add(favoriteButton);
        meta.Children.Add(new Label
        {
            Text = message.SentAt.ToString("HH:mm"),
            FontSize = 9,
            TextColor = Color.FromArgb("#718090"),
            VerticalTextAlignment = TextAlignment.Center
        });
        if (isMine)
        {
            var everyoneRead = _thread.ParticipantIds
                .Where(id => id != _currentUser.Id)
                .All(id =>
                    message.ReadByUserIds.Contains(id) &&
                    (_users.FirstOrDefault(user => user.Id == id)?.ReadReceiptsEnabled ?? true));
            meta.Children.Add(new Label
            {
                Text = "✓✓",
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb(everyoneRead ? "#1689D4" : "#81909E"),
                VerticalTextAlignment = TextAlignment.Center
            });
        }
        content.Children.Add(meta);

        return new Border
        {
            Padding = new Thickness(10, 6),
            Margin = isMine ? new Thickness(54, 0, 0, 0) : new Thickness(0, 0, 54, 0),
            HorizontalOptions = isMine ? LayoutOptions.End : LayoutOptions.Start,
            MaximumWidthRequest = 460,
            BackgroundColor = Color.FromArgb(isMine ? "#DDEEFF" : "#FFFFFF"),
            Stroke = Color.FromArgb(isMine ? "#C5DFF7" : "#DFE5EB"),
            StrokeThickness = 0.7,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Content = content
        };
    }

    private string GetUserName(string userId)
    {
        if (userId == _currentUser?.Id) return "Você";
        var saved = _store.Contacts.FirstOrDefault(contact =>
            contact.OwnerUserId == _currentUser?.Id && contact.ContactUserId == userId);
        return string.IsNullOrWhiteSpace(saved?.SavedName)
            ? _users.FirstOrDefault(user => user.Id == userId)?.DisplayName ?? "Contato"
            : saved.SavedName;
    }

    private async Task SendAsync()
    {
        var text = MessageEntry.Text?.Trim() ?? string.Empty;
        if (_isSending || string.IsNullOrWhiteSpace(text) || _thread == null || _currentUser == null) return;
        _isSending = true;
        MessageEntry.Text = string.Empty;
        try
        {
            await _chat.SendMessageAsync(_thread.Id, _currentUser.Id, text);
            _store = await _chat.GetSnapshotAsync();
            RenderMessages();
            await ScrollToEndAsync();
        }
        finally { _isSending = false; }
    }

    private async Task ScrollToEndAsync()
    {
        await Task.Delay(40);
        if (MessagesHost.Children.LastOrDefault() is Element last)
            await MessagesScroll.ScrollToAsync(last, ScrollToPosition.End, false);
    }

    private async void OnFavoriteClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string messageId } && _currentUser != null)
        {
            await _chat.ToggleFavoriteAsync(messageId, _currentUser.Id);
            _store = await _chat.GetSnapshotAsync();
            RenderMessages();
        }
    }

    private async void OnConversationMenuClicked(object? sender, EventArgs e)
    {
        var action = await DisplayActionSheetAsync("Conversa", "Cancelar", null, "Marcar como lida", "Ver favoritas");
        if (action == "Marcar como lida" && _thread != null && _currentUser != null)
        {
            await _chat.MarkThreadReadAsync(_thread.Id, _currentUser.Id);
            _store = await _chat.GetSnapshotAsync();
            RenderMessages();
        }
        else if (action == "Ver favoritas") await Shell.Current.GoToAsync(nameof(FavoriteMessagesPage));
    }

    private async void OnSendClicked(object? sender, EventArgs e) => await SendAsync();
    private async void OnMessageCompleted(object? sender, EventArgs e) => await SendAsync();
    private async void OnBackClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");
}
