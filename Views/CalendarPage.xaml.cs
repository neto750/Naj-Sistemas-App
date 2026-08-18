using System.Globalization;
using Microsoft.Maui.Controls.Shapes;
using NajGravador.Models;
using NajGravador.Services;

namespace NajGravador.Views;

public partial class CalendarPage : ContentPage
{
    private enum CalendarViewMode
    {
        Month,
        Day,
        List
    }

    private static readonly CultureInfo PortugueseCulture = new("pt-BR");
    private static readonly string[] MonthNames =
    [
        "Jan", "Fev", "Mar", "Abr", "Mai", "Jun",
        "Jul", "Ago", "Set", "Out", "Nov", "Dez"
    ];

    private static readonly string[] RecurrenceOptions =
    [
        "Não se repete", "Todos os dias", "Toda semana", "Todo mês", "Todo ano"
    ];

    private static readonly string[] ReminderOptions =
    [
        "Sem lembrete", "Na hora", "5 minutos antes", "10 minutos antes",
        "30 minutos antes", "1 hora antes", "1 dia antes"
    ];

    private static readonly int?[] ReminderValues =
    [
        null, 0, 5, 10, 30, 60, 1440
    ];

    private readonly CalendarEventRepository _repository = new();
    private readonly List<CalendarEvent> _events = new();
    private DateTime _displayedMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private DateTime _selectedDate = DateTime.Today;
    private CalendarViewMode _currentView = CalendarViewMode.Month;
    private CalendarEvent? _editingEvent;
    private CalendarItemType _selectedItemType = CalendarItemType.Event;
    private string _selectedColor = "#1E66C2";
    private int _pickerYear = DateTime.Today.Year;
    private bool _isUpdatingTime;
    private bool _isCreationMenuOpen;
    private bool _isCreationMenuAnimating;
    private bool _isMonthTransitioning;
    private CancellationTokenSource? _toastCancellation;

    public CalendarPage()
    {
        InitializeComponent();
        RecurrencePicker.ItemsSource = RecurrenceOptions;
        ReminderPicker.ItemsSource = ReminderOptions;
        BuildMonthPicker();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        ResetCreationMenuVisualState();
        await LoadEventsAsync();

        Opacity = 0;
        TranslationY = 12;
        await Task.WhenAll(
            this.FadeToAsync(1, 220, Easing.CubicOut),
            this.TranslateToAsync(0, 0, 220, Easing.CubicOut));
    }

    private async Task LoadEventsAsync()
    {
        _events.Clear();
        _events.AddRange(await _repository.GetAllAsync());
        RefreshCurrentView();
    }

    private void RefreshCurrentView()
    {
        MonthYearButton.Text = _currentView == CalendarViewMode.Month
            ? $"{ToTitleCase(_displayedMonth.ToString("MMMM 'de' yyyy", PortugueseCulture))} ⌄"
            : $"{ToTitleCase(_selectedDate.ToString("MMMM 'de' yyyy", PortugueseCulture))} ⌄";

        UpdateViewButtons();
        switch (_currentView)
        {
            case CalendarViewMode.Month:
                BuildMonthView();
                break;
            case CalendarViewMode.Day:
                BuildDayView();
                break;
            case CalendarViewMode.List:
                BuildListView();
                break;
        }
    }

    private void UpdateViewButtons()
    {
        SetViewButtonState(MonthViewButton, _currentView == CalendarViewMode.Month);
        SetViewButtonState(DayViewButton, _currentView == CalendarViewMode.Day);
        SetViewButtonState(ListModeButton, _currentView == CalendarViewMode.List);
        MonthView.IsVisible = _currentView == CalendarViewMode.Month;
        DayView.IsVisible = _currentView == CalendarViewMode.Day;
        ListModeView.IsVisible = _currentView == CalendarViewMode.List;
    }

    private static void SetViewButtonState(Button button, bool isSelected)
    {
        button.BackgroundColor = isSelected ? Color.FromArgb("#1E66C2") : Colors.Transparent;
        button.TextColor = isSelected ? Colors.White : Color.FromArgb("#516071");
    }

    private void BuildMonthView()
    {
        MonthGridHost.Children.Clear();
        MonthGridHost.RowDefinitions.Clear();
        MonthGridHost.ColumnDefinitions.Clear();

        for (var column = 0; column < 7; column++)
        {
            MonthGridHost.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }

        for (var row = 0; row < 6; row++)
        {
            MonthGridHost.RowDefinitions.Add(new RowDefinition(new GridLength(82)));
        }

        var firstCellDate = _displayedMonth.AddDays(-(int)_displayedMonth.DayOfWeek);
        for (var index = 0; index < 42; index++)
        {
            var date = firstCellDate.AddDays(index);
            var cell = CreateMonthCell(date);
            Grid.SetRow(cell, index / 7);
            Grid.SetColumn(cell, index % 7);
            MonthGridHost.Children.Add(cell);
        }
    }

