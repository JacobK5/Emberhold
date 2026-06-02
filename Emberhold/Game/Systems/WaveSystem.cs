using System.Numerics;
using Emberhold.Core;
using Emberhold.Data;

namespace Emberhold.Game;

/// <summary>
/// Drives wave spawning and the between-wave cadence. Enemies enter on the four
/// cardinal lanes and march to the keep. Composition + scaling ported from the
/// prototype's waveStats / spawnEnemy.
/// </summary>
public static class WaveSystem
{
    public static void StartWave(GameState s)
    {
        var stats = WaveStats.For(s.Wave);
        s.Live = new WaveSummary { Wave = s.Wave }; // reset per-wave tally
        s.Spawning = new Spawning
        {
            Remaining = Math.Max(1, (int)MathF.Round(stats.Count * Balance.EnemyCountMult)) + (stats.Elite ? 1 : 0),
            Timer = 0f,
            Interval = stats.Interval,
            ElitePending = stats.Elite,
        };
        if (stats.Elite) s.BossBannerTimer = 1.8f;
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
                s.LastSummary = s.Live; // snapshot for the wave-end stat card
                s.Wave += 1;
                s.UpgradeBreak = cleared % 5 == 0;
                s.BetweenWaves = s.UpgradeBreak ? 12f : 5f;

                int bonus = 2 + cleared / 2;
                for (int i = 0; i < bonus; i++)
                    s.SpawnDrop(new Vector2(s.Rand(-12, 13), 46f + s.Rand(-8, 8)), 1, fromMine: true);

                // Refresh the shop for the new between-wave window.
                s.Shop.Refresh(s.Wave);
                s.Shop.CanOpen = true;

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
        if (s.Spawning.Timer <= 0f && s.Spawning.Remaining > 0)
        {
            SpawnEnemy(s, s.Spawning.ElitePending);
            s.Spawning.ElitePending = false;
            s.Spawning.Remaining -= 1;
            s.Spawning.Timer = s.Spawning.Interval;
        }

        if (s.Spawning.Remaining <= 0)
        {
            s.Spawning = null;
            s.WaveBonusPending = true;
        }
    }

    private static void SpawnEnemy(GameState s, bool elite)
    {
        int side = (int)(s.Rand() * 4f) & 3;
        var stats = WaveStats.For(s.Wave);
        int wave = s.Wave;
        float roll = s.Rand();

        // Counter-types unlock with depth and occupy the low end of the roll;
        // basic raider/runner/brute fill the rest. Composition is the difficulty knob.
        EnemyKind kind = elite ? EnemyKind.Elite
            : wave >= 10 && roll < 0.10f ? EnemyKind.Healer
            : wave >= 8 && roll < 0.20f ? EnemyKind.Shielded
            : wave >= 6 && roll < 0.32f ? EnemyKind.Flyer
            : wave >= 4 && roll < 0.46f ? EnemyKind.Brute
            : wave >= 2 && roll < 0.70f ? EnemyKind.Runner
            : EnemyKind.Raider;

        var profile = EnemyProfile.Get(kind);
        float hp = stats.Health * profile.Health * Balance.EnemyHealthMult;

        s.Enemies.Add(new Enemy
        {
            Id = s.NextId(),
            Pos = Map.SpawnPoint(side, s.Chapter),
            Radius = profile.Radius,
            Health = hp,
            MaxHealth = hp,
            Speed = stats.Speed * profile.Speed * Balance.EnemySpeedMult,
            Damage = (int)MathF.Ceiling(stats.Damage * profile.Damage * Balance.EnemyDamageMult),
            Reward = (int)MathF.Ceiling(stats.Reward * profile.Reward * Balance.GoldRewardMult),
            Kind = kind,
            Elite = elite,
            Side = side,
            SlowFactor = 1f,
            Flying = kind == EnemyKind.Flyer,
            ShieldPerHit = kind == EnemyKind.Shielded ? 8f : 0f,
            Healer = kind == EnemyKind.Healer,
        });
    }
}
