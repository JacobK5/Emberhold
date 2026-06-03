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
                  : hero.Kind == HeroKind.Warden ? Palette.Hex("b9d9bd")
                  : hero.Kind == HeroKind.Elementalist ? Palette.Hex("8fd6e8")
                  : Palette.Hex("f7df9a");
        // Skill nodes: Ranger Ricochet / Elementalist Arc chain; Warden Cleave splashes; Ranger Piercing pierces.
        int heroChains = hero.Has(HeroSkills.RRicochet) || hero.Has(HeroSkills.EArc) ? 1 : 0;
        float heroSplash = hero.Has(HeroSkills.WCleave) ? 34f : 0f;
        bool heroPierce = heroChains == 0 && hero.Has(HeroSkills.RPierce);
        // Executioner Deathmark: hero shots hit elites/bosses harder.
        float markMult = hero.Has(HeroSkills.XMark) && (target.Elite || target.Boss) ? 1.25f : 1f;
        // Elementalist: bolts chill (slow) on hit; Shatter rewards hitting already-slowed foes.
        bool frost = hero.Kind == HeroKind.Elementalist;
        float shatterMult = hero.Has(HeroSkills.EShatter) && target.SlowTimer > 0f ? 1.35f : 1f;
        FireProjectile(s, hero.Pos, aim, damage: hero.Damage * profile.Damage * Balance.HeroDamageMult * s.StreakDamageMult * s.Modifier.HeroDamageMult * markMult * shatterMult,
            speed: HeroProjSpeed, color: color, source: ProjectileSource.Hero,
            splash: heroSplash, chains: heroChains, chainRange: heroChains > 0 ? 150f : 0f, pierce: heroPierce,
            slowFactor: frost ? 0.65f : 1f, slowDuration: frost ? 1.6f : 0f);

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
                if (p.Source == ProjectileSource.Hero) OnHeroHit(s, e, dmg);

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
        if (e.StatusImmune) return; // Wraiths shrug off burn and slow
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
            else if (d.Kind == DropKind.Cache)
            {
                s.EarnGold(d.Value);
                s.Live.GoldEarned += d.Value;
                s.AddParticles(d.Pos, Palette.Gold, 18, 78f);
                s.AddFloater(d.Pos + new Vector2(0, -10), $"+{d.Value} CACHE", Palette.Hex("ffd66b"));
                s.KickShake(4f);
            }
            else
            {
                s.EarnGold(d.Value);
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
        if (pool.Count == 0) { s.EarnGold(25); s.AddFloater(at + new Vector2(0, -8), "+25", Palette.Gold); return; }

        var relic = pool[(int)(s.Rand() * pool.Count) % pool.Count];
        hero.Relics.Add(relic);

        // Relics are run-wide gear: apply to every hero kind so swapping keeps them.
        string name;
        switch (relic)
        {
            case RelicKind.EmberRing:   hero.ApplyToAll(p => p.Damage *= 1.12f); name = "EMBER RING  +12% dmg"; break;
            case RelicKind.SwiftBoots:  hero.ApplyToAll(p => p.Speed *= 1.14f); name = "SWIFT BOOTS  +14% speed"; break;
            case RelicKind.WardenCloak: hero.ApplyToAll(p => { p.MaxHealth += 30f; p.Health += 30f; }); name = "WARDEN'S CLOAK  +30 HP"; break;
            default:                    hero.ApplyToAll(p => p.Range += 36f); name = "HAWK EYE  +36 range"; break;
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
            s.BossKills += 1;
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

        if (enemy.General) RoutWave(s, enemy);

        // Announce a freshly reached streak tier.
        if (tierUp && s.StreakTier > 0)
        {
            s.AddFloater(enemy.Pos + new Vector2(0, -28), $"{GameState.StreakLabel(s.StreakTier)} x{s.Streak}", Palette.Hex("ff9a4d"));
            s.AddParticles(enemy.Pos, Palette.Fire, 14, 66f);
            s.KickShake(4f);
            // Reaching the blazing tier rewards an Overdrive burst.
            if (s.StreakTier >= 3)
            {
                s.Hero.Overdrive = MathF.Max(s.Hero.Overdrive, 6f);
                s.AddFloater(s.Hero.Pos + new Vector2(0, -34), "OVERDRIVE!", Palette.Hex("ffb064"));
            }
        }
    }

    /// <summary>Killing the Raider General shatters the wave's morale: every other
    /// non-boss raider on the field is cut down (you still collect their bounty).</summary>
    private static void RoutWave(GameState s, Enemy general)
    {
        s.BannerText = "WAVE ROUTED";
        s.BannerTimer = 2.4f;
        s.AddFloater(general.Pos + new Vector2(0, -34), "ROUTED!", Palette.Gold);
        s.KickShake(8f);
        // Snapshot the current foes so the cascade of deaths doesn't re-enter mid-loop.
        var routed = new List<Enemy>();
        foreach (var e in s.Enemies)
            if (e != general && !e.Dead && !e.Boss && !e.General) routed.Add(e);
        foreach (var e in routed)
            DamageEnemy(s, e, e.Health + 9999f, mitigable: false);
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
            hero.Cur.SkillPoints += 1; // each level grants a point to spend in the hero's tree
            s.AddParticles(hero.Pos, Palette.Hex("ffd36c"), 18, 75f);
            s.AddFloater(hero.Pos + new Vector2(0, -34), "+1 SKILL POINT  (K)", Palette.Hex("b9e0ff"));
            s.AddParticles(hero.Pos, Palette.Hex("bfe0ff"), 12, 66f);
        }
    }

    /// <summary>Side-effects when a hero attack lands: Bloodthirst lifesteal + Rend slow.</summary>
    private static void OnHeroHit(GameState s, Enemy e, float dmg)
    {
        var hero = s.Hero;
        if (hero.Has(HeroSkills.WLifesteal) && hero.Health > 0f)
            hero.Health = MathF.Min(hero.MaxHealth, hero.Health + dmg * 0.06f);
        if (hero.Has(HeroSkills.WRend) && !e.StatusImmune && !e.Dead)
        {
            float factor = e.Boss ? 0.7f : 0.5f;
            e.SlowFactor = e.SlowTimer <= 0f ? factor : MathF.Min(e.SlowFactor, factor);
            e.SlowTimer = MathF.Max(e.SlowTimer, 1f);
        }
    }

    /// <summary>Fire the active hero's signature ability (Space). Per-kind dispatch.</summary>
    public static void Signature(GameState s)
    {
        switch (s.Hero.Kind)
        {
            case HeroKind.Warden: GroundSlam(s); break;
            case HeroKind.Artificer: Overcharge(s); break;
            case HeroKind.Bulwark: BulwarkStance(s); break;
            case HeroKind.Executioner: Execute(s); break;
            case HeroKind.Elementalist: FrostNova(s); break;
            case HeroKind.Beastmaster: RallyPack(s); break;
            default: ShootVolley(s); break;
        }
    }

    /// <summary>Beastmaster signature: summon a burst of temporary wolves to swarm the wave.</summary>
    public static void RallyPack(GameState s)
    {
        var hero = s.Hero;
        if (hero.AbilityCooldown > 0f || s.Over) return;
        hero.AbilityCooldown = hero.VolleyCooldown;
        for (int i = 0; i < 3; i++)
            s.Companions.Add(CompanionSystem.NewWolf(s, permanent: false, life: 9f));
        s.AddParticles(hero.Pos, Palette.Hex("d8c79a"), 22, 110f);
        s.AddFloater(hero.Pos + new Vector2(0, -26), "RALLY!", Palette.Hex("e6d6a8"));
        s.KickShake(5f);
    }

    /// <summary>Elementalist signature: a radial burst that damages and deeply chills
    /// all enemies around the hero (Deep Freeze deepens the slow; Emberwind ignites).</summary>
    public static void FrostNova(GameState s)
    {
        var hero = s.Hero;
        if (hero.AbilityCooldown > 0f || s.Over) return;
        hero.AbilityCooldown = hero.VolleyCooldown;

        const float radius = 135f;
        bool deep = hero.Has(HeroSkills.EDeepFreeze);
        float dmg = hero.Damage * 1.5f * hero.Profile.Damage * Balance.HeroDamageMult * s.StreakDamageMult * s.Modifier.HeroDamageMult;
        foreach (var e in s.Enemies)
        {
            if (e.Dead) continue;
            if (Vector2.Distance(e.Pos, hero.Pos) > radius + e.Radius) continue;
            DamageEnemy(s, e, dmg);
            if (e.StatusImmune) continue; // wraiths shrug off frost/burn
            float factor = deep ? 0.25f : 0.45f;
            float dur = deep ? 3.2f : 2.2f;
            e.SlowFactor = e.SlowTimer <= 0f ? factor : MathF.Min(e.SlowFactor, factor);
            e.SlowTimer = MathF.Max(e.SlowTimer, dur);
            if (hero.Has(HeroSkills.EEmber))
            {
                e.BurnTimer = MathF.Max(e.BurnTimer, 2.2f);
                e.BurnDps = MathF.Max(e.BurnDps, 9f);
            }
        }
        s.AddParticles(hero.Pos, Palette.Hex("9fe0ee"), 28, 150f);
        s.AddFloater(hero.Pos + new Vector2(0, -24), "FROST NOVA", Palette.Hex("bfeefa"));
        s.KickShake(5f);
    }

    /// <summary>Executioner signature: blink to the weakest enemy in reach and strike;
    /// finishes off anything below the execute threshold (bosses are immune to the
    /// instakill but still take the burst).</summary>
    public static void Execute(GameState s)
    {
        var hero = s.Hero;
        if (hero.AbilityCooldown > 0f || s.Over) return;

        float range = hero.Range * hero.Profile.Range * Balance.HeroRangeMult * 1.5f;
        Enemy? target = null;
        float lowest = float.MaxValue;
        foreach (var e in s.Enemies)
        {
            if (e.Dead) continue;
            if (Vector2.Distance(hero.Pos, e.Pos) > range) continue;
            if (e.Health < lowest) { lowest = e.Health; target = e; }
        }
        if (target is null) return; // nothing to execute — don't waste the cooldown
        hero.AbilityCooldown = hero.VolleyCooldown;

        // Blink adjacent to the target.
        var dir = MathUtils.Normalize(target.Pos - hero.Pos);
        if (dir == Vector2.Zero) dir = hero.Facing;
        var dest = target.Pos - dir * (target.Radius + hero.Radius + 2f);
        float roam = s.RoamLimit;
        hero.Pos = new Vector2(MathUtils.Clamp(dest.X, -roam, roam), MathUtils.Clamp(dest.Y, -roam, roam));
        hero.Facing = dir;
        hero.Invulnerable = MathF.Max(hero.Invulnerable, 0.3f);
        s.AddParticles(target.Pos, Palette.Hex("b03a4a"), 18, 96f);

        float threshold = hero.Has(HeroSkills.XHeadsman) ? 0.35f : 0.22f;
        bool canExecute = !target.Boss && target.Health <= target.MaxHealth * threshold;
        if (canExecute)
        {
            DamageEnemy(s, target, target.Health + 9999f, mitigable: false);
            s.AddFloater(target.Pos + new Vector2(0, -22), "EXECUTED", Palette.Hex("ff7a6a"));
            s.KickShake(6f);
            if (hero.Has(HeroSkills.XReap))
            {
                hero.AbilityCooldown = MathF.Max(0f, hero.AbilityCooldown - hero.VolleyCooldown * 0.5f);
                for (int i = 0; i < 3; i++) s.SpawnDrop(target.Pos + new Vector2(s.Rand(-10, 10), s.Rand(-10, 10)), 1);
            }
        }
        else
        {
            float dmg = hero.Damage * 2.4f * hero.Profile.Damage * Balance.HeroDamageMult * s.StreakDamageMult * s.Modifier.HeroDamageMult;
            if (target.Elite || target.Boss) dmg *= 1.4f; // assassins hit big targets harder
            DamageEnemy(s, target, dmg);
            OnHeroHit(s, target, dmg);
            s.AddFloater(target.Pos + new Vector2(0, -22), "STRIKE", Palette.Hex("ff9a8a"));
        }
    }

    /// <summary>Bulwark signature: brace — heavy damage reduction + a wide taunt that
    /// pulls enemies onto your body, holding the lane.</summary>
    public static void BulwarkStance(GameState s)
    {
        var hero = s.Hero;
        if (hero.AbilityCooldown > 0f || s.Over) return;
        hero.AbilityCooldown = hero.VolleyCooldown;
        hero.StanceTimer = hero.Has(HeroSkills.BAnchor) ? 7f : 4f;
        s.AddParticles(hero.Pos, Palette.Hex("aeb98c"), 24, 96f);
        s.AddFloater(hero.Pos + new Vector2(0, -26), "BRACE!", Palette.Hex("d8e0b4"));
        s.KickShake(6f);
    }

    public static void ShootVolley(GameState s)
    {
        var hero = s.Hero;
        var profile = hero.Profile;
        if (hero.AbilityCooldown > 0f || s.Over) return;
        hero.AbilityCooldown = hero.VolleyCooldown;

        // Wide Volley node widens the fan to 9 arrows; Arrow Storm makes them splash.
        int spread = hero.Has(HeroSkills.RWide) ? 4 : 3;
        float storm = hero.Has(HeroSkills.RStorm) ? 40f : 0f;
        float baseAngle = MathF.Atan2(hero.Facing.Y, hero.Facing.X);
        for (int i = -spread; i <= spread; i++)
        {
            float a = baseAngle + i * 0.16f;
            var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
            FireProjectile(s, hero.Pos, hero.Pos + dir,
                damage: hero.Damage * hero.VolleyDamage * profile.Damage * s.StreakDamageMult * s.Modifier.HeroDamageMult,
                speed: 470f, color: Palette.Hex("ffd46f"), source: ProjectileSource.Hero,
                life: 1.45f, radius: 4f,
                splash: MathF.Max(storm, s.VolleySplash ? 46f : 0f)); // Arrow Storm / Ember Battery keystone
        }
        s.AddParticles(hero.Pos, Palette.Hex("ffd46f"), 16, 62f);
        s.KickShake(4f);
    }

    /// <summary>Warden signature: a radial shockwave that damages, knocks back and slows.</summary>
    public static void GroundSlam(GameState s)
    {
        var hero = s.Hero;
        if (hero.AbilityCooldown > 0f || s.Over) return;
        hero.AbilityCooldown = hero.VolleyCooldown;

        const float radius = 120f;
        float dmg = hero.Damage * 2.0f * hero.Profile.Damage * Balance.HeroDamageMult * s.StreakDamageMult * s.Modifier.HeroDamageMult;
        foreach (var e in s.Enemies)
        {
            if (e.Dead) continue;
            float d = Vector2.Distance(e.Pos, hero.Pos);
            if (d > radius + e.Radius) continue;
            DamageEnemy(s, e, dmg);
            OnHeroHit(s, e, dmg);
            if (!e.StatusImmune && !e.Boss)
            {
                // Knock the enemy outward from the slam, and slow it briefly.
                var push = MathUtils.Normalize(e.Pos - hero.Pos) * 34f;
                e.Pos += push;
                e.SlowFactor = e.SlowTimer <= 0f ? 0.45f : MathF.Min(e.SlowFactor, 0.45f);
                e.SlowTimer = MathF.Max(e.SlowTimer, 1.6f);
            }
        }
        s.AddParticles(hero.Pos, Palette.Hex("c8a37a"), 26, 150f);
        s.AddFloater(hero.Pos + new Vector2(0, -24), "SLAM", Palette.Hex("e7c79a"));
        s.KickShake(8f);
    }

    /// <summary>Artificer signature: a fort-wide tower frenzy for several seconds.</summary>
    public static void Overcharge(GameState s)
    {
        var hero = s.Hero;
        if (hero.AbilityCooldown > 0f || s.Over) return;
        hero.AbilityCooldown = hero.VolleyCooldown;
        s.OverchargeTimer = hero.Has(HeroSkills.ASurge) ? 8f : 5f;
        s.AddParticles(hero.Pos, Palette.Hex("6fd0e0"), 24, 120f);
        s.AddFloater(hero.Pos + new Vector2(0, -24), "OVERCHARGE", Palette.Hex("9fe6f2"));
        s.BannerText = "TOWERS OVERCHARGED";
        s.BannerTimer = 1.6f;
        s.KickShake(5f);
    }

    public static void Dash(GameState s)
    {
        var hero = s.Hero;
        if (hero.DashCooldown > 0f || s.Over) return;
        hero.Pos = Geometry.MoveWithCollisions(hero.Pos, hero.Radius, hero.Facing * 96f, s.SolidRects());
        float roam = s.RoamLimit;
        hero.Pos = new Vector2(MathUtils.Clamp(hero.Pos.X, -roam, roam), MathUtils.Clamp(hero.Pos.Y, -roam, roam));
        // Shadowstep (Executioner): faster dash with longer i-frames.
        bool swift = hero.Has(HeroSkills.XSwift);
        hero.DashCooldown = swift ? 1.45f : 2.4f;
        hero.Invulnerable = MathF.Max(hero.Invulnerable, swift ? 0.5f : 0.3f); // brief i-frames: dash through danger

        // Dash strike: burst enemies you land among; slowed enemies shatter for more.
        float dmg = hero.Damage * 1.6f * hero.Profile.Damage * Balance.HeroDamageMult * s.Modifier.HeroDamageMult;
        bool hitAny = false;
        foreach (var e in s.Enemies)
        {
            if (e.Dead) continue;
            if (Vector2.Distance(e.Pos, hero.Pos) > e.Radius + 34f) continue;
            float strike = dmg * (e.SlowTimer > 0f ? 1.3f : 1f);
            DamageEnemy(s, e, strike);
            OnHeroHit(s, e, strike);
            hitAny = true;
        }
        if (hitAny)
        {
            s.AddFloater(hero.Pos + new Vector2(0, -22), "STRIKE", Palette.Hex("d5ebc5"));
            s.KickShake(5f);
        }

        s.AddParticles(hero.Pos, Palette.Hex("d5ebc5"), 14, 88f);
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
