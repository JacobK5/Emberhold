using Raylib_cs;

namespace Emberhold.Render;

/// <summary>
/// Warm frontier-fort palette, ported from the prototype's COLORS table.
/// Procedural rendering only — no sprite assets.
/// </summary>
public static class Palette
{
    public static Color Hex(string hex)
    {
        hex = hex.TrimStart('#');
        byte r = Convert.ToByte(hex.Substring(0, 2), 16);
        byte g = Convert.ToByte(hex.Substring(2, 2), 16);
        byte b = Convert.ToByte(hex.Substring(4, 2), 16);
        byte a = hex.Length >= 8 ? Convert.ToByte(hex.Substring(6, 2), 16) : (byte)255;
        return new Color(r, g, b, a);
    }

    public static readonly Color Grass     = Hex("34493f");
    public static readonly Color GrassDark  = Hex("2a3d36");
    public static readonly Color Path       = Hex("7b684e");
    public static readonly Color PathEdge   = Hex("5d513e");
    public static readonly Color Wall       = Hex("9d8962");
    public static readonly Color WallDark   = Hex("665b48");
    public static readonly Color Gold       = Hex("f3bd4d");
    public static readonly Color Fire       = Hex("ed7443");
    public static readonly Color Enemy      = Hex("b45142");
    public static readonly Color EnemyDark  = Hex("66332f");
    public static readonly Color Elite      = Hex("d48b49");
    public static readonly Color Hero       = Hex("d6b46c");
    public static readonly Color HeroCloak  = Hex("3d6c65");
    public static readonly Color Ink        = Hex("1e2928");
}
