using System.Numerics;
using Emberhold.Data;
using Emberhold.Game;
using Xunit;

namespace Emberhold.Tests;

/// <summary>Champion mini-bosses: promotion buffs, the enrage trait, and bounty.</summary>
public class ChampionTests
{
    private static Enemy Raider(GameState s, float hp = 40f) =>
        new() { Id = s.NextId(), Pos = Vector2.Zero, Radius = 11, Health = hp, MaxHealth = hp, SlowFactor = 1f, Kind = EnemyKind.Raider, Damage = 6, Reward = 3 };

    [Fact]
    public void Promote_BuffsTheRaider_AndMarksAChampion()
    {
        var s = new GameState(seedDebug: false);
        var e = Raider(s, 40f);
        Champions.Promote(s, e);
        Assert.True(e.Champion);
        Assert.True(e.MaxHealth > 40f * 2.5f); // tanky even for the fragile Swiftblade trait
        Assert.True(e.Reward >= 12);           // 4x bounty
    }

    [Fact]
    public void Warbringer_EnragesAsItLosesHealth_OthersDoNot()
    {
        var s = new GameState(seedDebug: false);
        var war = Raider(s); war.Champion = true; war.Trait = ChampionTrait.Warbringer;
        war.MaxHealth = 100f; war.Health = 100f;
        Assert.Equal(1f, Champions.EnrageSpeed(war), 3); // full HP, no enrage yet
        war.Health = 20f;
        Assert.True(Champions.EnrageSpeed(war) > 1f);    // hurt -> faster

        var iron = Raider(s); iron.Champion = true; iron.Trait = ChampionTrait.Ironhide;
        iron.MaxHealth = 100f; iron.Health = 10f;
        Assert.Equal(1f, Champions.EnrageSpeed(iron), 3); // only Warbringer enrages
    }

    [Fact]
    public void KillingAChampion_DropsEmber_AndChargesFury()
    {
        var s = new GameState(seedDebug: false);
        var e = Raider(s, 5f);
        Champions.Promote(s, e);
        e.Health = 5f; e.MaxHealth = 5f; // make it killable in one hit for the test
        float furyBefore = s.Fury;
        CombatSystem.DamageEnemy(s, e, 9999f);
        Assert.Contains(s.Drops, d => d.Kind == DropKind.Ember);
        Assert.True(s.Fury > furyBefore);
    }

    [Fact]
    public void DeepWave_FieldsExactlyOneChampion()
    {
        var s = new GameState(seedDebug: false) { Wave = 14 };
        s.NextWaveKinds = WaveSystem.BuildComposition(s, 14);
        WaveSystem.StartWave(s);
        for (int i = 0; i < 4000 && s.Spawning is not null; i++) WaveSystem.Update(s, 0.2f);
        Assert.Equal(1, s.Enemies.Count(e => e.Champion));
    }

    [Fact]
    public void EarlyWaves_HaveNoChampions()
    {
        var s = new GameState(seedDebug: false) { Wave = 8 };
        s.NextWaveKinds = WaveSystem.BuildComposition(s, 8);
        WaveSystem.StartWave(s);
        for (int i = 0; i < 4000 && s.Spawning is not null; i++) WaveSystem.Update(s, 0.2f);
        Assert.DoesNotContain(s.Enemies, e => e.Champion);
    }
}
