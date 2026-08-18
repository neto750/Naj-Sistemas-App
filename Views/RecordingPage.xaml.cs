using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Plugin.Maui.Audio;
using NajGravador.Models;
using NajGravador.Services;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System.IO;

namespace NajGravador.Views;

[QueryProperty(nameof(PlaybackFilePath), "playbackPath")]
public partial class RecordingPage : ContentPage
{
    private readonly AudioRecorderService _audioRecorderService;
    private readonly WaveformDrawable _waveformDrawable = new();
    private float _smoothedVoiceLevel;
    private double _waveformPanStartSliderValue = 0;
    private IDispatcherTimer? _rulerTimer;
    private float _rulerTarget = 0f;
    private readonly IDispatcherTimer _timer;
    private readonly IDispatcherTimer _visualizerTimer;
    // Visual / easing configuration
    private const double VisualizerIntervalMs = 40;
    private const float RulerEaseFactor = 0.24f;
    private readonly IDispatcherTimer _playbackTimer;
    private IAudioPlayer? _audioPlayer;
    private Stream? _audioPlaybackStream;
    private readonly List<string> _recordingSegments = new();
    private string? _currentFilePath;
    private bool _isPaused;
    private bool _isTimelineDragging;
    private bool _isPlaybackUpdatingTimeline;
    private bool _isTransitioning;
    private bool _isClosing;
    private TimeSpan _elapsedTime = TimeSpan.Zero;
    private const string RecordingsFileName = "recordings.json";
    private const string PreviewFileName = "recording_preview.wav";
    private string? _playbackFilePath;

    public RecordingPage()
    {
        InitializeComponent();

        _audioRecorderService = new AudioRecorderService(AudioManager.Current);

        WaveformView.Drawable = _waveformDrawable;

        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.IsRepeating = true;
        _timer.Tick += OnTimerTick;

        _visualizerTimer = Dispatcher.CreateTimer();
        _visualizerTimer.Interval = TimeSpan.FromMilliseconds(VisualizerIntervalMs);
        _visualizerTimer.IsRepeating = true;
        _visualizerTimer.Tick += OnVisualizerTick;

        _playbackTimer = Dispatcher.CreateTimer();
        _playbackTimer.Interval = TimeSpan.FromMilliseconds(80);
        _playbackTimer.IsRepeating = true;
        _playbackTimer.Tick += OnPlaybackTimerTick;
    }

    private async Task<bool> RequestMicrophonePermissionAsync()
    {
        PermissionStatus status =
            await Permissions.RequestAsync<Permissions.Microphone>();

        return status == PermissionStatus.Granted;
    }

