using System.Numerics;
using Emberhold.Core;
using Emberhold.Data;
using Emberhold.Render;
using Raylib_cs;

namespace Emberhold.Game;

/// <summary>
/// Hero offence: auto-fire, projectiles, damage resolution + drops, XP/leveling,
/// the volley + dash abilities, and gold/ember pickups. Towers reuse DamageEnemy
/// and FireProjectile once they come online.
/// </summary>
public static class CombatSystem
{
    private const float HeroProjSpeed = 390f;

    public static void UpdateHeroCombat(GameState s, float dt)
    {
        var hero = s.Hero;
        var profile = hero.Profile;
        hero.ShotTimer -= dt;
        if (hero.ShotTimer > 0f) return;

        float range = hero.Range * profile.Range * Balance.HeroRangeMult;
        var target = MathUtils.Nearest(hero.Pos, s.Enemies, e => e.Pos,
            e => !e.Dead && Vector2.Distance(hero.Pos, e.Pos) <= range);
        if (target is null) return;

        var aim = AimAhead(hero.Pos, target, HeroProjSpeed);
        var color = s.StreakTier > 0 ? Palette.Hex("ff9a4d")
                  : hero.Kind == HeroKind.Warden ? Palette.Hex("b9d9bd") : Palette.Hex("f7df9a");
        FireProjectile(s, hero.Pos, aim, damage: hero.Damage * profile.Damage * Balance.HeroDamageMult * s.StreakDamageMult,
            speed: HeroProjSpeed, color: color, source: ProjectileSource.Hero);

        hero.Facing = MathUtils.Normalize(target.Pos - hero.Pos);
        hero.ShotTimer = (hero.FireRate / Balance.HeroFireSpeedMult) * profile.Rate * (hero.Overdrive > 0f ? 0.58f : 1f);
    }

    public static void UpdateProjectiles(GameState s, float dt)
    {
        foreach (var p in s.Projectiles)
        {
            p.Pos += p.Vel * dt;
            p.Life -= dt;
            foreach (var e in s.Enemies)
            {
                if (e.Dead) continue;
                if (p.Pierce && p.HitIds is not null && p.HitIds.Contains(e.Id)) continue;
                if (Vector2.Distance(p.Pos, e.Pos) >= p.Radius + e.Radius) continue;

                float dmg = p.Damage;
                if (s.Glacier && p.Source == ProjectileSource.Cannon && e.SlowTimer > 0f) dmg *= 1.4f; // Glacier
                DamageEnemy(s, e, dmg, p.Splash);
                ApplyStatus(s, e, p);

                if (p.ChainsLeft > 0)
                {
                    ChainTo(s, e.Pos, p);
                    p.Life = 0f;
                }
                else if (p.Pierce)
                {
                    (p.HitIds ??= new HashSet<int>()).Add(e.Id);
                    // keeps flying through
                }
                else
                {
                    p.Life = 0f;
                }
                break;
            }
        }
    }

    private static void ApplyStatus(GameState s, Enemy e, Projectile p)
    {
        if (p.SlowFactor < 1f && p.SlowDuration > 0f)
        {
            // Strongest active slow wins; resets fresh once a prior slow has lapsed.
            // CryoForge keystone extends slow duration globally.
            float duration = p.SlowDuration * s.SlowDurationMult;
            e.SlowFactor = e.SlowTimer <= 0f ? p.SlowFactor : MathF.Min(e.SlowFactor, p.SlowFactor);
            e.SlowTimer = MathF.Max(e.SlowTimer, duration);
        }
        if (p.BurnDps > 0f && p.BurnDuration > 0f)
        {
            e.BurnTimer = MathF.Max(e.BurnTimer, p.BurnDuration);
            e.BurnDps = MathF.Max(e.BurnDps, p.BurnDps);
        }
    }

