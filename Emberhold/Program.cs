using Emberhold.Game;
using Raylib_cs;

namespace Emberhold;

public static class Program
{
    public const int DesignWidth = 1280;
    public const int DesignHeight = 720;

    public static int Main(string[] args)
    {
        // Headless-ish smoke mode: run a fixed number of frames then exit.
        // Usage: Emberhold --smoke 120 [--shot path.png]
        int? smokeFrames = ParseSmokeFrames(args);
        string? shotPath = ParseStringArg(args, "--shot");

        Raylib.SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.Msaa4xHint);
        Raylib.InitWindow(DesignWidth, DesignHeight, "Emberhold");
        Raylib.SetTargetFPS(60);

        int startWave = 0;
        if (ParseStringArg(args, "--wave") is string ws && int.TryParse(ws, out int wv)) startWave = wv;
        int startChapter = 0;
        if (ParseStringArg(args, "--chapter") is string cs && int.TryParse(cs, out int cv)) startChapter = cv;
        var game = new GameApp(
            auto: Array.IndexOf(args, "--auto") >= 0,
            seed: Array.IndexOf(args, "--seed") >= 0,
            startWave: startWave,
            codex: Array.IndexOf(args, "--codex") >= 0,
            lose: Array.IndexOf(args, "--lose") >= 0,
            startChapter: startChapter);

        int frame = 0;
        while (!Raylib.WindowShouldClose())
        {
            float dt = MathF.Min(0.05f, Raylib.GetFrameTime());
            game.Update(dt);

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
