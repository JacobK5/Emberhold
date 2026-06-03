using System.Numerics;
using Emberhold.Game;
using Xunit;

namespace Emberhold.Tests;

/// <summary>Dynamic late-game map events: rolling gates, meteor impacts, boons.</summary>
public class MapEventTests
{
    [Fact]
    public void Roll_SkipsEarlyWavesBossWavesAndRecentEvents()
    {
        var s = new GameState(seedDebug: false);

        s.Wave = 5; s.WavesSinceEvent = 99;                 // too early
        MapEventSystem.RollForNextWave(s);
        Assert.Equal(MapEventKind.None, s.PendingEvent);

        s.Wave = 10; s.WavesSinceEvent = 99;                // boss wave
        MapEventSystem.RollForNextWave(s);
        Assert.Equal(MapEventKind.None, s.PendingEvent);

        s.Wave = 12; s.WavesSinceEvent = 1;                 // too soon after the last
        MapEventSystem.RollForNextWave(s);
        Assert.Equal(MapEventKind.None, s.PendingEvent);
    }

    [Fact]
    public void Roll_EventuallyProducesAnEvent_WhenEligible()
    {
        var s = new GameState(seedDebug: false);
        bool any = false;
        for (int i = 0; i < 300 && !any; i++)
        {
            s.Wave = 12; s.WavesSinceEvent = 99;
            MapEventSystem.RollForNextWave(s);
            any = s.PendingEvent != MapEventKind.None;
        }
        Assert.True(any);
    }

    [Fact]
    public void Meteor_Impact_DamagesEnemiesInRadius_BypassingShields()
    {
        var s = new GameState(seedDebug: false);
        var hit = new Enemy { Id = s.NextId(), Pos = Vector2.Zero, Radius = 11, Health = 100, MaxHealth = 100, SlowFactor = 1f, ShieldPerHit = 40f };
        var far = new Enemy { Id = s.NextId(), Pos = new Vector2(400, 0), Radius = 11, Health = 100, MaxHealth = 100, SlowFactor = 1f };
        s.Enemies.Add(hit);
        s.Enemies.Add(far);

        // A meteor about to land on the origin; Update resolves it.
        s.Meteors.Add(new Meteor { Target = Vector2.Zero, Fall = 0.01f, MaxFall = 1.15f, Radius = 46f, Damage = 50f });
        MapEventSystem.Update(s, 0.05f);

        Assert.True(hit.Health < 100f);     // caught in the blast (shield ignored)
        Assert.Equal(100f, far.Health);     // outside the radius
        Assert.Empty(s.Meteors);            // resolved + removed
    }

    [Fact]
    public void SupplyDrop_Activation_LandsFreeStructures()
    {
        var s = new GameState(seedDebug: false);
        int before = s.Structures.Count;
        s.PendingEvent = MapEventKind.SupplyDrop;
        MapEventSystem.Activate(s);
        Assert.True(s.Structures.Count > before);
        Assert.Equal(MapEventKind.None, s.ActiveEvent); // instant boon, not a lingering state
    }

    [Fact]
    public void GoldRush_DoublesKillBounty()
    {
        int Bounty(bool rush)
        {
            var s = new GameState(seedDebug: false);
            s.GoldRushActive = rush;
            var e = new Enemy { Id = s.NextId(), Pos = Vector2.Zero, Radius = 11, Health = 1, MaxHealth = 1, SlowFactor = 1f, Reward = 5 };
            s.Enemies.Add(e);
            int drops = s.Drops.Count;
            CombatSystem.DamageEnemy(s, e, 999f);
            return s.Drops.Count - drops; // each gold coin is one Drop
        }
        Assert.Equal(Bounty(false) * 2, Bounty(true));
    }
}
