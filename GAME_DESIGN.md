# Emberhold — Strategy Rebuild Design Spec

> Working spec for the ground-up rebuild (C# + Raylib-cs). The old `DESIGN.md`
> describes the JS prototype and is kept only as a porting reference.

## Pillars

1. **Draft, don't unlock in fixed order.** Each chapter offers a choice across
   Attack / Defend / Support. You build a deck of pads over a run.
2. **Placement is strategy.** Pads are placed by hand inside buildable zones,
   during a paused placement phase. Locked once placed.
3. **Synergy beats specialization.** Three mono strategies are all viable, but
   the highest ceilings come from cross-category combos — both in *where* things
   sit and in *what* you drafted *when*.

## Run Structure

A run is a sequence of **Chapters** (the old "stages" — each a larger fort with
more buildable zone). Each chapter:

1. **Draft phase** *(paused)* — choose cards (see Draft).
2. **Placement phase** *(paused)* — place every pad you now own, anywhere in the
   unlocked zones. Pads lock on placement; they cannot be moved later.
3. **Combat phase** — waves arrive. Hero runs the field collecting gold, stands
   on pads to fund construction, and fights. Clearing the chapter's waves lets
   you pay to **expand** → next chapter (new draft + larger zone).

Gold is still the build currency. Placement is free; *building* the placed pad
costs gold, paid by standing on it.

## Draft

Each draft presents **3 cards — one Attack, one Defend, one Support** — each
drawn from its category pool. **You pick one.** That tradeoff (you can't take all
three) is what makes mono-vs-hybrid a real decision.

- ~1–2 drafts per chapter → ~8–12 picks per run.
- Picked cards enter your inventory as placeable pads.
- A small fixed starter (e.g. one Archer pad + the Keep) seeds chapter 1 so the
  first combat isn't empty.

*(Open knob: pick-1-of-3 vs pick-one-from-each. Recommendation: pick-1-of-3.)*

## Economy — build time

Standing-to-build time should grow ~**√cost**, so late upgrades don't strand the
hero on a pad. Achieve it by scaling the deposit *rate* with √cost:

```
depositRate = BASE_RATE * sqrt(cost) * depositSpeedMult
buildTime   = cost / depositRate = sqrt(cost) / BASE_RATE
```

So 4× the cost ⇒ only 2× the build time. Gold availability remains the real gate.

## Buildables (pad/card pool)

Six per category (small enough to find combos, varied enough for run variety).
Each carries **tags** used by synergy matching.

### Attack — kill things
| Card | Behavior | Tags |
|---|---|---|
| Archer Post | Fast, low dmg, single target | rapid, physical |
| Cannon | Slow, splash AoE | splash, siege |
| Ballista | Long range, high single-target, pierces | pierce, longrange |
| Chain Coil | Arcs between nearby enemies | chain, elemental |
| Flame Jet | Short cone, applies burn (DoT) | burn, dot, shortrange |
| Frost Spire | Low dmg, slows what it hits | slow, control, elemental |

### Defend — control space, absorb
| Card | Behavior | Tags |
|---|---|---|
| Barricade | Destructible wall, blocks a lane | wall, block |
| Spike Trap | Ground tile, damages passing enemies | ground, trap |
| Tar Pit | Ground tile, slows enemies inside | ground, slow, control |
| Bulwark | Tanky regenerating gate | wall, regen |
| Moat Line | Area that damages + slows | trap, slow |
| Redoubt | Wall that retaliates melee damage | wall, retaliate |

### Support — buff & economy
| Card | Behavior | Tags |
|---|---|---|
| Gold Mine | Periodic gold drops | economy |
| War Banner | +damage to towers in range | aura, damage |
| Forge | +fire rate to towers in range | aura, rate |
| Ember Shrine | Buffs hero (volley / overdrive) | hero |
| Watchtower | +range to towers in range, reveals | aura, range |
| Workshop | Cheapens/speeds nearby builds, repairs nearby structures | economy, repair |

## Synergy — two layers

**Layer 1 — Field synergies (placement / adjacency, local).** Reward *where* you
put things. Mostly cross-category, so mono builds miss most of them.

- **Kill Box** — Tar Pit (D) under a Cannon's (A) range: cannon splash +50% vs
  slowed targets.
- **Siege Breaker** — Ballista (A) screened behind a Barricade (D): while that
  wall lives, ballista gains +range and +1 pierce.
- **Overcharged Coil** — Chain Coil (A) inside a War Banner (S) aura: chain hits
  +2 extra targets.
- **Frostfire** — Frost Spire (A) + Flame Jet (A) overlapping: slowed *and*
  burning enemies take a "shatter" burst (the one strong intra-Attack combo).
- **Spoils** — enemies killed on a Defend ground-trap (D) near a Gold Mine (S)
  drop +1 gold.

**Layer 2 — Keystone synergies (ownership pairs, global).** Reward *what you
drafted together* across the run, independent of placement — this is the
"round-2 Attack + round-7 Support combo" you wanted.

- **Cryo-Forge** (Frost Spire + Forge owned) — all slows last 30% longer, everywhere.
- **Ember Battery** (Cannon + Ember Shrine owned) — hero volley also detonates splash.
- **Supply Lines** (Gold Mine + War Banner owned) — banners boost mine output globally.
- **Iron Tide** (Bulwark + Redoubt owned) — all walls share a pooled HP buffer.

**Mono-amplifiers (so going deep is still fine, just not best):**
- 3+ Attack clustered → "Battery": small shared damage bonus.
- 3+ Defend in a line → "Fortified": shared HP pool, tankier wall.
- 3+ Support → "Network": auras broadcast fort-wide instead of range-limited.

## Map & Zones

Defined **lanes**: enemies spawn at map edges and follow lanes through gates to
the keep. **Buildable zones** flank the lanes and grow each chapter.

**Placement rule (decided):** placement depends on the card's category, giving
each pillar a distinct spatial role:
- **Attack & Support → zones.** Killing towers and economy sit in the protected
  quadrants, firing/projecting into the lanes.
- **Defend → lanes.** Walls and ground traps are placed *on* the lane arms inside
  the fort — that's their purpose: blocking, funneling, and slowing the assault.

Nothing may overlap the keep clearance or another structure. This split is what
makes positional synergies geometric: a Tar Pit in the lane, covered by a Cannon
in the adjacent zone (Kill Box); a Ballista in a zone firing past a Barricade in
the lane it screens behind (Siege Breaker).

## Enemy counter-design (makes synergies matter)

Each type punishes a particular build, so no single strategy covers everything.
Composition unlocks with depth (the difficulty knob):

- **Raider / Runner / Brute** — the baseline; runners swarm, brutes are tanky.
- **Elite** (every 5th wave) — single huge target; drops an Ember Sigil.
- **Flyer** (wave 6+) — flies straight to the keep, **ignoring walls and traps**.
  Punishes wall/turtle builds; only towers and auras stop it.
- **Shielded** (wave 8+) — flat **per-hit mitigation**, so rapid/chain fire is wasted.
  Rewards big single hits (Cannon/Ballista) — and **burn/traps bypass the shield**,
  so Flame Jet / Moat counter it.
- **Healer** (wave 10+) — periodically mends nearby wounded enemies. Punishes low
  DPS / slow kills; rewards burst and focus fire.

## Balance (first pass)

Tuned with an autonomous bot + telemetry rather than guesswork: `--auto` drives a
naive hero (fund nearest pad, collect gold, fight) and the run prints a `REPORT`
line (wave/keep/structures/kills reached). The bot is a *lower bound* on skill.

Findings:
- A **pre-built fort thrives** (seeded autopilot held wave 8+ with the keep near
  full), so mid-game combat is well-tuned — enemies were *not* nerfed.
- The **opening** was too punishing (keep fell ~wave 4 before defenses existed).
  Smoothed by: starting gold 20, **two** free starters (Archer + Barricade), and a
  4s grace before wave 1. Naive-bot survival past the opening rose from ~⅓ to ~¾.

Key knobs live in `Game/Balance.cs` (enemy/hero/tower multipliers, √cost deposit
base rate). Final fine-tuning wants human playtesting.

## Tech / Architecture (C# + Raylib-cs, all code)

```
Emberhold.sln
src/
  Program.cs              entry: window, fixed-step loop (port of frame())
  Core/    Vec2, MathUtils, RNG        (port of core.js, unit-tested)
  Game/    GameState, Phase state machine (Draft/Placement/Combat),
           Hero, Enemy, Projectile, Drop, Structure, Pad,
           WaveSystem, EconomySystem, SynergyEngine
  Data/    CardDb, EnemyDb, SynergyDb   (definition tables, data-driven)
  Render/  Renderer (procedural shape drawing, ported from canvas)
  UI/      Hud, DraftScreen, PlacementOverlay
tests/     Core + Data unit tests (xUnit)
```

Definitions live as typed C# tables (compile-safe) for v1; can move to JSON for
modding later. Rendering stays procedural — no sprite assets.
```
