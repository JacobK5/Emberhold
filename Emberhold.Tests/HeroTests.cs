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
