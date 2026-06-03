using System.Numerics;
using System.Text.Json;
using Emberhold.Data;

namespace Emberhold.Game;

/// <summary>Serializable hero snapshot (omits transient combat timers + the Profile lookup).</summary>
public sealed class HeroSave
{
    public Vector2 Pos { get; set; }
    public HeroKind Kind { get; set; }
    public int Level { get; set; }
    public int Xp { get; set; }
    public int NextXp { get; set; }
    public float Health { get; set; }
    public float MaxHealth { get; set; }
    public float Damage { get; set; }
    public float FireRate { get; set; }
    public float Range { get; set; }
    public float VolleyCooldown { get; set; }
    public float VolleyDamage { get; set; }
    public int DmgUpgrades { get; set; }
    public int FrUpgrades { get; set; }
    public int RngUpgrades { get; set; }
    public int HpUpgrades { get; set; }
    public int VolleyUpgrades { get; set; }
    public float BasePickupRadius { get; set; }
    public List<RelicKind> Relics { get; set; } = new();
}

/// <summary>A funded-in-progress build pad (the card is referenced by id and re-resolved on load).</summary>
public sealed class PadSave
{
    public string CardId { get; set; } = "";
    public Vector2 Pos { get; set; }
    public int Invested { get; set; }
}

/// <summary>
/// A between-wave checkpoint of a run. Captured at the post-wave lull (no live
/// enemies), so it never has to serialize mid-flight combat entities.
/// </summary>
public sealed class RunSave
{
    public int Version { get; set; } = 1;
    public int Wave { get; set; }
    public int Chapter { get; set; }
    public int Gold { get; set; }
    public long GoldAccrued { get; set; }
    public float KeepHealth { get; set; }
    public float KeepMaxHealth { get; set; }
    public int HordeTier { get; set; }
    public float BetweenWaves { get; set; }
    public bool CodexAdept { get; set; }
    public string ModifierId { get; set; } = "none";
    public HeroSave Hero { get; set; } = new();
    public List<Structure> Structures { get; set; } = new();
    public List<PadSave> Pads { get; set; } = new();
    public List<string> SeenSynergies { get; set; } = new();
    public List<EnemyKind>? NextWaveKinds { get; set; }
    public List<EnemyKind>? NextWaveKinds2 { get; set; }
}

/// <summary>
/// Checkpoint save/load for an in-progress run, stored alongside the profile under
/// local-app-data. All IO is best-effort so the game never crashes on a bad file.
/// </summary>
public static class RunStore
{
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Emberhold");
    private static readonly string FilePath = Path.Combine(Dir, "run.json");

    // Structures use public fields; IncludeFields lets them (and Vector2) round-trip.
    private static readonly JsonSerializerOptions Options = new()
    {
        IncludeFields = true,
        WriteIndented = true,
    };

    public static bool Exists() => File.Exists(FilePath);

    public static void Delete()
    {
        try { if (File.Exists(FilePath)) File.Delete(FilePath); }
        catch { /* best-effort */ }
    }