    private void OnTimerTick(
        object? sender,
        EventArgs e)
    {
        if (!_isPaused)
        {
            _elapsedTime =
                _elapsedTime.Add(
                    TimeSpan.FromSeconds(1));
            TimerLabel.Text =
                _elapsedTime.ToString(@"hh\:mm\:ss");

            // update ruler/timeline max while recording so user can scrub
            TimelineSlider.Maximum = Math.Max(1, _elapsedTime.TotalSeconds);
            // keep ruler at end while recording unless something is playing
            if (!(_audioPlayer?.IsPlaying == true))
            {
                TimelineSlider.Value = _elapsedTime.TotalSeconds;
            }
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        bool hasPermission =
            await RequestMicrophonePermissionAsync();

        if (!hasPermission)
        {
            RecordingStatusLabel.Text =
                "Permissão do microfone negada";
            RecordingStatusLabel.TextColor =
                Color.FromArgb("#C62828");
            return;
        }

        if (!_audioRecorderService.IsRecording)
        {
            _currentFilePath = Path.Combine(
                FileSystem.AppDataDirectory,
                $"recording_{DateTime.Now:yyyyMMddHHmmss}.wav");

            await _audioRecorderService.StartRecordingAsync(_currentFilePath);
            _recordingSegments.Add(_currentFilePath);
            _isPaused = false;
            PositionRulerAtRecordingEnd();
            _timer.Start();
            _visualizerTimer.Start();
            RecordingStatusLabel.Text = "Gravando...";
            RecordingStatusLabel.TextColor = Color.FromArgb("#C62828");
            // entry animation: slide up + fade
            this.Opacity = 0;
            this.TranslationY = 20;
            await Task.WhenAll(this.FadeToAsync(1, 320, Easing.CubicOut), this.TranslateToAsync(0, 0, 320, Easing.CubicOut));
        }
    }

    // When navigated to for playback from RecordingsPage
    public string? PlaybackFilePath
    {
        get => _playbackFilePath;
        set
        {
            _playbackFilePath = value;
            if (!string.IsNullOrEmpty(value))
            {
                Dispatcher.Dispatch(async () => await StartPlaybackFileAsync(value));
            }
        }
    }

    private async Task StartPlaybackFileAsync(string filePath)
    {
        try
        {
            // stop any active recording
            if (_audioRecorderService.IsRecording)
            {
                await StopCurrentRecordingAsync();
            }

            if (!File.Exists(filePath)) return;

            DisposePlayback();
            _audioPlaybackStream = File.OpenRead(filePath);
            _audioPlayer = AudioManager.Current.CreatePlayer(_audioPlaybackStream);
            // if starting playback of an external file, try to load its waveform samples
            var fileSamples = WaveformHelper.GenerateSamplesFromWav(filePath, _waveformDrawable.Samples.Count);
            if (fileSamples != null && fileSamples.Count == _waveformDrawable.Samples.Count)
            {
                _waveformDrawable.Samples.Clear();
                _waveformDrawable.Samples.AddRange(fileSamples);
                WaveformView.Invalidate();
            }

            _audioPlayer.PlaybackEnded += OnPlaybackEnded;
            InitializePlaybackUI();
            SeekPlayer(_audioPlayer, TimeSpan.Zero);
            _audioPlayer.Play();
            SetListenButtonState(isPlaying: true);
        }
        catch
        {
        }
    }

    private async void OnPauseClicked(
        object? sender,
        EventArgs e)
    {
        if (_isTransitioning) return;
        _isTransitioning = true;
        try
        {
            if (!_isPaused)
            {
                await PauseRecordingAsync();
                return;
            }

            await ResumeRecordingAsync();
        }
        finally
        {
            _isTransitioning = false;
        }
    }

    private async Task PauseRecordingAsync()
    {
        await WaveformHost.FadeToAsync(0.72, 80, Easing.CubicOut);
        _isPaused = true;
        _timer.Stop();

        if (_audioRecorderService.IsRecording)
        {
            await _audioRecorderService.StopRecordingAsync();
        }

        // Ao entrar no modo de pré-escuta, a régua começa no início e passa
        // a representar exclusivamente o ponto escolhido para reprodução.
        TimelineSlider.Maximum = Math.Max(1d, _elapsedTime.TotalSeconds);
        TimelineSlider.Value = 0d;
        TimelineSlider.IsEnabled = true;
        TotalDurationLabel.Text = _elapsedTime.ToString(@"mm\:ss");
        UpdateRulerFromSeconds(0d);

        await AnimatePauseButtonStateAsync(isPaused: true);
        PauseLabel.Text = "Retomar";
        RecordingStatusLabel.Text = "Gravação pausada";
        RecordingStatusLabel.TextColor = Color.FromArgb("#1E3A5F");
        await WaveformHost.FadeToAsync(1, 160, Easing.CubicOut);
    }

    private async Task ResumeRecordingAsync()
    {
        await WaveformHost.FadeToAsync(0.72, 80, Easing.CubicOut);
        _isPaused = false;
        _timer.Start();

        var nextSegment = Path.Combine(
            FileSystem.AppDataDirectory,
            $"recording_{DateTime.Now:yyyyMMddHHmmss}_part{_recordingSegments.Count + 1}.wav");

        _currentFilePath = nextSegment;
        await _audioRecorderService.StartRecordingAsync(_currentFilePath);
        _recordingSegments.Add(_currentFilePath);
        PositionRulerAtRecordingEnd();

        await AnimatePauseButtonStateAsync(isPaused: false);
        PauseLabel.Text = "Pausar";
        RecordingStatusLabel.Text = "Gravando...";
        RecordingStatusLabel.TextColor = Color.FromArgb("#C62828");
        await WaveformHost.FadeToAsync(1, 160, Easing.CubicOut);
    }

    private async void OnListenClicked(
        object? sender,
        EventArgs e)
    {
        if (_isTransitioning) return;
        _isTransitioning = true;
        try
        {
            if (_audioPlayer?.IsPlaying == true)
            {
                PausePlaybackPreview();
                return;
            }

            if (_audioPlayer != null)
            {
                DisposePlayback();
                SetListenButtonState(isPlaying: false);
            }

            // Finaliza o WAV atual antes de abri-lo para a pré-escuta.
            if (_audioRecorderService.IsRecording)
            {
                await PauseRecordingAsync();
            }

            var previewFile = await GetPreviewAudioFileAsync();
            if (string.IsNullOrEmpty(previewFile) || !File.Exists(previewFile))
            {
                await DisplayAlertAsync("Áudio", "Nenhum arquivo disponível para reprodução.", "OK");
                return;
            }

            DisposePlayback();
            _audioPlaybackStream = File.OpenRead(previewFile);
            _audioPlayer = AudioManager.Current.CreatePlayer(_audioPlaybackStream);

            _audioPlayer.PlaybackEnded += OnPlaybackEnded;
            InitializePlaybackUI();
            var startPos = TimeSpan.FromSeconds(GetPlaybackStartSeconds());
            SeekPlayer(_audioPlayer, startPos);
            _audioPlayer.Play();
            SetListenButtonState(isPlaying: true);
        }
        catch (Exception ex)
        {
            DisposePlayback();
            SetListenButtonState(isPlaying: false);
            await DisplayAlertAsync("Erro", ex.Message, "OK");
        }
        finally
        {
            _isTransitioning = false;
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

            SetListenButtonState(isPlaying: false);
            ResetPlaybackUI();
            DisposePlayback();
        });
    }

