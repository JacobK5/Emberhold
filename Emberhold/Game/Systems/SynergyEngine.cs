using System.Numerics;
using Emberhold.Data;

namespace Emberhold.Game;

/// <summary>Metadata for an evaluable synergy (for HUD display + reference).</summary>
public sealed record SynergyDef(string Id, string Name, string Type, string Requires, string Effect);

/// <summary>
/// Evaluates the two synergy layers each combat frame and writes the results into
/// per-structure bonus fields + global flags on GameState. Combat systems read
/// those — keeping the synergy rules in one place and the systems synergy-agnostic.
///
/// Layer 1 (field): adjacency / range, mostly cross-category.
/// Layer 2 (keystone): owning a card pair flips a global effect.
/// Plus mono-amplifiers so going deep on one pillar stays viable.
/// </summary>
public static class SynergyEngine
{
    public static readonly IReadOnlyList<SynergyDef> Catalog = new[]
    {
        // Keystones
        new SynergyDef("cryo_forge", "Cryo-Forge", "Keystone", "Frost Spire + Forge", "All slows last 30% longer"),
        new SynergyDef("ember_battery", "Ember Battery", "Keystone", "Cannon + Ember Shrine", "Hero volley detonates splash"),
        new SynergyDef("supply_lines", "Supply Lines", "Keystone", "Gold Mine + War Banner", "Mines produce more gold"),
        new SynergyDef("iron_tide", "Iron Tide", "Keystone", "Bulwark + Redoubt", "All walls regenerate"),
        new SynergyDef("glacier", "Glacier", "Keystone", "Frost Spire + Cannon", "Cannons +40% vs slowed enemies"),
        new SynergyDef("wildfire", "Wildfire", "Keystone", "Flame Jet + Chain Coil", "Chain hits ignite their targets"),
        // Field
        new SynergyDef("siege_breaker", "Siege Breaker", "Field", "Ballista behind a wall", "+range, +25% damage"),
        new SynergyDef("kill_box", "Kill Box", "Field", "Cannon covering a slow trap", "+splash radius"),
        new SynergyDef("overcharged_coil", "Overcharged Coil", "Field", "Chain Coil in a War Banner aura", "+2 chain jumps"),
        new SynergyDef("frostfire", "Frostfire", "Field", "Frost Spire + Flame Jet adjacent", "Slowed+burning enemies shatter"),
        new SynergyDef("spoils", "Spoils", "Field", "Gold Mine near a trap", "Slowed kills drop +gold"),
        new SynergyDef("phalanx", "Phalanx", "Field", "Tower beside a wall", "+12% tower damage"),
        // Mono-amplifiers
        new SynergyDef("battery", "Battery", "Mono", "3+ towers clustered", "+18% tower damage"),
        new SynergyDef("fortified", "Fortified", "Mono", "3+ walls", "Walls take 35% less damage"),
        new SynergyDef("network", "Network", "Mono", "3+ support", "Auras project fort-wide"),
    };

    /// <summary>Keystone pairs (own both built structures) shared by evaluation + draft hints.</summary>
    public static readonly (StructureKind A, StructureKind B, string Id)[] Keystones =
    {
        (StructureKind.FrostSpire, StructureKind.Forge, "cryo_forge"),
        (StructureKind.Cannon, StructureKind.EmberShrine, "ember_battery"),
        (StructureKind.GoldMine, StructureKind.WarBanner, "supply_lines"),
        (StructureKind.Bulwark, StructureKind.Redoubt, "iron_tide"),
        (StructureKind.FrostSpire, StructureKind.Cannon, "glacier"),
        (StructureKind.FlameJet, StructureKind.ChainCoil, "wildfire"),
    };

    /// <summary>Names of keystones that drafting this card would complete, given owned kinds.</summary>
    public static IEnumerable<string> KeystoneHintsFor(CardDef card, ISet<StructureKind> owned)
    {
        foreach (var k in Keystones)
        {
            if (k.A == card.Kind && owned.Contains(k.B)) yield return NameOf(k.Id);
            else if (k.B == card.Kind && owned.Contains(k.A)) yield return NameOf(k.Id);
        }
    }

    public static string NameOf(string id) => Catalog.First(c => c.Id == id).Name;

