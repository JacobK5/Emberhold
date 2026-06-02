import test from "node:test";
import assert from "node:assert/strict";
import { attractionSpeed, clamp, depositAmount, nearest, normalize, waveStats } from "../src/core.js";

test("normalize handles unit vectors and zero length", () => {
  assert.deepEqual(normalize(0, 0), { x: 0, y: 0 });
  assert.deepEqual(normalize(3, 4), { x: 0.6, y: 0.8 });
});

test("nearest respects predicate", () => {
  const items = [{ x: 2, y: 0, alive: false }, { x: 5, y: 0, alive: true }];
  assert.equal(nearest({ x: 0, y: 0 }, items), items[0]);
  assert.equal(nearest({ x: 0, y: 0 }, items, (item) => item.alive), items[1]);
});

test("wave stats escalate and elite waves land every fifth wave", () => {
  assert.ok(waveStats(10).health > waveStats(1).health);
  assert.ok(waveStats(10).count > waveStats(1).count);
  assert.equal(waveStats(4).elite, false);
  assert.equal(waveStats(5).elite, true);
  assert.equal(waveStats(10).elite, true);
});

test("late waves keep scaling while movement and cadence stay capped", () => {
  const wave50 = waveStats(50);
  const wave100 = waveStats(100);
  assert.ok(wave100.health > wave50.health);
  assert.ok(wave100.damage > wave50.damage);
  assert.ok(wave100.count > wave50.count);
  assert.ok(wave100.reward > wave50.reward);
  assert.equal(wave100.speed, 89);
  assert.equal(wave100.interval, 0.34);
});

test("deposit amount stays within available gold and remaining cost", () => {
  assert.equal(depositAmount(50, 20, 30, 0.5), 15);
  assert.equal(depositAmount(3, 20, 30, 0.5), 3);
  assert.equal(depositAmount(50, 2, 30, 0.5), 2);
  assert.equal(depositAmount(50, 20, 30, 0.016), 0);
  assert.equal(clamp(20, 0, 10), 10);
});

test("coin attraction eases nearby loot toward the hero without pulling distant drops", () => {
  assert.equal(attractionSpeed(0), 0);
  assert.equal(attractionSpeed(112), 0);
  assert.equal(attractionSpeed(150), 0);
  assert.ok(attractionSpeed(40) > attractionSpeed(100));
  assert.ok(attractionSpeed(200, 245) > 0);
  assert.equal(attractionSpeed(245, 245), 0);
});
