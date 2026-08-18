using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using NajGravador.Models;
using Plugin.Maui.Audio;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Controls;
using System;


namespace NajGravador.Views;

public partial class RecordingsPage : ContentPage
{
    private readonly ObservableCollection<AudioRecording> _recordings = new();
    private readonly List<AudioRecording> _allRecordings = new();
    private IAudioPlayer? _audioPlayer;
    private Stream? _audioPlaybackStream;
    private Button? _currentPlayButton;
    private Border? _currentPlayingBorder;
    private string? _currentPlayingRecordingId;
    private const string RecordingsFileName = "recordings.json";
    // Timeline de reprodução do item ativo
    private Slider? _currentItemTimelineSlider;
    private Grid? _currentItemTimelineHost;
    private IDispatcherTimer? _itemPlaybackTimer;
    private bool _isItemTimelineDragging;
    private bool _isUpdatingItemTimeline;

    public RecordingsPage()
    {
        InitializeComponent();
        RecordingsCollection.ItemsSource = _recordings;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        RecordingsSearchBar.Text = string.Empty;
        SearchContainer.IsVisible = false;
        SearchContainer.Opacity = 0;
        await LoadRecordingsAsync();
    }

    private async Task<List<AudioRecording>> ReadRecordingsAsync()
    {
        var path = Path.Combine(FileSystem.AppDataDirectory, RecordingsFileName);
        if (!File.Exists(path))
        {
            return new List<AudioRecording>();
        }

        var json = await File.ReadAllTextAsync(path);
        return string.IsNullOrWhiteSpace(json)
            ? new List<AudioRecording>()
            : JsonSerializer.Deserialize<List<AudioRecording>>(json) ?? new List<AudioRecording>();
    }

    private async Task SaveRecordingsAsync(List<AudioRecording> recordings)
    {
        var path = Path.Combine(FileSystem.AppDataDirectory, RecordingsFileName);
        var json = JsonSerializer.Serialize(recordings);
        await File.WriteAllTextAsync(path, json);
    }

    private async Task LoadRecordingsAsync()
    {
        var recordings = await ReadRecordingsAsync();
        _allRecordings.Clear();
        _allRecordings.AddRange(recordings.OrderByDescending(r => r.CreatedAt));
        ApplySearchFilter(RecordingsSearchBar.Text);
    }

    private async void OnSearchClicked(object? sender, EventArgs e)
    {
        if (SearchContainer.IsVisible)
        {
            RecordingsSearchBar.Text = string.Empty;
            RecordingsSearchBar.Unfocus();
            await SearchContainer.FadeToAsync(0, 120, Easing.CubicIn);
            SearchContainer.IsVisible = false;
            SearchButton.TextColor = Color.FromArgb("#1E3A5F");
            return;
        }

        SearchContainer.IsVisible = true;
        SearchButton.TextColor = Color.FromArgb("#075BC7");
        await SearchContainer.FadeToAsync(1, 160, Easing.CubicOut);
        RecordingsSearchBar.Focus();
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        ApplySearchFilter(e.NewTextValue);
    }

    private void OnSearchButtonPressed(object? sender, EventArgs e)
    {
        RecordingsSearchBar.Unfocus();
    }

    private void ApplySearchFilter(string? searchText)
    {
        var prefix = searchText?.Trim() ?? string.Empty;
        var filtered = string.IsNullOrEmpty(prefix)
            ? _allRecordings
            : _allRecordings
                .Where(recording => recording.Name.Contains(
                    prefix,
                    StringComparison.CurrentCultureIgnoreCase))
                .ToList();

        _recordings.Clear();
        foreach (var recording in filtered)
        {
            _recordings.Add(recording);
        }
    }

    private async void OnBackClicked(
        object? sender,
        EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnStartRecordingClicked(
        object? sender,
        EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(RecordingPage));
    }

