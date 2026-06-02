using System.Numerics;
using Emberhold.Data;
using Emberhold.Game;
using Xunit;

namespace Emberhold.Tests;

public class EnemyTests
{
    [Fact]
    public void Shield_ReducesDirectHits_ButNotDoT()
    {
        var s = new GameState(seedDebug: false);
        var e = new Enemy { Health = 100, MaxHealth = 100, ShieldPerHit = 8f, Radius = 11f };
        s.Enemies.Add(e);

        CombatSystem.DamageEnemy(s, e, 20f);                    // mitigable: 20 - 8 = 12
        Assert.Equal(88f, e.Health, 2);

        CombatSystem.DamageEnemy(s, e, 20f, mitigable: false);  // DoT/trap bypasses shield
        Assert.Equal(68f, e.Health, 2);
    }

    [Fact]
    public void Shield_NeverReducesBelowOne()
    {
        var s = new GameState(seedDebug: false);
        var e = new Enemy { Health = 100, MaxHealth = 100, ShieldPerHit = 50f, Radius = 11f };
        s.Enemies.Add(e);
        CombatSystem.DamageEnemy(s, e, 3f); // 3 - 50 floored to 1
        Assert.Equal(99f, e.Health, 2);
    }

    [Fact]
    public void Healer_MendsNearbyWoundedEnemies()
    {
        var s = new GameState(seedDebug: false);
        var healer = new Enemy { Health = 50, MaxHealth = 50, Healer = true, Radius = 12f, Pos = new Vector2(200, 0) };
        var hurt = new Enemy { Health = 10, MaxHealth = 50, Radius = 11f, Pos = new Vector2(230, 0) };
        s.Enemies.Add(healer);
        s.Enemies.Add(hurt);

        EnemySystem.Update(s, 0.016f); // HealTimer starts at 0 -> heals on first tick

        Assert.True(hurt.Health > 10f, "healer should restore nearby wounded health");
    }

    [Fact]
    public void Composition_HasEliteEveryFifthWave_AndScalesCount()
    {
        var s = new GameState(seedDebug: false);
        Assert.Contains(EnemyKind.Elite, WaveSystem.BuildComposition(s, 5));
        Assert.DoesNotContain(EnemyKind.Elite, WaveSystem.BuildComposition(s, 4));
        Assert.True(WaveSystem.BuildComposition(s, 12).Count > WaveSystem.BuildComposition(s, 1).Count);
    }

    [Fact]
    public void PreviewLine_SummarizesSpecialThreats()
    {
        var kinds = new List<EnemyKind> { EnemyKind.Raider, EnemyKind.Raider, EnemyKind.Siege, EnemyKind.Elite };
        string line = WaveSystem.PreviewLine(kinds);
        Assert.Contains("4 incoming", line);
        Assert.Contains("Siege x1", line);
        Assert.Contains("Elite x1", line);
        Assert.Equal("", WaveSystem.PreviewLine(null));
    }

    [Fact]
    public void Composition_BossEveryTenthWave_ReplacesElite()
    {
        var s = new GameState(seedDebug: false);
        var w10 = WaveSystem.BuildComposition(s, 10);
        Assert.Contains(EnemyKind.Boss, w10);
        Assert.DoesNotContain(EnemyKind.Elite, w10);

        var w5 = WaveSystem.BuildComposition(s, 5);
        Assert.Contains(EnemyKind.Elite, w5);
        Assert.DoesNotContain(EnemyKind.Boss, w5);
    }

    [Fact]
    public void BossDeath_RampsHorde_AndDropsReward()
    {
        var s = new GameState(seedDebug: false);
        var boss = new Enemy { Id = s.NextId(), Health = 1, MaxHealth = 100, Boss = true, Reward = 14, Radius = 25, Pos = Vector2.Zero };
        s.Enemies.Add(boss);
        int tier0 = s.HordeTier;
        CombatSystem.DamageEnemy(s, boss, 50f);
        Assert.Equal(tier0 + 1, s.HordeTier);
        Assert.Contains(s.Drops, d => d.Kind == DropKind.Relic);
    }
}
