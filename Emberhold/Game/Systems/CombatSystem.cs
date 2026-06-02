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
        // Signature passive (lv5): Ranger shots ricochet to a second target; Warden shots cleave.
        int heroChains = hero.Signature && hero.Kind == HeroKind.Ranger ? 1 : 0;
        float heroSplash = hero.Signature && hero.Kind == HeroKind.Warden ? 34f : 0f;
        FireProjectile(s, hero.Pos, aim, damage: hero.Damage * profile.Damage * Balance.HeroDamageMult * s.StreakDamageMult,
            speed: HeroProjSpeed, color: color, source: ProjectileSource.Hero,
            splash: heroSplash, chains: heroChains, chainRange: heroChains > 0 ? 150f : 0f);

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
            // CryoForge keystone extends slow duration globally; bosses resist slows.
            float duration = p.SlowDuration * s.SlowDurationMult;
            float factor = p.SlowFactor;
            if (e.Boss) { duration *= 0.45f; factor = MathF.Min(1f, factor + 0.3f); }
            e.SlowFactor = e.SlowTimer <= 0f ? factor : MathF.Min(e.SlowFactor, factor);
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
            float reach = d.Kind == DropKind.Gold ? hero.PickupRadius : 24f;
            if (Vector2.Distance(hero.Pos, d.Pos) >= reach) continue;

            d.Collected = true;
            if (d.Kind == DropKind.Ember)
            {
                hero.Overdrive = 10f;
                s.AddParticles(d.Pos, Palette.Hex("ff8b52"), 16, 75f);
                s.AddFloater(d.Pos + new Vector2(0, -8), "OVERDRIVE", Palette.Hex("ffb064"));
            }
            else if (d.Kind == DropKind.Relic)
            {
                CollectRelic(s, d.Pos);
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

    /// <summary>Grant a random not-yet-owned relic and apply its permanent run bonus.</summary>
    private static void CollectRelic(GameState s, Vector2 at)
    {
        var hero = s.Hero;
        var pool = new List<RelicKind>();
        foreach (RelicKind k in Enum.GetValues<RelicKind>())
            if (!hero.Relics.Contains(k)) pool.Add(k);
        if (pool.Count == 0) { s.Gold += 25; s.AddFloater(at + new Vector2(0, -8), "+25", Palette.Gold); return; }

        var relic = pool[(int)(s.Rand() * pool.Count) % pool.Count];
        hero.Relics.Add(relic);

        string name;
        switch (relic)
        {
            case RelicKind.EmberRing:   hero.Damage *= 1.12f; name = "EMBER RING  +12% dmg"; break;
            case RelicKind.SwiftBoots:  hero.Speed *= 1.14f; name = "SWIFT BOOTS  +14% speed"; break;
            case RelicKind.WardenCloak: hero.MaxHealth += 30f; hero.Health += 30f; name = "WARDEN'S CLOAK  +30 HP"; break;
            default:                    hero.Range += 36f; name = "HAWK EYE  +36 range"; break;
        }
        s.AddParticles(at, Palette.Hex("c9a3ff"), 18, 80f);
        s.AddFloater(at + new Vector2(0, -10), name, Palette.Hex("d9b6ff"));
        s.KickShake(4f);
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
        GrantHeroXp(s, enemy.Boss ? 12 : enemy.Elite ? 4 : enemy.Kind == EnemyKind.Brute ? 2 : 1);
        bool fancy = enemy.Elite || enemy.Boss;
        s.AddParticles(enemy.Pos, fancy ? Palette.Hex("f2a552") : Palette.Hex("bd5d48"), enemy.Boss ? 32 : enemy.Elite ? 18 : 10, enemy.Boss ? 96f : 72f);
        int reward = enemy.Reward;
        if (s.SpoilsActive && enemy.SlowTimer > 0f) reward += 1; // Spoils synergy
        reward += s.StreakBonusGold; // Hot Streak bonus gold
        for (int i = 0; i < reward; i++)
            s.SpawnDrop(enemy.Pos + new Vector2(s.Rand(-9, 9), s.Rand(-9, 9)), 1);
        bool relicSpace = s.Hero.Relics.Count < Enum.GetValues<RelicKind>().Length;
        if (enemy.Boss)
        {
            // Guaranteed reward + a permanent horde escalation.
            s.SpawnEmber(enemy.Pos);
            if (relicSpace) s.SpawnRelic(enemy.Pos);
            for (int i = 0; i < 10; i++)
                s.SpawnDrop(enemy.Pos + new Vector2(s.Rand(-22, 22), s.Rand(-22, 22)), 1, fromMine: true);
            s.HordeTier += 1;
            s.BannerText = "THE HORDE GROWS STRONGER";
            s.BannerTimer = 2.6f;
            s.AddFloater(enemy.Pos + new Vector2(0, -38), "BOSS SLAIN", Palette.Gold);
            s.KickShake(12f);
        }
        else if (enemy.Elite)
        {
            s.SpawnEmber(enemy.Pos);
            // Elites also yield equipment until the hero has collected the full set.
            if (relicSpace) s.SpawnRelic(enemy.Pos + new Vector2(s.Rand(-14, 14), s.Rand(-14, 14)));
        }

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

            // Announce a passive ability unlocked at this level.
            string passive = Hero.PassiveName(hero.Level, hero.Kind);
            if (passive.Length > 0)
            {
                s.AddFloater(hero.Pos + new Vector2(0, -34), $"{passive} UNLOCKED", Palette.Hex("b9e0ff"));
                s.AddParticles(hero.Pos, Palette.Hex("bfe0ff"), 16, 70f);
            }
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
