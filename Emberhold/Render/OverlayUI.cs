using System.Numerics;
using Emberhold.Data;
using Emberhold.Game;
using Raylib_cs;

namespace Emberhold.Render;

/// <summary>
/// Draft + placement overlays. Card-rect layout is shared between drawing and
/// hit-testing so clicks line up with what's on screen.
/// </summary>
public static class OverlayUI
{
    public static Rectangle[] DraftCardRects()
    {
        int w = Raylib.GetScreenWidth(), h = Raylib.GetScreenHeight();
        const int cardW = 230, cardH = 300, gap = 30;
        int totalW = cardW * 3 + gap * 2;
        int x0 = w / 2 - totalW / 2;
        int y = h / 2 - cardH / 2;
        var rects = new Rectangle[3];
        for (int i = 0; i < 3; i++)
            rects[i] = new Rectangle(x0 + i * (cardW + gap), y, cardW, cardH);
        return rects;
    }

    public static void DrawDraft(GameState s, IReadOnlyList<CardDef> offer)
    {
        int w = Raylib.GetScreenWidth(), h = Raylib.GetScreenHeight();
        Raylib.DrawRectangle(0, 0, w, h, new Color(11, 17, 19, 180));
        DrawCentered("CHOOSE A FRONTIER CARD", 34, h / 2 - 220, Palette.Hex("efd18a"));
        DrawCentered("one Attack  /  one Defend  /  one Support  -  pick one (1 / 2 / 3 or click)", 18, h / 2 - 184, Palette.PathEdge);
        DrawCentered("(C) synergy codex", 16, h / 2 - 160, Palette.PathEdge);

        // Preview the wave you'll face after placing, below the cards (clear of them).
        string preview = WaveSystem.PreviewLine(s.NextWaveKinds);
        if (preview.Length > 0)
            DrawCentered($"Next wave:  {preview}", 17, h / 2 + 170, Palette.Hex("c49a62"));

        var owned = s.Structures.Select(st => st.Kind).ToHashSet();
        var rects = DraftCardRects();
        var mouse = Raylib.GetMousePosition();
        for (int i = 0; i < offer.Count && i < rects.Length; i++)
            DrawCard(rects[i], offer[i], i + 1, Raylib.CheckCollisionPointRec(mouse, rects[i]), owned);
    }

    private static void DrawCard(Rectangle r, CardDef def, int hotkey, bool hovered, ISet<StructureKind> owned)
    {
        Color accent = CategoryColor(def.Category);
        Raylib.DrawRectangleRec(r, new Color(28, 36, 34, 240));
        Raylib.DrawRectangleLinesEx(r, hovered ? 3f : 2f, hovered ? Palette.Gold : accent);

        Raylib.DrawRectangleRec(new Rectangle(r.X, r.Y, r.Width, 34), accent);
        Raylib.DrawText(def.Category.ToString().ToUpper(), (int)r.X + 12, (int)r.Y + 9, 18, new Color(20, 26, 24, 255));
        Raylib.DrawText($"[{hotkey}]", (int)(r.X + r.Width - 36), (int)r.Y + 9, 18, new Color(20, 26, 24, 255));

        Raylib.DrawText(def.Name, (int)r.X + 14, (int)r.Y + 56, 24, Palette.Hero);
        Raylib.DrawText($"Cost {def.Cost}g", (int)r.X + 14, (int)r.Y + 88, 18, Palette.Gold);

        int ty = (int)r.Y + 124;
        foreach (var line in Describe(def))
        {
            Raylib.DrawText(line, (int)r.X + 14, ty, 15, Palette.PathEdge);
            ty += 22;
        }

        // Keystone hint: drafting this would complete a synergy you already own a piece of.
        var hints = SynergyEngine.KeystoneHintsFor(def, owned).ToList();
        int hy = (int)(r.Y + r.Height) - 26 - 18 * (hints.Count - 1);
        foreach (var name in hints)
        {
            Raylib.DrawText($"+ {name}", (int)r.X + 14, hy, 14, Palette.Gold);
            hy += 18;
        }
    }

