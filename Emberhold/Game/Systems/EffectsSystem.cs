using System.Numerics;
using Emberhold.Core;

namespace Emberhold.Game;

/// <summary>
/// Advances and prunes transient state: gold attraction toward the hero, particle
/// and floater motion/lifetime, screen shake decay, and dead-entity cleanup.
/// </summary>
public static class EffectsSystem
{
    public static void Update(GameState s, float dt)
    {
        var hero = s.Hero;
        float attractionRange = hero.Range * hero.Profile.Range * Balance.HeroRangeMult;

        foreach (var d in s.Drops)
        {
            d.Life -= dt;
            d.Bob += dt * 4f;
            if (d.Kind != DropKind.Gold) continue;

            float dist = Vector2.Distance(hero.Pos, d.Pos);
            if (dist < attractionRange && dist > 1f)
            {
                var dir = MathUtils.Normalize(hero.Pos - d.Pos);
                float speed = MathUtils.AttractionSpeed(dist, attractionRange);
                d.Pos += dir * speed * dt;
            }
        }

        foreach (var p in s.Particles)
        {
            p.Pos += p.Vel * dt;
            p.Vel *= 0.94f;
            p.Life -= dt;
        }

        foreach (var f in s.Floaters)
        {
            f.Pos.Y -= 23f * dt;
            f.Life -= dt;
        }

        s.Enemies.RemoveAll(e => e.Dead);
        s.Projectiles.RemoveAll(p => p.Life <= 0f);
        s.Drops.RemoveAll(d => d.Collected || d.Life <= 0f);
        s.Particles.RemoveAll(p => p.Life <= 0f);
        s.Floaters.RemoveAll(f => f.Life <= 0f);

        s.Shake = MathF.Max(0f, s.Shake - dt * 22f);
    }
}
