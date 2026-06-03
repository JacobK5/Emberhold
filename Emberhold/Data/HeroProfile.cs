using Raylib_cs;
using Emberhold.Render;

namespace Emberhold.Data;

public enum HeroKind { Ranger, Warden, Artificer, Bulwark, Executioner, Elementalist }

/// <summary>
/// Per-hero multipliers + identity. Ported from config.js HERO_PROFILES.
/// Multipliers scale the hero's base stats; 1.0 = baseline (Ranger). BaseHealth
/// seeds each kind's starting max HP (the tank starts far higher).
/// </summary>
public sealed record HeroProfile(
    HeroKind Kind,
    string Name,
    string Initial,
    float Damage,
    float Rate,
    float Range,
    float Speed,
    Color Cloak,
    float BaseHealth = 100f)
{
    public static readonly HeroProfile Ranger = new(
        HeroKind.Ranger, "ASH, RANGER", "A",
        Damage: 1f, Rate: 1f, Range: 1f, Speed: 1f, Cloak: Palette.HeroCloak);

    public static readonly HeroProfile Warden = new(
        HeroKind.Warden, "MIRA, WARDEN", "M",
        Damage: 1.48f, Rate: 1.28f, Range: 0.86f, Speed: 0.92f, Cloak: Palette.Hex("765348"));

    public static readonly HeroProfile Artificer = new(
        HeroKind.Artificer, "TILDA, ARTIFICER", "T",
        Damage: 0.7f, Rate: 1.15f, Range: 1.05f, Speed: 1f, Cloak: Palette.Hex("4a6b7a"));

    public static readonly HeroProfile Bulwark = new(
        HeroKind.Bulwark, "BRAM, BULWARK", "B",
        Damage: 0.95f, Rate: 1.2f, Range: 0.78f, Speed: 0.82f, Cloak: Palette.Hex("5c6a4a"),
        BaseHealth: 230f);

    public static readonly HeroProfile Executioner = new(
        HeroKind.Executioner, "VESS, EXECUTIONER", "V",
        Damage: 1.4f, Rate: 0.92f, Range: 0.95f, Speed: 1.18f, Cloak: Palette.Hex("7a2f3a"),
        BaseHealth: 82f);

    public static readonly HeroProfile Elementalist = new(
        HeroKind.Elementalist, "NIVA, ELEMENTALIST", "N",
        Damage: 0.92f, Rate: 1.1f, Range: 1.12f, Speed: 0.96f, Cloak: Palette.Hex("4a7a8c"),
        BaseHealth: 90f);

    public static HeroProfile Get(HeroKind kind) => kind switch
    {
        HeroKind.Warden => Warden,
        HeroKind.Artificer => Artificer,
        HeroKind.Bulwark => Bulwark,
        HeroKind.Executioner => Executioner,
        HeroKind.Elementalist => Elementalist,
        _ => Ranger,
    };
}
