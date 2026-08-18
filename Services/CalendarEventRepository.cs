using System.Text.Json;
using NajGravador.Models;

namespace NajGravador.Services;

public sealed class CalendarEventRepository
{
    private const string FileName = "calendar_events.json";
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    private string FilePath => Path.Combine(FileSystem.AppDataDirectory, FileName);

    public async Task<IReadOnlyList<CalendarEvent>> GetAllAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            if (!File.Exists(FilePath))
            {
                return Array.Empty<CalendarEvent>();
            }

            var json = await File.ReadAllTextAsync(FilePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return Array.Empty<CalendarEvent>();
            }

            return JsonSerializer.Deserialize<List<CalendarEvent>>(json, _jsonOptions)
                   ?? new List<CalendarEvent>();
        }
        catch (JsonException)
        {
            return Array.Empty<CalendarEvent>();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task SaveAsync(CalendarEvent calendarEvent)
    {
        await _fileLock.WaitAsync();
        try
        {
            var events = await ReadUnsafeAsync();
            var existingIndex = events.FindIndex(item => item.Id == calendarEvent.Id);
            calendarEvent.Date = calendarEvent.Date.Date;
            calendarEvent.UpdatedAt = DateTime.Now;

            if (existingIndex >= 0)
            {
                events[existingIndex] = calendarEvent;
            }
            else
            {
                events.Add(calendarEvent);
            }

            await WriteUnsafeAsync(events);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task DeleteAsync(string eventId)
    {
        await _fileLock.WaitAsync();
        try
        {
            var events = await ReadUnsafeAsync();
            events.RemoveAll(item => item.Id == eventId);
            await WriteUnsafeAsync(events);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task<List<CalendarEvent>> ReadUnsafeAsync()
    {
        if (!File.Exists(FilePath))
        {
            return new List<CalendarEvent>();
        }

        var json = await File.ReadAllTextAsync(FilePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<CalendarEvent>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<CalendarEvent>>(json, _jsonOptions)
                   ?? new List<CalendarEvent>();
        }
        catch (JsonException)
        {
            return new List<CalendarEvent>();
        }
    }

    private Task WriteUnsafeAsync(List<CalendarEvent> events)
    {
        var json = JsonSerializer.Serialize(
            events.OrderBy(item => item.Date).ThenBy(item => item.StartTime),
            _jsonOptions);
        return File.WriteAllTextAsync(FilePath, json);
    }
}
