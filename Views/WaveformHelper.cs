using System;
using System.Collections.Generic;
using System.IO;

namespace NajGravador.Views;

public static class WaveformHelper
{
    // Extracts `sampleCount` normalized RMS samples (0..1) from a PCM WAV file.
    // Returns null on failure.
    public static List<float>? GenerateSamplesFromWav(string path, int sampleCount)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream);

            var riff = new string(reader.ReadChars(4));
            if (riff != "RIFF") return null;
            _ = reader.ReadInt32();
            var wave = new string(reader.ReadChars(4));
            if (wave != "WAVE") return null;

            int? audioFormat = null;
            int numChannels = 1;
            int bitsPerSample = 16;
            long dataStart = -1;
            int dataSize = 0;

            while (stream.Position < stream.Length)
            {
                var chunkId = new string(reader.ReadChars(4));
                var chunkSize = reader.ReadInt32();
                var chunkStart = stream.Position;

                if (chunkId == "fmt ")
                {
                    audioFormat = reader.ReadInt16();
                    numChannels = reader.ReadInt16();
                    _ = reader.ReadInt32(); // sampleRate
                    _ = reader.ReadInt32(); // byteRate
                    _ = reader.ReadInt16(); // blockAlign
                    bitsPerSample = reader.ReadInt16();
                }
                else if (chunkId == "data")
                {
                    dataStart = stream.Position;
                    dataSize = chunkSize;
                    stream.Position += chunkSize;
                }
                else
                {
                    stream.Position += chunkSize;
                }

                if (stream.Position % 2 == 1) stream.Position += 1;
            }

            if (dataStart < 0 || dataSize <= 0) return null;
            if (audioFormat.HasValue && audioFormat.Value != 1)
            {
                // not PCM
                return null;
            }

            stream.Position = dataStart;
            var bytesPerSample = Math.Max(1, bitsPerSample / 8);
            var totalSamples = dataSize / bytesPerSample / numChannels;
            if (totalSamples <= 0) return null;

            var samples = new List<float>(sampleCount);
            long samplesPerBucket = Math.Max(1, totalSamples / sampleCount);

            for (int b = 0; b < sampleCount; b++)
            {
                long startSample = b * samplesPerBucket;
                long endSample = Math.Min(totalSamples, startSample + samplesPerBucket);
                if (startSample >= endSample)
                {
                    samples.Add(0.0f);
                    continue;
                }

                double sumSq = 0;
                long count = 0;

                stream.Position = dataStart + startSample * bytesPerSample * numChannels;

                for (long s = startSample; s < endSample; s++)
                {
                    for (int ch = 0; ch < numChannels; ch++)
                    {
                        double sampleVal = 0;
                        if (bitsPerSample == 16)
                        {
                            var v = reader.ReadInt16();
                            sampleVal = v / 32768.0;
                        }
                        else if (bitsPerSample == 8)
                        {
                            var bval = reader.ReadByte();
                            sampleVal = (bval - 128) / 128.0;
                        }
                        else
                        {
                            // unsupported bit depth
                            sampleVal = 0;
                        }

                        sumSq += sampleVal * sampleVal;
                        count++;
                    }
                }

                var rms = count > 0 ? Math.Sqrt(sumSq / count) : 0.0;
                samples.Add(ToVisualLevel((float)rms));
            }

            return samples;
        }
        catch
        {
            return null;
        }
    }

    public static float ToVisualLevel(float rms)
    {
        if (rms <= 0.0015f) return 0.05f;

        var decibels = 20f * MathF.Log10(Math.Clamp(rms, 0.00001f, 1f));
        var normalized = Math.Clamp((decibels + 52f) / 44f, 0f, 1f);
        return 0.05f + MathF.Sqrt(normalized) * 0.95f;
    }
}
