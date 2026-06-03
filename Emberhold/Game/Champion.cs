using System.Numerics;
using Emberhold.Core;
using Emberhold.Render;

namespace Emberhold.Game;

/// <summary>A champion's signature trait (mini-boss flavour).</summary>
public enum ChampionTrait { Ironhide, Warbringer, Swiftblade }

/// <summary>
/// Champions are named mini-boss raiders promoted from the rank-and-file on deep
/// waves: much tankier, a crown render, a guaranteed ember + bonus gold and Fury on
/// death, and one signature trait. A high-value priority target between bosses.
/// </summary>
public static class Champions
{
    public static string Name(ChampionTrait t) => t switch
    {
        ChampionTrait.Ironhide   => "Ironhide",
        ChampionTrait.Warbringer => "Warbringer",
        ChampionTrait.Swiftblade => "Swiftblade",
        _ => "Champion",
    };

    public static string Blurb(ChampionTrait t) => t switch
    {
        ChampionTrait.Ironhide   => "heavily armoured",
        ChampionTrait.Warbringer => "enrages as it's hurt",
        ChampionTrait.Swiftblade => "blindingly fast",
        _ => "",
    };

    /// <summary>Promote a freshly-spawned rank-and-file enemy into a named champion.</summary>
    public static void Promote(GameState s, Enemy e)
    {
        e.Champion = true;
        e.Trait = (ChampionTrait)(int)(s.Rand() * 3f);

        // Base champion buff (a beefy priority target).
        e.Health *= 3.5f; e.MaxHealth *= 3.5f;
        e.Radius *= 1.3f;
        e.Reward *= 4;

        switch (e.Trait)
        {
            case ChampionTrait.Ironhide:    // armoured wall: high per-hit mitigation, slow, extra HP
                e.ShieldPerHit = MathF.Max(e.ShieldPerHit, 22f);
                e.Speed *= 0.8f;
                e.Health *= 1.3f; e.MaxHealth *= 1.3f;
                break;
            case ChampionTrait.Swiftblade:  // fast glass cannon: less HP, more speed + bite
                e.Speed *= 1.6f;
                e.Radius *= 0.82f;
                e.Damage = (int)MathF.Ceiling(e.Damage * 1.4f);
                e.Health *= 0.75f; e.MaxHealth *= 0.75f;
                break;
            case ChampionTrait.Warbringer:  // enrages as it's hurt (see EnrageSpeed)
                e.Damage = (int)MathF.Ceiling(e.Damage * 1.2f);
                break;
        }

        s.BannerText = $"CHAMPION: {Name(e.Trait).ToUpper()}";
        s.BannerTimer = 2.4f;
        s.AddFloater(e.Pos + new Vector2(0, -30), Name(e.Trait), Palette.Hex("ffd66b"));
    }

    /// <summary>Warbringer enrage: a speed multiplier that climbs as the champion loses HP.</summary>
    public static float EnrageSpeed(Enemy e)
        => e.Champion && e.Trait == ChampionTrait.Warbringer
            ? 1f + (1f - MathUtils.Clamp(e.Health / e.MaxHealth, 0f, 1f)) * 0.7f
            : 1f;
}
