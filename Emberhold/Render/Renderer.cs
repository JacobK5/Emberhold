using System.Numerics;
using Emberhold.Core;
using Emberhold.Data;
using Emberhold.Game;
using Raylib_cs;

namespace Emberhold.Render;

/// <summary>
/// Procedural renderer. Draws the world in world-space inside a Camera2D pass
/// (with screen shake), then the HUD in screen space. No sprite assets.
/// </summary>
public static class Renderer
{
    private static readonly Random Jitter = new();

    public static void Draw(GameState s, DraftController draft, bool showIntro = false, bool showCodex = false)
    {
        Raylib.ClearBackground(Palette.Grass);

        var cam = s.Cam;
        if (s.Shake > 0f)
            cam.Offset += new Vector2(
                (float)(Jitter.NextDouble() - 0.5) * s.Shake,
                (float)(Jitter.NextDouble() - 0.5) * s.Shake);

        Raylib.BeginMode2D(cam);
        DrawLanes(s);
        DrawZones(s);
        DrawWalls(s);
        DrawGroundTraps(s);
        DrawKeep(s);
        DrawStructures(s);
        DrawPads(s);
        DrawDrops(s);
        DrawEnemies(s);
        DrawProjectiles(s);
        DrawHero(s.Hero);
        DrawParticles(s);
        DrawFloaters(s);
        if (s.Phase == Phase.Placement) OverlayUI.DrawPlacementWorld(s, draft);
        Raylib.EndMode2D();

        DrawEdgeIndicators(s);
        DrawHud(s);
        DrawAbilityBar(s);
        DrawWaveStatus(s);
        DrawStreak(s);
        DrawWaveSummary(s);
        DrawSynergyPopup(s);
        if (showIntro && s.Phase == Phase.Combat) DrawIntro(s);
        if (s.BossBannerTimer > 0f)
        {
            string txt = s.BossIncoming ? "!!  CHAPTER BOSS INCOMING  !!" : "ELITE RAID INCOMING";
            DrawCentered(txt, s.BossIncoming ? 44 : 40, Raylib.GetScreenHeight() / 2 - 130,
                s.BossIncoming ? Palette.Hex("e0584a") : Palette.Hex("e0994f"));
        }
        if (s.BannerTimer > 0f)
            DrawCentered(s.BannerText, 30, Raylib.GetScreenHeight() / 2 - 64, Palette.Hex("e07a4a"));
        if (s.Paused && !s.Over) DrawCentered("PAUSED", 44, Raylib.GetScreenHeight() / 2 - 20, Palette.Hex("efd18a"));

        if (s.Phase == Phase.Draft) OverlayUI.DrawDraft(s, draft.Offer);
        else if (s.Phase == Phase.Placement) OverlayUI.DrawPlacementHud(s, draft);

        if (s.Shop.Open) OverlayUI.DrawShop(s);
        if (showCodex) OverlayUI.DrawCodex(s);
    }

    private static void DrawAbilityBar(GameState s)
    {
        var hero = s.Hero;
        int w = Raylib.GetScreenWidth(), h = Raylib.GetScreenHeight();
        int y = h - 64;
        DrawAbilityPill(w / 2 - 150, y, "VOLLEY", "SPACE", hero.AbilityCooldown, hero.VolleyCooldown);
        DrawAbilityPill(w / 2 + 14, y, "DASH", "SHIFT", hero.DashCooldown, 2.4f);
        DrawCentered($"{hero.Profile.Name}   (H) switch hero", 16, y - 24, Palette.PathEdge);
        if (hero.Overdrive > 0f)
            DrawCentered($"OVERDRIVE {hero.Overdrive:0.0}s", 18, y - 46, Palette.Fire);
    }

    private static void DrawAbilityPill(int x, int y, string name, string key, float cooldown, float max)
    {
        const int pw = 136, ph = 40;
        bool ready = cooldown <= 0f;
        Raylib.DrawRectangle(x, y, pw, ph, new Color(19, 25, 24, 220));
        if (!ready && max > 0f)
            Raylib.DrawRectangle(x, y, (int)(pw * (1f - cooldown / max)), ph, new Color(60, 80, 70, 220));
        Raylib.DrawRectangleLinesEx(new Rectangle(x, y, pw, ph), 2f, ready ? Palette.Gold : Palette.PathEdge);
        Raylib.DrawText(name, x + 10, y + 6, 18, ready ? Palette.Hero : Palette.PathEdge);
        Raylib.DrawText(ready ? key : $"{cooldown:0.0}s", x + 10, y + 24, 12, Palette.PathEdge);
    }

    private static void DrawWaveStatus(GameState s)
    {
        if (s.Phase != Phase.Combat || s.Over) return;
        if (s.Spawning is null && s.BetweenWaves > 0f && !s.PendingDraft)
        {
            DrawCentered($"NEXT WAVE IN {MathF.Ceiling(s.BetweenWaves)}s", 22, 24, Palette.Hero);
            if (s.Shop.CanOpen)
                DrawCentered("[S] Supply Shop", 16, 54, Palette.Hex("c49a62"));
        }
    }

