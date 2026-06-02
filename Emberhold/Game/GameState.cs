using System.Numerics;
using Emberhold.Core;
using Emberhold.Data;
using Raylib_cs;

namespace Emberhold.Game;

/// <summary>The current run phase. Combat is the only active phase until the
/// draft/placement systems are wired in.</summary>
public enum Phase { Draft, Placement, Combat }

/// <summary>Wave spawn progress. Kinds are precomputed so the wave preview is exact.</summary>
public sealed class Spawning
{
    public int Remaining;
    public float Timer;
    public float Interval;
    public Queue<Data.EnemyKind> Kinds = new();
}

/// <summary>
/// Per-wave combat tally. One instance accumulates during the active wave; a
/// snapshot is shown on the wave-end stat card. Feeds kill-streak rewards too.
/// </summary>
public struct WaveSummary
{
    public int Wave;
    public int Kills;
    public int GoldEarned;
    public int DamageDealt;
    public int StructuresLost;
    public int BestStreak;
    public int Interest;
}

/// <summary>
/// All mutable simulation state for a run. Systems read and mutate this; it owns
/// helper methods for the shared FX (particles, floaters, drops) but no system
/// behaviour itself.
/// </summary>
public sealed class GameState
{
    public Hero Hero = new();
    public Camera2D Cam;

    public int Chapter = 1;
    public int Gold = 20;
    public int Wave = 1;
    public Phase Phase = Phase.Combat;

    // The per-run challenge modifier ("trial"). Set by GameApp at run start.
    public RunModifier Modifier = RunModifier.None;

    public float Elapsed;
    public int Kills;
    public int BossKills;
    public bool Over;
    public bool Paused;
    public float Shake;
    public float BossBannerTimer;
    public bool BossIncoming;        // tints the incoming banner for a boss wave
    public int HordeTier;            // War Drums: each boss cleared ramps a gentle global enemy buff
    public float BannerTimer;        // generic notice banner (e.g. "THE HORDE GROWS STRONGER")
    public string BannerText = "";
    public int BestWave = 1;
    public Profile? Profile;  // lifetime stats, set on game-over for the recap
    public readonly HashSet<string> SeenSynergies = new(); // discovered this run, for the summary

    // Per-wave stats: Live accumulates during the wave; LastSummary holds the most
    // recently cleared wave for the between-wave stat card.
    public WaveSummary Live;
    public WaveSummary? LastSummary;

    // Kill-streak ("Hot Streak"): consecutive kills within StreakWindow keep the
    // chain alive. Higher tiers buff hero damage and bonus gold per kill.
    public int Streak;
    public float StreakTimer;
    public const float StreakWindow = 2.5f;

    // Rally Horn: a gold-for-time clutch ability that slows the whole wave.
    public float RallyCooldown;
    public const float RallyMaxCooldown = 12f;
    public int RallyCost => Math.Min(140, 25 + Wave * 4);

    // Synergy discovery popups: queued ids shown one at a time as a banner.
    public readonly Queue<string> SynergyPopups = new();
    public string? ActivePopup;
    public float PopupTimer;
    public const float PopupDuration = 2.8f;

    // Global synergy flags, recomputed each combat frame by SynergyEngine.
    public float SlowDurationMult = 1f; // CryoForge keystone
    public bool VolleySplash;           // Ember Battery keystone
    public bool SupplyLines;            // Supply Lines keystone (mines +output)
    public bool WallsSharePool;         // Iron Tide keystone (wall regen)
    public bool Fortified;              // mono-Defend amplifier (wall damage reduction)
    public bool AurasGlobal;            // mono-Support amplifier (fort-wide auras)
    public bool FrostfireActive;        // Frostfire field synergy
    public bool SpoilsActive;           // Spoils field synergy
    public bool Glacier;                // Glacier keystone (cannons crush slowed)
    public bool Wildfire;               // Wildfire keystone (chains ignite)
    public bool Minefield;              // Minefield rune (3+ Trap: bigger, deadlier traps)
    public bool BoomTown;               // Boom Town rune (3+ Economy: richer mines)
    public readonly HashSet<string> ActiveSynergies = new();

