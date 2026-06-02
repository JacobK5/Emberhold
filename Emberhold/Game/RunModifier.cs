namespace Emberhold.Game;

/// <summary>
/// A run-scoped "trial" rolled at the start of each run. Effects are expressed as
/// multipliers read by the systems (never by mutating the static Balance, so each
/// new run starts clean). Every modifier carries a clear upside and downside.
/// </summary>
public sealed record RunModifier(
    string Id,
    string Name,
    string Desc,
    float GoldMult = 1f,
    float ShopPriceMult = 1f,
    float EnemySpeedMult = 1f,
    float EnemyHealthMult = 1f,
    float EnemyCountMult = 1f,
    float HeroDamageMult = 1f,
    float HeroMaxHpMult = 1f,
    int StartHeroLevel = 1)
{
    public static readonly RunModifier None = new("none", "Open Field", "No trial in effect.");

    public static readonly IReadOnlyList<RunModifier> Catalog = new[]
    {
        new RunModifier("gold_rush", "Gold Rush", "Gold drops +40%, but shop prices +25%",
            GoldMult: 1.4f, ShopPriceMult: 1.25f),
        new RunModifier("bloodthirst", "Bloodthirst", "Enemies +18% speed, but +30% gold",
            EnemySpeedMult: 1.18f, GoldMult: 1.3f),
        new RunModifier("iron_horde", "Iron Horde", "Enemies +25% HP, hero +20% damage",
            EnemyHealthMult: 1.25f, HeroDamageMult: 1.2f),
        new RunModifier("endless_swarm", "Endless Swarm", "+30% enemy count, but +35% gold",
            EnemyCountMult: 1.3f, GoldMult: 1.35f),
        new RunModifier("glass_cannon", "Glass Cannon", "Hero +45% damage, but -40% max HP",
            HeroDamageMult: 1.45f, HeroMaxHpMult: 0.6f),
        new RunModifier("veteran", "Veteran", "Start at hero level 4, but enemies +12% HP",
            EnemyHealthMult: 1.12f, StartHeroLevel: 4),
    };

    public static RunModifier Roll(Random rng) => Catalog[rng.Next(Catalog.Count)];
}
