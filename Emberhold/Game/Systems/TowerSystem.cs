using System.Numerics;
using Emberhold.Core;
using Emberhold.Render;
using Raylib_cs;

namespace Emberhold.Game;

/// <summary>
/// Drives attack structures: acquire targets in (aura-extended) range and fire
/// projectiles carrying the tower's splash / pierce / chain / status payload.
/// Support auras (banner/forge/watchtower) buff towers within their radius.
/// </summary>
public static class TowerSystem
{
    public static void Update(GameState s, float dt)
    {
        foreach (var t in s.Structures)
        {
            if (t.Role != StructureRole.Tower) continue;
            t.Cooldown -= dt;
            t.MuzzleFlash = MathF.Max(0f, t.MuzzleFlash - dt);

            var (dmgMult, rateMult, rangeBonus) = Aura(s, t);

            // Artificer hero: nearby towers get a personal overclock while she's active.
            if (s.Hero.Kind == Data.HeroKind.Artificer)
            {
                float auraR = s.Hero.Has(Data.HeroSkills.AWideAura) ? 225f : 165f; // Broadcast node widens it
                if (System.Numerics.Vector2.Distance(t.Pos, s.Hero.Pos) <= auraR)
                {
                    dmgMult *= s.Hero.Has(Data.HeroSkills.AOverclock) ? 1.5f : 1.35f; // Overclock node is stronger
                    rateMult *= 0.85f;
                }
            }

            // Overcharge signature: every tower in the fort frenzies for a few seconds.
            if (s.OverchargeTimer > 0f) { dmgMult *= 1.5f; rateMult *= 0.7f; }

            // Fortified Ground: towers in an upgraded quadrant hit harder.
            dmgMult *= s.ZoneBonus(t.Pos);

            if (s.VolatilePact) rateMult *= 0.85f; // anti-synergy: fort-wide fire-rate boost

            float range = t.Range + rangeBonus + t.SynRangeBonus;
            int chains = t.ChainCount + t.SynExtraChains;

            var target = MathUtils.Nearest(t.Pos, s.Enemies, e => e.Pos,
                e => !e.Dead && Vector2.Distance(t.Pos, e.Pos) <= range);
            if (target is null) continue;

            // Rotate the barrel toward the target every frame (even between shots).
            var aimDir = MathUtils.Normalize(target.Pos - t.Pos);
            if (aimDir != System.Numerics.Vector2.Zero)
                t.Facing = MathUtils.Normalize(t.Facing + (aimDir - t.Facing) * MathF.Min(1f, dt * 12f));

            if (t.Cooldown > 0f) continue; // aimed, but not ready to fire

            // Burn payload: native + Wildfire keystone (chains ignite) + Hellfire field (cannon ignites).
            float burnDps = MathF.Max(t.BurnDps, t.SynBurnDps);
            float burnDur = MathF.Max(t.BurnDuration, t.SynBurnDuration);
            if (s.Wildfire && t.ChainCount > 0) { burnDps = MathF.Max(burnDps, 8f); burnDur = MathF.Max(burnDur, 2f); }

            // Slow payload: native + Conduit field (chains slow).
            float slowFactor = MathF.Min(t.SlowFactor, t.SynSlowFactor);
            float slowDur = MathF.Max(t.SlowDuration, t.SynSlowDuration);

            var aim = CombatSystem.AimAhead(t.Pos, target, t.ProjSpeed);
            CombatSystem.FireProjectile(s, t.Pos, aim,
                damage: t.Damage * dmgMult * t.SynDamageMult * Balance.TowerDamageMult,
                speed: t.ProjSpeed,
                color: ColorFor(t.ProjSource),
                source: t.ProjSource,
                life: 1.4f,
                radius: t.ProjSource is ProjectileSource.Cannon ? 6f : 4f,
                splash: t.Splash + t.SynSplashBonus,
                slowFactor: slowFactor, slowDuration: slowDur,
                burnDps: burnDps, burnDuration: burnDur,
                chains: chains, chainRange: chains > 0 ? 130f : 0f,
                pierce: t.Pierce);

            // Muzzle flash + spark at the barrel tip.
            t.MuzzleFlash = 0.08f;
            s.AddParticles(t.Pos + t.Facing * (t.Radius + 6f), ColorFor(t.ProjSource), 4, 64f);

            t.Cooldown = t.Rate * rateMult / Balance.TowerFireSpeedMult;
        }
    }

    /// <summary>Aggregate support-aura effects on a tower: (damage×, rate×, +range).</summary>
    public static (float dmgMult, float rateMult, float rangeBonus) Aura(GameState s, Structure tower)
    {
        float dmg = 1f, rate = 1f, range = 0f;
        foreach (var a in s.Structures)
        {
            if (a.Role != StructureRole.Aura) continue;
            float reach = s.AurasGlobal ? float.PositiveInfinity : a.AuraRange;
            if (Vector2.Distance(tower.Pos, a.Pos) > reach) continue;
            switch (a.AuraKind)
            {
                case AuraKind.Damage: dmg *= a.AuraMagnitude; break;
                case AuraKind.Rate:   rate *= a.AuraMagnitude; break;
                case AuraKind.Range:  range += a.AuraMagnitude; break;
            }
        }
        return (dmg, rate, range);
    }

    private static Color ColorFor(ProjectileSource src) => src switch
    {
        ProjectileSource.Cannon => Palette.Hex("ec8b4d"),
        ProjectileSource.Ballista => Palette.Hex("d4e8aa"),
        ProjectileSource.Chain => Palette.Hex("a9d8ff"),
        ProjectileSource.Flame => Palette.Fire,
        _ => Palette.Hex("cfdfb2"),
    };
}
