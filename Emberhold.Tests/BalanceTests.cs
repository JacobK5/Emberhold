using Emberhold.Game;
using Xunit;

namespace Emberhold.Tests;

/// <summary>
/// The balancing tuner over the static <see cref="Balance"/> fields. Each test
/// snapshots and restores the global config so it can't bleed into other tests.
/// </summary>
public class BalanceTests
{
    private static T Isolated<T>(Func<T> body)
    {
        string before = BalanceConfig.Export();
        try { return body(); }
        finally { BalanceConfig.Reset(); BalanceConfig.Import(before); }
    }

    [Fact]
    public void Adjust_StepsOnAGrid_AndClampsToRange()
    {
        Isolated<object?>(() =>
        {
            var e = BalanceConfig.Entries.First(x => x.Field == "EnemyHealthMult");
            BalanceConfig.Set(e.Field, 1.0f); // grid-aligned start
            BalanceConfig.Adjust(e, +1);
            Assert.Equal(1.0f + e.Step, BalanceConfig.Get(e.Field), 3);

            // Drive it past the floor — it clamps, never goes below Min.
            for (int i = 0; i < 500; i++) BalanceConfig.Adjust(e, -1);
            Assert.Equal(e.Min, BalanceConfig.Get(e.Field), 3);
            return null;
        });
    }

    [Fact]
    public void Reset_RestoresCompiledDefaults()
    {
        Isolated<object?>(() =>
        {
            var e = BalanceConfig.Entries.First(x => x.Field == "HeroDamageMult");
            float def = BalanceConfig.Get(e.Field);
            BalanceConfig.Adjust(e, +3);
            Assert.False(BalanceConfig.IsDefault(e.Field));
            BalanceConfig.Reset();
            Assert.True(BalanceConfig.IsDefault(e.Field));
            Assert.Equal(def, BalanceConfig.Get(e.Field), 3);
            return null;
        });
    }

    [Fact]
    public void ExportImport_RoundTrips()
    {
        Isolated<object?>(() =>
        {
            BalanceConfig.Set("TowerDamageMult", 2.15f);
            BalanceConfig.Set("MineSpeedMult", 0.5f);
            string preset = BalanceConfig.Export();

            BalanceConfig.Reset();
            Assert.True(BalanceConfig.IsDefault("TowerDamageMult"));

            Assert.True(BalanceConfig.Import(preset));
            Assert.Equal(2.15f, BalanceConfig.Get("TowerDamageMult"), 3);
            Assert.Equal(0.5f, BalanceConfig.Get("MineSpeedMult"), 3);
            return null;
        });
    }

    [Fact]
    public void Import_IgnoresJunkAndUnknownFields()
    {
        Isolated<object?>(() =>
        {
            Assert.False(BalanceConfig.Import("not-a-preset"));
            Assert.False(BalanceConfig.Import("NoSuchField=2;alsoBad"));
            // A mix: the one valid token applies, the rest are skipped.
            Assert.True(BalanceConfig.Import("GoldRewardMult=1.5;garbage;Foo=9"));
            Assert.Equal(1.5f, BalanceConfig.Get("GoldRewardMult"), 3);
            return null;
        });
    }
}
