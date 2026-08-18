namespace NajGravador.Models;

public enum CalendarRecurrence
{
    None,
    Daily,
    Weekly,
    Monthly,
    Yearly
}

public enum CalendarItemType
{
    Event,
    Task,
    Birthday
}

public sealed class CalendarEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Title { get; set; } = string.Empty;

    public CalendarItemType Type { get; set; } = CalendarItemType.Event;

    public DateTime Date { get; set; } = DateTime.Today;

    public TimeSpan StartTime { get; set; } = TimeSpan.FromHours(9);

    public TimeSpan EndTime { get; set; } = TimeSpan.FromHours(10);

    public DateTime? DeadlineDate { get; set; }

    public TimeSpan? DeadlineTime { get; set; }

    public bool IsAllDay { get; set; }

    public string Location { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int? ReminderMinutes { get; set; } = 10;

    public CalendarRecurrence Recurrence { get; set; }

    public string ColorHex { get; set; } = "#1E66C2";

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