    /// <summary>Live kill-streak meter with a draining timer bar, shown mid-combat.</summary>
    private static void DrawStreak(GameState s)
    {
        if (s.Over || s.Streak < 3) return;
        int tier = s.StreakTier;
        Color col = tier switch { 3 => Palette.Hex("ff7a3a"), 2 => Palette.Hex("ff9a4d"), 1 => Palette.Hex("ffc15c"), _ => Palette.Hex("e8cf8a") };
        string label = tier > 0 ? $"{GameState.StreakLabel(tier)}  x{s.Streak}" : $"STREAK x{s.Streak}";
        int fs = tier >= 2 ? 24 : 20;
        int w = Raylib.MeasureText(label, fs);
        int sw = Raylib.GetScreenWidth();
        int x = sw / 2 - w / 2, y = 92;
        // Pulse the blazing tier.
        if (tier == 3)
        {
            float pulse = 0.5f + 0.5f * MathF.Sin((float)Raylib.GetTime() * 9f);
            Raylib.DrawText(label, x, y, fs, new Color(col.R, col.G, col.B, (byte)(160 + 95 * pulse)));
        }
        else Raylib.DrawText(label, x, y, fs, col);
        // Timer bar underneath.
        float frac = MathUtils.Clamp(s.StreakTimer / GameState.StreakWindow, 0f, 1f);
        Raylib.DrawRectangle(sw / 2 - 70, y + fs + 4, 140, 4, new Color(19, 25, 24, 180));
        Raylib.DrawRectangle(sw / 2 - 70, y + fs + 4, (int)(140 * frac), 4, col);
    }

    /// <summary>Between-wave recap of the wave just cleared.</summary>
    private static void DrawWaveSummary(GameState s)
    {
        if (s.Phase != Phase.Combat || s.Over || s.PendingDraft || s.Shop.Open) return;
        if (s.Spawning is not null || s.BetweenWaves <= 0f) return;
        if (s.LastSummary is not WaveSummary sum) return;

        const int pw = 300;
        var rows = new List<(string, string, Color)>
        {
            ("Raiders slain", sum.Kills.ToString(), Palette.Hero),
            ("Gold gathered", sum.GoldEarned.ToString(), Palette.Gold),
            ("Damage dealt", sum.DamageDealt.ToString(), Palette.Hex("d98f6b")),
            ("Best streak", $"x{sum.BestStreak}", Palette.Hex("ff9a4d")),
        };
        if (sum.Interest > 0)
            rows.Add(("Treasury interest", $"+{sum.Interest}", Palette.Gold));
        if (sum.StructuresLost > 0)
            rows.Add(("Structures lost", sum.StructuresLost.ToString(), Palette.Hex("d2604f")));

        string preview = WaveSystem.PreviewLine(s.NextWaveKinds);
        bool hasPreview = preview.Length > 0;

        int ph = 44 + rows.Count * 22 + 12 + (hasPreview ? 42 : 0);
        int sw = Raylib.GetScreenWidth();
        int px = sw / 2 - pw / 2;
        int py = 78;
        Raylib.DrawRectangle(px, py, pw, ph, new Color(16, 23, 22, 215));
        Raylib.DrawRectangleLinesEx(new Rectangle(px, py, pw, ph), 2f, Palette.Hex("c49a62"));
        DrawCenteredAt($"WAVE {sum.Wave} CLEARED", 20, px, pw, py + 10, Palette.Hex("efd18a"));

        int ry = py + 40;
        foreach (var (label, value, col) in rows)
        {
            Raylib.DrawText(label, px + 16, ry, 15, Palette.PathEdge);
            int vw = Raylib.MeasureText(value, 16);
            Raylib.DrawText(value, px + pw - vw - 16, ry - 1, 16, col);
            ry += 22;
        }

        if (hasPreview)
        {
            int fy = ry + 4;
            Raylib.DrawLine(px + 12, fy, px + pw - 12, fy, Palette.Hex("3c4a44"));
            DrawCenteredAt("NEXT WAVE", 13, px, pw, fy + 6, Palette.Hex("c49a62"));
            DrawCenteredAt(preview, 12, px, pw, fy + 22, Palette.PathEdge);
        }
    }

    private static void DrawCenteredAt(string text, int fontSize, int x, int width, int y, Color color)
    {
        int w = Raylib.MeasureText(text, fontSize);
        Raylib.DrawText(text, x + width / 2 - w / 2, y, fontSize, color);
    }

    /// <summary>Animated banner the first time a synergy triggers in a run.</summary>
    private static void DrawSynergyPopup(GameState s)
    {
        if (s.ActivePopup is null) return;
        var def = SynergyEngine.Catalog.FirstOrDefault(c => c.Id == s.ActivePopup);
        string name = def?.Name ?? s.ActivePopup;
        string effect = def?.Effect ?? "";

        // Fade in over 0.25s, hold, fade out over the last 0.5s; drift upward as it appears.
        float age = GameState.PopupDuration - s.PopupTimer;
        float alpha = MathUtils.Clamp(MathF.Min(age / 0.25f, s.PopupTimer / 0.5f), 0f, 1f);
        byte a = (byte)(alpha * 255);
        byte ad = (byte)(alpha * 220);

        const int pw = 440, ph = 92;
        int w = Raylib.GetScreenWidth();
        int px = w / 2 - pw / 2;
        int py = (int)(Raylib.GetScreenHeight() / 2 - 110 - (1f - alpha) * 12f); // slight rise-in

        Raylib.DrawRectangle(px, py, pw, ph, new Color((byte)18, (byte)24, (byte)22, (byte)(ad * 0.92f)));
        Raylib.DrawRectangleLinesEx(new Rectangle(px, py, pw, ph), 2.5f, new Color(Palette.Gold.R, Palette.Gold.G, Palette.Gold.B, a));

        DrawCenteredAt("SYNERGY DISCOVERED", 16, px, pw, py + 12, new Color((byte)0x9a, (byte)0xa6, (byte)0xa0, a));
        DrawCenteredAt(name, 30, px, pw, py + 34, new Color(Palette.Gold.R, Palette.Gold.G, Palette.Gold.B, a));
        DrawCenteredAt(effect, 15, px, pw, py + 68, new Color(Palette.Hero.R, Palette.Hero.G, Palette.Hero.B, a));
    }