    private void PausePlaybackPreview()
    {
        if (_audioPlayer == null)
        {
            SetListenButtonState(isPlaying: false);
            return;
        }

        _audioPlayer.Pause();
        var position = GetPlayerPosition(_audioPlayer);
        TimelineSlider.Value = Math.Clamp(
            position.TotalSeconds,
            TimelineSlider.Minimum,
            TimelineSlider.Maximum);
        UpdateRulerFromSeconds(TimelineSlider.Value);
        _playbackTimer.Stop();
        StopRulerAnimation();
        SetListenButtonState(isPlaying: false);
    }

    private void SetListenButtonState(bool isPlaying)
    {
        ListenButton.Source = isPlaying ? "pause_blue.svg" : "play_blue.svg";
    }

    private async Task AnimatePauseButtonStateAsync(bool isPaused)
    {
        await PauseButton.ScaleToAsync(0.82, 75, Easing.CubicIn);
        PauseButton.Source = isPaused ? "record_white.svg" : "pause_blue.svg";
        PauseButton.BackgroundColor = isPaused
            ? Color.FromArgb("#E32636")
            : Colors.White;
        PauseButton.BorderColor = isPaused
            ? Color.FromArgb("#E32636")
            : Color.FromArgb("#B9CAE0");
        await PauseButton.ScaleToAsync(1, 150, Easing.CubicOut);
    }

    private void InitializePlaybackUI()
    {
        var duration = GetPlayerDuration(_audioPlayer);
        if (duration.TotalSeconds > 0)
        {
            TimelineSlider.Maximum = duration.TotalSeconds;
            TotalDurationLabel.Text = duration.ToString(@"mm\:ss");
        }
        TimelineSlider.IsEnabled = true;
        _playbackTimer.Start();
        if (TimelineSlider.Maximum > 0)
        {
            var startSeconds = GetPlaybackStartSeconds();
            TimelineSlider.Value = startSeconds;
            _waveformDrawable.RulerNormalized = (float)(startSeconds / TimelineSlider.Maximum);
            WaveformView.Invalidate();
        }
    }

