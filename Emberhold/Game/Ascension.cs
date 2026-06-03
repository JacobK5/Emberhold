namespace Emberhold.Game;

/// <summary>
/// Ascension difficulty tiers (Hades-style). Each level you unlock adds a cumulative
/// rule; you pick how high to climb at run start. All effects are pure functions of the
/// level so the run state just stores an int. Beating wave 10 (a boss) at your current
/// ceiling unlocks the next tier.
/// </summary>
public static class Ascensions
{
    public const int Cap = 5;

    public static int Clamp(int level) => Math.Clamp(level, 0, Cap);

    // Cumulative difficulty multipliers by level.
    public static float EnemyHp(int a)    => 1f + Clamp(a) * 0.12f;                 // +12% HP / level
    public static float EnemySpeed(int a) => 1f + Math.Max(0, Clamp(a) - 2) * 0.05f; // +5% speed from A3
    public static float KeepMult(int a)   => 1f - Math.Min(Clamp(a), 4) * 0.05f;     // up to -20% keep HP
    public static float PriceMult(int a)  => 1f + Clamp(a) * 0.06f;                  // +6% shop prices / level

    /// <summary>One-line summary of a level's stacked rules, for the select screen.</summary>
    public static string Summary(int a)
    {
        a = Clamp(a);
        if (a == 0) return "standard difficulty";
        var parts = new List<string> { $"enemies +{(EnemyHp(a) - 1f) * 100f:0}% HP" };
        if (EnemySpeed(a) > 1f) parts.Add($"+{(EnemySpeed(a) - 1f) * 100f:0}% speed");
        if (KeepMult(a) < 1f) parts.Add($"keep {(KeepMult(a) - 1f) * 100f:0}% HP");
        parts.Add($"shop +{(PriceMult(a) - 1f) * 100f:0}%");
        return string.Join("  ·  ", parts);
    }
}
