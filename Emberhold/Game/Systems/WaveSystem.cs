using System.Numerics;
using Emberhold.Core;
using Emberhold.Data;
using Emberhold.Render;

namespace Emberhold.Game;

/// <summary>
/// Drives wave spawning and the between-wave cadence. Enemies enter on the four
/// cardinal lanes and march to the keep. Each wave's enemy composition is
/// precomputed (so the upcoming-wave preview is exact) and consumed at spawn time.
/// </summary>
public static class WaveSystem
{
    public static void StartWave(GameState s)
    {
        s.Live = new WaveSummary { Wave = s.Wave }; // reset per-wave tally
        var kinds = s.NextWaveKinds ?? BuildComposition(s, s.Wave);
        s.NextWaveKinds = null; // consumed; recomputed when this wave clears
        var stats = WaveStats.For(s.Wave);
        s.Spawning = new Spawning
        {
            Remaining = kinds.Count,
            Timer = 0f,
            Interval = stats.Interval,
            Kinds = new Queue<EnemyKind>(kinds),
        };
        if (kinds.Contains(EnemyKind.Boss)) { s.BossBannerTimer = 2.4f; s.BossIncoming = true; }
        else if (kinds.Contains(EnemyKind.Elite)) { s.BossBannerTimer = 1.8f; s.BossIncoming = false; }
    }

    /// <summary>Precompute a wave's full enemy composition (kinds only; lanes chosen at spawn).</summary>
    public static List<EnemyKind> BuildComposition(GameState s, int wave)
    {
        var stats = WaveStats.For(wave);
        int count = Math.Max(1, (int)MathF.Round(stats.Count * Balance.EnemyCountMult * s.Modifier.EnemyCountMult));
        var list = new List<EnemyKind>(count + 1);
        for (int i = 0; i < count; i++)
            list.Add(PickKind(wave, s.Rand()));
        // Every tenth wave is a chapter boss; other fifths are an elite raid.
        if (wave % 10 == 0) list.Add(EnemyKind.Boss);
        else if (stats.Elite) list.Add(EnemyKind.Elite);
        // Late-game: a Raider General marches in behind the swarm on deep non-boss
        // fifths (wave 25, 35, …). Spawns last so it trails the wave.
        if (wave >= 25 && wave % 5 == 0 && wave % 10 != 0) list.Add(EnemyKind.General);
        return list;
    }

    /// <summary>
    /// Counter-types unlock with depth and occupy the low end of the roll; basic
    /// raider/runner/brute fill the rest. Composition is the difficulty knob.
    /// </summary>
    private static EnemyKind PickKind(int wave, float roll)
        => wave >= 7 && roll < 0.07f ? EnemyKind.Siege
         : wave >= 9 && roll < 0.14f ? EnemyKind.Assassin
         : wave >= 11 && roll < 0.21f ? EnemyKind.Wraith
         : wave >= 10 && roll < 0.28f ? EnemyKind.Healer
         : wave >= 8 && roll < 0.37f ? EnemyKind.Shielded
         : wave >= 6 && roll < 0.47f ? EnemyKind.Flyer
         : wave >= 4 && roll < 0.57f ? EnemyKind.Brute
         : wave >= 2 && roll < 0.76f ? EnemyKind.Runner
         : EnemyKind.Raider;

    /// <summary>Human-readable summary of a wave's composition for the preview UI.</summary>
    public static string PreviewLine(IReadOnlyList<EnemyKind>? kinds)
    {
        if (kinds is null || kinds.Count == 0) return "";
        int boss = 0, siege = 0, elite = 0, healer = 0, shield = 0, flyer = 0, brute = 0, assassin = 0, wraith = 0, general = 0;
        foreach (var k in kinds)
            switch (k)
            {
                case EnemyKind.General: general++; break;
                case EnemyKind.Boss: boss++; break;
                case EnemyKind.Siege: siege++; break;
                case EnemyKind.Elite: elite++; break;
                case EnemyKind.Healer: healer++; break;
                case EnemyKind.Shielded: shield++; break;
                case EnemyKind.Flyer: flyer++; break;
                case EnemyKind.Brute: brute++; break;
                case EnemyKind.Assassin: assassin++; break;
                case EnemyKind.Wraith: wraith++; break;
            }
        var parts = new List<string> { $"{kinds.Count} incoming" };
        if (general > 0) parts.Add("GENERAL");
        if (boss > 0) parts.Add("BOSS");
        if (elite > 0) parts.Add($"Elite x{elite}");
        if (siege > 0) parts.Add($"Siege x{siege}");
        if (assassin > 0) parts.Add($"Assassin x{assassin}");
        if (wraith > 0) parts.Add($"Wraith x{wraith}");
        if (healer > 0) parts.Add($"Healer x{healer}");
        if (shield > 0) parts.Add($"Shielded x{shield}");
        if (flyer > 0) parts.Add($"Flyer x{flyer}");
        if (brute > 0) parts.Add($"Brute x{brute}");
        return string.Join("  -  ", parts);
    }

