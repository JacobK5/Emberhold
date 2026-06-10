using Emberhold.Core;
using Raylib_cs;

namespace Emberhold.Render;

/// <summary>Every game sound, by id. All are synthesized at startup (no assets).</summary>
public enum SfxId
{
    Shot, TowerShot, CannonShot, Kill, Coin, Build, Upgrade, WaveStart, BossHorn,
    KeepHit, HeroHurt, Ultimate, Click, Synergy, GameOver, Nova, Dash, Rally, LevelUp,
    // War-drum kit (driven by the adaptive music sequencer, not gameplay events).
    Kick, Tom, Hat,
}

/// <summary>
/// Procedural audio: synthesizes the whole SFX set from <see cref="SfxSynth"/>
/// recipes at init, then plays them with per-id rate limits, round-robin alias
/// polyphony, and light pitch jitter. Every call is a no-op until Init succeeds,
/// so headless tests and --mute runs never touch the audio device.
/// </summary>
public static class Audio
{
    public static bool Ready { get; private set; }

    private static readonly Dictionary<SfxId, Sound[]> Pool = new();
    private static readonly Dictionary<SfxId, int> Next = new();
    private static readonly Dictionary<SfxId, double> LastPlay = new();
    private static readonly Random Rng = new();

    /// <summary>Minimum seconds between plays of the same sound (anti-spam).</summary>
    private static readonly Dictionary<SfxId, float> MinGap = new()
    {
        [SfxId.Shot] = 0.04f, [SfxId.TowerShot] = 0.05f, [SfxId.CannonShot] = 0.09f,
        [SfxId.Kill] = 0.05f, [SfxId.Coin] = 0.08f, [SfxId.KeepHit] = 0.18f,
        [SfxId.HeroHurt] = 0.3f, [SfxId.Click] = 0.05f, [SfxId.WaveStart] = 0.5f,
        [SfxId.BossHorn] = 0.6f, [SfxId.Synergy] = 0.4f, [SfxId.LevelUp] = 0.25f,
        [SfxId.Build] = 0.2f, [SfxId.Upgrade] = 0.2f, [SfxId.Nova] = 0.25f,
        [SfxId.Dash] = 0.15f, [SfxId.Rally] = 0.3f, [SfxId.Ultimate] = 0.5f,
        [SfxId.GameOver] = 1f,
    };

    public static void Init(bool mute = false)
    {
        if (mute || Ready) return;
        try
        {
            Raylib.InitAudioDevice();
            if (!Raylib.IsAudioDeviceReady()) return;
            Raylib.SetMasterVolume(0.8f);
            foreach (var (id, samples, voices) in Recipes())
            {
                var wave = Raylib.LoadWaveFromMemory(".wav", SfxSynth.ToWav(samples));
                var sound = Raylib.LoadSoundFromWave(wave);
                Raylib.UnloadWave(wave);
                var arr = new Sound[voices];
                arr[0] = sound;
                for (int i = 1; i < voices; i++) arr[i] = Raylib.LoadSoundAlias(sound);
                Pool[id] = arr;
            }
            Ready = true;
        }
        catch
        {
            Ready = false; // no device / init failure: play() stays a no-op
        }
    }

    public static void Shutdown()
    {
        if (!Ready) return;
        Ready = false;
        Raylib.CloseAudioDevice(); // releases buffers with the device
        Pool.Clear();
    }

    /// <summary>Play a sound (rate-limited per id) with volume, pitch, and jitter.</summary>
    public static void Play(SfxId id, float vol = 1f, float pitch = 1f)
    {
        if (!Ready || !Pool.TryGetValue(id, out var voices)) return;
        double now = Raylib.GetTime();
        if (LastPlay.TryGetValue(id, out var last) && now - last < MinGap.GetValueOrDefault(id, 0.06f))
            return;
        LastPlay[id] = now;

        int i = Next.GetValueOrDefault(id);
        Next[id] = (i + 1) % voices.Length;
        var s = voices[i];
        Raylib.SetSoundVolume(s, Math.Clamp(vol, 0f, 1f));
        Raylib.SetSoundPitch(s, pitch * (0.95f + (float)Rng.NextDouble() * 0.1f));
        Raylib.PlaySound(s);
    }

    // ---- the sound set ----------------------------------------------------

