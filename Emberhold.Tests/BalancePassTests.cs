using System.Numerics;
using Emberhold.Data;
using Emberhold.Game;
using Xunit;

namespace Emberhold.Tests;

/// <summary>The Counterweight balance pass: trap/flyer counterplay, wall mending,
/// and the stacked fire-rate floor.</summary>
public class BalancePassTests
{
    [Fact]
    public void GroundTraps_IgnoreFlyers_ButHitGroundedRaiders()
    {
        var s = new GameState(seedDebug: false);
        var trap = StructureFactory.Create(s, CardDb.Get("spike_trap"), new Vector2(0, 95));
        s.Structures.Add(trap);

        var raider = new Enemy { Id = s.NextId(), Pos = trap.Pos, Radius = 11, Health = 100, MaxHealth = 100, SlowFactor = 1f };
        var flyer = new Enemy { Id = s.NextId(), Pos = trap.Pos, Radius = 10, Health = 100, MaxHealth = 100, SlowFactor = 1f, Flying = true };
        s.Enemies.Add(raider); s.Enemies.Add(flyer);

        DefenseSystem.Update(s, 1f);

        Assert.True(raider.Health < 100f, "grounded raider should take trap damage");
        Assert.Equal(100f, flyer.Health);   // flyers pass over ground traps
    }

    [Fact]
    public void Walls_MendBetweenWaves()
    {
        var s = new GameState(seedDebug: false);
        var wall = StructureFactory.Create(s, CardDb.Get("barricade"), new Vector2(0, -95));
        s.Structures.Add(wall);
        wall.Health = wall.MaxHealth * 0.25f; // battered but standing

        // Simulate the wave-clear beat: no spawner, field empty, bonus pending.
        s.Spawning = null;
        s.WaveBonusPending = true;
        WaveSystem.Update(s, 0.016f);

        // 25% + 40% of the missing 75% = 55% of max.
        Assert.Equal(wall.MaxHealth * 0.55f, wall.Health, 1);
    }

    [Fact]
    public void TowerFireInterval_HasAFloor_UnderStackedRateBuffs()
    {
        var s = new GameState(seedDebug: false);
        var tower = StructureFactory.Create(s, CardDb.Get("archer_post"), new Vector2(84, -84));
        tower.Rate = 0.05f; // absurdly fast on paper (stacked buffs)
        s.Structures.Add(tower);
        s.Enemies.Add(new Enemy { Id = s.NextId(), Pos = tower.Pos + new Vector2(40, 0), Radius = 11, Health = 100, MaxHealth = 100, SlowFactor = 1f });

        TowerSystem.Update(s, 0.016f); // fires once and re-arms

        Assert.True(tower.Cooldown >= 0.15f - 1e-4f, $"cooldown {tower.Cooldown} should respect the 0.15s floor");
    }
}