    public static void Update(GameState s, float dt)
    {
        bool allDead = s.Enemies.TrueForAll(e => e.Dead);

        if (s.Spawning is null && allDead)
        {
            if (s.WaveBonusPending)
            {
                int cleared = s.Wave;
                s.WaveBonusPending = false;

                // Gold interest: a capped treasury return on banked gold rewards a
                // reserve without letting it snowball.
                int interest = s.Gold > 30 ? Math.Min(30, s.Gold * 8 / 100) : 0;
                if (interest > 0)
                {
                    s.EarnGold(interest);
                    s.AddFloater(s.Hero.Pos + new Vector2(0, -42), $"TREASURY +{interest}", Palette.Gold);
                }
                s.Live.Interest = interest;

                s.LastSummary = s.Live; // snapshot for the wave-end stat card
                s.Wave += 1;
                // Carry the previously-previewed wave forward so foresight stays exact.
                s.NextWaveKinds = s.NextWaveKinds2 ?? BuildComposition(s, s.Wave);
                s.NextWaveKinds2 = BuildComposition(s, s.Wave + 1);
                s.UpgradeBreak = cleared % 5 == 0;
                s.BetweenWaves = s.UpgradeBreak ? 12f : 5f;

                int bonus = 2 + cleared / 2;
                for (int i = 0; i < bonus; i++)
                    s.SpawnDrop(new Vector2(s.Rand(-12, 13), 46f + s.Rand(-8, 8)), 1, fromMine: true);

                // Refresh the shop for the new between-wave window.
                s.Shop.Refresh(s.Wave, s.ZoneFortified);
                s.Shop.CanOpen = true;

                // Checkpoint the run once the post-wave lull settles (after any draft).
                s.NeedsAutosave = true;

                // Every third cleared wave also hands off to a draft + placement beat.
                if (cleared % 3 == 0) { s.PendingDraft = true; return; }
            }

            if (s.PendingDraft) return; // waiting for the draft to resolve

            // Pause the countdown while the player has the shop open.
            if (s.Shop.Open) return;

            s.BetweenWaves -= dt;
            if (s.BetweenWaves <= 0f)
            {
                s.Shop.CanOpen = false;
                s.UpgradeBreak = false;
                StartWave(s);
            }
            return;
        }

        if (s.Spawning is null) return;

        s.Spawning.Timer -= dt;
        if (s.Spawning.Timer <= 0f && s.Spawning.Remaining > 0 && s.Spawning.Kinds.Count > 0)
        {
            SpawnEnemy(s, s.Spawning.Kinds.Dequeue());
            s.Spawning.Remaining -= 1;
            s.Spawning.Timer = s.Spawning.Interval;
        }

        if (s.Spawning.Remaining <= 0)
        {
            s.Spawning = null;
            s.WaveBonusPending = true;
        }
    }

    private static void SpawnEnemy(GameState s, EnemyKind kind)
    {
        int side = (int)(s.Rand() * 4f) & 3;
        var stats = WaveStats.For(s.Wave);
        bool elite = kind == EnemyKind.Elite;

        var profile = EnemyProfile.Get(kind);
        // War Drums (horde tier) and the run modifier both scale spawn stats.
        var mod = s.Modifier;
        float hpBuff = 1f + s.HordeTier * 0.08f;
        float spdBuff = 1f + s.HordeTier * 0.03f;
        // Accumulated-wealth threat: HP scales fully, damage at half rate (so a rich
        // run stays dangerous without raiders one-shotting the hero).
        float threat = s.GoldThreat;
        float dmgThreat = 1f + (threat - 1f) * 0.5f;
        float hp = stats.Health * profile.Health * Balance.EnemyHealthMult * hpBuff * mod.EnemyHealthMult * threat;

        s.Enemies.Add(new Enemy
        {
            Id = s.NextId(),
            Pos = Map.SpawnPoint(side, s.Chapter),
            Radius = profile.Radius,
            Health = hp,
            MaxHealth = hp,
            Speed = stats.Speed * profile.Speed * Balance.EnemySpeedMult * spdBuff * mod.EnemySpeedMult,
            Damage = (int)MathF.Ceiling(stats.Damage * profile.Damage * Balance.EnemyDamageMult * dmgThreat),
            Reward = (int)MathF.Ceiling(stats.Reward * profile.Reward * Balance.GoldRewardMult * mod.GoldMult),
            Kind = kind,
            Elite = elite,
            Side = side,
            SlowFactor = 1f,
            Flying = kind == EnemyKind.Flyer,
            ShieldPerHit = kind == EnemyKind.Shielded ? 8f : 0f,
            Healer = kind == EnemyKind.Healer,
            Siege = kind == EnemyKind.Siege,
            Boss = kind == EnemyKind.Boss,
            SummonTimer = kind == EnemyKind.Boss ? 5f : 0f,
            Phantom = kind == EnemyKind.Assassin,
            BlinkTimer = kind == EnemyKind.Assassin ? 1.6f : 0f,
            StatusImmune = kind == EnemyKind.Wraith,
            General = kind == EnemyKind.General,
        });
        if (kind == EnemyKind.General) { s.BossBannerTimer = 2.2f; s.BossIncoming = false; }
    }
}
