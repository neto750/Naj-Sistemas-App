using Microsoft.Maui.Controls.Shapes;
using NajGravador.Models;
using NajGravador.Services;

namespace NajGravador.Views;

public partial class NewGroupPage : ContentPage
{
    private readonly LocalAccountRepository _accounts = new();
    private readonly ChatRepository _chat = new();
    private LocalUser? _user;
    private List<SavedContact> _contacts = [];
    private readonly HashSet<string> _selectedIds = [];

    public NewGroupPage() => InitializeComponent();

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _user = await _accounts.GetCurrentAsync();
        if (_user == null) return;
        _contacts = (await _chat.GetContactsAsync(_user.Id)).ToList();
        RenderContacts();
    }

    private void RenderContacts()
    {
        ContactsHost.Children.Clear();
        var search = ContactSearchBar.Text?.Trim() ?? string.Empty;
        var contacts = _contacts.Where(contact => string.IsNullOrWhiteSpace(search) || contact.SavedName.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var contact in contacts) ContactsHost.Children.Add(CreateSelectionRow(contact));
        NoContactsLabel.IsVisible = contacts.Count == 0;
        NoContactsLabel.Text = _contacts.Count == 0 ? "Nenhum contato salvo!" : "Nenhum contato encontrado.";
    }

    private View CreateSelectionRow(SavedContact contact)
    {
        var grid = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
        grid.Children.Add(new Label { Text = contact.SavedName, FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#263A50"), VerticalTextAlignment = TextAlignment.Center });
        var check = new CheckBox { IsChecked = _selectedIds.Contains(contact.ContactUserId), Color = Color.FromArgb("#1E66C2"), CommandParameter = contact.ContactUserId };
        check.CheckedChanged += OnContactChecked;
        Grid.SetColumn(check, 1); grid.Children.Add(check);
        return new Border { Padding = new Thickness(12, 5), BackgroundColor = Colors.White, Stroke = Color.FromArgb("#E2E7ED"), StrokeShape = new RoundRectangle { CornerRadius = 11 }, Content = grid };
    }

    private void OnContactChecked(object? sender, CheckedChangedEventArgs e)
    {
        if (sender is not CheckBox { CommandParameter: string id }) return;
        if (e.Value) _selectedIds.Add(id); else _selectedIds.Remove(id);
    }

    private async void OnCreateClicked(object? sender, EventArgs e)
    {
        if (_user == null) return;
        if (string.IsNullOrWhiteSpace(GroupNameEntry.Text)) { await DisplayAlertAsync("Novo grupo", "Informe o nome do grupo.", "OK"); return; }
        if (_selectedIds.Count == 0) { await DisplayAlertAsync("Novo grupo", "Selecione pelo menos um contato.", "OK"); return; }
        var thread = await _chat.CreateGroupAsync(_user.Id, GroupNameEntry.Text, _selectedIds);
        await Shell.Current.GoToAsync($"{nameof(ChatConversationPage)}?threadId={Uri.EscapeDataString(thread.Id)}");
    }
    private void OnSearchChanged(object? sender, TextChangedEventArgs e) => RenderContacts();
    private async void OnBackClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");
}
