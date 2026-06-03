using Emberhold.Game;
using Xunit;

namespace Emberhold.Tests;

/// <summary>Ascension difficulty tiers: cumulative multipliers + spawn wiring.</summary>
public class AscensionTests
{
    [Fact]
    public void Multipliers_StackWithLevel_AndClamp()
    {
        Assert.Equal(1f, Ascensions.EnemyHp(0), 3);
        Assert.True(Ascensions.EnemyHp(3) > Ascensions.EnemyHp(1));
        Assert.True(Ascensions.PriceMult(5) > Ascensions.PriceMult(2));

        // Speed only kicks in from tier 3.
        Assert.Equal(1f, Ascensions.EnemySpeed(2), 3);
        Assert.True(Ascensions.EnemySpeed(3) > 1f);

        // Keep weakens but never past the -20% floor (capped at level 4).
        Assert.True(Ascensions.KeepMult(4) < Ascensions.KeepMult(1));
        Assert.Equal(Ascensions.KeepMult(4), Ascensions.KeepMult(5), 3);

        // Out-of-range levels clamp.
        Assert.Equal(Ascensions.EnemyHp(Ascensions.Cap), Ascensions.EnemyHp(99), 3);
        Assert.Equal(1f, Ascensions.EnemyHp(-3), 3);
    }

    [Fact]
    public void Summary_IsEmptyDescriptorAtZero_AndDescribesHigherTiers()
    {
        Assert.Equal("standard difficulty", Ascensions.Summary(0));
        Assert.Contains("HP", Ascensions.Summary(3));
        Assert.NotEqual(Ascensions.Summary(1), Ascensions.Summary(4));
    }

    [Fact]
    public void GameState_ExposesAscensionMultipliers()
    {
        var s = new GameState(seedDebug: false) { Ascension = 3 };
        Assert.Equal(Ascensions.EnemyHp(3), s.AscEnemyHpMult, 3);
        Assert.Equal(Ascensions.EnemySpeed(3), s.AscEnemySpeedMult, 3);
    }

    [Fact]
    public void Ascension_RaisesSpawnedEnemyHealth()
    {
        // Aggregate a few normal (pre-archetype) waves to damp RNG kind variance.
        long TotalHp(int ascension)
        {
            long sum = 0;
            for (int seed = 0; seed < 4; seed++)
            {
                var s = new GameState(seedDebug: false) { Wave = 8, Ascension = ascension };
                s.NextWaveKinds = WaveSystem.BuildComposition(s, 8);
                WaveSystem.StartWave(s);
                for (int i = 0; i < 4000 && s.Spawning is not null; i++) WaveSystem.Update(s, 0.2f);
                sum += s.Enemies.Sum(e => (long)e.MaxHealth);
            }
            return sum;
        }
        Assert.True(TotalHp(5) > TotalHp(0) * 1.3, "ascension 5 roster should be much tougher");
    }
}
