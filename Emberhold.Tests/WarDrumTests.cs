using Emberhold.Render;
using Xunit;

namespace Emberhold.Tests;

/// <summary>The adaptive war-drum pattern (pure sequencer logic; playback is device-gated).</summary>
public class WarDrumTests
{
    private static int HitsAt(float intensity)
    {
        int total = 0;
        for (int step = 0; step < 8; step++)
            total += Audio.PatternHits(step, intensity).Count();
        return total;
    }

    [Fact]
    public void Pattern_AddsLayersAsIntensityRises()
    {
        int calm = HitsAt(0.2f);
        int busy = HitsAt(0.6f);
        int frantic = HitsAt(1f);
        Assert.True(calm < busy, "more drums when the field gets busy");
        Assert.True(busy < frantic, "boss-tier threat is the loudest");
    }

    [Fact]
    public void Pattern_AlwaysKeepsTheHeartbeatKick()
    {
        foreach (float i in new[] { 0.1f, 0.5f, 1f })
        {
            Assert.Contains(Audio.PatternHits(0, i), h => h.Id == SfxId.Kick);
            Assert.Contains(Audio.PatternHits(4, i), h => h.Id == SfxId.Kick);
        }
    }

    [Fact]
    public void Pattern_IsQuietOnOffBeats_AtLowIntensity()
    {
        // At minimum intensity only the two heartbeat kicks play per bar.
        Assert.Equal(2, HitsAt(0.1f));
    }
}