    private static void DrawIntro(GameState s)
    {
        int h = Raylib.GetScreenHeight();
        if (s.Modifier.Id != "none")
        {
            DrawCentered($"TRIAL:  {s.Modifier.Name}", 24, h - 196, Palette.Hex("d6a6e0"));
            DrawCentered(s.Modifier.Desc, 17, h - 168, Palette.Hex("b489c4"));
        }
        DrawCentered("Collect gold, stand on pads to build. WASD / click to move.", 20, h - 150, Palette.Hero);
        DrawCentered("SPACE volley   /   SHIFT dash   /   H switch hero   /   S shop   /   C codex   /   P pause", 18, h - 124, Palette.PathEdge);
    }

    private static void DrawLanes(GameState s)
    {
        foreach (var lane in Map.Lanes(s.Chapter))
        {
            Raylib.DrawRectangleRec(lane, Palette.PathEdge);
            var inner = new Rectangle(lane.X + 6, lane.Y + 6, lane.Width - 12, lane.Height - 12);
            Raylib.DrawRectangleRec(inner, Palette.Path);
        }
    }

    private static void DrawZones(GameState s)
    {
        foreach (var zone in Map.BuildZones(s.Chapter))
            Raylib.DrawRectangleLinesEx(zone, 1.5f, new Color(241, 194, 96, 40));
    }

    private static void DrawWalls(GameState s)
    {
        foreach (var w in Map.WallRects(s.Chapter))
        {
            Raylib.DrawRectangleRec(new Rectangle(w.X - 2, w.Y + 3, w.Width + 4, w.Height + 2), Palette.WallDark);
            Raylib.DrawRectangleRec(w, Palette.Wall);
        }
    }

    private static void DrawKeep(GameState s)
    {
        Raylib.DrawRectangleRec(new Rectangle(-26, -23, 52, 51), Palette.Hex("4d4033"));
        Raylib.DrawRectangleRec(new Rectangle(-25, -18, 50, 43), Palette.Hex("ba9662"));
        Raylib.DrawRectangleRec(new Rectangle(-8, 4, 16, 21), Palette.Hex("59412f"));
        Raylib.DrawRectangleRec(new Rectangle(-3, -12, 6, 6), Palette.Hex("e2b554"));
    }

    private static void DrawGroundTraps(GameState s)
    {
        foreach (var st in s.Structures)
        {
            if (st.Role != StructureRole.GroundTrap) continue;
            Color fill = st.Kind switch
            {
                StructureKind.TarPit => new Color(28, 24, 22, 170),
                StructureKind.MoatLine => new Color(70, 110, 150, 150),
                StructureKind.Caltrops => new Color(96, 92, 86, 130),
                _ => new Color(120, 120, 120, 130), // spike
            };
            Raylib.DrawCircleV(st.Pos, st.Radius, fill);
            Raylib.DrawCircleLinesV(st.Pos, st.Radius, new Color(20, 20, 20, 120));
            if (st.Kind == StructureKind.SpikeTrap || st.Kind == StructureKind.Caltrops)
                for (int i = 0; i < 6; i++)
                {
                    float a = i / 6f * MathUtils.Tau;
                    Raylib.DrawCircleV(st.Pos + new Vector2(MathF.Cos(a), MathF.Sin(a)) * st.Radius * 0.55f, 2.5f, new Color(200, 200, 205, 200));
                }
        }
    }

    private static void DrawStructures(GameState s)
    {
        foreach (var st in s.Structures)
        {
            switch (st.Role)
            {
                case StructureRole.Tower: DrawTower(st); break;
                case StructureRole.Wall: DrawWall(st); break;
                case StructureRole.Mine: DrawMine(st); break;
                case StructureRole.Aura: DrawAura(st); break;
                case StructureRole.HeroBuff: DrawShrine(st); break;
            }
            DrawStructureLevel(s, st);
        }
    }

