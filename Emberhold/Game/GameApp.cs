using System.Numerics;
using Emberhold.Core;
using Emberhold.Data;
using Emberhold.Render;
using Raylib_cs;

namespace Emberhold.Game;

/// <summary>
/// Top-level game object: owns the simulation state + draft controller, drives the
/// Draft → Placement → Combat phase machine, runs systems, and renders.
/// </summary>
public sealed class GameApp
{
    private GameState _state = null!;
    private DraftController _draft = null!;
    private Vector2? _pointerTarget;

    /// <summary>When set, the draft/placement phases auto-resolve (for smoke tests).</summary>
    public bool Auto;

    private readonly bool _seed;
    private readonly int _startWave;
    private readonly int _startChapter;
    private readonly int _startHero;
    private readonly bool _lose;
    private float _introTimer = 8f;
    private bool _showCodex;
    private bool _skillsOpen;
    private Profile _profile = Persistence.Load();
    private readonly Random _rng = new();
    private float _targetZoom = 1f;
    private const float ZoomMin = 0.5f;
    private const float ZoomMax = 2.0f;
    private const float ZoomStep = 0.12f;

    // Front-of-house screens above the in-run phase machine.
    private enum Screen { Title, HeroSelect, Playing }
    private Screen _screen = Screen.Playing;
    private HeroKind _heroChoice = HeroKind.Ranger; // hero picked on the select screen
    private int _ascensionChoice;                    // ascension tier chosen on the select screen
    private bool _ascendUnlocked;                     // this run already unlocked the next tier
    private bool _heroSwapOpen;                      // in-game (H) hero-swap overlay
    private bool _balanceOpen;                       // balancing tuner overlay (title or pause)
    private bool _balanceFromPause;                  // opened from a paused run (vs the title)
    private RunSave? _save;                          // cached checkpoint for the title's Resume

    /// <summary>Set when the player chooses Quit from the title menu; Program ends the loop.</summary>
    public bool ShouldQuit { get; private set; }

    /// <param name="auto">Auto-resolve draft/placement (smoke).</param>
    /// <param name="seed">Seed debug structures and start straight in combat (smoke).</param>
    /// <param name="startWave">Debug: begin at this wave (to exercise late-game content).</param>
    /// <param name="lose">Debug: force a game-over on the first combat frame.</param>
    public GameApp(bool auto = false, bool seed = false, int startWave = 0, bool codex = false, bool lose = false, int startChapter = 0, int startHero = 0, bool paused = false, bool skills = false, bool startAtTitle = false, bool heroSwap = false, bool balance = false, bool meteorEvent = false, bool exoticShop = false, bool swarmWave = false, bool ascendDemo = false)
    {
        _balanceOpen = balance; // debug: screenshot the balancing panel over the title/run
        Auto = auto;
        if (ascendDemo)
        {
            // Debug: hero-select with the ascension selector unlocked, for screenshots.
            _profile = _profile with { MaxAscension = Ascensions.Cap };
            _ascensionChoice = 2;
            _screen = Screen.HeroSelect;
            return;
        }
        _seed = seed;
        _startWave = startWave;
        _startChapter = startChapter;
        _startHero = startHero;
        _heroChoice = (HeroKind)Math.Clamp(startHero, 0, Enum.GetValues<HeroKind>().Length - 1);
        _showCodex = codex;
        _lose = lose;

        // A clean launch (no smoke/debug flags) opens the title menu; the run begins
        // only once the player picks New Run + a hero (or Resume).
        if (startAtTitle)
        {
            _screen = Screen.Title;
            if (RunStore.TryLoad(out var save)) _save = save;
            return;
        }

        NewRun();
        if (paused) _state.Paused = true;
        if (skills)
        {
            // Debug: open the skill tree with points + a few owned nodes to screenshot all states.
            _state.Hero.Cur.SkillPoints = 3;
            _state.Hero.Cur.Nodes.Add(Data.HeroSkills.Vitality);
            _state.Hero.Cur.Nodes.Add(Data.HeroSkills.RRicochet);
            _skillsOpen = true;
        }
        if (heroSwap) _heroSwapOpen = true; // debug: screenshot the in-game hero-swap overlay
        if (meteorEvent)
        {
            // Debug: jump into a live wave with a Meteor Storm raging for screenshots.
            _state.Wave = Math.Max(_state.Wave, 12);
            _state.NextWaveKinds = WaveSystem.BuildComposition(_state, _state.Wave);
            WaveSystem.StartWave(_state);
            _state.ActiveEvent = MapEventKind.MeteorShower;
            _state.MeteorTimer = 0.2f;
        }
        if (exoticShop)
        {
            // Debug: a deep-run supply shop (with an exotic on offer) for screenshots.
            _state.Wave = 20;
            _state.Gold = 9999;
            _state.Shop.Refresh(_state.Wave, _state.ZoneFortified, _state.OwnedKinds(), _state.Exotics);
            _state.Shop.CanOpen = true;
            _state.Shop.Open = true;
        }
        if (swarmWave)
        {
            // Debug: jump into a Swarm archetype wave for screenshots.
            _state.Wave = 12;
            for (int salt = 1; salt < 200000; salt++)
                if (WaveArchetypes.For(12, salt) == WaveArchetype.Swarm) { _state.ArchetypeSalt = salt; break; }
            _state.NextWaveKinds = WaveSystem.BuildComposition(_state, _state.Wave);
            WaveSystem.StartWave(_state);
        }
    }

