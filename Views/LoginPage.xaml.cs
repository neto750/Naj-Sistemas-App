using NajGravador.Services;

namespace NajGravador.Views;

public partial class LoginPage : ContentPage
{
    private readonly LocalAccountRepository _accounts = new();
    private bool _isSubmitting;

    public LoginPage() => InitializeComponent();

    protected override bool OnBackButtonPressed() => true;

    private async Task LoginAsync()
    {
        if (_isSubmitting) return;
        _isSubmitting = true;
        LoginButton.IsEnabled = false;
        ErrorLabel.IsVisible = false;
        try
        {
            var user = await _accounts.AuthenticateAsync(
                EmailEntry.Text ?? string.Empty,
                PasswordEntry.Text ?? string.Empty);
            if (user == null)
            {
                ErrorLabel.Text = "E-mail ou senha incorretos.";
                ErrorLabel.IsVisible = true;
                return;
            }

            _accounts.SetCurrentUser(user.Id);
            PasswordEntry.Text = string.Empty;
            await Shell.Current.GoToAsync("//MainPage");
        }
        finally
        {
            LoginButton.IsEnabled = true;
            _isSubmitting = false;
        }
    }

    private async void OnLoginClicked(object? sender, EventArgs e) => await LoginAsync();
    private async void OnLoginCompleted(object? sender, EventArgs e) => await LoginAsync();
    private async void OnRegisterClicked(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync(nameof(RegisterPage));
}
