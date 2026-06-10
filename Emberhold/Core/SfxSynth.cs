namespace Emberhold.Core;

/// <summary>Oscillator shapes for the procedural SFX synthesizer.</summary>
public enum WaveShape { Sine, Triangle, Square, Saw, Noise }

/// <summary>
/// Pure-math SFX synthesis: tones, sweeps, and noise with simple envelopes,
/// mixed/concatenated into float buffers and serialized as 16-bit mono WAV.
/// No engine dependencies — every game sound is generated from these at startup,
/// keeping the project asset-free. Covered by unit tests.
/// </summary>
public static class SfxSynth
{
    public const int Rate = 22050;

    /// <summary>A single tone with a linear attack and polynomial decay.</summary>
    public static float[] Tone(float freq, float dur, WaveShape shape = WaveShape.Sine,
        float gain = 1f, float attack = 0.004f, float decayPow = 3f)
        => Sweep(freq, freq, dur, shape, gain, attack, decayPow);

    /// <summary>A tone whose frequency glides exponentially from f0 to f1.</summary>
    public static float[] Sweep(float f0, float f1, float dur, WaveShape shape = WaveShape.Sine,
        float gain = 1f, float attack = 0.004f, float decayPow = 3f)
    {
        int n = Math.Max(1, (int)(dur * Rate));
        var buf = new float[n];
        var rng = new Random(1234); // deterministic: same sound every run
        double phase = 0;
        float logRatio = MathF.Log(MathF.Max(1e-4f, f1 / MathF.Max(1e-4f, f0)));
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float u = i / (float)n;
            float freq = f0 * MathF.Exp(logRatio * u);
            phase += freq / Rate;
            float ph = (float)(phase - Math.Floor(phase));
            float v = shape switch
            {
                WaveShape.Triangle => 4f * MathF.Abs(ph - 0.5f) - 1f,
                WaveShape.Square => ph < 0.5f ? 0.7f : -0.7f, // softened square
                WaveShape.Saw => 2f * ph - 1f,
                WaveShape.Noise => (float)(rng.NextDouble() * 2 - 1),
                _ => MathF.Sin(ph * MathUtils.Tau),
            };
            buf[i] = v * gain * Envelope(t, u, attack, decayPow);
        }
        return buf;
    }

    /// <summary>One-pole low-passed white noise (impacts, whooshes, drums).</summary>
    public static float[] Noise(float dur, float lowpassHz, float gain = 1f,
        float attack = 0.002f, float decayPow = 3f)
    {
        int n = Math.Max(1, (int)(dur * Rate));
        var buf = new float[n];
        var rng = new Random(4321);
        float alpha = 1f - MathF.Exp(-MathUtils.Tau * lowpassHz / Rate);
        float y = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float u = i / (float)n;
            float x = (float)(rng.NextDouble() * 2 - 1);
            y += alpha * (x - y);
            buf[i] = y * gain * Envelope(t, u, attack, decayPow);
        }
        return buf;
    }

    private static float Envelope(float t, float u, float attack, float decayPow)
    {
        float env = t < attack ? t / attack : 1f;
        return env * MathF.Pow(1f - u, decayPow);
    }

    /// <summary>Sum buffers sample-wise (result spans the longest input).</summary>
    public static float[] Mix(params float[][] parts)
    {
        int n = 0;
        foreach (var p in parts) n = Math.Max(n, p.Length);
        var buf = new float[n];
        foreach (var p in parts)
            for (int i = 0; i < p.Length; i++)
                buf[i] += p[i];
        return buf;
    }

    /// <summary>Join buffers end to end (arpeggios, multi-note stings).</summary>
    public static float[] Concat(params float[][] parts)
    {
        int n = 0;
        foreach (var p in parts) n += p.Length;
        var buf = new float[n];
        int at = 0;
        foreach (var p in parts) { Array.Copy(p, 0, buf, at, p.Length); at += p.Length; }
        return buf;
    }

    /// <summary>Serialize samples as a 16-bit mono PCM WAV file image (44-byte header).</summary>
    public static byte[] ToWav(float[] samples)
    {
        int dataLen = samples.Length * 2;
        var bytes = new byte[44 + dataLen];
        using var ms = new MemoryStream(bytes);
        using var w = new BinaryWriter(ms);
        w.Write("RIFF"u8); w.Write(36 + dataLen); w.Write("WAVE"u8);
        w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)1);
        w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
        w.Write("data"u8); w.Write(dataLen);
        foreach (var s in samples)
            w.Write((short)(MathUtils.Clamp(s, -1f, 1f) * short.MaxValue));
        return bytes;
    }
}
