import test from "node:test";
import assert from "node:assert/strict";
import { BUILD_DEFS, ENEMY_PROFILES, HERO_PROFILES } from "../src/config.js";

test("build pad ids stay unique and costs stay positive", () => {
  const ids = BUILD_DEFS.map((pad) => pad.id);
  assert.equal(new Set(ids).size, ids.length);
  assert.ok(BUILD_DEFS.every((pad) => pad.cost > 0));
});

test("each fort stage exposes the next expansion at an increasing cost", () => {
  const expansions = BUILD_DEFS
    .filter((pad) => pad.type === "expand")
    .sort((a, b) => a.stage - b.stage);
  assert.deepEqual(expansions.map((pad) => pad.stage), [1, 2, 3]);
  assert.deepEqual(expansions.map((pad) => pad.id), ["expand-2", "expand-3", "expand-4"]);
  assert.ok(expansions.every((pad, index) => index === 0 || pad.cost > expansions[index - 1].cost));
});

test("special late-game unlocks remain stage-four rewards", () => {
  const shrine = BUILD_DEFS.find((pad) => pad.id === "ember-shrine");
  const lodge = BUILD_DEFS.find((pad) => pad.id === "warden-lodge");
  assert.equal(shrine?.stage, 4);
  assert.equal(lodge?.stage, 4);
});

test("the stage-two repair yard remains an explicit sustain purchase", () => {
  const repairYard = BUILD_DEFS.find((pad) => pad.id === "repair-yard");
  assert.equal(repairYard?.type, "repair");
  assert.equal(repairYard?.stage, 2);
});

test("cardinal barricade pads cover each approach lane", () => {
  const barricades = BUILD_DEFS.filter((pad) => pad.type === "barricade");
  assert.equal(barricades.length, 4);
  assert.deepEqual(barricades.map((pad) => pad.side).sort(), [0, 1, 2, 3]);
  assert.ok(barricades.every((pad) => pad.stage === 1));
});

test("combat profiles preserve meaningful tradeoffs", () => {
  assert.ok(ENEMY_PROFILES.runner.speed > ENEMY_PROFILES.raider.speed);
  assert.ok(ENEMY_PROFILES.brute.health > ENEMY_PROFILES.raider.health);
  assert.ok(ENEMY_PROFILES.elite.reward > ENEMY_PROFILES.brute.reward);
  assert.ok(HERO_PROFILES.warden.damage > HERO_PROFILES.ranger.damage);
  assert.ok(HERO_PROFILES.warden.speed < HERO_PROFILES.ranger.speed);
});

test("dependent upgrade pads reference existing build pads", () => {
  const pads = new Map(BUILD_DEFS.map((pad) => [pad.id, pad]));
  const upgrades = BUILD_DEFS.filter((pad) => pad.requires);
  assert.ok(upgrades.length > 0);
  assert.ok(upgrades.every((pad) => pads.has(pad.requires)));
  assert.ok(upgrades.every((pad) => pads.get(pad.requires).stage <= pad.stage));
});

test("combat profile multipliers remain positive", () => {
  const multipliers = [...Object.values(ENEMY_PROFILES), ...Object.values(HERO_PROFILES)];
  assert.ok(multipliers.every((profile) => Object.values(profile).every((value) => typeof value !== "number" || value > 0)));
});
