using Emberhold.Game;
using Emberhold.Render;
using Xunit;

namespace Emberhold.Tests;

/// <summary>Cape regalia: trophy-gated cosmetic cloak colours.</summary>
public class CapeTests
{
    private static Profile WithTrophies(int n) => new()
    {
        Trophies = new HashSet<string>(Enumerable.Range(0, n).Select(i => $"t{i}")),
    };

    [Fact]
    public void Unlocks_FollowTrophyThresholds()
    {
        Assert.True(Capes.Unlocked(WithTrophies(0), 0));   // default always available
        Assert.False(Capes.Unlocked(WithTrophies(1), 1));  // Emberweave needs 2
        Assert.True(Capes.Unlocked(WithTrophies(2), 1));
        Assert.True(Capes.Unlocked(WithTrophies(10), Capes.All.Length - 1)); // last cape at 10
        Assert.False(Capes.Unlocked(WithTrophies(9), Capes.All.Length - 1));
    }

    [Fact]
    public void Override_ReturnsNullForDefault_LockedChoice_AndColourWhenEarned()
    {
        Assert.Null(Capes.Override(WithTrophies(5)));                              // choice 0 = default
        Assert.Null(Capes.Override(WithTrophies(0) with { CapeChoice = 3 }));      // locked -> default
        var earned = WithTrophies(4) with { CapeChoice = 2 };                      // Gilded at 4 trophies
        Assert.Equal(Capes.All[2].Color, Capes.Override(earned)!.Value);
    }
}
