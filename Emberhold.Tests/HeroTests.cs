using System.Numerics;
using Emberhold.Data;
using Emberhold.Game;
using Xunit;

namespace Emberhold.Tests;

/// <summary>Hero progression: per-kind levels, skill trees, signatures, relic drops.</summary>
public class HeroTests
{
    [Fact]
    public void Passives_ComeFromSkillNodes_NotLevels()
    {
        var h = new Hero();
        Assert.False(h.QuickHands);
        Assert.False(h.SecondWind);
        h.Cur.Nodes.Add(HeroSkills.QuickHands);
        h.Cur.Nodes.Add(HeroSkills.SecondWind);
        Assert.True(h.QuickHands);
        Assert.True(h.SecondWind);
    }

    [Fact]
    public void QuickHands_Node_WidensPickupRadius()
    {
        var h = new Hero();
        float before = h.PickupRadius;
        h.Cur.Nodes.Add(HeroSkills.QuickHands);
        Assert.True(h.PickupRadius > before);
    }

    [Fact]
    public void Progression_IsIndependentPerKind()
    {
        var h = new Hero();                 // active = Ranger
        h.Level = 5;
        h.Cur.Nodes.Add(HeroSkills.RRicochet);
        Assert.Equal(5, h.Progress[HeroKind.Ranger].Level);
        Assert.Equal(1, h.Progress[HeroKind.Warden].Level);   // untouched

        h.Kind = HeroKind.Warden;
        Assert.Equal(1, h.Level);            // delegate now reads Warden
        Assert.False(h.Has(HeroSkills.RRicochet));
    }

    [Fact]
    public void LevelUp_GrantsSkillPoint()
    {
        var s = new GameState(seedDebug: false);
        int before = s.Hero.Cur.SkillPoints;
        CombatSystem.GrantHeroXp(s, s.Hero.NextXp + 100); // force at least one level
        Assert.True(s.Hero.Level > 1);
        Assert.True(s.Hero.Cur.SkillPoints > before);
    }

    [Fact]
    public void Unlock_RespectsPoints_AndPrerequisites()
    {
        var h = new Hero();
        var pierce = HeroSkills.Find(HeroKind.Ranger, HeroSkills.RPierce)!;
        var ricochet = HeroSkills.Find(HeroKind.Ranger, HeroSkills.RRicochet)!;

        Assert.False(h.CanUnlock(ricochet));     // no points yet
        h.Cur.SkillPoints = 2;
        Assert.False(h.CanUnlock(pierce));        // prerequisite (ricochet) not owned
        Assert.True(h.Unlock(ricochet));
        Assert.Equal(1, h.Cur.SkillPoints);       // spent one
        Assert.True(h.CanUnlock(pierce));         // prereq now satisfied
        Assert.True(h.Unlock(pierce));
        Assert.True(h.Has(HeroSkills.RPierce));
    }

    [Fact]
    public void Vitality_Node_GrantsMaxHealthOnUnlock()
    {
        var h = new Hero { Level = 1 };
        h.Cur.SkillPoints = 1;
        float before = h.MaxHealth;
        h.Unlock(HeroSkills.Find(HeroKind.Ranger, HeroSkills.Vitality)!);
        Assert.True(h.MaxHealth > before);
    }

    [Fact]
    public void Toughness_Node_ReducesDamageTaken()
    {
        var h = new Hero();
        Assert.Equal(1f, h.DamageTakenMult, 3);
        h.Cur.Nodes.Add(HeroSkills.Toughness);
        Assert.True(h.DamageTakenMult < 1f);
    }

    [Fact]
    public void Signature_Dispatches_PerHero()
    {
        // Artificer signature is Overcharge: it sets the fort-wide frenzy timer.
        var s = new GameState(seedDebug: false);
        s.Hero.Kind = HeroKind.Artificer;
        s.Hero.AbilityCooldown = 0f;
        CombatSystem.Signature(s);
        Assert.True(s.OverchargeTimer > 0f);

        // Warden signature is Ground Slam: it damages a nearby enemy.
        var s2 = new GameState(seedDebug: false);
        s2.Hero.Kind = HeroKind.Warden;
        s2.Hero.Pos = Vector2.Zero;
        s2.Hero.AbilityCooldown = 0f;
        var e = new Enemy { Id = s2.NextId(), Health = 200, MaxHealth = 200, Radius = 11, Pos = new Vector2(20, 0) };
        s2.Enemies.Add(e);
        CombatSystem.Signature(s2);
        Assert.True(e.Health < 200f);
    }

