using Microsoft.Maui.Controls.Shapes;
using NajGravador.Models;
using NajGravador.Services;

namespace NajGravador.Views;

public partial class NewChatListPage : ContentPage
{
    private readonly LocalAccountRepository _accounts = new();
    private readonly ChatRepository _chat = new();
    private LocalUser? _user;
    private List<SavedContact> _contacts = [];
    private readonly HashSet<string> _selectedIds = [];

    public NewChatListPage() => InitializeComponent();

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _user = await _accounts.GetCurrentAsync();
        if (_user == null) return;
        _contacts = (await _chat.GetContactsAsync(_user.Id)).ToList();
        ContactsHost.Children.Clear();
        foreach (var contact in _contacts)
        {
            var grid = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
            grid.Children.Add(new Label { Text = contact.SavedName, FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#263A50"), VerticalTextAlignment = TextAlignment.Center });
            var check = new CheckBox { Color = Color.FromArgb("#1E66C2"), CommandParameter = contact.ContactUserId };
            check.CheckedChanged += OnContactChecked;
            Grid.SetColumn(check, 1); grid.Children.Add(check);
            ContactsHost.Children.Add(new Border { Padding = new Thickness(12, 5), BackgroundColor = Colors.White, Stroke = Color.FromArgb("#E2E7ED"), StrokeShape = new RoundRectangle { CornerRadius = 11 }, Content = grid });
        }
        NoContactsLabel.IsVisible = _contacts.Count == 0;
    }

    private void OnContactChecked(object? sender, CheckedChangedEventArgs e)
    {
        if (sender is not CheckBox { CommandParameter: string id }) return;
        if (e.Value) _selectedIds.Add(id); else _selectedIds.Remove(id);
    }

    private async void OnCreateClicked(object? sender, EventArgs e)
    {
        if (_user == null) return;
        if (string.IsNullOrWhiteSpace(ListNameEntry.Text)) { await DisplayAlertAsync("Nova lista", "Informe o nome da lista.", "OK"); return; }
        if (_selectedIds.Count == 0) { await DisplayAlertAsync("Nova lista", "Selecione pelo menos um contato.", "OK"); return; }
        await _chat.SaveFilterAsync(_user.Id, ListNameEntry.Text, _selectedIds);
        await Shell.Current.GoToAsync("..");
    }
    private async void OnBackClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");
}
