# Emberhold — Ideas & Future Direction

Running list of expansion ideas. Rough groupings; nothing here is committed to the roadmap yet.

---

## Shipped

Ideas that have made it into a release (newest first).

### v0.14.1 — "Field Manual" (QoL)
- **Rich pause screen** — summarizes the run at a glance: trial, hero passives/relics,
  horde tier, active synergies, and next-wave preview.

### v0.14.0 — "Momentum"
- **Hero combo (dash)** — Dash is now an offensive dash-strike (bursts enemies, shatters
  slowed ones) with brief i-frames; rewards mechanical skill.

### v0.13.0 — "Volatile Pact"
- **Anti-synergy** — a new synergy category: pairing Cannon + Storm Spire weakens both
  but grants all towers +15% fire rate (deckbuilding tension around what NOT to pair).

### v0.12.0 — "Artificer"
- **Third hero class: Artificer** — overclocks nearby towers and repairs structures
  instead of fighting directly; a tower-synergy playstyle.

### v0.11.0 — "Fortune"
- **Supply cache objectives** — periodic high-value gold caches drop out on a lane
  mid-fight; the hero must fight out to collect them.

### v0.10.0 — "Legacy"
- **Meta-progression (light)** — the profile tracks lifetime runs, kills, bosses slain,
  and an all-time **codex completion** count, shown on the game-over recap.

### v0.9.0 — "Quartermaster"
- **Gold-for-time** — the Rally Horn (F) spends gold to slow the whole wave for a few
  seconds, an emergency clutch tool on a cooldown.

### v0.8.1 — "Spectacle" (polish)
- **Structure personalities** — towers rotate a barrel to track their target and
  flash at the muzzle when firing.

### v0.8.0 — "Onslaught"
- **Assassin enemies** — teleport/blink past walls & traps to the keep; walls alone
  can't stop them.
- **Enemy immunities** — Wraiths are immune to burn & slow, rewarding broad damage
  coverage over single-element stacks.

### v0.7.0 — "Frontier"
- **New cards** — Storm Spire (Attack), Caltrops (Defend), Trading Post (Support),
  widening the build/tag space.
- **Rune Words** (3rd synergy layer) — owning 3+ structures sharing a tag unlocks a
  tag-wide passive (Resonance / Minefield / Boom Town).

### v0.6.0 — "Trials"
- **Challenge modifiers** — each run rolls one of six trials with a clear tradeoff
  (Gold Rush, Bloodthirst, Iron Horde, Endless Swarm, Glass Cannon, Veteran), shown
  on the intro and a HUD chip.

### v0.5.0 — "Warlord"
- **Chapter boss** — a unique boss every 10th wave: huge HP, summons adds, resists
  slows, telegraphed, with a guaranteed relic/gold reward.
- **Fortnight clock** — each boss defeated ramps a permanent horde buff (War Drums),
  keeping late-game pressure rising.

### v0.4.0 — "Strategist"
- **Upcoming wave preview** — exact composition of the next wave shown on the
  between-wave card and the draft screen (e.g. "14 incoming · Siege x2 · Shielded x4").
- **Gold interest** — capped treasury return (8%, max 30) on banked gold each wave clear.

### v0.3.0 — "Champions"
- **Passive level-up abilities** — Lv3 Quick Hands (pickup radius), Lv5 signature
  (Ranger ricochet / Warden cleave), Lv7 Second Wind (regen).
- **Equipment drops** — elites drop relics (Ember Ring, Swift Boots, Warden's Cloak,
  Hawk Eye) with permanent run bonuses, shown as chips under the hero bar.

