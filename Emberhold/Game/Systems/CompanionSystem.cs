using System.Numerics;
using Emberhold.Core;
using Emberhold.Data;
using Emberhold.Render;

namespace Emberhold.Game;

/// <summary>
/// Drives the Beastmaster's wolves: keeps the right number of loyal wolves while she
/// is active, expires summoned ones, and makes each chase + bite the nearest enemy
/// (or trot back to the hero when none is near). Wolves don't take damage — they're
/// spirit-bonded — so enemy AI stays unchanged; they're pure offence.
/// </summary>
public static class CompanionSystem
{
    private const float LeashRange = 340f;  // wolves hunt enemies within this of the hero
    private const float BiteRange = 16f;     // contact bite distance (plus radii)
    private const float WolfSpeed = 205f;

    public static void Update(GameState s, float dt)
    {
        MaintainPack(s);

        var hero = s.Hero;
        bool alpha = hero.Has(HeroSkills.MAlpha);
        bool frenzy = hero.Has(HeroSkills.MFrenzy);
        bool maul = hero.Has(HeroSkills.MMaul);

        for (int i = s.Companions.Count - 1; i >= 0; i--)
        {
            var w = s.Companions[i];
            if (!w.Permanent)
            {
                w.Life -= dt;
                if (w.Life <= 0f) { s.Companions.RemoveAt(i); continue; }
            }
            w.AttackTimer = MathF.Max(0f, w.AttackTimer - dt);

            // Target: nearest live enemy within leash of the hero.
            var target = MathUtils.Nearest(w.Pos, s.Enemies, e => e.Pos,
                e => !e.Dead && Vector2.Distance(hero.Pos, e.Pos) <= LeashRange);

            Vector2 goal = target?.Pos ?? hero.Pos;
            float stopDist = target is not null ? w.Radius + 10f : 26f;
            float d = Vector2.Distance(w.Pos, goal);
            if (d > stopDist)
            {
                var dir = MathUtils.Normalize(goal - w.Pos);
                if (dir != Vector2.Zero) w.Facing = dir;
                w.Pos += dir * WolfSpeed * dt;
            }

            if (target is not null && w.AttackTimer <= 0f
                && Vector2.Distance(w.Pos, target.Pos) <= BiteRange + target.Radius)
            {
                w.AttackTimer = frenzy ? 0.55f : 0.85f;
                float bite = (alpha ? 16f : 11f) + hero.Damage * 0.25f;
                CombatSystem.DamageEnemy(s, target, bite);
                if (maul && !target.StatusImmune && !target.Dead)
                {
                    target.SlowFactor = target.SlowTimer <= 0f ? 0.6f : MathF.Min(target.SlowFactor, 0.6f);
                    target.SlowTimer = MathF.Max(target.SlowTimer, 1.2f);
                }
                s.AddParticles(target.Pos, Palette.Hex("c9b58a"), 4, 40f);
            }
        }
    }

    /// <summary>Ensure the loyal wolf count matches the active hero (0 when not Beastmaster).</summary>
    private static void MaintainPack(GameState s)
    {
        int desired = s.Hero.Kind == HeroKind.Beastmaster
            ? (s.Hero.Has(HeroSkills.MPack2) ? 2 : 1)
            : 0;

        if (desired == 0)
        {
            s.Companions.RemoveAll(w => w.Permanent);
            return;
        }

        int have = s.Companions.Count(w => w.Permanent);
        for (int i = have; i < desired; i++) s.Companions.Add(NewWolf(s, permanent: true));
        // If a node was refunded (e.g. Pack2 lost on switch), trim extras.
        while (s.Companions.Count(w => w.Permanent) > desired)
        {
            int idx = s.Companions.FindLastIndex(w => w.Permanent);
            if (idx < 0) break;
            s.Companions.RemoveAt(idx);
        }
    }

    public static Companion NewWolf(GameState s, bool permanent, float life = 0f)
        => new()
        {
            Id = s.NextId(),
            Pos = s.Hero.Pos + new Vector2(s.Rand(-22, 22), s.Rand(-22, 22)),
            Permanent = permanent,
            Life = life,
        };
}
