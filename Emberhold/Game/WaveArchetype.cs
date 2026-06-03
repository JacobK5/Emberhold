namespace Emberhold.Game;

/// <summary>A special shape for a wave's composition + enemy stats (see WaveArchetypes).</summary>
public enum WaveArchetype { Normal, Swarm, Juggernaut, Flight, Frenzy }

/// <summary>Per-archetype stat multipliers applied to its (non-elite) raiders.</summary>
public readonly record struct ArchetypeMods(float Count, float Health, float Speed, float Radius, float Reward, float Damage);

/// <summary>
/// Wave archetypes give the deep game texture: from wave 10 a third of non-boss waves
/// take on a shape — a Swarm of weak fast runners, a Juggernaut of a few huge tanks,
/// an Air Raid of wall-ignoring flyers, or a Frenzy of fast high-bounty raiders. The
/// archetype is a pure function of (wave, run salt) so the preview and the actual
/// spawn always agree without storing parallel state.
/// </summary>
public static class WaveArchetypes
{
    public static WaveArchetype For(int wave, int salt)
    {
        if (wave < 10 || wave % 10 == 0) return WaveArchetype.Normal; // never early or on boss waves
        var r = new Random(salt ^ (wave * 9176 + 13));
        if (r.NextDouble() > 0.33) return WaveArchetype.Normal;        // ~1/3 of eligible waves are special
        return (WaveArchetype)(1 + r.Next(4));                         // Swarm / Juggernaut / Flight / Frenzy
    }

    public static ArchetypeMods Mods(WaveArchetype a) => a switch
    {
        //                               Count  Health Speed Radius Reward Damage
        WaveArchetype.Swarm      => new(1.85f, 0.55f, 1.22f, 0.80f, 0.6f, 0.9f),
        WaveArchetype.Juggernaut => new(0.45f, 2.30f, 0.82f, 1.25f, 2.4f, 1.3f),
        WaveArchetype.Flight     => new(1.05f, 1.00f, 1.00f, 1.00f, 1.1f, 1.0f),
        WaveArchetype.Frenzy     => new(1.15f, 0.85f, 1.40f, 1.00f, 1.4f, 1.0f),
        _                        => new(1.00f, 1.00f, 1.00f, 1.00f, 1.0f, 1.0f),
    };

    public static string Name(WaveArchetype a) => a switch
    {
        WaveArchetype.Swarm      => "SWARM",
        WaveArchetype.Juggernaut => "JUGGERNAUT",
        WaveArchetype.Flight     => "AIR RAID",
        WaveArchetype.Frenzy     => "FRENZY",
        _ => "",
    };

    public static string Blurb(WaveArchetype a) => a switch
    {
        WaveArchetype.Swarm      => "a tide of weak, fast raiders",
        WaveArchetype.Juggernaut => "a few towering, armoured brutes",
        WaveArchetype.Flight     => "wall-ignoring flyers in force",
        WaveArchetype.Frenzy     => "fast raiders with fat bounties",
        _ => "",
    };
}
