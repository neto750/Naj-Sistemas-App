using Microsoft.Maui.Controls.Shapes;
using NajGravador.Models;
using NajGravador.Services;

namespace NajGravador.Views;

public partial class FavoriteMessagesPage : ContentPage
{
    private readonly LocalAccountRepository _accounts = new();
    private readonly ChatRepository _chat = new();
    private LocalUser? _user;

    public FavoriteMessagesPage() => InitializeComponent();

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _user = await _accounts.GetCurrentAsync();
        if (_user == null) return;
        var users = await _accounts.GetAllAsync();
        var store = await _chat.GetSnapshotAsync();
        var favorites = store.Messages
            .Where(message => message.FavoriteByUserIds.Contains(_user.Id))
            .OrderByDescending(message => message.SentAt)
            .ToList();
        MessagesHost.Children.Clear();
        if (favorites.Count == 0)
        {
            MessagesHost.Children.Add(new Label { Text = "Nenhuma mensagem favoritada.", FontSize = 14, TextColor = Color.FromArgb("#748292"), HorizontalTextAlignment = TextAlignment.Center, Margin = new Thickness(20, 70) });
            return;
        }

        foreach (var message in favorites)
        {
            var sender = message.SenderUserId == _user.Id ? "Você" : users.FirstOrDefault(item => item.Id == message.SenderUserId)?.DisplayName ?? "Contato";
            var thread = store.Threads.FirstOrDefault(item => item.Id == message.ThreadId);
            var content = new VerticalStackLayout { Spacing = 4 };
            content.Children.Add(new Label { Text = thread?.IsGroup == true ? thread.Name : sender, FontSize = 11, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#1E66C2") });
            content.Children.Add(new Label { Text = message.Text, FontSize = 14, TextColor = Color.FromArgb("#263A50") });
            content.Children.Add(new Label { Text = $"{sender} • {message.SentAt:dd/MM/yyyy HH:mm}", FontSize = 9, TextColor = Color.FromArgb("#7A8795") });
            var card = new Border { Padding = 12, BackgroundColor = Colors.White, Stroke = Color.FromArgb("#DFE5EB"), StrokeShape = new RoundRectangle { CornerRadius = 12 }, Content = content };
            var tap = new TapGestureRecognizer { CommandParameter = message.ThreadId };
            tap.Tapped += OnMessageTapped;
            card.GestureRecognizers.Add(tap);
            MessagesHost.Children.Add(card);
        }
    }

    private async void OnMessageTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is string threadId)
            await Shell.Current.GoToAsync($"{nameof(ChatConversationPage)}?threadId={Uri.EscapeDataString(threadId)}");
    }
    private async void OnBackClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");
}
