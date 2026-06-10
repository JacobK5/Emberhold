using Emberhold.Game;
using Raylib_cs;

namespace Emberhold.Render;

/// <summary>An unlockable cape colour (trophy-gated cosmetic).</summary>
public sealed record CapeDef(string Name, Color Color, int TrophiesNeeded);

/// <summary>
/// Cape regalia: cosmetic cloak colours unlocked by lifetime trophy count and
/// picked on the hero-select screen. Index 0 is the hero's own cloak (always
/// available); the rest are earned. The choice persists on the profile and is
/// applied to the hero render via <c>Hero.CapeOverride</c>.
/// </summary>
public static class Capes
{
    public static readonly CapeDef[] All =
    {
        new("Hero's Own", new Color(0, 0, 0, 0), 0), // sentinel: per-hero default cloak
        new("Emberweave", new Color(0xc2, 0x52, 0x3a, 0xff), 2),
        new("Gilded", new Color(0xd8, 0xa8, 0x3a, 0xff), 4),
        new("Frostmantle", new Color(0x5d, 0x86, 0xb8, 0xff), 6),
        new("Nightshade", new Color(0x7a, 0x4f, 0x96, 0xff), 8),
        new("Wardens' Moss", new Color(0x6f, 0x9e, 0x5a, 0xff), 10),
    };

    public static bool Unlocked(Profile profile, int index)
        => index >= 0 && index < All.Length && profile.Trophies.Count >= All[index].TrophiesNeeded;

    /// <summary>The cloak override colour for a profile's choice (null = hero default).</summary>
    public static Color? Override(Profile profile)
    {
        int i = profile.CapeChoice;
        if (i <= 0 || !Unlocked(profile, i)) return null;
        return All[i].Color;
    }
}