    private void ResetPlaybackUI()
    {
        StopRulerAnimation();
        TimelineSlider.Value = 0;
        TimelineSlider.IsEnabled = false;
        CurrentPositionLabel.Text = "00:00";
        TotalDurationLabel.Text = "00:00";
        _playbackTimer.Stop();
        _isTimelineDragging = false;
        _waveformDrawable.RulerNormalized = 0f;
        WaveformView.Invalidate();
    }

    private async void OnFinalizeClicked(
        object? sender,
        EventArgs e)
    {
        if (!BeginClosing()) return;
        try
        {
            _timer.Stop();
            await StopCurrentRecordingAsync();

            string? finalPath = null;
            if (_recordingSegments.Count > 0)
            {
                finalPath = _recordingSegments.Count == 1
                    ? _recordingSegments[0]
                    : await FinalizeRecordingFileAsync();
            }

            if (!string.IsNullOrEmpty(finalPath))
            {
                var defaultName = $"Gravação {DateTime.Now:dd/MM/yyyy HH:mm}";
                var enteredName = await DisplayPromptAsync(
                    "Nome da gravação",
                    "Digite um nome para identificar esta gravação.",
                    "Salvar",
                    "Usar nome padrão",
                    placeholder: "Ex.: Teste de reunião",
                    maxLength: 80,
                    keyboard: Keyboard.Text,
                    initialValue: defaultName);
                var recordingName = string.IsNullOrWhiteSpace(enteredName)
                    ? defaultName
                    : enteredName.Trim();

                var recordings = await LoadRecordingsAsync();
                recordings.Add(new AudioRecording
                {
                    Name = recordingName,
                    FilePath = finalPath,
                    CreatedAt = DateTime.Now,
                    Duration = _elapsedTime
                });

                await SaveRecordingsAsync(recordings);
            }

            await this.FadeToAsync(0, 220, Easing.CubicIn);
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            EndClosing();
            await DisplayAlertAsync("Erro", $"Não foi possível finalizar a gravação: {ex.Message}", "OK");
        }
    }

    private async void OnCancelClicked(
        object? sender,
        EventArgs e)
    {
        if (!BeginClosing()) return;
        try
        {
            _timer.Stop();
            await CancelRecordingAsync();
            ResetPlaybackUI();
            await this.FadeToAsync(0, 220, Easing.CubicIn);
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            EndClosing();
            await DisplayAlertAsync("Erro", $"Não foi possível cancelar a gravação: {ex.Message}", "OK");
        }
    }

    private async Task CancelRecordingAsync()
    {
        if (_audioRecorderService.IsRecording)
        {
            await _audioRecorderService.StopRecordingAsync();
        }

        DisposePlayback();

        foreach (var file in _recordingSegments)
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }

        _recordingSegments.Clear();
        _currentFilePath = null;
        _elapsedTime = TimeSpan.Zero;

