using System.Numerics;
using Emberhold.Game;
using Xunit;

namespace Emberhold.Tests;

/// <summary>Last Stand: a critically-wounded keep pulses a defensive nova.</summary>
public class LastStandTests
{
    private static Enemy Foe(GameState s, Vector2 pos, float hp = 400f, bool boss = false) =>
        new() { Id = s.NextId(), Pos = pos, Radius = 11, Health = hp, MaxHealth = hp, SlowFactor = 1f, Boss = boss };

    [Fact]
    public void Active_OnlyBelowThreshold()
    {
        var s = new GameState(seedDebug: false) { KeepMaxHealth = 100f };
        s.KeepHealth = 40f;
        Assert.False(LastStand.Active(s));      // above 30%
        s.KeepHealth = 20f;
        Assert.True(LastStand.Active(s));        // below 30%
        s.KeepHealth = 0f;
        Assert.False(LastStand.Active(s));       // dead keep is not "last stand"
    }

    [Fact]
    public void Nova_DamagesAndSlowsNearbyFoes_BossesResist()
    {
        var s = new GameState(seedDebug: false) { Wave = 12 };
        var near = Foe(s, new Vector2(40, 0));
        var far = Foe(s, new Vector2(600, 0));
        var boss = Foe(s, new Vector2(40, 20), boss: true);
        s.Enemies.Add(near); s.Enemies.Add(far); s.Enemies.Add(boss);

        LastStand.Nova(s);

        Assert.True(near.Health < 400f);
        Assert.True(near.SlowTimer > 0f);       // chilled by the nova
        Assert.Equal(400f, far.Health);          // out of range
        float bossLoss = 400f - boss.Health;
        float nearLoss = 400f - near.Health;
        Assert.True(bossLoss < nearLoss);        // bosses take reduced nova damage
    }

    [Fact]
    public void Update_AnnouncesOnEntry_PulsesOverTime_ResetsOnRecovery()
    {
        var s = new GameState(seedDebug: false) { KeepMaxHealth = 100f, KeepHealth = 18f };
        var foe = Foe(s, new Vector2(30, 0));
        s.Enemies.Add(foe);

        LastStand.Update(s, 0.016f);
        Assert.True(s.LastStandAnnounced);
        Assert.Equal("THE KEEP RALLIES", s.BannerText);

        // Tick until the first pulse fires and damages the nearby foe.
        for (int i = 0; i < 200 && foe.Health >= 400f; i++) LastStand.Update(s, 0.05f);
        Assert.True(foe.Health < 400f);

        // Healing the keep above the threshold clears the Last Stand state.
        s.KeepHealth = 90f;
        LastStand.Update(s, 0.016f);
        Assert.False(s.LastStandAnnounced);
    }
}
