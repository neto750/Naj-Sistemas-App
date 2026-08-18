using NajGravador.Services;

namespace NajGravador.Views;

public partial class NewContactPage : ContentPage
{
    private readonly LocalAccountRepository _accounts = new();
    private readonly ChatRepository _chat = new();

    public NewContactPage() => InitializeComponent();

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;
        var current = await _accounts.GetCurrentAsync();
        var target = await _accounts.GetByEmailAsync(EmailEntry.Text);
        if (current == null) return;
        if (target == null)
        {
            ShowError("Nenhuma conta encontrada com este e-mail.");
            return;
        }
        if (target.Id == current.Id)
        {
            ShowError("Você não pode adicionar sua própria conta.");
            return;
        }
        if (string.IsNullOrWhiteSpace(FirstNameEntry.Text))
        {
            ShowError("Informe o nome do contato.");
            return;
        }
        await _chat.SaveContactAsync(current.Id, target, FirstNameEntry.Text ?? string.Empty, LastNameEntry.Text ?? string.Empty);
        await DisplayAlertAsync("Novo contato", "Contato salvo com sucesso.", "OK");
        await Shell.Current.GoToAsync("..");
    }

    private void ShowError(string message) { ErrorLabel.Text = message; ErrorLabel.IsVisible = true; }
    private async void OnBackClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");
}
