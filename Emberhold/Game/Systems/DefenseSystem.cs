using System.Numerics;
using Emberhold.Render;

namespace Emberhold.Game;

/// <summary>
/// Defend structures: ground-trap effects (slow / damage to enemies inside),
/// wall regeneration, and breach cleanup. Enemy attacks on walls and wall
/// retaliation are resolved in EnemySystem alongside movement.
/// </summary>
public static class DefenseSystem
{
    private const float WallRegenPerSec = 12f;

    public static void Update(GameState s, float dt)
    {
        foreach (var st in s.Structures)
        {
            if (st.Role == StructureRole.GroundTrap)
            {
                // Minefield rune enlarges and sharpens every trap.
                float radius = st.Radius * (s.Minefield ? 1.4f : 1f);
                float dpsMult = s.Minefield ? 1.5f : 1f;
                foreach (var e in s.Enemies)
                {
                    if (e.Dead) continue;
                    if (e.Phantom) continue; // assassins phase over ground traps
                    if (Vector2.Distance(e.Pos, st.Pos) > radius + e.Radius) continue;

                    if (st.TrapSlowFactor < 1f && !e.StatusImmune)
                    {
                        float factor = e.Boss ? MathF.Min(1f, st.TrapSlowFactor + 0.3f) : st.TrapSlowFactor; // bosses resist
                        e.SlowFactor = e.SlowTimer <= 0f ? factor : MathF.Min(e.SlowFactor, factor);
                        e.SlowTimer = MathF.Max(e.SlowTimer, 0.35f * s.SlowDurationMult); // CryoForge extends
                    }
                    if (st.TrapDps > 0f) // direct damage still hits wraiths
                        CombatSystem.DamageEnemy(s, e, st.TrapDps * dpsMult * s.ZoneBonus(st.Pos) * dt, mitigable: false);
                    if (st.SynTrapBurnDps > 0f && !e.StatusImmune) // Backdraft: the trap sets enemies ablaze
                    {
                        e.BurnTimer = MathF.Max(e.BurnTimer, 1.6f);
                        e.BurnDps = MathF.Max(e.BurnDps, st.SynTrapBurnDps);
                    }
                }
            }
            else if (st.Role == StructureRole.Wall && (st.Regen || s.WallsSharePool) && st.Health < st.MaxHealth)
            {
                // Bulwark regen; Iron Tide keystone regenerates all walls.
                st.Health = MathF.Min(st.MaxHealth, st.Health + WallRegenPerSec * dt);
            }
        }

        // Artificer hero repairs nearby damaged structures while she's active.
        if (s.Hero.Kind == Emberhold.Data.HeroKind.Artificer)
        {
            // Field Repair node doubles the rate; Broadcast node widens the radius.
            float rate = s.Hero.Has(Emberhold.Data.HeroSkills.ARepair) ? 36f : 18f;
            float reach = s.Hero.Has(Emberhold.Data.HeroSkills.AWideAura) ? 210f : 150f;
            foreach (var st in s.Structures)
            {
                if (st.Role == StructureRole.HeroBuff || st.MaxHealth <= 0f || st.Health >= st.MaxHealth) continue;
                if (Vector2.Distance(st.Pos, s.Hero.Pos) > reach) continue;
                st.Health = MathF.Min(st.MaxHealth, st.Health + rate * dt);
            }
        }

        // Remove any demolished structure (breached walls reopen the lane; siege
        // engines can wreck towers/mines/auras too). Tally the loss for the stat card.
        for (int i = s.Structures.Count - 1; i >= 0; i--)
        {
            var st = s.Structures[i];
            if (st.Role == StructureRole.HeroBuff || st.MaxHealth <= 0f || st.Health > 0f) continue;
            s.Live.StructuresLost += 1;
            s.AddParticles(st.Pos, Palette.Hex("c7794f"), 22, 95f);
            s.KickShake(7f);
            s.Structures.RemoveAt(i);
        }
    }
}
