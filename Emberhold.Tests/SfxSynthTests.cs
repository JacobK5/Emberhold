using Emberhold.Core;
using Xunit;

namespace Emberhold.Tests;

/// <summary>The pure SFX synthesizer that generates every game sound at startup.</summary>
public class SfxSynthTests
{
    [Fact]
    public void Tone_HasExpectedLength_AndStaysInRange()
    {
        var buf = SfxSynth.Tone(440f, 0.1f, WaveShape.Triangle, gain: 1f);
        Assert.Equal((int)(0.1f * SfxSynth.Rate), buf.Length);
        Assert.All(buf, s => Assert.InRange(s, -1.001f, 1.001f));
        Assert.Contains(buf, s => MathF.Abs(s) > 0.1f); // actually makes sound
    }

    [Fact]
    public void Sweep_IsDeterministic()
    {
        var a = SfxSynth.Sweep(880f, 440f, 0.05f, WaveShape.Saw);
        var b = SfxSynth.Sweep(880f, 440f, 0.05f, WaveShape.Saw);
        Assert.Equal(a, b); // same recipe -> identical samples every run
    }

    [Fact]
    public void Mix_SpansLongest_AndConcat_SumsLengths()
    {
        var shortBuf = SfxSynth.Tone(440f, 0.02f);
        var longBuf = SfxSynth.Tone(220f, 0.06f);
        Assert.Equal(longBuf.Length, SfxSynth.Mix(shortBuf, longBuf).Length);
        Assert.Equal(shortBuf.Length + longBuf.Length, SfxSynth.Concat(shortBuf, longBuf).Length);
    }

    [Fact]
    public void ToWav_WritesValidHeaderAndPayload()
    {
        var buf = SfxSynth.Noise(0.05f, 1000f);
        var wav = SfxSynth.ToWav(buf);
        Assert.Equal(44 + buf.Length * 2, wav.Length);
        Assert.Equal((byte)'R', wav[0]); // RIFF
        Assert.Equal((byte)'W', wav[8]); // WAVE
        Assert.Equal((byte)'d', wav[36]); // data chunk
        // sample rate field (offset 24, little-endian)
        Assert.Equal(SfxSynth.Rate, BitConverter.ToInt32(wav, 24));
    }
}