    private static void ChainTo(GameState s, Vector2 from, Projectile p)
    {
        var hit = p.HitIds ??= new HashSet<int>();
        var next = MathUtils.Nearest(from, s.Enemies, e => e.Pos,
            e => !e.Dead && !hit.Contains(e.Id) && Vector2.Distance(from, e.Pos) <= p.ChainRange);
        if (next is null) return;
        hit.Add(next.Id);
        DamageEnemy(s, next, p.Damage * 0.8f);
        ApplyStatus(s, next, p);
        s.AddParticles(next.Pos, p.Color, 4, 40f);
        var residual = new Projectile
        {
            Id = s.NextId(), Pos = next.Pos, Vel = Vector2.Zero, Damage = p.Damage * 0.8f,
            Life = 0f, Radius = p.Radius, Color = p.Color, Source = p.Source,
            ChainsLeft = p.ChainsLeft - 1, ChainRange = p.ChainRange, HitIds = hit,
            SlowFactor = p.SlowFactor, SlowDuration = p.SlowDuration,
            BurnDps = p.BurnDps, BurnDuration = p.BurnDuration,
        };
        if (residual.ChainsLeft > 0) ChainTo(s, next.Pos, residual);
    }

    public static void UpdatePickups(GameState s)
    {
        var hero = s.Hero;
        foreach (var d in s.Drops)
        {
            if (d.Collected) continue;
            if (Vector2.Distance(hero.Pos, d.Pos) >= 24f) continue;

            d.Collected = true;
            if (d.Kind == DropKind.Ember)
            {
                hero.Overdrive = 10f;
                s.AddParticles(d.Pos, Palette.Hex("ff8b52"), 16, 75f);
                s.AddFloater(d.Pos + new Vector2(0, -8), "OVERDRIVE", Palette.Hex("ffb064"));
            }
            else
            {
                s.Gold += d.Value;
                s.Live.GoldEarned += d.Value;
                s.AddParticles(d.Pos, Palette.Gold, 5, 43f);
                s.AddFloater(d.Pos + new Vector2(0, -8), $"+{d.Value}", Palette.Hex("ffd66b"));
            }
        }
    }

    /// <param name="mitigable">Direct hits are reduced by enemy shields; DoT/traps pass true=false to bypass.</param>
    public static void DamageEnemy(GameState s, Enemy enemy, float damage, float splash = 0f, bool mitigable = true)
    {
        if (enemy.Dead) return;
        if (mitigable && enemy.ShieldPerHit > 0f)
            damage = MathF.Max(1f, damage - enemy.ShieldPerHit); // Shielded resists per-hit
        s.Live.DamageDealt += (int)MathF.Round(MathF.Min(damage, MathF.Max(0f, enemy.Health)));
        enemy.Health -= damage;
        enemy.HitTimer = 0.12f;
        s.AddParticles(enemy.Pos, enemy.Elite ? Palette.Elite : Palette.Hex("cf6b52"), 3, 34f);

        if (splash > 0f)
        {
            foreach (var other in s.Enemies)
                if (other != enemy && !other.Dead && Vector2.Distance(enemy.Pos, other.Pos) < splash)
                    DamageEnemy(s, other, damage * 0.45f);
        }

        if (enemy.Health > 0f) return;

        enemy.Dead = true;
        s.Kills += 1;
        s.Live.Kills += 1;
        bool tierUp = s.RegisterStreakKill();
        GrantHeroXp(s, enemy.Elite ? 4 : enemy.Kind == EnemyKind.Brute ? 2 : 1);
        s.AddParticles(enemy.Pos, enemy.Elite ? Palette.Hex("f2a552") : Palette.Hex("bd5d48"), enemy.Elite ? 18 : 10, 72f);
        int reward = enemy.Reward;
        if (s.SpoilsActive && enemy.SlowTimer > 0f) reward += 1; // Spoils synergy
        reward += s.StreakBonusGold; // Hot Streak bonus gold
        for (int i = 0; i < reward; i++)
            s.SpawnDrop(enemy.Pos + new Vector2(s.Rand(-9, 9), s.Rand(-9, 9)), 1);
        if (enemy.Elite) s.SpawnEmber(enemy.Pos);

        // Announce a freshly reached streak tier.
        if (tierUp && s.StreakTier > 0)
        {
            s.AddFloater(enemy.Pos + new Vector2(0, -28), $"{GameState.StreakLabel(s.StreakTier)} x{s.Streak}", Palette.Hex("ff9a4d"));
            s.AddParticles(enemy.Pos, Palette.Fire, 14, 66f);
            s.KickShake(4f);
        }
    }