        var previewPath = Path.Combine(FileSystem.CacheDirectory, PreviewFileName);
        if (File.Exists(previewPath))
        {
            File.Delete(previewPath);
        }
    }

    private async Task StopCurrentRecordingAsync()
    {
        if (_audioRecorderService.IsRecording)
        {
            await _audioRecorderService.StopRecordingAsync();
        }

        DisposePlayback();
        _visualizerTimer.Stop();
        ResetPlaybackUI();
    }

    private void DisposePlayback()
    {
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
    }

    private void OnVisualizerTick(object? sender, EventArgs e)
    {
        // Only update visualizer while actively recording (keeps waveform static after stop)
        if (_audioRecorderService.IsRecording && !_isPaused)
        {
            var real = _audioRecorderService.TryGetCurrentLevel();
            var targetLevel = real.HasValue
                ? WaveformHelper.ToVisualLevel(real.Value)
                : 0.05f;

            // Ataque rápido para acompanhar a voz e queda suave para evitar tremulação.
            var smoothing = targetLevel > _smoothedVoiceLevel ? 0.65f : 0.20f;
            _smoothedVoiceLevel += (targetLevel - _smoothedVoiceLevel) * smoothing;
            _waveformDrawable.AddSample(_smoothedVoiceLevel);
            WaveformView.Invalidate();
        }
    }

    private void OnPlaybackTimerTick(object? sender, EventArgs e)
    {
        if (_audioPlayer == null || _isTimelineDragging)
        {
            return;
        }

        var position = GetPlayerPosition(_audioPlayer);
        var duration = GetPlayerDuration(_audioPlayer);
        if (duration.TotalSeconds > 0 && Math.Abs(TimelineSlider.Maximum - duration.TotalSeconds) > 0.1)
        {
            TimelineSlider.Maximum = duration.TotalSeconds;
            TotalDurationLabel.Text = duration.ToString(@"mm\:ss");
        }
        CurrentPositionLabel.Text = position.ToString(@"mm\:ss");
        _isPlaybackUpdatingTimeline = true;
        TimelineSlider.Value = Math.Clamp(position.TotalSeconds, 0, TimelineSlider.Maximum);
        _isPlaybackUpdatingTimeline = false;
        // update ruler while playing
        if (duration.TotalSeconds > 0)
        {
            AnimateRulerTo((float)Math.Clamp(position.TotalSeconds / duration.TotalSeconds, 0d, 1d));
        }
    }

    private void OnTimelineSliderValueChanged(object? sender, ValueChangedEventArgs e)
    {
        if (_isPlaybackUpdatingTimeline) return;
        UpdateRulerFromSeconds(e.NewValue);
    }

    private void OnTimelineDragStarted(object? sender, EventArgs e)
    {
        _isTimelineDragging = true;
    }

    private void OnTimelineDragCompleted(object? sender, EventArgs e)
    {
        _isTimelineDragging = false;
        if (_audioPlayer != null)
        {
            SeekPlayer(_audioPlayer, TimeSpan.FromSeconds(TimelineSlider.Value));
        }
    }

    private void OnWaveformPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        var host = WaveformHost;

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                StopRulerAnimation();
                _waveformPanStartSliderValue = TimelineSlider.Value;
                _isTimelineDragging = true;
                break;
            case GestureStatus.Running:
                if (host.Width <= 0) return;
                var deltaNormalized = e.TotalX / host.Width;
                var newVal = _waveformPanStartSliderValue + deltaNormalized * Math.Max(1, TimelineSlider.Maximum);
                newVal = Math.Clamp(newVal, TimelineSlider.Minimum, TimelineSlider.Maximum);
                TimelineSlider.Value = newVal;
                UpdateRulerFromSeconds(newVal);
                break;
            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                _isTimelineDragging = false;
                if (_audioPlayer != null)
                {
                    SeekPlayer(_audioPlayer, TimeSpan.FromSeconds(TimelineSlider.Value));
                }
                break;
        }
    }

    private void OnWaveformTapped(object? sender, TappedEventArgs e)
    {
        var host = WaveformHost;
        if (host.Width <= 0) return;
        StopRulerAnimation();
        var position = e.GetPosition(host);
        if (position == null) return;
        var relativeX = position.Value.X;
        var normalized = Math.Clamp(relativeX / host.Width, 0d, 1d);
        var targetSeconds = normalized * Math.Max(1d, TimelineSlider.Maximum);
        TimelineSlider.Value = targetSeconds;
        UpdateRulerFromSeconds(targetSeconds);
        if (_audioPlayer != null)
        {
            SeekPlayer(_audioPlayer, TimeSpan.FromSeconds(targetSeconds));
        }
    }

    private void UpdateRulerFromSeconds(double seconds)
    {
        if (TimelineSlider.Maximum > 0)
        {
            var safeSeconds = Math.Clamp(seconds, TimelineSlider.Minimum, TimelineSlider.Maximum);
            var normalized = (float)(safeSeconds / TimelineSlider.Maximum);
            CurrentPositionLabel.Text = TimeSpan.FromSeconds(safeSeconds).ToString(@"mm\:ss");
            _waveformDrawable.RulerNormalized = normalized;
            WaveformView.Invalidate();
        }
    }

    private void PositionRulerAtRecordingEnd()
    {
        StopRulerAnimation();
        TimelineSlider.Maximum = Math.Max(1d, _elapsedTime.TotalSeconds);
        TimelineSlider.Value = Math.Clamp(
            _elapsedTime.TotalSeconds,
            TimelineSlider.Minimum,
            TimelineSlider.Maximum);
        CurrentPositionLabel.Text = _elapsedTime.ToString(@"mm\:ss");
        TotalDurationLabel.Text = _elapsedTime.ToString(@"mm\:ss");
        _waveformDrawable.RulerNormalized = 1f;
        WaveformView.Invalidate();
    }

    private double GetPlaybackStartSeconds()
    {
        if (TimelineSlider.Maximum > 0)
        {
            var max = TimelineSlider.Maximum;
            var normalized = _waveformDrawable.RulerNormalized;
            if (normalized > 0f)
            {
                return Math.Clamp(normalized * max, TimelineSlider.Minimum, max);
            }

            return Math.Clamp(TimelineSlider.Value, TimelineSlider.Minimum, max);
        }

        return 0d;
    }

    private void AnimateRulerTo(float target)
    {
        _rulerTarget = target;
        if (_rulerTimer != null)
        {
            // already animating; keep target updated
            return;
        }

        _rulerTimer = Dispatcher.CreateTimer();
        _rulerTimer.Interval = TimeSpan.FromMilliseconds(16);
        _rulerTimer.Tick += (s, ev) =>
        {
            var cur = _waveformDrawable.RulerNormalized;
            var next = cur + (_rulerTarget - cur) * RulerEaseFactor; // ease
            _waveformDrawable.RulerNormalized = next;
            WaveformView.Invalidate();
            if (Math.Abs(_rulerTarget - next) < 0.001f)
            {
                _waveformDrawable.RulerNormalized = _rulerTarget;
                _rulerTimer?.Stop();
                _rulerTimer = null;
            }
        };
        _rulerTimer.Start();
    }

    private void StopRulerAnimation()
    {
        _rulerTimer?.Stop();
        _rulerTimer = null;
    }
    
    private static TimeSpan GetPlayerDuration(IAudioPlayer? player)
    {
        if (player == null)
        {
            return TimeSpan.Zero;
        }

        return ReadPlayerTime(player, "Duration");
    }

    private static TimeSpan GetPlayerPosition(IAudioPlayer? player)
    {
        if (player == null)
        {
            return TimeSpan.Zero;
        }

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
        if (method == null)
            return;

        var parameters = method.GetParameters();
        if (parameters.Length == 0)
        {
            method.Invoke(player, null);
            return;
        }

        var pType = parameters[0].ParameterType;
        object arg;
        if (pType == typeof(TimeSpan))
        {
            arg = position;
        }
        else if (pType == typeof(double) || pType == typeof(Double))
        {
            arg = position.TotalSeconds;
        }
        else if (pType == typeof(float) || pType == typeof(Single))
        {
            arg = (float)position.TotalSeconds;
        }
        else if (pType == typeof(int) || pType == typeof(Int32))
        {
            arg = (int)position.TotalMilliseconds;
        }
        else if (pType == typeof(long) || pType == typeof(Int64))
        {
            arg = (long)position.TotalMilliseconds;
        }
        else
        {
            // attempt convert
            try
            {
                arg = Convert.ChangeType(position.TotalSeconds, pType);
            }
            catch
            {
                // fallback to seconds as double
                arg = position.TotalSeconds;
            }
        }

        method.Invoke(player, new object[] { arg });
    }

    private async Task<List<AudioRecording>> LoadRecordingsAsync()
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

    private async Task<string?> GetPreviewAudioFileAsync()
    {
        if (_recordingSegments.Count == 0)
        {
            return null;
        }

        if (_recordingSegments.Count == 1)
        {
            return _recordingSegments[0];
        }

        var previewPath = Path.Combine(FileSystem.CacheDirectory, PreviewFileName);

        if (File.Exists(previewPath))
        {
            File.Delete(previewPath);
        }

        await MergeWavFilesAsync(_recordingSegments, previewPath);
        return previewPath;
    }

    private async Task<string> FinalizeRecordingFileAsync()
    {
        if (_recordingSegments.Count == 0)
        {
            throw new InvalidOperationException("Nenhum segmento de gravação disponível.");
        }

        if (_recordingSegments.Count == 1)
        {
            return _recordingSegments[0];
        }

        var finalPath = Path.Combine(
            FileSystem.AppDataDirectory,
            $"recording_{DateTime.Now:yyyyMMddHHmmss}.wav");

        await MergeWavFilesAsync(_recordingSegments, finalPath);

        foreach (var segment in _recordingSegments)
        {
            if (File.Exists(segment))
            {
                File.Delete(segment);
            }
        }

        _recordingSegments.Clear();
        _recordingSegments.Add(finalPath);
        return finalPath;
    }

    private async Task MergeWavFilesAsync(List<string> inputPaths, string outputPath)
    {
        var dataChunks = new List<byte[]>();
        int totalDataLength = 0;
        byte[]? fmtChunk = null;

        foreach (var path in inputPaths)
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream);

            var riff = new string(reader.ReadChars(4));
            if (riff != "RIFF")
                throw new InvalidDataException("Arquivo de gravação inválido.");

            _ = reader.ReadInt32();
            var wave = new string(reader.ReadChars(4));
            if (wave != "WAVE")
                throw new InvalidDataException("Arquivo de gravação inválido.");

            byte[]? currentFmtChunk = null;
            int dataLength = 0;
            long dataStart = 0;

            while (stream.Position < stream.Length)
            {
                var chunkId = new string(reader.ReadChars(4));
                var chunkSize = reader.ReadInt32();
                var chunkStart = stream.Position;

                if (chunkId == "fmt ")
                {
                    currentFmtChunk = reader.ReadBytes(chunkSize);
                }
                else if (chunkId == "data")
                {
                    dataStart = stream.Position;
                    dataLength = chunkSize;
                    stream.Position += chunkSize;
                }
                else
                {
                    stream.Position += chunkSize;
                }

                if (stream.Position % 2 == 1)
                {
                    stream.Position += 1;
                }
            }

            if (currentFmtChunk == null)
                throw new InvalidDataException("Arquivo WAV sem chunk fmt.");

            if (fmtChunk == null)
            {
                fmtChunk = currentFmtChunk;
            }
            else if (!fmtChunk.SequenceEqual(currentFmtChunk))
            {
                throw new InvalidDataException("Os segmentos WAV possuem configurações diferentes.");
            }

            stream.Position = dataStart;
            var data = reader.ReadBytes(dataLength);
            dataChunks.Add(data);
            totalDataLength += dataLength;
        }

        if (fmtChunk == null)
            throw new InvalidDataException("Formato WAV inválido.");

        using var outputStream = File.Create(outputPath);
        using var writer = new BinaryWriter(outputStream);

        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + totalDataLength);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        writer.Write(fmtChunk.Length);
        writer.Write(fmtChunk);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        writer.Write(totalDataLength);

        foreach (var chunk in dataChunks)
        {
            writer.Write(chunk);
        }

        writer.Flush();
    }

    private async Task SaveRecordingsAsync(List<AudioRecording> recordings)
    {
        var path = Path.Combine(FileSystem.AppDataDirectory, RecordingsFileName);
        var json = JsonSerializer.Serialize(recordings);
        await File.WriteAllTextAsync(path, json);
    }

    // WaveformDrawable moved to Views/WaveformDrawable.cs

    private async void OnBackClicked(
        object? sender,
        EventArgs e)
    {
        if (!BeginClosing()) return;
        try
        {
            _timer.Stop();
            _visualizerTimer.Stop();
            await CancelRecordingAsync();
            ResetPlaybackUI();
            await this.FadeToAsync(0, 220, Easing.CubicIn);
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            EndClosing();
            await DisplayAlertAsync("Erro", $"Não foi possível sair da gravação: {ex.Message}", "OK");
        }
    }

    private bool BeginClosing()
    {
        if (_isClosing) return false;
        _isClosing = true;
        PauseButton.IsEnabled = false;
        ListenButton.IsEnabled = false;
        FinalizeButton.IsEnabled = false;
        return true;
    }

    private void EndClosing()
    {
        _isClosing = false;
        PauseButton.IsEnabled = true;
        ListenButton.IsEnabled = true;
        FinalizeButton.IsEnabled = true;
    }
}
 