    // Keep
    public float KeepHealth = 260f;
    public float KeepMaxHealth = 260f;

    // Between-wave shop
    public readonly ShopState Shop = new();

    // Wave flow
    public Spawning? Spawning;
    public List<Data.EnemyKind>? NextWaveKinds; // precomputed composition of the upcoming wave (for the preview)
    public float BetweenWaves = 4f; // grace before the first wave to establish defenses
    public bool WaveBonusPending;
    public bool UpgradeBreak;
    public bool PendingDraft;   // a milestone wave cleared; hand off to the draft

    // Entities
    public readonly List<Enemy> Enemies = new();
    public readonly List<Projectile> Projectiles = new();
    public readonly List<Drop> Drops = new();
    public readonly List<Particle> Particles = new();
    public readonly List<Floater> Floaters = new();
    public readonly List<Pad> Pads = new();
    public readonly List<Structure> Structures = new();

    private int _nextId = 1;
    public int NextId() => _nextId++;

    private readonly Random _rng = new();
    public float Rand() => (float)_rng.NextDouble();
    public float Rand(float min, float max) => min + (float)_rng.NextDouble() * (max - min);

    public GameState(bool seedDebug = true)
    {
        Cam = new Camera2D
        {
            Target = Vector2.Zero,
            Offset = new Vector2(Program.DesignWidth / 2f, Program.DesignHeight / 2f),
            Rotation = 0f,
            Zoom = 1f,
        };

        if (seedDebug) SeedDebugStructures();
    }

    public float FortHalfSize => Map.FortHalfSize(Chapter);
    public float RoamLimit => Map.RoamLimit(Chapter);

    /// <summary>Solid rectangles for collision: fort walls plus standing wall structures.</summary>
    public IReadOnlyList<Rectangle> SolidRects()
    {
        var rects = new List<Rectangle>(Map.WallRects(Chapter));
        foreach (var st in Structures)
            if (st.IsWallAlive)
                rects.Add(new Rectangle(st.Pos.X - st.Radius, st.Pos.Y - st.Radius, st.Radius * 2f, st.Radius * 2f));
        return rects;
    }

    /// <summary>
    /// Temporary: seed a representative set of built structures so combat systems
    /// can be exercised before the draft/placement UI exists. Replaced in task 6.
    /// </summary>
    private void SeedDebugStructures()
    {
        void Add(StructureKind kind, Vector2 pos)
            => Structures.Add(StructureFactory.Create(this, Data.CardDb.All.First(c => c.Kind == kind), pos));

        Add(StructureKind.ArcherPost, new Vector2(84, -84));   // NE zone
        Add(StructureKind.Cannon,     new Vector2(-84, -84));  // NW zone
        Add(StructureKind.WarBanner,  new Vector2(60, -120));  // buffs the archer
        Add(StructureKind.GoldMine,   new Vector2(-84, 84));   // SW zone
        Add(StructureKind.Barricade,  new Vector2(0, -95));    // blocks the north lane
        Add(StructureKind.TarPit,     new Vector2(0, 95));     // slows the south lane
    }

    // ---- Kill-streak helpers -------------------------------------------

    /// <summary>0 = no streak, 1 = warm (5+), 2 = hot (10+), 3 = blazing (18+).</summary>
    public int StreakTier => Streak >= 18 ? 3 : Streak >= 10 ? 2 : Streak >= 5 ? 1 : 0;

    /// <summary>Hero damage multiplier granted by the current streak tier.</summary>
    public float StreakDamageMult => StreakTier switch { 3 => 1.5f, 2 => 1.3f, 1 => 1.15f, _ => 1f };

    /// <summary>Bonus gold dropped per kill at the current streak tier.</summary>
    public int StreakBonusGold => StreakTier;

    public static string StreakLabel(int tier) => tier switch
    {
        3 => "BLAZING STREAK", 2 => "HOT STREAK", 1 => "ON A STREAK", _ => "",
    };

