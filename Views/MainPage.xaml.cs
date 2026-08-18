namespace NajGravador.Views;

public partial class MainPage : ContentPage
{
    private readonly Services.LegalTaskRepository _legalTaskRepository = new();
    private readonly Services.CalendarEventRepository _calendarRepository = new();
    private readonly Services.LocalAccountRepository _accountRepository = new();
    private readonly Services.ChatRepository _chatRepository = new();

    public MainPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var currentUser = await _accountRepository.GetCurrentAsync();
        if (currentUser == null)
        {
            await Shell.Current.GoToAsync(nameof(LoginPage));
            return;
        }

        HeaderUserNameLabel.Text = currentUser.DisplayName;
        HeaderUserEmailLabel.Text = currentUser.Email;
        WelcomeHeadingLabel.Text = $"Bem-vindo, {currentUser.DisplayName}!";
        HeaderAvatarHost.Content = Services.ChatUi.CreateAvatar(
            currentUser.PhotoPath,
            currentUser.DisplayName,
            42);
        await RefreshNotificationBadgesAsync(currentUser.Id);
    }

    private async Task RefreshNotificationBadgesAsync(string currentUserId)
    {
        var legalTasksTask = _legalTaskRepository.GetAllAsync();
        var calendarEventsTask = _calendarRepository.GetAllAsync();
        var unreadChatTask = _chatRepository.GetUnreadCountAsync(currentUserId);
        await Task.WhenAll(legalTasksTask, calendarEventsTask, unreadChatTask);

        var today = DateTime.Today;
        var overdueTasks = legalTasksTask.Result.Count(task =>
            task.Status != Models.LegalTaskStatus.Completed &&
            task.FinalDeadline.Date < today);
        var tasksDueToday = legalTasksTask.Result.Count(task =>
            task.Status != Models.LegalTaskStatus.Completed &&
            (task.InternalDeadline.Date == today || task.FinalDeadline.Date == today));

        var calendarEvents = calendarEventsTask.Result;
        var todayAppointments = calendarEvents.Count(item => OccursOn(item, today));
        var overdueAppointments = calendarEvents.Count(item =>
            item.Recurrence == Models.CalendarRecurrence.None &&
            GetScheduledDate(item) < today);

        SetBadge(OverdueTasksBadge, OverdueTasksBadgeLabel, overdueTasks);
        SetBadge(TodayTasksBadge, TodayTasksBadgeLabel, tasksDueToday);
        SetBadge(TodayCalendarBadge, TodayCalendarBadgeLabel, todayAppointments);
        SetBadge(OverdueCalendarBadge, OverdueCalendarBadgeLabel, overdueAppointments);

        SetBadge(UnreadChatBadge, UnreadChatBadgeLabel, unreadChatTask.Result);
    }

    private static DateTime GetScheduledDate(Models.CalendarEvent calendarEvent) =>
        (calendarEvent.Type == Models.CalendarItemType.Task
            ? calendarEvent.DeadlineDate ?? calendarEvent.Date
            : calendarEvent.Date).Date;

    private static bool OccursOn(Models.CalendarEvent calendarEvent, DateTime date)
    {
        var start = GetScheduledDate(calendarEvent);
        date = date.Date;
        if (date < start)
        {
            return false;
        }

        return calendarEvent.Recurrence switch
        {
            Models.CalendarRecurrence.None => date == start,
            Models.CalendarRecurrence.Daily => true,
            Models.CalendarRecurrence.Weekly => (date - start).Days % 7 == 0,
            Models.CalendarRecurrence.Monthly => date.Day == start.Day,
            Models.CalendarRecurrence.Yearly => date.Month == start.Month && date.Day == start.Day,
            _ => false
        };
    }

    private static void SetBadge(Border badge, Label label, int count)
    {
        label.Text = count > 99 ? "99+" : count.ToString();
        label.FontSize = count > 99 ? 8 : 10;
        badge.WidthRequest = count > 99 ? 32 : count > 9 ? 27 : 24;
        badge.IsVisible = count > 0;
    }

    private async void OnRecordingsTapped(
        object? sender,
        TappedEventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(RecordingsPage));
    }

    private async void OnCalendarTapped(object? sender, TappedEventArgs e)
    {
        CalendarCard.InputTransparent = true;
        await CalendarCard.ScaleToAsync(0.985, 70, Easing.CubicOut);
        await CalendarCard.ScaleToAsync(1, 130, Easing.CubicOut);

        try
        {
            await Shell.Current.GoToAsync(nameof(CalendarPage));
        }
        finally
        {
            CalendarCard.InputTransparent = false;
        }
    }

    private async void OnTasksTapped(object? sender, TappedEventArgs e)
    {
        TasksCard.InputTransparent = true;
        await TasksCard.ScaleToAsync(0.985, 70, Easing.CubicOut);
        await TasksCard.ScaleToAsync(1, 130, Easing.CubicOut);

        try
        {
            await Shell.Current.GoToAsync(nameof(TaskBoardsPage));
        }
        finally
        {
            TasksCard.InputTransparent = false;
        }
    }

    private async void OnChatTapped(object? sender, TappedEventArgs e)
    {
        InternalChatCard.InputTransparent = true;
        await InternalChatCard.ScaleToAsync(0.985, 70, Easing.CubicOut);
        await InternalChatCard.ScaleToAsync(1, 130, Easing.CubicOut);
        try
        {
            await Shell.Current.GoToAsync(nameof(ChatPage));
        }
        finally
        {
            InternalChatCard.InputTransparent = false;
        }
    }

    private async void OnProfileTapped(object? sender, TappedEventArgs e) =>
        await Shell.Current.GoToAsync(nameof(ChatSettingsPage));
}
