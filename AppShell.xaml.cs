using NajGravador.Views;

namespace NajGravador;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute(
            nameof(RecordingsPage),
            typeof(RecordingsPage)
        );

        Routing.RegisterRoute(
            nameof(RecordingPage),
            typeof(RecordingPage)
        );

        Routing.RegisterRoute(
            nameof(CalendarPage),
            typeof(CalendarPage)
        );

        Routing.RegisterRoute(
            nameof(TaskBoardsPage),
            typeof(TaskBoardsPage)
        );

    }
}
