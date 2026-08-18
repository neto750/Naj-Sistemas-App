using NajGravador.Services;

namespace NajGravador.Views;

public partial class RegisterPage : ContentPage
{
    private readonly LocalAccountRepository _accounts = new();
    private bool _isSubmitting;

    public RegisterPage() => InitializeComponent();

    private async void OnCreateAccountClicked(object? sender, EventArgs e)
    {
        if (_isSubmitting) return;
        ErrorLabel.IsVisible = false;
        var password = PasswordEntry.Text ?? string.Empty;
        if (password != (ConfirmPasswordEntry.Text ?? string.Empty))
        {
            ErrorLabel.Text = "As senhas não são iguais.";
            ErrorLabel.IsVisible = true;
            return;
        }

        _isSubmitting = true;
        RegisterButton.IsEnabled = false;
        try
        {
            var (user, error) = await _accounts.RegisterAsync(
                NameEntry.Text ?? string.Empty,
                EmailEntry.Text ?? string.Empty,
                password);
            if (user == null)
            {
                ErrorLabel.Text = error;
                ErrorLabel.IsVisible = true;
                return;
            }

            _accounts.SetCurrentUser(user.Id);
            await Shell.Current.GoToAsync("//MainPage");
        }
        finally
        {
            RegisterButton.IsEnabled = true;
            _isSubmitting = false;
        }
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");
}
