using System.Text.Json;
using NajGravador.Models;

namespace NajGravador.Services;

public sealed class KanbanRepository
{
    private const string FileName = "task_boards.json";
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    private string FilePath => Path.Combine(FileSystem.AppDataDirectory, FileName);
    public string AttachmentsDirectory => Path.Combine(FileSystem.AppDataDirectory, "task_attachments");

    public async Task<List<KanbanBoard>> GetAllAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            var boards = await ReadUnsafeAsync();
            EnsureCollections(boards);
            return boards;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<KanbanBoard?> GetAsync(string boardId) =>
        (await GetAllAsync()).FirstOrDefault(board => board.Id == boardId);

    public async Task SaveAsync(KanbanBoard board)
    {
        await _fileLock.WaitAsync();
        try
        {
            var boards = await ReadUnsafeAsync();
            var index = boards.FindIndex(item => item.Id == board.Id);
            board.UpdatedAt = DateTime.Now;
            if (index >= 0)
                boards[index] = board;
            else
                boards.Add(board);

            await WriteUnsafeAsync(boards);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task DeleteAsync(string boardId)
    {
        await _fileLock.WaitAsync();
        try
        {
            var boards = await ReadUnsafeAsync();
            boards.RemoveAll(board => board.Id == boardId);
            await WriteUnsafeAsync(boards);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<string> CopyAttachmentAsync(FileResult file)
    {
        Directory.CreateDirectory(AttachmentsDirectory);
        var safeName = Path.GetFileName(file.FileName);
        var destination = Path.Combine(AttachmentsDirectory, $"{Guid.NewGuid():N}_{safeName}");
        await using var source = await file.OpenReadAsync();
        await using var target = File.Create(destination);
        await source.CopyToAsync(target);
        return destination;
    }

    private async Task<List<KanbanBoard>> ReadUnsafeAsync()
    {
        if (!File.Exists(FilePath))
            return [];

        try
        {
            var json = await File.ReadAllTextAsync(FilePath);
            return string.IsNullOrWhiteSpace(json)
                ? []
                : JsonSerializer.Deserialize<List<KanbanBoard>>(json, _jsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private Task WriteUnsafeAsync(List<KanbanBoard> boards)
    {
        var json = JsonSerializer.Serialize(boards, _jsonOptions);
        return File.WriteAllTextAsync(FilePath, json);
    }

    private static void EnsureCollections(IEnumerable<KanbanBoard> boards)
    {
        foreach (var board in boards)
        {
            board.Lists ??= [];
            board.Activity ??= [];
            foreach (var list in board.Lists)
            {
                list.Cards ??= [];
                foreach (var card in list.Cards)
                {
                    card.Checklist ??= [];
                    card.Attachments ??= [];
                    card.Comments ??= [];
                }
            }
        }
    }
}