    private static IEnumerable<(SfxId Id, float[] Samples, int Voices)> Recipes()
    {
        // Hero shot: a quick "pew" pluck.
        yield return (SfxId.Shot, SfxSynth.Sweep(880f, 520f, 0.07f, WaveShape.Triangle, 0.5f), 4);

        // Tower shot: shorter, duller thunk so a full fort stays un-shrill.
        yield return (SfxId.TowerShot, SfxSynth.Sweep(420f, 290f, 0.05f, WaveShape.Triangle, 0.45f), 4);

        // Cannon: low thump + muffled blast.
        yield return (SfxId.CannonShot, SfxSynth.Mix(
            SfxSynth.Noise(0.16f, 900f, 0.7f),
            SfxSynth.Sweep(110f, 60f, 0.16f, WaveShape.Sine, 0.8f)), 3);

        // Kill: descending pop.
        yield return (SfxId.Kill, SfxSynth.Sweep(660f, 210f, 0.09f, WaveShape.Square, 0.5f), 4);

        // Coin: bright two-partial ping.
        yield return (SfxId.Coin, SfxSynth.Mix(
            SfxSynth.Tone(1320f, 0.06f, WaveShape.Sine, 0.55f),
            SfxSynth.Tone(2640f, 0.045f, WaveShape.Sine, 0.22f)), 3);

        // Build complete: rising major arpeggio.
        yield return (SfxId.Build, SfxSynth.Concat(
            SfxSynth.Tone(523f, 0.07f, WaveShape.Triangle, 0.5f),
            SfxSynth.Tone(659f, 0.07f, WaveShape.Triangle, 0.5f),
            SfxSynth.Tone(784f, 0.12f, WaveShape.Triangle, 0.55f)), 2);

        // Structure upgrade: same shape, brighter cap note.
        yield return (SfxId.Upgrade, SfxSynth.Concat(
            SfxSynth.Tone(659f, 0.06f, WaveShape.Triangle, 0.5f),
            SfxSynth.Tone(784f, 0.06f, WaveShape.Triangle, 0.5f),
            SfxSynth.Tone(1047f, 0.12f, WaveShape.Triangle, 0.55f)), 2);

        // Wave start: a low drum hit.
        yield return (SfxId.WaveStart, SfxSynth.Mix(
            SfxSynth.Sweep(140f, 70f, 0.18f, WaveShape.Sine, 0.9f),
            SfxSynth.Noise(0.1f, 420f, 0.4f)), 2);

        // Boss/general/champion horn: two grinding low saws a fifth apart.
        yield return (SfxId.BossHorn, SfxSynth.Mix(
            SfxSynth.Tone(146f, 0.5f, WaveShape.Saw, 0.5f, attack: 0.03f, decayPow: 1.6f),
            SfxSynth.Tone(98f, 0.5f, WaveShape.Saw, 0.5f, attack: 0.03f, decayPow: 1.6f)), 2);

        // Keep hit: dull thud.
        yield return (SfxId.KeepHit, SfxSynth.Mix(
            SfxSynth.Sweep(130f, 55f, 0.12f, WaveShape.Sine, 0.9f),
            SfxSynth.Noise(0.06f, 320f, 0.5f)), 3);

        // Hero hurt: a harsh noise bite.
        yield return (SfxId.HeroHurt, SfxSynth.Mix(
            SfxSynth.Noise(0.08f, 1600f, 0.7f),
            SfxSynth.Sweep(300f, 140f, 0.08f, WaveShape.Square, 0.4f)), 2);

        // Cataclysm: long falling boom.
        yield return (SfxId.Ultimate, SfxSynth.Mix(
            SfxSynth.Sweep(320f, 42f, 0.55f, WaveShape.Saw, 0.7f, decayPow: 2f),
            SfxSynth.Noise(0.5f, 700f, 0.6f, decayPow: 2f)), 1);

        // UI click.
        yield return (SfxId.Click, SfxSynth.Tone(1150f, 0.03f, WaveShape.Square, 0.35f), 2);

        // Synergy discovered: sparkle arpeggio.
        yield return (SfxId.Synergy, SfxSynth.Concat(
            SfxSynth.Tone(784f, 0.06f, WaveShape.Sine, 0.45f),
            SfxSynth.Tone(988f, 0.06f, WaveShape.Sine, 0.45f),
            SfxSynth.Tone(1175f, 0.06f, WaveShape.Sine, 0.45f),
            SfxSynth.Tone(1568f, 0.14f, WaveShape.Sine, 0.5f)), 2);

        // Game over: slow descending minor line.
        yield return (SfxId.GameOver, SfxSynth.Concat(
            SfxSynth.Tone(392f, 0.2f, WaveShape.Triangle, 0.55f, decayPow: 1.5f),
            SfxSynth.Tone(330f, 0.2f, WaveShape.Triangle, 0.55f, decayPow: 1.5f),
            SfxSynth.Tone(262f, 0.2f, WaveShape.Triangle, 0.55f, decayPow: 1.5f),
            SfxSynth.Tone(196f, 0.4f, WaveShape.Triangle, 0.6f, decayPow: 1.5f)), 1);

        // Keep nova / radial signatures: mid boom with a ring.
        yield return (SfxId.Nova, SfxSynth.Mix(
            SfxSynth.Sweep(500f, 130f, 0.32f, WaveShape.Sine, 0.7f),
            SfxSynth.Tone(880f, 0.18f, WaveShape.Sine, 0.18f)), 2);

        // Dash: short whoosh.
        yield return (SfxId.Dash, SfxSynth.Noise(0.11f, 2400f, 0.5f, attack: 0.02f), 2);

        // Rally horn.
        yield return (SfxId.Rally, SfxSynth.Mix(
            SfxSynth.Tone(330f, 0.3f, WaveShape.Saw, 0.4f, attack: 0.02f, decayPow: 1.8f),
            SfxSynth.Tone(334f, 0.3f, WaveShape.Saw, 0.3f, attack: 0.02f, decayPow: 1.8f)), 2);

        // Level up / skill point: bright double ping.
        yield return (SfxId.LevelUp, SfxSynth.Concat(
            SfxSynth.Tone(880f, 0.06f, WaveShape.Sine, 0.5f),
            SfxSynth.Tone(1175f, 0.1f, WaveShape.Sine, 0.55f)), 2);

        // ---- war-drum kit (adaptive music) ----
        yield return (SfxId.Kick, SfxSynth.Sweep(160f, 45f, 0.14f, WaveShape.Sine, 1f, decayPow: 2.5f), 2);
        yield return (SfxId.Tom, SfxSynth.Sweep(240f, 130f, 0.1f, WaveShape.Sine, 0.8f), 2);
        yield return (SfxId.Hat, SfxSynth.Noise(0.03f, 8000f, 0.5f), 2);
    }

