using System.Numerics;
using Emberhold.Data;
using Emberhold.Game;
using Raylib_cs;

namespace Emberhold.Render;

/// <summary>An action chosen from the title-screen menu.</summary>
public enum MenuAction { NewRun, Resume, Settings, Quit }

/// <summary>
/// Front-of-house screens drawn before/around a run: the title menu and the
/// hero-select grid. The same hero grid backs both the start-of-run choice and
/// the in-game (H) hero swap, so the card layout + hit-testing live here once.
/// </summary>
public static class MenuUI
{
    // ---- Title screen -----------------------------------------------------

    /// <summary>Menu items (label + action) for the title, in draw order.</summary>
    public static List<(string Label, MenuAction Action)> TitleItems(bool hasSave)
    {
        var items = new List<(string, MenuAction)>();
        if (hasSave) items.Add(("Resume Run", MenuAction.Resume));
        items.Add((hasSave ? "New Run" : "Begin Run", MenuAction.NewRun));
        items.Add(("Balancing", MenuAction.Settings));
        items.Add(("Quit", MenuAction.Quit));
        return items;
    }

    private const int MenuItemW = 320, MenuItemH = 52, MenuItemGap = 14;

    /// <summary>Screen-space rects for the title menu items.</summary>
    public static Rectangle[] TitleItemRects(int count)
    {
        int w = Raylib.GetScreenWidth(), h = Raylib.GetScreenHeight();
        int x0 = w / 2 - MenuItemW / 2;
        int y0 = (int)(h * 0.46f);
        var rects = new Rectangle[count];
        for (int i = 0; i < count; i++)
            rects[i] = new Rectangle(x0, y0 + i * (MenuItemH + MenuItemGap), MenuItemW, MenuItemH);
        return rects;
    }

    public static void DrawTitle(bool hasSave, RunSave? save, string version)
    {
        int w = Raylib.GetScreenWidth(), h = Raylib.GetScreenHeight();
        Raylib.ClearBackground(Palette.Grass);

        // Subtle banded backdrop so the flat colour reads as a frontier sky/field.
        Raylib.DrawRectangle(0, 0, w, (int)(h * 0.4f), new Color(40, 56, 49, 90));
        Raylib.DrawRectangle(0, (int)(h * 0.4f), w, h, new Color(28, 40, 34, 60));

        float t = (float)Raylib.GetTime();
        // Title with a soft drop shadow + faint ember flicker on the keystone glyph.
        DrawCentered("EMBERHOLD", 88, (int)(h * 0.16f) + 4, new Color(10, 16, 14, 180));
        DrawCentered("EMBERHOLD", 88, (int)(h * 0.16f), Palette.Hex("efd18a"));
        byte glow = (byte)(150 + 90 * (0.5f + 0.5f * MathF.Sin(t * 2.2f)));
        DrawCentered("F R O N T I E R   S I E G E", 22, (int)(h * 0.16f) + 96,
            new Color(Palette.Fire.R, Palette.Fire.G, Palette.Fire.B, glow));

        var items = TitleItems(hasSave);
        var rects = TitleItemRects(items.Count);
        var mouse = Raylib.GetMousePosition();
        for (int i = 0; i < items.Count; i++)
        {
            var r = rects[i];
            bool hovered = Raylib.CheckCollisionPointRec(mouse, r);
            var border = hovered ? Palette.Gold : Palette.Hex("6a5c45");
            var bg = hovered ? new Color(40, 54, 46, 240) : new Color(22, 30, 28, 230);
            Raylib.DrawRectangleRec(r, bg);
            Raylib.DrawRectangleLinesEx(r, hovered ? 2.5f : 1.5f, border);
            DrawCenteredAt(items[i].Label, 24, (int)r.X, (int)r.Width, (int)(r.Y + r.Height / 2 - 14),
                hovered ? Palette.Hex("efd18a") : Palette.Hero);

            // Resume item shows a one-line summary of the saved run.
            if (items[i].Action == MenuAction.Resume && save is RunSave sv)
                DrawCenteredAt($"wave {sv.Wave} · fort {sv.Chapter} · {sv.Gold}g", 13,
                    (int)r.X, (int)r.Width, (int)(r.Y + r.Height) - 2, Palette.Hex("9aa6a0"));
        }

        DrawCentered("click an option to continue", 16, (int)(h * 0.46f) - 34, Palette.PathEdge);
        Raylib.DrawText($"v{version}", 14, h - 26, 15, Palette.Hex("6a7269"));
        string credit = "procedural · code-only";
        Raylib.DrawText(credit, w - Raylib.MeasureText(credit, 15) - 14, h - 26, 15, Palette.Hex("6a7269"));
    }

