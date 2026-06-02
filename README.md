# Emberhold

Emberhold is an original action-defense **strategy** game: defend a keep, collect dropped
gold, build a fort, and survive escalating raids. The strategy comes from a roguelike
**draft** (choose your buildings), **hand placement** within the fort, and **cross-building
synergies** that reward unique combinations over going deep on one path.

This repository is mid-migration from a browser prototype to a native desktop build:

- **`Emberhold/`** — the current game. **C# + Raylib-cs**, all code, procedural rendering,
  no engine/editor. This is what's being actively developed (targets Steam eventually).
- **`Emberhold.Tests/`** — xUnit tests for the math, economy, placement, and synergy logic.
- **`src/`, `index.html`, `styles.css`** — the original JavaScript/canvas prototype, kept as
  a porting reference. Its docs are in the git history.

See [GAME_DESIGN.md](./GAME_DESIGN.md) for the full design spec (draft, placement, the
two-layer synergy system, the card pool, economy, and map).

## Run (C# build)

Requires the .NET 8 SDK.

```powershell
dotnet run --project Emberhold/Emberhold.csproj
```

A headless smoke mode runs a fixed number of frames and can capture a screenshot — used for
automated verification of a GUI app:

```powershell
# run 120 frames then exit; optionally write a screenshot
dotnet run --project Emberhold/Emberhold.csproj -- --smoke 120 --shot shot.png
# --auto  auto-resolves the draft/placement phases
# --seed  seeds a debug fort and starts straight in combat
```

## Test

```powershell
dotnet test
```

## Controls

- Move with `WASD` / arrow keys, or click a destination.
- `SPACE` fires the hero's Ember Volley; `SHIFT` dashes.
- `H` switches hero (Ash the Ranger ↔ Mira the Warden — faster fire/more damage vs. speed/range).
- `C` opens the synergy codex; `P` / `Escape` pauses.
- **Draft:** press `1` / `2` / `3` or click a card to choose one of Attack / Defend / Support.
- **Placement:** click to drop the current building — Attack/Support snap into the quadrant
  zones, Defend (walls/traps) onto the lanes. A green/red ghost shows valid spots.

## Core Loop

1. **Draft** one card from three (one per category) at the start and on each expansion.
2. **Place** your buildings by hand inside the fort's zones and lanes (locked once placed).
3. **Fight:** the hero auto-fires; run the field collecting gold and stand on pads to fund
   construction (build time scales with √cost). Towers, traps, and auras do the rest.
4. **Synergize:** field synergies (adjacency, e.g. a Cannon covering a Tar Pit) and keystone
   synergies (owning a pair, e.g. Frost Spire + Forge) make combinations stronger than the
   sum of their parts.
5. **Expand** the fort to grow your buildable space and open the next draft. Survive forever.