    // ---- adaptive war drums ------------------------------------------------

    private static double _stepTimer;
    private static int _step;

    /// <summary>
    /// Advance the battle-drum sequencer. Intensity 0 = silence (lulls, menus,
    /// pause); above 0 an 8-step pattern plays, adding layers and pace as the
    /// threat rises. Call once per frame.
    /// </summary>
    public static void UpdateMusic(float dt, float intensity)
    {
        if (!Ready) return;
        if (intensity <= 0f) { _stepTimer = 0; _step = 0; return; }
        float stepDur = 0.34f - 0.08f * Math.Clamp(intensity, 0f, 1f); // ~88 -> ~115 bpm
        _stepTimer += dt;
        while (_stepTimer >= stepDur)
        {
            _stepTimer -= stepDur;
            foreach (var (id, vol, pitch) in PatternHits(_step, intensity))
                Play(id, vol, pitch);
            _step = (_step + 1) % 8;
        }
    }

    /// <summary>The drum hits for one sequencer step at a given intensity (pure: testable).</summary>
    public static IEnumerable<(SfxId Id, float Vol, float Pitch)> PatternHits(int step, float intensity)
    {
        if (step is 0 or 4) yield return (SfxId.Kick, 0.30f + 0.25f * intensity, 1f);
        if (intensity > 0.3f && step % 2 == 1) yield return (SfxId.Hat, 0.09f + 0.07f * intensity, 1f);
        if (intensity > 0.45f && step is 3 or 6) yield return (SfxId.Tom, 0.22f, step == 6 ? 0.85f : 1f);
        if (intensity > 0.8f && step == 2) yield return (SfxId.Kick, 0.22f, 1.2f); // urgent extra kick
    }
}
