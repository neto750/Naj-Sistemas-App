using System.Security.Cryptography;
using System.Text.Json;
using NajGravador.Models;

namespace NajGravador.Services;

public sealed class LocalAccountRepository
{
    private const string FileName = "local_accounts.json";
    private const string SessionKey = "naj_active_user_id";
    private const int HashIterations = 100_000;
    private static readonly SemaphoreSlim FileLock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    private string FilePath => Path.Combine(FileSystem.AppDataDirectory, FileName);

    public async Task<IReadOnlyList<LocalUser>> GetAllAsync()
    {
        await FileLock.WaitAsync();
        try
        {
            return await ReadUnsafeAsync();
        }
        finally
        {
            FileLock.Release();
        }
    }

    public async Task<LocalUser?> GetByIdAsync(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return null;
        return (await GetAllAsync()).FirstOrDefault(user => user.Id == userId);
    }

    public async Task<LocalUser?> GetByEmailAsync(string? email)
    {
        var normalizedEmail = NormalizeEmail(email);
        return (await GetAllAsync()).FirstOrDefault(user => user.Email == normalizedEmail);
    }

    public Task<LocalUser?> GetCurrentAsync() =>
        GetByIdAsync(Preferences.Default.Get(SessionKey, string.Empty));

    public void SetCurrentUser(string userId) => Preferences.Default.Set(SessionKey, userId);

    public void Logout() => Preferences.Default.Remove(SessionKey);

    public async Task<(LocalUser? User, string? Error)> RegisterAsync(
        string displayName,
        string email,
        string password)
    {
        displayName = displayName.Trim();
        var normalizedEmail = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(displayName)) return (null, "Informe seu nome.");
        if (!IsValidEmail(normalizedEmail)) return (null, "Informe um e-mail válido.");
        if (password.Length < 4) return (null, "A senha deve ter pelo menos 4 caracteres.");

        await FileLock.WaitAsync();
        try
        {
            var users = await ReadUnsafeAsync();
            if (users.Any(user => user.Email == normalizedEmail))
                return (null, "Já existe uma conta com este e-mail.");

            var salt = RandomNumberGenerator.GetBytes(16);
            var user = new LocalUser
            {
                DisplayName = displayName,
                Email = normalizedEmail,
                PasswordSalt = Convert.ToBase64String(salt),
                PasswordHash = HashPassword(password, salt)
            };
            users.Add(user);
            await WriteUnsafeAsync(users);
            return (user, null);
        }
        finally
        {
            FileLock.Release();
        }
    }

    public async Task<LocalUser?> AuthenticateAsync(string email, string password)
    {
        var normalizedEmail = NormalizeEmail(email);
        var user = (await GetAllAsync()).FirstOrDefault(item => item.Email == normalizedEmail);
        if (user == null || string.IsNullOrWhiteSpace(user.PasswordSalt)) return null;
        try
        {
            var expected = Convert.FromBase64String(user.PasswordHash);
            var actual = Convert.FromBase64String(HashPassword(
                password,
                Convert.FromBase64String(user.PasswordSalt)));
            return CryptographicOperations.FixedTimeEquals(expected, actual) ? user : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public async Task UpdateProfileAsync(
        string userId,
        string displayName,
        string photoPath,
        bool notificationsEnabled,
        bool readReceiptsEnabled)
    {
        await FileLock.WaitAsync();
        try
        {
            var users = await ReadUnsafeAsync();
            var user = users.FirstOrDefault(item => item.Id == userId);
            if (user == null) return;
            user.DisplayName = displayName.Trim();
            user.PhotoPath = photoPath;
            user.NotificationsEnabled = notificationsEnabled;
            user.ReadReceiptsEnabled = readReceiptsEnabled;
            await WriteUnsafeAsync(users);
        }
        finally
        {
            FileLock.Release();
        }
    }

    public async Task<string> CopyProfilePhotoAsync(FileResult photo)
    {
        var directory = Path.Combine(FileSystem.AppDataDirectory, "profile_photos");
        Directory.CreateDirectory(directory);
        var extension = Path.GetExtension(photo.FileName);
        var destination = Path.Combine(directory, $"{Guid.NewGuid():N}{extension}");
        await using var source = await photo.OpenReadAsync();
        await using var target = File.Create(destination);
        await source.CopyToAsync(target);
        return destination;
    }

    private async Task<List<LocalUser>> ReadUnsafeAsync()
    {
        if (!File.Exists(FilePath)) return [];
        try
        {
            var json = await File.ReadAllTextAsync(FilePath);
            return string.IsNullOrWhiteSpace(json)
                ? []
                : JsonSerializer.Deserialize<List<LocalUser>>(json, _jsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private Task WriteUnsafeAsync(List<LocalUser> users) =>
        File.WriteAllTextAsync(FilePath, JsonSerializer.Serialize(users, _jsonOptions));

    private static string NormalizeEmail(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();

    private static bool IsValidEmail(string email)
    {
        try { return new System.Net.Mail.MailAddress(email).Address == email; }
        catch { return false; }
    }

    private static string HashPassword(string password, byte[] salt) =>
        Convert.ToBase64String(Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            HashIterations,
            HashAlgorithmName.SHA256,
            32));
}
