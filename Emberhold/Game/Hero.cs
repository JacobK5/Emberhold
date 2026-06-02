using System.Numerics;
using Emberhold.Data;

namespace Emberhold.Game;

/// <summary>Run-long equipment dropped by elites; each grants a permanent bonus.</summary>
public enum RelicKind { EmberRing, SwiftBoots, WardenCloak, HawkEye }

/// <summary>The player-controlled hero. Stats ported from the prototype's hero state.</summary>
public sealed class Hero
{
    public Vector2 Pos = new(0, 48);
    public Vector2 Facing = new(0, -1);
    public float Radius = 12f;

    public float Speed = 150f;
    public float Health = 100f;
    public float MaxHealth = 100f;

    public float Damage = 14f;
    public float FireRate = 0.56f;   // seconds between shots (lower = faster)
    public float ShotTimer;
    public float Range = 245f;

    public int Level = 1;
    public int Xp;
    public int NextXp = 8;

    public float Invulnerable;
    public float AbilityCooldown;
    public float VolleyCooldown = 8f;
    public float VolleyDamage = 1.2f;
    public float DashCooldown;
    public float Overdrive;

    public HeroKind Kind = HeroKind.Ranger;
    public HeroProfile Profile => HeroProfile.Get(Kind);

    // Gold-based upgrade tiers purchased via the shop. Applied on purchase.
    public int DmgUpgrades;
    public int FrUpgrades;
    public int RngUpgrades;
    public int HpUpgrades;
    public int VolleyUpgrades;

    // Equipment collected this run (elite drops). Effects applied once on pickup.
    public readonly HashSet<RelicKind> Relics = new();
    public float BasePickupRadius = 24f;

    // Level-gated passive abilities (computed live so hero-switching is seamless).
    public bool QuickHands => Level >= 3;  // wider gold pickup radius
    public bool Signature  => Level >= 5;  // Ranger: ricochet shots / Warden: cleave
    public bool SecondWind => Level >= 7;  // slow passive health regen
    public float PickupRadius => BasePickupRadius + (QuickHands ? 14f : 0f);

    public static string PassiveName(int level, HeroKind kind) => level switch
    {
        3 => "QUICK HANDS",
        5 => kind == HeroKind.Warden ? "CLEAVE" : "RICOCHET",
        7 => "SECOND WIND",
        _ => "",
    };
}
