using System.Text.Json;
using NajGravador.Models;

namespace NajGravador.Services;

public sealed class ChatRepository
{
    private const string FileName = "internal_chat.json";
    private static readonly SemaphoreSlim FileLock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private string FilePath => Path.Combine(FileSystem.AppDataDirectory, FileName);

    public async Task<IReadOnlyList<SavedContact>> GetContactsAsync(string ownerUserId) =>
        (await ReadAsync()).Contacts
            .Where(contact => contact.OwnerUserId == ownerUserId)
            .OrderBy(contact => contact.SavedName)
            .ToList();

    public async Task<SavedContact> SaveContactAsync(
        string ownerUserId,
        LocalUser contactUser,
        string firstName,
        string lastName)
    {
        return await MutateAsync(store =>
        {
            var contact = store.Contacts.FirstOrDefault(item =>
                item.OwnerUserId == ownerUserId && item.ContactUserId == contactUser.Id);
            if (contact == null)
            {
                contact = new SavedContact
                {
                    OwnerUserId = ownerUserId,
                    ContactUserId = contactUser.Id
                };
                store.Contacts.Add(contact);
            }
            contact.Email = contactUser.Email;
            contact.FirstName = firstName.Trim();
            contact.LastName = lastName.Trim();
            return contact;
        });
    }

    public async Task<IReadOnlyList<ChatThread>> GetThreadsAsync(string userId) =>
        (await ReadAsync()).Threads
            .Where(thread => thread.ParticipantIds.Contains(userId))
            .ToList();

    public async Task<ChatThread?> GetThreadAsync(string threadId) =>
        (await ReadAsync()).Threads.FirstOrDefault(thread => thread.Id == threadId);

    public async Task<ChatThread> GetOrCreateDirectThreadAsync(string firstUserId, string secondUserId)
    {
        return await MutateAsync(store =>
        {
            var thread = store.Threads.FirstOrDefault(item =>
                !item.IsGroup && item.ParticipantIds.Count == 2 &&
                item.ParticipantIds.Contains(firstUserId) && item.ParticipantIds.Contains(secondUserId));
            if (thread != null) return thread;
            thread = new ChatThread
            {
                CreatedByUserId = firstUserId,
                ParticipantIds = [firstUserId, secondUserId]
            };
            store.Threads.Add(thread);
            return thread;
        });
    }

    public async Task<ChatThread> CreateGroupAsync(string ownerUserId, string name, IEnumerable<string> participantIds)
    {
        return await MutateAsync(store =>
        {
            var participants = participantIds.Append(ownerUserId).Distinct().ToList();
            var thread = new ChatThread
            {
                IsGroup = true,
                Name = name.Trim(),
                CreatedByUserId = ownerUserId,
                ParticipantIds = participants
            };
            store.Threads.Add(thread);
            return thread;
        });
    }

    public async Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(string threadId) =>
        (await ReadAsync()).Messages
            .Where(message => message.ThreadId == threadId)
            .OrderBy(message => message.SentAt)
            .ToList();

    public async Task<ChatMessage> SendMessageAsync(string threadId, string senderUserId, string text)
    {
        return await MutateAsync(store =>
        {
            var message = new ChatMessage
            {
                ThreadId = threadId,
                SenderUserId = senderUserId,
                Text = text.Trim(),
                ReadByUserIds = [senderUserId]
            };
            store.Messages.Add(message);
            return message;
        });
    }

    public Task MarkThreadReadAsync(string threadId, string userId) => MutateAsync<object?>(store =>
    {
        foreach (var message in store.Messages.Where(item =>
                     item.ThreadId == threadId && item.SenderUserId != userId && !item.ReadByUserIds.Contains(userId)))
            message.ReadByUserIds.Add(userId);
        return null;
    });

