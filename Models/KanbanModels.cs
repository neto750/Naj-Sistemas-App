namespace NajGravador.Models;

public sealed class KanbanBoard
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ThemeKey { get; set; } = "ocean";
    public bool IsFavorite { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public List<KanbanList> Lists { get; set; } = [];
    public List<KanbanActivity> Activity { get; set; } = [];
}

public sealed class KanbanList
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public bool IsCollapsed { get; set; }
    public List<KanbanCard> Cards { get; set; } = [];
}

public sealed class KanbanCard
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#FFFFFF";
    public string LabelName { get; set; } = string.Empty;
    public string LabelColorHex { get; set; } = "#579DFF";
    public string Assignee { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public List<KanbanChecklistItem> Checklist { get; set; } = [];
    public List<KanbanAttachment> Attachments { get; set; } = [];
    public List<KanbanComment> Comments { get; set; } = [];
}

public sealed class KanbanChecklistItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Text { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
}

public sealed class KanbanAttachment
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FileName { get; set; } = string.Empty;
    public string LocalPath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public DateTime AddedAt { get; set; } = DateTime.Now;
}

public sealed class KanbanComment
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public sealed class KanbanActivity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public sealed record KanbanTheme(
    string Key,
    string Name,
    string StartHex,
    string EndHex,
    string AccentHex,
    bool UsesDarkText = false);

public static class KanbanThemes
{
    public static IReadOnlyList<KanbanTheme> All { get; } =
    [
        new("ocean", "Oceano", "#0C66E4", "#0055CC", "#579DFF"),
        new("aurora", "Aurora", "#6E5DC6", "#9F8FEF", "#B8ACF6"),
        new("forest", "Floresta", "#1F845A", "#216E4E", "#4BCE97"),
        new("sunset", "Pôr do sol", "#E56910", "#AE2E24", "#FEA362"),
        new("berry", "Amora", "#943D73", "#5E4DB2", "#E774BB"),
        new("sky", "Céu claro", "#B8E6FF", "#85B8FF", "#0C66E4", true),
        new("slate", "Grafite", "#44546F", "#1D2125", "#8590A2")
    ];

    public static KanbanTheme Find(string? key) =>
        All.FirstOrDefault(theme => theme.Key == key) ?? All[0];
}
