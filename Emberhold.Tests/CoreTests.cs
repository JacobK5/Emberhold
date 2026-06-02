using System.Numerics;
using Emberhold.Core;
using Xunit;

namespace Emberhold.Tests;

public class CoreTests
{
    [Fact]
    public void Clamp_BoundsValue()
    {
        Assert.Equal(0f, MathUtils.Clamp(-3f, 0f, 10f));
        Assert.Equal(10f, MathUtils.Clamp(15f, 0f, 10f));
        Assert.Equal(5f, MathUtils.Clamp(5f, 0f, 10f));
    }

    [Fact]
    public void Normalize_ZeroVector_IsZero()
        => Assert.Equal(Vector2.Zero, MathUtils.Normalize(0f, 0f));

    [Fact]
    public void Normalize_UnitLength()
    {
        var n = MathUtils.Normalize(3f, 4f);
        Assert.Equal(1f, n.Length(), 4);
    }

    [Fact]
    public void AttractionSpeed_ZeroOutsideRange()
    {
        Assert.Equal(0f, MathUtils.AttractionSpeed(200f, 112f));
        Assert.True(MathUtils.AttractionSpeed(20f, 112f) > 0f);
    }

    [Fact]
    public void DepositRate_ScalesWithSqrtCost_SoBuildTimeGrowsSqrt()
    {
        // 4x cost should be ~2x build time (time = cost / rate = sqrt(cost)/base).
        float rateLo = MathUtils.DepositRate(100f, 3.3f);
        float rateHi = MathUtils.DepositRate(400f, 3.3f);
        float timeLo = 100f / rateLo;
        float timeHi = 400f / rateHi;
        Assert.Equal(2f, timeHi / timeLo, 3);
    }

    [Fact]
    public void DepositAmount_CapsByGoldRemainingAndCarry()
    {
        Assert.Equal(5, MathUtils.DepositAmount(gold: 5, remainingCost: 20, carry: 12f)); // gold-limited
        Assert.Equal(8, MathUtils.DepositAmount(gold: 50, remainingCost: 8, carry: 12f));  // cost-limited
        Assert.Equal(3, MathUtils.DepositAmount(gold: 50, remainingCost: 20, carry: 3.9f)); // carry-limited (floored)
        Assert.Equal(0, MathUtils.DepositAmount(gold: 0, remainingCost: 20, carry: 12f));
    }

    [Theory]
    [InlineData(5, true)]
    [InlineData(10, true)]
    [InlineData(4, false)]
    public void WaveStats_EliteEveryFifthWave(int wave, bool elite)
        => Assert.Equal(elite, WaveStats.For(wave).Elite);

    [Fact]
    public void WaveStats_ScalesAndCapsSpeed()
    {
        Assert.True(WaveStats.For(10).Count > WaveStats.For(1).Count);
        Assert.True(WaveStats.For(10).Health > WaveStats.For(1).Health);
        Assert.True(WaveStats.For(100).Speed <= 89f);
    }
}
