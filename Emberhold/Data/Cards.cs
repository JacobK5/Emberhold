namespace Emberhold.Data;

/// <summary>The three draft pillars. Each draft offers one card per category.</summary>
public enum Category { Attack, Defend, Support }

/// <summary>
/// Synergy-matching tags. A card can carry several. Field and keystone synergies
/// are expressed in terms of these tags + concrete kinds, so new cards slot into
/// the synergy graph without bespoke wiring.
/// </summary>
[Flags]
public enum Tag
{
    None      = 0,
    Rapid     = 1 << 0,
    Physical  = 1 << 1,
    Splash    = 1 << 2,
    Siege     = 1 << 3,
    Pierce    = 1 << 4,
    LongRange = 1 << 5,
    Chain     = 1 << 6,
    Elemental = 1 << 7,
    Burn      = 1 << 8,
    Dot       = 1 << 9,
    ShortRange= 1 << 10,
    Slow      = 1 << 11,
    Control   = 1 << 12,
    Wall      = 1 << 13,
    Block     = 1 << 14,
    Ground    = 1 << 15,
    Trap      = 1 << 16,
    Regen     = 1 << 17,
    Retaliate = 1 << 18,
    Aura      = 1 << 19,
    Damage    = 1 << 20,
    Rate      = 1 << 21,
    Range     = 1 << 22,
    Hero      = 1 << 23,
    Economy   = 1 << 24,
    Repair    = 1 << 25,
}

/// <summary>Concrete structure a built card becomes. The build system switches on this.</summary>
public enum StructureKind
{
    // Attack
    ArcherPost, Cannon, Ballista, ChainCoil, FlameJet, FrostSpire, StormSpire,
    // Defend
    Barricade, SpikeTrap, TarPit, Bulwark, MoatLine, Redoubt, Caltrops,
    // Support
    GoldMine, WarBanner, Forge, EmberShrine, Watchtower, Workshop, TradingPost,
}

/// <summary>
/// A draftable card == a placeable pad that becomes a structure when funded.
/// Cost is gold to build; Short is the on-map label.
/// </summary>
public sealed record CardDef(
    string Id,
    string Name,
    string Short,
    Category Category,
    StructureKind Kind,
    Tag Tags,
    int Cost);

public static class CardDb
{
    public static readonly IReadOnlyList<CardDef> All = new[]
    {
        // ---- Attack ----
        new CardDef("archer_post", "Archer Post", "ARCHR", Category.Attack, StructureKind.ArcherPost,
            Tag.Rapid | Tag.Physical, 20),
        new CardDef("cannon", "Cannon", "CANON", Category.Attack, StructureKind.Cannon,
            Tag.Splash | Tag.Siege, 45),
        new CardDef("ballista", "Ballista", "BLST", Category.Attack, StructureKind.Ballista,
            Tag.Pierce | Tag.LongRange, 40),
        new CardDef("chain_coil", "Chain Coil", "CHAIN", Category.Attack, StructureKind.ChainCoil,
            Tag.Chain | Tag.Elemental, 55),
        new CardDef("flame_jet", "Flame Jet", "FLAME", Category.Attack, StructureKind.FlameJet,
            Tag.Burn | Tag.Dot | Tag.ShortRange, 50),
        new CardDef("frost_spire", "Frost Spire", "FROST", Category.Attack, StructureKind.FrostSpire,
            Tag.Slow | Tag.Control | Tag.Elemental, 50),
        new CardDef("storm_spire", "Storm Spire", "STORM", Category.Attack, StructureKind.StormSpire,
            Tag.Elemental | Tag.Chain | Tag.LongRange, 55),

        // ---- Defend ----
        new CardDef("barricade", "Barricade", "WALL", Category.Defend, StructureKind.Barricade,
            Tag.Wall | Tag.Block, 28),
        new CardDef("spike_trap", "Spike Trap", "SPIKE", Category.Defend, StructureKind.SpikeTrap,
            Tag.Ground | Tag.Trap, 35),
        new CardDef("tar_pit", "Tar Pit", "TAR", Category.Defend, StructureKind.TarPit,
            Tag.Ground | Tag.Slow | Tag.Control, 40),
        new CardDef("bulwark", "Bulwark", "BLWRK", Category.Defend, StructureKind.Bulwark,
            Tag.Wall | Tag.Regen, 55),
        new CardDef("moat_line", "Moat Line", "MOAT", Category.Defend, StructureKind.MoatLine,
            Tag.Trap | Tag.Slow, 60),
        new CardDef("redoubt", "Redoubt", "RDBT", Category.Defend, StructureKind.Redoubt,
            Tag.Wall | Tag.Retaliate, 50),
        new CardDef("caltrops", "Caltrops", "CALT", Category.Defend, StructureKind.Caltrops,
            Tag.Ground | Tag.Trap, 30),

        // ---- Support ----
        new CardDef("gold_mine", "Gold Mine", "MINE", Category.Support, StructureKind.GoldMine,
            Tag.Economy, 30),
        new CardDef("war_banner", "War Banner", "BANR", Category.Support, StructureKind.WarBanner,
            Tag.Aura | Tag.Damage, 75),
        new CardDef("forge", "Forge", "FORGE", Category.Support, StructureKind.Forge,
            Tag.Aura | Tag.Rate, 70),
        new CardDef("ember_shrine", "Ember Shrine", "SHRNE", Category.Support, StructureKind.EmberShrine,
            Tag.Hero, 65),
        new CardDef("watchtower", "Watchtower", "WATCH", Category.Support, StructureKind.Watchtower,
            Tag.Aura | Tag.Range, 60),
        new CardDef("workshop", "Workshop", "WRKSP", Category.Support, StructureKind.Workshop,
            Tag.Economy | Tag.Repair, 70),
        new CardDef("trading_post", "Trading Post", "TRADE", Category.Support, StructureKind.TradingPost,
            Tag.Economy, 50),
    };

    private static readonly Dictionary<string, CardDef> ById =
        All.ToDictionary(c => c.Id);

    public static CardDef Get(string id) => ById[id];

    public static IEnumerable<CardDef> ByCategory(Category category)
        => All.Where(c => c.Category == category);
}
