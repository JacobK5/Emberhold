# Emberhold — Ideas & Future Direction

Running list of expansion ideas. Rough groupings; nothing here is committed to the roadmap yet.

---

## Shipped

Ideas that have made it into a release (newest first).

### v0.35.0 — "Counterweight" (balance pass)
- **Attack-tower nerf** — tower upgrades now scale ×1.45 damage / ×0.88 rate per level
  (was ×1.6 / ×0.85 ≈ ×1.88 DPS per level): maxing a couple of attack towers alone no
  longer carries a whole run. Stacked fire-rate buffs (Forge/Artificer/Overcharge/
  Volatile Pact/Overdrive Core) now floor at a 0.15s interval.
- **Everything-else buff** — traps hit harder at base (Spike 24 / Caltrops 17 / Moat 13
  DPS) and upgrade better (×1.7 DPS, −0.12 slow, +6 radius); mine upgrades tick ×0.7
  interval (was ×0.78); aura upgrades grant more (+0.22 damage, −0.07 rate, +30 aura
  radius); wall upgrades give ×1.6 HP.
- **Walls mend between waves** — standing walls recover 40% of missing HP at each wave
  clear, so defend investment stops bleeding out run-long.
- **Flyers now truly ignore ground traps** (they always ignored walls; traps damaging
  airborne units was a bug) — air raids genuinely punish trap-only forts.

### v0.34.1 — bug-fix patch
- Fixed the recurring screen flash: Last Stand novas no longer fire on an empty field
  between waves, and the danger vignette only pulses while a wave is live.
- Resume now restores shop pricing from the saved trial + ascension; Ember Shrine no
  longer overwrites upgraded volley stats (now run-wide, only improves); the Workshop
  card actually speeds nearby builds + repairs structures; deep-run supply shops fit
  on screen (adaptive columns); DoT no longer floods particles every frame (burning
  foes get a flame marker); game-over recap is no longer drawn over by HUD/banners.

### v0.34.0 — "Last Stand"
- **Keep Last Stand** — when the keep drops below 30% HP it rallies: every few seconds it
  emits a defensive nova (area damage + knockback + slow around the keep, bosses resist)
  and the screen edges pulse a danger-red vignette that deepens as the keep weakens. A
  dramatic comeback beat that can buy a losing run a few more waves.

### v0.33.0 — "Champions"
- **Champion mini-bosses** — from wave 14, one rank-and-file raider per non-boss wave is
  promoted into a named, crowned champion: far tankier, a guaranteed ember + bonus gold
  and Fury on death — a high-value priority target between bosses. Each has a trait:
  **Ironhide** (heavily armoured, slow), **Warbringer** (enrages — speeds up as it loses
  HP), **Swiftblade** (fragile but blindingly fast). Telegraphed by a spawn banner.

### v0.32.0 — "Cataclysm"
- **Fury ultimate** — a kill-charged meter (under the ability bar) that fills as you slay
  raiders (tougher foes charge it faster). At full, press **Q** to unleash the Cataclysm:
  a huge radial blast around the hero that shreds and knocks back the swarm (ignoring
  shields, bosses resist), plus a 4s Overdrive burst and an expanding shockwave. A
  performance-gated power spike that rewards aggressive play.

### v0.31.0 — "Ascension"
- **Ascension tiers** — a Hades-style difficulty ladder chosen at run start on the
  hero-select screen ([-]/[+] selector, with a live rule summary). Each tier stacks
  cumulative rules: +12% enemy HP/level, +6% shop prices/level, +5% enemy speed from
  tier 3, and up to -20% keep HP. Clearing wave 10 (a boss) at your current ceiling
  unlocks the next tier (persisted in the profile, capped at 5). The run's tier shows
  on the HUD + game-over recap and persists in run saves.

### v0.30.0 — "Tempest"
- **Wave archetypes** — from wave 10, about a third of non-boss waves take on a shape
  (telegraphed in the preview + a wave banner), reshaping composition and raider stats:
  - **Swarm** — a tide of weak, fast, small runners (≈1.85× count).
  - **Juggernaut** — a few towering, armoured brutes/siege (huge HP, big bounty).
  - **Air Raid** — a flock of wall-ignoring flyers.
  - **Frenzy** — fast raiders with fat bounties.
  The archetype is a pure function of (wave, per-run salt) so the preview always matches
  the real spawn; the salt persists in run saves. Elites/bosses keep their identity.