    // ---- Hero select grid -------------------------------------------------

    private const int HeroCardW = 250, HeroCardH = 224, HeroCardGap = 16, HeroCols = 4;

    /// <summary>Screen-space rects for the hero cards, one per kind in enum order.</summary>
    public static Rectangle[] HeroCardRects()
    {
        var profiles = HeroProfile.All;
        int w = Raylib.GetScreenWidth(), h = Raylib.GetScreenHeight();
        int rows = (profiles.Length + HeroCols - 1) / HeroCols;
        var rects = new Rectangle[profiles.Length];
        int gridH = rows * HeroCardH + (rows - 1) * HeroCardGap;
        int y0 = h / 2 - gridH / 2 + 24;
        for (int i = 0; i < profiles.Length; i++)
        {
            int row = i / HeroCols;
            int col = i % HeroCols;
            // Last (partial) row is centred.
            int inRow = Math.Min(HeroCols, profiles.Length - row * HeroCols);
            int rowW = inRow * HeroCardW + (inRow - 1) * HeroCardGap;
            int x0 = w / 2 - rowW / 2;
            rects[i] = new Rectangle(x0 + col * (HeroCardW + HeroCardGap),
                                     y0 + row * (HeroCardH + HeroCardGap),
                                     HeroCardW, HeroCardH);
        }
        return rects;
    }

    /// <param name="current">When set, the hero already in play (in-game swap mode); marked CURRENT.</param>
    /// <param name="cooldown">Switch cooldown remaining (in-game swap); shows a wait note.</param>
    public static void DrawHeroSelect(string header, string footer, HeroKind? current = null, float cooldown = 0f)
    {
        int w = Raylib.GetScreenWidth(), h = Raylib.GetScreenHeight();
        Raylib.DrawRectangle(0, 0, w, h, new Color(8, 14, 16, 224));

        DrawCentered(header, 36, (int)(h / 2 - HeroCardH - 96), Palette.Hex("efd18a"));

        var profiles = HeroProfile.All;
        var rects = HeroCardRects();
        var mouse = Raylib.GetMousePosition();
        for (int i = 0; i < profiles.Length; i++)
        {
            bool isCurrent = current is HeroKind c && c == profiles[i].Kind;
            bool hovered = Raylib.CheckCollisionPointRec(mouse, rects[i]);
            DrawHeroCard(rects[i], profiles[i], isCurrent, hovered);
        }

        if (cooldown > 0f)
            DrawCentered($"switch ready in {cooldown:0.0}s", 16, (int)(h / 2 + HeroCardH + 24), Palette.Hex("d2604f"));
        DrawCentered(footer, 17, (int)(h / 2 + HeroCardH + (cooldown > 0f ? 48 : 28)), Palette.PathEdge);
    }

