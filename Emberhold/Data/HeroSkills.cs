namespace Emberhold.Data;

/// <summary>
/// A node in a hero's skill tree. Nodes live in a column/row grid: column 0 is the
/// shared "Foundations" spine (identical for every hero), columns 1-2 are the hero's
/// two unique branches. A node unlocks only once the node directly above it in its
/// column (its <see cref="Requires"/>) is owned; row-0 nodes are branch roots.
/// </summary>
public sealed record SkillNode(
    string Id,
    string Name,
    string Desc,
    int Col,
    int Row,
    string? Requires);

/// <summary>
/// Per-hero skill trees. Effects are applied either immediately on unlock
/// (<see cref="OnUnlock"/> stat grants) or read live by the combat systems via
/// node-id checks (<c>hero.Has(id)</c>). Foundations nodes are shared verbatim.
/// </summary>
public static class HeroSkills
{
    // ---- shared node ids (Foundations) ----------------------------------
    public const string Vitality   = "f_vitality";
    public const string QuickHands = "f_quick_hands";
    public const string Toughness  = "f_toughness";
    public const string SecondWind = "f_second_wind";

    // ---- Ranger node ids ------------------------------------------------
    public const string RRicochet = "r_ricochet";
    public const string RPierce   = "r_pierce";
    public const string RWide     = "r_wide";
    public const string RStorm    = "r_storm";

    // ---- Warden node ids ------------------------------------------------
    public const string WCleave    = "w_cleave";
    public const string WRend      = "w_rend";
    public const string WArmor     = "w_armor";
    public const string WLifesteal = "w_lifesteal";

    // ---- Artificer node ids ---------------------------------------------
    public const string AOverclock = "a_overclock";
    public const string AWideAura  = "a_wide_aura";
    public const string ARepair    = "a_repair_fast";
    public const string ASurge     = "a_overcharge_long";

    // ---- Bulwark node ids -----------------------------------------------
    public const string BProvoke = "b_provoke";
    public const string BThorns  = "b_thorns";
    public const string BAegis   = "b_aegis";
    public const string BAnchor  = "b_anchor";

    // ---- Executioner node ids -------------------------------------------
    public const string XHeadsman = "x_headsman";
    public const string XReap     = "x_reap";
    public const string XSwift    = "x_swift";
    public const string XMark     = "x_mark";

    // ---- Elementalist node ids ------------------------------------------
    public const string EDeepFreeze = "e_deepfreeze";
    public const string EShatter    = "e_shatter";
    public const string EArc        = "e_arc";
    public const string EEmber      = "e_ember";

    // ---- Beastmaster node ids -------------------------------------------
    public const string MAlpha  = "m_alpha";
    public const string MFrenzy = "m_frenzy";
    public const string MPack2  = "m_pack2";
    public const string MMaul   = "m_maul";

    private static readonly SkillNode[] Foundations =
    {
        new(Vitality,   "Vitality",     "+30 max HP",                0, 0, null),
        new(QuickHands, "Quick Hands",  "Wider gold pickup radius",  0, 1, Vitality),
        new(Toughness,  "Toughness",    "Take 12% less damage",      0, 2, QuickHands),
        new(SecondWind, "Second Wind",  "Regenerate 4.5 HP/s",       0, 3, Toughness),
    };

    private static readonly SkillNode[] RangerTree =
    {
        new(RRicochet, "Ricochet",   "Shots chain to a 2nd target",  1, 0, null),
        new(RPierce,   "Piercing",   "Shots pierce through enemies",  1, 1, RRicochet),
        new(RWide,     "Wide Volley","Volley fires 9 arrows",         2, 0, null),
        new(RStorm,    "Arrow Storm","Volley arrows splash on impact",2, 1, RWide),
    };

    private static readonly SkillNode[] WardenTree =
    {
        new(WCleave,    "Cleave",     "Shots splash nearby enemies",   1, 0, null),
        new(WRend,      "Rend",       "Hero hits briefly slow enemies",1, 1, WCleave),
        new(WArmor,     "Iron Skin",  "Take 18% less damage",          2, 0, null),
        new(WLifesteal, "Bloodthirst","Heal for 6% of damage dealt",   2, 1, WArmor),
    };

