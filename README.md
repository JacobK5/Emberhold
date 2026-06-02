# Emberhold

Emberhold is an original browser action-defense prototype inspired by the satisfying loop
shown in mobile strategy ads: defend a keep, collect dropped gold, stand on build pads, and
grow a small camp into an automated fortress.

The current build includes four fort stages, mines, archer towers, cannons, support banners,
tower upgrades, buildable barricades, solid wall collision, between-wave keep repairs, elite
raids, temporary Ember Sigil power-ups, between-wave boon drafts, hero XP, range-based coin
attraction, frontier navigation beacons, and an unlockable Warden hero.

## Run

```powershell
npm install
npm start
```

Open `http://localhost:4173`.

## Controls

- Move with `WASD`, arrow keys, or a click/tap destination.
- Press `Shift` or tap the on-canvas control for a short collection-run dash.
- The deployed hero automatically shoots the nearest raider in range.
- Press `Space` to fire Ember Volley when its cooldown is ready.
- After unlocking the Warden Lodge, click the hero portrait or press `H` to switch heroes.
- Hold position on glowing pads briefly to invest carried gold.
- Press `P`, `Escape`, or use the HUD button to pause.

## Gameplay Guide

1. Defeat incoming raiders with the hero's automatic ranged attacks and Ember Volley.
2. Leave the walls to collect dropped gold. Coins within the deployed hero's shooting range
   drift toward the hero, and edge beacons point toward off-screen loot and back toward the
   keep during longer runs.
3. Stand on glowing pads to invest carried gold into archer towers, mines, training,
   expansions, support banners, cannons, barricades, the armory, repairs, and late-game
   unlocks. Pads wait half a second before spending so crossing one does not drain gold.
4. Expand the walls three times to reveal the full four-stage frontier. Mines create
   collectible gold automatically while towers defend the keep.
5. Survive increasingly dense waves. Every fifth wave adds an elite carrying an Ember Sigil
   overdrive pickup, and every third cleared wave pauses for a three-way boon choice.

Ash the Ranger is the starting hero. The stage-four Warden Lodge unlocks Mira the Warden,
who trades some speed and range for heavier attacks. The Repair Yard restores keep integrity
after held waves, banners improve nearby towers, the armory improves all towers, and level-II
pads upgrade specific defenses without adding map clutter. Buildable cardinal barricades
block gate lanes, absorb raider attacks before the keep, and return as rebuild pads when
destroyed. Heroes and raiders cannot walk through fort walls or active barricades.

## Verify

```powershell
npm run check
npm test
npm run smoke
```

The smoke test launches a headless Edge session, plays through representative state
transitions, and writes barricade, desktop, frontier, boon, and mobile visual QA captures
under `tmp/`. Keep `npm start` running in another terminal for the smoke test. Run all checks
together with `npm run verify`.

See [DESIGN.md](./DESIGN.md) for the core loop, architecture, roadmap, and decision log.
