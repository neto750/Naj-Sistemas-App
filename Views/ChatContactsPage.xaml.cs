using Microsoft.Maui.Controls.Shapes;
using NajGravador.Models;
using NajGravador.Services;

namespace NajGravador.Views;

public partial class ChatContactsPage : ContentPage
{
    private readonly LocalAccountRepository _accounts = new();
    private readonly ChatRepository _chat = new();
    private LocalUser? _user;
    private List<SavedContact> _contacts = [];
    private List<LocalUser> _users = [];

    public ChatContactsPage() => InitializeComponent();

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _user = await _accounts.GetCurrentAsync();
        if (_user == null) return;
        _contacts = (await _chat.GetContactsAsync(_user.Id)).ToList();
        _users = (await _accounts.GetAllAsync()).ToList();
        ContactCountLabel.Text = $"{_contacts.Count} contato{(_contacts.Count == 1 ? string.Empty : "s")}";
        RenderContacts();
    }

    private void RenderContacts()
    {
        ContactsHost.Children.Clear();
        var search = ContactSearchBar.Text?.Trim() ?? string.Empty;
        var contacts = _contacts.Where(contact =>
            string.IsNullOrWhiteSpace(search) ||
            contact.SavedName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            contact.Email.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var contact in contacts)
        {
            var profile = _users.FirstOrDefault(user => user.Id == contact.ContactUserId);
            var grid = new Grid { ColumnDefinitions = { new ColumnDefinition(new GridLength(50)), new ColumnDefinition(GridLength.Star) }, ColumnSpacing = 9 };
            grid.Children.Add(ChatUi.CreateAvatar(profile?.PhotoPath, contact.SavedName, 44));
            var text = new VerticalStackLayout { Spacing = 1, VerticalOptions = LayoutOptions.Center };
            text.Children.Add(new Label { Text = contact.SavedName, FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#263A50") });
            text.Children.Add(new Label { Text = contact.Email, FontSize = 10, TextColor = Color.FromArgb("#7A8795") });
            Grid.SetColumn(text, 1);
            grid.Children.Add(text);
            var row = new Border { Padding = new Thickness(9, 7), BackgroundColor = Colors.White, Stroke = Color.FromArgb("#E5EAF0"), StrokeShape = new RoundRectangle { CornerRadius = 11 }, Content = grid };
            var tap = new TapGestureRecognizer { CommandParameter = contact.ContactUserId };
            tap.Tapped += OnContactTapped;
            row.GestureRecognizers.Add(tap);
            ContactsHost.Children.Add(row);
        }
        NoContactsLabel.IsVisible = contacts.Count == 0;
        NoContactsLabel.Text = _contacts.Count == 0 ? "Nenhum contato salvo!" : "Nenhum contato encontrado.";
    }

    private async void OnContactTapped(object? sender, TappedEventArgs e)
    {
        if (_user == null || e.Parameter is not string contactUserId) return;
        var thread = await _chat.GetOrCreateDirectThreadAsync(_user.Id, contactUserId);
        await Shell.Current.GoToAsync($"{nameof(ChatConversationPage)}?threadId={Uri.EscapeDataString(thread.Id)}");
    }
    private void OnSearchChanged(object? sender, TextChangedEventArgs e) => RenderContacts();
    private async void OnNewGroupTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync(nameof(NewGroupPage));
    private async void OnNewContactTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync(nameof(NewContactPage));
    private async void OnBackClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");
}