    private static void DrawStructureLevel(GameState s, Structure st)
    {
        // Damage bar for non-wall structures (walls draw their own in DrawWall).
        if (st.Role is not StructureRole.Wall and not StructureRole.HeroBuff
            && st.MaxHealth > 0f && st.Health < st.MaxHealth)
        {
            float frac = MathUtils.Clamp(st.Health / st.MaxHealth, 0f, 1f);
            Raylib.DrawRectangleRec(new Rectangle(st.Pos.X - 16, st.Pos.Y - st.Radius - 12, 32, 3), new Color(19, 25, 24, 210));
            Raylib.DrawRectangleRec(new Rectangle(st.Pos.X - 16, st.Pos.Y - st.Radius - 12, 32 * frac, 3), Palette.Hex("d2604f"));
        }

        // Level pips above the structure.
        for (int i = 0; i < st.Level - 1; i++)
            Raylib.DrawCircleV(st.Pos + new Vector2(-5 + i * 5, -st.Radius - 8), 2f, Palette.Hex("bfe0ff"));

        // Upgrade affordance + progress when the hero is standing on it.
        if (!st.Upgradable) return;
        float showReach = st.Role == StructureRole.Wall ? st.Radius + 28f : st.Radius + 32f;
        if (Vector2.Distance(s.Hero.Pos, st.Pos) > showReach) return;

        float prog = st.UpgradeCost > 0 ? (float)st.UpgradeInvested / st.UpgradeCost : 0f;
        Raylib.DrawRing(st.Pos, st.Radius + 5f, st.Radius + 8f, -90f, -90f + 360f * prog, 28, Palette.Hex("9fd0ff"));
        string label = $"UP {st.UpgradeInvested}/{st.UpgradeCost}";
        int w = Raylib.MeasureText(label, 10);
        Raylib.DrawText(label, (int)(st.Pos.X - w / 2f), (int)(st.Pos.Y + st.Radius + 6f), 10, Palette.Hex("9fd0ff"));
    }

    private static void DrawTower(Structure t)
    {
        Raylib.DrawCircleV(t.Pos + new Vector2(2, 3), t.Radius, new Color(22, 31, 29, 60));
        Raylib.DrawCircleV(t.Pos, t.Radius, Palette.Hex("6b573e"));
        Raylib.DrawCircleLinesV(t.Pos, t.Radius, Palette.Hex("c49a62"));
        Color top = t.Kind switch
        {
            StructureKind.Cannon => Palette.Hex("d27d48"),
            StructureKind.Ballista => Palette.Hex("ddc07a"),
            StructureKind.ChainCoil => Palette.Hex("a9d8ff"),
            StructureKind.FlameJet => Palette.Fire,
            StructureKind.FrostSpire => Palette.Hex("bcdcff"),
            StructureKind.StormSpire => Palette.Hex("9fb8ff"),
            _ => Palette.Hex("b08b59"),
        };
        Raylib.DrawCircleV(t.Pos, t.Radius * 0.5f, top);
    }

    private static void DrawWall(Structure w)
    {
        var r = new Rectangle(w.Pos.X - w.Radius, w.Pos.Y - w.Radius, w.Radius * 2f, w.Radius * 2f);
        Raylib.DrawRectangleRec(new Rectangle(r.X - 2, r.Y + 3, r.Width + 4, r.Height + 2), Palette.Hex("584535"));
        Raylib.DrawRectangleRec(r, w.Regen ? Palette.Hex("8a8f9a") : w.Retaliate ? Palette.Hex("9a6b52") : Palette.Hex("b48152"));
        float frac = MathUtils.Clamp(w.Health / w.MaxHealth, 0f, 1f);
        Raylib.DrawRectangleRec(new Rectangle(w.Pos.X - 21, w.Pos.Y - w.Radius - 8, 42, 4), new Color(19, 25, 24, 210));
        Raylib.DrawRectangleRec(new Rectangle(w.Pos.X - 21, w.Pos.Y - w.Radius - 8, 42 * frac, 4), Palette.Hex("b9cc78"));
    }

    private static void DrawMine(Structure m)
    {
        Raylib.DrawRectangleRec(new Rectangle(m.Pos.X - 17, m.Pos.Y - 13, 34, 28), Palette.Hex("5e4e3e"));
        Raylib.DrawCircleV(m.Pos + new Vector2(0, 1), 9f, Palette.Hex("2b302e"));
        Raylib.DrawCircleV(m.Pos + new Vector2(12, -10), 4f, Palette.Gold);
        if (m.Kind == StructureKind.TradingPost) // a second coin marks the richer mine
            Raylib.DrawCircleV(m.Pos + new Vector2(-11, -9), 3.5f, Palette.Hex("ffd66b"));
    }

    private static void DrawAura(Structure a)
    {
        Raylib.DrawCircleV(a.Pos, a.AuraRange, new Color(222, 179, 90, 14));
        Raylib.DrawCircleLinesV(a.Pos, a.AuraRange, new Color(222, 179, 90, 40));
        Color mark = a.AuraKind switch
        {
            AuraKind.Damage => Palette.Hex("a64c3e"),
            AuraKind.Rate => Palette.Hex("d27d48"),
            AuraKind.Range => Palette.Hex("7fa9d6"),
            _ => Palette.Hex("8fbf7f"),
        };
        Raylib.DrawCircleV(a.Pos, 9f, Palette.Hex("6e5b49"));
        Raylib.DrawLineEx(a.Pos + new Vector2(0, 14), a.Pos + new Vector2(0, -18), 3f, Palette.Hex("d6b370"));
        Raylib.DrawTriangle(a.Pos + new Vector2(2, -18), a.Pos + new Vector2(18, -12), a.Pos + new Vector2(2, -3), mark);
    }