    public static void DrawCodex(GameState s)
    {
        int w = Raylib.GetScreenWidth(), h = Raylib.GetScreenHeight();
        Raylib.DrawRectangle(0, 0, w, h, new Color(11, 17, 19, 215));
        DrawCentered("SYNERGY CODEX", 34, 40, Palette.Hex("efd18a"));
        DrawCentered("active synergies are highlighted   -   (C) to close", 16, 80, Palette.PathEdge);

        int x = w / 2 - 430;
        int y = 102;
        string lastType = "";
        foreach (var def in SynergyEngine.Catalog)
        {
            if (def.Type != lastType)
            {
                lastType = def.Type;
                y += 8;
                Raylib.DrawText(def.Type.ToUpper() + " SYNERGIES", x, y, 19, Palette.Hero);
                y += 23;
            }

            bool active = s.ActiveSynergies.Contains(def.Id);
            Color nameCol = active ? Palette.Gold : Palette.Hex("9aa6a0");
            Raylib.DrawText(def.Name, x + 16, y, 17, nameCol);
            Raylib.DrawText(def.Requires, x + 230, y, 16, active ? Palette.Hero : Palette.PathEdge);
            Raylib.DrawText(def.Effect, x + 520, y, 16, active ? Palette.Hero : Palette.PathEdge);
            if (active) Raylib.DrawText("ACTIVE", x - 60, y, 16, Palette.Hex("8fbf7f"));
            y += 21;
        }
    }

    public static void DrawPlacementWorld(GameState s, DraftController draft)
    {
        // Highlight legal zones / lanes for the current card.
        if (draft.Placing is null) return;
        bool defend = draft.Placing.Category == Category.Defend;

        if (defend)
        {
            foreach (var lane in Map.Lanes(s.Chapter))
                Raylib.DrawRectangleRec(lane, new Color(120, 170, 230, 26));
        }
        else
        {
            foreach (var zone in Map.BuildZones(s.Chapter))
                Raylib.DrawRectangleRec(zone, new Color(241, 194, 96, 26));
        }

        var world = Raylib.GetScreenToWorld2D(Raylib.GetMousePosition(), s.Cam);
        bool ok = DraftController.IsValid(s, draft.Placing, world);
        var col = ok ? new Color(150, 220, 130, 200) : new Color(220, 110, 100, 200);
        Raylib.DrawCircleV(world, DraftController.PadRadius, new Color(col.R, col.G, col.B, (byte)70));
        Raylib.DrawCircleLinesV(world, DraftController.PadRadius, col);
    }

    public static void DrawPlacementHud(GameState s, DraftController draft)
    {
        if (draft.Placing is null) return;
        int w = Raylib.GetScreenWidth();
        int remaining = draft.ToPlace.Count + 1;
        DrawCentered($"PLACE: {draft.Placing.Name}", 26, 24, CategoryColor(draft.Placing.Category));
        DrawCentered(
            draft.Placing.Category == Category.Defend
                ? "Click on a lane inside the fort  -  walls & traps shape the path"
                : "Click in a glowing zone  -  towers & support sit in the quadrants",
            18, 58, Palette.PathEdge);
        DrawCentered($"{remaining} to place", 18, 82, Palette.Hero);
    }

    // ---- Shop UI ----------------------------------------------------------

    private const int ShopItemW = 220, ShopItemH = 80, ShopItemGap = 8;
    private const int ShopPadX = 40, ShopPadY = 90;

    /// <summary>Returns bounding rects for each shop item in screen space.</summary>
    public static Rectangle[] ShopItemRects(GameState s)
    {
        int w = Raylib.GetScreenWidth(), h = Raylib.GetScreenHeight();
        var items = s.Shop.Items;
        int cols = 2;
        int panelW = cols * ShopItemW + (cols - 1) * ShopItemGap + ShopPadX * 2;
        int x0 = w / 2 - panelW / 2 + ShopPadX;
        int y0 = h / 2 - 200 + ShopPadY;
        var rects = new Rectangle[items.Count];
        for (int i = 0; i < items.Count; i++)
        {
            int col = i % cols;
            int row = i / cols;
            rects[i] = new Rectangle(x0 + col * (ShopItemW + ShopItemGap),
                                     y0 + row * (ShopItemH + ShopItemGap),
                                     ShopItemW, ShopItemH);
        }
        return rects;
    }

