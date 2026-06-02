using System.Numerics;
using Emberhold.Data;
using Raylib_cs;

namespace Emberhold.Game;

/// <summary>
/// The lane-based fort. The keep sits at the origin; four cardinal lanes run from
/// the map edges through wall gates to the keep. Enemies travel the lanes;
/// buildable zones are the quadrants between them and grow with each chapter.
///
/// Side convention: 0=North (-Y), 1=East (+X), 2=South (+Y), 3=West (-X).
/// </summary>
public static class Map
{
    public const float LaneWidth = 70f;
    public const float WallThickness = 16f;
    public const float ZoneMargin = 8f;
    public const float KeepRadius = 34f;
    public const float KeepClearance = 54f; // no building this close to the keep

    public static readonly Vector2 KeepPos = Vector2.Zero;

    /// <summary>Outer half-extent of the fort walls for a given chapter.</summary>
    public static float FortHalfSize(int chapter) => 150f + (chapter - 1) * 90f;

    /// <summary>How far the hero may roam beyond the walls on collection runs.</summary>
    public static float RoamLimit(int chapter) => FortHalfSize(chapter) + 280f;

    private static float LaneHalf => LaneWidth / 2f;

    // ---- Lanes ----------------------------------------------------------

    /// <summary>The two crossing lane corridors (vertical + horizontal), full span.</summary>
    public static IReadOnlyList<Rectangle> Lanes(int chapter)
    {
        float reach = RoamLimit(chapter) + 80f;
        return new[]
        {
            new Rectangle(-LaneHalf, -reach, LaneWidth, reach * 2f), // vertical (N/S)
            new Rectangle(-reach, -LaneHalf, reach * 2f, LaneWidth), // horizontal (E/W)
        };
    }

    public static bool OnLane(Vector2 p)
        => MathF.Abs(p.X) <= LaneHalf || MathF.Abs(p.Y) <= LaneHalf;

    // ---- Walls (with cardinal gates aligned to the lanes) ---------------

    public static IReadOnlyList<Rectangle> WallRects(int chapter)
    {
        float half = FortHalfSize(chapter);
        float t = WallThickness;
        float gate = LaneHalf; // gate gap half-width == lane half-width

        return new[]
        {
            // North & South walls (split by vertical lane gate)
            new Rectangle(-half, -half, half - gate, t),
            new Rectangle(gate,  -half, half - gate, t),
            new Rectangle(-half, half - t, half - gate, t),
            new Rectangle(gate,  half - t, half - gate, t),
            // West & East walls (split by horizontal lane gate)
            new Rectangle(-half, -half, t, half - gate),
            new Rectangle(-half, gate,  t, half - gate),
            new Rectangle(half - t, -half, t, half - gate),
            new Rectangle(half - t, gate,  t, half - gate),
        };
    }

    // ---- Buildable zones (the four inner quadrants) ---------------------

    public static IReadOnlyList<Rectangle> BuildZones(int chapter)
    {
        float half = FortHalfSize(chapter) - WallThickness - ZoneMargin;
        float inner = LaneHalf + ZoneMargin;
        float span = half - inner;
        if (span <= 0f) return Array.Empty<Rectangle>();

        return new[]
        {
            new Rectangle(inner,  -half,  span, span), // NE
            new Rectangle(-half,  -half,  span, span), // NW
            new Rectangle(inner,  inner,  span, span), // SE
            new Rectangle(-half,  inner,  span, span), // SW
        };
    }

    /// <summary>
    /// True if a structure of the given radius may be placed centred at p:
    /// inside a build zone, clear of the keep, and not on a lane.
    /// (Overlap with other structures is checked by the placement system.)
    /// </summary>
    public static bool IsBuildable(Vector2 p, float radius, int chapter)
    {
        if (Vector2.Distance(p, KeepPos) < KeepClearance + radius) return false;
        foreach (var lane in Lanes(chapter))
            if (Geometry.CircleTouchesRect(p, radius, lane)) return false;
        foreach (var zone in BuildZones(chapter))
        {
            // Require the whole footprint inside the zone.
            if (p.X - radius >= zone.X && p.X + radius <= zone.X + zone.Width &&
                p.Y - radius >= zone.Y && p.Y + radius <= zone.Y + zone.Height)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Category-aware placement test. Attack/Support must sit fully inside a build
    /// zone; Defend must sit within a lane arm inside the fort (walls/traps shape
    /// the enemy path). Overlap with other structures is checked by the caller.
    /// </summary>
    public static bool IsPlaceable(Category cat, Vector2 p, float radius, int chapter)
    {
        if (Vector2.Distance(p, KeepPos) < KeepClearance + radius) return false;
        float fort = FortHalfSize(chapter) - WallThickness;

        if (cat == Category.Defend)
        {
            bool onVertical = MathF.Abs(p.X) <= LaneHalf - radius && MathF.Abs(p.Y) <= fort - radius;
            bool onHorizontal = MathF.Abs(p.Y) <= LaneHalf - radius && MathF.Abs(p.X) <= fort - radius;
            return onVertical || onHorizontal;
        }

        foreach (var zone in BuildZones(chapter))
            if (p.X - radius >= zone.X && p.X + radius <= zone.X + zone.Width &&
                p.Y - radius >= zone.Y && p.Y + radius <= zone.Y + zone.Height)
                return true;
        return false;
    }

    // ---- Enemy routing --------------------------------------------------

    /// <summary>Spawn point just beyond the wall for a cardinal side, on its lane.</summary>
    public static Vector2 SpawnPoint(int side, int chapter)
    {
        float d = FortHalfSize(chapter) + 60f;
        return side switch
        {
            0 => new Vector2(0, -d),
            1 => new Vector2(d, 0),
            2 => new Vector2(0, d),
            _ => new Vector2(-d, 0),
        };
    }

    /// <summary>Gate point on the wall the lane passes through.</summary>
    public static Vector2 Gate(int side, int chapter)
    {
        float g = FortHalfSize(chapter) - WallThickness - 4f;
        return side switch
        {
            0 => new Vector2(0, -g),
            1 => new Vector2(g, 0),
            2 => new Vector2(0, g),
            _ => new Vector2(-g, 0),
        };
    }
}