    private static void DrawShrine(Structure st)
    {
        Raylib.DrawCircleV(st.Pos, 14f, Palette.Hex("554737"));
        Raylib.DrawCircleLinesV(st.Pos, 14f, Palette.Hex("d4a65b"));
        Raylib.DrawCircleV(st.Pos, 5f, Palette.Fire);
    }

    private static void DrawPads(GameState s)
    {
        float now = (float)Raylib.GetTime();
        foreach (var pad in s.Pads)
        {
            float progress = (float)pad.Invested / pad.Def.Cost;
            float pulse = 1f + MathF.Sin(now * 3f + pad.Pos.X) * 0.07f;
            float r = 22f * pulse;
            Raylib.DrawCircleV(pad.Pos, r, new Color(35, 50, 47, 210));
            Raylib.DrawCircleLinesV(pad.Pos, r, new Color(241, 194, 96, 150));
            // progress arc approximated by a second ring scaled to progress
            Raylib.DrawRing(pad.Pos, 14f, 17f, -90f, -90f + 360f * progress, 32, Palette.Hex("f0bd58"));

            int label = Raylib.MeasureText(pad.Def.Short, 9);
            Raylib.DrawText(pad.Def.Short, (int)(pad.Pos.X - label / 2f), (int)(pad.Pos.Y - 33), 9, Palette.Hex("f4d78d"));
            string cost = $"{pad.Invested}/{pad.Def.Cost}";
            int cw = Raylib.MeasureText(cost, 10);
            Raylib.DrawText(cost, (int)(pad.Pos.X - cw / 2f), (int)(pad.Pos.Y - 2), 10, Palette.Hex("f3d17c"));
        }
    }

    private static void DrawDrops(GameState s)
    {
        foreach (var d in s.Drops)
        {
            var p = d.Pos + new Vector2(0, MathF.Sin(d.Bob) * 3f);
            if (d.Kind == DropKind.Ember)
            {
                Raylib.DrawRectanglePro(new Rectangle(p.X, p.Y, 14, 14), new Vector2(7, 7), 45f, Palette.Hex("e76f42"));
            }
            else if (d.Kind == DropKind.Relic)
            {
                // A glinting violet gem, with a pulsing halo so it stands out as loot.
                float pulse = 8f + MathF.Sin((float)Raylib.GetTime() * 4f + d.Bob) * 2f;
                Raylib.DrawCircleLinesV(p, pulse + 4f, new Color((byte)201, (byte)163, (byte)255, (byte)120));
                Raylib.DrawPoly(p, 4, pulse, (float)Raylib.GetTime() * 40f, Palette.Hex("b78cf0"));
                Raylib.DrawPolyLines(p, 4, pulse, (float)Raylib.GetTime() * 40f, Palette.Hex("e6d2ff"));
            }
            else
            {
                Raylib.DrawCircleV(p, d.Radius, Palette.Hex("d8842d"));
                Raylib.DrawCircleLinesV(p, d.Radius, Palette.Hex("ffd064"));
            }
        }
    }

