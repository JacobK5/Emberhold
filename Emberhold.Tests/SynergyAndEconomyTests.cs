using System.Numerics;
using Emberhold.Data;
using Emberhold.Game;
using Xunit;

namespace Emberhold.Tests;

public class SynergyTests
{
    private static GameState State() => new(seedDebug: false);

    private static void Add(GameState s, StructureKind kind, Vector2 pos)
        => s.Structures.Add(StructureFactory.Create(s, CardDb.All.First(c => c.Kind == kind), pos));

    [Fact]
    public void CryoForge_ExtendsSlowDuration()
    {
        var s = State();
        Add(s, StructureKind.FrostSpire, new Vector2(80, -80));
        Add(s, StructureKind.Forge, new Vector2(-80, -80));
        SynergyEngine.Evaluate(s);
        Assert.Contains("cryo_forge", s.ActiveSynergies);
        Assert.Equal(1.3f, s.SlowDurationMult, 3);
    }

    [Fact]
    public void SupplyLines_FromMineAndBanner()
    {
        var s = State();
        Add(s, StructureKind.GoldMine, new Vector2(80, 80));
        Add(s, StructureKind.WarBanner, new Vector2(-80, 80));
        SynergyEngine.Evaluate(s);
        Assert.True(s.SupplyLines);
        Assert.Contains("supply_lines", s.ActiveSynergies);
    }

    [Fact]
    public void KillBox_CannonCoveringSlowTrap_GainsSplash()
    {
        var s = State();
        Add(s, StructureKind.Cannon, new Vector2(80, -80));
        Add(s, StructureKind.TarPit, new Vector2(0, -100)); // within cannon range
        SynergyEngine.Evaluate(s);
        Assert.Contains("kill_box", s.ActiveSynergies);
        var cannon = s.Structures.First(st => st.Kind == StructureKind.Cannon);
        Assert.True(cannon.SynSplashBonus > 0f);
    }

    [Fact]
    public void Fortified_FromThreeWalls_ReducesDamageFlag()
    {
        var s = State();
        Add(s, StructureKind.Barricade, new Vector2(0, -90));
        Add(s, StructureKind.Barricade, new Vector2(0, 90));
        Add(s, StructureKind.Redoubt, new Vector2(90, 0));
        SynergyEngine.Evaluate(s);
        Assert.True(s.Fortified);
    }

    [Fact]
    public void Network_FromThreeAuras_GoesGlobal()
    {
        var s = State();
        Add(s, StructureKind.WarBanner, new Vector2(80, -80));
        Add(s, StructureKind.Forge, new Vector2(-80, -80));
        Add(s, StructureKind.Watchtower, new Vector2(80, 80));
        SynergyEngine.Evaluate(s);
        Assert.True(s.AurasGlobal);
        Assert.Contains("network", s.ActiveSynergies);
    }

    [Fact]
    public void Glacier_FromFrostAndCannon()
    {
        var s = State();
        Add(s, StructureKind.FrostSpire, new Vector2(80, -80));
        Add(s, StructureKind.Cannon, new Vector2(-80, -80));
        SynergyEngine.Evaluate(s);
        Assert.True(s.Glacier);
        Assert.Contains("glacier", s.ActiveSynergies);
    }

    [Fact]
    public void Wildfire_FromFlameAndChain()
    {
        var s = State();
        Add(s, StructureKind.FlameJet, new Vector2(80, -80));
        Add(s, StructureKind.ChainCoil, new Vector2(-80, -80));
        SynergyEngine.Evaluate(s);
        Assert.True(s.Wildfire);
        Assert.Contains("wildfire", s.ActiveSynergies);
    }

    [Fact]
    public void Phalanx_TowerBesideWall_GainsDamage()
    {
        var s = State();
        Add(s, StructureKind.ArcherPost, new Vector2(0, -90)); // near the wall
        Add(s, StructureKind.Barricade, new Vector2(0, -100));
        SynergyEngine.Evaluate(s);
        Assert.Contains("phalanx", s.ActiveSynergies);
        var tower = s.Structures.First(st => st.Role == StructureRole.Tower);
        Assert.True(tower.SynDamageMult > 1f);
    }

