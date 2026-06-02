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
    private readonly bool _lose;
    private float _introTimer = 8f;
    private bool _showCodex;
    private Profile _profile = Persistence.Load();
    private readonly Random _rng = new();

    /// <param name="auto">Auto-resolve draft/placement (smoke).</param>
    /// <param name="seed">Seed debug structures and start straight in combat (smoke).</param>
    /// <param name="startWave">Debug: begin at this wave (to exercise late-game content).</param>
    /// <param name="lose">Debug: force a game-over on the first combat frame.</param>
    public GameApp(bool auto = false, bool seed = false, int startWave = 0, bool codex = false, bool lose = false, int startChapter = 0)
    {
        Auto = auto;
        _seed = seed;
        _startWave = startWave;
        _startChapter = startChapter;
        _showCodex = codex;
        _lose = lose;
        NewRun();
    }

    private void NewRun()
    {
        _state = new GameState(seedDebug: _seed) { BestWave = _profile.BestWave };
        _draft = new DraftController();
        _pointerTarget = null;
        if (_startChapter > 0) _state.Chapter = _startChapter;
        if (_startWave > 0) _state.Wave = _startWave;
        ApplyRunModifier(); // roll the run's trial before previewing the first wave
        _state.NextWaveKinds = WaveSystem.BuildComposition(_state, _state.Wave); // seed the wave-1 preview
        if (_seed) _state.Phase = Phase.Combat;
        else _draft.StartRun(_state);
        if (_lose) { _state.Phase = Phase.Combat; _state.KeepHealth = 0f; }
    }

    public void Update(float dt)
    {
        if (Raylib.IsKeyPressed(KeyboardKey.R) && _state.Over) { NewRun(); return; }

        _state.Elapsed += dt;
        _introTimer -= dt;
        UpdateCameraOffset();

        if (Raylib.IsKeyPressed(KeyboardKey.C)) _showCodex = !_showCodex;
        if (_showCodex)
        {
            SynergyEngine.Evaluate(_state); // refresh active set for the codex outside combat
            return;
        }

        if (_state.Over) return;

        switch (_state.Phase)
        {
            case Phase.Draft: UpdateDraft(); EaseCameraHome(dt); break;
            case Phase.Placement: UpdatePlacement(); EaseCameraHome(dt); break;
            case Phase.Combat: UpdateCombat(dt); break;
        }
    }

    public bool ShowIntro => _introTimer > 0f;

    public void Draw() => Renderer.Draw(_state, _draft, ShowIntro, _showCodex);

    // ---- phases ---------------------------------------------------------

    private void UpdateDraft()
    {
        if (Auto) { _draft.AutoAdvance(_state); return; }

        if (Raylib.IsKeyPressed(KeyboardKey.One)) _draft.Pick(_state, 0);
        else if (Raylib.IsKeyPressed(KeyboardKey.Two)) _draft.Pick(_state, 1);
        else if (Raylib.IsKeyPressed(KeyboardKey.Three)) _draft.Pick(_state, 2);
        else if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            var rects = OverlayUI.DraftCardRects();
            var m = Raylib.GetMousePosition();
            for (int i = 0; i < rects.Length; i++)
                if (Raylib.CheckCollisionPointRec(m, rects[i])) { _draft.Pick(_state, i); break; }
        }
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
        if (Raylib.IsKeyPressed(KeyboardKey.P) || Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            if (_state.Shop.Open) CloseShop();
            else _state.Paused = !_state.Paused;
        }
        if (_state.Paused) return;

        _state.BossBannerTimer = MathF.Max(0f, _state.BossBannerTimer - dt);
        _state.BannerTimer = MathF.Max(0f, _state.BannerTimer - dt);
        _state.RallyCooldown = MathF.Max(0f, _state.RallyCooldown - dt);

        // Shop toggle — available during the between-wave countdown.
        if (Raylib.IsKeyPressed(KeyboardKey.S) && _state.Shop.CanOpen && !_state.PendingDraft)
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
        UpdateCamera(dt);

        if (_state.KeepHealth <= 0f || _state.Hero.Health <= 0f)
        {
            _state.Over = true;
            _profile = Persistence.Record(_profile, _state.Wave, _state.Kills, _state.BossKills, _state.SeenSynergies);
            _state.BestWave = _profile.BestWave;
            _state.Profile = _profile;
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
        _state.Shop.PriceMult = mod.ShopPriceMult;
    }

    /// <summary>Milestone wave cleared: open a card draft (no auto-expansion).</summary>
    private void TriggerDraft()
    {
        _state.PendingDraft = false;
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
                // Queue as a pre-funded pad (cost 0 so it builds instantly on placement).
                var freeDef = new CardDef(
                    item.Card.Id, item.Card.Name, item.Card.Short,
                    item.Card.Category, item.Card.Kind, item.Card.Tags, Cost: 0);
                _draft.ToPlace.Enqueue(freeDef);
                _state.AddFloater(hero.Pos, item.Card.Name, Palette.Gold);
                item.Purchased = true;
                shop.OnPurchase();
                return;
        }
    }

    private static void ApplyHeroUpgrade(Hero hero, HeroUpgradeKind kind)
    {
        switch (kind)
        {
            case HeroUpgradeKind.Damage:
                hero.Damage += 7f;
                hero.DmgUpgrades++;
                break;
            case HeroUpgradeKind.FireRate:
                hero.FireRate = MathF.Max(0.22f, hero.FireRate * 0.82f);
                hero.FrUpgrades++;
                break;
            case HeroUpgradeKind.Range:
                hero.Range += 30f;
                hero.RngUpgrades++;
                break;
            case HeroUpgradeKind.Health:
                hero.MaxHealth += 25f;
                hero.Health = MathF.Min(hero.MaxHealth, hero.Health + 25f);
                hero.HpUpgrades++;
                break;
            case HeroUpgradeKind.Volley:
                hero.VolleyCooldown = MathF.Max(3.5f, hero.VolleyCooldown - 1.5f);
                hero.VolleyUpgrades++;
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
        if (Raylib.IsKeyPressed(KeyboardKey.Space)) CombatSystem.ShootVolley(_state);
        if (Raylib.IsKeyPressed(KeyboardKey.LeftShift) || Raylib.IsKeyPressed(KeyboardKey.RightShift))
            CombatSystem.Dash(_state);
        if (Raylib.IsKeyPressed(KeyboardKey.F)) _state.TryRally();
        if (Raylib.IsKeyPressed(KeyboardKey.H)) SwitchHero();
    }

    private void SwitchHero()
    {
        _state.Hero.Kind = _state.Hero.Kind == HeroKind.Ranger ? HeroKind.Warden : HeroKind.Ranger;
        _state.AddParticles(_state.Hero.Pos, Palette.Hex("f3c878"), 14, 68f);
    }

    private void UpdateCameraOffset()
        => _state.Cam.Offset = new Vector2(Raylib.GetScreenWidth() / 2f, Raylib.GetScreenHeight() / 2f);

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
