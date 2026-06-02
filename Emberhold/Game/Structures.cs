using System.Numerics;
using Emberhold.Data;

namespace Emberhold.Game;

/// <summary>How a built structure behaves. Systems switch on this.</summary>
public enum StructureRole { Tower, Wall, GroundTrap, Mine, Aura, HeroBuff }

/// <summary>What an aura projects to nearby towers.</summary>
public enum AuraKind { None, Damage, Rate, Range, Economy }

/// <summary>
/// A placed card awaiting construction. The hero funds it by standing on it; the
/// deposit rate follows √cost so late builds don't strand the hero (MathUtils.DepositRate).
/// </summary>
public sealed class Pad
{
    public required CardDef Def;
    public Vector2 Pos;
    public float Radius = 18f;
    public int Invested;
    public float DepositCarry;
    public float Dwell;

    public int Remaining => Def.Cost - Invested;
}

/// <summary>
/// A built structure. One class with a role + the fields each role needs, created
/// by StructureFactory from a CardDef. Footprint Radius is used for drawing,
/// placement overlap, and (for walls) collision.
/// </summary>
public sealed class Structure
{
    public int Id;
    public StructureKind Kind;
    public Category Category;
    public Tag Tags;
    public StructureRole Role;
    public Vector2 Pos;
    public float Radius = 16f;
    public int Level = 1;

    // Upgrades (late-game gold sink): stand on a built structure to level it up.
    public const int MaxLevel = 3;
    public int BaseCost;
    public float Dwell;
    public int UpgradeInvested;
    public float UpgradeCarry;
    public bool Upgradable => Role != StructureRole.HeroBuff && Level < MaxLevel;
    public int UpgradeCost => (int)MathF.Round(BaseCost * (Level + 1) * 0.75f);

    // Tower
    public float Range;
    public float Damage;
    public float Rate;
    public float Cooldown;
    public float Splash;
    public bool Pierce;
    public int ChainCount;
    public float SlowFactor = 1f;
    public float SlowDuration;
    public float BurnDps;
    public float BurnDuration;
    public ProjectileSource ProjSource;
    public float ProjSpeed = 360f;

    // Mine
    public float Interval;
    public float Timer;

    // Aura
    public float AuraRange;
    public AuraKind AuraKind;
    public float AuraMagnitude;

    // Wall
    public float Health;
    public float MaxHealth;
    public bool Regen;
    public bool Retaliate;

    // Ground trap
    public float TrapDps;
    public float TrapSlowFactor = 1f;

    // Synergy-derived bonuses, recomputed each combat frame by SynergyEngine.
    public float SynDamageMult = 1f;
    public float SynRangeBonus;
    public float SynSplashBonus;
    public int SynExtraChains;
    public bool SynKillBox;   // display flag; effect applied via SynSplashBonus

    public bool IsWallAlive => Role == StructureRole.Wall && Health > 0f;
}

/// <summary>Maps a drafted card to a concrete built structure.</summary>
public static class StructureFactory
{
    public static Structure Create(GameState s, CardDef def, Vector2 pos)
    {
        var st = new Structure
        {
            Id = s.NextId(),
            Kind = def.Kind,
            Category = def.Category,
            Tags = def.Tags,
            Pos = pos,
            BaseCost = def.Cost,
        };

        switch (def.Kind)
        {
            // ---- Attack (towers) ----
            case StructureKind.ArcherPost:
                st.Role = StructureRole.Tower; st.Range = 230; st.Damage = 10; st.Rate = 0.88f;
                st.ProjSource = ProjectileSource.Tower; st.ProjSpeed = 340; break;
            case StructureKind.Cannon:
                st.Role = StructureRole.Tower; st.Range = 260; st.Damage = 31; st.Rate = 1.95f;
                st.Splash = 58; st.ProjSource = ProjectileSource.Cannon; st.ProjSpeed = 275; st.Radius = 17; break;
            case StructureKind.Ballista:
                st.Role = StructureRole.Tower; st.Range = 310; st.Damage = 24; st.Rate = 1.35f;
                st.Pierce = true; st.ProjSource = ProjectileSource.Ballista; st.ProjSpeed = 480; break;
            case StructureKind.ChainCoil:
                st.Role = StructureRole.Tower; st.Range = 205; st.Damage = 14; st.Rate = 1.1f;
                st.ChainCount = 3; st.ProjSource = ProjectileSource.Chain; st.ProjSpeed = 520; break;
            case StructureKind.FlameJet:
                st.Role = StructureRole.Tower; st.Range = 135; st.Damage = 5; st.Rate = 0.5f;
                st.BurnDps = 10; st.BurnDuration = 2.2f; st.ProjSource = ProjectileSource.Flame; st.ProjSpeed = 300; break;
            case StructureKind.FrostSpire:
                st.Role = StructureRole.Tower; st.Range = 210; st.Damage = 4; st.Rate = 0.8f;
                st.SlowFactor = 0.55f; st.SlowDuration = 1.6f; st.ProjSource = ProjectileSource.Tower; st.ProjSpeed = 360; break;

            // ---- Defend ----
            case StructureKind.Barricade:
                st.Role = StructureRole.Wall; st.Health = st.MaxHealth = 180; st.Radius = 22; break;
            case StructureKind.Bulwark:
                st.Role = StructureRole.Wall; st.Health = st.MaxHealth = 320; st.Regen = true; st.Radius = 24; break;
            case StructureKind.Redoubt:
                st.Role = StructureRole.Wall; st.Health = st.MaxHealth = 200; st.Retaliate = true; st.Radius = 22; break;
            case StructureKind.SpikeTrap:
                st.Role = StructureRole.GroundTrap; st.TrapDps = 20; st.Radius = 26; break;
            case StructureKind.TarPit:
                st.Role = StructureRole.GroundTrap; st.TrapSlowFactor = 0.5f; st.Radius = 30; break;
            case StructureKind.MoatLine:
                st.Role = StructureRole.GroundTrap; st.TrapDps = 11; st.TrapSlowFactor = 0.7f; st.Radius = 34; break;

            // ---- Support ----
            case StructureKind.GoldMine:
                st.Role = StructureRole.Mine; st.Interval = 2.6f; break;
            case StructureKind.WarBanner:
                st.Role = StructureRole.Aura; st.AuraKind = AuraKind.Damage; st.AuraRange = 142; st.AuraMagnitude = 1.42f; break;
            case StructureKind.Forge:
                st.Role = StructureRole.Aura; st.AuraKind = AuraKind.Rate; st.AuraRange = 142; st.AuraMagnitude = 0.84f; break;
            case StructureKind.Watchtower:
                st.Role = StructureRole.Aura; st.AuraKind = AuraKind.Range; st.AuraRange = 150; st.AuraMagnitude = 60f; break;
            case StructureKind.Workshop:
                st.Role = StructureRole.Aura; st.AuraKind = AuraKind.Economy; st.AuraRange = 150; st.AuraMagnitude = 1.5f; break;
            case StructureKind.EmberShrine:
                st.Role = StructureRole.HeroBuff;
                s.Hero.VolleyCooldown = 5.8f; s.Hero.VolleyDamage = 1.55f; break;
        }
        return st;
    }
}