    [Fact]
    public void ApplyToAll_ChangesEveryKind()
    {
        // Run-wide gear/upgrades apply to all kinds, so swapping keeps the bonus.
        var h = new Hero();
        h.ApplyToAll(p => p.Damage += 7f);
        Assert.Equal(21f, h.Progress[HeroKind.Ranger].Damage, 2);
        Assert.Equal(21f, h.Progress[HeroKind.Warden].Damage, 2);
        Assert.Equal(21f, h.Progress[HeroKind.Artificer].Damage, 2);
    }

    [Fact]
    public void Save_RoundTrips_PerKindProgressAndNodes()
    {
        var s = new GameState(seedDebug: false);
        s.Hero.Kind = HeroKind.Warden;          // resume as the Warden
        s.Hero.Level = 7;                        // Warden's level
        s.Hero.Cur.SkillPoints = 2;
        s.Hero.Cur.Nodes.Add(HeroSkills.WCleave);
        s.Hero.Progress[HeroKind.Ranger].Level = 4;
        s.Hero.Progress[HeroKind.Ranger].Nodes.Add(HeroSkills.RRicochet);

        var restored = RunStore.FromJson(RunStore.ToJson(RunStore.Capture(s)));
        Assert.NotNull(restored);
        var t = new GameState(seedDebug: false);
        RunStore.Apply(t, restored!);

        Assert.Equal(HeroKind.Warden, t.Hero.Kind);
        Assert.Equal(7, t.Hero.Level);
        Assert.Equal(2, t.Hero.Cur.SkillPoints);
        Assert.True(t.Hero.Has(HeroSkills.WCleave));
        // Inactive kind's independent progression survives too.
        Assert.Equal(4, t.Hero.Progress[HeroKind.Ranger].Level);
        Assert.Contains(HeroSkills.RRicochet, t.Hero.Progress[HeroKind.Ranger].Nodes);
    }

    [Fact]
    public void EliteDeath_DropsRelic_WhenSetIncomplete()
    {
        var s = new GameState(seedDebug: false);
        var e = new Enemy { Id = s.NextId(), Health = 1, MaxHealth = 10, Elite = true, Reward = 1, Pos = Vector2.Zero };
        s.Enemies.Add(e);
        CombatSystem.DamageEnemy(s, e, 50f);
        Assert.Contains(s.Drops, d => d.Kind == DropKind.Relic);
    }

    [Fact]
    public void Rally_SpendsGold_SlowsWave_ExceptWraiths()
    {
        var s = new GameState(seedDebug: false) { Gold = 200, Wave = 1 };
        var normal = new Enemy { Id = s.NextId(), Health = 100, MaxHealth = 100, Radius = 11, Pos = Vector2.Zero };
        var wraith = new Enemy { Id = s.NextId(), Health = 100, MaxHealth = 100, Radius = 12, StatusImmune = true, Pos = Vector2.Zero };
        s.Enemies.Add(normal);
        s.Enemies.Add(wraith);

        int cost = s.RallyCost;
        Assert.True(s.TryRally());
        Assert.Equal(200 - cost, s.Gold);
        Assert.True(s.RallyCooldown > 0f);
        Assert.True(normal.SlowTimer > 0f);
        Assert.Equal(0f, wraith.SlowTimer);
    }

    [Fact]
    public void Rally_FailsWithoutEnoughGold()
    {
        var s = new GameState(seedDebug: false) { Gold = 0, Wave = 1 };
        Assert.False(s.TryRally());
        Assert.Equal(0f, s.RallyCooldown);
    }

    [Fact]
    public void Dash_StrikesNearbyEnemies()
    {
        var s = new GameState(seedDebug: false);
        s.Hero.Pos = new Vector2(0, 48);
        s.Hero.Facing = new Vector2(0, -1);
        s.Hero.DashCooldown = 0f;
        var e = new Enemy { Id = s.NextId(), Health = 100, MaxHealth = 100, Radius = 11, Pos = new Vector2(0, -48) };
        s.Enemies.Add(e);
        CombatSystem.Dash(s);
        Assert.True(e.Health < 100f);
    }

    [Fact]
    public void Artificer_RepairsNearbyDamagedStructures()
    {
        var s = new GameState(seedDebug: false);
        s.Hero.Kind = HeroKind.Artificer;
        s.Hero.Pos = Vector2.Zero;
        var tower = StructureFactory.Create(s, CardDb.Get("archer_post"), new Vector2(40, 0));
        tower.Health = 10f;
        s.Structures.Add(tower);
        DefenseSystem.Update(s, 0.5f);
        Assert.True(tower.Health > 10f);
    }