    public static void GrantHeroXp(GameState s, int amount)
    {
        var hero = s.Hero;
        hero.Xp += amount;
        while (hero.Xp >= hero.NextXp)
        {
            hero.Xp -= hero.NextXp;
            hero.NextXp += 4;
            hero.Level += 1;
            hero.Damage += 1.5f;
            hero.MaxHealth += 6f;
            hero.Health = MathF.Min(hero.MaxHealth, hero.Health + 12f);
            hero.FireRate = MathF.Max(0.22f, hero.FireRate - 0.012f);
            s.AddParticles(hero.Pos, Palette.Hex("ffd36c"), 18, 75f);
        }
    }

    public static void ShootVolley(GameState s)
    {
        var hero = s.Hero;
        var profile = hero.Profile;
        if (hero.AbilityCooldown > 0f || s.Over) return;
        hero.AbilityCooldown = hero.VolleyCooldown;

        float baseAngle = MathF.Atan2(hero.Facing.Y, hero.Facing.X);
        for (int i = -3; i <= 3; i++)
        {
            float a = baseAngle + i * 0.16f;
            var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
            FireProjectile(s, hero.Pos, hero.Pos + dir,
                damage: hero.Damage * hero.VolleyDamage * profile.Damage * s.StreakDamageMult,
                speed: 470f, color: Palette.Hex("ffd46f"), source: ProjectileSource.Hero,
                life: 1.45f, radius: 4f,
                splash: s.VolleySplash ? 46f : 0f); // Ember Battery keystone
        }
        s.AddParticles(hero.Pos, Palette.Hex("ffd46f"), 16, 62f);
        s.KickShake(4f);
    }

    public static void Dash(GameState s)
    {
        var hero = s.Hero;
        if (hero.DashCooldown > 0f || s.Over) return;
        hero.Pos = Geometry.MoveWithCollisions(hero.Pos, hero.Radius, hero.Facing * 96f, s.SolidRects());
        float roam = s.RoamLimit;
        hero.Pos = new Vector2(MathUtils.Clamp(hero.Pos.X, -roam, roam), MathUtils.Clamp(hero.Pos.Y, -roam, roam));
        hero.DashCooldown = 2.4f;
        s.AddParticles(hero.Pos, Palette.Hex("d5ebc5"), 12, 84f);
        s.KickShake(3f);
    }

    // ---- helpers --------------------------------------------------------

    public static void FireProjectile(GameState s, Vector2 from, Vector2 toward,
        float damage, float speed, Color color, ProjectileSource source,
        float life = 1.25f, float radius = 3f, float splash = 0f,
        float slowFactor = 1f, float slowDuration = 0f, float burnDps = 0f, float burnDuration = 0f,
        int chains = 0, float chainRange = 0f, bool pierce = false)
    {
        var dir = MathUtils.Normalize(toward - from);
        s.Projectiles.Add(new Projectile
        {
            Id = s.NextId(), Pos = from, Vel = dir * speed,
            Damage = damage, Life = life, Radius = radius, Color = color,
            Splash = splash, Source = source,
            SlowFactor = slowFactor, SlowDuration = slowDuration,
            BurnDps = burnDps, BurnDuration = burnDuration,
            ChainsLeft = chains, ChainRange = chainRange,
            Pierce = pierce, HitIds = pierce || chains > 0 ? new HashSet<int>() : null,
        });
    }

    /// <summary>Lead a moving enemy (heading to the keep) for a projectile of given speed.</summary>
    public static Vector2 AimAhead(Vector2 source, Enemy target, float projSpeed)
    {
        if (target.Speed <= 0f) return target.Pos;
        var heading = MathUtils.Normalize(Map.KeepPos - target.Pos);
        var ev = heading * target.Speed;
        var dp = target.Pos - source;

        float a = ev.X * ev.X + ev.Y * ev.Y - projSpeed * projSpeed;
        float b = 2f * (dp.X * ev.X + dp.Y * ev.Y);
        float c = dp.X * dp.X + dp.Y * dp.Y;

        if (MathF.Abs(a) < 0.001f)
        {
            float tt = b != 0f ? MathF.Max(0f, -c / b) : 0f;
            return target.Pos + ev * tt;
        }
        float disc = b * b - 4f * a * c;
        if (disc < 0f) return target.Pos;
        float t = MathF.Max(0f, (-b - MathF.Sqrt(disc)) / (2f * a));
        return target.Pos + ev * t;
    }
}
