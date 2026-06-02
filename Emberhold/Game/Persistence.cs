using System.Text.Json;

namespace Emberhold.Game;

/// <summary>Cross-run profile saved to disk: bests plus lifetime tallies + collection.</summary>
public sealed record Profile
{
    public int BestWave { get; init; } = 1;
    public int BestKills { get; init; }
    public int Runs { get; init; }
    public int LifetimeKills { get; init; }
    public int BossesSlain { get; init; }
    public HashSet<string> DiscoveredSynergies { get; init; } = new();
}

/// <summary>
/// Loads/saves the player profile under the OS local-app-data folder. All IO is
/// best-effort: failures fall back to defaults so the game never crashes on a
/// missing or unreadable save.
/// </summary>
public static class Persistence
{
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Emberhold");
    private static readonly string FilePath = Path.Combine(Dir, "profile.json");

    public static Profile Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Profile>(File.ReadAllText(FilePath)) ?? new Profile();
        }
        catch { /* fall through to default */ }
        return new Profile();
    }

    public static void Save(Profile profile)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* best-effort; ignore */ }
    }

    /// <summary>Merge a finished run into the profile, keeping bests + lifetime tallies, and persist.</summary>
    public static Profile Record(Profile current, int wave, int kills, int bossesSlain, IEnumerable<string> synergies)
    {
        var discovered = new HashSet<string>(current.DiscoveredSynergies);
        discovered.UnionWith(synergies);
        var updated = current with
        {
            BestWave = Math.Max(current.BestWave, wave),
            BestKills = Math.Max(current.BestKills, kills),
            Runs = current.Runs + 1,
            LifetimeKills = current.LifetimeKills + kills,
            BossesSlain = current.BossesSlain + bossesSlain,
            DiscoveredSynergies = discovered,
        };
        Save(updated);
        return updated;
    }
}
