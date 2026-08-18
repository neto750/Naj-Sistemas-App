using Microsoft.Maui.Graphics;

namespace NajGravador.Views;

/// <summary>
/// Onda compacta no estilo Samsung, com trecho percorrido e playhead arrastável.
/// </summary>
public sealed class WaveformDrawable : IDrawable
{
    private const int MaxSamples = 150;
    private const float MinimumSample = 0.035f;

    public List<float> Samples { get; } = Enumerable
        .Repeat(MinimumSample, MaxSamples)
        .ToList();

    public float RulerNormalized { get; set; }

    public void AddSample(float level)
    {
        Samples.Add(Math.Clamp(level, MinimumSample, 1f));
        if (Samples.Count > MaxSamples) Samples.RemoveAt(0);
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (dirtyRect.Width <= 0 || dirtyRect.Height <= 0 || Samples.Count == 0) return;

        var topPadding = 16f;
        var bottomPadding = 10f;
        var waveformTop = dirtyRect.Top + topPadding;
        var waveformHeight = Math.Max(1f, dirtyRect.Height - topPadding - bottomPadding);
        var centerY = waveformTop + waveformHeight / 2f;
        var spacing = dirtyRect.Width / Samples.Count;
        var barWidth = Math.Clamp(spacing * 0.48f, 1.5f, 3.5f);
        var ruler = Math.Clamp(RulerNormalized, 0f, 1f);
        var rulerX = dirtyRect.Left + ruler * dirtyRect.Width;

        // Linha central discreta ajuda a leitura de silêncio sem simular voz.
        canvas.StrokeColor = Color.FromArgb("#E5EBF2");
        canvas.StrokeSize = 1f;
        canvas.DrawLine(dirtyRect.Left, centerY, dirtyRect.Right, centerY);

        for (var index = 0; index < Samples.Count; index++)
        {
            var x = dirtyRect.Left + (index + 0.5f) * spacing;
            var height = Math.Max(3f, Samples[index] * waveformHeight * 0.9f);
            var y = centerY - height / 2f;

            // A onda é imutável durante a pré-escuta; somente a régua se move.
            canvas.FillColor = Color.FromArgb("#1268D3");
            canvas.FillRoundedRectangle(x - barWidth / 2f, y, barWidth, height, barWidth / 2f);
        }

        // Régua vermelha com alça visível. A área inteira da onda continua interativa.
        canvas.StrokeColor = Color.FromArgb("#E32636");
        canvas.StrokeSize = 2.5f;
        canvas.DrawLine(rulerX, dirtyRect.Top + 7f, rulerX, dirtyRect.Bottom - 5f);
        canvas.FillColor = Color.FromArgb("#E32636");
        canvas.FillCircle(rulerX, dirtyRect.Top + 7f, 5.5f);
        canvas.FillCircle(rulerX, dirtyRect.Bottom - 5f, 3f);
    }
}
