using Emberhold.Game;
using Raylib_cs;

namespace Emberhold;

public static class Program
{
    public const int DesignWidth = 1280;
    public const int DesignHeight = 720;

    /// <summary>Current build version, shown on the title screen. MUST mirror the
    /// repo-root <c>VERSION</c> file, which is what the release pipeline reads for the
    /// release tag/name — bump both together (see AGENTS.md).</summary>
    public const string Version = "0.40.0";

    public static int Main(string[] args)
    {
        // Headless-ish smoke mode: run a fixed number of frames then exit.
        // Usage: Emberhold --smoke 120 [--shot path.png]
        int? smokeFrames = ParseSmokeFrames(args);
        string? shotPath = ParseStringArg(args, "--shot");

        Raylib.SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.Msaa4xHint);
        Raylib.InitWindow(DesignWidth, DesignHeight, "Emberhold");
        Raylib.SetTargetFPS(60);
        Raylib.SetExitKey(KeyboardKey.Null);
        Game.BalanceConfig.Load(); // apply any persisted balance tuning
        // Smoke runs stay silent (they're for CI/screenshots, often on dev boxes);
        // --sound forces audio on anyway (for testing the device path), --mute forces it off.
        bool mute = Array.IndexOf(args, "--mute") >= 0
            || (smokeFrames is not null && Array.IndexOf(args, "--sound") < 0);
        Render.Audio.Init(mute);

        int startWave = 0;
        if (ParseStringArg(args, "--wave") is string ws && int.TryParse(ws, out int wv)) startWave = wv;
        int startChapter = 0;
        if (ParseStringArg(args, "--chapter") is string cs && int.TryParse(cs, out int cv)) startChapter = cv;
        int startHero = 0;
        if (ParseStringArg(args, "--hero") is string hs && int.TryParse(hs, out int hv)) startHero = hv;

        // A clean launch (no smoke/debug flags) opens the title menu; any debug entry
        // point jumps straight into a run so smoke/auto tooling is unchanged.
        bool debugStart = smokeFrames is not null
            || Array.IndexOf(args, "--auto") >= 0 || Array.IndexOf(args, "--seed") >= 0
            || Array.IndexOf(args, "--lose") >= 0 || startWave > 0 || startChapter > 0
            || Array.IndexOf(args, "--pause") >= 0 || Array.IndexOf(args, "--skills") >= 0;

        bool forceTitle = Array.IndexOf(args, "--title") >= 0; // debug: screenshot the title menu
        var game = new GameApp(
            auto: Array.IndexOf(args, "--auto") >= 0,
            seed: Array.IndexOf(args, "--seed") >= 0,
            startWave: startWave,
            codex: Array.IndexOf(args, "--codex") >= 0,
            lose: Array.IndexOf(args, "--lose") >= 0,
            startChapter: startChapter,
            startHero: startHero,
            paused: Array.IndexOf(args, "--pause") >= 0,
            skills: Array.IndexOf(args, "--skills") >= 0,
            startAtTitle: forceTitle || !debugStart,
            heroSwap: Array.IndexOf(args, "--heroswap") >= 0,
            balance: Array.IndexOf(args, "--balance") >= 0,
            meteorEvent: Array.IndexOf(args, "--meteor") >= 0,
            exoticShop: Array.IndexOf(args, "--exotics") >= 0,
            swarmWave: Array.IndexOf(args, "--swarm") >= 0,
            ascendDemo: Array.IndexOf(args, "--ascend") >= 0,
            furyDemo: Array.IndexOf(args, "--fury") >= 0,
            champDemo: Array.IndexOf(args, "--champion") >= 0,
            lastStand: Array.IndexOf(args, "--laststand") >= 0,
            trophyHall: Array.IndexOf(args, "--trophies") >= 0);

        int frame = 0;
        while (!Raylib.WindowShouldClose())
        {
            float dt = MathF.Min(0.05f, Raylib.GetFrameTime());
            game.Update(dt);
            if (game.ShouldQuit) break;

            Raylib.BeginDrawing();
            game.Draw();
            Raylib.EndDrawing();

            frame++;
            bool lastFrame = smokeFrames is int l && frame >= l;
            if (lastFrame && shotPath is not null)
                Raylib.TakeScreenshot(shotPath);
            if (lastFrame)
                break;
        }

        if (smokeFrames is not null)
            Console.WriteLine($"REPORT {game.Report()}");

        Render.Audio.Shutdown();
        Raylib.CloseWindow();
        return 0;
    }

    private static int? ParseSmokeFrames(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] is "--smoke" or "-s")
            {
                if (i + 1 < args.Length && int.TryParse(args[i + 1], out int n))
                    return n;
                return 120;
            }
        }
        return null;
    }

    private static string? ParseStringArg(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name)
                return args[i + 1];
        return null;
    }
}