    [Fact]
    public void NonArtificer_DoesNotRepairStructures()
    {
        var s = new GameState(seedDebug: false);
        s.Hero.Kind = HeroKind.Ranger;
        var tower = StructureFactory.Create(s, CardDb.Get("archer_post"), new Vector2(40, 0));
        tower.Health = 10f;
        s.Structures.Add(tower);
        DefenseSystem.Update(s, 0.5f);
        Assert.Equal(10f, tower.Health);
    }

    [Fact]
    public void EliteDeath_NoRelic_WhenAllOwned()
    {
        var s = new GameState(seedDebug: false);
        foreach (RelicKind k in System.Enum.GetValues<RelicKind>()) s.Hero.Relics.Add(k);
        var e = new Enemy { Id = s.NextId(), Health = 1, MaxHealth = 10, Elite = true, Reward = 1, Pos = Vector2.Zero };
        s.Enemies.Add(e);
        CombatSystem.DamageEnemy(s, e, 50f);
        Assert.DoesNotContain(s.Drops, d => d.Kind == DropKind.Relic);
    }

    // ---- Bulwark (tank) -------------------------------------------------

    [Fact]
    public void Bulwark_HasHigherBaseHealth()
    {
        var h = new Hero();
        Assert.True(h.Progress[HeroKind.Bulwark].MaxHealth > h.Progress[HeroKind.Ranger].MaxHealth);
    }

    [Fact]
    public void BulwarkStance_ReducesDamageTaken()
    {
        var s = new GameState(seedDebug: false);
        s.Hero.Kind = HeroKind.Bulwark;
        s.Hero.AbilityCooldown = 0f;
        float before = s.Hero.DamageTakenMult;
        CombatSystem.Signature(s);
        Assert.True(s.Hero.StanceTimer > 0f);
        Assert.True(s.Hero.DamageTakenMult < before);
    }

    [Fact]
    public void Bulwark_TauntsNearbyEnemyTowardItself()
    {
        var s = new GameState(seedDebug: false);
        s.Hero.Kind = HeroKind.Bulwark;
        s.Hero.Pos = new Vector2(0, 220);   // on the south lane (x=0), clear of walls
        var e = new Enemy { Id = s.NextId(), Health = 100, MaxHealth = 100, Radius = 11, Speed = 40, Pos = new Vector2(0, 290) };
        s.Enemies.Add(e);
        float before = Vector2.Distance(e.Pos, s.Hero.Pos);
        EnemySystem.Update(s, 0.2f);
        Assert.True(Vector2.Distance(e.Pos, s.Hero.Pos) < before);
    }

    [Fact]
    public void BulwarkThorns_ReflectDamageToAttacker()
    {
        var s = new GameState(seedDebug: false);
        s.Hero.Kind = HeroKind.Bulwark;
        s.Hero.Pos = new Vector2(0, 220);
        s.Hero.Cur.Nodes.Add(HeroSkills.BThorns);
        var e = new Enemy { Id = s.NextId(), Health = 100, MaxHealth = 100, Radius = 11, Speed = 40, Damage = 10, AttackTimer = 0f, Pos = new Vector2(0, 222) };
        s.Enemies.Add(e);
        EnemySystem.Update(s, 0.1f);
        Assert.True(e.Health < 100f);
    }

    [Fact]
    public void NonBulwark_DoesNotTaunt()
    {
        var s = new GameState(seedDebug: false);
        s.Hero.Kind = HeroKind.Ranger;          // no taunt radius
        Assert.Equal(0f, s.Hero.TauntRadius);
    }

    // ---- Executioner (assassin) -----------------------------------------

    [Fact]
    public void Executioner_IsSquishierThanRanger()
    {
        var h = new Hero();
        Assert.True(h.Progress[HeroKind.Executioner].MaxHealth < h.Progress[HeroKind.Ranger].MaxHealth);
    }

    [Fact]
    public void Execute_FinishesLowHealthEnemy()
    {
        var s = new GameState(seedDebug: false);
        s.Hero.Kind = HeroKind.Executioner;
        s.Hero.Pos = Vector2.Zero;
        s.Hero.AbilityCooldown = 0f;
        var e = new Enemy { Id = s.NextId(), Health = 10, MaxHealth = 100, Radius = 11, Pos = new Vector2(60, 0) };
        s.Enemies.Add(e);
        CombatSystem.Signature(s);
        Assert.True(e.Dead);
    }

