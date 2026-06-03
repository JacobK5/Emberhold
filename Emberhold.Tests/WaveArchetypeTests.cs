using Emberhold.Data;
using Emberhold.Game;
using Xunit;

namespace Emberhold.Tests;

/// <summary>Wave archetypes: deterministic rolling, reshaped composition, scaled raiders.</summary>
public class WaveArchetypeTests
{
    private static int SaltFor(int wave, WaveArchetype want)
    {
        for (int salt = 1; salt < 500_000; salt++)
            if (WaveArchetypes.For(wave, salt) == want) return salt;
        throw new Xunit.Sdk.XunitException($"no salt yields {want} at wave {wave}");
    }

    private static GameState WaveState(int wave, WaveArchetype arch)
    {
        var s = new GameState(seedDebug: false) { Wave = wave, ArchetypeSalt = SaltFor(wave, arch) };
        s.NextWaveKinds = WaveSystem.BuildComposition(s, wave);
        return s;
    }

    /// <summary>Fully spawn a wave's roster (no enemy AI/death) and return the raiders.</summary>
    private static List<Enemy> SpawnAll(GameState s)
    {
        WaveSystem.StartWave(s);
        for (int i = 0; i < 4000 && s.Spawning is not null; i++) WaveSystem.Update(s, 0.2f);
        return s.Enemies.ToList();
    }

    [Fact]
    public void For_IsDeterministic_AndGatedByDepthAndBossWaves()
    {
        Assert.Equal(WaveArchetype.Normal, WaveArchetypes.For(7, 12345));   // too early
        Assert.Equal(WaveArchetype.Normal, WaveArchetypes.For(20, 12345));  // boss wave
        Assert.Equal(WaveArchetypes.For(13, 999), WaveArchetypes.For(13, 999)); // stable
    }

    [Fact]
    public void For_CanProduceEverySpecialArchetype()
    {
        foreach (var a in new[] { WaveArchetype.Swarm, WaveArchetype.Juggernaut, WaveArchetype.Flight, WaveArchetype.Frenzy })
            Assert.Equal(a, WaveArchetypes.For(12, SaltFor(12, a)));
    }

    [Fact]
    public void Swarm_HasMoreRaidersThanNormal_Juggernaut_HasFewer()
    {
        int normal = WaveState(12, WaveArchetype.Normal).NextWaveKinds!.Count;
        int swarm = WaveState(12, WaveArchetype.Swarm).NextWaveKinds!.Count;
        int jugg = WaveState(12, WaveArchetype.Juggernaut).NextWaveKinds!.Count;
        Assert.True(swarm > normal, $"swarm {swarm} !> normal {normal}");
        Assert.True(jugg < normal, $"jugg {jugg} !< normal {normal}");
    }

    [Fact]
    public void Swarm_IsMostlyRunnersAndRaiders()
    {
        var kinds = WaveState(13, WaveArchetype.Swarm).NextWaveKinds!;
        int light = kinds.Count(k => k is EnemyKind.Runner or EnemyKind.Raider);
        Assert.True(light >= kinds.Count * 0.8, $"only {light}/{kinds.Count} light");
    }

    [Fact]
    public void Juggernaut_IsMostlyBrutesAndSiege()
    {
        var kinds = WaveState(13, WaveArchetype.Juggernaut).NextWaveKinds!;
        int heavy = kinds.Count(k => k is EnemyKind.Brute or EnemyKind.Siege);
        Assert.True(heavy >= kinds.Count * 0.8, $"only {heavy}/{kinds.Count} heavy");
    }

    [Fact]
    public void Swarm_Raiders_AreSmallerThanTheirProfile()
    {
        var enemies = SpawnAll(WaveState(12, WaveArchetype.Swarm));
        // Every rank-and-file swarm raider has the 0.8x radius mod applied.
        Assert.All(enemies.Where(e => !e.Elite && !e.Boss && !e.General),
            e => Assert.True(e.Radius < EnemyProfile.Get(e.Kind).Radius));
    }
}