    private async void OnPlayRecordingClicked(
        object? sender,
        EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is AudioRecording recording)
        {
            await PlayRecordingAsync(recording, button);
        }
    }

    private async Task PlayRecordingAsync(AudioRecording recording, Button button)
    {
        try
        {
            if (_audioPlayer != null && _currentPlayingRecordingId == recording.Id)
            {
                if (_audioPlayer.IsPlaying)
                {
                    _audioPlayer.Pause();
                    _itemPlaybackTimer?.Stop();
                    button.Text = "▶";
                    return;
                }

                SeekInlinePlayer();
                _audioPlayer.Play();
                button.Text = "Ⅱ";
                _itemPlaybackTimer?.Start();
                return;
            }

            if (_audioPlayer != null)
            {
                DisposePlayback();
            }

            if (!File.Exists(recording.FilePath))
            {
                await DisplayAlertAsync("Áudio", "Arquivo de gravação não encontrado.", "OK");
                return;
            }

            DisposePlayback();
            _audioPlaybackStream = File.OpenRead(recording.FilePath);
            _audioPlayer = AudioManager.Current.CreatePlayer(_audioPlaybackStream);

            // Configura a timeline simples dentro do template do item.
            try
            {
                var parent = button.Parent;
                if (parent is Grid grid)
                {
                    var timelineHost = grid.Children
                        .OfType<Grid>()
                        .FirstOrDefault(child => child.StyleId == "InlineTimelineHost");
                    var slider = timelineHost?.Children.OfType<Slider>().FirstOrDefault();

                    if (slider != null)
                    {
                        timelineHost!.IsVisible = true;
                        slider.IsVisible = true;
                        slider.Value = 0;
                        _currentItemTimelineHost = timelineHost;
                        _currentItemTimelineSlider = slider;
                    }
                }
            }
            catch
            {
            }

            // seek to current inline ruler position if available
            try
            {
                var duration = GetPlayerDuration(_audioPlayer);
                if (_currentItemTimelineSlider != null && duration.TotalSeconds > 0)
                {
                    var startSeconds = GetInlinePlaybackStartSeconds(duration.TotalSeconds);
                    SeekPlayer(_audioPlayer, TimeSpan.FromSeconds(startSeconds));
                }
            }
            catch
            {
            }

            _audioPlayer.PlaybackEnded += OnPlaybackEnded;

            _currentPlayingRecordingId = recording.Id;
            _currentPlayButton = button;
            SetPlayingAppearance(button);
            _audioPlayer.Play();

            // start inline playback timer to update ruler
            if (_itemPlaybackTimer == null)
            {
                _itemPlaybackTimer = Dispatcher.CreateTimer();
                _itemPlaybackTimer.Interval = TimeSpan.FromSeconds(1d / 60d);
                _itemPlaybackTimer.IsRepeating = true;
                _itemPlaybackTimer.Tick += (s, ev) =>
                {
                    if (_audioPlayer == null || _currentItemTimelineSlider == null || _isItemTimelineDragging) return;
                    var duration = GetPlayerDuration(_audioPlayer);
                    var pos = GetPlayerPosition(_audioPlayer);
                    if (duration.TotalSeconds > 0)
                    {
                        var target = Math.Clamp(pos.TotalSeconds / duration.TotalSeconds, 0d, 1d);
                        var current = _currentItemTimelineSlider.Value;
                        var difference = target - current;
                        var next = Math.Abs(difference) > 0.08d
                            ? target
                            : current + difference * 0.38d;

                        _isUpdatingItemTimeline = true;
                        _currentItemTimelineSlider.Value = Math.Clamp(next, 0d, 1d);
                        _isUpdatingItemTimeline = false;
                    }
                };
            }

            _itemPlaybackTimer?.Start();
        }
        catch (Exception ex)
        {
            DisposePlayback();
            _currentPlayButton?.Text = "▶";
            _currentPlayButton = null;
            _currentPlayingRecordingId = null;
            await DisplayAlertAsync("Erro", ex.Message, "OK");
        }
    }

    private void OnPlaybackEnded(object? sender, EventArgs e)
    {
        Dispatcher.Dispatch(() =>
        {
            if (sender != _audioPlayer)
            {
                return;
            }

            DisposePlayback();
        });
    }

    private void OnItemTimelineDragStarted(object? sender, EventArgs e)
    {
        _isItemTimelineDragging = true;
    }

    private void OnItemTimelineValueChanged(object? sender, ValueChangedEventArgs e)
    {
        if (_isUpdatingItemTimeline || !_isItemTimelineDragging) return;
        SeekInlinePlayer();
    }

    private void OnItemTimelineDragCompleted(object? sender, EventArgs e)
    {
        _isItemTimelineDragging = false;
        SeekInlinePlayer();
    }

    private void SeekInlinePlayer()
    {
        if (_audioPlayer == null) return;
        var duration = GetPlayerDuration(_audioPlayer);
        if (duration.TotalSeconds <= 0) return;
        SeekPlayer(_audioPlayer, TimeSpan.FromSeconds(GetInlinePlaybackStartSeconds(duration.TotalSeconds)));
    }

    private double GetInlinePlaybackStartSeconds(double totalDuration)
    {
        if (_currentItemTimelineSlider == null || totalDuration <= 0) return 0d;
        var normalized = Math.Clamp(_currentItemTimelineSlider.Value, 0d, 1d);
        return normalized * totalDuration;
    }

    private async void OnDeleteRecordingClicked(
        object? sender,
        EventArgs e)
    {
        if (sender is ImageButton button && button.CommandParameter is AudioRecording recording)
        {
            var container = button.Parent is Grid { Parent: Border border }
                ? border
                : null;
            await DeleteRecordingAsync(recording, button, container);
        }
    }

    private async Task DeleteRecordingAsync(
        AudioRecording recording,
        ImageButton deleteButton,
        Border? container)
    {
        if (!await DisplayAlertAsync("Apagar", "Deseja excluir esta gravação?", "Sim", "Não"))
        {
            return;
        }

        deleteButton.IsEnabled = false;
        try
        {
            if (_currentPlayingRecordingId == recording.Id)
            {
                DisposePlayback();
            }

            if (container != null)
            {
                await Task.WhenAll(
                    container.FadeToAsync(0, 220, Easing.CubicIn),
                    container.TranslateToAsync(-32, 0, 220, Easing.CubicIn),
                    container.ScaleToAsync(0.96, 220, Easing.CubicIn));
            }

            var recordings = await ReadRecordingsAsync();
            recordings.RemoveAll(r => r.Id == recording.Id);
            await SaveRecordingsAsync(recordings);

            if (File.Exists(recording.FilePath))
            {
                File.Delete(recording.FilePath);
            }

            _allRecordings.RemoveAll(item => item.Id == recording.Id);
            ApplySearchFilter(RecordingsSearchBar.Text);
        }
        catch (Exception ex)
        {
            if (container != null)
            {
                container.Opacity = 1;
                container.TranslationX = 0;
                container.Scale = 1;
            }
            deleteButton.IsEnabled = true;
            await DisplayAlertAsync("Erro", $"Não foi possível excluir a gravação: {ex.Message}", "OK");
        }
    }

    private void DisposePlayback()
    {
        ResetPlayingAppearance();
        if (_audioPlayer != null)
        {
            var player = _audioPlayer;
            _audioPlayer = null;
            player.PlaybackEnded -= OnPlaybackEnded;
            try { player.Stop(); } catch { }
            player.Dispose();
        }

        _audioPlaybackStream?.Dispose();
        _audioPlaybackStream = null;
        // stop and clear inline waveform timer
        try
        {
            _itemPlaybackTimer?.Stop();
            _itemPlaybackTimer = null;
            if (_currentItemTimelineSlider != null)
            {
                _currentItemTimelineSlider.IsVisible = false;
                _currentItemTimelineSlider = null;
            }
            if (_currentItemTimelineHost != null)
            {
                _currentItemTimelineHost.IsVisible = false;
                _currentItemTimelineHost = null;
            }
            _isItemTimelineDragging = false;
            _isUpdatingItemTimeline = false;
        }
        catch
        {
        }
        _currentPlayButton = null;
        _currentPlayingRecordingId = null;
    }

    private void SetPlayingAppearance(Button button)
    {
        button.Text = "Ⅱ";
        button.BackgroundColor = Color.FromArgb("#E32636");
        if (button.Parent is Grid { Parent: Border border })
        {
            _currentPlayingBorder = border;
            border.BackgroundColor = Color.FromArgb("#FFF1F2");
            border.Stroke = Color.FromArgb("#E32636");
            border.StrokeThickness = 1.5;
        }
    }

    private void ResetPlayingAppearance()
    {
        if (_currentPlayButton != null)
        {
            _currentPlayButton.Text = "▶";
            _currentPlayButton.BackgroundColor = Color.FromArgb("#1E3A5F");
        }
        if (_currentPlayingBorder != null)
        {
            _currentPlayingBorder.BackgroundColor = Colors.White;
            _currentPlayingBorder.Stroke = Color.FromArgb("#E2E6EA");
            _currentPlayingBorder.StrokeThickness = 1;
        }
        _currentPlayingBorder = null;
    }

    private static TimeSpan GetPlayerDuration(IAudioPlayer? player)
    {
        if (player == null) return TimeSpan.Zero;
        return ReadPlayerTime(player, "Duration");
    }

    private static TimeSpan GetPlayerPosition(IAudioPlayer? player)
    {
        if (player == null) return TimeSpan.Zero;
        var current = ReadPlayerTime(player, "CurrentPosition");
        return current > TimeSpan.Zero ? current : ReadPlayerTime(player, "Position");
    }

    private static TimeSpan ReadPlayerTime(IAudioPlayer player, string propertyName)
    {
        var value = player.GetType().GetProperty(propertyName)?.GetValue(player);
        if (value is TimeSpan timeSpan) return timeSpan;
        if (value is IConvertible convertible)
        {
            try { return TimeSpan.FromSeconds(convertible.ToDouble(null)); }
            catch { }
        }
        return TimeSpan.Zero;
    }

    private static void SeekPlayer(IAudioPlayer player, TimeSpan position)
    {
        var method = player.GetType().GetMethod("Seek") ?? player.GetType().GetMethod("SeekTo");
        if (method == null) return;
        var parameters = method.GetParameters();
        if (parameters.Length == 0)
        {
            method.Invoke(player, null);
            return;
        }
        var pType = parameters[0].ParameterType;
        object arg;
        if (pType == typeof(TimeSpan)) arg = position;
        else if (pType == typeof(double) || pType == typeof(Double)) arg = position.TotalSeconds;
        else if (pType == typeof(float) || pType == typeof(Single)) arg = (float)position.TotalSeconds;
        else if (pType == typeof(int) || pType == typeof(Int32)) arg = (int)position.TotalMilliseconds;
        else if (pType == typeof(long) || pType == typeof(Int64)) arg = (long)position.TotalMilliseconds;
        else
        {
            try { arg = Convert.ChangeType(position.TotalSeconds, pType); }
            catch { arg = position.TotalSeconds; }
        }
        method.Invoke(player, new object[] { arg });
    }
}
