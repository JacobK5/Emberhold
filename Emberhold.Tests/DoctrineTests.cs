using System.Numerics;
using Emberhold.Data;
using Emberhold.Game;
using Xunit;

namespace Emberhold.Tests;

/// <summary>War Doctrines (per-boss horde buffs) + the hero combo system.</summary>
public class DoctrineTests
{
    [Fact]
    public void Roll_IsDeterministic_AndNeverRepeatsAcrossTheLadder()
    {
        const int salt = 777;
        var ladder = Enumerable.Range(0, Doctrines.All.Length)
            .Select(i => Doctrines.Roll(salt, i)).ToList();
        Assert.Equal(Doctrines.All.Length, ladder.Distinct().Count()); // a full shuffle
        // Same salt -> same ladder (preview == resume == actual).
        for (int i = 0; i < ladder.Count; i++)
            Assert.Equal(ladder[i], Doctrines.Roll(salt, i));
    }

    [Fact]
    public void WaveClear_OnBossWave_AdoptsADoctrine()
    {
        var s = new GameState(seedDebug: false) { Wave = 10 };
        s.Spawning = null;
        s.WaveBonusPending = true;
        WaveSystem.Update(s, 0.016f);
        Assert.Single(s.Doctrines);

        // Non-boss clears don't add more.
        s.WaveBonusPending = true; // s.Wave is now 11
        WaveSystem.Update(s, 0.016f);
        Assert.Single(s.Doctrines);
    }

    [Fact]
    public void Doctrines_ScaleSpawns()
    {
        var baseline = new GameState(seedDebug: false) { Wave = 12 };
        var buffed = new GameState(seedDebug: false) { Wave = 12 };
        buffed.Doctrines.Add(DoctrineKind.Phalanx);
        buffed.Doctrines.Add(DoctrineKind.Swift);
        buffed.Doctrines.Add(DoctrineKind.Berserkers);

        // Multipliers are pure functions of the owned set.
        Assert.Equal(1.12f, Doctrines.HpMult(buffed.Doctrines), 2);
        Assert.Equal(1.10f, Doctrines.SpeedMult(buffed.Doctrines), 2);
        Assert.Equal(1.15f, Doctrines.DamageMult(buffed.Doctrines), 2);
        Assert.Equal(1f, Doctrines.HpMult(baseline.Doctrines), 2);
    }

    [Fact]
    public void FrostWard_WeakensEffectiveSlow()
    {
        var owned = new List<DoctrineKind> { DoctrineKind.FrostWard };
        // A 50% slow becomes a 35% slow under Frost Ward.
        Assert.Equal(0.65f, Doctrines.ApplySlowResist(owned, 0.5f), 2);
        // No doctrine: unchanged.
        Assert.Equal(0.5f, Doctrines.ApplySlowResist(new List<DoctrineKind>(), 0.5f), 2);
    }

    [Fact]
    public void Doctrines_RoundTripThroughSaves()
    {
        var s = new GameState(seedDebug: false);
        s.Doctrines.Add(DoctrineKind.Siegecraft);
        s.Doctrines.Add(DoctrineKind.Swift);
        var restored = RunStore.FromJson(RunStore.ToJson(RunStore.Capture(s)))!;
        var t = new GameState(seedDebug: false);
        RunStore.Apply(t, restored);
        Assert.Equal(new[] { DoctrineKind.Siegecraft, DoctrineKind.Swift }, t.Doctrines);
    }

    [Fact]
    public void DashAcrossASlowTrap_IgnitesIt_AndItBurnsEnemies()
    {
        var s = new GameState(seedDebug: false);
        var tar = StructureFactory.Create(s, CardDb.Get("tar_pit"), new Vector2(0, 60));
        s.Structures.Add(tar);

        // Dash straight through the tar pit.
        s.Hero.Pos = new Vector2(0, 10);
        s.Hero.Facing = new Vector2(0, 1);
        CombatSystem.Dash(s);
        Assert.True(tar.ComboBurnTimer > 0f, "the dash should ignite the tar pit");

        // An enemy standing in the burning tar catches fire.
        var e = new Enemy { Id = s.NextId(), Pos = tar.Pos, Radius = 11, Health = 100, MaxHealth = 100, SlowFactor = 1f };
        s.Enemies.Add(e);
        DefenseSystem.Update(s, 0.1f);
        Assert.True(e.BurnTimer > 0f && e.BurnDps > 0f);

        // The ignition burns out over time.
        for (int i = 0; i < 40; i++) DefenseSystem.Update(s, 0.1f);
        Assert.Equal(0f, tar.ComboBurnTimer);
    }

    [Fact]
    public void KillsDuringOverdrive_ExtendIt()
    {
        var s = new GameState(seedDebug: false);
        s.Hero.Overdrive = 2f;
        var e = new Enemy { Id = s.NextId(), Pos = Vector2.Zero, Radius = 10, Health = 1f, MaxHealth = 10f, SlowFactor = 1f };
        s.Enemies.Add(e);
        CombatSystem.DamageEnemy(s, e, 50f);
        Assert.True(e.Dead);
        Assert.True(s.Hero.Overdrive > 2f, "the kill should stoke Overdrive");
    }
}
