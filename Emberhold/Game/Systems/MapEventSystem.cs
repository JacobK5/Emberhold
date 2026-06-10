using System.Numerics;
using Emberhold.Data;
using Raylib_cs;

namespace Emberhold.Game;

/// <summary>
/// Dynamic battlefield events for the late game. An event is rolled when a wave
/// clears (telegraphed during the lull), then activated as the next wave starts:
/// Meteor Storm rains area-damage across the field, Supply Drop lands free
/// structures in your zones, and Gold Rush doubles bounties + mine yield. Events
/// are gated by depth and a minimum gap so they stay special.
/// </summary>
public static class MapEventSystem
{
    private const int MinWave = 8;   // events only appear once the run is rolling
    private const int MinGap = 3;    // waves between events
    private const float RollChance = 0.45f;

    private static readonly Color Fire = new(237, 116, 67, 255);
    private static readonly Color Gold = new(243, 189, 77, 255);

    /// <summary>After a wave clears (s.Wave now the upcoming wave), maybe telegraph an event.</summary>
    public static void RollForNextWave(GameState s)
    {
        s.PendingEvent = MapEventKind.None;
        int wave = s.Wave;
        if (wave < MinWave || wave % 10 == 0) return;        // never on boss waves
        if (s.WavesSinceEvent < MinGap) return;
        if (s.Rand() > RollChance) return;
        int i = (int)(s.Rand() * MapEvents.Rollable.Length) % MapEvents.Rollable.Length;
        s.PendingEvent = MapEvents.Rollable[i];
    }

    /// <summary>On wave start, promote the telegraphed event and fire its instant effects.</summary>
    public static void Activate(GameState s)
    {
        s.GoldRushActive = false;
        s.ActiveEvent = s.PendingEvent;
        s.PendingEvent = MapEventKind.None;
        if (s.ActiveEvent == MapEventKind.None) { s.WavesSinceEvent++; return; }

        s.WavesSinceEvent = 0;
        s.BannerText = MapEvents.Name(s.ActiveEvent);
        s.BannerTimer = 2.6f;
        switch (s.ActiveEvent)
        {
            case MapEventKind.MeteorShower: s.MeteorTimer = 1.2f; break;
            case MapEventKind.SupplyDrop: DropSupplies(s); s.ActiveEvent = MapEventKind.None; break;
            case MapEventKind.GoldRush: s.GoldRushActive = true; break;
        }
    }

    public static void Update(GameState s, float dt)
    {
        UpdateMeteors(s, dt);

        // Gold Rush and Meteor Storm both end when the wave is cleared.
        bool waveLive = s.Spawning is not null || s.Enemies.Exists(e => !e.Dead);
        if (!waveLive && s.Meteors.Count == 0)
        {
            if (s.ActiveEvent == MapEventKind.GoldRush) { s.GoldRushActive = false; s.ActiveEvent = MapEventKind.None; }
            if (s.ActiveEvent == MapEventKind.MeteorShower) s.ActiveEvent = MapEventKind.None;
        }
    }

    // ---- Meteor Storm -----------------------------------------------------

    private static void UpdateMeteors(GameState s, float dt)
    {
        for (int i = s.Meteors.Count - 1; i >= 0; i--)
        {
            var m = s.Meteors[i];
            m.Fall -= dt;
            if (m.Fall <= 0f) { Impact(s, m); s.Meteors.RemoveAt(i); }
        }

        if (s.ActiveEvent != MapEventKind.MeteorShower) return;
        bool waveLive = s.Spawning is not null || s.Enemies.Exists(e => !e.Dead);
        if (!waveLive) return; // stop spawning once the wave is beaten; in-flight ones resolve

        s.MeteorTimer -= dt;
        if (s.MeteorTimer > 0f) return;
        s.MeteorTimer = s.Rand(1.0f, 1.7f);
        SpawnMeteor(s);
    }

    private static void SpawnMeteor(GameState s)
    {
        Vector2 target;
        var alive = s.Enemies.Where(e => !e.Dead).ToList();
        if (alive.Count > 0 && s.Rand() < 0.7f)
        {
            // Mostly aim at the swarm so the storm reads as helping you thin it.
            var e = alive[(int)(s.Rand() * alive.Count) % alive.Count];
            target = e.Pos + new Vector2(s.Rand(-22f, 22f), s.Rand(-22f, 22f));
        }
        else
        {
            float r = s.RoamLimit * 0.82f;
            target = new Vector2(s.Rand(-r, r), s.Rand(-r, r));
        }
        s.Meteors.Add(new Meteor
        {
            Target = target,
            Fall = 1.15f,
            MaxFall = 1.15f,
            Radius = 46f,
            Damage = 26f + s.Wave * 2.2f, // scales with depth
        });
    }

    private static void Impact(GameState s, Meteor m)
    {
        s.KickShake(7f);
        s.AddParticles(m.Target, Fire, 22, 130f);
        s.AddParticles(m.Target, new Color(96, 84, 72, 255), 10, 80f);
        Emberhold.Render.Audio.Play(Emberhold.Render.SfxId.CannonShot, 0.5f, 0.8f);

        // Cosmic damage ignores per-hit shields (a soft counter to Shielded packs).
        foreach (var e in s.Enemies)
            if (!e.Dead && Vector2.Distance(e.Pos, m.Target) <= m.Radius)
                CombatSystem.DamageEnemy(s, e, m.Damage, mitigable: false);

        // Scorch the hero if caught in the blast — the cost of standing still.
        var hero = s.Hero;
        if (hero.Invulnerable <= 0f && hero.Health > 0f
            && Vector2.Distance(hero.Pos, m.Target) <= m.Radius)
        {
            hero.Health -= MathF.Max(8f, m.Damage * 0.5f) * hero.DamageTakenMult;
            hero.Invulnerable = 0.8f;
            s.AddFloater(hero.Pos + new Vector2(0, -30), "SCORCHED", Fire);
        }
    }

    // ---- Supply Drop ------------------------------------------------------

    private static void DropSupplies(GameState s)
    {
        // Land 1-2 free, fully-built tower/support structures in valid quadrant spots.
        var pool = CardDb.All.Where(c => !c.Legendary && c.Category != Category.Defend).ToList();
        var zones = Map.BuildZones(s.Chapter);
        int want = 1 + (s.Rand() < 0.5f ? 1 : 0);
        int placed = 0;
        for (int attempt = 0; attempt < 60 && placed < want; attempt++)
        {
            var card = pool[(int)(s.Rand() * pool.Count) % pool.Count];
            var zone = zones[(int)(s.Rand() * zones.Count) % zones.Count];
            var pos = new Vector2(zone.X + s.Rand(0f, zone.Width), zone.Y + s.Rand(0f, zone.Height));
            if (!DraftController.IsValid(s, card, pos)) continue;
            s.Structures.Add(StructureFactory.Create(s, card, pos));
            s.AddParticles(pos, Gold, 16, 80f);
            s.AddFloater(pos + new Vector2(0, -24), card.Name, Gold);
            placed++;
        }
    }
}