### v0.2.0 — "Arsenal"
- **More field synergies** — four new cross-category combos (Backdraft, Hellfire,
  Conduit, Sniper's Nest) rewarding specific placements; appear in the codex/HUD automatically.
- **Animated synergy popups** — a discovery banner the first time each synergy triggers in a run.

### v0.1.0 — "Heat of Battle"
- **Kill streaks** — consecutive kills within 2.5s build a streak that buffs hero
  damage (+15/30/50%) and drops bonus gold, with tier-up floaters and a live meter.
- **Wave-end stat card** — between-wave recap: kills, gold, damage, best streak, structures lost.
- **Siege engines** — slow, high-HP enemies (wave 7+) that hunt and demolish your
  structures (all structures now have health); forces proactive defense.
- **Typed enemy edge indicators** — screen-edge arrows coloured by enemy type,
  enlarged for siege/elite/brute, telegraphing off-screen threats.

---

## Core loop deepening

### Enemy variety & counterplay
- **Siege engines** — slow, high-HP units that target and demolish structures; forces the hero to be proactive rather than passive.
- **Assassin enemies** — teleport past walls directly to the keep; walls alone can't stop them.
- **Swarm waves vs elite waves** — 100 tiny enemies vs one massive brute; radically different defensive needs, forces flexible builds.
- **Enemy factions with immunities** — fireproof, frost-immune, shield-immune factions that rotate by run; rewards building broad coverage over single-element stacks.
- **Chapter boss** — one unique boss enemy at the end of each chapter with a scripted mechanic; always drops a guaranteed rare card.

### Hero depth
- **Third hero class: Artificer** — deploys mini-turrets and repairs structures; a full tower-synergy playstyle where the hero barely attacks directly.
- **Passive level-up abilities** — earned through combat XP (already tracked): Ash gets a ricochet shot at lv 5, Mira gets a parry/reflect at lv 6, etc.
- **Equipment drops** — ring, cloak, boots dropped by elite enemies; passive stat bonuses the hero keeps for the rest of the run.
- **Hero combo system** — contextual interactions: dash through a Tar Pit to ignite it; land a volley into slowed enemies for a shatter burst; adds mechanical skill expression.

### Tower synergy expansion
- **More field synergies** — most of the 18-card combination space is untapped; add at least 4–6 more cross-category field synergies.
- **Anti-synergy** — pairing two specific cards reduces both their stats but unlocks a unique powerful passive (creates real deckbuilding tension around what NOT to pair).
- **Rune Words** (3rd synergy layer) — owning 3+ cards with the same Tag unlocks a tag-wide passive (e.g. 3× Elemental = all elemental towers chain-link; 3× Wall = walls regenerate); stacks with existing keystones.
- **Animated synergy popups** — satisfying discovery moment when a new synergy activates for the first time in a run.

---

## Strategic decisions

### Economy & gold
- **Gold interest** — idle gold above a threshold generates small passive income each wave; incentivises spending and punishes hoarding.
- **Mine investment** — spend 150g to permanently increase a mine's output for the run; competes with buying structures.
- **Supply cache objectives** — rare mid-wave drops that require the hero to fight to a location to collect; dynamic combat objective.
- **Gold-for-time** — spend 50g to freeze a wave for 8 seconds; limited uses per run, emergency mechanic only.

### Placement strategy
- **Destructible terrain** — enemies can permanently demolish wall-gates, shortening lanes and forcing reactive placement late-game.
- **Zone upgrades** — buy a "Fortified Ground" improvement for a specific quadrant; all structures in that zone get +15% output for the run.
- **Rotating zones** — each expansion flips which quadrants are buildable (e.g. only NW/SE unlocked initially); creates drastically different layouts across runs.
- **Layered lanes** — two parallel lanes per cardinal direction once chapter ≥ 4; doubles placement pressure and opens new Kill Box setups.

### Draft & card design
- **Legendary cards** — rare draft slot replacement (1% chance), much higher build cost, game-changing unique effect.
- **Draft veto** — once per run, skip a draft entirely and bank it; next milestone gives a double-pick instead.
- **Upcoming wave preview** — show the composition of the next two waves during draft so picks feel informed rather than guessed.
- **Card fusion** — owning both variants of a concept (Barricade + Bulwark = Fortress Wall) unlocks a merged card in the shop.

---

## Replayability systems

### Run structure
- **Challenge modifiers** — randomly applied at run start: "Siege of Fire" (burn-immune enemies), "Gold Curse" (60% gold drops, halved shop prices), "Iron Curtain" (no Defend cards appear in drafts), etc.
- **Weekly challenge** — fixed seed + specific modifier pool; shared leaderboard.
- **Ascension levels** (Hades-style) — each unlocked ascension adds a permanent difficulty rule but also a permanent passive bonus; gives long-term skill progression.
- **Milestone unlocks** — beat wave 15 with a pure-hero build → unlock Artificer permanently; beat wave 20 without expanding → unlock a "Compact Fort" modifier; etc.

### Meta-progression (keep it light)
- **Codex completion reward** — discover all field synergies in one run → unlock a second free starting card on future runs.
- **Named builds** — auto-snapshot the card+upgrade loadout at game-over; name it and replay it as a fixed challenge later.
- **Achievement cosmetics** — cape colour variants for the hero, tile themes for the fort; cosmetic only.

### Late-game escalation (wave 20+)
- **Raider general** — a commander unit appears behind the swarm at wave 25; killing it routes the wave early, but missing it doubles the next wave's count.
- **Fortnight clock** — every 10 waves the enemy faction gains a permanent buff (ignore slow, ignore pierce, etc.); keeps pressure rising even when defenses are maxed.
- **Endless mode** (wave 30+) — structure cap removed; shop offers exotic one-time ultra-cards (double-range aura, chain-splash fusion, fort-wide overdrive); enemy scaling goes exponential.
- **Dynamic map events** — "Bridge Collapse" blocks a lane temporarily, "Supply Drop" spawns a free random structure mid-map, "Cursed Ore" makes the next mine drop harmful coins to the hero.

---

## Moment-to-moment feel

### Juice & polish
- **Typed enemy edge indicators** — screen-edge arrows coloured by enemy type so you see "Shielded incoming from west" before it arrives.
- **Structure personalities** — cannon barrels visually track their targets, flame jets puff particles, mines flash when producing gold.
- **Kill streaks** — rapid multi-kills within 2 seconds give a brief gold/XP multiplier and a "HOT STREAK" floater.
- **Wave-end stat card** — a brief overlay after each wave: damage dealt, gold earned, synergies triggered, hero kills vs tower kills.
- **Death replay** — on game-over, rewind the last 8 seconds of combat to show the exact moment the keep fell.

### Sound design (when audio is added)
- Distinct sound per tower: ballista thunk, cannon boom, frost ping, chain zap, flame crackle.
- Adaptive music: bass track layers build as enemy count rises, strips back to sparse ambient in between-wave silence.
- Synergy activation sting — short musical stab the first time a keystone triggers in a run.

---

## Long-term roadmap

- **Co-op** — two heroes, split-screen or network; one manages the build phase, one fights aggressively.
- **Procedural map seeds** — different fort shapes (circular keep, star fort, narrow-choke maps) rotate across runs.
- **Story mode** — 5-chapter campaign, each chapter is a named fort siege with a scripted boss, unlocks lore and new heroes.
- **Steam Workshop** — share card-pool mods and custom challenge seeds.