    [Fact]
    public void NoSynergies_WhenUnrelatedStructures()
    {
        var s = State();
        Add(s, StructureKind.ArcherPost, new Vector2(80, -80));
        SynergyEngine.Evaluate(s);
        Assert.Empty(s.ActiveSynergies);
        Assert.Equal(1f, s.SlowDurationMult);
    }

    // ---- Arsenal batch: new field synergies ----

    [Fact]
    public void Hellfire_CannonBesideFlameJet_GivesCannonBurn()
    {
        var s = State();
        Add(s, StructureKind.Cannon, new Vector2(0, -80));
        Add(s, StructureKind.FlameJet, new Vector2(60, -80)); // within 90
        SynergyEngine.Evaluate(s);
        Assert.Contains("hellfire", s.ActiveSynergies);
        Assert.True(s.Structures.First(st => st.Kind == StructureKind.Cannon).SynBurnDps > 0f);
    }

    [Fact]
    public void Conduit_ChainCoilBesideFrostSpire_GivesChainSlow()
    {
        var s = State();
        Add(s, StructureKind.ChainCoil, new Vector2(0, -80));
        Add(s, StructureKind.FrostSpire, new Vector2(60, -80)); // within 90
        SynergyEngine.Evaluate(s);
        Assert.Contains("conduit", s.ActiveSynergies);
        Assert.True(s.Structures.First(st => st.Kind == StructureKind.ChainCoil).SynSlowFactor < 1f);
    }

    [Fact]
    public void SnipersNest_BallistaInWatchtowerAura_BuffsDamage()
    {
        var s = State();
        Add(s, StructureKind.Ballista, new Vector2(0, -80));
        Add(s, StructureKind.Watchtower, new Vector2(80, -80)); // within aura range
        SynergyEngine.Evaluate(s);
        Assert.Contains("snipers_nest", s.ActiveSynergies);
        Assert.True(s.Structures.First(st => st.Kind == StructureKind.Ballista).SynDamageMult > 1f);
    }

    [Fact]
    public void Backdraft_FlameJetCoversSlowTrap_TrapBurns()
    {
        var s = State();
        Add(s, StructureKind.TarPit, new Vector2(0, -80));
        Add(s, StructureKind.FlameJet, new Vector2(90, -80)); // within 120
        SynergyEngine.Evaluate(s);
        Assert.Contains("backdraft", s.ActiveSynergies);
        Assert.True(s.Structures.First(st => st.Kind == StructureKind.TarPit).SynTrapBurnDps > 0f);
    }

    // ---- Frontier batch: Rune Words ----

    [Fact]
    public void Resonance_FromThreeElemental_BuffsElementalTowers()
    {
        var s = State();
        Add(s, StructureKind.ChainCoil, new Vector2(80, -80));
        Add(s, StructureKind.FrostSpire, new Vector2(-80, -80));
        Add(s, StructureKind.StormSpire, new Vector2(80, 80));
        SynergyEngine.Evaluate(s);
        Assert.Contains("resonance", s.ActiveSynergies);
        Assert.True(s.Structures.First(st => st.Kind == StructureKind.StormSpire).SynExtraChains >= 1);
    }

    [Fact]
    public void Minefield_FromThreeTraps()
    {
        var s = State();
        Add(s, StructureKind.SpikeTrap, new Vector2(0, -90));
        Add(s, StructureKind.MoatLine, new Vector2(0, 90));
        Add(s, StructureKind.Caltrops, new Vector2(90, 0));
        SynergyEngine.Evaluate(s);
        Assert.True(s.Minefield);
        Assert.Contains("minefield", s.ActiveSynergies);
    }

    [Fact]
    public void BoomTown_FromThreeEconomy()
    {
        var s = State();
        Add(s, StructureKind.GoldMine, new Vector2(80, 80));
        Add(s, StructureKind.Workshop, new Vector2(-80, 80));
        Add(s, StructureKind.TradingPost, new Vector2(80, -80));
        SynergyEngine.Evaluate(s);
        Assert.True(s.BoomTown);
        Assert.Contains("boom_town", s.ActiveSynergies);
    }