    private View CreateMonthCell(DateTime date)
    {
        var isCurrentMonth = date.Month == _displayedMonth.Month;
        var isToday = date.Date == DateTime.Today;
        var isSelected = date.Date == _selectedDate.Date;
        var border = new Border
        {
            Padding = new Thickness(2, 3),
            BackgroundColor = isSelected ? Color.FromArgb("#EDF5FF") : Colors.White,
            Stroke = Color.FromArgb(isSelected ? "#79A9E3" : "#E5EAF0"),
            StrokeThickness = isSelected ? 1.5 : 0.6,
            Content = new VerticalStackLayout { Spacing = 1 }
        };

        var content = (VerticalStackLayout)border.Content;
        var dayBadge = new Border
        {
            WidthRequest = 23,
            HeightRequest = 23,
            Padding = 0,
            HorizontalOptions = LayoutOptions.Center,
            BackgroundColor = isToday ? Color.FromArgb("#1E66C2") : Colors.Transparent,
            Stroke = Colors.Transparent,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Content = new Label
            {
                Text = date.Day.ToString(PortugueseCulture),
                FontSize = 11,
                FontAttributes = isToday || isSelected ? FontAttributes.Bold : FontAttributes.None,
                TextColor = isToday
                    ? Colors.White
                    : Color.FromArgb(isCurrentMonth ? "#233548" : "#A8B1BC"),
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            }
        };
        content.Children.Add(dayBadge);

        var allOccurrences = GetOccurrences(date);
        foreach (var occurrence in allOccurrences.Take(2))
        {
            var eventButton = new Button
            {
                Text = occurrence.Event.Title,
                FontSize = 9.5,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.White,
                BackgroundColor = SafeColor(occurrence.Event.ColorHex),
                CornerRadius = 4,
                Padding = new Thickness(2, 1),
                HeightRequest = 20,
                MinimumHeightRequest = 20,
                HorizontalOptions = LayoutOptions.Fill,
                LineBreakMode = LineBreakMode.TailTruncation,
                CommandParameter = occurrence.Event
            };
            eventButton.Clicked += OnEventClicked;
            content.Children.Add(eventButton);
        }

        var totalEvents = allOccurrences.Count;
        if (totalEvents > 2)
        {
            content.Children.Add(new Label
            {
                Text = $"+ {totalEvents - 2} mais",
                FontSize = 7.5,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#516071"),
                HorizontalTextAlignment = TextAlignment.Center
            });
        }

        var tap = new TapGestureRecognizer { CommandParameter = date };
        tap.Tapped += OnMonthDayTapped;
        border.GestureRecognizers.Add(tap);
        return border;
    }

    private void BuildDayView()
    {
        SelectedDayNumberLabel.Text = _selectedDate.Day.ToString(PortugueseCulture);
        SelectedDayTitleLabel.Text = ToTitleCase(_selectedDate.ToString("dddd", PortugueseCulture));
        SelectedDaySubtitleLabel.Text = _selectedDate.ToString("dd 'de' MMMM 'de' yyyy", PortugueseCulture);

        AllDayEventsHost.Children.Clear();
        var allDayOccurrences = GetOccurrences(_selectedDate)
            .Where(item => item.Event.IsAllDay)
            .ToList();

        foreach (var occurrence in allDayOccurrences)
        {
            AllDayEventsHost.Children.Add(CreateDayEventCard(occurrence.Event, "Dia inteiro"));
        }

        if (allDayOccurrences.Count > 0)
        {
            AllDayEventsHost.Margin = new Thickness(58, 0, 0, 8);
        }

        DayTimelineHost.Children.Clear();
        var timedEvents = GetOccurrences(_selectedDate)
            .Where(item => !item.Event.IsAllDay)
            .ToList();

        for (var hour = 0; hour < 24; hour++)
        {
            var row = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(new GridLength(52)),
                    new ColumnDefinition(GridLength.Star)
                },
                MinimumHeightRequest = 68
            };

            row.Children.Add(new Label
            {
                Text = $"{hour:00}:00",
                FontSize = 10,
                TextColor = Color.FromArgb("#718096"),
                HorizontalTextAlignment = TextAlignment.End,
                VerticalOptions = LayoutOptions.Start,
                Margin = new Thickness(0, 0, 7, 0)
            });

            var slot = new Border
            {
                BackgroundColor = hour == DateTime.Now.Hour && _selectedDate.Date == DateTime.Today
                    ? Color.FromArgb("#F4F8FE")
                    : Colors.White,
                Stroke = Color.FromArgb("#E2E7ED"),
                StrokeThickness = 0.7,
                Padding = new Thickness(5, 3),
                MinimumHeightRequest = 68,
                Content = new VerticalStackLayout { Spacing = 3 }
            };
            Grid.SetColumn(slot, 1);

            foreach (var occurrence in timedEvents.Where(item => (int)item.Event.StartTime.TotalHours == hour))
            {
                var time = occurrence.Event.Type == CalendarItemType.Task
                    ? "Prazo"
                    : $"{occurrence.Event.StartTime.ToString(@"hh\:mm")} – {occurrence.Event.EndTime.ToString(@"hh\:mm")}";
                ((VerticalStackLayout)slot.Content).Children.Add(CreateDayEventCard(occurrence.Event, time));
            }