### v0.29.0 — "Endless"
- **Exotic mega-upgrades** — from wave 18 the supply shop offers one rare, expensive,
  one-time run-defining upgrade at a time (kept at the top of the list). A premium gold
  sink for the deep game: **Overdrive Core** (+25% tower fire rate), **Siege Breaker**
  (+35% tower damage to bosses/elites/siege), **Aegis Matrix** (keep regenerates 3 HP/s),
  **Mother Lode** (richer, faster mines), **Phoenix Heart** (revive once at 50% HP).
  Owned exotics persist in run saves and show on the pause panel.

### v0.28.0 — "Upheaval"
- **Dynamic map events** — from wave 8 on (never on boss waves, with a 3-wave gap), a
  battlefield event is telegraphed during the lull and runs through the next wave:
  - **Meteor Storm** (hazard) — meteors rain across the field; area bursts shred the
    swarm (ignoring shields) but scorch the hero if he stands in the blast.
  - **Supply Drop** (boon) — one or two free, fully-built tower/support structures land
    in your quadrants.
  - **Gold Rush** (boon) — raider bounties and mine yields are doubled for the wave.

### v0.27.0 — "Frontier Hall"
- **Home screen** — a proper title menu (EMBERHOLD / Frontier Siege) with Resume /
  New Run / Balancing / Quit. A clean launch opens here; the run begins only once you
  pick New Run + a hero (or Resume a checkpoint). Replaces the old launch resume-prompt.
- **Hero-select screen** — a card grid of all seven heroes (portrait, role, stat bars,
  signature, one-line blurb), shown both at the start of a run and as the in-game (H)
  hero swap. H now opens a pick overlay instead of blind-cycling to the next hero.
- **Balancing panel** — a live tuning screen (from the title or a paused run via B) over
  every Balance multiplier: grouped -/+ steppers, Reset to defaults, and Copy/Paste a
  preset through the clipboard. Changes persist to disk (balance.cfg).

### v0.26.0 — "Synthesis"
- **Card fusion** — own both halves of a pair and a merged, legendary-grade card
  appears in the supply shop: Barricade + Bulwark → Fortress Wall, Archer Post +
  Cannon → Siege Battery, Gold Mine + Workshop → Grand Exchange. Buy it, place it,
  fund it like any card. Rewards building toward a combo.

### v0.25.0 — "Relics of Power"
- **Legendary cards** — a draft slot is occasionally (10%) replaced by a Legendary: a
  souped-up version of a base structure (Dragon's Maw, Tempest Coil, Aegis Wall, King's
  Mint) at a much higher build cost, with gold styling and a pulsing halo on the map.

### v0.24.0 — "The General"
- **Raider General** — a commander marches in behind the swarm on deep non-boss
  fifth waves (25, 35, …). Kill it and the whole wave is **routed** — every other
  raider on the field is cut down (you still collect their bounty). Let it break
  through to the keep and it lands a heavy blow and **rallies the horde** (a permanent
  War Drums escalation). High-stakes priority target.

### v0.23.0 — "Tactician"
- **Draft veto** — once per run, press X to bank a draft (take no card); the next
  draft then grants a double-pick instead. A reward for passing on a weak offer.

### v0.22.0 — "Fortified Ground"
- **Zone upgrades** — buy a "Fortify" improvement for any of the four buildable
  quadrants in the supply shop; every structure in that quadrant gets +15% output
  (tower damage, trap DPS, mine yield). A new gold sink that rewards clustering a
  quadrant and competes with expanding/hero upgrades.
- Fortified quadrants are tinted gold on the map; the upgrade persists in run saves.

### v0.21.0 — "Packlord"
- **New hero: Rurik, the Beastmaster** — a summoner. A loyal wolf fights at his side
  (a second body on the field), chasing and biting nearby enemies while he supports.
- **Rally Pack (signature)** — summon a burst of three temporary wolves to swarm a wave.
- **Pack / Wild skill branches** — Pack (Alpha = harder bites, Frenzy = faster bites) and
  Wild (Greater Pack = keep two loyal wolves, Maul = wolf bites slow their prey).
- Wolves are a new lightweight ally entity (`Companion` + `CompanionSystem`); they deal
  damage but don't take it, so enemy AI is unchanged.

### v0.20.0 — "Frostweaver"
- **New hero: Niva, the Elementalist** — a frost mage. Her bolts chill (slow) every
  enemy they hit, leaning into the existing slow / Glacier / Frostfire systems.
- **Frost Nova (signature)** — a radial burst that damages and deeply slows everything
  around her.
