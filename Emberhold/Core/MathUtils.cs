using System.Numerics;

namespace Emberhold.Core;

/// <summary>
/// Pure math + progression helpers. Ported from the prototype's core.js and
/// covered by unit tests. No engine or rendering dependencies live here.
/// </summary>
public static class MathUtils
{
    public const float Tau = MathF.PI * 2f;

    public static float Clamp(float value, float min, float max)
        => MathF.Max(min, MathF.Min(max, value));

    public static float Distance(Vector2 a, Vector2 b)
        => Vector2.Distance(a, b);

    /// <summary>Unit vector of (x, y), or zero for a zero-length input.</summary>
    public static Vector2 Normalize(float x, float y)
    {
        float length = MathF.Sqrt(x * x + y * y);
        return length > 0f ? new Vector2(x / length, y / length) : Vector2.Zero;
    }

    public static Vector2 Normalize(Vector2 v) => Normalize(v.X, v.Y);

    /// <summary>Nearest item to origin that satisfies the predicate, or null.</summary>
    public static T? Nearest<T>(Vector2 origin, IEnumerable<T> items, Func<T, Vector2> position, Func<T, bool>? predicate = null)
        where T : class
    {
        T? result = null;
        float best = float.PositiveInfinity;
        foreach (var item in items)
        {
            if (predicate is not null && !predicate(item)) continue;
            float d = Vector2.Distance(origin, position(item));
            if (d < best) { best = d; result = item; }
        }
        return result;
    }

    /// <summary>
    /// Per-second deposit RATE for funding a build of the given total cost.
    /// Scales with sqrt(cost) so total build time grows ~sqrt(cost): a 4x more
    /// expensive structure takes only ~2x as long to stand and fund.
    /// </summary>
    public static float DepositRate(float cost, float baseRate, float speedMult = 1f)
        => baseRate * MathF.Sqrt(MathF.Max(1f, cost)) * speedMult;

    /// <summary>
    /// Gold to deposit this frame: bounded by gold on hand, cost remaining, and
    /// the accumulated rate-over-time carry. Returns an integer amount.
    /// </summary>
    public static int DepositAmount(int gold, int remainingCost, float carry)
    {
        float capped = MathF.Min(gold, MathF.Min(remainingCost, MathF.Floor(carry)));
        return (int)Clamp(capped, 0f, remainingCost);
    }

    public static float AttractionSpeed(float distanceToHero, float maxDistance = 112f)
    {
        if (distanceToHero <= 0f || distanceToHero >= maxDistance) return 0f;
        return 42f + (maxDistance - distanceToHero) * 2.15f;
    }
}

/// <summary>Per-wave scaling stats. Ported from waveStats().</summary>
public readonly record struct WaveStats(
    int Count, float Health, float Speed, int Damage, float Interval, int Reward, bool Elite)
{
    public static WaveStats For(int wave)
    {
        int tier = (wave - 1) / 5;
        return new WaveStats(
            Count: 4 + (int)MathF.Floor(wave * 1.55f),
            Health: 22f + wave * 6f + tier * 12f,
            Speed: MathF.Min(46f + wave * 1.25f, 89f),
            Damage: 5 + wave / 3,
            Interval: MathF.Max(0.34f, 0.88f - wave * 0.018f),
            Reward: 2 + wave / 4,
            Elite: wave % 5 == 0);
    }
}
