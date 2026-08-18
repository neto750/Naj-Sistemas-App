using NajGravador.Models;
using NajGravador.Services;

namespace NajGravador.Views;

public partial class ChatSettingsPage : ContentPage
{
    private readonly LocalAccountRepository _accounts = new();
    private LocalUser? _user;
    private string _photoPath = string.Empty;

    public ChatSettingsPage() => InitializeComponent();

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _user = await _accounts.GetCurrentAsync();
        if (_user == null) return;
        NameEntry.Text = _user.DisplayName;
        EmailLabel.Text = _user.Email;
        _photoPath = _user.PhotoPath;
        NotificationsSwitch.IsToggled = _user.NotificationsEnabled;
        ReadReceiptsSwitch.IsToggled = _user.ReadReceiptsEnabled;
        RenderAvatar();
    }

    private void RenderAvatar() => AvatarHost.Content = ChatUi.CreateAvatar(_photoPath, NameEntry.Text, 92);

    private async void OnChangePhotoClicked(object? sender, EventArgs e)
    {
        try
        {
            var photo = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Escolha sua foto",
                FileTypes = FilePickerFileType.Images
            });
            if (photo == null) return;
            _photoPath = await _accounts.CopyProfilePhotoAsync(photo);
            RenderAvatar();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Foto", $"Não foi possível abrir esta imagem: {ex.Message}", "OK");
        }
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (_user == null) return;
        var name = NameEntry.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            await DisplayAlertAsync("Configurações", "Informe seu nome.", "OK");
            return;
        }
        await _accounts.UpdateProfileAsync(
            _user.Id, name, _photoPath,
            NotificationsSwitch.IsToggled,
            ReadReceiptsSwitch.IsToggled);
        await DisplayAlertAsync("Configurações", "Perfil atualizado.", "OK");
        await Shell.Current.GoToAsync("..");
    }

    private async void OnLogoutClicked(object? sender, EventArgs e)
    {
        var confirm = await DisplayAlertAsync("Sair da conta", "Deseja encerrar esta sessão?", "Sair", "Cancelar");
        if (!confirm) return;
        _accounts.Logout();
        await Shell.Current.GoToAsync(nameof(LoginPage));
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");
}
