namespace Emberhold.Game;

/// <summary>A dynamic battlefield event that colours a late-game wave.</summary>
public enum MapEventKind { None, MeteorShower, SupplyDrop, GoldRush }

/// <summary>Flavour + classification for the dynamic map events (see MapEventSystem).</summary>
public static class MapEvents
{
    /// <summary>Events eligible to roll, in no particular order.</summary>
    public static readonly MapEventKind[] Rollable =
        { MapEventKind.MeteorShower, MapEventKind.SupplyDrop, MapEventKind.GoldRush };

    public static string Name(MapEventKind k) => k switch
    {
        MapEventKind.MeteorShower => "METEOR STORM",
        MapEventKind.SupplyDrop   => "SUPPLY DROP",
        MapEventKind.GoldRush     => "GOLD RUSH",
        _ => "",
    };

    public static string Blurb(MapEventKind k) => k switch
    {
        MapEventKind.MeteorShower => "meteors rain across the field - keep moving",
        MapEventKind.SupplyDrop   => "reinforcement structures land in your zones",
        MapEventKind.GoldRush     => "raider bounties and mine yields are doubled",
        _ => "",
    };

    /// <summary>Hazards are telegraphed in alarm red; boons in gold.</summary>
    public static bool IsHazard(MapEventKind k) => k == MapEventKind.MeteorShower;
}