    public Task MarkAllReadAsync(string userId) => MutateAsync<object?>(store =>
    {
        var threadIds = store.Threads
            .Where(thread => thread.ParticipantIds.Contains(userId))
            .Select(thread => thread.Id)
            .ToHashSet();
        foreach (var message in store.Messages.Where(item =>
                     threadIds.Contains(item.ThreadId) && item.SenderUserId != userId && !item.ReadByUserIds.Contains(userId)))
            message.ReadByUserIds.Add(userId);
        return null;
    });

    public async Task<int> GetUnreadCountAsync(string userId)
    {
        var store = await ReadAsync();
        var threadIds = store.Threads
            .Where(thread => thread.ParticipantIds.Contains(userId))
            .Select(thread => thread.Id)
            .ToHashSet();
        return store.Messages.Count(message =>
            threadIds.Contains(message.ThreadId) &&
            message.SenderUserId != userId &&
            !message.ReadByUserIds.Contains(userId));
    }

    public Task ToggleFavoriteAsync(string messageId, string userId) => MutateAsync<object?>(store =>
    {
        var message = store.Messages.FirstOrDefault(item => item.Id == messageId);
        if (message == null) return null;
        if (!message.FavoriteByUserIds.Remove(userId)) message.FavoriteByUserIds.Add(userId);
        return null;
    });

    public async Task<IReadOnlyList<ChatMessage>> GetFavoriteMessagesAsync(string userId) =>
        (await ReadAsync()).Messages
            .Where(message => message.FavoriteByUserIds.Contains(userId))
            .OrderByDescending(message => message.SentAt)
            .ToList();

    public async Task<IReadOnlyList<ChatCustomFilter>> GetFiltersAsync(string ownerUserId) =>
        (await ReadAsync()).Filters.Where(filter => filter.OwnerUserId == ownerUserId).ToList();

    public async Task<ChatCustomFilter> SaveFilterAsync(
        string ownerUserId,
        string name,
        IEnumerable<string> contactUserIds)
    {
        return await MutateAsync(store =>
        {
            var filter = new ChatCustomFilter
            {
                OwnerUserId = ownerUserId,
                Name = name.Trim(),
                ContactUserIds = contactUserIds.Distinct().ToList()
            };
            store.Filters.Add(filter);
            return filter;
        });
    }

    public async Task<ChatStore> GetSnapshotAsync() => await ReadAsync();

    private async Task<ChatStore> ReadAsync()
    {
        await FileLock.WaitAsync();
        try { return await ReadUnsafeAsync(); }
        finally { FileLock.Release(); }
    }

    private async Task<T> MutateAsync<T>(Func<ChatStore, T> action)
    {
        await FileLock.WaitAsync();
        try
        {
            var store = await ReadUnsafeAsync();
            var result = action(store);
            await WriteUnsafeAsync(store);
            return result;
        }
        finally
        {
            FileLock.Release();
        }
    }

    private async Task<ChatStore> ReadUnsafeAsync()
    {
        if (!File.Exists(FilePath)) return new ChatStore();
        try
        {
            var json = await File.ReadAllTextAsync(FilePath);
            var store = string.IsNullOrWhiteSpace(json)
                ? new ChatStore()
                : JsonSerializer.Deserialize<ChatStore>(json, _jsonOptions) ?? new ChatStore();
            EnsureCollections(store);
            return store;
        }
        catch (JsonException)
        {
            return new ChatStore();
        }
    }

    private Task WriteUnsafeAsync(ChatStore store) =>
        File.WriteAllTextAsync(FilePath, JsonSerializer.Serialize(store, _jsonOptions));

    private static void EnsureCollections(ChatStore store)
    {
        store.Contacts ??= [];
        store.Threads ??= [];
        store.Messages ??= [];
        store.Filters ??= [];
        foreach (var thread in store.Threads) thread.ParticipantIds ??= [];
        foreach (var message in store.Messages)
        {
            message.ReadByUserIds ??= [];
            message.FavoriteByUserIds ??= [];
        }
        foreach (var filter in store.Filters) filter.ContactUserIds ??= [];
    }
}