    private static void DrawEnemies(GameState s)
    {
        foreach (var e in s.Enemies)
        {
            // Flyers cast a raised shadow to read as airborne.
            float shadowOff = e.Flying ? 12f : 4f;
            Raylib.DrawCircleV(e.Pos + new Vector2(2, shadowOff), e.Radius, new Color(22, 31, 29, 64));
            Color body = EnemyBodyColor(e);
            if (e.HitTimer > 0f) body = Palette.Hex("f4b06e");

            // Siege engines render as an armored chassis (square hull + treads).
            if (e.Siege)
            {
                var r = new Rectangle(e.Pos.X - e.Radius, e.Pos.Y - e.Radius, e.Radius * 2f, e.Radius * 2f);
                Raylib.DrawRectangleRec(r, body);
                Raylib.DrawRectangleLinesEx(r, 2f, Palette.Hex("3c3026"));
                Raylib.DrawRectangleRec(new Rectangle(e.Pos.X - e.Radius, e.Pos.Y - e.Radius, 4f, e.Radius * 2f), Palette.Hex("4a3c30"));
                Raylib.DrawRectangleRec(new Rectangle(e.Pos.X + e.Radius - 4f, e.Pos.Y - e.Radius, 4f, e.Radius * 2f), Palette.Hex("4a3c30"));
                Raylib.DrawRectangleRec(new Rectangle(e.Pos.X - 5f, e.Pos.Y - 5f, 10f, 10f), Palette.Hex("9a5240"));
            }
            else if (e.Phantom)
            {
                // Assassin: dark sharp core with a faint blink halo.
                Raylib.DrawCircleV(e.Pos, e.Radius, body);
                Raylib.DrawCircleLinesV(e.Pos, e.Radius + 2f, new Color((byte)176, (byte)123, (byte)208, (byte)120));
                Raylib.DrawCircleV(e.Pos, e.Radius * 0.4f, Palette.Hex("3a2647"));
            }
            else if (e.StatusImmune)
            {
                // Wraith: translucent, ghostly outline.
                Raylib.DrawCircleV(e.Pos, e.Radius, new Color(body.R, body.G, body.B, (byte)160));
                Raylib.DrawCircleLinesV(e.Pos, e.Radius + 3f, new Color((byte)150, (byte)210, (byte)200, (byte)130));
            }
            else
            {
                Raylib.DrawCircleV(e.Pos, e.Radius, body);
                Raylib.DrawCircleLinesV(e.Pos, e.Radius, Palette.EnemyDark);
            }

            // Chapter boss: a pulsing menace ring + a rotating crown of spikes.
            if (e.Boss)
            {
                float t = (float)Raylib.GetTime();
                Raylib.DrawCircleLinesV(e.Pos, e.Radius + 5f + MathF.Sin(t * 4f) * 1.5f, new Color((byte)224, (byte)88, (byte)74, (byte)190));
                for (int i = 0; i < 8; i++)
                {
                    float a = i / 8f * MathUtils.Tau + t * 0.5f;
                    Raylib.DrawCircleV(e.Pos + new Vector2(MathF.Cos(a), MathF.Sin(a)) * (e.Radius + 9f), 2.6f, Palette.Hex("e0a24a"));
                }
            }

            if (e.ShieldPerHit > 0f)
                Raylib.DrawCircleLinesV(e.Pos, e.Radius + 4f, new Color(170, 200, 230, 200));
            if (e.Healer)
            {
                Raylib.DrawRectangleRec(new Rectangle(e.Pos.X - 4, e.Pos.Y - 1, 8, 2), Palette.Hex("d6f0d2"));
                Raylib.DrawRectangleRec(new Rectangle(e.Pos.X - 1, e.Pos.Y - 4, 2, 8), Palette.Hex("d6f0d2"));
            }
            if (e.Flying)
            {
                Raylib.DrawLineEx(e.Pos + new Vector2(-e.Radius - 3, -2), e.Pos + new Vector2(-2, 0), 2f, Palette.Hex("c8b6d6"));
                Raylib.DrawLineEx(e.Pos + new Vector2(e.Radius + 3, -2), e.Pos + new Vector2(2, 0), 2f, Palette.Hex("c8b6d6"));
            }

            if (e.SlowTimer > 0f)
                Raylib.DrawCircleLinesV(e.Pos, e.Radius + 3f, new Color(150, 200, 235, 150));

            if (e.Elite || e.Health < e.MaxHealth)
            {
                float frac = MathUtils.Clamp(e.Health / e.MaxHealth, 0f, 1f);
                Raylib.DrawRectangleRec(new Rectangle(e.Pos.X - 17, e.Pos.Y - e.Radius - 9, 34, 4), Palette.Hex("362726"));
                Raylib.DrawRectangleRec(new Rectangle(e.Pos.X - 17, e.Pos.Y - e.Radius - 9, 34 * frac, 4),
                    e.Elite ? Palette.Hex("e0994f") : Palette.Hex("c15d4d"));
            }
        }
    }

    private static Color EnemyBodyColor(Enemy e) => e.Kind switch
    {
        EnemyKind.Runner => Palette.Hex("cc704b"),
        EnemyKind.Brute => Palette.Hex("88453e"),
        EnemyKind.Flyer => Palette.Hex("9a7bb0"),
        EnemyKind.Shielded => Palette.Hex("6e7a86"),
        EnemyKind.Healer => Palette.Hex("5f9e6a"),
        EnemyKind.Siege => Palette.Hex("6f5a48"),
        EnemyKind.Boss => Palette.Hex("9c3b46"),
        EnemyKind.Assassin => Palette.Hex("8a5aa0"),
        EnemyKind.Wraith => Palette.Hex("7fb6ad"),
        _ => e.Elite ? Palette.Elite : Palette.Enemy,
    };

    /// <summary>
    /// Screen-edge arrows for off-screen enemies, coloured by type and sized by
    /// threat — telegraphs incoming siege/elite/brute before they reach the walls.
    /// </summary>
    private static void DrawEdgeIndicators(GameState s)
    {
        if (s.Phase != Phase.Combat || s.Over) return;
        int w = Raylib.GetScreenWidth(), h = Raylib.GetScreenHeight();
        const float margin = 26f;
        var center = new Vector2(w / 2f, h / 2f);
        float halfW = w / 2f - margin, halfH = h / 2f - margin;

        foreach (var e in s.Enemies)
        {
            if (e.Dead) continue;
            var sp = Raylib.GetWorldToScreen2D(e.Pos, s.Cam);
            if (sp.X >= 0 && sp.X <= w && sp.Y >= 0 && sp.Y <= h) continue; // on-screen

            var dir = MathUtils.Normalize(sp - center);
            if (dir == Vector2.Zero) continue;

            // Project the direction onto the inset screen-rect border.
            float tx = MathF.Abs(dir.X) > 1e-4f ? halfW / MathF.Abs(dir.X) : float.PositiveInfinity;
            float ty = MathF.Abs(dir.Y) > 1e-4f ? halfH / MathF.Abs(dir.Y) : float.PositiveInfinity;
            var pos = center + dir * MathF.Min(tx, ty);

            bool big = e.Boss || e.Siege || e.Elite || e.Kind == EnemyKind.Brute;
            float size = e.Boss ? 16f : big ? 13f : 8f;
            float angle = MathF.Atan2(dir.Y, dir.X);
            Color col = e.Boss ? Palette.Hex("e0584a") : e.Siege ? Palette.Hex("c79256") : e.Elite ? Palette.Elite : EnemyBodyColor(e);

            // Arrow triangle pointing outward (toward the threat).
            Vector2 Tip(float lx, float ly)
            {
                float c = MathF.Cos(angle), si = MathF.Sin(angle);
                return pos + new Vector2(lx * c - ly * si, lx * si + ly * c);
            }
            // Raylib back-face-culls DrawTriangle; vertices must be counter-clockwise.
            Raylib.DrawTriangle(Tip(size, 0), Tip(-size * 0.7f, -size * 0.7f), Tip(-size * 0.7f, size * 0.7f), col);
            if (big)
                Raylib.DrawTriangleLines(Tip(size + 2f, 0), Tip(-size * 0.9f, size * 0.9f), Tip(-size * 0.9f, -size * 0.9f), Palette.Hex("1e2928"));
        }
    }

