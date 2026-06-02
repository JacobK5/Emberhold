using Emberhold.Game;
using Xunit;

namespace Emberhold.Tests;

/// <summary>Run-modifier ("trial") system from the Trials batch.</summary>
public class ModifierTests
{
    [Fact]
    public void Roll_AlwaysReturnsCatalogModifier()
    {
        var rng = new Random(7);
        for (int i = 0; i < 30; i++)
            Assert.Contains(RunModifier.Roll(rng), RunModifier.Catalog);
    }

    [Fact]
    public void EveryModifier_HasNameAndDescription()
    {
        foreach (var m in RunModifier.Catalog)
        {
            Assert.False(string.IsNullOrWhiteSpace(m.Name));
            Assert.False(string.IsNullOrWhiteSpace(m.Desc));
        }
    }

    [Fact]
    public void ShopPriceMult_ScalesCosts()
    {
        var shop = new ShopState();
        int basePrice = shop.CardCost;
        shop.PriceMult = 1.25f;
        Assert.True(shop.CardCost > basePrice);
    }
}