            var tap = new TapGestureRecognizer { CommandParameter = hour };
            tap.Tapped += OnHourSlotTapped;
            slot.GestureRecognizers.Add(tap);
            row.Children.Add(slot);
            DayTimelineHost.Children.Add(row);
        }
    }

    private View CreateDayEventCard(CalendarEvent calendarEvent, string timeText)
    {
        var eventBorder = new Border
        {
            Padding = new Thickness(10, 6),
            BackgroundColor = SafeColor(calendarEvent.ColorHex),
            Stroke = Colors.Transparent,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Content = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                }
            }
        };

        var grid = (Grid)eventBorder.Content;
        grid.Children.Add(new Label
        {
            Text = $"{GetTypeGlyph(calendarEvent.Type)}{calendarEvent.Title}",
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            LineBreakMode = LineBreakMode.TailTruncation,
            VerticalTextAlignment = TextAlignment.Center
        });
        var timeLabel = new Label
        {
            Text = timeText,
            FontSize = 10,
            TextColor = Color.FromArgb("#F4F7FB"),
            Margin = new Thickness(8, 0, 0, 0),
            VerticalTextAlignment = TextAlignment.Center
        };
        Grid.SetColumn(timeLabel, 1);
        grid.Children.Add(timeLabel);

        var tap = new TapGestureRecognizer { CommandParameter = calendarEvent };
        tap.Tapped += OnEventTapped;
        eventBorder.GestureRecognizers.Add(tap);
        return eventBorder;
    }

    private void BuildListView()
    {
        ListModeHost.Children.Clear();
        var startDate = _selectedDate.Date;
        var occurrenceDays = _events
            .Select(calendarEvent => new
            {
                Event = calendarEvent,
                Date = GetNextOccurrenceDate(calendarEvent, startDate)
            })
            .Where(occurrence =>
                occurrence.Date.HasValue &&
                occurrence.Date.Value.Year == startDate.Year)
            .GroupBy(occurrence => occurrence.Date!.Value.Date)
            .OrderBy(group => group.Key)
            .Select(group => new
            {
                Date = group.Key,
                Events = group
                    .Select(occurrence => new CalendarOccurrence(occurrence.Event, occurrence.Date!.Value))
                    .OrderBy(occurrence => occurrence.Event.IsAllDay ? 0 : 1)
                    .ThenBy(occurrence => occurrence.Event.StartTime)
                    .ToList()
            })
            .ToList();

        if (occurrenceDays.Count == 0)
        {
            ListModeHost.Children.Add(CreateEmptyListState());
            return;
        }

        var occurrenceCount = occurrenceDays.Sum(day => day.Events.Count);
        ListModeHost.Children.Add(CreateListRangeSummary(occurrenceCount, startDate));

        foreach (var day in occurrenceDays)
        {
            var heading = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(new GridLength(48)),
                    new ColumnDefinition(GridLength.Star)
                },
                Margin = new Thickness(0, 6, 0, 0)
            };
            heading.Children.Add(new Label
            {
                Text = day.Date.Day.ToString(PortugueseCulture),
                FontSize = 24,
                FontAttributes = FontAttributes.Bold,
                TextColor = day.Date == DateTime.Today ? Color.FromArgb("#E32636") : Color.FromArgb("#1E3A5F"),
                HorizontalTextAlignment = TextAlignment.Center
            });
            var dateText = new VerticalStackLayout { Spacing = 0 };
            dateText.Children.Add(new Label
            {
                Text = ToTitleCase(day.Date.ToString("dddd", PortugueseCulture)),
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#1E3A5F")
            });
            dateText.Children.Add(new Label
            {
                Text = day.Date.ToString("MMMM 'de' yyyy", PortugueseCulture),
                FontSize = 11,
                TextColor = Color.FromArgb("#718096")
            });
            Grid.SetColumn(dateText, 1);
            heading.Children.Add(dateText);
            ListModeHost.Children.Add(heading);

            foreach (var occurrence in day.Events)
            {
                var eventBorder = new Border
                {
                    Margin = new Thickness(48, 0, 0, 0),
                    Padding = new Thickness(12, 10),
                    BackgroundColor = Colors.White,
                    Stroke = Color.FromArgb("#DDE4EC"),
                    StrokeShape = new RoundRectangle { CornerRadius = 12 },
                    Content = CreateListEventContent(occurrence.Event)
                };
                var tap = new TapGestureRecognizer { CommandParameter = occurrence.Event };
                tap.Tapped += OnEventTapped;
                eventBorder.GestureRecognizers.Add(tap);
                ListModeHost.Children.Add(eventBorder);
            }
        }
    }

    private static View CreateListRangeSummary(int occurrenceCount, DateTime startDate)
    {
        var countText = occurrenceCount == 1
            ? "1 compromisso"
            : $"{occurrenceCount} compromissos";
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };
        grid.Children.Add(new Label
        {
            Text = $"Próximos compromissos de {startDate.Year}",
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1E3A5F"),
            VerticalTextAlignment = TextAlignment.Center
        });
        var countBadge = new Border
        {
            Padding = new Thickness(9, 4),
            BackgroundColor = Color.FromArgb("#1E66C2"),
            Stroke = Colors.Transparent,
            StrokeShape = new RoundRectangle { CornerRadius = 7 },
            Content = new Label
            {
                Text = $"{countText} • {startDate:dd/MM} a 31/12",
                FontSize = 10,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.White
            }
        };
        Grid.SetColumn(countBadge, 1);
        grid.Children.Add(countBadge);
        return new Border
        {
            Padding = new Thickness(13, 9),
            BackgroundColor = Color.FromArgb("#EAF2FC"),
            Stroke = Colors.Transparent,
            StrokeShape = new RoundRectangle { CornerRadius = 9 },
            Content = grid
        };
    }

    private View CreateListEventContent(CalendarEvent calendarEvent)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(5)),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 10
        };
        grid.Children.Add(new BoxView
        {
            BackgroundColor = SafeColor(calendarEvent.ColorHex),
            CornerRadius = 3
        });
        var text = new VerticalStackLayout { Spacing = 2 };
        text.Children.Add(new Label
        {
            Text = $"{GetTypeGlyph(calendarEvent.Type)}{calendarEvent.Title}",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#24364A")
        });
        if (!string.IsNullOrWhiteSpace(calendarEvent.Location))
        {
            text.Children.Add(new Label
            {
                Text = $"⌖ {calendarEvent.Location}",
                FontSize = 11,
                TextColor = Color.FromArgb("#718096"),
                LineBreakMode = LineBreakMode.TailTruncation
            });
        }
        if (calendarEvent.Recurrence != CalendarRecurrence.None)
        {
            text.Children.Add(new Label
            {
                Text = $"↻ {RecurrenceOptions[(int)calendarEvent.Recurrence]}",
                FontSize = 10,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#6E5DC6")
            });
        }
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        var timeLabel = new Label
        {
            Text = calendarEvent.IsAllDay
                ? "Dia inteiro"
                : calendarEvent.Type == CalendarItemType.Task
                    ? "Prazo"
                : $"{calendarEvent.StartTime.ToString(@"hh\:mm")}\n{calendarEvent.EndTime.ToString(@"hh\:mm")}",
            FontSize = 11,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1E66C2"),
            HorizontalTextAlignment = TextAlignment.End,
            VerticalTextAlignment = TextAlignment.Center
        };
        Grid.SetColumn(timeLabel, 2);
        grid.Children.Add(timeLabel);
        return grid;
    }

    private View CreateEmptyListState()
    {
        var container = new VerticalStackLayout
        {
            Spacing = 10,
            Padding = new Thickness(28, 70),
            HorizontalOptions = LayoutOptions.Center
        };
        container.Children.Add(new Image
        {
            Source = "calendar_naj.svg",
            WidthRequest = 76,
            HeightRequest = 76,
            Opacity = 0.8
        });
        container.Children.Add(new Label
        {
            Text = "Nenhum compromisso restante neste ano",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1E3A5F"),
            HorizontalTextAlignment = TextAlignment.Center
        });
        container.Children.Add(new Label
        {
            Text = "Escolha outro ano ou crie um novo compromisso.",
            FontSize = 13,
            TextColor = Color.FromArgb("#718096"),
            HorizontalTextAlignment = TextAlignment.Center
        });
        return container;
    }

    private List<CalendarOccurrence> GetOccurrences(DateTime date)
    {
        return _events
            .Where(item => OccursOn(item, date.Date))
            .Select(item => new CalendarOccurrence(item, date.Date))
            .OrderBy(item => item.Event.IsAllDay ? 0 : 1)
            .ThenBy(item => item.Event.StartTime)
            .ToList();
    }

    private static DateTime? GetNextOccurrenceDate(CalendarEvent calendarEvent, DateTime fromDate)
    {
        var start = calendarEvent.Date.Date;
        var from = fromDate.Date;
        if (calendarEvent.Recurrence == CalendarRecurrence.None)
            return start >= from ? start : null;
        if (from <= start)
            return start;

        return calendarEvent.Recurrence switch
        {
            CalendarRecurrence.Daily => from,
            CalendarRecurrence.Weekly => start.AddDays(
                Math.Ceiling((from - start).TotalDays / 7d) * 7),
            CalendarRecurrence.Monthly => GetNextMonthlyOccurrence(start, from),
            CalendarRecurrence.Yearly => GetNextYearlyOccurrence(start, from),
            _ => null
        };
    }

    private static DateTime? GetNextMonthlyOccurrence(DateTime start, DateTime from)
    {
        var month = new DateTime(from.Year, from.Month, 1);
        while (true)
        {
            if (DateTime.DaysInMonth(month.Year, month.Month) >= start.Day)
            {
                var candidate = new DateTime(month.Year, month.Month, start.Day);
                if (candidate >= from && candidate >= start)
                    return candidate;
            }

            if (month.Year == DateTime.MaxValue.Year && month.Month == 12)
                return null;
            month = month.AddMonths(1);
        }
    }

    private static DateTime? GetNextYearlyOccurrence(DateTime start, DateTime from)
    {
        for (var year = Math.Max(start.Year, from.Year); year <= DateTime.MaxValue.Year; year++)
        {
            if (DateTime.DaysInMonth(year, start.Month) < start.Day)
                continue;
            var candidate = new DateTime(year, start.Month, start.Day);
            if (candidate >= from && candidate >= start)
                return candidate;
        }
        return null;
    }

    private static bool OccursOn(CalendarEvent calendarEvent, DateTime date)
    {
        var start = calendarEvent.Date.Date;
        if (date < start)
        {
            return false;
        }

        return calendarEvent.Recurrence switch
        {
            CalendarRecurrence.None => date == start,
            CalendarRecurrence.Daily => true,
            CalendarRecurrence.Weekly => (date - start).Days % 7 == 0,
            CalendarRecurrence.Monthly => date.Day == start.Day,
            CalendarRecurrence.Yearly => date.Month == start.Month && date.Day == start.Day,
            _ => false
        };
    }

    private void BuildMonthPicker()
    {
        MonthPickerGrid.Children.Clear();
        for (var month = 1; month <= 12; month++)
        {
            var button = new Button
            {
                Text = MonthNames[month - 1],
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#1E3A5F"),
                BackgroundColor = Color.FromArgb("#F2F6FA"),
                CornerRadius = 10,
                Padding = new Thickness(8, 9),
                CommandParameter = month
            };
            button.Clicked += OnPickerMonthClicked;
            Grid.SetRow(button, (month - 1) / 4);
            Grid.SetColumn(button, (month - 1) % 4);
            MonthPickerGrid.Children.Add(button);
        }
    }

    private async Task OpenMonthPickerAsync()
    {
        _pickerYear = (_currentView == CalendarViewMode.Month ? _displayedMonth : _selectedDate).Year;
        PickerYearLabel.Text = _pickerYear.ToString(PortugueseCulture);
        UpdateMonthPickerSelection();
        MonthPickerOverlay.IsVisible = true;
        MonthPickerOverlay.Opacity = 0;
        MonthPickerCard.Scale = 0.93;
        await Task.WhenAll(
            MonthPickerOverlay.FadeToAsync(1, 160, Easing.CubicOut),
            MonthPickerCard.ScaleToAsync(1, 200, Easing.CubicOut));
    }

    private void UpdateMonthPickerSelection()
    {
        var activeMonth = (_currentView == CalendarViewMode.Month ? _displayedMonth : _selectedDate).Month;
        foreach (var button in MonthPickerGrid.Children.OfType<Button>())
        {
            var month = (int)button.CommandParameter;
            var selected = month == activeMonth && _pickerYear == (_currentView == CalendarViewMode.Month ? _displayedMonth.Year : _selectedDate.Year);
            button.BackgroundColor = selected ? Color.FromArgb("#1E66C2") : Color.FromArgb("#F2F6FA");
            button.TextColor = selected ? Colors.White : Color.FromArgb("#1E3A5F");
        }
    }

    private async Task CloseMonthPickerAsync()
    {
        if (!MonthPickerOverlay.IsVisible)
        {
            return;
        }

        await MonthPickerOverlay.FadeToAsync(0, 120, Easing.CubicIn);
        MonthPickerOverlay.IsVisible = false;
    }

    private async Task OpenEditorAsync(
        DateTime date,
        TimeSpan? startTime = null,
        CalendarEvent? calendarEvent = null,
        bool dateIsContextual = false,
        CalendarItemType itemType = CalendarItemType.Event)
    {
        _editingEvent = calendarEvent;
        _selectedItemType = calendarEvent?.Type ?? itemType;
        var isTask = _selectedItemType == CalendarItemType.Task;
        var isBirthday = _selectedItemType == CalendarItemType.Birthday;
        TitleValidationLabel.IsVisible = false;
        TimeValidationLabel.IsVisible = false;
        DeleteEventButton.IsVisible = calendarEvent != null;
        EditorHeadingLabel.Text = GetEditorHeading(_selectedItemType, calendarEvent != null);
        TitleFieldLabel.Text = isBirthday ? "Nome da pessoa *" : isTask ? "Tarefa *" : "Título *";
        TitleEntry.Placeholder = isBirthday ? "Adicionar nome" : isTask ? "Adicionar tarefa" : "Adicionar título";
        DateFieldLabel.Text = isBirthday ? "Data do aniversário" : "Data";
        DateAndAllDayGrid.IsVisible = !isTask && !dateIsContextual;
        AllDayField.IsVisible = !isBirthday;
        TaskDeadlineGrid.IsVisible = isTask;
        RecurrenceReminderGrid.IsVisible = !isBirthday;
        EditorSubtitleLabel.Text = dateIsContextual
            ? ToTitleCase(date.ToString("dddd, dd 'de' MMMM", PortugueseCulture))
            : calendarEvent == null
                ? GetCreationSubtitle(_selectedItemType)
                : "Altere os detalhes do compromisso";

        if (calendarEvent == null)
        {
            var start = startTime ?? RoundToNextHour(DateTime.Now.TimeOfDay);
            TitleEntry.Text = string.Empty;
            EventDatePicker.Date = date.Date;
            StartTimePicker.Time = start;
            EndTimePicker.Time = start.Add(TimeSpan.FromHours(1));
            TaskDeadlineDatePicker.Date = date.Date;
            TaskDeadlineTimePicker.Time = startTime ?? TimeSpan.FromHours(18);
            AllDaySwitch.IsToggled = isBirthday;
            LocationEntry.Text = string.Empty;
            DescriptionEditor.Text = string.Empty;
            RecurrencePicker.SelectedIndex = isBirthday ? (int)CalendarRecurrence.Yearly : 0;
            ReminderPicker.SelectedIndex = isBirthday ? 6 : 3;
            _selectedColor = _selectedItemType switch
            {
                CalendarItemType.Task => "#16856B",
                CalendarItemType.Birthday => "#E32636",
                _ => "#1E66C2"
            };
        }
        else
        {
            TitleEntry.Text = calendarEvent.Title;
            EventDatePicker.Date = date.Date;
            StartTimePicker.Time = calendarEvent.StartTime;
            EndTimePicker.Time = calendarEvent.EndTime;
            TaskDeadlineDatePicker.Date = calendarEvent.DeadlineDate ?? calendarEvent.Date;
            TaskDeadlineTimePicker.Time = calendarEvent.DeadlineTime ?? calendarEvent.StartTime;
            AllDaySwitch.IsToggled = calendarEvent.IsAllDay;
            LocationEntry.Text = calendarEvent.Location;
            DescriptionEditor.Text = calendarEvent.Description;
            RecurrencePicker.SelectedIndex = (int)calendarEvent.Recurrence;
            ReminderPicker.SelectedIndex = Array.IndexOf(ReminderValues, calendarEvent.ReminderMinutes);
            if (ReminderPicker.SelectedIndex < 0)
            {
                ReminderPicker.SelectedIndex = 0;
            }
            _selectedColor = calendarEvent.ColorHex;
        }

        TimeFieldsGrid.IsVisible = !isTask && !isBirthday && !AllDaySwitch.IsToggled;
        UpdateColorSelection();
        EventEditorOverlay.IsVisible = true;
        EventEditorOverlay.Opacity = 0;
        EventEditorCard.TranslationY = 28;
        EventEditorCard.Scale = 0.98;
        await Task.WhenAll(
            EventEditorOverlay.FadeToAsync(1, 170, Easing.CubicOut),
            EventEditorCard.TranslateToAsync(0, 0, 240, Easing.CubicOut),
            EventEditorCard.ScaleToAsync(1, 240, Easing.CubicOut));
        TitleEntry.Focus();
    }

    private async Task CloseEditorAsync()
    {
        if (!EventEditorOverlay.IsVisible)
        {
            return;
        }

        TitleEntry.Unfocus();
        await Task.WhenAll(
            EventEditorOverlay.FadeToAsync(0, 140, Easing.CubicIn),
            EventEditorCard.TranslateToAsync(0, 18, 140, Easing.CubicIn));
        EventEditorOverlay.IsVisible = false;
        _editingEvent = null;
    }

    private void UpdateColorSelection()
    {
        foreach (var button in EventEditorCard.GetVisualTreeDescendants().OfType<Button>()
                     .Where(item => item.StyleId?.StartsWith('#') == true))
        {
            button.Text = string.Equals(button.StyleId, _selectedColor, StringComparison.OrdinalIgnoreCase) ? "✓" : string.Empty;
            button.BorderColor = string.Equals(button.StyleId, _selectedColor, StringComparison.OrdinalIgnoreCase)
                ? Color.FromArgb("#1E3A5F")
                : Colors.Transparent;
            button.BorderWidth = string.Equals(button.StyleId, _selectedColor, StringComparison.OrdinalIgnoreCase) ? 3 : 0;
        }
    }

    private async Task SaveEditorAsync()
    {
        var title = TitleEntry.Text?.Trim() ?? string.Empty;
        var isTimedEvent = _selectedItemType == CalendarItemType.Event && !AllDaySwitch.IsToggled;
        TitleValidationLabel.IsVisible = string.IsNullOrWhiteSpace(title);
        TimeValidationLabel.IsVisible = isTimedEvent && EndTimePicker.Time <= StartTimePicker.Time;
        if (TitleValidationLabel.IsVisible || TimeValidationLabel.IsVisible)
        {
            if (TitleValidationLabel.IsVisible)
            {
                TitleEntry.Focus();
            }
            return;
        }

        var calendarEvent = _editingEvent ?? new CalendarEvent();
        calendarEvent.Title = title;
        calendarEvent.Type = _selectedItemType;
        var wasEditing = _editingEvent != null;
        if (_selectedItemType == CalendarItemType.Task)
        {
            var deadlineDate = (TaskDeadlineDatePicker.Date ?? _selectedDate).Date;
            var deadlineTime = TaskDeadlineTimePicker.Time ?? TimeSpan.FromHours(18);
            calendarEvent.Date = deadlineDate;
            calendarEvent.DeadlineDate = deadlineDate;
            calendarEvent.DeadlineTime = deadlineTime;
            calendarEvent.StartTime = deadlineTime;
            calendarEvent.EndTime = deadlineTime.Add(TimeSpan.FromMinutes(30));
            calendarEvent.IsAllDay = false;
        }
        else
        {
            calendarEvent.Date = (EventDatePicker.Date ?? _selectedDate).Date;
            calendarEvent.DeadlineDate = null;
            calendarEvent.DeadlineTime = null;
            calendarEvent.StartTime = StartTimePicker.Time ?? TimeSpan.FromHours(9);
            calendarEvent.EndTime = EndTimePicker.Time ?? TimeSpan.FromHours(10);
            calendarEvent.IsAllDay = _selectedItemType == CalendarItemType.Birthday || AllDaySwitch.IsToggled;
        }
        calendarEvent.Location = LocationEntry.Text?.Trim() ?? string.Empty;
        calendarEvent.Description = DescriptionEditor.Text?.Trim() ?? string.Empty;
        calendarEvent.Recurrence = _selectedItemType == CalendarItemType.Birthday
            ? CalendarRecurrence.Yearly
            : (CalendarRecurrence)Math.Max(0, RecurrencePicker.SelectedIndex);
        calendarEvent.ReminderMinutes = ReminderPicker.SelectedIndex >= 0
            ? ReminderValues[ReminderPicker.SelectedIndex]
            : null;
        calendarEvent.ColorHex = _selectedColor;

        await _repository.SaveAsync(calendarEvent);
        _selectedDate = calendarEvent.Date;
        _displayedMonth = new DateTime(calendarEvent.Date.Year, calendarEvent.Date.Month, 1);
        await CloseEditorAsync();
        await LoadEventsAsync();
        await ShowToastAsync(GetSaveConfirmation(_selectedItemType, wasEditing));
    }

    private async Task DeleteEditingEventAsync()
    {
        if (_editingEvent == null)
        {
            return;
        }

        var shouldDelete = await DisplayAlertAsync(
            $"Excluir {GetItemName(_editingEvent.Type).ToLower(PortugueseCulture)}",
            $"Deseja excluir “{_editingEvent.Title}”?",
            "Excluir",
            "Cancelar");
        if (!shouldDelete)
        {
            return;
        }

        await _repository.DeleteAsync(_editingEvent.Id);
        await CloseEditorAsync();
        await LoadEventsAsync();
        await ShowToastAsync("Compromisso excluído");
    }

    private async Task ShowToastAsync(string message)
    {
        _toastCancellation?.Cancel();
        _toastCancellation = new CancellationTokenSource();
        var token = _toastCancellation.Token;
        ToastLabel.Text = message;
        ToastBorder.IsVisible = true;
        ToastBorder.Opacity = 0;
        ToastBorder.TranslationY = 12;
        await Task.WhenAll(
            ToastBorder.FadeToAsync(1, 160, Easing.CubicOut),
            ToastBorder.TranslateToAsync(0, 0, 160, Easing.CubicOut));

        try
        {
            await Task.Delay(2100, token);
            await ToastBorder.FadeToAsync(0, 180, Easing.CubicIn);
            ToastBorder.IsVisible = false;
        }
        catch (TaskCanceledException)
        {
        }
    }

    private async Task AnimateViewChangeAsync(View visibleView)
    {
        visibleView.Opacity = 0;
        visibleView.TranslationX = 12;
        await Task.WhenAll(
            visibleView.FadeToAsync(1, 180, Easing.CubicOut),
            visibleView.TranslateToAsync(0, 0, 180, Easing.CubicOut));
    }

    private void ChangeView(CalendarViewMode viewMode)
    {
        _currentView = viewMode;
        if (viewMode == CalendarViewMode.Month)
        {
            _displayedMonth = new DateTime(_selectedDate.Year, _selectedDate.Month, 1);
        }
        RefreshCurrentView();
        _ = AnimateViewChangeAsync(viewMode switch
        {
            CalendarViewMode.Month => MonthView,
            CalendarViewMode.Day => DayView,
            _ => ListModeView
        });
    }

    private async Task ChangeDisplayedMonthAsync(int monthOffset)
    {
        if (_isMonthTransitioning || monthOffset == 0)
        {
            return;
        }

        _isMonthTransitioning = true;
        var exitDirection = monthOffset > 0 ? -1d : 1d;
        try
        {
            await Task.WhenAll(
                MonthView.TranslateToAsync(28 * exitDirection, 0, 105, Easing.CubicIn),
                MonthView.FadeToAsync(0.35, 105, Easing.CubicIn));

            _displayedMonth = _displayedMonth.AddMonths(monthOffset);
            _selectedDate = _displayedMonth;
            RefreshCurrentView();

            MonthView.TranslationX = -28 * exitDirection;
            MonthView.Opacity = 0.35;
            await Task.WhenAll(
                MonthView.TranslateToAsync(0, 0, 190, Easing.CubicOut),
                MonthView.FadeToAsync(1, 190, Easing.CubicOut));
        }
        finally
        {
            MonthView.TranslationX = 0;
            MonthView.Opacity = 1;
            _isMonthTransitioning = false;
        }
    }

    private async Task ToggleCreationMenuAsync()
    {
        if (_isCreationMenuAnimating)
        {
            return;
        }

        _isCreationMenuAnimating = true;
        try
        {
            if (!_isCreationMenuOpen)
            {
                _isCreationMenuOpen = true;
                CreationMenuScrim.IsVisible = true;
                CreationActionsHost.IsVisible = true;
                PrepareCreationAction(TaskAction);
                PrepareCreationAction(EventAction);
                PrepareCreationAction(BirthdayAction);

                await Task.WhenAll(
                    CreationMenuScrim.FadeToAsync(1, 180, Easing.CubicOut),
                    FloatingCreateGlyph.RotateToAsync(45, 190, Easing.CubicOut),
                    FloatingCreateButton.ScaleToAsync(1.06, 110, Easing.CubicOut));
                await FloatingCreateButton.ScaleToAsync(1, 90, Easing.CubicOut);

                await AnimateCreationActionInAsync(TaskAction);
                await AnimateCreationActionInAsync(EventAction);
                await AnimateCreationActionInAsync(BirthdayAction);
                return;
            }

            await CloseCreationMenuAsync();
        }
        finally
        {
            _isCreationMenuAnimating = false;
        }
    }

    private async Task CloseCreationMenuAsync()
    {
        if (!_isCreationMenuOpen)
        {
            return;
        }

        await Task.WhenAll(
            CreationMenuScrim.FadeToAsync(0, 150, Easing.CubicIn),
            CreationActionsHost.FadeToAsync(0, 130, Easing.CubicIn),
            FloatingCreateGlyph.RotateToAsync(0, 180, Easing.CubicOut));
        ResetCreationMenuVisualState();
    }

    private static void PrepareCreationAction(VisualElement action)
    {
        action.Opacity = 0;
        action.TranslationY = 22;
        action.Scale = 0.94;
    }

    private static Task AnimateCreationActionInAsync(VisualElement action)
    {
        return Task.WhenAll(
            action.FadeToAsync(1, 105, Easing.CubicOut),
            action.TranslateToAsync(0, 0, 150, Easing.CubicOut),
            action.ScaleToAsync(1, 150, Easing.CubicOut));
    }

    private void ResetCreationMenuVisualState()
    {
        _isCreationMenuOpen = false;
        CreationMenuScrim.IsVisible = false;
        CreationMenuScrim.Opacity = 0;
        CreationActionsHost.IsVisible = false;
        CreationActionsHost.Opacity = 1;
        FloatingCreateGlyph.Rotation = 0;
        FloatingCreateButton.Scale = 1;
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");

    private void OnTodayClicked(object? sender, EventArgs e)
    {
        _selectedDate = DateTime.Today;
        _displayedMonth = new DateTime(_selectedDate.Year, _selectedDate.Month, 1);
        RefreshCurrentView();
    }

    private async void OnPreviousClicked(object? sender, EventArgs e)
    {
        if (_currentView == CalendarViewMode.Month)
        {
            await ChangeDisplayedMonthAsync(-1);
            return;
        }
        else
        {
            _selectedDate = _currentView == CalendarViewMode.Day
                ? _selectedDate.AddDays(-1)
                : _selectedDate.AddMonths(-1);
            _displayedMonth = new DateTime(_selectedDate.Year, _selectedDate.Month, 1);
        }
        RefreshCurrentView();
    }

    private async void OnNextClicked(object? sender, EventArgs e)
    {
        if (_currentView == CalendarViewMode.Month)
        {
            await ChangeDisplayedMonthAsync(1);
            return;
        }
        else
        {
            _selectedDate = _currentView == CalendarViewMode.Day
                ? _selectedDate.AddDays(1)
                : _selectedDate.AddMonths(1);
            _displayedMonth = new DateTime(_selectedDate.Year, _selectedDate.Month, 1);
        }
        RefreshCurrentView();
    }

    private async void OnMonthYearClicked(object? sender, EventArgs e) => await OpenMonthPickerAsync();
    private void OnMonthViewClicked(object? sender, EventArgs e) => ChangeView(CalendarViewMode.Month);
    private void OnDayViewClicked(object? sender, EventArgs e) => ChangeView(CalendarViewMode.Day);
    private void OnListViewClicked(object? sender, EventArgs e) => ChangeView(CalendarViewMode.List);

    private async void OnMonthSwipedLeft(object? sender, SwipedEventArgs e) => await ChangeDisplayedMonthAsync(1);
    private async void OnMonthSwipedRight(object? sender, SwipedEventArgs e) => await ChangeDisplayedMonthAsync(-1);

    private void OnMonthDayTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not DateTime date)
        {
            return;
        }
        _selectedDate = date;
        _displayedMonth = new DateTime(date.Year, date.Month, 1);
        ChangeView(CalendarViewMode.Day);
    }

    private async void OnHourSlotTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is int hour)
        {
            await OpenEditorAsync(
                _selectedDate,
                TimeSpan.FromHours(hour),
                dateIsContextual: true);
        }
    }

    private async void OnEventClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: CalendarEvent calendarEvent })
        {
            await OpenEditorAsync(calendarEvent.Date, calendarEvent.StartTime, calendarEvent);
        }
    }

    private async void OnEventTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is CalendarEvent calendarEvent)
        {
            await OpenEditorAsync(calendarEvent.Date, calendarEvent.StartTime, calendarEvent);
        }
    }

    private async void OnFloatingCreateTapped(object? sender, TappedEventArgs e) => await ToggleCreationMenuAsync();
    private async void OnCreationScrimTapped(object? sender, TappedEventArgs e) => await ToggleCreationMenuAsync();

    private async void OnCreateEventTapped(object? sender, TappedEventArgs e)
    {
        await CloseCreationMenuAsync();
        await OpenEditorAsync(_selectedDate, itemType: CalendarItemType.Event);
    }

    private async void OnCreateTaskTapped(object? sender, TappedEventArgs e)
    {
        await CloseCreationMenuAsync();
        await OpenEditorAsync(_selectedDate, itemType: CalendarItemType.Task);
    }

    private async void OnCreateBirthdayTapped(object? sender, TappedEventArgs e)
    {
        await CloseCreationMenuAsync();
        await OpenEditorAsync(_selectedDate, itemType: CalendarItemType.Birthday);
    }

    private void OnPickerPreviousYearClicked(object? sender, EventArgs e)
    {
        _pickerYear--;
        PickerYearLabel.Text = _pickerYear.ToString(PortugueseCulture);
        UpdateMonthPickerSelection();
    }

    private void OnPickerNextYearClicked(object? sender, EventArgs e)
    {
        _pickerYear++;
        PickerYearLabel.Text = _pickerYear.ToString(PortugueseCulture);
        UpdateMonthPickerSelection();
    }

    private async void OnPickerMonthClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: int month })
        {
            return;
        }

        _displayedMonth = new DateTime(_pickerYear, month, 1);
        var desiredDay = Math.Min(_selectedDate.Day, DateTime.DaysInMonth(_pickerYear, month));
        _selectedDate = new DateTime(_pickerYear, month, desiredDay);
        await CloseMonthPickerAsync();
        RefreshCurrentView();
    }

    private async void OnCloseMonthPickerClicked(object? sender, EventArgs e) => await CloseMonthPickerAsync();
    private async void OnMonthPickerBackdropTapped(object? sender, TappedEventArgs e) => await CloseMonthPickerAsync();
    private void OnOverlayCardTapped(object? sender, TappedEventArgs e) { }

    private async void OnCloseEditorClicked(object? sender, EventArgs e) => await CloseEditorAsync();
    private async void OnEditorBackdropTapped(object? sender, TappedEventArgs e) => await CloseEditorAsync();
    private async void OnSaveEventClicked(object? sender, EventArgs e) => await SaveEditorAsync();
    private async void OnDeleteEventClicked(object? sender, EventArgs e) => await DeleteEditingEventAsync();

    private void OnAllDayToggled(object? sender, ToggledEventArgs e)
    {
        TimeFieldsGrid.IsVisible = _selectedItemType == CalendarItemType.Event && !e.Value;
        TimeValidationLabel.IsVisible = false;
    }

    private void OnEventDateSelected(object? sender, DateChangedEventArgs e)
    {
        TimeValidationLabel.IsVisible = false;
    }

    private void OnStartTimePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_isUpdatingTime || e.PropertyName != TimePicker.TimeProperty.PropertyName)
        {
            return;
        }

        _isUpdatingTime = true;
        var start = StartTimePicker.Time ?? TimeSpan.FromHours(9);
        var end = EndTimePicker.Time ?? TimeSpan.FromHours(10);
        if (end <= start)
        {
            EndTimePicker.Time = start.Add(TimeSpan.FromHours(1));
        }
        _isUpdatingTime = false;
        TimeValidationLabel.IsVisible = false;
    }

    private void OnColorClicked(object? sender, EventArgs e)
    {
        if (sender is Button { StyleId: not null } button)
        {
            _selectedColor = button.StyleId;
            UpdateColorSelection();
        }
    }

    private static TimeSpan RoundToNextHour(TimeSpan time)
    {
        var hour = time.Minutes == 0 ? time.Hours : time.Hours + 1;
        return TimeSpan.FromHours(hour % 24);
    }

    private static string GetTypeGlyph(CalendarItemType itemType) => itemType switch
    {
        CalendarItemType.Task => "✓ ",
        CalendarItemType.Birthday => "🎂 ",
        _ => string.Empty
    };

    private static string GetItemName(CalendarItemType itemType) => itemType switch
    {
        CalendarItemType.Task => "Tarefa",
        CalendarItemType.Birthday => "Aniversário",
        _ => "Evento"
    };

    private static string GetSaveConfirmation(CalendarItemType itemType, bool wasEditing)
    {
        return (itemType, wasEditing) switch
        {
            (CalendarItemType.Task, true) => "Tarefa atualizada",
            (CalendarItemType.Task, false) => "Tarefa criada",
            (CalendarItemType.Birthday, true) => "Aniversário atualizado",
            (CalendarItemType.Birthday, false) => "Aniversário criado",
            (_, true) => "Evento atualizado",
            _ => "Evento criado"
        };
    }

    private static string GetEditorHeading(CalendarItemType itemType, bool isEditing)
    {
        var action = isEditing ? "Editar" : "Novo";
        return itemType switch
        {
            CalendarItemType.Task => isEditing ? "Editar tarefa" : "Nova tarefa",
            CalendarItemType.Birthday => isEditing ? "Editar aniversário" : "Novo aniversário",
            _ => $"{action} evento"
        };
    }

    private static string GetCreationSubtitle(CalendarItemType itemType) => itemType switch
    {
        CalendarItemType.Task => "Defina a tarefa e o prazo para concluí-la",
        CalendarItemType.Birthday => "Adicione a data para lembrar todos os anos",
        _ => "Adicione data, horário e detalhes do evento"
    };

    private static string ToTitleCase(string text) => PortugueseCulture.TextInfo.ToTitleCase(text);

    private static Color SafeColor(string? hex)
    {
        try
        {
            return Color.FromArgb(string.IsNullOrWhiteSpace(hex) ? "#1E66C2" : hex);
        }
        catch
        {
            return Color.FromArgb("#1E66C2");
        }
    }

    private sealed record CalendarOccurrence(CalendarEvent Event, DateTime Date);
}
