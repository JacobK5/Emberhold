using System.Numerics;

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

        // Remove breached walls so the lane reopens.
        s.Structures.RemoveAll(st => st.Role == StructureRole.Wall && st.Health <= 0f);
    }
}