    [Fact]
    public void VolatilePact_PenalizesPair_AndFlagsHaste()
    {
        var s = State();
        Add(s, StructureKind.Cannon, new Vector2(80, -80));
        Add(s, StructureKind.StormSpire, new Vector2(-80, -80));
        SynergyEngine.Evaluate(s);
        Assert.True(s.VolatilePact);
        Assert.Contains("volatile_pact", s.ActiveSynergies);
        Assert.True(s.Structures.First(st => st.Kind == StructureKind.Cannon).SynDamageMult < 1f);
    }

    [Fact]
    public void Discovery_QueuesEachSynergyPopupOnce()
    {
        var s = State();
        Add(s, StructureKind.Cannon, new Vector2(0, -80));
        Add(s, StructureKind.FlameJet, new Vector2(60, -80));
        SynergyEngine.Evaluate(s);
        int afterFirst = s.SynergyPopups.Count;
        Assert.True(afterFirst >= 1);
        SynergyEngine.Evaluate(s); // already discovered — must not re-queue
        Assert.Equal(afterFirst, s.SynergyPopups.Count);
    }
}

public class EconomyTests
{
    [Fact]
    public void StandingOnPad_FundsAndBuilds_DeductingGold()
    {
        var s = new GameState(seedDebug: false) { Gold = 100 };
        var def = CardDb.Get("archer_post"); // cost 20
        var pos = Geometry.Center(Map.BuildZones(1)[0]);
        s.Pads.Add(new Pad { Def = def, Pos = pos });
        s.Hero.Pos = pos; // stand on the pad

        // ~3 seconds at 0.1s steps: clears dwell grace then funds the build.
        for (int i = 0; i < 40; i++) EconomySystem.UpdateBuilding(s, 0.1f);

        Assert.Empty(s.Pads);
        Assert.Single(s.Structures);
        Assert.Equal(StructureKind.ArcherPost, s.Structures[0].Kind);
        Assert.Equal(80, s.Gold); // 100 - 20
    }

    [Fact]
    public void StandingOnStructure_UpgradesAndDeductsGold()
    {
        var s = new GameState(seedDebug: false) { Gold = 500 };
        var st = StructureFactory.Create(s, CardDb.Get("archer_post"), new Vector2(84, -84));
        s.Structures.Add(st);
        s.Hero.Pos = st.Pos;

        float dmg0 = st.Damage;
        int lvl0 = st.Level;
        int gold0 = s.Gold;

        for (int i = 0; i < 60; i++) EconomySystem.UpdateUpgrades(s, 0.1f);

        Assert.True(st.Level > lvl0, "structure should gain at least one level");
        Assert.True(st.Damage > dmg0, "upgraded tower should hit harder");
        Assert.True(s.Gold < gold0, "upgrades should consume gold");
    }

    [Fact]
    public void Upgrade_StopsAtMaxLevel()
    {
        var s = new GameState(seedDebug: false) { Gold = 100000 };
        var st = StructureFactory.Create(s, CardDb.Get("archer_post"), new Vector2(84, -84));
        s.Structures.Add(st);
        s.Hero.Pos = st.Pos;

        for (int i = 0; i < 400; i++) EconomySystem.UpdateUpgrades(s, 0.1f);

        Assert.Equal(Structure.MaxLevel, st.Level);
    }

    [Fact]
    public void Mine_ProducesMoreWithSupplyLines()
    {
        var s = new GameState(seedDebug: false);
        s.Structures.Add(StructureFactory.Create(s, CardDb.Get("gold_mine"), new Vector2(80, 80)));

        s.SupplyLines = false;
        EconomySystem.UpdateMines(s, 100f); // force a production tick
        int withoutLines = s.Drops.Count;

        s.Drops.Clear();
        s.SupplyLines = true;
        EconomySystem.UpdateMines(s, 100f);
        int withLines = s.Drops.Count;

        Assert.True(withLines > withoutLines);
    }
}
