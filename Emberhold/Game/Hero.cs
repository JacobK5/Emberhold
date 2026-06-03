using System.Numerics;
using Emberhold.Data;

namespace Emberhold.Game;

/// <summary>Run-long equipment dropped by elites; each grants a permanent bonus.</summary>
public enum RelicKind { EmberRing, SwiftBoots, WardenCloak, HawkEye }

/// <summary>
/// Independent progression for one hero kind: level/XP, unspent skill points, the
/// set of unlocked skill-tree nodes, and the stat block those levels/upgrades build
/// up. Each kind levels separately so switching heroes (H) means playing an
/// under-invested character — specialisation emerges without forbidding swaps.
/// </summary>
public sealed class HeroProgress
{
    public int Level = 1;
    public int Xp;
    public int NextXp = 8;
    public int SkillPoints;                       // unspent points earned from levels
    public readonly HashSet<string> Nodes = new(); // unlocked skill-tree node ids

    public float Health = 100f;
    public float MaxHealth = 100f;
    public float Damage = 14f;
    public float FireRate = 0.56f;  // seconds between shots (lower = faster)
    public float Range = 245f;
    public float Speed = 150f;

    public float VolleyCooldown = 8f; // governs the hero's signature (Space) ability
    public float VolleyDamage = 1.2f;
    public float BasePickupRadius = 24f;

    // Gold-shop upgrade tiers (applied to every kind so the shop stays run-wide).
    public int DmgUpgrades;
    public int FrUpgrades;
    public int RngUpgrades;
    public int HpUpgrades;
    public int VolleyUpgrades;
}

/// <summary>
/// The player-controlled hero. Position/facing and transient combat timers live
/// here directly; all progression stats live in a per-kind <see cref="HeroProgress"/>
/// so each hero builds up independently. Stat properties delegate to the active
/// kind's progress, keeping call sites (hero.Damage, hero.Level, …) unchanged.
/// </summary>
public sealed class Hero
{
    public Vector2 Pos = new(0, 48);
    public Vector2 Facing = new(0, -1);
    public float Radius = 12f;

    // Transient combat timers — moment-to-moment, shared across kinds.
    public float ShotTimer;
    public float Invulnerable;
    public float AbilityCooldown;
    public float DashCooldown;
    public float Overdrive;
    public float SwitchCooldown;  // gate on H so heroes can't be juggled every frame

    public HeroKind Kind = HeroKind.Ranger;
    public HeroProfile Profile => HeroProfile.Get(Kind);

    /// <summary>One progression slot per hero kind.</summary>
    public readonly Dictionary<HeroKind, HeroProgress> Progress = new();

    /// <summary>The active kind's progression.</summary>
    public HeroProgress Cur => Progress[Kind];

    // Equipment collected this run (elite drops). Run-wide: applied to every kind.
    public readonly HashSet<RelicKind> Relics = new();

    public Hero()
    {
        foreach (HeroKind k in Enum.GetValues<HeroKind>())
            Progress[k] = new HeroProgress();
    }

    /// <summary>Apply a stat change to every kind's progress (run-wide gear/upgrades).</summary>
    public void ApplyToAll(Action<HeroProgress> change)
    {
        foreach (var p in Progress.Values) change(p);
    }

    // ---- stat delegates → active progress -------------------------------
    public float Health { get => Cur.Health; set => Cur.Health = value; }
    public float MaxHealth { get => Cur.MaxHealth; set => Cur.MaxHealth = value; }
    public float Damage { get => Cur.Damage; set => Cur.Damage = value; }
    public float FireRate { get => Cur.FireRate; set => Cur.FireRate = value; }
    public float Range { get => Cur.Range; set => Cur.Range = value; }
    public float Speed { get => Cur.Speed; set => Cur.Speed = value; }
    public float VolleyCooldown { get => Cur.VolleyCooldown; set => Cur.VolleyCooldown = value; }
    public float VolleyDamage { get => Cur.VolleyDamage; set => Cur.VolleyDamage = value; }
    public float BasePickupRadius { get => Cur.BasePickupRadius; set => Cur.BasePickupRadius = value; }

    public int Level { get => Cur.Level; set => Cur.Level = value; }
    public int Xp { get => Cur.Xp; set => Cur.Xp = value; }
    public int NextXp { get => Cur.NextXp; set => Cur.NextXp = value; }

    public int DmgUpgrades { get => Cur.DmgUpgrades; set => Cur.DmgUpgrades = value; }
    public int FrUpgrades { get => Cur.FrUpgrades; set => Cur.FrUpgrades = value; }
    public int RngUpgrades { get => Cur.RngUpgrades; set => Cur.RngUpgrades = value; }
    public int HpUpgrades { get => Cur.HpUpgrades; set => Cur.HpUpgrades = value; }
    public int VolleyUpgrades { get => Cur.VolleyUpgrades; set => Cur.VolleyUpgrades = value; }

    // ---- skill tree -----------------------------------------------------

    /// <summary>True if the active hero has unlocked the given skill node.</summary>
    public bool Has(string nodeId) => Cur.Nodes.Contains(nodeId);

    // Node-backed passives (shared Foundations spine).
    public bool QuickHands => Has(HeroSkills.QuickHands);
    public bool SecondWind => Has(HeroSkills.SecondWind);
    public float PickupRadius => BasePickupRadius + (QuickHands ? 14f : 0f);

    /// <summary>Incoming-damage multiplier from defensive skill nodes (lower = tougher).</summary>
    public float DamageTakenMult
        => (Has(HeroSkills.Toughness) ? 0.88f : 1f) * (Has(HeroSkills.WArmor) ? 0.82f : 1f);
}