    private static void DrawProjectiles(GameState s)
    {
        foreach (var p in s.Projectiles)
            Raylib.DrawCircleV(p.Pos, p.Radius, p.Color);
    }

    private static void DrawHero(Hero hero)
    {
        var p = hero.Pos;
        float angle = MathF.Atan2(hero.Facing.Y, hero.Facing.X);
        float now = (float)Raylib.GetTime();

        // Standard rotation helper: lx = forward, ly = sideways (right).
        // JS origin used rotate(angle+PI/2); point (jx,jy) → Rot(-jy, jx) here.
        Vector2 Rot(float lx, float ly)
        {
            float c = MathF.Cos(angle), si = MathF.Sin(angle);
            return p + new Vector2(lx * c - ly * si, lx * si + ly * c);
        }

        // Invulnerability blink — alternate between 35% and full alpha.
        bool blink = hero.Invulnerable > 0f && ((int)(hero.Invulnerable * 12f) % 2 == 0);
        byte al = blink ? (byte)88 : (byte)255;

        Color Tint(Color c) => new(c.R, c.G, c.B, al);

        // Drop shadow (fixed light direction, independent of facing).
        Raylib.DrawCircleV(p + new Vector2(2f, 7f), 11f, new Color(15, 24, 23, 70));

        // Cape — wider trailing triangle: tip forward, base trailing.
        // JS points: (0,-11)→Rot(11,0), (12,14)→Rot(-14,12), (-12,14)→Rot(-14,-12)
        Raylib.DrawTriangle(Rot(11, 0), Rot(-14, 12), Rot(-14, -12), Tint(hero.Profile.Cloak));

        // Body circle, offset 4 units forward (matches JS body at local (0,-4)).
        var bodyPos = Rot(4, 0);
        Raylib.DrawCircleV(bodyPos, 8f, Tint(Palette.Hero));
        Raylib.DrawCircleLinesV(bodyPos, 8f, new Color((byte)0x6c, (byte)0x4d, (byte)0x38, al));

        // Weapon — Ranger: bow arc (D-curve to the right); Warden: diagonal blade.
        var weapCol = new Color((byte)0xe3, (byte)0xbb, (byte)0x6a, al);
        if (hero.Kind == HeroKind.Warden)
        {
            // Heavier diagonal blade (light bluish steel).
            var bladeCol = new Color((byte)0xb0, (byte)0xc8, (byte)0xff, al);
            Raylib.DrawLineEx(Rot(-3, -7), Rot(9, 8), 4f, bladeCol);
            Raylib.DrawCircleV(Rot(9, 8), 2.5f, bladeCol);
        }
        else
        {
            // Bow arc: centre at JS (9,-2) → Rot(2,9); spans ±92° facing outward.
            var bowCenter = Rot(2, 9);
            float bowMid = angle * (180f / MathF.PI) + 77f; // outward direction in Raylib degrees
            Raylib.DrawRing(bowCenter, 5.5f, 7.5f, bowMid - 92f, bowMid + 92f, 16, weapCol);
        }

        // Ability-ready pulse ring.
        if (hero.AbilityCooldown <= 0f)
        {
            float pulse = 18f + MathF.Sin(now * 6.25f) * 2f;
            Raylib.DrawCircleLinesV(p, pulse, new Color(255, 208, 102, 128));
        }

        // Overdrive ring.
        if (hero.Overdrive > 0f)
            Raylib.DrawCircleLinesV(p, 22f + MathF.Sin(now * 11f) * 3f, new Color(255, 135, 75, 165));
    }

    private static void DrawParticles(GameState s)
    {
        foreach (var part in s.Particles)
        {
            byte a = (byte)(MathUtils.Clamp(part.Life / part.MaxLife, 0f, 1f) * 255f);
            Raylib.DrawCircleV(part.Pos, part.Size, new Color(part.Color.R, part.Color.G, part.Color.B, a));
        }
    }

    private static void DrawFloaters(GameState s)
    {
        foreach (var f in s.Floaters)
        {
            byte a = (byte)(MathUtils.Clamp(f.Life / f.MaxLife, 0f, 1f) * 255f);
            int w = Raylib.MeasureText(f.Text, 11);
            Raylib.DrawText(f.Text, (int)(f.Pos.X - w / 2f), (int)f.Pos.Y, 11,
                new Color(f.Color.R, f.Color.G, f.Color.B, a));
        }
    }

