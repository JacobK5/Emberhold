using System.Numerics;
using Emberhold.Data;
using Emberhold.Game;
using Xunit;

namespace Emberhold.Tests;

/// <summary>Hero progression added in the Champions batch: passives + relic drops.</summary>
public class HeroTests
{
    [Theory]
    [InlineData(1, false, false, false)]
    [InlineData(3, true, false, false)]
    [InlineData(5, true, true, false)]
    [InlineData(7, true, true, true)]
    public void Passives_UnlockAtLevels(int level, bool quickHands, bool signature, bool secondWind)
    {
        var h = new Hero { Level = level };
        Assert.Equal(quickHands, h.QuickHands);
        Assert.Equal(signature, h.Signature);
        Assert.Equal(secondWind, h.SecondWind);
    }

    [Fact]
    public void QuickHands_WidensPickupRadius()
    {
        var lo = new Hero { Level = 1 };
        var hi = new Hero { Level = 3 };
        Assert.True(hi.PickupRadius > lo.PickupRadius);
    }

    [Fact]
    public void EliteDeath_DropsRelic_WhenSetIncomplete()
    {
        var s = new GameState(seedDebug: false);
        var e = new Enemy { Id = s.NextId(), Health = 1, MaxHealth = 10, Elite = true, Reward = 1, Pos = Vector2.Zero };
        s.Enemies.Add(e);
        CombatSystem.DamageEnemy(s, e, 50f);
        Assert.Contains(s.Drops, d => d.Kind == DropKind.Relic);
    }

    [Fact]
    public void Rally_SpendsGold_SlowsWave_ExceptWraiths()
    {
        var s = new GameState(seedDebug: false) { Gold = 200, Wave = 1 };
        var normal = new Enemy { Id = s.NextId(), Health = 100, MaxHealth = 100, Radius = 11, Pos = Vector2.Zero };
        var wraith = new Enemy { Id = s.NextId(), Health = 100, MaxHealth = 100, Radius = 12, StatusImmune = true, Pos = Vector2.Zero };
        s.Enemies.Add(normal);
        s.Enemies.Add(wraith);

        int cost = s.RallyCost;
        Assert.True(s.TryRally());
        Assert.Equal(200 - cost, s.Gold);
        Assert.True(s.RallyCooldown > 0f);
        Assert.True(normal.SlowTimer > 0f);
        Assert.Equal(0f, wraith.SlowTimer);
    }

    [Fact]
    public void Rally_FailsWithoutEnoughGold()
    {
        var s = new GameState(seedDebug: false) { Gold = 0, Wave = 1 };
        Assert.False(s.TryRally());
        Assert.Equal(0f, s.RallyCooldown);
    }

    [Fact]
    public void Artificer_RepairsNearbyDamagedStructures()
    {
        var s = new GameState(seedDebug: false);
        s.Hero.Kind = HeroKind.Artificer;
        s.Hero.Pos = Vector2.Zero;
        var tower = StructureFactory.Create(s, CardDb.Get("archer_post"), new Vector2(40, 0));
        tower.Health = 10f;
        s.Structures.Add(tower);
        DefenseSystem.Update(s, 0.5f);
        Assert.True(tower.Health > 10f);
    }

    [Fact]
    public void NonArtificer_DoesNotRepairStructures()
    {
        var s = new GameState(seedDebug: false);
        s.Hero.Kind = HeroKind.Ranger;
        var tower = StructureFactory.Create(s, CardDb.Get("archer_post"), new Vector2(40, 0));
        tower.Health = 10f;
        s.Structures.Add(tower);
        DefenseSystem.Update(s, 0.5f);
        Assert.Equal(10f, tower.Health);
    }

    [Fact]
    public void EliteDeath_NoRelic_WhenAllOwned()
    {
        var s = new GameState(seedDebug: false);
        foreach (RelicKind k in System.Enum.GetValues<RelicKind>()) s.Hero.Relics.Add(k);
        var e = new Enemy { Id = s.NextId(), Health = 1, MaxHealth = 10, Elite = true, Reward = 1, Pos = Vector2.Zero };
        s.Enemies.Add(e);
        CombatSystem.DamageEnemy(s, e, 50f);
        Assert.DoesNotContain(s.Drops, d => d.Kind == DropKind.Relic);
    }
}