    public static void Save(RunSave save)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, ToJson(save));
        }
        catch { /* best-effort; a failed autosave shouldn't interrupt play */ }
    }

    public static bool TryLoad(out RunSave save)
    {
        try
        {
            if (File.Exists(FilePath) && FromJson(File.ReadAllText(FilePath)) is RunSave s)
            {
                save = s;
                return true;
            }
        }
        catch { /* fall through */ }
        save = new RunSave();
        return false;
    }

    public static string ToJson(RunSave save) => JsonSerializer.Serialize(save, Options);
    public static RunSave? FromJson(string json) => JsonSerializer.Deserialize<RunSave>(json, Options);

    /// <summary>Snapshot the durable run state from a (between-wave) game state.</summary>
    public static RunSave Capture(GameState s)
    {
        var h = s.Hero;
        var save = new RunSave
        {
            Wave = s.Wave,
            Chapter = s.Chapter,
            Gold = s.Gold,
            GoldAccrued = s.GoldAccrued,
            KeepHealth = s.KeepHealth,
            KeepMaxHealth = s.KeepMaxHealth,
            HordeTier = s.HordeTier,
            BetweenWaves = s.BetweenWaves,
            CodexAdept = s.CodexAdept,
            ModifierId = s.Modifier.Id,
            Hero = new HeroSave
            {
                Pos = h.Pos,
                Kind = h.Kind,
                Level = h.Level,
                Xp = h.Xp,
                NextXp = h.NextXp,
                Health = h.Health,
                MaxHealth = h.MaxHealth,
                Damage = h.Damage,
                FireRate = h.FireRate,
                Range = h.Range,
                VolleyCooldown = h.VolleyCooldown,
                VolleyDamage = h.VolleyDamage,
                DmgUpgrades = h.DmgUpgrades,
                FrUpgrades = h.FrUpgrades,
                RngUpgrades = h.RngUpgrades,
                HpUpgrades = h.HpUpgrades,
                VolleyUpgrades = h.VolleyUpgrades,
                BasePickupRadius = h.BasePickupRadius,
                Relics = h.Relics.ToList(),
            },
            Structures = s.Structures.ToList(),
            Pads = s.Pads.Select(p => new PadSave { CardId = p.Def.Id, Pos = p.Pos, Invested = p.Invested }).ToList(),
            SeenSynergies = s.SeenSynergies.ToList(),
            NextWaveKinds = s.NextWaveKinds?.ToList(),
            NextWaveKinds2 = s.NextWaveKinds2?.ToList(),
        };
        return save;
    }

    /// <summary>Restore a captured run into a fresh game state, resuming between waves.</summary>
    public static void Apply(GameState s, RunSave save)
    {
        s.Wave = save.Wave;
        s.Chapter = save.Chapter;
        s.Gold = save.Gold;
        s.GoldAccrued = save.GoldAccrued;
        s.KeepHealth = save.KeepHealth;
        s.KeepMaxHealth = save.KeepMaxHealth;
        s.HordeTier = save.HordeTier;
        s.CodexAdept = save.CodexAdept;
        s.Modifier = RunModifier.Catalog.FirstOrDefault(m => m.Id == save.ModifierId) ?? RunModifier.None;

        var hs = save.Hero;
        var h = s.Hero;
        h.Pos = hs.Pos;
        h.Kind = hs.Kind;
        h.Level = hs.Level;
        h.Xp = hs.Xp;
        h.NextXp = hs.NextXp;
        h.Health = hs.Health;
        h.MaxHealth = hs.MaxHealth;
        h.Damage = hs.Damage;
        h.FireRate = hs.FireRate;
        h.Range = hs.Range;
        h.VolleyCooldown = hs.VolleyCooldown;
        h.VolleyDamage = hs.VolleyDamage;
        h.DmgUpgrades = hs.DmgUpgrades;
        h.FrUpgrades = hs.FrUpgrades;
        h.RngUpgrades = hs.RngUpgrades;
        h.HpUpgrades = hs.HpUpgrades;
        h.VolleyUpgrades = hs.VolleyUpgrades;
        h.BasePickupRadius = hs.BasePickupRadius;
        h.Relics.Clear();
        foreach (var r in hs.Relics) h.Relics.Add(r);

        s.Structures.Clear();
        s.Structures.AddRange(save.Structures);
        s.Pads.Clear();
        foreach (var p in save.Pads)
            s.Pads.Add(new Pad { Def = CardDb.Get(p.CardId), Pos = p.Pos, Invested = p.Invested });

        s.SeenSynergies.Clear();
        foreach (var id in save.SeenSynergies) s.SeenSynergies.Add(id);

        // Rebuild shop hero-upgrade tiers from the restored upgrade counts so caps
        // and pricing stay correct (HeroUpgradeKind order: Dmg, FR, Rng, HP, Volley).
        var tiers = s.Shop.HeroTiers;
        tiers[0] = h.DmgUpgrades;
        tiers[1] = h.FrUpgrades;
        tiers[2] = h.RngUpgrades;
        tiers[3] = h.HpUpgrades;
        tiers[4] = h.VolleyUpgrades;

        s.NextWaveKinds = save.NextWaveKinds?.ToList();
        s.NextWaveKinds2 = save.NextWaveKinds2?.ToList();

        // Resume in the between-wave lull: no live enemies, shop available, next wave queued.
        s.Enemies.Clear();
        s.Projectiles.Clear();
        s.Drops.Clear();
        s.Spawning = null;
        s.WaveBonusPending = false;
        s.PendingDraft = false;
        s.NeedsAutosave = false;
        s.Over = false;
        s.Paused = false;
        s.Phase = Phase.Combat;
        s.BetweenWaves = MathF.Max(4f, save.BetweenWaves);
        s.Shop.Refresh(s.Wave);
        s.Shop.CanOpen = true;
        s.Shop.Open = false;
    }
}
