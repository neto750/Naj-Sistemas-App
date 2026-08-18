using Microsoft.Maui.Controls.Shapes;

namespace NajGravador.Services;

public static class ChatUi
{
    public static Border CreateAvatar(string? photoPath, string? name, double size = 48, bool isGroup = false)
    {
        View content;
        if (!string.IsNullOrWhiteSpace(photoPath) && File.Exists(photoPath))
        {
            content = new Image { Source = ImageSource.FromFile(photoPath), Aspect = Aspect.AspectFill };
        }
        else
        {
            content = new Image
            {
                Source = isGroup ? "default_group.svg" : "default_avatar.svg",
                Aspect = Aspect.AspectFit,
                Margin = new Thickness(size * 0.18)
            };
        }

        return new Border
        {
            WidthRequest = size,
            HeightRequest = size,
            Padding = 0,
            BackgroundColor = Color.FromArgb(isGroup ? "#DCEBFA" : "#E3E6EA"),
            Stroke = Colors.Transparent,
            StrokeShape = new RoundRectangle { CornerRadius = size / 2 },
            Content = content
        };
    }

    public static string FormatElapsed(DateTime sentAt)
    {
        var elapsed = DateTime.Now - sentAt;
        if (elapsed.TotalMinutes < 1) return "agora";
        if (elapsed.TotalHours < 1) return $"{Math.Max(1, (int)elapsed.TotalMinutes)} min";
        if (elapsed.TotalDays < 1) return $"{(int)elapsed.TotalHours} h";
        if (elapsed.TotalDays < 7) return $"{(int)elapsed.TotalDays}d";
        return $"{Math.Max(1, (int)(elapsed.TotalDays / 7))} sem";
    }
}
