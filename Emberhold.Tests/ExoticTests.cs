using System.Numerics;
using Emberhold.Data;
using Emberhold.Game;
using Xunit;

namespace Emberhold.Tests;

/// <summary>Endless exotics: deep-shop offering + each mega-upgrade's live effect.</summary>
public class ExoticTests
{
    private static Structure Tower(GameState s, StructureKind kind, Vector2 pos)
    {
        var st = StructureFactory.Create(s, CardDb.All.First(c => c.Kind == kind), pos);
        s.Structures.Add(st);
        return st;
    }

    private static Enemy Foe(GameState s, Vector2 pos, bool boss = false)
    {
        var e = new Enemy { Id = s.NextId(), Pos = pos, Radius = 11, Health = 9999, MaxHealth = 9999, SlowFactor = 1f, Boss = boss };
        s.Enemies.Add(e);
        return e;
    }

    [Fact]
    public void Shop_OffersOneExotic_DeepAndUnowned_NotEarlyOrWhenComplete()
    {
        var shop = new ShopState();
        var none = new HashSet<ExoticKind>();

        shop.Refresh(18, new bool[4], new HashSet<StructureKind>(), none);
        Assert.Equal(1, shop.Items.Count(i => i.Kind == ShopItemKind.Exotic));

        shop.Refresh(10, new bool[4], new HashSet<StructureKind>(), none); // too early
        Assert.DoesNotContain(shop.Items, i => i.Kind == ShopItemKind.Exotic);

        var all = Enum.GetValues<ExoticKind>().ToHashSet();
        shop.Refresh(25, new bool[4], new HashSet<StructureKind>(), all);  // nothing left to offer
        Assert.DoesNotContain(shop.Items, i => i.Kind == ShopItemKind.Exotic);
    }

    [Fact]
    public void OverdriveCore_ShortensTowerCooldown()
    {
        float Cooldown(bool exotic)
        {
            var s = new GameState(seedDebug: false);
            if (exotic) s.Exotics.Add(ExoticKind.OverdriveCore);
            var t = Tower(s, StructureKind.ArcherPost, Vector2.Zero);
            t.Cooldown = 0f;
            Foe(s, new Vector2(40, 0)); // within range
            TowerSystem.Update(s, 0.016f);
            return t.Cooldown; // set to the post-shot reload time
        }
        Assert.True(Cooldown(true) < Cooldown(false));
    }

    [Fact]
    public void SiegeBreaker_BuffsTowerDamage_VsHeavies()
    {
        float ShotDamage(bool exotic)
        {
            var s = new GameState(seedDebug: false);
            if (exotic) s.Exotics.Add(ExoticKind.SiegeBreaker);
            var t = Tower(s, StructureKind.ArcherPost, Vector2.Zero);
            t.Cooldown = 0f;
            Foe(s, new Vector2(40, 0), boss: true);
            TowerSystem.Update(s, 0.016f);
            return s.Projectiles[^1].Damage;
        }
        Assert.True(ShotDamage(true) > ShotDamage(false) * 1.3f);
    }

    [Fact]
    public void MotherLode_RichensMineYield()
    {
        int Yield(bool exotic)
        {
            var s = new GameState(seedDebug: false);
            if (exotic) s.Exotics.Add(ExoticKind.MotherLode);
            var m = Tower(s, StructureKind.GoldMine, new Vector2(-84, 84));
            m.Timer = 0f;
            int before = s.Drops.Count;
            EconomySystem.UpdateMines(s, 0.016f);
            return s.Drops.Skip(before).Sum(d => d.Value);
        }
        Assert.True(Yield(true) > Yield(false));
    }
}
