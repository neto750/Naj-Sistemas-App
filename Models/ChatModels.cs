namespace NajGravador.Models;

public sealed class LocalUser
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PhotoPath { get; set; } = string.Empty;
    public bool NotificationsEnabled { get; set; } = true;
    public bool ReadReceiptsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public sealed class SavedContact
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string OwnerUserId { get; set; } = string.Empty;
    public string ContactUserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public string SavedName => string.Join(" ", new[] { FirstName, LastName }
        .Where(value => !string.IsNullOrWhiteSpace(value)));
}

public sealed class ChatThread
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public bool IsGroup { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CreatedByUserId { get; set; } = string.Empty;
    public List<string> ParticipantIds { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public sealed class ChatMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ThreadId { get; set; } = string.Empty;
    public string SenderUserId { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTime SentAt { get; set; } = DateTime.Now;
    public List<string> ReadByUserIds { get; set; } = [];
    public List<string> FavoriteByUserIds { get; set; } = [];
}

public sealed class ChatCustomFilter
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string OwnerUserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<string> ContactUserIds { get; set; } = [];
}

public sealed class ChatStore
{
    public List<SavedContact> Contacts { get; set; } = [];
    public List<ChatThread> Threads { get; set; } = [];
    public List<ChatMessage> Messages { get; set; } = [];
    public List<ChatCustomFilter> Filters { get; set; } = [];
}
