using System.Numerics;
using Emberhold.Data;
using Raylib_cs;

namespace Emberhold.Game;

public sealed class Enemy
{
    public int Id;
    public Vector2 Pos;
    public float Radius;
    public float Health;
    public float MaxHealth;
    public float Speed;
    public int Damage;
    public int Reward;
    public EnemyKind Kind;
    public bool Elite;
    public int Side;
    public float HitTimer;
    public float AttackTimer;
    public bool Dead;
    public bool Inside;     // has passed through the gate

    // Status effects (drive counter-design + synergies)
    public float SlowTimer;     // movement scaled while > 0
    public float SlowFactor = 1f;
    public float BurnTimer;     // damage-over-time while > 0
    public float BurnDps;

    // Counter-type traits
    public bool Flying;         // ignores walls/traps, flies straight to the keep
    public float ShieldPerHit;  // flat mitigation per direct hit (not DoT/traps)
    public bool Healer;         // periodically heals nearby enemies
    public float HealTimer;
    public bool Siege;          // targets and demolishes structures, not just walls
    public bool Boss;           // chapter boss: summons adds, resists slow, big reward
    public float SummonTimer;
    public bool Phantom;        // assassin: ignores walls/traps, blinks toward the keep
    public float BlinkTimer;
    public bool StatusImmune;   // wraith: immune to burn + slow
}

public enum ProjectileSource { Hero, Tower, Cannon, Ballista, Chain, Flame }

public sealed class Projectile
{
    public int Id;
    public Vector2 Pos;
    public Vector2 Vel;
    public float Damage;
    public float Life;
    public float Radius;
    public Color Color;
    public float Splash;
    public ProjectileSource Source;

    // On-hit status / behaviour (towers).
    public float SlowFactor = 1f;   // < 1 slows the struck enemy
    public float SlowDuration;
    public float BurnDps;
    public float BurnDuration;
    public int ChainsLeft;          // jumps to nearby enemies on hit
    public float ChainRange;
    public bool Pierce;             // passes through; tracks who it already hit
    public HashSet<int>? HitIds;
}

public enum DropKind { Gold, Ember, Relic }

public sealed class Drop
{
    public int Id;
    public Vector2 Pos;
    public int Value;
    public bool FromMine;
    public float Radius;
    public DropKind Kind;
    public float Life;
    public float Bob;
    public bool Collected;
}

public sealed class Particle
{
    public Vector2 Pos;
    public Vector2 Vel;
    public Color Color;
    public float Life;
    public float MaxLife;
    public float Size;
}

public sealed class Floater
{
    public Vector2 Pos;
    public string Text = "";
    public Color Color;
    public float Life;
    public float MaxLife;
}
