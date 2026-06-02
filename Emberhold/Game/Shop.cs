using Emberhold.Data;
using System.Linq;

namespace Emberhold.Game;

public enum HeroUpgradeKind { Damage, FireRate, Range, Health, Volley }
public enum ShopItemKind { StructureCard, HeroUpgrade, Expansion }

public sealed class ShopItem
{
    public ShopItemKind Kind;
    public CardDef? Card;               // ShopItemKind.StructureCard
    public HeroUpgradeKind UpgradeKind; // ShopItemKind.HeroUpgrade
    public bool Purchased;
}

/// <summary>
/// Manages the between-wave supply shop.  Always contains: fort expansion and any
/// non-maxed hero upgrades.  Wave 20+ also adds 6 random structure cards.
/// Prices escalate by PriceBumpPerBuy after each purchase this wave, then reset
/// next wave (base rises slightly after wave 20 for long-game gold sinks).
/// </summary>
public sealed class ShopState
{
    public bool Open;
    public bool CanOpen;   // set true while BetweenWaves countdown is running

    public List<ShopItem> Items = new();
    private readonly Random _rng = new();

    // Price state
    public int PriceBump;          // resets each wave
    public int WaveBaseRise;       // accumulates 8g / wave after wave 20
    public float PriceMult = 1f;   // run-modifier shop price multiplier
    public const int CardBasePrice      = 65;
    public const int PriceBumpPerBuy    = 28;

    // Hero upgrade caps
    public static readonly int[] HeroUpgradeMaxTiers = { 3, 3, 3, 4, 2 };  // indexed by (int)HeroUpgradeKind
    public static readonly int[] HeroUpgradeBaseCosts = { 52, 58, 46, 38, 68 };

    // Per-hero-upgrade tier tracking (parallels Hero's DmgUpgrades etc.).
    // Read by OverlayUI to display current tier.
    public int[] HeroTiers = new int[5];

    // ---- Pricing ----------------------------------------------------------

    public int CardCost          => Scaled(CardBasePrice + WaveBaseRise + PriceBump);
    public int ExpansionCost(int chapter) => Scaled(90 + chapter * 55);
    public int HeroUpgradeCost(HeroUpgradeKind kind)
        => Scaled(HeroUpgradeBaseCosts[(int)kind] + HeroTiers[(int)kind] * 18 + PriceBump);

    private int Scaled(int baseCost) => (int)MathF.Round(baseCost * PriceMult);

    public void OnPurchase() => PriceBump += PriceBumpPerBuy;

    // ---- Content strings --------------------------------------------------

    public static string UpgradeName(HeroUpgradeKind kind) => kind switch
    {
        HeroUpgradeKind.Damage   => "Battle Fury",
        HeroUpgradeKind.FireRate => "Iron Resolve",
        HeroUpgradeKind.Range    => "Eagle Eye",
        HeroUpgradeKind.Health   => "Bulwark Training",
        HeroUpgradeKind.Volley   => "Overdrive Mastery",
        _ => "?",
    };

    public static string UpgradeDesc(HeroUpgradeKind kind) => kind switch
    {
        HeroUpgradeKind.Damage   => "+7 damage",
        HeroUpgradeKind.FireRate => "+18% fire rate",
        HeroUpgradeKind.Range    => "+30 range",
        HeroUpgradeKind.Health   => "+25 max HP",
        HeroUpgradeKind.Volley   => "-1.5s volley cd",
        _ => "",
    };

    // ---- Wave refresh -----------------------------------------------------

    public void Refresh(int wave)
    {
        PriceBump = 0;
        Open = false;
        CanOpen = false;
        if (wave > 20) WaveBaseRise = (wave - 20) * 8;

        Items.Clear();

        // Expansion is always available.
        Items.Add(new ShopItem { Kind = ShopItemKind.Expansion });

        // Hero upgrades that haven't been maxed.
        foreach (HeroUpgradeKind kind in Enum.GetValues<HeroUpgradeKind>())
            if (HeroTiers[(int)kind] < HeroUpgradeMaxTiers[(int)kind])
                Items.Add(new ShopItem { Kind = ShopItemKind.HeroUpgrade, UpgradeKind = kind });

        // Wave 20+: six random structure cards.
        if (wave >= 20)
        {
            var pool = CardDb.All.OrderBy(_ => _rng.Next()).Take(6).ToList();
            foreach (var card in pool)
                Items.Add(new ShopItem { Kind = ShopItemKind.StructureCard, Card = card });
        }
    }
}
