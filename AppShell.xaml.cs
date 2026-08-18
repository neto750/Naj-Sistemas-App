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

        Routing.RegisterRoute(nameof(LoginPage), typeof(LoginPage));
        Routing.RegisterRoute(nameof(RegisterPage), typeof(RegisterPage));
        Routing.RegisterRoute(nameof(ChatPage), typeof(ChatPage));
        Routing.RegisterRoute(nameof(ChatConversationPage), typeof(ChatConversationPage));
        Routing.RegisterRoute(nameof(ChatContactsPage), typeof(ChatContactsPage));
        Routing.RegisterRoute(nameof(NewContactPage), typeof(NewContactPage));
        Routing.RegisterRoute(nameof(NewGroupPage), typeof(NewGroupPage));
        Routing.RegisterRoute(nameof(NewChatListPage), typeof(NewChatListPage));
        Routing.RegisterRoute(nameof(ChatSettingsPage), typeof(ChatSettingsPage));
        Routing.RegisterRoute(nameof(FavoriteMessagesPage), typeof(FavoriteMessagesPage));

    }
}
