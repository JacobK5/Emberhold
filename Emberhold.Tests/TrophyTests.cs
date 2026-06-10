using Emberhold.Game;
using Xunit;

namespace Emberhold.Tests;

/// <summary>Trophy catalog conditions + the Quartermaster starting-gold perk.</summary>
public class TrophyTests
{
    [Fact]
    public void EvaluateNew_AwardsOnlyUnownedEarnedTrophies()
    {
        var s = new GameState(seedDebug: false);
        s.SeenSynergies.UnionWith(new[] { "a", "b", "c", "d", "e" }); // Synergist feat
        var profile = new Profile { BestWave = 16, BossesSlain = 2 };

        var fresh = Trophies.EvaluateNew(profile, s);
        var ids = fresh.Select(t => t.Id).ToList();
        Assert.Contains("hold_the_line", ids);   // bestWave 16 >= 6
        Assert.Contains("boss_slayer", ids);
        Assert.Contains("quartermaster", ids);   // bestWave >= 15
        Assert.Contains("synergist", ids);       // 5 synergies this run
        Assert.DoesNotContain("deep_frontier", ids); // needs wave 25
        Assert.DoesNotContain("centurion", ids);

        // Already-owned trophies never re-award.
        var owned = profile with { Trophies = new HashSet<string>(ids) };
        Assert.Empty(Trophies.EvaluateNew(owned, s).Where(t => ids.Contains(t.Id)));
    }

    [Fact]
    public void RunFeats_AreCheckedAgainstTheRunState()
    {
        var s = new GameState(seedDebug: false);
        foreach (RelicKind r in Enum.GetValues<RelicKind>()) s.Hero.Relics.Add(r);
        s.Exotics.Add(ExoticKind.MotherLode);
        s.Exotics.Add(ExoticKind.AegisMatrix);
        s.Exotics.Add(ExoticKind.PhoenixHeart);
        s.Doctrines.Add(DoctrineKind.Swift);
        s.Doctrines.Add(DoctrineKind.Phalanx);
        s.Doctrines.Add(DoctrineKind.Berserkers);

        var ids = Trophies.EvaluateNew(new Profile(), s).Select(t => t.Id).ToList();
        Assert.Contains("relic_hunter", ids);
        Assert.Contains("exotic_taste", ids);
        Assert.Contains("doctrine_survivor", ids);
    }

    [Fact]
    public void StartingGold_GrantsQuartermasterPerk()
    {
        Assert.Equal(20, Trophies.StartingGold(new Profile()));
        Assert.Equal(30, Trophies.StartingGold(new Profile { BestWave = 15 }));
        Assert.Equal(30, Trophies.StartingGold(new Profile { Trophies = new HashSet<string> { "quartermaster" } }));
    }

    [Fact]
    public void CatalogIds_AreUnique()
    {
        Assert.Equal(Trophies.Catalog.Count, Trophies.Catalog.Select(t => t.Id).Distinct().Count());
    }
}