- **Frost / Storm skill branches** — Frost (Deep Freeze = stronger/longer Nova slow,
  Shatter = +35% hero damage to slowed foes) and Storm (Arc = bolts chain to a second
  enemy, Emberwind = Frost Nova also ignites).

### v0.19.0 — "Reaper"
- **New hero: Vess, the Executioner** — a glass-cannon assassin: high damage, fast,
  fragile (82 HP). Built around bursting down priority targets.
- **Execute (signature)** — blink to the weakest enemy in reach and strike; finishes
  off anything below the execute threshold instantly (bosses are immune to the
  instakill but still take a heavy hit). A natural counter to elites/bosses.
- **Assassin skill branches** — Reaping (Headsman = higher execute threshold, Reaping =
  executes refund the cooldown and drop gold) and Shadow (Shadowstep = faster dash with
  longer i-frames, Deathmark = +25% hero damage vs elites/bosses).

### v0.18.0 — "Bulwark"
- **New hero: Bram, the Bulwark** — a tank. Starts with far more HP (230 vs 100),
  hits a little softer and moves slower. A genuinely different playstyle built around
  body-blocking instead of damage.
- **Lane-blocking / taunt** — the Bulwark's body holds a lane: nearby enemies are
  taunted off the keep and funnel onto him (the novel bit — enemies used to ignore the
  hero entirely). Stand in a chokepoint to stem a push.
- **Bulwark Stance (signature)** — brace for a few seconds: heavy damage reduction and
  a much wider taunt that drags a whole pack onto your shield.
- **Tank skill branches** — Wall (Provoke = wider taunt, Thorns = reflect to attackers)
  and Guardian (Aegis = -18% damage taken, Anchor = longer stance that also slows
  attackers). Plus the shared Foundations spine.

### v0.17.0 — "Ascension"
- **Per-hero progression** — each hero (Ranger / Warden / Artificer) now levels
  independently with its own XP, skill points and skill tree. Switching (H) is still
  free, but your alts are under-invested — so you naturally specialise into a main
  while keeping tactical swaps as a real tradeoff. A short switch cooldown stops
  panic-juggling.
- **Skill trees** — spend a point each level in a tree of a shared "Foundations"
  spine (Vitality / Quick Hands / Toughness / Second Wind, identical for all heroes)
  plus two unique branches per hero. Opened with **K** (freezes the sim); click nodes
  to unlock. Old level-gated passives are now tree nodes.
- **Unique signature abilities** — Space now fires a hero-specific signature instead
  of one shared volley: Ranger **Volley** (Wide/Arrow Storm nodes), Warden **Ground
  Slam** (radial knockback + slow), Artificer **Overcharge** (fort-wide tower frenzy).
- **Hero branch identities** — Ranger Precision/Barrage (ricochet, pierce, wide
  volley, splash), Warden Cleave/Juggernaut (cleave, rend-slow, armor, lifesteal),
  Artificer Overclock/Construct (stronger+wider aura, faster repair, longer surge).
- Relics and gold-shop upgrades are now run-wide (apply to every hero kind).

### v0.16.2 — "Frontier Comforts" (QoL patch #2)
- **Wealth-scaled threat** — enemy HP (and, at half rate, damage) now ramps with a run's
  total accumulated gold (held + spent), 1.0x→2.0x, so a snowballing economy stays dangerous.
- **Run saves** — checkpoint autosave at each between-wave lull; the launch screen offers to
  resume ([Enter]) or start fresh ([N]). Cleared on game-over.
- **Shop cards cost their build** — structure cards bought from the shop are no longer
  pre-funded; you pay the shop price for the card, then fund its construction (and upgrades).
- **Draft mis-click guard** — a 0.75s grace before draft cards become selectable.
- **Shop key moved to B** — was S, which collided with move-down.
- **Readability** — brighter hero label; world no longer shakes during placement.

### v0.16.1 — "Frontier Comforts" (QoL patch #1)
- **Camera zoom** — mouse wheel zooms 0.5x–2.0x with smooth lerp.
- **View base during draft** — press V to dismiss the draft overlay and survey your defenses.
- **Structure tooltips** — hover any building to see its name, role stats, and effect.
- **Pause fix** — ESC no longer quits the game; it pauses (alongside P).

### v0.16.0 — "Codex Adept"
- **Codex completion reward** — discover 12+ synergies across all runs to earn a
  bonus free Gold Mine starter at the start of every future run.

### v0.15.0 — "Foresight"
- **Two-wave preview** — the draft now shows the next two waves (exact), so you can plan
  around incoming bosses/threats.
- **Kill-streak reward** — reaching the blazing streak tier grants an Overdrive burst.

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
