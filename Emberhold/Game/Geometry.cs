using System.Numerics;
using Emberhold.Core;
using Raylib_cs;

namespace Emberhold.Game;

/// <summary>
/// Axis-aligned rectangle + circle helpers used for walls, zones, and collision.
/// Wraps Raylib's Rectangle (X, Y, Width, Height) with the circle-vs-rect sweeps
/// ported from the prototype so neither movement nor dashes can tunnel walls.
/// </summary>
public static class Geometry
{
    public static Rectangle Rect(float x, float y, float w, float h) => new(x, y, w, h);

    public static bool ContainsPoint(in Rectangle r, Vector2 p)
        => p.X >= r.X && p.X <= r.X + r.Width && p.Y >= r.Y && p.Y <= r.Y + r.Height;

    public static Vector2 Center(in Rectangle r)
        => new(r.X + r.Width / 2f, r.Y + r.Height / 2f);

    public static Vector2 NearestPointOnRect(in Rectangle r, Vector2 p)
        => new(MathUtils.Clamp(p.X, r.X, r.X + r.Width),
               MathUtils.Clamp(p.Y, r.Y, r.Y + r.Height));

    public static bool CircleTouchesRect(Vector2 center, float radius, in Rectangle r)
    {
        var n = NearestPointOnRect(r, center);
        return Vector2.Distance(center, n) <= radius + 0.5f;
    }

    public static bool CircleOverlapsRect(Vector2 center, float radius, in Rectangle r)
    {
        var n = NearestPointOnRect(r, center);
        return Vector2.Distance(center, n) < radius - 0.1f;
    }

    /// <summary>Pushes a circle out of every rect it overlaps. Mutates and returns position.</summary>
    public static Vector2 ResolveCircleRects(Vector2 pos, float radius, IReadOnlyList<Rectangle> rects)
    {
        foreach (var r in rects)
        {
            var n = NearestPointOnRect(r, pos);
            float dx = pos.X - n.X;
            float dy = pos.Y - n.Y;
            float dist = MathF.Sqrt(dx * dx + dy * dy);
            float overlap = radius - dist;
            if (overlap <= 0f) continue;

            if (dx != 0f || dy != 0f)
            {
                var dir = MathUtils.Normalize(dx, dy);
                pos += dir * overlap;
                continue;
            }

            // Centre exactly on the rect: eject toward the closest edge.
            (float amount, Vector2 to)[] edges =
            {
                (MathF.Abs(pos.X - r.X),               new Vector2(r.X - radius, pos.Y)),
                (MathF.Abs(r.X + r.Width - pos.X),     new Vector2(r.X + r.Width + radius, pos.Y)),
                (MathF.Abs(pos.Y - r.Y),               new Vector2(pos.X, r.Y - radius)),
                (MathF.Abs(r.Y + r.Height - pos.Y),    new Vector2(pos.X, r.Y + r.Height + radius)),
            };
            var nearest = edges[0];
            foreach (var e in edges) if (e.amount < nearest.amount) nearest = e;
            pos = nearest.to;
        }
        return pos;
    }

    /// <summary>Steps movement so fast entities can't tunnel through thin walls.</summary>
    public static Vector2 MoveWithCollisions(Vector2 pos, float radius, Vector2 delta, IReadOnlyList<Rectangle> rects)
    {
        int steps = Math.Max(1, (int)MathF.Ceiling(delta.Length() / 6f));
        var step = delta / steps;
        for (int i = 0; i < steps; i++)
        {
            pos += step;
            pos = ResolveCircleRects(pos, radius, rects);
        }
        return pos;
    }
}
