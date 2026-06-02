using Raylib_cs;
using Emberhold.Render;

namespace Emberhold.Data;

public enum HeroKind { Ranger, Warden }

/// <summary>
/// Per-hero multipliers + identity. Ported from config.js HERO_PROFILES.
/// Multipliers scale the hero's base stats; 1.0 = baseline (Ranger).
/// </summary>
public sealed record HeroProfile(
    HeroKind Kind,
    string Name,
    string Initial,
    float Damage,
    float Rate,
    float Range,
    float Speed,
    Color Cloak)
{
    public static readonly HeroProfile Ranger = new(
        HeroKind.Ranger, "ASH, RANGER", "A",
        Damage: 1f, Rate: 1f, Range: 1f, Speed: 1f, Cloak: Palette.HeroCloak);

    public static readonly HeroProfile Warden = new(
        HeroKind.Warden, "MIRA, WARDEN", "M",
        Damage: 1.48f, Rate: 1.28f, Range: 0.86f, Speed: 0.92f, Cloak: Palette.Hex("765348"));

    public static HeroProfile Get(HeroKind kind) => kind switch
    {
        HeroKind.Warden => Warden,
        _ => Ranger,
    };
}
