using System.Numerics;
using Emberhold.Data;

namespace Emberhold.Game;

/// <summary>
/// Runs the draft → placement → combat beat. Each draft offers one card per
/// category (pick one); chosen cards queue for hand placement, then play resumes.
/// </summary>
public sealed class DraftController
{
    public const float PadRadius = 18f;

    public List<CardDef> Offer { get; private set; } = new();
    public readonly Queue<CardDef> ToPlace = new();
    public CardDef? Placing { get; private set; }

    private readonly Random _rng = new();

    /// <summary>Begin a new run: seed free starters, then the opening draft.</summary>
    public void StartRun(GameState s, bool bonusStarter = false)
    {
        ToPlace.Clear();
        // Free starters so the opening isn't a death sentence: a tower + a lane blocker.
        ToPlace.Enqueue(CardDb.Get("archer_post"));
        ToPlace.Enqueue(CardDb.Get("barricade"));
        // Codex Adept meta-reward: an extra economy starter.
        if (bonusStarter) ToPlace.Enqueue(CardDb.Get("gold_mine"));
        StartDraft(s);
    }

    public const float LegendaryChance = 0.10f;

    public void StartDraft(GameState s)
    {
        Offer = new List<CardDef>
        {
            RandomOf(Category.Attack),
            RandomOf(Category.Defend),
            RandomOf(Category.Support),
        };
        // Rare legendary: swap one slot for a powerful unique of that category.
        if (_rng.NextDouble() < LegendaryChance)
        {
            int slot = _rng.Next(Offer.Count);
            var pool = CardDb.LegendariesIn(Offer[slot].Category);
            if (pool.Count > 0) Offer[slot] = pool[_rng.Next(pool.Count)];
        }
        s.Phase = Phase.Draft;
    }

    public void Pick(GameState s, int index)
    {
        if (s.Phase != Phase.Draft || index < 0 || index >= Offer.Count) return;
        ToPlace.Enqueue(Offer[index]);
        Offer = new List<CardDef>();

        // A banked veto grants a second pick: re-open the draft once, then place.
        if (s.DraftDoublePick)
        {
            s.DraftDoublePick = false;
            StartDraft(s);
            s.DraftReadyTimer = MathF.Max(s.DraftReadyTimer, 0.4f); // double-click guard
            return;
        }
        BeginPlacement(s);
    }

    /// <summary>Bank the current draft (take nothing) for a double-pick next time.</summary>
    public bool Veto(GameState s)
    {
        if (s.Phase != Phase.Draft || !s.DraftVetoAvailable || s.DraftDoublePick) return false;
        s.DraftVetoAvailable = false;
        s.DraftDoublePick = true;
        Offer = new List<CardDef>();
        BeginPlacement(s); // resolves to combat when nothing is queued
        return true;
    }

    private void BeginPlacement(GameState s)
    {
        if (ToPlace.Count == 0) { s.Phase = Phase.Combat; Placing = null; return; }
        Placing = ToPlace.Dequeue();
        s.Phase = Phase.Placement;
    }

    /// <summary>Place the current card at a world position if the spot is legal.</summary>
    public bool TryPlace(GameState s, Vector2 pos)
    {
        if (s.Phase != Phase.Placement || Placing is null) return false;
        if (!IsValid(s, Placing, pos)) return false;

        s.Pads.Add(new Pad { Def = Placing, Pos = pos, Radius = PadRadius });

        if (ToPlace.Count > 0) Placing = ToPlace.Dequeue();
        else { Placing = null; s.Phase = Phase.Combat; }
        return true;
    }

    public static bool IsValid(GameState s, CardDef def, Vector2 pos)
    {
        if (!Map.IsPlaceable(def.Category, pos, PadRadius, s.Chapter)) return false;
        foreach (var pad in s.Pads)
            if (Vector2.Distance(pos, pad.Pos) < PadRadius + pad.Radius + 4f) return false;
        foreach (var st in s.Structures)
            if (Vector2.Distance(pos, st.Pos) < PadRadius + st.Radius + 4f) return false;
        return true;
    }

    /// <summary>Starts placing any cards queued (e.g. from shop purchases).</summary>
    public void StartPlacements(GameState s)
    {
        if (ToPlace.Count == 0) return;
        Placing = ToPlace.Dequeue();
        s.Phase = Phase.Placement;
    }

    // ---- smoke-test auto-resolution ------------------------------------

    /// <summary>Picks the first offer and places everything at the first legal spot found.</summary>
    public void AutoAdvance(GameState s)
    {
        if (s.Phase == Phase.Draft) { Pick(s, 0); return; }
        if (s.Phase == Phase.Placement && Placing is not null)
        {
            var spot = FindSpot(s, Placing);
            if (spot is Vector2 p) TryPlace(s, p);
            else { Placing = null; s.Phase = Phase.Combat; } // give up gracefully
        }
    }

    private static Vector2? FindSpot(GameState s, CardDef def)
    {
        if (def.Category == Category.Defend)
        {
            foreach (float d in new[] { 90f, 120f, 150f })
                foreach (var c in new[] { new Vector2(0, -d), new Vector2(0, d), new Vector2(d, 0), new Vector2(-d, 0) })
                    if (IsValid(s, def, c)) return c;
            return null;
        }
        foreach (var zone in Map.BuildZones(s.Chapter))
        {
            for (float gx = zone.X + PadRadius; gx <= zone.X + zone.Width - PadRadius; gx += 40f)
                for (float gy = zone.Y + PadRadius; gy <= zone.Y + zone.Height - PadRadius; gy += 40f)
                {
                    var p = new Vector2(gx, gy);
                    if (IsValid(s, def, p)) return p;
                }
        }
        return null;
    }

    private CardDef RandomOf(Category cat)
    {
        var pool = CardDb.ByCategory(cat).ToList();
        return pool[_rng.Next(pool.Count)];
    }
}
