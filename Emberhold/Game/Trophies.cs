namespace Emberhold.Game;

/// <summary>A lifetime achievement: earned once, kept forever on the profile.</summary>
public sealed record TrophyDef(string Id, string Name, string Desc, Func<Profile, GameState, bool> Earned);

/// <summary>
/// Trophy catalog. Conditions are evaluated at game-over against the just-updated
/// profile + the finished run's state, so both lifetime ("1000 kills") and
/// single-run ("all four relics in one run") feats work. Earned ids persist on
/// the profile; the title screen shows the hall, the death recap calls out new ones.
/// </summary>
public static class Trophies
{
    public static readonly IReadOnlyList<TrophyDef> Catalog = new TrophyDef[]
    {
        new("hold_the_line", "Hold the Line", "Reach wave 6",
            (p, s) => p.BestWave >= 6),
        new("boss_slayer", "Boss Slayer", "Slay a chapter boss",
            (p, s) => p.BossesSlain >= 1),
        new("quartermaster", "Quartermaster's Favor", "Reach wave 15  ·  perk: +10 starting gold",
            (p, s) => p.BestWave >= 15),
        new("deep_frontier", "Deep Frontier", "Reach wave 25",
            (p, s) => p.BestWave >= 25),
        new("synergist", "Synergist", "Trigger 5 synergies in one run",
            (p, s) => s.SeenSynergies.Count >= 5),
        new("relic_hunter", "Relic Hunter", "Carry all four relics in one run",
            (p, s) => s.Hero.Relics.Count >= 4),
        new("exotic_taste", "Exotic Taste", "Own 3 exotics in one run",
            (p, s) => s.Exotics.Count >= 3),
        new("doctrine_survivor", "Doctrine Survivor", "Survive 3 horde doctrines in one run",
            (p, s) => s.Doctrines.Count >= 3),
        new("centurion", "Centurion", "1,000 lifetime kills",
            (p, s) => p.LifetimeKills >= 1000),
        new("ascendant", "Ascendant", "Unlock ascension tier 3",
            (p, s) => p.MaxAscension >= 3),
        new("codex_scholar", "Codex Scholar", "Discover 12 synergies lifetime",
            (p, s) => p.DiscoveredSynergies.Count >= 12),
        new("old_guard", "Old Guard", "Finish 10 runs",
            (p, s) => p.Runs >= 10),
    };

    /// <summary>Catalog entries earned by this run that the profile doesn't have yet.</summary>
    public static List<TrophyDef> EvaluateNew(Profile profile, GameState s)
    {
        var fresh = new List<TrophyDef>();
        foreach (var def in Catalog)
            if (!profile.Trophies.Contains(def.Id) && def.Earned(profile, s))
                fresh.Add(def);
        return fresh;
    }

    /// <summary>Perk: Quartermaster's Favor grants extra starting gold to new runs.</summary>
    public static int StartingGold(Profile profile)
        => profile.Trophies.Contains("quartermaster") || profile.BestWave >= 15 ? 30 : 20;
}