    private void NewRun()
    {
        _state = new GameState(seedDebug: _seed) { BestWave = _profile.BestWave };
        _draft = new DraftController();
        _pointerTarget = null;
        _skillsOpen = false;
        if (_startChapter > 0) _state.Chapter = _startChapter;
        if (_startWave > 0) _state.Wave = _startWave;
        _state.Hero.Kind = _heroChoice;
        _state.Ascension = Ascensions.Clamp(_ascensionChoice);
        _ascendUnlocked = false;
        // Ascension scales the keep down (overwritten by a save on resume).
        _state.KeepMaxHealth *= Ascensions.KeepMult(_state.Ascension);
        _state.KeepHealth = _state.KeepMaxHealth;
        ApplyRunModifier(); // roll the run's trial before previewing the first wave
        _state.NextWaveKinds = WaveSystem.BuildComposition(_state, _state.Wave);      // wave-1 preview
        _state.NextWaveKinds2 = WaveSystem.BuildComposition(_state, _state.Wave + 1); // wave-2 foresight
        _state.CodexAdept = _profile.DiscoveredSynergies.Count >= 12; // lifetime collection perk
        if (_seed) _state.Phase = Phase.Combat;
        else _draft.StartRun(_state, _state.CodexAdept);
        if (_lose) { _state.Phase = Phase.Combat; _state.KeepHealth = 0f; }
    }

    public void Update(float dt)
    {
        if (_balanceOpen) { UpdateBalance(); return; }
        if (_screen == Screen.Title) { UpdateTitle(); return; }
        if (_screen == Screen.HeroSelect) { UpdateHeroSelect(); return; }

        if (Raylib.IsKeyPressed(KeyboardKey.R) && _state.Over) { _screen = Screen.Title; _save = null; return; }

        _state.Elapsed += dt;
        _introTimer -= dt;
        UpdateCameraOffset();
        UpdateZoom(dt);

        if (Raylib.IsKeyPressed(KeyboardKey.C)) _showCodex = !_showCodex;
        if (_showCodex)
        {
            SynergyEngine.Evaluate(_state); // refresh active set for the codex outside combat
            return;
        }

        if (_state.Over) return;

        switch (_state.Phase)
        {
            // Settle any leftover screen shake while drafting/placing so the world is still.
            case Phase.Draft: _state.Shake = 0f; UpdateDraft(dt); EaseCameraHome(dt); break;
            case Phase.Placement: _state.Shake = 0f; UpdatePlacement(); EaseCameraHome(dt); break;
            case Phase.Combat: UpdateCombat(dt); break;
        }
    }

    public bool ShowIntro => _introTimer > 0f;

    public void Draw()
    {
        if (_screen == Screen.Title)
        {
            MenuUI.DrawTitle(_save is not null, _save, Program.Version);
        }
        else if (_screen == Screen.HeroSelect)
        {
            Raylib.ClearBackground(Palette.Grass);
            MenuUI.DrawHeroSelect("CHOOSE YOUR HERO", "click a hero to begin   ·   ESC back to menu",
                ascension: _ascensionChoice, maxAscension: _profile.MaxAscension);
        }
        else
        {
            Renderer.Draw(_state, _draft, ShowIntro, _showCodex);
            if (_skillsOpen && !_state.Over) OverlayUI.DrawSkillTree(_state);
            if (_heroSwapOpen && !_state.Over)
                MenuUI.DrawHeroSelect("SWITCH HERO", "click a hero to switch   ·   ESC / H cancel",
                    _state.Hero.Kind, _state.Hero.SwitchCooldown);
        }

        if (_balanceOpen) OverlayUI.DrawBalancePanel(_balanceFromPause);
    }

    // ---- title / hero-select screens ------------------------------------

