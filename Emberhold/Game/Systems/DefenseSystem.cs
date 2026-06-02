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
                foreach (var e in s.Enemies)
                {
                    if (e.Dead) continue;
                    if (Vector2.Distance(e.Pos, st.Pos) > st.Radius + e.Radius) continue;

                    if (st.TrapSlowFactor < 1f)
                    {
                        e.SlowFactor = e.SlowTimer <= 0f ? st.TrapSlowFactor : MathF.Min(e.SlowFactor, st.TrapSlowFactor);
                        e.SlowTimer = MathF.Max(e.SlowTimer, 0.35f * s.SlowDurationMult); // CryoForge extends
                    }
                    if (st.TrapDps > 0f)
                        CombatSystem.DamageEnemy(s, e, st.TrapDps * dt, mitigable: false);
                }
            }
            else if (st.Role == StructureRole.Wall && (st.Regen || s.WallsSharePool) && st.Health < st.MaxHealth)
            {
                // Bulwark regen; Iron Tide keystone regenerates all walls.
                st.Health = MathF.Min(st.MaxHealth, st.Health + WallRegenPerSec * dt);
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
