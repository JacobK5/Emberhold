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

    // Resume-on-launch: when a checkpoint save exists, prompt before starting fresh.
    private RunSave? _pendingSave;
    private bool _resumePrompt;

    /// <param name="auto">Auto-resolve draft/placement (smoke).</param>
    /// <param name="seed">Seed debug structures and start straight in combat (smoke).</param>
    /// <param name="startWave">Debug: begin at this wave (to exercise late-game content).</param>
    /// <param name="lose">Debug: force a game-over on the first combat frame.</param>
    public GameApp(bool auto = false, bool seed = false, int startWave = 0, bool codex = false, bool lose = false, int startChapter = 0, int startHero = 0, bool paused = false, bool skills = false)
    {
        Auto = auto;
        _seed = seed;
        _startWave = startWave;
        _startChapter = startChapter;
        _startHero = startHero;
        _showCodex = codex;
        _lose = lose;
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

        // Offer to resume a saved run (skip in smoke/debug-start modes).
        bool debugStart = _seed || Auto || _lose || _startWave > 0 || _startChapter > 0;
        if (!debugStart && RunStore.TryLoad(out var save))
        {
            _pendingSave = save;
            _resumePrompt = true;
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
        _state.Hero.Kind = (HeroKind)Math.Clamp(_startHero, 0, 2);
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
        if (_resumePrompt) { HandleResumePrompt(); return; }
        if (Raylib.IsKeyPressed(KeyboardKey.R) && _state.Over) { NewRun(); return; }

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
        Renderer.Draw(_state, _draft, ShowIntro, _showCodex);
        if (_skillsOpen && !_state.Over) OverlayUI.DrawSkillTree(_state);
        if (_resumePrompt && _pendingSave is RunSave sv)
            OverlayUI.DrawResumePrompt(sv);
    }

    /// <summary>Title-screen resume choice: [Enter] continue the saved run, [N] start fresh.</summary>
    private void HandleResumePrompt()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.KpEnter))
        {
            if (_pendingSave is RunSave save) RunStore.Apply(_state, save);
            _resumePrompt = false;
            _pendingSave = null;
        }
        else if (Raylib.IsKeyPressed(KeyboardKey.N))
        {
            RunStore.Delete();
            _resumePrompt = false;
            _pendingSave = null;
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
            if (Raylib.IsKeyPressed(KeyboardKey.H)) { _state.Hero.SwitchCooldown = 0f; SwitchHero(); }
            if (Raylib.IsMouseButtonPressed(MouseButton.Left)) HandleSkillTreeClick();
            return;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.P) || Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            if (_state.Shop.Open) CloseShop();
            else _state.Paused = !_state.Paused;
        }
        if (_state.Paused) return;

        _state.BossBannerTimer = MathF.Max(0f, _state.BossBannerTimer - dt);
        _state.BannerTimer = MathF.Max(0f, _state.BannerTimer - dt);
        _state.RallyCooldown = MathF.Max(0f, _state.RallyCooldown - dt);
        _state.OverchargeTimer = MathF.Max(0f, _state.OverchargeTimer - dt);
        _state.Hero.SwitchCooldown = MathF.Max(0f, _state.Hero.SwitchCooldown - dt);

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
        EnemySystem.Update(_state, dt);
        DefenseSystem.Update(_state, dt);
        CombatSystem.UpdateProjectiles(_state, dt);
        CombatSystem.UpdatePickups(_state);
        EconomySystem.UpdateMines(_state, dt);
        EffectsSystem.Update(_state, dt);
        UpdateSupplyCache(dt);
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
        _state.Shop.PriceMult = mod.ShopPriceMult;
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
        if (Raylib.IsKeyPressed(KeyboardKey.H)) SwitchHero();
    }

    private void SwitchHero()
    {
        var hero = _state.Hero;
        if (hero.SwitchCooldown > 0f) return; // brief gate so heroes can't be juggled
        hero.Kind = hero.Kind switch
        {
            HeroKind.Ranger => HeroKind.Warden,
            HeroKind.Warden => HeroKind.Artificer,
            _ => HeroKind.Ranger,
        };
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

        if (hero.AbilityCooldown <= 0f && _state.Enemies.Count > 0) CombatSystem.ShootVolley(_state);
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
        => $"wave={_state.Wave} fort={_state.Chapter} keep={_state.KeepHealth:0}/{_state.KeepMaxHealth:0} "
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