    private void UpdateTitle()
    {
        if (!Raylib.IsMouseButtonPressed(MouseButton.Left)) return;
        var items = MenuUI.TitleItems(_save is not null);
        var rects = MenuUI.TitleItemRects(items.Count);
        var m = Raylib.GetMousePosition();
        for (int i = 0; i < items.Count; i++)
        {
            if (!Raylib.CheckCollisionPointRec(m, rects[i])) continue;
            DoMenuAction(items[i].Action);
            break;
        }
    }

    private void DoMenuAction(MenuAction action)
    {
        switch (action)
        {
            case MenuAction.Resume:
                NewRun();
                if (_save is RunSave sv) RunStore.Apply(_state, sv);
                _introTimer = 0f; // resuming mid-run; skip the opening hint crawl
                _screen = Screen.Playing;
                break;
            case MenuAction.NewRun:
                _ascensionChoice = _profile.MaxAscension; // default to your highest unlocked tier
                _screen = Screen.HeroSelect;
                break;
            case MenuAction.Settings:
                _balanceOpen = true;
                _balanceFromPause = false;
                break;
            case MenuAction.Quit:
                ShouldQuit = true;
                break;
        }
    }

    /// <summary>Input for the balancing tuner overlay: nudge values, reset, clipboard, close.</summary>
    private void UpdateBalance()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape)) { _balanceOpen = false; return; }
        if (!Raylib.IsMouseButtonPressed(MouseButton.Left)) return;
        var m = Raylib.GetMousePosition();

        var adj = OverlayUI.BalanceAdjustRects();
        for (int i = 0; i < adj.Length; i++)
        {
            if (Raylib.CheckCollisionPointRec(m, adj[i].Minus)) { BalanceConfig.Adjust(BalanceConfig.Entries[i], -1); return; }
            if (Raylib.CheckCollisionPointRec(m, adj[i].Plus)) { BalanceConfig.Adjust(BalanceConfig.Entries[i], +1); return; }
        }

        var act = OverlayUI.BalanceActionRects();
        if (Raylib.CheckCollisionPointRec(m, act[0])) { BalanceConfig.Reset(); return; }
        if (Raylib.CheckCollisionPointRec(m, act[1])) { Raylib.SetClipboardText(BalanceConfig.Export()); return; }
        if (Raylib.CheckCollisionPointRec(m, act[2])) { BalanceConfig.Import(Raylib.GetClipboardText_()); return; }
        if (Raylib.CheckCollisionPointRec(m, act[3])) { _balanceOpen = false; return; }
    }

    private void UpdateHeroSelect()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape)) { _screen = Screen.Title; return; }
        if (!Raylib.IsMouseButtonPressed(MouseButton.Left)) return;
        var m = Raylib.GetMousePosition();

        // Ascension tier selector (only once a tier is unlocked).
        if (_profile.MaxAscension > 0)
        {
            var (minus, plus) = MenuUI.AscensionButtonRects();
            if (Raylib.CheckCollisionPointRec(m, minus)) { _ascensionChoice = Math.Max(0, _ascensionChoice - 1); return; }
            if (Raylib.CheckCollisionPointRec(m, plus)) { _ascensionChoice = Math.Min(_profile.MaxAscension, _ascensionChoice + 1); return; }
        }

        var rects = MenuUI.HeroCardRects();
        for (int i = 0; i < rects.Length; i++)
        {
            if (!Raylib.CheckCollisionPointRec(m, rects[i])) continue;
            _heroChoice = Data.HeroProfile.All[i].Kind;
            RunStore.Delete();   // beginning a fresh run; drop any stale checkpoint
            _save = null;
            NewRun();
            _introTimer = 8f;
            _screen = Screen.Playing;
            break;
        }
    }

    // ---- phases ---------------------------------------------------------

    private void UpdateDraft(float dt)
    {
        _state.DraftReadyTimer = MathF.Max(0f, _state.DraftReadyTimer - dt);

        if (Raylib.IsKeyPressed(KeyboardKey.V)) _state.ViewingBase = !_state.ViewingBase;
        if (_state.ViewingBase) return;
        if (_state.DraftReadyTimer > 0f) return;

        if (Auto) { _draft.AutoAdvance(_state); return; }

        // Bank this draft (X) for a double-pick next time — once per run.
        if (Raylib.IsKeyPressed(KeyboardKey.X) && _draft.Veto(_state)) { _state.ViewingBase = false; return; }

        int picked = -1;
        if (Raylib.IsKeyPressed(KeyboardKey.One)) picked = 0;
        else if (Raylib.IsKeyPressed(KeyboardKey.Two)) picked = 1;
        else if (Raylib.IsKeyPressed(KeyboardKey.Three)) picked = 2;
        else if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            var rects = OverlayUI.DraftCardRects();
            var m = Raylib.GetMousePosition();
            for (int i = 0; i < rects.Length; i++)
                if (Raylib.CheckCollisionPointRec(m, rects[i])) { picked = i; break; }
        }

        if (picked >= 0) { _draft.Pick(_state, picked); _state.ViewingBase = false; }
    }

    private void UpdatePlacement()
    {
        if (Auto) { _draft.AutoAdvance(_state); return; }

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            var world = Raylib.GetScreenToWorld2D(Raylib.GetMousePosition(), _state.Cam);
            _draft.TryPlace(_state, world);
        }
    }

    private void UpdateCombat(float dt)
    {
        // Skill tree overlay — toggles with K, freezes the sim while open (like the shop).
        if (Raylib.IsKeyPressed(KeyboardKey.K) && !_state.Shop.Open && !_state.PendingDraft)
            _skillsOpen = !_skillsOpen;
        if (_skillsOpen)
        {
            if (Raylib.IsKeyPressed(KeyboardKey.Escape)) { _skillsOpen = false; return; }
            if (Raylib.IsKeyPressed(KeyboardKey.H)) { _skillsOpen = false; _heroSwapOpen = true; return; }
            if (Raylib.IsMouseButtonPressed(MouseButton.Left)) HandleSkillTreeClick();
            return;
        }

        // Hero-swap overlay — opens with H, freezes the sim like the skill tree.
        if (_heroSwapOpen)
        {
            if (Raylib.IsKeyPressed(KeyboardKey.Escape) || Raylib.IsKeyPressed(KeyboardKey.H)) { _heroSwapOpen = false; return; }
            if (Raylib.IsMouseButtonPressed(MouseButton.Left)) HandleHeroSwapClick();
            return;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.P) || Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            if (_state.Shop.Open) CloseShop();
            else _state.Paused = !_state.Paused;
        }
        // While paused, B opens the balancing tuner (returns here on close).
        if (_state.Paused && Raylib.IsKeyPressed(KeyboardKey.B))
        {
            _balanceOpen = true; _balanceFromPause = true; return;
        }
        if (_state.Paused) return;

        _state.BossBannerTimer = MathF.Max(0f, _state.BossBannerTimer - dt);
        _state.BannerTimer = MathF.Max(0f, _state.BannerTimer - dt);
        _state.RallyCooldown = MathF.Max(0f, _state.RallyCooldown - dt);
        _state.OverchargeTimer = MathF.Max(0f, _state.OverchargeTimer - dt);
        _state.Hero.SwitchCooldown = MathF.Max(0f, _state.Hero.SwitchCooldown - dt);
        _state.Hero.StanceTimer = MathF.Max(0f, _state.Hero.StanceTimer - dt);

        // Shop toggle — available during the between-wave countdown. (B, not S — S is move-down.)
        if (Raylib.IsKeyPressed(KeyboardKey.B) && _state.Shop.CanOpen && !_state.PendingDraft)
        {
            if (_state.Shop.Open) CloseShop();
            else _state.Shop.Open = true;
        }

        // While shop is open only process mouse clicks for purchases.
        if (_state.Shop.Open) { HandleShopInput(); return; }

        HandleAbilityInput();

        WaveSystem.Update(_state, dt);

        if (_state.PendingDraft)
        {
            TriggerDraft();
            return;
        }

        SynergyEngine.Evaluate(_state);
        _state.UpdateStreak(dt);
        _state.UpdatePopups(dt);
        if (Auto) AutoHeroMove(dt); else UpdateHeroMovement(dt);
        EconomySystem.UpdateBuilding(_state, dt);
        EconomySystem.UpdateUpgrades(_state, dt);
        CombatSystem.UpdateHeroCombat(_state, dt);
        TowerSystem.Update(_state, dt);
        CompanionSystem.Update(_state, dt);
        EnemySystem.Update(_state, dt);
        DefenseSystem.Update(_state, dt);
        CombatSystem.UpdateProjectiles(_state, dt);
        CombatSystem.UpdatePickups(_state);
        EconomySystem.UpdateMines(_state, dt);
        EffectsSystem.Update(_state, dt);
        UpdateSupplyCache(dt);
        MapEventSystem.Update(_state, dt);
        UpdateExotics(dt);
        UpdateCamera(dt);

        if (_state.KeepHealth <= 0f || _state.Hero.Health <= 0f)
        {
            _state.Over = true;
            RunStore.Delete(); // the run is finished; don't resume a dead keep
            _profile = Persistence.Record(_profile, _state.Wave, _state.Kills, _state.BossKills, _state.SeenSynergies);
            _state.BestWave = _profile.BestWave;
            _state.Profile = _profile;
            return;
        }

        // Ascension unlock: surviving to clear wave 10 (a boss) at your current ceiling
        // opens the next tier for future runs.
        if (!_ascendUnlocked && _state.Wave >= 11 && _state.Ascension >= _profile.MaxAscension
            && _profile.MaxAscension < Ascensions.Cap)
        {
            _ascendUnlocked = true;
            _profile = _profile with { MaxAscension = _profile.MaxAscension + 1 };
            Persistence.Save(_profile);
            _state.BannerText = $"ASCENSION {_profile.MaxAscension} UNLOCKED";
            _state.BannerTimer = 3.2f;
        }

        // Checkpoint once the post-wave lull is stable (after any draft/placement resolves).
        if (_state.NeedsAutosave && _state.Spawning is null && _state.BetweenWaves > 0f
            && !_state.PendingDraft && !_state.Shop.Open && _state.Enemies.TrueForAll(e => e.Dead))
        {
            RunStore.Save(RunStore.Capture(_state));
            _state.NeedsAutosave = false;
        }
    }

    /// <summary>Periodically drop a high-value supply cache out on a lane during a fight.</summary>
    private void UpdateSupplyCache(float dt)
    {
        bool fighting = _state.Spawning is not null || _state.Enemies.Exists(e => !e.Dead);
        if (!fighting) return;
        _state.CacheTimer -= dt;
        if (_state.CacheTimer > 0f) return;

        _state.CacheTimer = _state.Rand(20f, 30f);
        int side = (int)(_state.Rand() * 4f) & 3;
        var pos = Vector2.Lerp(Map.Gate(side, _state.Chapter), Map.SpawnPoint(side, _state.Chapter), 0.6f);
        _state.SpawnCache(pos, 14 + _state.Wave * 2);
        _state.BannerText = "SUPPLY CACHE";
        _state.BannerTimer = 2.2f;
        _state.AddFloater(pos + new Vector2(0, -18), "SUPPLY", Palette.Gold);
    }

    /// <summary>Per-frame exotic effects: keep regen (Aegis) and the one-shot hero revive (Phoenix).</summary>
    private void UpdateExotics(float dt)
    {
        // Aegis Matrix: the keep slowly mends itself.
        if (_state.HasExotic(ExoticKind.AegisMatrix) && _state.KeepHealth > 0f && _state.KeepHealth < _state.KeepMaxHealth)
            _state.KeepHealth = MathF.Min(_state.KeepMaxHealth, _state.KeepHealth + 3f * dt);

        // Phoenix Heart: cheat death once per run (only a hero death, not a keep breach).
        var hero = _state.Hero;
        if (_state.HasExotic(ExoticKind.PhoenixHeart) && !_state.PhoenixUsed
            && hero.Health <= 0f && _state.KeepHealth > 0f)
        {
            _state.PhoenixUsed = true;
            hero.Health = hero.MaxHealth * 0.5f;
            hero.Invulnerable = MathF.Max(hero.Invulnerable, 2f);
            _state.AddParticles(hero.Pos, Palette.Hex("ffb064"), 30, 120f);
            _state.AddFloater(hero.Pos + new Vector2(0, -36), "PHOENIX REVIVES!", Palette.Hex("ffd66b"));
            _state.BannerText = "THE PHOENIX RISES";
            _state.BannerTimer = 2.6f;
            _state.KickShake(12f);
        }
    }

    /// <summary>Roll the run's trial modifier and apply its start-of-run effects.</summary>
    private void ApplyRunModifier()
    {
        var mod = RunModifier.Roll(_rng);
        _state.Modifier = mod;
        var hero = _state.Hero;

        if (mod.HeroMaxHpMult != 1f)
        {
            hero.MaxHealth *= mod.HeroMaxHpMult;
            hero.Health = hero.MaxHealth;
        }
        if (mod.StartHeroLevel > 1)
        {
            int steps = mod.StartHeroLevel - 1;
            hero.Level = mod.StartHeroLevel;
            hero.Damage += 1.5f * steps;
            hero.MaxHealth += 6f * steps;
            hero.Health = hero.MaxHealth;
            hero.FireRate = MathF.Max(0.22f, hero.FireRate - 0.012f * steps);
        }
        _state.Shop.PriceMult = mod.ShopPriceMult * Ascensions.PriceMult(_state.Ascension);
    }

    /// <summary>Milestone wave cleared: open a card draft (no auto-expansion).</summary>
    private void TriggerDraft()
    {
        _state.PendingDraft = false;
        _state.DraftReadyTimer = 0.75f;
        _state.ViewingBase = false;
        _draft.StartDraft(_state);
    }

    // ---- Shop logic -------------------------------------------------------

    private void HandleShopInput()
    {
        if (!Raylib.IsMouseButtonPressed(MouseButton.Left)) return;
        var mouse = Raylib.GetMousePosition();
        var rects = OverlayUI.ShopItemRects(_state);
        for (int i = 0; i < rects.Length && i < _state.Shop.Items.Count; i++)
        {
            if (!Raylib.CheckCollisionPointRec(mouse, rects[i])) continue;
            var item = _state.Shop.Items[i];
            if (item.Purchased) break;
            TryBuyShopItem(item);
            break;
        }
    }

    private void TryBuyShopItem(ShopItem item)
    {
        var shop = _state.Shop;
        var hero = _state.Hero;
        int cost;

        switch (item.Kind)
        {
            case ShopItemKind.Expansion:
                cost = shop.ExpansionCost(_state.Chapter);
                if (_state.Gold < cost) return;
                _state.Gold -= cost;
                _state.Chapter += 1;
                _state.KeepMaxHealth += 80f;
                _state.KeepHealth = MathF.Min(_state.KeepMaxHealth, _state.KeepHealth + 80f);
                _state.AddFloater(Vector2.Zero, "FORT EXPANDED", Palette.Hex("efd18a"));
                _state.KickShake(8f);
                item.Purchased = true;
                shop.OnPurchase();
                return;

            case ShopItemKind.HeroUpgrade:
                cost = shop.HeroUpgradeCost(item.UpgradeKind);
                if (_state.Gold < cost) return;
                _state.Gold -= cost;
                ApplyHeroUpgrade(hero, item.UpgradeKind);
                shop.HeroTiers[(int)item.UpgradeKind]++;
                _state.AddFloater(hero.Pos + new Vector2(0, -30),
                    ShopState.UpgradeName(item.UpgradeKind), Palette.Hex("bfe0ff"));
                item.Purchased = true;
                shop.OnPurchase();
                return;

            case ShopItemKind.ZoneUpgrade:
                cost = shop.ZoneCost(_state.Chapter);
                if (_state.Gold < cost) return;
                _state.Gold -= cost;
                _state.ZoneFortified[item.Zone] = true;
                _state.AddFloater(Vector2.Zero, $"{GameState.ZoneName(item.Zone)} FORTIFIED", Palette.Hex("efd18a"));
                _state.KickShake(6f);
                item.Purchased = true;
                shop.OnPurchase();
                return;

            case ShopItemKind.StructureCard:
                if (item.Card is null) return;
                cost = shop.CardCost;
                if (_state.Gold < cost) return;
                _state.Gold -= cost;
                // The shop price buys the card; you still place and fund its build
                // (and any upgrades) with gold, like a drafted card.
                _draft.ToPlace.Enqueue(item.Card);
                _state.AddFloater(hero.Pos, item.Card.Name, Palette.Gold);
                item.Purchased = true;
                shop.OnPurchase();
                return;

            case ShopItemKind.Exotic:
                cost = shop.ExoticCost(item.Exotic);
                if (_state.Gold < cost) return;
                _state.Gold -= cost;
                _state.Exotics.Add(item.Exotic); // run-wide passive, read live by the systems
                _state.AddFloater(hero.Pos + new Vector2(0, -32), ShopState.ExoticName(item.Exotic), Palette.Hex("ffd66b"));
                _state.AddParticles(hero.Pos, Palette.Hex("ffd66b"), 20, 90f);
                _state.KickShake(7f);
                item.Purchased = true;
                shop.OnPurchase();
                return;
        }
    }

    private static void ApplyHeroUpgrade(Hero hero, HeroUpgradeKind kind)
    {
        // Shop upgrades are run-wide: apply to every hero kind (counters stay in sync).
        switch (kind)
        {
            case HeroUpgradeKind.Damage:
                hero.ApplyToAll(p => { p.Damage += 7f; p.DmgUpgrades++; });
                break;
            case HeroUpgradeKind.FireRate:
                hero.ApplyToAll(p => { p.FireRate = MathF.Max(0.22f, p.FireRate * 0.82f); p.FrUpgrades++; });
                break;
            case HeroUpgradeKind.Range:
                hero.ApplyToAll(p => { p.Range += 30f; p.RngUpgrades++; });
                break;
            case HeroUpgradeKind.Health:
                hero.ApplyToAll(p => { p.MaxHealth += 25f; p.Health = MathF.Min(p.MaxHealth, p.Health + 25f); p.HpUpgrades++; });
                break;
            case HeroUpgradeKind.Volley:
                hero.ApplyToAll(p => { p.VolleyCooldown = MathF.Max(3.5f, p.VolleyCooldown - 1.5f); p.VolleyUpgrades++; });
                break;
        }
    }

    private void CloseShop()
    {
        _state.Shop.Open = false;
        // If any structure cards were purchased, start placement immediately.
        if (_draft.ToPlace.Count > 0)
            _draft.StartPlacements(_state);
    }

    // ---- input / movement / camera -------------------------------------

    private void HandleAbilityInput()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Space)) CombatSystem.Signature(_state);
        if (Raylib.IsKeyPressed(KeyboardKey.LeftShift) || Raylib.IsKeyPressed(KeyboardKey.RightShift))
            CombatSystem.Dash(_state);
        if (Raylib.IsKeyPressed(KeyboardKey.F)) _state.TryRally();
        if (Raylib.IsKeyPressed(KeyboardKey.H)) _heroSwapOpen = true;
    }

    /// <summary>Pick the hero card under the cursor in the in-game swap overlay.</summary>
    private void HandleHeroSwapClick()
    {
        var rects = MenuUI.HeroCardRects();
        var m = Raylib.GetMousePosition();
        for (int i = 0; i < rects.Length; i++)
        {
            if (!Raylib.CheckCollisionPointRec(m, rects[i])) continue;
            var kind = Data.HeroProfile.All[i].Kind;
            if (kind != _state.Hero.Kind) SwitchHeroTo(kind);
            _heroSwapOpen = false;
            break;
        }
    }

    /// <summary>Switch to a specific hero kind, gated by the brief switch cooldown.</summary>
    private void SwitchHeroTo(HeroKind kind)
    {
        var hero = _state.Hero;
        if (hero.SwitchCooldown > 0f) return; // brief gate so heroes can't be juggled
        hero.Kind = kind;
        // A swapped-in hero starts its ability ready; reset transient combat timers.
        hero.SwitchCooldown = 4f;
        hero.ShotTimer = 0f;
        hero.Invulnerable = MathF.Max(hero.Invulnerable, 0.3f);
        _state.AddParticles(hero.Pos, Palette.Hex("f3c878"), 16, 72f);
        _state.AddFloater(hero.Pos + new Vector2(0, -34), hero.Profile.Name, Palette.Hex("efd18a"));
    }

    /// <summary>Spend a skill point on a clicked node in the skill-tree overlay.</summary>
    private void HandleSkillTreeClick()
    {
        var mouse = Raylib.GetMousePosition();
        foreach (var (node, rect) in OverlayUI.SkillNodeRects(_state))
        {
            if (!Raylib.CheckCollisionPointRec(mouse, rect)) continue;
            if (_state.Hero.Unlock(node))
            {
                _state.AddFloater(_state.Hero.Pos + new Vector2(0, -30), node.Name, Palette.Hex("efd18a"));
                _state.AddParticles(_state.Hero.Pos, Palette.Hex("bfe0ff"), 14, 70f);
            }
            break;
        }
    }

    private void UpdateCameraOffset()
        => _state.Cam.Offset = new Vector2(Raylib.GetScreenWidth() / 2f, Raylib.GetScreenHeight() / 2f);

    private void UpdateZoom(float dt)
    {
        float wheel = Raylib.GetMouseWheelMove();
        if (wheel != 0f)
            _targetZoom = MathUtils.Clamp(_targetZoom + wheel * ZoomStep, ZoomMin, ZoomMax);
        _state.Cam.Zoom += (_targetZoom - _state.Cam.Zoom) * MathF.Min(1f, dt * 9f);
    }

    private void EaseCameraHome(float dt)
        => _state.Cam.Target += (Vector2.Zero - _state.Cam.Target) * MathF.Min(1f, dt * 4.5f);

    private void UpdateHeroMovement(float dt)
    {
        var hero = _state.Hero;

        float mx = 0, my = 0;
        if (Raylib.IsKeyDown(KeyboardKey.W) || Raylib.IsKeyDown(KeyboardKey.Up)) my -= 1;
        if (Raylib.IsKeyDown(KeyboardKey.S) || Raylib.IsKeyDown(KeyboardKey.Down)) my += 1;
        if (Raylib.IsKeyDown(KeyboardKey.A) || Raylib.IsKeyDown(KeyboardKey.Left)) mx -= 1;
        if (Raylib.IsKeyDown(KeyboardKey.D) || Raylib.IsKeyDown(KeyboardKey.Right)) mx += 1;

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
            _pointerTarget = Raylib.GetScreenToWorld2D(Raylib.GetMousePosition(), _state.Cam);

        var movement = MathUtils.Normalize(mx, my);
        var desired = movement;
        if (movement != Vector2.Zero)
            _pointerTarget = null;
        else if (_pointerTarget is Vector2 target)
        {
            desired = MathUtils.Normalize(target - hero.Pos);
            if (Vector2.Distance(hero.Pos, target) < 6f) _pointerTarget = null;
        }

        MoveHero(desired, dt);
    }

    /// <summary>Simple bot for smoke/balance runs: fund pads, collect gold, fight.</summary>
    private void AutoHeroMove(float dt)
    {
        var hero = _state.Hero;
        Vector2? target = null;

        if (_state.Gold > 0 && _state.Pads.Count > 0)
            target = MathUtils.Nearest(hero.Pos, _state.Pads, p => p.Pos)?.Pos;
        if (target is null && _state.Gold >= 40)
            target = MathUtils.Nearest(hero.Pos, _state.Structures, st => st.Pos, st => st.Upgradable)?.Pos;
        if (target is null)
        {
            // Grab any uncollected loot — relics/embers included so smoke runs exercise them.
            var drop = MathUtils.Nearest(hero.Pos, _state.Drops, d => d.Pos, d => !d.Collected);
            target = drop?.Pos;
        }

        var desired = target is Vector2 p ? MathUtils.Normalize(p - hero.Pos) : Vector2.Zero;
        MoveHero(desired, dt);

        if (hero.AbilityCooldown <= 0f && _state.Enemies.Count > 0) CombatSystem.Signature(_state);
        if (_state.RallyCooldown <= 0f && _state.Gold >= _state.RallyCost && _state.Enemies.Count >= 6)
            _state.TryRally();
    }

    private void MoveHero(Vector2 desired, float dt)
    {
        var hero = _state.Hero;
        var profile = hero.Profile;

        if (desired != Vector2.Zero)
        {
            hero.Facing = desired;
            var delta = desired * hero.Speed * profile.Speed * Balance.HeroSpeedMult * dt;
            hero.Pos = Geometry.MoveWithCollisions(hero.Pos, hero.Radius, delta, _state.SolidRects());
        }

        float roam = _state.RoamLimit;
        hero.Pos = new Vector2(MathUtils.Clamp(hero.Pos.X, -roam, roam), MathUtils.Clamp(hero.Pos.Y, -roam, roam));
        hero.Pos = Geometry.ResolveCircleRects(hero.Pos, hero.Radius, _state.SolidRects());

        hero.Invulnerable = MathF.Max(0, hero.Invulnerable - dt);
        hero.AbilityCooldown = MathF.Max(0, hero.AbilityCooldown - dt);
        hero.DashCooldown = MathF.Max(0, hero.DashCooldown - dt);
        hero.Overdrive = MathF.Max(0, hero.Overdrive - dt);

        // Second Wind passive (lv7): slow health regen.
        if (hero.SecondWind && hero.Health > 0f && hero.Health < hero.MaxHealth)
            hero.Health = MathF.Min(hero.MaxHealth, hero.Health + 4.5f * dt);
    }

    public string Report()
        => _state is null ? "screen=menu"
         : $"wave={_state.Wave} fort={_state.Chapter} keep={_state.KeepHealth:0}/{_state.KeepMaxHealth:0} "
         + $"gold={_state.Gold} structures={_state.Structures.Count} pads={_state.Pads.Count} "
         + $"kills={_state.Kills} synergies={_state.SeenSynergies.Count} over={_state.Over} heroLv={_state.Hero.Level} "
         + $"enemies={_state.Enemies.Count} relics={_state.Hero.Relics.Count}";

    private void UpdateCamera(float dt)
    {
        var hero = _state.Hero;
        float screenMin = MathF.Min(Raylib.GetScreenWidth(), Raylib.GetScreenHeight());
        float deadzone = MathUtils.Clamp(screenMin * 0.22f, 88f, 150f);
        float limit = MathF.Max(0f, _state.RoamLimit - deadzone);

        float TargetAxis(float h)
            => MathUtils.Clamp(MathF.Abs(h) > deadzone ? h - MathF.Sign(h) * deadzone : 0f, -limit, limit);

        var target = new Vector2(TargetAxis(hero.Pos.X), TargetAxis(hero.Pos.Y));
        _state.Cam.Target += (target - _state.Cam.Target) * MathF.Min(1f, dt * 4.5f);
    }
}
