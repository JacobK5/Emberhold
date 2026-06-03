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

        if (s.ViewingBase)
        {
            DrawCentered("V  -  return to draft", 17, h - 40, Palette.PathEdge);
            return;
        }

        Raylib.DrawRectangle(0, 0, w, h, new Color(11, 17, 19, 180));
        DrawCentered(s.DraftDoublePick ? "DOUBLE PICK  -  CHOOSE A FRONTIER CARD" : "CHOOSE A FRONTIER CARD",
            34, h / 2 - 220, s.DraftDoublePick ? Palette.Gold : Palette.Hex("efd18a"));

        bool ready = s.DraftReadyTimer <= 0f;
        DrawCentered(
            ready ? "one Attack  /  one Defend  /  one Support  -  pick one (1 / 2 / 3 or click)"
                  : $"Ready in  {s.DraftReadyTimer:0.0}s ...",
            18, h / 2 - 184, ready ? Palette.PathEdge : Palette.Hex("9aa6a0"));
        string vetoHint = s.DraftVetoAvailable && !s.DraftDoublePick
            ? "(C) codex   /   (V) view base   /   (X) bank draft for a double-pick"
            : "(C) synergy codex   /   (V) view base";
        DrawCentered(vetoHint, 16, h / 2 - 160, Palette.PathEdge);

        // Preview the next two waves you'll face after placing, below the cards.
        string preview = WaveSystem.PreviewLine(s.NextWaveKinds);
        if (preview.Length > 0)
            DrawCentered($"Next wave:  {preview}", 17, h / 2 + 170, Palette.Hex("c49a62"));
        string preview2 = WaveSystem.PreviewLine(s.NextWaveKinds2);
        if (preview2.Length > 0)
            DrawCentered($"Then:  {preview2}", 15, h / 2 + 196, Palette.Hex("8a7350"));

        var owned = s.Structures.Select(st => st.Kind).ToHashSet();
        var rects = DraftCardRects();
        var mouse = Raylib.GetMousePosition();
        for (int i = 0; i < offer.Count && i < rects.Length; i++)
            DrawCard(rects[i], offer[i], i + 1, ready && Raylib.CheckCollisionPointRec(mouse, rects[i]), owned, ready);
    }

    private static void DrawCard(Rectangle r, CardDef def, int hotkey, bool hovered, ISet<StructureKind> owned, bool enabled = true)
    {
        Color accent = def.Legendary ? Palette.Gold : CategoryColor(def.Category);
        Raylib.DrawRectangleRec(r, def.Legendary ? new Color(40, 36, 22, 244) : new Color(28, 36, 34, 240));
        Raylib.DrawRectangleLinesEx(r, def.Legendary || hovered ? 3f : 2f, hovered ? Palette.Gold : accent);

        Raylib.DrawRectangleRec(new Rectangle(r.X, r.Y, r.Width, 34), accent);
        Raylib.DrawText(def.Legendary ? "LEGENDARY" : def.Category.ToString().ToUpper(),
            (int)r.X + 12, (int)r.Y + 9, 18, new Color(20, 26, 24, 255));
        Raylib.DrawText($"[{hotkey}]", (int)(r.X + r.Width - 36), (int)r.Y + 9, 18, new Color(20, 26, 24, 255));

        Raylib.DrawText(def.Name, (int)r.X + 14, (int)r.Y + 56, 24, def.Legendary ? Palette.Hex("ffd66b") : Palette.Hero);
        Raylib.DrawText($"Cost {def.Cost}g", (int)r.X + 14, (int)r.Y + 88, 18, Palette.Gold);

        int ty = (int)r.Y + 124;
        foreach (var line in Describe(def))
        {
            Raylib.DrawText(line, (int)r.X + 14, ty, 15, Palette.PathEdge);
            ty += 22;
        }

        if (!enabled)
        {
            Raylib.DrawRectangleRec(r, new Color(11, 17, 16, 160));
            return;
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
        int y = 96;
        string lastType = "";
        foreach (var def in SynergyEngine.Catalog)
        {
            if (def.Type != lastType)
            {
                lastType = def.Type;
                y += 6;
                Raylib.DrawText(def.Type.ToUpper() + " SYNERGIES", x, y, 19, Palette.Hero);
                y += 22;
            }

            bool active = s.ActiveSynergies.Contains(def.Id);
            Color nameCol = active ? Palette.Gold : Palette.Hex("9aa6a0");
            Raylib.DrawText(def.Name, x + 16, y, 17, nameCol);
            Raylib.DrawText(def.Requires, x + 230, y, 16, active ? Palette.Hero : Palette.PathEdge);
            Raylib.DrawText(def.Effect, x + 520, y, 16, active ? Palette.Hero : Palette.PathEdge);
            if (active) Raylib.DrawText("ACTIVE", x - 60, y, 16, Palette.Hex("8fbf7f"));
            y += 20;
        }
    }

    // ---- Skill tree UI ----------------------------------------------------

    private const int SkillNodeW = 168, SkillNodeH = 58, SkillColGap = 26, SkillRowGap = 24;

    /// <summary>Node rects for the active hero's tree, paired with their definitions.</summary>
    public static (SkillNode Node, Rectangle Rect)[] SkillNodeRects(GameState s)
    {
        var tree = HeroSkills.Tree(s.Hero.Kind);
        int w = Raylib.GetScreenWidth(), h = Raylib.GetScreenHeight();
        int gridW = 3 * SkillNodeW + 2 * SkillColGap;
        int x0 = w / 2 - gridW / 2;
        int y0 = h / 2 - 120;
        var arr = new (SkillNode, Rectangle)[tree.Count];
        for (int i = 0; i < tree.Count; i++)
        {
            var n = tree[i];
            arr[i] = (n, new Rectangle(
                x0 + n.Col * (SkillNodeW + SkillColGap),
                y0 + n.Row * (SkillNodeH + SkillRowGap),
                SkillNodeW, SkillNodeH));
        }
        return arr;
    }

    public static void DrawSkillTree(GameState s)
    {
        int w = Raylib.GetScreenWidth(), h = Raylib.GetScreenHeight();
        var hero = s.Hero;
        Raylib.DrawRectangle(0, 0, w, h, new Color(8, 14, 16, 220));

        var rects = SkillNodeRects(s);
        var lookup = rects.ToDictionary(e => e.Node.Id, e => e.Rect);

        // Header.
        DrawCentered($"{hero.Profile.Name}   -   Lv {hero.Level}   SKILL TREE", 30, h / 2 - 244, Palette.Hex("efd18a"));
        string ptStr = hero.Cur.SkillPoints > 0 ? $"{hero.Cur.SkillPoints} skill point{(hero.Cur.SkillPoints > 1 ? "s" : "")} to spend"
                                                 : "no skill points - level up to earn more";
        DrawCentered(ptStr, 20, h / 2 - 210, hero.Cur.SkillPoints > 0 ? Palette.Gold : Palette.Hex("9aa6a0"));
        DrawCentered("click a node to unlock   /   (H) switch hero   /   (K / ESC) close", 15, h / 2 - 184, Palette.PathEdge);

        // Column headers, anchored just above the grid.
        for (int col = 0; col < 3; col++)
        {
            var anchor = rects.FirstOrDefault(e => e.Node.Col == col);
            if (anchor.Rect.Width == 0) continue;
            Raylib.DrawText(HeroSkills.Column(hero.Kind, col), (int)anchor.Rect.X, (int)anchor.Rect.Y - 22, 14,
                col == 0 ? Palette.Hex("9fd0ff") : Palette.Hex("d6b46c"));
        }

        var mouse = Raylib.GetMousePosition();
        foreach (var (node, r) in rects)
        {
            // Prerequisite link line (vertical, from the node above in the column).
            if (node.Requires is not null && lookup.TryGetValue(node.Requires, out var pr))
            {
                var col = hero.Has(node.Requires) ? Palette.Gold : Palette.Hex("3c4642");
                Raylib.DrawLineEx(new Vector2(pr.X + pr.Width / 2f, pr.Y + pr.Height),
                                  new Vector2(r.X + r.Width / 2f, r.Y), 2f, col);
            }

            bool owned = hero.Has(node.Id);
            bool can = hero.CanUnlock(node);
            bool hovered = Raylib.CheckCollisionPointRec(mouse, r);

            Color bg = owned ? new Color(52, 44, 20, 240)
                     : can ? (hovered ? new Color(40, 54, 46, 245) : new Color(30, 40, 36, 235))
                     : new Color(20, 26, 24, 220);
            Color border = owned ? Palette.Gold
                         : can ? (hovered ? Palette.Hero : Palette.Hex("7fae6f"))
                         : Palette.Hex("3c4642");
            Color nameCol = owned ? Palette.Hex("efd18a") : can ? Palette.Hero : Palette.Hex("6a7269");

            Raylib.DrawRectangleRec(r, bg);
            Raylib.DrawRectangleLinesEx(r, owned || (can && hovered) ? 2.5f : 1.5f, border);
            Raylib.DrawText(node.Name, (int)r.X + 10, (int)r.Y + 8, 17, nameCol);
            DrawWrapped(node.Desc, (int)r.X + 10, (int)r.Y + 30, SkillNodeW - 18, 12, Palette.PathEdge);
            if (owned) Raylib.DrawText("OWNED", (int)(r.X + r.Width - 52), (int)r.Y + 8, 11, Palette.Hex("c9b074"));
        }
    }

    /// <summary>Draw text wrapped to a pixel width (greedy word-wrap), two lines max.</summary>
    private static void DrawWrapped(string text, int x, int y, int maxW, int fontSize, Color color)
    {
        var words = text.Split(' ');
        string line = "";
        int ly = y, lines = 0;
        foreach (var word in words)
        {
            string trial = line.Length == 0 ? word : line + " " + word;
            if (Raylib.MeasureText(trial, fontSize) > maxW && line.Length > 0)
            {
                Raylib.DrawText(line, x, ly, fontSize, color);
                ly += fontSize + 2; line = word;
                if (++lines >= 1) { /* allow one more line */ }
            }
            else line = trial;
        }
        if (line.Length > 0) Raylib.DrawText(line, x, ly, fontSize, color);
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
        DrawCentered("B / ESC to close", 14, py + 48, Palette.PathEdge);

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
                case ShopItemKind.ZoneUpgrade:
                    Raylib.DrawText($"Fortify {GameState.ZoneName(item.Zone)}", tx, ty, 18, Palette.Hex("efd18a"));
                    Raylib.DrawText("+15% output to that quadrant", tx, ty + 24, 13, Palette.PathEdge);
                    DrawItemCost(r, cost, canAfford);
                    break;
                case ShopItemKind.StructureCard when item.Card is not null:
                    Raylib.DrawText(item.Card.Name, tx, ty, 18, item.Card.Legendary ? Palette.Hex("ffd66b") : CategoryColor(item.Card.Category));
                    Raylib.DrawText(item.Card.Legendary ? "Legendary build (place + fund)" : $"{item.Card.Category} structure (place + fund)",
                        tx, ty + 24, 13, Palette.PathEdge);
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
        ShopItemKind.ZoneUpgrade  => shop.ZoneCost(chapter),
        _                         => shop.CardCost,
    };

    private static void DrawItemCost(Rectangle r, int cost, bool canAfford)
    {
        string costStr = $"{cost}g";
        int cw = Raylib.MeasureText(costStr, 15);
        Raylib.DrawText(costStr, (int)(r.X + r.Width - cw - 10), (int)(r.Y + r.Height - 22), 15,
            canAfford ? Palette.Gold : Palette.Hex("a05040"));
    }

    private static IEnumerable<string> Describe(CardDef def) => Describe(def.Kind);

    private static IEnumerable<string> Describe(StructureKind kind) => kind switch
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

    public static void DrawStructureTooltip(GameState s)
    {
        if (s.Over || s.ViewingBase) return;
        if (s.Phase == Phase.Draft) return;

        var mouse = Raylib.GetMousePosition();
        var world = Raylib.GetScreenToWorld2D(mouse, s.Cam);

        Structure? hit = null;
        foreach (var st in s.Structures)
            if (Vector2.Distance(world, st.Pos) <= st.Radius + 8f) { hit = st; break; }
        if (hit is null) return;

        string name = CardDb.All.FirstOrDefault(c => c.Kind == hit.Kind)?.Name ?? hit.Kind.ToString();
        var lines = new List<(string, Color)>();

        switch (hit.Role)
        {
            case StructureRole.Tower:
                lines.Add(($"Dmg {hit.Damage:0}   Range {hit.Range:0}   {1f / hit.Rate:0.#}/s", Palette.Hex("bfe0ff")));
                break;
            case StructureRole.Wall:
                lines.Add(($"HP  {hit.Health:0} / {hit.MaxHealth:0}", Palette.Hex("b9cc78")));
                break;
            case StructureRole.Mine:
                lines.Add(($"Every {hit.Interval:0.#}s  (~4g/tick)", Palette.Gold));
                break;
            case StructureRole.Aura:
                string auraLine = hit.AuraKind switch
                {
                    AuraKind.Damage  => $"+{(hit.AuraMagnitude - 1f) * 100f:0}% damage  r={hit.AuraRange:0}",
                    AuraKind.Rate    => $"-{(1f - hit.AuraMagnitude) * 100f:0}% cd  r={hit.AuraRange:0}",
                    AuraKind.Range   => $"+{hit.AuraMagnitude:0} range  r={hit.AuraRange:0}",
                    _                => $"Economy aura  r={hit.AuraRange:0}",
                };
                lines.Add((auraLine, Palette.Hex("d6b46c")));
                break;
            case StructureRole.GroundTrap:
                string trapLine = $"{hit.TrapDps:0} DPS";
                if (hit.TrapSlowFactor < 1f) trapLine += $"  slows {hit.TrapSlowFactor * 100f:0}%";
                lines.Add((trapLine, Palette.Hex("c49a62")));
                break;
            case StructureRole.HeroBuff:
                lines.Add(("Empowers hero volley", Palette.Hero));
                break;
        }

        string effect = string.Join(" ", Describe(hit.Kind));
        if (effect.Trim().Length > 0)
            lines.Add((effect.Trim(), Palette.PathEdge));

        if (hit.Level > 1)
            lines.Add(($"Level {hit.Level}", Palette.Hex("9fd0ff")));

        const int tipW = 210, padX = 10, padY = 8, lineH = 19;
        int tipH = padY * 2 + 22 + lines.Count * lineH;
        int tx = (int)mouse.X + 16;
        int ty = (int)mouse.Y - tipH / 2;

        int sw = Raylib.GetScreenWidth(), sh = Raylib.GetScreenHeight();
        if (tx + tipW > sw - 4) tx = (int)mouse.X - tipW - 16;
        ty = Math.Clamp(ty, 4, sh - tipH - 4);

        Raylib.DrawRectangle(tx, ty, tipW, tipH, new Color(16, 23, 22, 230));
        Raylib.DrawRectangleLinesEx(new Rectangle(tx, ty, tipW, tipH), 1.5f, Palette.Hex("c49a62"));
        Raylib.DrawText(name, tx + padX, ty + padY, 17, Palette.Hero);

        int ly = ty + padY + 22;
        foreach (var (text, col) in lines)
        {
            Raylib.DrawText(text, tx + padX, ly, 13, col);
            ly += lineH;
        }
    }

    private static void DrawCentered(string text, int fontSize, int y, Color color)
    {
        int w = Raylib.MeasureText(text, fontSize);
        Raylib.DrawText(text, Raylib.GetScreenWidth() / 2 - w / 2, y, fontSize, color);
    }

    // ---- Balancing panel --------------------------------------------------

    private const int BalPanelW = 920, BalPanelH = 560;
    private const int BalPad = 28, BalColGap = 16, BalRowH = 44, BalBtn = 30;

    private static int BalColWidth()
        => (BalPanelW - BalPad * 2 - BalColGap * (BalanceConfig.Groups.Length - 1)) / BalanceConfig.Groups.Length;

    /// <summary>Shared geometry for the balancing panel (draw + hit-test agree).</summary>
    private static (Rectangle Panel, Rectangle[] Row, Rectangle[] Minus, Rectangle[] Plus, Rectangle[] Action) BalanceLayout()
    {
        int w = Raylib.GetScreenWidth(), h = Raylib.GetScreenHeight();
        int px = w / 2 - BalPanelW / 2, py = h / 2 - BalPanelH / 2;
        var panel = new Rectangle(px, py, BalPanelW, BalPanelH);

        int colW = BalColWidth();
        int gridTop = py + 122;
        var entries = BalanceConfig.Entries;
        var rows = new Rectangle[entries.Length];
        var minus = new Rectangle[entries.Length];
        var plus = new Rectangle[entries.Length];
        var rowInCol = new int[BalanceConfig.Groups.Length];
        for (int i = 0; i < entries.Length; i++)
        {
            int col = Array.IndexOf(BalanceConfig.Groups, entries[i].Group);
            int r = rowInCol[col]++;
            int cx = px + BalPad + col * (colW + BalColGap);
            int cy = gridTop + r * BalRowH;
            rows[i] = new Rectangle(cx, cy, colW, BalRowH - 8);
            plus[i] = new Rectangle(cx + colW - BalBtn, cy + 2, BalBtn, BalBtn);
            minus[i] = new Rectangle(cx + colW - BalBtn * 2 - 6, cy + 2, BalBtn, BalBtn);
        }

        const int aw = 150, ag = 16, ah = 40;
        int totalW = 4 * aw + 3 * ag;
        int ax = w / 2 - totalW / 2, ay = py + BalPanelH - 54;
        var action = new Rectangle[4];
        for (int i = 0; i < 4; i++) action[i] = new Rectangle(ax + i * (aw + ag), ay, aw, ah);

        return (panel, rows, minus, plus, action);
    }

    /// <summary>Per-entry [-] / [+] button rects, indexed like BalanceConfig.Entries.</summary>
    public static (Rectangle Minus, Rectangle Plus)[] BalanceAdjustRects()
    {
        var l = BalanceLayout();
        var arr = new (Rectangle, Rectangle)[l.Minus.Length];
        for (int i = 0; i < arr.Length; i++) arr[i] = (l.Minus[i], l.Plus[i]);
        return arr;
    }

    /// <summary>Action button rects: [0]=Reset [1]=Copy [2]=Paste [3]=Close.</summary>
    public static Rectangle[] BalanceActionRects() => BalanceLayout().Action;

    public static void DrawBalancePanel(bool fromPause)
    {
        var l = BalanceLayout();
        int w = Raylib.GetScreenWidth(), h = Raylib.GetScreenHeight();
        Raylib.DrawRectangle(0, 0, w, h, new Color(8, 14, 16, 232));
        var p = l.Panel;
        Raylib.DrawRectangleRec(p, new Color(18, 26, 24, 246));
        Raylib.DrawRectangleLinesEx(p, 2f, Palette.Hex("c49a62"));
        DrawCentered("BALANCING", 32, (int)p.Y + 16, Palette.Hex("efd18a"));
        DrawCentered("tune live  ·  changes persist  ·  Copy / Paste shares a preset via clipboard",
            15, (int)p.Y + 56, Palette.PathEdge);

        var mouse = Raylib.GetMousePosition();
        int colW = BalColWidth();
        for (int c = 0; c < BalanceConfig.Groups.Length; c++)
        {
            int cx = (int)p.X + BalPad + c * (colW + BalColGap);
            Raylib.DrawText(BalanceConfig.Groups[c].ToUpper(), cx, (int)p.Y + 94, 16, Palette.Hex("d6b46c"));
            Raylib.DrawLine(cx, (int)p.Y + 116, cx + colW - 4, (int)p.Y + 116, Palette.Hex("3c4a44"));
        }

        var entries = BalanceConfig.Entries;
        for (int i = 0; i < entries.Length; i++)
        {
            var e = entries[i];
            var row = l.Row[i];
            Raylib.DrawText(e.Label, (int)row.X + 2, (int)row.Y + 8, 15, Palette.Hero);
            string val = BalanceConfig.Get(e.Field).ToString("0.##");
            Color vcol = BalanceConfig.IsDefault(e.Field) ? Palette.Hex("9aa6a0") : Palette.Gold;
            int vw = Raylib.MeasureText(val, 16);
            Raylib.DrawText(val, (int)l.Minus[i].X - vw - 10, (int)row.Y + 7, 16, vcol);
            DrawMiniButton(l.Minus[i], "-", mouse);
            DrawMiniButton(l.Plus[i], "+", mouse);
        }

        string[] labels = { "Reset", "Copy", "Paste", fromPause ? "Resume" : "Back" };
        for (int i = 0; i < l.Action.Length; i++)
            DrawTextButton(l.Action[i], labels[i], mouse);
    }

    private static void DrawMiniButton(Rectangle r, string label, Vector2 mouse)
    {
        bool hov = Raylib.CheckCollisionPointRec(mouse, r);
        Raylib.DrawRectangleRec(r, hov ? new Color(48, 64, 54, 255) : new Color(30, 40, 36, 255));
        Raylib.DrawRectangleLinesEx(r, 1.5f, hov ? Palette.Gold : Palette.Hex("6a5c45"));
        int tw = Raylib.MeasureText(label, 20);
        Raylib.DrawText(label, (int)(r.X + r.Width / 2 - tw / 2), (int)(r.Y + r.Height / 2 - 10), 20,
            hov ? Palette.Hex("efd18a") : Palette.Hero);
    }

    private static void DrawTextButton(Rectangle r, string label, Vector2 mouse)
    {
        bool hov = Raylib.CheckCollisionPointRec(mouse, r);
        Raylib.DrawRectangleRec(r, hov ? new Color(40, 54, 46, 245) : new Color(24, 32, 30, 235));
        Raylib.DrawRectangleLinesEx(r, hov ? 2.5f : 1.5f, hov ? Palette.Gold : Palette.Hex("6a5c45"));
        int tw = Raylib.MeasureText(label, 18);
        Raylib.DrawText(label, (int)(r.X + r.Width / 2 - tw / 2), (int)(r.Y + r.Height / 2 - 9), 18,
            hov ? Palette.Hex("efd18a") : Palette.Hero);
    }
}
