namespace Emberhold.Game;

/// <summary>
/// Central balance multipliers. Defaults mirror the prototype's tuned values so
/// the rebuilt combat keeps a known feel; this is the single place to retune and
/// will later back an in-game settings panel.
/// </summary>
public static class Balance
{
    public static float EnemyHealthMult    = 0.88f;
    public static float EnemySpeedMult      = 1f;
    public static float EnemyDamageMult     = 1f;
    public static float EnemyCountMult      = 0.9f;
    public static float GoldRewardMult      = 1.3f;

    public static float HeroDamageMult      = 1f;
    public static float HeroSpeedMult       = 1f;
    public static float HeroRangeMult       = 1f;
    public static float HeroFireSpeedMult   = 1f;

    public static float TowerDamageMult     = 1f;
    public static float TowerFireSpeedMult  = 1f;
    public static float MineSpeedMult       = 1f;

    /// <summary>Base rate for the √cost deposit model (see MathUtils.DepositRate).</summary>
    public static float DepositBaseRate     = 3.3f;
    public static float DepositSpeedMult     = 1f;
}