    /// <summary>Register a kill toward the streak; returns true when a new tier is reached.</summary>
    public bool RegisterStreakKill()
    {
        int before = StreakTier;
        Streak += 1;
        StreakTimer = StreakWindow;
        if (Streak > Live.BestStreak) Live.BestStreak = Streak;
        return StreakTier > before;
    }

    public void UpdateStreak(float dt)
    {
        if (Streak <= 0) return;
        StreakTimer -= dt;
        if (StreakTimer <= 0f) { Streak = 0; StreakTimer = 0f; }
    }

    /// <summary>Spend gold to blast the wave with a strong slow. Returns true if it fired.</summary>
    public bool TryRally()
    {
        if (RallyCooldown > 0f || Over) return false;
        int cost = RallyCost;
        if (Gold < cost) return false;

        Gold -= cost;
        RallyCooldown = RallyMaxCooldown;
        var chill = new Color((byte)150, (byte)200, (byte)235, (byte)255);
        foreach (var e in Enemies)
        {
            if (e.Dead || e.StatusImmune) continue; // wraiths shrug it off
            float factor = e.Boss ? 0.6f : 0.25f;
            float dur = e.Boss ? 2.5f : 4.5f;
            e.SlowFactor = e.SlowTimer <= 0f ? factor : MathF.Min(e.SlowFactor, factor);
            e.SlowTimer = MathF.Max(e.SlowTimer, dur);
            AddParticles(e.Pos, chill, 5, 46f);
        }
        AddFloater(Hero.Pos + new Vector2(0, -42), "RALLY!", chill);
        KickShake(6f);
        return true;
    }

    /// <summary>Advance the synergy-discovery banner, pulling the next from the queue.</summary>
    public void UpdatePopups(float dt)
    {
        if (ActivePopup is null)
        {
            if (SynergyPopups.Count == 0) return;
            ActivePopup = SynergyPopups.Dequeue();
            PopupTimer = PopupDuration;
        }
        PopupTimer -= dt;
        if (PopupTimer <= 0f) ActivePopup = null;
    }

    // ---- Shared FX helpers ---------------------------------------------

    public void KickShake(float amount) => Shake = MathF.Max(Shake, amount);

    public void AddParticles(Vector2 at, Color color, int count = 8, float speed = 48f)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = Rand() * MathUtils.Tau;
            float v = (0.35f + Rand() * 0.65f) * speed;
            Particles.Add(new Particle
            {
                Pos = at,
                Vel = new Vector2(MathF.Cos(angle) * v, MathF.Sin(angle) * v),
                Color = color,
                Life = 0.35f + Rand() * 0.38f,
                MaxLife = 0.73f,
                Size = 1.5f + Rand() * 3f,
            });
        }
    }

    public void AddFloater(Vector2 at, string text, Color color)
        => Floaters.Add(new Floater { Pos = at, Text = text, Color = color, Life = 1f, MaxLife = 1f });

    public void SpawnDrop(Vector2 at, int value, bool fromMine = false)
        => Drops.Add(new Drop
        {
            Id = NextId(), Pos = at, Value = value, FromMine = fromMine,
            Radius = fromMine ? 7f : 6f, Kind = DropKind.Gold,
            Life = fromMine ? 24f : 14f, Bob = Rand() * MathUtils.Tau,
        });

    public void SpawnEmber(Vector2 at)
        => Drops.Add(new Drop
        {
            Id = NextId(), Pos = at, Value = 0, FromMine = false, Radius = 9f,
            Kind = DropKind.Ember, Life = 20f, Bob = Rand() * MathUtils.Tau,
        });

    public void SpawnRelic(Vector2 at)
        => Drops.Add(new Drop
        {
            Id = NextId(), Pos = at, Value = 0, FromMine = false, Radius = 10f,
            Kind = DropKind.Relic, Life = 30f, Bob = Rand() * MathUtils.Tau,
        });
}
