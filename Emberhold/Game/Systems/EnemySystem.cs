using System.Numerics;
using Emberhold.Core;
using Emberhold.Data;
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
        List<Enemy>? summons = null; // bosses summon adds; appended after the loop to keep iteration safe
        foreach (var e in s.Enemies)
        {
            if (e.Dead) continue;

            // Chapter boss periodically summons a pair of raider adds.
            if (e.Boss)
            {
                e.SummonTimer -= dt;
                if (e.SummonTimer <= 0f)
                {
                    e.SummonTimer = 5.5f;
                    (summons ??= new List<Enemy>()).AddRange(MakeAdds(s, e));
                    s.AddParticles(e.Pos, Palette.Hex("c77a8f"), 14, 64f);
                }
            }

            // Assassin blinks forward toward the keep, phasing past walls and traps.
            if (e.Phantom)
            {
                e.BlinkTimer -= dt;
                if (e.BlinkTimer <= 0f && Vector2.Distance(e.Pos, Map.KeepPos) > Map.KeepRadius + e.Radius + 20f)
                {
                    e.BlinkTimer = 1.8f;
                    var dir = MathUtils.Normalize(Map.KeepPos - e.Pos);
                    s.AddParticles(e.Pos, Palette.Hex("b07bd0"), 8, 55f);
                    e.Pos += dir * 85f;
                    s.AddParticles(e.Pos, Palette.Hex("b07bd0"), 8, 55f);
                }
            }

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

            if (e.Siege)
            {
                UpdateSiege(s, e, speedScale, dt);
            }
            else if (IsTaunted(s, e))
            {
                // Bulwark taunt: the tank's body holds the lane. Enemies funnel onto
                // him and the global contact block (below) deals the hero damage.
                float bodyReach = e.Radius + hero.Radius;
                if (Vector2.Distance(e.Pos, hero.Pos) > bodyReach)
                {
                    var dir = MathUtils.Normalize(hero.Pos - e.Pos);
                    var delta = dir * e.Speed * speedScale * dt;
                    e.Pos = Geometry.MoveWithCollisions(e.Pos, e.Radius, delta, s.SolidRects());
                }
                else if (e.AttackTimer <= 0f)
                {
                    // Thorns / Stance reflect damage; Anchor slows attackers. Rate-limited
                    // per enemy so a whole swarm pressing the tank each gets retaliated on.
                    e.AttackTimer = 0.85f;
                    bool reflect = hero.Has(HeroSkills.BThorns) || hero.StanceTimer > 0f;
                    if (reflect) CombatSystem.DamageEnemy(s, e, 14f + hero.Damage * 0.4f);
                    if (hero.StanceTimer > 0f && hero.Has(HeroSkills.BAnchor) && !e.StatusImmune)
                    {
                        e.SlowFactor = e.SlowTimer <= 0f ? 0.5f : MathF.Min(e.SlowFactor, 0.5f);
                        e.SlowTimer = MathF.Max(e.SlowTimer, 1.5f);
                    }
                }
            }
            else
            {
                bool ignoresWalls = e.Flying || e.Phantom; // flyers and assassins bypass walls
                float reach = e.Radius + Map.KeepRadius;
                var blockingWall = ignoresWalls ? null : NearestBlockingWall(s, e);

                if (Vector2.Distance(e.Pos, Map.KeepPos) <= reach)
                {
                    if (e.General) GeneralBreakthrough(s, e);
                    else AttackKeep(s, e);
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
                    e.Pos = ignoresWalls ? e.Pos + delta : Geometry.MoveWithCollisions(e.Pos, e.Radius, delta, s.SolidRects());
                }
            }

            if (Vector2.Distance(e.Pos, hero.Pos) < e.Radius + hero.Radius && hero.Invulnerable <= 0f)
            {
                hero.Health -= MathF.Max(4f, e.Damage * 0.65f) * hero.DamageTakenMult;
                hero.Invulnerable = 0.75f;
                s.AddParticles(hero.Pos, Palette.Hex("d9795c"), 8, 58f);
            }
        }

        if (summons is not null) s.Enemies.AddRange(summons);
    }

    /// <summary>Whether the Bulwark hero is body-blocking this enemy (in taunt range).</summary>
    private static bool IsTaunted(GameState s, Enemy e)
    {
        var hero = s.Hero;
        if (hero.Kind != HeroKind.Bulwark || hero.Health <= 0f) return false;
        if (e.Flying || e.Phantom) return false; // can't body-block flyers / assassins
        float r = hero.TauntRadius;
        return r > 0f && Vector2.Distance(e.Pos, hero.Pos) <= r;
    }

    /// <summary>Two weakened raiders spawned beside a boss as it summons.</summary>
    private static IEnumerable<Enemy> MakeAdds(GameState s, Enemy boss)
    {
        var stats = WaveStats.For(s.Wave);
        var profile = EnemyProfile.Raider;
        for (int i = 0; i < 2; i++)
        {
            float hp = stats.Health * profile.Health * Balance.EnemyHealthMult * 0.7f;
            yield return new Enemy
            {
                Id = s.NextId(),
                Pos = boss.Pos + new Vector2(s.Rand(-28, 28), s.Rand(-28, 28)),
                Radius = profile.Radius,
                Health = hp,
                MaxHealth = hp,
                Speed = stats.Speed * profile.Speed * Balance.EnemySpeedMult,
                Damage = (int)MathF.Ceiling(stats.Damage * profile.Damage * Balance.EnemyDamageMult),
                Reward = (int)MathF.Ceiling(stats.Reward * profile.Reward * Balance.GoldRewardMult),
                Kind = EnemyKind.Raider,
                Side = boss.Side,
                SlowFactor = 1f,
            };
        }
    }

    /// <summary>The Raider General reached the keep: a heavy blow that rallies the
    /// horde (permanent escalation), then it breaks off. The cost of not killing it.</summary>
    private static void GeneralBreakthrough(GameState s, Enemy e)
    {
        e.Dead = true; // it slips away after landing the blow (no rout reward)
        s.KeepHealth -= e.Damage * 2.5f;
        s.HordeTier += 1;
        s.BannerText = "THE GENERAL RALLIES THE HORDE";
        s.BannerTimer = 2.6f;
        s.AddParticles(Map.KeepPos, Palette.Hex("d37455"), 20, 80f);
        s.KickShake(12f);
    }

    private static void AttackKeep(GameState s, Enemy e)
    {
        if (e.AttackTimer > 0f) return;
        e.AttackTimer = 0.85f;
        s.KeepHealth -= e.Damage;
        s.KickShake(7f);
        s.AddParticles(Map.KeepPos, Palette.Hex("d37455"), 8, 54f);
        s.AddFloater(Map.KeepPos + new Vector2(0, -30), $"-{e.Damage}", Palette.Hex("ef896c"));
    }

    /// <summary>
    /// Siege engines bypass the "march to keep" logic: they hunt the nearest standing
    /// structure (tower / mine / aura / wall) and demolish it, smashing through any
    /// wall in the way. Once nothing's left to wreck they fall on the keep.
    /// </summary>
    private static void UpdateSiege(GameState s, Enemy e, float speedScale, float dt)
    {
        // Smash any wall it's pressed against first (clears its own path).
        var wall = NearestBlockingWall(s, e);
        if (wall is not null) { SiegeAttack(s, e, wall); return; }

        var target = NearestTargetStructure(s, e);
        if (target is not null)
        {
            if (Vector2.Distance(e.Pos, target.Pos) <= e.Radius + target.Radius + 4f)
            { SiegeAttack(s, e, target); return; }
            var dir = MathUtils.Normalize(target.Pos - e.Pos);
            e.Pos = Geometry.MoveWithCollisions(e.Pos, e.Radius, dir * e.Speed * speedScale * dt, s.SolidRects());
            return;
        }

        // No structures remain — march on the keep.
        if (Vector2.Distance(e.Pos, Map.KeepPos) <= e.Radius + Map.KeepRadius) { AttackKeep(s, e); return; }
        var toKeep = MathUtils.Normalize(Map.KeepPos - e.Pos);
        e.Pos = Geometry.MoveWithCollisions(e.Pos, e.Radius, toKeep * e.Speed * speedScale * dt, s.SolidRects());
    }

    private static void SiegeAttack(GameState s, Enemy e, Structure st)
    {
        if (e.AttackTimer > 0f) return;
        e.AttackTimer = 1.0f;
        float dmg = e.Damage * 2.2f; // heavy structural damage
        if (st.Role == StructureRole.Wall && s.Fortified) dmg *= 0.65f;
        st.Health -= dmg;
        s.KickShake(5f);
        s.AddParticles(st.Pos, Palette.Hex("c9a06a"), 8, 50f);
        s.AddFloater(st.Pos + new Vector2(0, -st.Radius - 6), $"-{(int)dmg}", Palette.Hex("efb36c"));
        if (st.Retaliate) CombatSystem.DamageEnemy(s, e, 12f);
    }

    /// <summary>Nearest standing structure a siege engine will target.</summary>
    private static Structure? NearestTargetStructure(GameState s, Enemy e)
    {
        Structure? best = null;
        float bestDist = float.PositiveInfinity;
        foreach (var st in s.Structures)
        {
            bool targetable = st.Role is StructureRole.Tower or StructureRole.Mine or StructureRole.Aura || st.IsWallAlive;
            if (!targetable || st.Health <= 0f) continue;
            float d = Vector2.Distance(e.Pos, st.Pos);
            if (d < bestDist) { bestDist = d; best = st; }
        }
        return best;
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
