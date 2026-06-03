using System.Numerics;
using Emberhold.Data;
using Emberhold.Game;
using Xunit;

namespace Emberhold.Tests;

public class RunStoreTests
{
    [Fact]
    public void GoldThreat_RampsWithAccumulatedGold_AndCaps()
    {
        var s = new GameState(seedDebug: false);

        s.GoldAccrued = 0;
        Assert.Equal(1f, s.GoldThreat, 3);          // early game: no extra threat

        s.GoldAccrued = 400;
        Assert.Equal(1f, s.GoldThreat, 3);          // threshold: still baseline

        s.GoldAccrued = 1500;
        Assert.True(s.GoldThreat > 1f && s.GoldThreat < 2f, "mid-run threat ramps up");

        s.GoldAccrued = 100_000;
        Assert.Equal(2f, s.GoldThreat, 3);          // capped at 2.0x
    }

    [Fact]
    public void EarnGold_TracksAccrual_ButSpendingDoesNot()
    {
        var s = new GameState(seedDebug: false);  // Gold 20, GoldAccrued 20
        s.EarnGold(50);
        Assert.Equal(70, s.Gold);
        Assert.Equal(70, s.GoldAccrued);

        s.Gold -= 40;                               // spending leaves accrual intact
        Assert.Equal(30, s.Gold);
        Assert.Equal(70, s.GoldAccrued);
    }

    [Fact]
    public void RunSave_RoundTripsThroughJson()
    {
        var s = new GameState(seedDebug: false);
        s.Wave = 14;
        s.Chapter = 3;
        s.Gold = 220;
        s.GoldAccrued = 1850;
        s.KeepHealth = 180f;
        s.KeepMaxHealth = 340f;
        s.HordeTier = 2;
        s.Modifier = RunModifier.Catalog[2]; // iron_horde
        s.Hero.Level = 6;
        s.Hero.Damage = 38f;
        s.Hero.Relics.Add(RelicKind.EmberRing);
        s.SeenSynergies.Add("supply_lines");
        s.Structures.Add(StructureFactory.Create(s, CardDb.Get("archer_post"), new Vector2(80, -80)));
        s.Pads.Add(new Pad { Def = CardDb.Get("gold_mine"), Pos = new Vector2(-60, 60), Invested = 12 });

        string json = RunStore.ToJson(RunStore.Capture(s));
        var restored = RunStore.FromJson(json);
        Assert.NotNull(restored);

        var t = new GameState(seedDebug: false);
        RunStore.Apply(t, restored!);

        Assert.Equal(14, t.Wave);
        Assert.Equal(3, t.Chapter);
        Assert.Equal(220, t.Gold);
        Assert.Equal(1850, t.GoldAccrued);
        Assert.Equal(180f, t.KeepHealth, 2);
        Assert.Equal(2, t.HordeTier);
        Assert.Equal("iron_horde", t.Modifier.Id);
        Assert.Equal(6, t.Hero.Level);
        Assert.Equal(38f, t.Hero.Damage, 2);
        Assert.Contains(RelicKind.EmberRing, t.Hero.Relics);
        Assert.Contains("supply_lines", t.SeenSynergies);
        Assert.Single(t.Structures);
        Assert.Equal(StructureKind.ArcherPost, t.Structures[0].Kind);
        Assert.Single(t.Pads);
        Assert.Equal("gold_mine", t.Pads[0].Def.Id);
        Assert.Equal(12, t.Pads[0].Invested);
        Assert.Equal(Phase.Combat, t.Phase);
        Assert.Empty(t.Enemies);
    }
}