    [Fact]
    public void Execute_DoesNotInstakillBoss_ButHurtsIt()
    {
        var s = new GameState(seedDebug: false);
        s.Hero.Kind = HeroKind.Executioner;
        s.Hero.Pos = Vector2.Zero;
        s.Hero.AbilityCooldown = 0f;
        var boss = new Enemy { Id = s.NextId(), Health = 300, MaxHealth = 4000, Radius = 20, Boss = true, Pos = new Vector2(60, 0) };
        s.Enemies.Add(boss);
        CombatSystem.Signature(s);
        Assert.False(boss.Dead);          // boss immune to the instakill
        Assert.True(boss.Health < 300f);  // but still takes the burst
    }

    [Fact]
    public void Execute_BlinksHeroAdjacentToTarget()
    {
        var s = new GameState(seedDebug: false);
        s.Hero.Kind = HeroKind.Executioner;
        s.Hero.Pos = Vector2.Zero;
        s.Hero.AbilityCooldown = 0f;
        var e = new Enemy { Id = s.NextId(), Health = 500, MaxHealth = 500, Radius = 11, Pos = new Vector2(140, 0) };
        s.Enemies.Add(e);
        CombatSystem.Signature(s);
        Assert.True(Vector2.Distance(s.Hero.Pos, e.Pos) < 60f);
    }

    // ---- Elementalist (frost mage) --------------------------------------

    [Fact]
    public void Elementalist_BoltsChillEnemies()
    {
        var s = new GameState(seedDebug: false);
        s.Hero.Kind = HeroKind.Elementalist;
        s.Hero.Pos = Vector2.Zero;
        s.Hero.ShotTimer = 0f;
        // Speed 0 → AimAhead doesn't lead, so the bolt flies straight at the enemy.
        var e = new Enemy { Id = s.NextId(), Health = 200, MaxHealth = 200, Radius = 11, Speed = 0, Pos = new Vector2(60, 0) };
        s.Enemies.Add(e);
        CombatSystem.UpdateHeroCombat(s, 0.016f);   // fires a frost bolt
        for (int i = 0; i < 20 && e.SlowTimer <= 0f; i++)
            CombatSystem.UpdateProjectiles(s, 0.02f); // step it into the enemy
        Assert.True(e.SlowTimer > 0f);
    }

    [Fact]
    public void FrostNova_DamagesAndSlowsAround()
    {
        var s = new GameState(seedDebug: false);
        s.Hero.Kind = HeroKind.Elementalist;
        s.Hero.Pos = Vector2.Zero;
        s.Hero.AbilityCooldown = 0f;
        var e = new Enemy { Id = s.NextId(), Health = 200, MaxHealth = 200, Radius = 11, Pos = new Vector2(40, 0) };
        s.Enemies.Add(e);
        CombatSystem.Signature(s);
        Assert.True(e.Health < 200f);
        Assert.True(e.SlowTimer > 0f);
    }

    // ---- Beastmaster (summoner) -----------------------------------------

    [Fact]
    public void Beastmaster_KeepsALoyalWolf()
    {
        var s = new GameState(seedDebug: false);
        s.Hero.Kind = HeroKind.Beastmaster;
        CompanionSystem.Update(s, 0.016f);
        Assert.Contains(s.Companions, w => w.Permanent);

        // Switching away dismisses the loyal wolves.
        s.Hero.Kind = HeroKind.Ranger;
        CompanionSystem.Update(s, 0.016f);
        Assert.DoesNotContain(s.Companions, w => w.Permanent);
    }

    [Fact]
    public void Wolf_BitesNearbyEnemy()
    {
        var s = new GameState(seedDebug: false);
        s.Hero.Kind = HeroKind.Beastmaster;
        s.Hero.Pos = Vector2.Zero;
        var e = new Enemy { Id = s.NextId(), Health = 100, MaxHealth = 100, Radius = 11, Pos = new Vector2(30, 0) };
        s.Enemies.Add(e);
        // Several ticks: the wolf spawns, closes the gap, and bites.
        for (int i = 0; i < 40 && e.Health >= 100f; i++) CompanionSystem.Update(s, 0.05f);
        Assert.True(e.Health < 100f);
    }

    [Fact]
    public void RallyPack_SummonsTemporaryWolves()
    {
        var s = new GameState(seedDebug: false);
        s.Hero.Kind = HeroKind.Beastmaster;
        s.Hero.AbilityCooldown = 0f;
        CombatSystem.Signature(s);
        Assert.True(s.Companions.Count(w => !w.Permanent) >= 3);
    }
}
