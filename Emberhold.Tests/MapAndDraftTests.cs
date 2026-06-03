using System.Numerics;
using Emberhold.Data;
using Emberhold.Game;
using Xunit;

namespace Emberhold.Tests;

public class MapTests
{
    [Fact]
    public void AttackPlacesInZone_NotOnLane()
    {
        var zone = Map.BuildZones(1)[0]; // NE quadrant
        var inZone = Geometry.Center(zone);
        Assert.True(Map.IsPlaceable(Category.Attack, inZone, 18f, 1));
        Assert.False(Map.IsPlaceable(Category.Attack, new Vector2(0, -100), 18f, 1)); // on the vertical lane
    }

    [Fact]
    public void DefendPlacesOnLane_NotInZone()
    {
        Assert.True(Map.IsPlaceable(Category.Defend, new Vector2(0, -100), 18f, 1)); // on lane inside fort
        var zone = Map.BuildZones(1)[0];
        Assert.False(Map.IsPlaceable(Category.Defend, Geometry.Center(zone), 18f, 1)); // off the lane
    }

    [Fact]
    public void KeepClearance_RejectsCenter()
        => Assert.False(Map.IsPlaceable(Category.Defend, Vector2.Zero, 18f, 1));
}

public class DraftTests
{
    private static GameState NewState() => new(seedDebug: false);

    [Fact]
    public void StartDraft_OffersOnePerCategory()
    {
        var s = NewState();
        var draft = new DraftController();
        draft.StartDraft(s);

        Assert.Equal(Phase.Draft, s.Phase);
        Assert.Equal(3, draft.Offer.Count);
        Assert.Contains(draft.Offer, c => c.Category == Category.Attack);
        Assert.Contains(draft.Offer, c => c.Category == Category.Defend);
        Assert.Contains(draft.Offer, c => c.Category == Category.Support);
    }

    [Fact]
    public void Pick_QueuesCardAndEntersPlacement()
    {
        var s = NewState();
        var draft = new DraftController();
        draft.StartDraft(s);
        draft.Pick(s, 0);

        Assert.Equal(Phase.Placement, s.Phase);
        Assert.NotNull(draft.Placing);
    }

    [Fact]
    public void CodexAdept_AddsBonusStarter()
    {
        var s = NewState();
        var plain = new DraftController();
        plain.StartRun(s, bonusStarter: false);
        var adept = new DraftController();
        adept.StartRun(s, bonusStarter: true);

        Assert.Equal(plain.ToPlace.Count + 1, adept.ToPlace.Count);
        Assert.Contains(adept.ToPlace, c => c.Id == "gold_mine");
    }

    [Fact]
    public void TryPlace_RejectsInvalidThenAcceptsValid()
    {
        var s = NewState();
        var draft = new DraftController();
        // Place a known Attack card so we control the target zone.
        typeof(DraftController).GetProperty(nameof(DraftController.Placing))!
            .SetValue(draft, CardDb.Get("archer_post"));
        s.Phase = Phase.Placement;

        Assert.False(draft.TryPlace(s, new Vector2(0, -100))); // on a lane -> invalid for Attack
        var spot = Geometry.Center(Map.BuildZones(1)[0]);
        Assert.True(draft.TryPlace(s, spot));
        Assert.Single(s.Pads);
    }

    [Fact]
    public void TryPlace_RejectsOverlap()
    {
        var s = NewState();
        var draft = new DraftController();
        var spot = Geometry.Center(Map.BuildZones(1)[0]);
        s.Pads.Add(new Pad { Def = CardDb.Get("archer_post"), Pos = spot });

        typeof(DraftController).GetProperty(nameof(DraftController.Placing))!
            .SetValue(draft, CardDb.Get("cannon"));
        s.Phase = Phase.Placement;

        Assert.False(draft.TryPlace(s, spot)); // overlaps existing pad
    }
}
