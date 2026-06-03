using System.Numerics;
using Emberhold.Data;
using Emberhold.Game;
using Xunit;

namespace Emberhold.Tests;

/// <summary>Fortified Ground: per-quadrant fort upgrades bought in the shop.</summary>
public class ZoneTests
{
    [Theory]
    [InlineData(-50f, -50f, 0)] // NW
    [InlineData(50f, -50f, 1)]  // NE
    [InlineData(-50f, 50f, 2)]  // SW
    [InlineData(50f, 50f, 3)]   // SE
    public void ZoneOf_MapsByQuadrant(float x, float y, int expected)
        => Assert.Equal(expected, GameState.ZoneOf(new Vector2(x, y)));

    [Fact]
    public void ZoneBonus_AppliesOnlyToFortifiedQuadrant()
    {
        var s = new GameState(seedDebug: false);
        var pos = new Vector2(80, 80); // SE (q=3)
        Assert.Equal(1f, s.ZoneBonus(pos), 3);
        s.ZoneFortified[GameState.ZoneOf(pos)] = true;
        Assert.Equal(GameState.ZoneOutputBonus, s.ZoneBonus(pos), 3);
        Assert.Equal(1f, s.ZoneBonus(new Vector2(-80, -80)), 3); // other quadrant unaffected
    }

    [Fact]
    public void FortifiedMine_YieldsExtraGoldPerDrop()
    {
        var s = new GameState(seedDebug: false);
        var mine = StructureFactory.Create(s, CardDb.Get("gold_mine"), new Vector2(80, 80));
        mine.Timer = 0f;
        s.Structures.Add(mine);
        s.ZoneFortified[GameState.ZoneOf(mine.Pos)] = true;

        EconomySystem.UpdateMines(s, 0.1f);
        Assert.Contains(s.Drops, d => d.Kind == DropKind.Gold && d.Value >= 3);
    }

    [Fact]
    public void Shop_OffersUnfortifiedQuadrantsOnly()
    {
        var shop = new ShopState();
        var fortified = new bool[4];
        fortified[1] = true; // NE already fortified
        shop.Refresh(5, fortified);

        var zoneItems = shop.Items.FindAll(i => i.Kind == ShopItemKind.ZoneUpgrade);
        Assert.Equal(3, zoneItems.Count);                       // the other three offered
        Assert.DoesNotContain(zoneItems, i => i.Zone == 1);     // not the fortified one
    }
}