    public static void DrawShop(GameState s)
    {
        int w = Raylib.GetScreenWidth(), h = Raylib.GetScreenHeight();
        var shop = s.Shop;
        var hero = s.Hero;

        // Dim background.
        Raylib.DrawRectangle(0, 0, w, h, new Color(8, 14, 16, 210));

        // Panel background.
        int cols = 2;
        int rows = (shop.Items.Count + cols - 1) / cols;
        int panelW = cols * ShopItemW + (cols - 1) * ShopItemGap + ShopPadX * 2;
        int panelH = rows * (ShopItemH + ShopItemGap) + ShopPadY + 60;
        int px = w / 2 - panelW / 2;
        int py = h / 2 - 200;
        Raylib.DrawRectangle(px, py, panelW, panelH, new Color(18, 26, 24, 240));
        Raylib.DrawRectangleLinesEx(new Rectangle(px, py, panelW, panelH), 2f, Palette.Hex("c49a62"));

        // Header.
        DrawCentered("FRONTIER SUPPLY", 28, py + 14, Palette.Hex("efd18a"));
        string goldStr = $"GOLD: {s.Gold}";
        int gw = Raylib.MeasureText(goldStr, 18);
        Raylib.DrawText(goldStr, px + panelW - gw - 14, py + 18, 18, Palette.Gold);
        DrawCentered("S / ESC to close", 14, py + 48, Palette.PathEdge);

        // Items.
        var mouse = Raylib.GetMousePosition();
        var rects = ShopItemRects(s);
        for (int i = 0; i < shop.Items.Count; i++)
        {
            var item = shop.Items[i];
            var r = rects[i];
            bool hovered = Raylib.CheckCollisionPointRec(mouse, r);
            int cost = ItemCost(shop, item, s.Chapter);
            bool canAfford = s.Gold >= cost && !item.Purchased;

            Color border = item.Purchased ? Palette.Hex("484035")
                           : hovered && canAfford ? Palette.Gold
                           : Palette.Hex("6a5c45");
            Color bg = item.Purchased ? new Color(22, 28, 26, 200)
                       : hovered && canAfford ? new Color(38, 52, 44, 240)
                       : new Color(28, 38, 34, 230);

            Raylib.DrawRectangleRec(r, bg);
            Raylib.DrawRectangleLinesEx(r, 1.5f, border);

            if (item.Purchased)
            {
                DrawCentered("PURCHASED", 14, (int)(r.Y + r.Height / 2f - 7), Palette.Hex("5a6355"));
                continue;
            }

            int tx = (int)r.X + 10, ty = (int)r.Y + 8;
            switch (item.Kind)
            {
                case ShopItemKind.Expansion:
                    Raylib.DrawText("Expand Fort", tx, ty, 18, Palette.Hex("efd18a"));
                    Raylib.DrawText($"Grows the fort, +80 keep HP", tx, ty + 24, 13, Palette.PathEdge);
                    DrawItemCost(r, cost, canAfford);
                    break;
                case ShopItemKind.HeroUpgrade:
                    int tier = shop.HeroTiers[(int)item.UpgradeKind];
                    Raylib.DrawText(ShopState.UpgradeName(item.UpgradeKind), tx, ty, 18, Palette.Hex("bfe0ff"));
                    Raylib.DrawText(ShopState.UpgradeDesc(item.UpgradeKind), tx, ty + 24, 13, Palette.PathEdge);
                    Raylib.DrawText($"Tier {tier + 1}/{ShopState.HeroUpgradeMaxTiers[(int)item.UpgradeKind]}",
                        tx, ty + 42, 12, Palette.Hex("7aa0c8"));
                    DrawItemCost(r, cost, canAfford);
                    break;
                case ShopItemKind.StructureCard when item.Card is not null:
                    Raylib.DrawText(item.Card.Name, tx, ty, 18, CategoryColor(item.Card.Category));
                    Raylib.DrawText($"{item.Card.Category} structure (place + fund)", tx, ty + 24, 13, Palette.PathEdge);
                    DrawItemCost(r, cost, canAfford);
                    break;
            }
        }

        // Hero upgrade summary strip.
        int sy = py + panelH + 12;
        if (sy + 22 < h)
        {
            string heroLine = $"Hero: Dmg+{hero.DmgUpgrades*7}  FR×{MathF.Pow(0.82f, hero.FrUpgrades):0.00}"
                            + $"  Rng+{hero.RngUpgrades*30}  HP+{hero.HpUpgrades*25}"
                            + $"  Volley {hero.VolleyCooldown:0.0}s";
            DrawCentered(heroLine, 14, sy, Palette.Hex("8ab0cc"));
        }
    }