    private static readonly SkillNode[] ArtificerTree =
    {
        new(AOverclock, "Overclock",  "Stronger nearby-tower buff",    1, 0, null),
        new(AWideAura,  "Broadcast",  "+60 tower buff / repair radius",1, 1, AOverclock),
        new(ARepair,    "Field Repair","Repairs structures twice as fast",2, 0, null),
        new(ASurge,     "Power Surge","Overcharge lasts 8s (was 5s)",  2, 1, ARepair),
    };

    private static readonly SkillNode[] BulwarkTree =
    {
        new(BProvoke, "Provoke",  "Wider taunt - blocks more enemies", 1, 0, null),
        new(BThorns,  "Thorns",   "Attackers take reflected damage",   1, 1, BProvoke),
        new(BAegis,   "Aegis",    "Take 18% less damage",              2, 0, null),
        new(BAnchor,  "Anchor",   "Stance lasts 7s & slows attackers", 2, 1, BAegis),
    };

    private static readonly SkillNode[] ExecutionerTree =
    {
        new(XHeadsman, "Headsman", "Execute threshold 22% -> 35% HP", 1, 0, null),
        new(XReap,     "Reaping",  "Executes refund cd & drop gold",  1, 1, XHeadsman),
        new(XSwift,    "Shadowstep","Dash cd -40%, longer i-frames",  2, 0, null),
        new(XMark,     "Deathmark","+25% hero damage vs elites/bosses",2, 1, XSwift),
    };

    private static readonly SkillNode[] ElementalistTree =
    {
        new(EDeepFreeze, "Deep Freeze","Frost Nova slows harder & longer", 1, 0, null),
        new(EShatter,    "Shatter",    "+35% hero damage to slowed foes",  1, 1, EDeepFreeze),
        new(EArc,        "Arc",        "Hero bolts chain to a 2nd foe",    2, 0, null),
        new(EEmber,      "Emberwind",  "Frost Nova also ignites enemies",  2, 1, EArc),
    };

    private static readonly SkillNode[] BeastmasterTree =
    {
        new(MAlpha,  "Alpha",        "Wolves bite for more damage",     1, 0, null),
        new(MFrenzy, "Frenzy",       "Wolves attack faster",            1, 1, MAlpha),
        new(MPack2,  "Greater Pack", "Keep two loyal wolves, not one",  2, 0, null),
        new(MMaul,   "Maul",         "Wolf bites slow their prey",      2, 1, MPack2),
    };

    /// <summary>Every node a given hero can unlock: shared Foundations + its two branches.</summary>
    public static IReadOnlyList<SkillNode> Tree(HeroKind kind)
    {
        var unique = kind switch
        {
            HeroKind.Warden => WardenTree,
            HeroKind.Artificer => ArtificerTree,
            HeroKind.Bulwark => BulwarkTree,
            HeroKind.Executioner => ExecutionerTree,
            HeroKind.Elementalist => ElementalistTree,
            HeroKind.Beastmaster => BeastmasterTree,
            _ => RangerTree,
        };
        return Foundations.Concat(unique).ToList();
    }

    public static SkillNode? Find(HeroKind kind, string id)
        => Tree(kind).FirstOrDefault(n => n.Id == id);

    /// <summary>Short label for the hero's signature (Space) ability.</summary>
    public static string SignatureName(HeroKind kind) => kind switch
    {
        HeroKind.Warden => "GROUND SLAM",
        HeroKind.Artificer => "OVERCHARGE",
        HeroKind.Bulwark => "STANCE",
        HeroKind.Executioner => "EXECUTE",
        HeroKind.Elementalist => "FROST NOVA",
        HeroKind.Beastmaster => "RALLY PACK",
        _ => "VOLLEY",
    };

    /// <summary>Title for a skill-tree column: 0 = shared spine, 1-2 = the hero's branches.</summary>
    public static string Column(HeroKind kind, int col)
    {
        if (col == 0) return "FOUNDATIONS";
        return kind switch
        {
            HeroKind.Warden => col == 1 ? "CLEAVE" : "JUGGERNAUT",
            HeroKind.Artificer => col == 1 ? "OVERCLOCK" : "CONSTRUCT",
            HeroKind.Bulwark => col == 1 ? "WALL" : "GUARDIAN",
            HeroKind.Executioner => col == 1 ? "REAPING" : "SHADOW",
            HeroKind.Elementalist => col == 1 ? "FROST" : "STORM",
            HeroKind.Beastmaster => col == 1 ? "PACK" : "WILD",
            _ => col == 1 ? "PRECISION" : "BARRAGE",
        };
    }
}
