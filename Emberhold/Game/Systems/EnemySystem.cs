using System.Numerics;
using Emberhold.Core;
using Emberhold.Render;

namespace Emberhold.Game;

/// <summary>
/// Moves enemies along their lane to the keep, applies status effects (slow,
/// burn), and resolves attacks on the keep and contact damage to the hero.
/// </summary>
public static class EnemySystem
{
    public static void Update(GameState s, float dt)
    {
        var hero = s.Hero;
        foreach (var e in s.Enemies)
        {
            if (e.Dead) continue;

            e.HitTimer = MathF.Max(0f, e.HitTimer - dt);
            e.AttackTimer -= dt;

            // Burn damage-over-time. Frostfire makes slowed+burning enemies shatter.
            if (e.BurnTimer > 0f)
            {
                e.BurnTimer -= dt;
                float burn = e.BurnDps;
                if (s.FrostfireActive && e.SlowTimer > 0f) burn *= 2.2f;
                CombatSystem.DamageEnemy(s, e, burn * dt, mitigable: false);
                if (e.Dead) continue;
            }

            // Slow expiry.
            float speedScale = 1f;
            if (e.SlowTimer > 0f)
            {
                e.SlowTimer -= dt;
                speedScale = e.SlowFactor;
                if (e.SlowTimer <= 0f) e.SlowFactor = 1f;
            }

            if (!e.Inside && MathF.Abs(e.Pos.X) < s.FortHalfSize && MathF.Abs(e.Pos.Y) < s.FortHalfSize)
                e.Inside = true;

            // Healers periodically mend nearby wounded enemies.
            if (e.Healer)
            {
                e.HealTimer -= dt;
                if (e.HealTimer <= 0f)
                {
                    e.HealTimer = 2f;
                    foreach (var other in s.Enemies)
                        if (other != e && !other.Dead && other.Health < other.MaxHealth
                            && Vector2.Distance(e.Pos, other.Pos) < 90f)
                        {
                            other.Health = MathF.Min(other.MaxHealth, other.Health + 14f);
                            s.AddParticles(other.Pos, Palette.Hex("8fd08a"), 4, 30f);
                        }
                }
            }

            float reach = e.Radius + Map.KeepRadius;
            var blockingWall = e.Flying ? null : NearestBlockingWall(s, e); // flyers pass over walls

            if (Vector2.Distance(e.Pos, Map.KeepPos) <= reach)
            {
                if (e.AttackTimer <= 0f)
                {
                    e.AttackTimer = 0.85f;
                    s.KeepHealth -= e.Damage;
                    s.KickShake(7f);
                    s.AddParticles(Map.KeepPos, Palette.Hex("d37455"), 8, 54f);
                    s.AddFloater(Map.KeepPos + new Vector2(0, -30), $"-{e.Damage}", Palette.Hex("ef896c"));
                }
            }
            else if (blockingWall is not null)
            {
                if (e.AttackTimer <= 0f)
                {
                    e.AttackTimer = 0.85f;
                    float wallDamage = e.Damage * (s.Fortified ? 0.65f : 1f); // Fortified amplifier
                    blockingWall.Health -= wallDamage;
                    s.KickShake(4f);
                    s.AddParticles(blockingWall.Pos, Palette.Hex("b98a59"), 5, 38f);
                    s.AddFloater(blockingWall.Pos + new Vector2(0, -18), $"-{(int)wallDamage}", Palette.Hex("efb36c"));
                    if (blockingWall.Retaliate) CombatSystem.DamageEnemy(s, e, 12f);
                }
            }
            else
            {
                var dir = MathUtils.Normalize(Map.KeepPos - e.Pos);
                var delta = dir * e.Speed * speedScale * dt;
                e.Pos = e.Flying ? e.Pos + delta : Geometry.MoveWithCollisions(e.Pos, e.Radius, delta, s.SolidRects());
            }

            if (Vector2.Distance(e.Pos, hero.Pos) < e.Radius + hero.Radius && hero.Invulnerable <= 0f)
            {
                hero.Health -= MathF.Max(4f, e.Damage * 0.65f);
                hero.Invulnerable = 0.75f;
                s.AddParticles(hero.Pos, Palette.Hex("d9795c"), 8, 58f);
            }
        }
    }

    /// <summary>An alive wall the enemy is pressed against on its way to the keep.</summary>
    private static Structure? NearestBlockingWall(GameState s, Enemy e)
    {
        Structure? best = null;
        float bestDist = float.PositiveInfinity;
        foreach (var st in s.Structures)
        {
            if (!st.IsWallAlive) continue;
            float d = Vector2.Distance(e.Pos, st.Pos);
            if (d > e.Radius + st.Radius + 3f) continue;
            if (d < bestDist) { bestDist = d; best = st; }
        }
        return best;
    }
}