    private static int ItemCost(ShopState shop, ShopItem item, int chapter) => item.Kind switch
    {
        ShopItemKind.Expansion    => shop.ExpansionCost(chapter),
        ShopItemKind.HeroUpgrade  => shop.HeroUpgradeCost(item.UpgradeKind),
        _                         => shop.CardCost,
    };

    private static void DrawItemCost(Rectangle r, int cost, bool canAfford)
    {
        string costStr = $"{cost}g";
        int cw = Raylib.MeasureText(costStr, 15);
        Raylib.DrawText(costStr, (int)(r.X + r.Width - cw - 10), (int)(r.Y + r.Height - 22), 15,
            canAfford ? Palette.Gold : Palette.Hex("a05040"));
    }

    private static IEnumerable<string> Describe(CardDef def) => def.Kind switch
    {
        StructureKind.ArcherPost => new[] { "Fast single-target", "fire. Reliable DPS." },
        StructureKind.Cannon => new[] { "Slow, heavy splash", "damage in an area." },
        StructureKind.Ballista => new[] { "Long range, pierces", "through enemies." },
        StructureKind.ChainCoil => new[] { "Arcs between nearby", "enemies (3 jumps)." },
        StructureKind.FlameJet => new[] { "Short range, sets", "enemies burning." },
        StructureKind.FrostSpire => new[] { "Low damage but", "slows what it hits." },
        StructureKind.StormSpire => new[] { "Long-range bolts that", "chain between foes." },
        StructureKind.Barricade => new[] { "Destructible wall.", "Blocks a lane." },
        StructureKind.Bulwark => new[] { "Tanky wall that", "regenerates." },
        StructureKind.Redoubt => new[] { "Wall that retaliates", "against attackers." },
        StructureKind.SpikeTrap => new[] { "Ground trap: damages", "enemies passing over." },
        StructureKind.TarPit => new[] { "Ground trap: slows", "enemies inside it." },
        StructureKind.MoatLine => new[] { "Damages and slows", "enemies in the area." },
        StructureKind.Caltrops => new[] { "Cheap, wide trap;", "damages over its area." },
        StructureKind.GoldMine => new[] { "Produces gold", "over time." },
        StructureKind.WarBanner => new[] { "+Damage to towers", "in range." },
        StructureKind.Forge => new[] { "+Fire rate to towers", "in range." },
        StructureKind.Watchtower => new[] { "+Range to towers", "in range." },
        StructureKind.Workshop => new[] { "Cheaper builds &", "repairs nearby." },
        StructureKind.TradingPost => new[] { "A fast-producing", "gold mine." },
        StructureKind.EmberShrine => new[] { "Empowers the hero's", "volley ability." },
        _ => new[] { "" },
    };

    private static Color CategoryColor(Category cat) => cat switch
    {
        Category.Attack => Palette.Hex("c2624f"),
        Category.Defend => Palette.Hex("6f97c4"),
        _ => Palette.Hex("8fbf7f"),
    };

    private static void DrawCentered(string text, int fontSize, int y, Color color)
    {
        int w = Raylib.MeasureText(text, fontSize);
        Raylib.DrawText(text, Raylib.GetScreenWidth() / 2 - w / 2, y, fontSize, color);
    }
}