    public static void Evaluate(GameState s)
    {
        Reset(s);
        var built = s.Structures;
        bool Has(StructureKind k) => built.Exists(st => st.Kind == k && (st.Role != StructureRole.Wall || st.IsWallAlive));

        // ---- Keystones (ownership) ----
        if (Has(StructureKind.FrostSpire) && Has(StructureKind.Forge)) { s.SlowDurationMult = 1.3f; s.ActiveSynergies.Add("cryo_forge"); }
        if (Has(StructureKind.Cannon) && Has(StructureKind.EmberShrine)) { s.VolleySplash = true; s.ActiveSynergies.Add("ember_battery"); }
        if (Has(StructureKind.GoldMine) && Has(StructureKind.WarBanner)) { s.SupplyLines = true; s.ActiveSynergies.Add("supply_lines"); }
        if (Has(StructureKind.Bulwark) && Has(StructureKind.Redoubt)) { s.WallsSharePool = true; s.ActiveSynergies.Add("iron_tide"); }
        if (Has(StructureKind.FrostSpire) && Has(StructureKind.Cannon)) { s.Glacier = true; s.ActiveSynergies.Add("glacier"); }
        if (Has(StructureKind.FlameJet) && Has(StructureKind.ChainCoil)) { s.Wildfire = true; s.ActiveSynergies.Add("wildfire"); }

        // ---- Field (per structure / adjacency) ----
        int towerCount = 0, wallCount = 0, auraCount = 0;
        foreach (var st in built)
        {
            if (st.Role == StructureRole.Tower) towerCount++;
            else if (st.IsWallAlive) wallCount++;
            else if (st.Role == StructureRole.Aura) auraCount++;
        }

        foreach (var t in built)
        {
            if (t.Role != StructureRole.Tower) continue;

            // Battery: clustered towers.
            int near = 0;
            foreach (var o in built)
                if (o.Role == StructureRole.Tower && Vector2.Distance(t.Pos, o.Pos) <= 110f) near++;
            if (near >= 3) { t.SynDamageMult *= 1.18f; s.ActiveSynergies.Add("battery"); }

            // Phalanx: any tower tucked beside an alive wall.
            if (built.Exists(w => w.IsWallAlive && Vector2.Distance(t.Pos, w.Pos) <= 70f))
            { t.SynDamageMult *= 1.12f; s.ActiveSynergies.Add("phalanx"); }

            switch (t.Kind)
            {
                case StructureKind.Ballista:
                    if (built.Exists(w => w.IsWallAlive && Vector2.Distance(t.Pos, w.Pos) <= 78f))
                    { t.SynRangeBonus += 45f; t.SynDamageMult *= 1.25f; s.ActiveSynergies.Add("siege_breaker"); }
                    break;
                case StructureKind.Cannon:
                    if (built.Exists(g => g.Role == StructureRole.GroundTrap && g.TrapSlowFactor < 1f
                                          && Vector2.Distance(t.Pos, g.Pos) <= t.Range))
                    { t.SynKillBox = true; t.SynSplashBonus += 26f; s.ActiveSynergies.Add("kill_box"); }
                    break;
                case StructureKind.ChainCoil:
                    if (built.Exists(a => a.Kind == StructureKind.WarBanner && Vector2.Distance(t.Pos, a.Pos) <= a.AuraRange))
                    { t.SynExtraChains += 2; s.ActiveSynergies.Add("overcharged_coil"); }
                    break;
            }
        }

        // Frostfire: a Frost Spire and Flame Jet placed close together.
        foreach (var f in built)
        {
            if (f.Kind != StructureKind.FrostSpire) continue;
            if (built.Exists(j => j.Kind == StructureKind.FlameJet && Vector2.Distance(f.Pos, j.Pos) <= 90f))
            { s.FrostfireActive = true; s.ActiveSynergies.Add("frostfire"); break; }
        }

        // Spoils: a slow/damage trap near a gold mine.
        foreach (var g in built)
        {
            if (g.Role != StructureRole.GroundTrap) continue;
            if (built.Exists(m => m.Role == StructureRole.Mine && Vector2.Distance(g.Pos, m.Pos) <= 150f))
            { s.SpoilsActive = true; s.ActiveSynergies.Add("spoils"); break; }
        }

        // ---- Mono-amplifiers ----
        if (wallCount >= 3) { s.Fortified = true; s.ActiveSynergies.Add("fortified"); }
        if (auraCount >= 3) { s.AurasGlobal = true; s.ActiveSynergies.Add("network"); }

        s.SeenSynergies.UnionWith(s.ActiveSynergies); // remember discoveries for the run summary
    }

    private static void Reset(GameState s)
    {
        s.SlowDurationMult = 1f;
        s.VolleySplash = false;
        s.SupplyLines = false;
        s.WallsSharePool = false;
        s.Fortified = false;
        s.AurasGlobal = false;
        s.FrostfireActive = false;
        s.SpoilsActive = false;
        s.Glacier = false;
        s.Wildfire = false;
        s.ActiveSynergies.Clear();

        foreach (var st in s.Structures)
        {
            st.SynDamageMult = 1f;
            st.SynRangeBonus = 0f;
            st.SynSplashBonus = 0f;
            st.SynExtraChains = 0;
            st.SynKillBox = false;
        }
    }
}
