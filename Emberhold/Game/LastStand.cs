using System.Numerics;
using Emberhold.Core;
using Emberhold.Render;

namespace Emberhold.Game;

/// <summary>
/// Last Stand: when the keep is critically wounded (&lt; 30% HP), it rallies and emits a
/// defensive nova every few seconds — searing and knocking back the raiders pressing it.
/// A dramatic comeback beat that can buy a losing run a few more waves.
/// </summary>
public static class LastStand
{
    public const float Threshold = 0.3f;
    private const float PulsePeriod = 3.5f;
    private const float Radius = 165f;

    public static bool Active(GameState s) => s.KeepHealth > 0f && s.KeepHealth / s.KeepMaxHealth < Threshold;

    public static void Update(GameState s, float dt)
    {
        if (!Active(s))
        {
            s.LastStandAnnounced = false;
            s.KeepPulseTimer = 0f;
            return;
        }

        if (!s.LastStandAnnounced)
        {
            s.LastStandAnnounced = true;
            s.BannerText = "THE KEEP RALLIES";
            s.BannerTimer = 2.4f;
            s.KeepPulseTimer = 0.8f; // a quick first pulse when it kicks in
        }

        s.KeepPulseTimer -= dt;
        if (s.KeepPulseTimer > 0f) return;
        s.KeepPulseTimer = PulsePeriod;
        Nova(s);
    }

    /// <summary>One defensive shockwave from the keep: area damage + knockback + slow.</summary>
    public static void Nova(GameState s)
    {
        float dmg = 30f + s.Wave * 2.5f;
        foreach (var e in s.Enemies)
        {
            if (e.Dead) continue;
            if (Vector2.Distance(e.Pos, Map.KeepPos) > Radius + e.Radius) continue;
            CombatSystem.DamageEnemy(s, e, e.Boss ? dmg * 0.5f : dmg, mitigable: false);
            if (!e.Boss)
            {
                var dir = MathUtils.Normalize(e.Pos - Map.KeepPos);
                if (dir != Vector2.Zero) e.Pos += dir * 30f;
            }
            if (!e.StatusImmune)
            {
                e.SlowFactor = e.SlowTimer <= 0f ? 0.55f : MathF.Min(e.SlowFactor, 0.55f);
                e.SlowTimer = MathF.Max(e.SlowTimer, 1.4f);
            }
        }

        s.AddParticles(Map.KeepPos, Palette.Hex("ffd66b"), 30, 180f);
        s.UltFxPos = Map.KeepPos;       // reuse the shockwave ring effect
        s.UltFxTimer = 0.5f;
        s.KickShake(9f);
        s.AddFloater(Map.KeepPos + new Vector2(0, -32), "KEEP NOVA", Palette.Hex("ffb04a"));
    }
}
