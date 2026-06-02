using System.Numerics;
using Emberhold.Data;

namespace Emberhold.Game;

/// <summary>The player-controlled hero. Stats ported from the prototype's hero state.</summary>
public sealed class Hero
{
    public Vector2 Pos = new(0, 48);
    public Vector2 Facing = new(0, -1);
    public float Radius = 12f;

    public float Speed = 150f;
    public float Health = 100f;
    public float MaxHealth = 100f;

    public float Damage = 14f;
    public float FireRate = 0.56f;   // seconds between shots (lower = faster)
    public float ShotTimer;
    public float Range = 245f;

    public int Level = 1;
    public int Xp;
    public int NextXp = 8;

    public float Invulnerable;
    public float AbilityCooldown;
    public float VolleyCooldown = 8f;
    public float VolleyDamage = 1.2f;
    public float DashCooldown;
    public float Overdrive;

    public HeroKind Kind = HeroKind.Ranger;
    public HeroProfile Profile => HeroProfile.Get(Kind);
}
