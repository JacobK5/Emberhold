using System.Numerics;
using Emberhold.Core;
using Emberhold.Render;

namespace Emberhold.Game;

/// <summary>
/// Construction + gold generation. The hero funds a placed pad by standing on it
/// (after a short dwell grace); the deposit rate scales with √cost so expensive
/// late builds take only modestly longer to fund. Mines drip gold over time.
/// </summary>
public static class EconomySystem
{
    private const float DwellGrace = 0.5f;
    private const float BuildReach = 30f;

    /// <summary>
    /// Deposit-rate multiplier from any Workshop whose aura covers the position
    /// (fort-wide under the Network amplifier). 1.0 when no workshop applies.
    /// </summary>
    public static float BuildRateMult(GameState s, Vector2 pos)
    {
        float best = 1f;
        foreach (var st in s.Structures)
        {
            if (st.Kind != Data.StructureKind.Workshop) continue;
            float reach = s.AurasGlobal ? float.PositiveInfinity : st.AuraRange;
            if (Vector2.Distance(st.Pos, pos) <= reach) best = MathF.Max(best, st.AuraMagnitude);
        }
        return best;
    }

    public static void UpdateBuilding(GameState s, float dt)
    {
        var hero = s.Hero;
        for (int i = s.Pads.Count - 1; i >= 0; i--)
        {
            var pad = s.Pads[i];
            bool onPad = Vector2.Distance(hero.Pos, pad.Pos) < BuildReach && s.Gold > 0;
            if (!onPad) { pad.Dwell = 0f; continue; }

            pad.Dwell += dt;
            if (pad.Dwell < DwellGrace) continue;

            float rate = MathUtils.DepositRate(pad.Def.Cost, Balance.DepositBaseRate, Balance.DepositSpeedMult)
                       * BuildRateMult(s, pad.Pos);
            pad.DepositCarry += rate * dt;
            int amount = MathUtils.DepositAmount(s.Gold, pad.Remaining, pad.DepositCarry);
            pad.DepositCarry -= amount;
            pad.Invested += amount;
            s.Gold -= amount;
            if (amount > 0) s.AddParticles(pad.Pos, Palette.Gold, 2, 24f);

            if (pad.Invested >= pad.Def.Cost)
            {
                s.Structures.Add(StructureFactory.Create(s, pad.Def, pad.Pos));
                s.AddParticles(pad.Pos, Palette.Hex("f2c766"), 22, 92f);
                s.KickShake(6f);
                s.Pads.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Late-game gold sink: standing on a built structure (with gold) pours gold into
    /// leveling it up. Same √cost dwell model as building, so gold always has a use.
    /// </summary>
    public static void UpdateUpgrades(GameState s, float dt)
    {
        var hero = s.Hero;
        foreach (var st in s.Structures)
        {
            if (!st.Upgradable) { st.Dwell = 0f; continue; }

            // Walls are solid so the hero can only press against them; use a wider
            // reach so collision resolution doesn't leave the hero just outside the threshold.
            float upgradeReach = st.Role == StructureRole.Wall ? st.Radius + 26f : st.Radius + 16f;
            bool onIt = Vector2.Distance(hero.Pos, st.Pos) < upgradeReach && s.Gold > 0;
            if (!onIt) { st.Dwell = 0f; continue; }

            st.Dwell += dt;
            if (st.Dwell < DwellGrace) continue;

            int cost = st.UpgradeCost;
            float rate = MathUtils.DepositRate(cost, Balance.DepositBaseRate, Balance.DepositSpeedMult)
                       * BuildRateMult(s, st.Pos);
            st.UpgradeCarry += rate * dt;
            int amount = MathUtils.DepositAmount(s.Gold, cost - st.UpgradeInvested, st.UpgradeCarry);
            st.UpgradeCarry -= amount;
            st.UpgradeInvested += amount;
            s.Gold -= amount;
            if (amount > 0) s.AddParticles(st.Pos, Palette.Hex("9fd0ff"), 2, 24f);

            if (st.UpgradeInvested >= cost)
            {
                ApplyUpgrade(st);
                st.Level += 1;
                st.UpgradeInvested = 0;
                st.UpgradeCarry = 0f;
                st.Dwell = 0f;
                s.AddParticles(st.Pos, Palette.Hex("bfe0ff"), 20, 80f);
                s.AddFloater(st.Pos + new Vector2(0, -24), $"LV {st.Level}", Palette.Hex("bfe0ff"));
                s.KickShake(5f);
            }
        }
    }

    private static void ApplyUpgrade(Structure st)
    {
        switch (st.Role)
        {
            case StructureRole.Tower:
                // Gentler vertical scaling than the original x1.6/x0.85 (~x1.88 DPS
                // per level) — maxed attack towers alone were carrying whole runs.
                st.Damage *= 1.45f;
                st.Rate *= 0.88f;
                st.Range += 15f;
                if (st.Splash > 0f) st.Splash *= 1.2f;
                if (st.BurnDps > 0f) st.BurnDps *= 1.5f;
                if (st.SlowFactor < 1f) st.SlowFactor = MathF.Max(0.3f, st.SlowFactor - 0.08f);
                if (st.ChainCount > 0) st.ChainCount += 1;
                break;
            case StructureRole.Wall:
                st.MaxHealth *= 1.6f;
                st.Health = st.MaxHealth;
                break;
            case StructureRole.GroundTrap:
                if (st.TrapDps > 0f) st.TrapDps *= 1.7f;
                if (st.TrapSlowFactor < 1f) st.TrapSlowFactor = MathF.Max(0.3f, st.TrapSlowFactor - 0.12f);
                st.Radius += 6f;
                break;
            case StructureRole.Mine:
                st.Interval *= 0.7f;
                break;
            case StructureRole.Aura:
                st.AuraRange += 30f;
                st.AuraMagnitude = st.AuraKind switch
                {
                    AuraKind.Damage => st.AuraMagnitude + 0.22f,  // stronger damage buff
                    AuraKind.Rate => MathF.Max(0.6f, st.AuraMagnitude - 0.07f), // faster (lower is better)
                    AuraKind.Range => st.AuraMagnitude + 25f,
                    AuraKind.Economy => st.AuraMagnitude + 0.2f,  // faster deposits (Workshop)
                    _ => st.AuraMagnitude,
                };
                break;
        }

        // Non-wall structures gain durability per level so investment resists siege.
        if (st.Role is not StructureRole.Wall and not StructureRole.HeroBuff)
        {
            st.MaxHealth *= 1.35f;
            st.Health = st.MaxHealth;
        }
    }

    public static void UpdateMines(GameState s, float dt)
    {
        foreach (var m in s.Structures)
        {
            if (m.Role != StructureRole.Mine) continue;
            m.Timer -= dt;
            if (m.Timer > 0f) continue;
            float lode = s.HasExotic(ExoticKind.MotherLode) ? 1.3f : 1f; // exotic: faster ticks
            m.Timer = m.Interval / (Balance.MineSpeedMult * lode);
            int drops = s.SupplyLines ? 3 : 2; // Supply Lines keystone
            int value = (s.BoomTown ? 3 : 2)   // Boom Town rune: richer gold
                      + (s.ZoneFortified[GameState.ZoneOf(m.Pos)] ? 1 : 0)  // Fortified Ground
                      + (s.GoldRushActive ? 2 : 0)  // Gold Rush map event
                      + (s.HasExotic(ExoticKind.MotherLode) ? 1 : 0); // exotic: richer mines
            for (int i = 0; i < drops; i++)
                s.SpawnDrop(m.Pos + new Vector2(s.Rand(-15, 15), 10f + i * 10f), value, fromMine: true);
            s.AddParticles(m.Pos + new Vector2(0, 10), Palette.Gold, 6, 30f);
        }
    }
}
