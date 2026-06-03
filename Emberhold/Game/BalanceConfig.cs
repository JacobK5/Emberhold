using System.Globalization;
using System.Reflection;

namespace Emberhold.Game;

/// <summary>One tunable field in <see cref="Balance"/>, with display + adjust metadata.</summary>
public sealed record BalanceEntry(string Field, string Label, string Group, float Step, float Min, float Max);

/// <summary>
/// Drives the in-game balancing panel: a registry over <see cref="Balance"/>'s static
/// float multipliers that can be read, nudged, reset to compiled defaults, and
/// exported/imported as a compact text string. Field access is reflective so the
/// list lives in one place; the compiled values are snapshotted as the defaults.
/// </summary>
public static class BalanceConfig
{
    /// <summary>Tunables in display order; the Group field also drives column layout.</summary>
    public static readonly BalanceEntry[] Entries =
    {
        new("EnemyHealthMult",   "Enemy HP",        "Enemies", 0.05f, 0.1f, 4f),
        new("EnemyDamageMult",   "Enemy Damage",    "Enemies", 0.05f, 0.1f, 4f),
        new("EnemySpeedMult",    "Enemy Speed",     "Enemies", 0.05f, 0.1f, 3f),
        new("EnemyCountMult",    "Enemy Count",     "Enemies", 0.05f, 0.1f, 3f),
        new("GoldRewardMult",    "Gold Reward",     "Enemies", 0.05f, 0.1f, 4f),

        new("HeroDamageMult",    "Hero Damage",     "Hero",    0.05f, 0.1f, 4f),
        new("HeroFireSpeedMult", "Hero Fire Speed", "Hero",    0.05f, 0.1f, 4f),
        new("HeroRangeMult",     "Hero Range",      "Hero",    0.05f, 0.1f, 3f),
        new("HeroSpeedMult",     "Hero Speed",      "Hero",    0.05f, 0.1f, 3f),

        new("TowerDamageMult",    "Tower Damage",    "Towers & Economy", 0.05f, 0.1f, 4f),
        new("TowerFireSpeedMult", "Tower Fire Speed","Towers & Economy", 0.05f, 0.1f, 4f),
        new("MineSpeedMult",      "Mine Speed",      "Towers & Economy", 0.05f, 0.1f, 4f),
        new("DepositBaseRate",    "Build Deposit",   "Towers & Economy", 0.2f,  0.5f, 12f),
        new("DepositSpeedMult",   "Build Speed",     "Towers & Economy", 0.05f, 0.1f, 4f),
    };

    /// <summary>Distinct groups in first-appearance order (drives the panel columns).</summary>
    public static readonly string[] Groups =
        Entries.Select(e => e.Group).Distinct().ToArray();

    // Snapshot the compiled values as defaults. This runs at class init, before any
    // Load() override is applied, so Reset() always returns to the shipped balance.
    private static readonly Dictionary<string, float> Defaults =
        Entries.ToDictionary(e => e.Field, e => Get(e.Field));

    private static readonly string FilePath = Path.Combine(Persistence.DataDir, "balance.cfg");

    private static FieldInfo Field(string name)
        => typeof(Balance).GetField(name, BindingFlags.Public | BindingFlags.Static)
           ?? throw new ArgumentException($"Unknown balance field: {name}");

    public static float Get(string field) => (float)Field(field).GetValue(null)!;
    private static void SetRaw(string field, float v) => Field(field).SetValue(null, v);

    /// <summary>Set a field, clamped to its entry's range; persists the change.</summary>
    public static void Set(string field, float value)
    {
        var e = Entries.First(x => x.Field == field);
        SetRaw(field, Math.Clamp(value, e.Min, e.Max));
        Save();
    }

    /// <summary>Nudge a field by N steps (sign = direction), clamped + persisted.</summary>
    public static void Adjust(BalanceEntry e, int steps)
        => Set(e.Field, MathF.Round((Get(e.Field) + e.Step * steps) / e.Step) * e.Step);

    /// <summary>Restore every field to its compiled default and persist.</summary>
    public static void Reset()
    {
        foreach (var kv in Defaults) SetRaw(kv.Key, kv.Value);
        Save();
    }

    public static bool IsDefault(string field)
        => MathF.Abs(Get(field) - Defaults[field]) < 1e-4f;

    /// <summary>Serialize all tunables to a compact "field=value;..." string.</summary>
    public static string Export()
        => string.Join(";", Entries.Select(e =>
            $"{e.Field}={Get(e.Field).ToString("0.###", CultureInfo.InvariantCulture)}"));

    /// <summary>Apply a previously exported string (unknown/invalid tokens are ignored).</summary>
    public static bool Import(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        bool any = false;
        foreach (var tok in text.Split(new[] { ';', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = tok.Split('=', 2);
            if (kv.Length != 2) continue;
            string field = kv[0].Trim();
            if (!Entries.Any(e => e.Field == field)) continue;
            if (float.TryParse(kv[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
            {
                var e = Entries.First(x => x.Field == field);
                SetRaw(field, Math.Clamp(v, e.Min, e.Max));
                any = true;
            }
        }
        if (any) Save();
        return any;
    }

    /// <summary>Persist current tunables to disk (best-effort).</summary>
    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Persistence.DataDir);
            File.WriteAllText(FilePath, Export());
        }
        catch { /* best-effort */ }
    }

    /// <summary>Load persisted tunables from disk if present (best-effort).</summary>
    public static void Load()
    {
        try
        {
            if (File.Exists(FilePath)) Import(File.ReadAllText(FilePath));
        }
        catch { /* fall back to compiled defaults */ }
    }
}