    private static void DrawHud(GameState s)
    {
        Raylib.DrawText($"GOLD {s.Gold}", 16, 16, 22, Palette.Gold);
        Raylib.DrawText($"WAVE {s.Wave}", 16, 42, 22, Palette.Hero);
        Raylib.DrawText($"FORT {s.Chapter}", 16, 68, 22, Palette.Hero);
        int alive = s.Enemies.Count + (s.Spawning?.Remaining ?? 0);
        Raylib.DrawText($"RAIDERS {alive}", 16, 94, 18, Palette.PathEdge);
        Raylib.DrawText($"BEST WAVE {s.BestWave}", 16, 112, 16, Palette.Hex("c9b074"));

        // Keep + hero bars (top-right).
        int sw = Raylib.GetScreenWidth();
        DrawBar(sw - 220, 18, 200, 14, s.KeepHealth / s.KeepMaxHealth, Palette.Hex("b9cc78"), "KEEP");
        DrawBar(sw - 220, 40, 200, 14, s.Hero.Health / s.Hero.MaxHealth, Palette.Hex("d6b46c"),
            $"{s.Hero.Profile.Initial} LV{s.Hero.Level}");
        DrawHeroLoadout(s);

        if (s.Modifier.Id != "none")
            Raylib.DrawText($"TRIAL  {s.Modifier.Name}", 16, Raylib.GetScreenHeight() - 48, 16, Palette.Hex("d6a6e0"));
        Raylib.DrawText($"{Raylib.GetFPS()} FPS", 16, Raylib.GetScreenHeight() - 28, 18, Palette.PathEdge);

        DrawActiveSynergies(s);

        if (s.Over)
        {
            int cy = Raylib.GetScreenHeight() / 2;
            Raylib.DrawRectangle(0, 0, sw, Raylib.GetScreenHeight(), new Color(11, 17, 19, 180));
            DrawCentered("THE KEEP HAS FALLEN", 40, cy - 70, Palette.Hex("efd18a"));
            DrawCentered($"Reached wave {s.Wave}   -   best wave {s.BestWave}", 24, cy - 22, Palette.Hero);
            DrawCentered($"{s.Kills} raiders defeated   /   {s.Structures.Count} structures standing   /   {s.SeenSynergies.Count} synergies discovered",
                18, cy + 12, Palette.PathEdge);
            DrawCentered("press R to begin again", 20, cy + 48, Palette.Gold);
        }
    }

    /// <summary>Hero passives + collected relics, under the hero bar (top-right).</summary>
    private static void DrawHeroLoadout(GameState s)
    {
        var hero = s.Hero;
        int sw = Raylib.GetScreenWidth();
        int x = sw - 220, y = 58;

        var passives = new List<string>();
        if (hero.QuickHands) passives.Add("QH");
        if (hero.Signature) passives.Add(hero.Kind == HeroKind.Warden ? "CLV" : "RIC");
        if (hero.SecondWind) passives.Add("SW");
        if (passives.Count > 0)
        {
            Raylib.DrawText("PASSIVES " + string.Join(" ", passives), x, y, 12, Palette.Hex("9fd0ff"));
            y += 16;
        }

        if (hero.Relics.Count == 0) return;
        int cx = x;
        foreach (RelicKind r in Enum.GetValues<RelicKind>())
        {
            if (!hero.Relics.Contains(r)) continue;
            var (col, letter) = r switch
            {
                RelicKind.EmberRing  => (Palette.Hex("e0994f"), "R"),
                RelicKind.SwiftBoots => (Palette.Hex("8fbf7f"), "B"),
                RelicKind.WardenCloak => (Palette.Hex("7fa9d6"), "C"),
                _ => (Palette.Hex("e2c452"), "E"),
            };
            Raylib.DrawRectangle(cx, y, 18, 18, new Color(col.R, col.G, col.B, (byte)210));
            Raylib.DrawRectangleLines(cx, y, 18, 18, Palette.Ink);
            Raylib.DrawText(letter, cx + 6, y + 3, 13, Palette.Ink);
            cx += 22;
        }
    }

    private static void DrawActiveSynergies(GameState s)
    {
        if (s.ActiveSynergies.Count == 0) return;
        int y = 130;
        Raylib.DrawText("SYNERGIES", 16, y, 16, Palette.Gold);
        y += 22;
        foreach (var def in SynergyEngine.Catalog)
        {
            if (!s.ActiveSynergies.Contains(def.Id)) continue;
            Color c = def.Type switch
            {
                "Keystone" => Palette.Hex("e0a85a"),
                "Field" => Palette.Hex("8fbf7f"),
                "Rune" => Palette.Hex("c79be0"),
                _ => Palette.Hex("9fb6c9"),
            };
            Raylib.DrawText($"+ {def.Name}", 18, y, 15, c);
            y += 20;
        }
    }

    private static void DrawBar(int x, int y, int w, int h, float frac, Color fill, string label)
    {
        frac = MathUtils.Clamp(frac, 0f, 1f);
        Raylib.DrawRectangle(x, y, w, h, new Color(19, 25, 24, 210));
        Raylib.DrawRectangle(x, y, (int)(w * frac), h, fill);
        Raylib.DrawText(label, x - Raylib.MeasureText(label, 14) - 8, y, 14, Palette.Hero);
    }

    private static void DrawCentered(string text, int fontSize, int y, Color color)
    {
        int w = Raylib.MeasureText(text, fontSize);
        Raylib.DrawText(text, Raylib.GetScreenWidth() / 2 - w / 2, y, fontSize, color);
    }
}
