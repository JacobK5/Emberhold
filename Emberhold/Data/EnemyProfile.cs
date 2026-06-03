namespace Emberhold.Data;

public enum EnemyKind { Raider, Runner, Brute, Elite, Flyer, Shielded, Healer, Siege, Boss, Assassin, Wraith, General }

/// <summary>
/// Per-enemy-type multipliers applied on top of the wave's scaling stats.
/// Ported from config.js ENEMY_PROFILES. Counter-design (which build each type
/// punishes) is layered on in the combat/synergy systems.
/// </summary>
public sealed record EnemyProfile(
    EnemyKind Kind,
    float Health,
    float Speed,
    float Damage,
    int Reward,
    float Radius)
{
    public static readonly EnemyProfile Raider   = new(EnemyKind.Raider,   1f,    1f,    1f,    1, 11f);
    public static readonly EnemyProfile Runner   = new(EnemyKind.Runner,   0.72f, 1.38f, 0.82f, 1, 9f);
    public static readonly EnemyProfile Brute    = new(EnemyKind.Brute,    2.1f,  0.72f, 1.65f, 2, 15f);
    public static readonly EnemyProfile Elite    = new(EnemyKind.Elite,    3.5f,  0.82f, 2f,    5, 17f);
    // Counter-types: each punishes a particular build.
    public static readonly EnemyProfile Flyer    = new(EnemyKind.Flyer,    0.8f,  1.2f,  0.9f,  1, 10f); // ignores walls/traps
    public static readonly EnemyProfile Shielded = new(EnemyKind.Shielded, 1.3f,  0.85f, 1.1f,  2, 13f); // resists per-hit; weak to burn/traps
    public static readonly EnemyProfile Healer   = new(EnemyKind.Healer,   1.1f,  0.9f,  0.6f,  2, 12f); // heals nearby; rewards burst
    public static readonly EnemyProfile Siege    = new(EnemyKind.Siege,    5.2f,  0.55f, 1.9f,  6, 19f); // slow tank; demolishes structures
    public static readonly EnemyProfile Boss     = new(EnemyKind.Boss,     11f,   0.62f, 2.6f, 14, 25f); // chapter boss; summons adds, resists slow
    public static readonly EnemyProfile Assassin = new(EnemyKind.Assassin, 0.65f, 1.45f, 1.2f,  2, 9f);  // ignores walls/traps, blinks to keep
    public static readonly EnemyProfile Wraith   = new(EnemyKind.Wraith,   1.25f, 0.95f, 1.1f,  2, 12f); // immune to burn + slow
    public static readonly EnemyProfile General  = new(EnemyKind.General,  7f,    0.7f,  2.2f, 12, 18f); // late-game commander; kill it to rout the wave

    public static EnemyProfile Get(EnemyKind kind) => kind switch
    {
        EnemyKind.Runner   => Runner,
        EnemyKind.Brute    => Brute,
        EnemyKind.Elite    => Elite,
        EnemyKind.Flyer    => Flyer,
        EnemyKind.Shielded => Shielded,
        EnemyKind.Healer   => Healer,
        EnemyKind.Siege    => Siege,
        EnemyKind.Boss     => Boss,
        EnemyKind.Assassin => Assassin,
        EnemyKind.Wraith   => Wraith,
        EnemyKind.General  => General,
        _ => Raider,
    };
}
