using System.Numerics;
using Emberhold.Game;
using Xunit;

namespace Emberhold.Tests;

/// <summary>The Fury ultimate: kill-charged meter + the Cataclysm detonation.</summary>
public class FuryTests
{
    private static Enemy Foe(GameState s, Vector2 pos, float hp = 50f)
    {
        var e = new Enemy { Id = s.NextId(), Pos = pos, Radius = 11, Health = hp, MaxHealth = hp, SlowFactor = 1f, Reward = 1 };
        s.Enemies.Add(e);
        return e;
    }

    [Fact]
    public void Fury_ChargesFromKills_AndCapsAtFull()
    {
        var s = new GameState(seedDebug: false);
        Assert.False(s.FuryReady);
        for (int i = 0; i < 40; i++)
            CombatSystem.DamageEnemy(s, Foe(s, Vector2.Zero, 1f), 999f); // each kill adds fury
        Assert.True(s.FuryReady);
        Assert.Equal(1f, s.Fury, 3); // clamped, never above full
    }

    [Fact]
    public void Ultimate_OnlyFiresWhenReady()
    {
        var s = new GameState(seedDebug: false);
        s.Fury = 0.9f;
        Assert.False(CombatSystem.Ultimate(s)); // not charged
        s.Fury = 1f;
        Assert.True(CombatSystem.Ultimate(s));
        Assert.Equal(0f, s.Fury, 3);            // meter spent
    }

    [Fact]
    public void Cataclysm_DamagesEnemiesInRadius_GrantsOverdrive_AndTriggersFx()
    {
        var s = new GameState(seedDebug: false);
        s.Hero.Pos = Vector2.Zero;
        var near = Foe(s, new Vector2(60, 0), 400f);   // inside the 240 radius
        var far = Foe(s, new Vector2(900, 0), 400f);   // well outside
        s.Fury = 1f;

        Assert.True(CombatSystem.Ultimate(s));
        Assert.True(near.Health < 400f, "near foe should take Cataclysm damage");
        Assert.Equal(400f, far.Health);               // untouched
        Assert.True(s.Hero.Overdrive > 0f);            // ult grants an Overdrive burst
        Assert.True(s.UltFxTimer > 0f);                // shockwave armed
    }
}
