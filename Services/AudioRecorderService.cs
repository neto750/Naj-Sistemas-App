using Plugin.Maui.Audio;

namespace NajGravador.Services;

/// <summary>
/// Grava o PCM recebido do streamer no WAV e calcula o RMS do mesmo buffer.
/// A onda e o arquivo, portanto, sempre representam exatamente o mesmo áudio.
/// </summary>
public sealed class AudioRecorderService
{
    private const int SampleRate = 44100;
    private const short ChannelCount = 1;
    private const short BitsPerSample = 16;

    private readonly IAudioManager _audioManager;
    private readonly object _writeLock = new();
    private IAudioStreamer? _audioStreamer;
    private FileStream? _outputStream;
    private long _pcmByteCount;
    private float _currentLevel;

    public AudioRecorderService(IAudioManager audioManager)
    {
        _audioManager = audioManager;
    }

    public bool IsRecording => _audioStreamer?.IsStreaming == true;

    public float CurrentLevel => _currentLevel;

    public async Task StartRecordingAsync(string? filePath = null)
    {
        if (IsRecording) return;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("O caminho do arquivo de gravação é obrigatório.", nameof(filePath));
        }

        _pcmByteCount = 0;
        _currentLevel = 0;
        _outputStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        WriteWavHeader(_outputStream, 0);

        _audioStreamer = _audioManager.CreateStreamer();
        _audioStreamer.Options.SampleRate = SampleRate;
        _audioStreamer.Options.Channels = ChannelType.Mono;
        _audioStreamer.Options.BitDepth = BitDepth.Pcm16bit;
        _audioStreamer.OnAudioCaptured += OnAudioCaptured;

        try
        {
            await _audioStreamer.StartAsync();
        }
        catch
        {
            CleanupStreamer();
            _outputStream.Dispose();
            _outputStream = null;
            throw;
        }
    }

    public async Task StopRecordingAsync()
    {
        var streamer = _audioStreamer;
        if (streamer?.IsStreaming == true)
        {
            await streamer.StopAsync();
        }

        CleanupStreamer();

        lock (_writeLock)
        {
            if (_outputStream != null)
            {
                WriteWavHeader(_outputStream, _pcmByteCount);
                _outputStream.Flush();
                _outputStream.Dispose();
                _outputStream = null;
            }
        }

        _currentLevel = 0;
    }

    public float? TryGetCurrentLevel() => IsRecording ? _currentLevel : null;

    private void OnAudioCaptured(object? sender, AudioStreamEventArgs e)
    {
        var audio = e.Audio;
        if (audio == null || audio.Length < 2) return;

        lock (_writeLock)
        {
            if (_outputStream == null) return;
            _outputStream.Write(audio, 0, audio.Length);
            _pcmByteCount += audio.Length;
        }

        _currentLevel = CalculatePcm16Rms(audio);
    }

    private void CleanupStreamer()
    {
        if (_audioStreamer == null) return;
        var streamer = _audioStreamer;
        _audioStreamer = null;
        streamer.OnAudioCaptured -= OnAudioCaptured;
        if (streamer is IDisposable disposable) disposable.Dispose();
    }

    private static float CalculatePcm16Rms(byte[] audio)
    {
        var length = audio.Length - audio.Length % 2;
        if (length == 0) return 0;

        double sum = 0;
        var samples = length / 2;
        for (var index = 0; index < length; index += 2)
        {
            var sample = (short)(audio[index] | audio[index + 1] << 8);
            var normalized = sample / 32768d;
            sum += normalized * normalized;
        }

        return (float)Math.Clamp(Math.Sqrt(sum / samples), 0d, 1d);
    }

    private static void WriteWavHeader(Stream stream, long pcmByteCount)
    {
        stream.Position = 0;
        using var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        writer.Write((int)(36 + pcmByteCount));
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(ChannelCount);
        writer.Write(SampleRate);
        writer.Write(SampleRate * ChannelCount * BitsPerSample / 8);
        writer.Write((short)(ChannelCount * BitsPerSample / 8));
        writer.Write(BitsPerSample);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        writer.Write((int)pcmByteCount);
        stream.Position = stream.Length;
    }
}