    private static void DrawHeroCard(Rectangle r, HeroProfile p, bool current, bool hovered)
    {
        Color accent = current ? Palette.Hex("8fbf7f") : hovered ? Palette.Gold : Palette.Hex("6a5c45");
        Raylib.DrawRectangleRec(r, hovered ? new Color(34, 46, 42, 245) : new Color(24, 32, 30, 238));
        Raylib.DrawRectangleLinesEx(r, current || hovered ? 2.5f : 1.5f, accent);

        // Portrait disc with the hero's cloak colour + initial.
        var pc = new Vector2(r.X + 34, r.Y + 34);
        Raylib.DrawCircleV(pc + new Vector2(1.5f, 2.5f), 20f, new Color(12, 18, 16, 120));
        Raylib.DrawCircleV(pc, 20f, p.Cloak);
        Raylib.DrawCircleV(pc, 12f, new Color(Palette.Hero.R, Palette.Hero.G, Palette.Hero.B, (byte)235));
        int iw = Raylib.MeasureText(p.Initial, 18);
        Raylib.DrawText(p.Initial, (int)(pc.X - iw / 2f), (int)(pc.Y - 9), 18, Palette.Ink);

        Raylib.DrawText(p.FirstName, (int)r.X + 64, (int)r.Y + 16, 24, Palette.Hero);
        Raylib.DrawText(p.Role.ToUpper(), (int)r.X + 64, (int)r.Y + 42, 14, Palette.Hex("c49a62"));
        if (current)
            Raylib.DrawText("CURRENT", (int)(r.X + r.Width - 76), (int)r.Y + 12, 13, Palette.Hex("8fbf7f"));

        // Stat bars.
        int bx = (int)r.X + 16, by = (int)r.Y + 74;
        StatBar(bx, by, "HP",  p.BaseHealth, 80f, 240f, Palette.Hex("b9cc78"));
        StatBar(bx, by + 18, "DMG", p.Damage, 0.6f, 1.6f, Palette.Hex("d98f6b"));
        StatBar(bx, by + 36, "RATE", 1f / p.Rate, 0.7f, 1.15f, Palette.Hex("d27d48"));
        StatBar(bx, by + 54, "RNG", p.Range, 0.75f, 1.2f, Palette.Hex("7fa9d6"));
        StatBar(bx, by + 72, "SPD", p.Speed, 0.8f, 1.2f, Palette.Hex("8fbf7f"));

        // Signature + one-line role blurb.
        Raylib.DrawText($"> {HeroSkills.SignatureName(p.Kind)}", (int)r.X + 16, (int)r.Y + 162, 14, Palette.Gold);
        DrawWrapped(p.Blurb, (int)r.X + 16, (int)r.Y + 184, HeroCardW - 28, 13, Palette.PathEdge);
    }

    private static void StatBar(int x, int y, string label, float value, float min, float max, Color fill)
    {
        const int barW = 120, barH = 8, labelW = 42;
        Raylib.DrawText(label, x, y - 1, 12, Palette.Hex("8a9088"));
        int bx = x + labelW;
        float frac = Math.Clamp((value - min) / (max - min), 0f, 1f);
        Raylib.DrawRectangle(bx, y, barW, barH, new Color(16, 22, 21, 220));
        Raylib.DrawRectangle(bx, y, (int)(barW * frac), barH, fill);
        Raylib.DrawRectangleLines(bx, y, barW, barH, new Color(0, 0, 0, 60));
    }

    // ---- shared text helpers ---------------------------------------------

    private static void DrawWrapped(string text, int x, int y, int maxW, int fontSize, Color color)
    {
        var words = text.Split(' ');
        string line = "";
        int ly = y;
        foreach (var word in words)
        {
            string trial = line.Length == 0 ? word : line + " " + word;
            if (Raylib.MeasureText(trial, fontSize) > maxW && line.Length > 0)
            {
                Raylib.DrawText(line, x, ly, fontSize, color);
                ly += fontSize + 2; line = word;
            }
            else line = trial;
        }
        if (line.Length > 0) Raylib.DrawText(line, x, ly, fontSize, color);
    }

    private static void DrawCentered(string text, int fontSize, int y, Color color)
    {
        int w = Raylib.MeasureText(text, fontSize);
        Raylib.DrawText(text, Raylib.GetScreenWidth() / 2 - w / 2, y, fontSize, color);
    }

    private static void DrawCenteredAt(string text, int fontSize, int x, int width, int y, Color color)
    {
        int w = Raylib.MeasureText(text, fontSize);
        Raylib.DrawText(text, x + width / 2 - w / 2, y, fontSize, color);
    }
}
