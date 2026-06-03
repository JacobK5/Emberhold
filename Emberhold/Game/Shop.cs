using Emberhold.Data;
using System.Linq;

namespace Emberhold.Game;

public enum HeroUpgradeKind { Damage, FireRate, Range, Health, Volley }
public enum ShopItemKind { StructureCard, HeroUpgrade, Expansion, ZoneUpgrade, Exotic }

/// <summary>Run-defining one-time mega-upgrades that surface deep in a run (wave 18+).</summary>
public enum ExoticKind { OverdriveCore, SiegeBreaker, AegisMatrix, MotherLode, PhoenixHeart }

public sealed class ShopItem
{
    public ShopItemKind Kind;
    public CardDef? Card;               // ShopItemKind.StructureCard
    public HeroUpgradeKind UpgradeKind; // ShopItemKind.HeroUpgrade
    public int Zone;                    // ShopItemKind.ZoneUpgrade (quadrant 0-3)
    public ExoticKind Exotic;           // ShopItemKind.Exotic
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
    public int ZoneCost(int chapter) => Scaled(110 + chapter * 30 + PriceBump);
    public int HeroUpgradeCost(HeroUpgradeKind kind)
        => Scaled(HeroUpgradeBaseCosts[(int)kind] + HeroTiers[(int)kind] * 18 + PriceBump);

    // Exotics are premium late-game gold sinks; base cost rises with depth.
    public static readonly int[] ExoticBaseCosts = { 240, 240, 210, 190, 300 }; // by (int)ExoticKind
    public int ExoticCost(ExoticKind kind) => Scaled(ExoticBaseCosts[(int)kind] + WaveBaseRise + PriceBump);

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

    public static string ExoticName(ExoticKind kind) => kind switch
    {
        ExoticKind.OverdriveCore => "Overdrive Core",
        ExoticKind.SiegeBreaker  => "Siege Breaker",
        ExoticKind.AegisMatrix   => "Aegis Matrix",
        ExoticKind.MotherLode    => "Mother Lode",
        ExoticKind.PhoenixHeart  => "Phoenix Heart",
        _ => "?",
    };

    public static string ExoticDesc(ExoticKind kind) => kind switch
    {
        ExoticKind.OverdriveCore => "All towers fire 25% faster",
        ExoticKind.SiegeBreaker  => "+35% tower dmg to heavies",
        ExoticKind.AegisMatrix   => "Keep regenerates 3 HP/s",
        ExoticKind.MotherLode    => "Mines: +1 gold, tick faster",
        ExoticKind.PhoenixHeart  => "Revive once at 50% HP",
        _ => "",
    };

    // ---- Wave refresh -----------------------------------------------------

    public void Refresh(int wave, bool[]? zoneFortified = null, IReadOnlyCollection<StructureKind>? owned = null,
        IReadOnlyCollection<ExoticKind>? ownedExotics = null)
    {
        PriceBump = 0;
        Open = false;
        CanOpen = false;
        if (wave > 20) WaveBaseRise = (wave - 20) * 8;

        Items.Clear();

        // Expansion is always available.
        Items.Add(new ShopItem { Kind = ShopItemKind.Expansion });

        // Deep run (wave 18+): one un-owned exotic mega-upgrade, kept near the top so
        // this premium offer stays visible even when the list grows long.
        if (wave >= 18 && ownedExotics is not null)
        {
            var remaining = Enum.GetValues<ExoticKind>().Where(e => !ownedExotics.Contains(e)).ToList();
            if (remaining.Count > 0)
                Items.Add(new ShopItem { Kind = ShopItemKind.Exotic, Exotic = remaining[_rng.Next(remaining.Count)] });
        }

        // Fortified Ground: offer to upgrade any quadrant not already fortified.
        if (zoneFortified is not null)
            for (int q = 0; q < 4; q++)
                if (!zoneFortified[q])
                    Items.Add(new ShopItem { Kind = ShopItemKind.ZoneUpgrade, Zone = q });

        // Card fusions: offered once you own both component structures.
        if (owned is not null)
            foreach (var f in CardDb.Fusions)
                if (owned.Contains(f.A) && owned.Contains(f.B))
                    Items.Add(new ShopItem { Kind = ShopItemKind.StructureCard, Card = f.Result });

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
