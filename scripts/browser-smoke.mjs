import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import { mkdir, mkdtemp, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import WebSocket from "ws";

const EDGE = "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe";
const port = 9300 + Math.floor(Math.random() * 500);
const profile = await mkdtemp(join(tmpdir(), "emberhold-edge-"));
const edge = spawn(EDGE, [
  "--headless",
  "--disable-gpu",
  "--no-first-run",
  "--window-size=1440,1000",
  `--remote-debugging-port=${port}`,
  `--user-data-dir=${profile}`,
  "http://localhost:4173",
], { stdio: "ignore", windowsHide: true });
process.on("exit", () => edge.kill());

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

async function retry(action, attempts = 30) {
  let error;
  for (let i = 0; i < attempts; i += 1) {
    try {
      return await action();
    } catch (nextError) {
      error = nextError;
      await sleep(150);
    }
  }
  throw error;
}

let socket;
try {
const page = await retry(async () => {
  const response = await fetch(`http://localhost:${port}/json`);
  if (!response.ok) throw new Error("CDP discovery not ready");
  const result = await response.json();
  const localGame = result.find((candidate) => candidate.url === "http://localhost:4173/");
  if (!localGame) throw new Error("Local game page not found");
  return localGame;
});

socket = new WebSocket(page.webSocketDebuggerUrl);
await new Promise((resolve, reject) => {
  socket.addEventListener("open", resolve, { once: true });
  socket.addEventListener("error", reject, { once: true });
});

let sequence = 0;
const pending = new Map();
const errors = [];
socket.addEventListener("message", ({ data }) => {
  const message = JSON.parse(data);
  if (message.method === "Runtime.exceptionThrown") errors.push(message.params.exceptionDetails.text);
  if (!message.id || !pending.has(message.id)) return;
  const { resolve, reject } = pending.get(message.id);
  pending.delete(message.id);
  if (message.error) reject(new Error(message.error.message));
  else resolve(message.result);
});

function call(method, params = {}) {
  const id = ++sequence;
  socket.send(JSON.stringify({ id, method, params }));
  return new Promise((resolve, reject) => pending.set(id, { resolve, reject }));
}

async function evaluate(expression) {
  const result = await call("Runtime.evaluate", { expression, returnByValue: true, awaitPromise: true });
  if (result.exceptionDetails) throw new Error(result.exceptionDetails.text);
  return result.result.value;
}

async function snapshot() {
  return evaluate("window.__emberhold.snapshot()");
}

await call("Runtime.enable");
await retry(async () => {
  const ready = await evaluate("Boolean(window.__emberhold)");
  if (!ready) throw new Error("Game runtime not ready");
});

const initial = await snapshot();
assert.equal(initial.stage, 1);
assert.equal(initial.towers, 0);
assert.equal(initial.barricades.length, 0);

const wallRaiderId = await evaluate("window.__emberhold.spawnWallRaider()");
await sleep(700);
assert.ok((await evaluate(`window.__emberhold.enemyPosition(${wallRaiderId}).y`)) < -140, "raider aimed through a solid fort wall should remain outside");
await evaluate("window.__emberhold.restart()");
await evaluate("window.__emberhold.moveHero(80, -100); window.__emberhold.setFacing(0, -1); window.__emberhold.dash()");
assert.ok((await snapshot()).hero.y > -128, "dash should stop on the near side of a solid fort wall");
await evaluate("window.__emberhold.restart()");
await evaluate("window.__emberhold.addGold(28); window.__emberhold.moveHero(0, -78)");
await sleep(2300);
assert.ok((await snapshot()).completed.includes("barricade-north"), "standing on a barricade pad should construct the wall");
await evaluate("window.__emberhold.complete('barricade-east'); window.__emberhold.complete('barricade-south'); window.__emberhold.complete('barricade-west')");
const barricaded = await snapshot();
assert.equal(barricaded.barricades.find((barricade) => barricade.side === 0).health, 180);
await call("Page.enable");
const barricadeScreenshot = await call("Page.captureScreenshot", { format: "png" });
await mkdir("tmp", { recursive: true });
await writeFile("tmp/barricade-smoke.png", Buffer.from(barricadeScreenshot.data, "base64"));
await evaluate("window.__emberhold.moveHero(80, -135)");
await sleep(80);
assert.equal(await evaluate("window.__emberhold.heroOverlapsSolid()"), false, "hero should resolve out of existing fort walls");
await evaluate("window.__emberhold.moveHero(0, -78)");
await sleep(80);
assert.equal(await evaluate("window.__emberhold.heroOverlapsSolid()"), false, "hero should resolve out of built barricades");
await evaluate("window.__emberhold.moveHero(0, 48); window.__emberhold.spawnGateRaider()");
await sleep(1000);
const barricadeHit = await snapshot();
assert.ok(barricadeHit.barricades.find((barricade) => barricade.side === 0).health < 180, "gate raider should damage a barricade");
assert.equal(barricadeHit.keepHealth, initial.keepHealth, "gate raider should hit the barricade before the keep");
await evaluate("window.__emberhold.damageBarricade(0, 9999)");
assert.ok(!(await snapshot()).completed.includes("barricade-north"), "breached barricade should return as a rebuildable pad");
await sleep(1100);
assert.ok((await snapshot()).keepHealth < barricadeHit.keepHealth, "raider should advance to the keep only after breaching the barricade");
await evaluate("window.__emberhold.complete('barricade-north')");
const rebuiltBarricade = await snapshot();
assert.equal(rebuiltBarricade.barricades.find((barricade) => barricade.side === 0).health, 180);
await evaluate("window.__emberhold.restart()");

await evaluate("window.__emberhold.complete('tower-west'); window.__emberhold.complete('mine-east'); window.__emberhold.complete('expand-2')");
const built = await snapshot();
assert.equal(built.stage, 2);
assert.equal(built.towers, 1);
assert.equal(built.mines, 1);
assert.ok(built.completed.includes("expand-2"));

await evaluate("window.__emberhold.complete('tower-west-2')");
const upgraded = await snapshot();
assert.equal(upgraded.towers, built.towers);
assert.equal(upgraded.towerLevels.find((tower) => tower.id === "tower-west").level, 2);

await evaluate("window.__emberhold.addGold(45); window.__emberhold.moveHero(0, 112)");
await sleep(300);
assert.equal(await evaluate("window.__emberhold.padInvestment('training')"), 0, "crossing a pad briefly should not spend gold");
await sleep(300);
const investingTitle = await evaluate("document.querySelector('#mission-title').textContent");
assert.equal(investingTitle, "INVEST IN BOW TRAINING");
await sleep(2800);
const trained = await snapshot();
assert.equal(await evaluate("window.__emberhold.padInvestment('training')"), 45);
assert.equal(trained.heroLevel, 2);
assert.ok(trained.completed.includes("training"));

await evaluate("window.__emberhold.grantXp(8)");
const progressed = await snapshot();
assert.ok(progressed.heroLevel > trained.heroLevel);
assert.ok(progressed.heroDamage > trained.heroDamage);

await evaluate("window.__emberhold.complete('expand-3'); window.__emberhold.complete('armory'); window.__emberhold.complete('banner-west')");
const fortified = await snapshot();
assert.equal(fortified.stage, 3);
assert.equal(fortified.towerPower, 1.35);
assert.equal(fortified.armories, 1);
assert.equal(fortified.banners, 1);
const screenshot = await call("Page.captureScreenshot", { format: "png" });
await writeFile("tmp/fortified-smoke.png", Buffer.from(screenshot.data, "base64"));

await evaluate("window.__emberhold.damageKeep(100); window.__emberhold.complete('repair-yard'); window.__emberhold.complete('cannon-south')");
const repaired = await snapshot();
assert.ok(repaired.keepHealth > fortified.keepHealth);
assert.equal(repaired.towers, 2);

await evaluate(`(() => {
  const canvas = document.querySelector("#game-canvas");
  const rect = canvas.getBoundingClientRect();
  canvas.dispatchEvent(new PointerEvent("pointerdown", {
    clientX: rect.left + rect.width / 2 - 100,
    clientY: rect.top + rect.height / 2 + 112,
  }));
})()`);
await sleep(520);
const clicked = await snapshot();
assert.ok(clicked.hero.x < fortified.hero.x, "hero should respond to click-to-move");

await call("Input.dispatchKeyEvent", { type: "keyDown", code: "KeyD", key: "d" });
await sleep(250);
await call("Input.dispatchKeyEvent", { type: "keyUp", code: "KeyD", key: "d" });
const moved = await snapshot();
assert.ok(moved.hero.x > clicked.hero.x, "hero should respond to keyboard movement");

await call("Input.dispatchKeyEvent", { type: "keyDown", code: "Space", key: " " });
await call("Input.dispatchKeyEvent", { type: "keyUp", code: "Space", key: " " });
const volley = await snapshot();
assert.ok(volley.abilityCooldown > 7, "volley should begin its cooldown");

await sleep(4300);
const running = await snapshot();
assert.ok(running.drops > 0 || running.gold > trained.gold, "mine should generate collectible or collected gold");
assert.ok(running.enemies > 0 || running.kills > 0, "waves should create combat pressure");
await evaluate("window.__emberhold.damageKeep(70); window.__emberhold.clearWave()");
await sleep(120);
const cleared = await snapshot();
assert.equal(cleared.wave, 2);
assert.ok(cleared.drops > running.drops || cleared.gold > running.gold, "wave clear should award keep-side gold");
assert.equal(cleared.keepHealth, repaired.keepHealth - 34, "repair yard should restore 36 keep integrity after a held wave");

await evaluate("window.__emberhold.complete('expand-4'); window.__emberhold.complete('ember-shrine'); window.__emberhold.complete('warden-lodge'); window.__emberhold.moveHero(400, 0)");
await sleep(620);
const frontier = await snapshot();
assert.equal(frontier.stage, 4);
assert.equal(frontier.volleyCooldown, 5.8);
assert.ok(frontier.heroes.includes("warden"));
assert.equal(frontier.heroKind, "warden");
assert.ok(frontier.camera.x > 100, "camera should follow the hero across an expanded frontier");
const frontierScreenshot = await call("Page.captureScreenshot", { format: "png" });
await writeFile("tmp/frontier-smoke.png", Buffer.from(frontierScreenshot.data, "base64"));

await evaluate("window.dispatchEvent(new KeyboardEvent('keydown', { code: 'KeyH', repeat: true }))");
assert.equal((await snapshot()).heroKind, "warden", "held hero-switch key should not rapidly toggle profiles");
await evaluate("window.__emberhold.switchHero()");
const swapped = await snapshot();
assert.equal(swapped.heroKind, "ranger");

await evaluate("window.__emberhold.lose()");
const defeated = await snapshot();
assert.equal(defeated.over, true);
assert.ok(defeated.bestWave >= 2);
assert.equal(await evaluate("document.activeElement.id"), "restart-button");
assert.equal(await evaluate("document.querySelector('#game-over').getAttribute('role')"), "dialog");
assert.equal(await evaluate("document.querySelector('#game-over').getAttribute('aria-modal')"), "true");
await evaluate("window.__emberhold.restart()");
const restarted = await snapshot();
assert.equal(restarted.over, false);
assert.equal(restarted.stage, 1);
assert.ok(restarted.bestWave >= 2, "best wave should persist after restart");
assert.equal(await evaluate("document.querySelector('#pause-button').textContent"), "PAUSE");
assert.equal(await evaluate("document.querySelector('#boon-summary').textContent"), "NO FRONTIER BOONS YET");
assert.equal(await evaluate("document.activeElement.id"), "game-canvas");
await evaluate("window.__emberhold.moveHero(0, 0); window.__emberhold.spawnGold(200, 0)");
const distantGoldX = await evaluate("window.__emberhold.goldDrops().at(-1).x");
await sleep(220);
const attractedGoldX = await evaluate("window.__emberhold.goldDrops().at(-1).x");
assert.ok(attractedGoldX < distantGoldX - 8, "nearby gold should visibly attract toward the hero");
await evaluate("window.__emberhold.moveHero(9999, 0)");
await sleep(320);
const roaming = await snapshot();
assert.equal(roaming.hero.x, 402, "stage-one roam should extend beyond the old 267-unit boundary");
assert.ok(roaming.camera.x > 100, "camera should follow early-frontier collection runs");
await evaluate("window.__emberhold.restart()");
const beforeDash = await snapshot();
await call("Input.dispatchKeyEvent", { type: "keyDown", code: "ShiftLeft", key: "Shift" });
await call("Input.dispatchKeyEvent", { type: "keyUp", code: "ShiftLeft", key: "Shift" });
const dashed = await snapshot();
assert.ok(Math.hypot(dashed.hero.x - beforeDash.hero.x, dashed.hero.y - beforeDash.hero.y) > 80, "shift should dash the hero along their facing direction");
assert.ok(dashed.dashCooldown > 2, "dash should begin its cooldown");
await evaluate("window.dispatchEvent(new KeyboardEvent('keydown', { code: 'ShiftLeft', repeat: true }))");
assert.deepEqual((await snapshot()).hero, dashed.hero, "held dash key should not repeatedly move the hero");
await evaluate("window.__emberhold.restart()");

await call("Input.dispatchKeyEvent", { type: "keyDown", code: "KeyP", key: "p" });
await call("Input.dispatchKeyEvent", { type: "keyUp", code: "KeyP", key: "p" });
const paused = await snapshot();
assert.equal(paused.paused, true);
await evaluate("window.dispatchEvent(new KeyboardEvent('keydown', { code: 'KeyP', repeat: true }))");
assert.equal((await snapshot()).paused, true, "held pause key should not rapidly toggle state");
await call("Input.dispatchKeyEvent", { type: "keyDown", code: "KeyP", key: "p" });
await call("Input.dispatchKeyEvent", { type: "keyUp", code: "KeyP", key: "p" });
const resumed = await snapshot();
assert.equal(resumed.paused, false);
await call("Input.dispatchKeyEvent", { type: "keyDown", code: "Escape", key: "Escape" });
await call("Input.dispatchKeyEvent", { type: "keyUp", code: "Escape", key: "Escape" });
assert.equal((await snapshot()).paused, true, "escape should pause the game");
await call("Input.dispatchKeyEvent", { type: "keyDown", code: "Escape", key: "Escape" });
await call("Input.dispatchKeyEvent", { type: "keyUp", code: "Escape", key: "Escape" });
assert.equal((await snapshot()).paused, false, "escape should resume the game");

await evaluate("window.__emberhold.offerBoon()");
const offered = await snapshot();
assert.equal(offered.paused, true);
assert.equal(await evaluate("document.activeElement.dataset.boon"), "ranger");
assert.equal(await evaluate("document.querySelector('#boon-draft').getAttribute('role')"), "dialog");
assert.equal(await evaluate("document.querySelector('#boon-draft').getAttribute('aria-modal')"), "true");
const boonScreenshot = await call("Page.captureScreenshot", { format: "png" });
await writeFile("tmp/boon-smoke.png", Buffer.from(boonScreenshot.data, "base64"));
await call("Input.dispatchKeyEvent", { type: "keyDown", code: "KeyP", key: "p" });
await call("Input.dispatchKeyEvent", { type: "keyUp", code: "KeyP", key: "p" });
const boonLocked = await snapshot();
assert.equal(boonLocked.paused, true, "boon draft should stay paused until a choice is made");
await evaluate("document.querySelector('[data-boon=\"tower\"]').click()");
await sleep(50);
const boonClaimed = await snapshot();
assert.equal(boonClaimed.paused, false);
assert.equal(boonClaimed.boons.at(-1), "tower");
assert.equal(boonClaimed.towerPower, 1.22);
assert.equal(await evaluate("document.querySelector('#boon-summary').textContent"), "BOONS: TOWER +1");
assert.equal(await evaluate("document.activeElement.id"), "game-canvas");

await evaluate("window.__emberhold.spawnGateRaider()");
await sleep(1800);
const breached = await snapshot();
assert.ok(breached.keepHealth < restarted.keepHealth, "gate raider should reach and damage the keep");

await evaluate("window.__emberhold.spawnEmber()");
await sleep(120);
const empowered = await snapshot();
assert.ok(empowered.overdrive > 9, "ember sigil should activate hero overdrive");
await evaluate("document.querySelector('#ability-button').click()");
const tapped = await snapshot();
assert.ok(tapped.abilityCooldown > 7, "tappable ability control should fire volley");

await call("Emulation.setDeviceMetricsOverride", { width: 430, height: 900, deviceScaleFactor: 1, mobile: false });
const mobileLayout = await evaluate(`(() => {
  const pauseRect = document.querySelector("#pause-button").getBoundingClientRect();
  const dashRect = document.querySelector("#dash-button").getBoundingClientRect();
  return {
    pause: { left: pauseRect.left, right: pauseRect.right, top: pauseRect.top, width: pauseRect.width },
    dash: { left: dashRect.left, right: dashRect.right, top: dashRect.top, bottom: dashRect.bottom, width: dashRect.width },
  };
})()`);
assert.ok(mobileLayout.pause.width > 0);
assert.ok(mobileLayout.pause.left >= 0 && mobileLayout.pause.right <= 430, `mobile pause control should remain inside viewport: ${JSON.stringify(mobileLayout)}`);
assert.ok(mobileLayout.dash.left >= 0 && mobileLayout.dash.right <= 430, `mobile dash control should remain inside viewport: ${JSON.stringify(mobileLayout)}`);
assert.ok(mobileLayout.dash.top >= 0 && mobileLayout.dash.bottom <= 900, `mobile dash control should fit vertically: ${JSON.stringify(mobileLayout)}`);
await evaluate("document.querySelector('#dash-button').click()");
assert.ok((await snapshot()).dashCooldown > 2, "tappable dash control should trigger the movement cooldown");
await sleep(50);
assert.equal(await evaluate("document.querySelector('#dash-button').disabled"), true);
await evaluate("window.__emberhold.spawnGold(window.__emberhold.snapshot().hero.x + 700, window.__emberhold.snapshot().hero.y)");
const offscreenGold = await evaluate("window.__emberhold.offscreenGold()");
assert.ok(offscreenGold > 0, "off-camera gold should register for the loot beacon");
await evaluate("window.__emberhold.moveHero(9999, 0)");
await sleep(320);
const keepBeacon = await evaluate("window.__emberhold.offscreenKeep()");
assert.equal(keepBeacon, true, "distant collection runs should register the keep beacon");
const mobileScreenshot = await call("Page.captureScreenshot", { format: "png" });
await writeFile("tmp/mobile-smoke.png", Buffer.from(mobileScreenshot.data, "base64"));
await evaluate("window.__emberhold.moveHero(0, 0)");
await evaluate("window.__emberhold.offerBoon()");
const mobileBoonLayout = await evaluate(`(() => {
  const rect = document.querySelector("#boon-draft").getBoundingClientRect();
  return { left: rect.left, right: rect.right, top: rect.top, bottom: rect.bottom, width: rect.width };
})()`);
assert.ok(mobileBoonLayout.left >= 0 && mobileBoonLayout.right <= 430, `mobile boon draft should remain inside viewport: ${JSON.stringify(mobileBoonLayout)}`);
assert.ok(mobileBoonLayout.top >= 0 && mobileBoonLayout.bottom <= 900, `mobile boon draft should fit vertically: ${JSON.stringify(mobileBoonLayout)}`);
const mobileBoonScreenshot = await call("Page.captureScreenshot", { format: "png" });
await writeFile("tmp/mobile-boon-smoke.png", Buffer.from(mobileBoonScreenshot.data, "base64"));
await evaluate("document.querySelector('[data-boon=\"keep\"]').click()");
for (let index = 0; index < 3; index += 1) {
  await evaluate("window.__emberhold.clearWave()");
  await sleep(90);
}
const automaticBoon = await snapshot();
assert.equal(automaticBoon.wave, 4);
assert.equal(automaticBoon.paused, true, "third cleared wave should automatically offer a boon");
await evaluate("document.querySelector('[data-boon=\"ranger\"]').click()");
const splashKills = await evaluate("window.__emberhold.testSplash()");
assert.equal(splashKills, 2, "cannon splash should run the shared kill path for nearby enemies");
await evaluate("localStorage.setItem('emberhold-best-wave', 'invalid'); window.__emberhold.restart()");
assert.equal((await snapshot()).bestWave, 1, "malformed persisted best-wave data should fall back safely");
assert.deepEqual(errors, []);

console.log(JSON.stringify({
  initial: { stage: initial.stage, wave: initial.wave },
  build: {
    stage: frontier.stage,
    completed: frontier.completed.length,
    towers: repaired.towers,
    towerLevels: upgraded.towerLevels,
    heroes: frontier.heroes,
  },
  combat: {
    kills: running.kills,
    keepAfterBreach: breached.keepHealth,
    overdrive: empowered.overdrive,
    splashKills,
  },
  defense: {
    cardinalBarricades: barricaded.barricades.length,
    barricadeHealthAfterRaid: barricadeHit.barricades.find((barricade) => barricade.side === 0).health,
    rebuiltBarricadeHealth: rebuiltBarricade.barricades.find((barricade) => barricade.side === 0).health,
    padDwellGrace: true,
  },
  progression: {
    trainedLevel: trained.heroLevel,
    xpLevel: progressed.heroLevel,
    boon: boonClaimed.boons.at(-1),
    automaticBoonWave: automaticBoon.wave,
  },
  collection: {
    attractionDelta: Math.round(distantGoldX - attractedGoldX),
    roamX: roaming.hero.x,
    earlyCameraX: roaming.camera.x,
    dashCooldown: dashed.dashCooldown,
    offscreenGold,
    keepBeacon,
  },
  responsive: { controls: mobileLayout, boon: mobileBoonLayout },
  lifecycle: { bestWave: restarted.bestWave, restartedStage: restarted.stage },
}, null, 2));

} finally {
  socket?.close();
  edge.kill();
  await new Promise((resolve) => {
    if (edge.exitCode !== null) resolve();
    else {
      edge.once("exit", resolve);
      setTimeout(resolve, 1200);
    }
  });
  await rm(profile, { recursive: true, force: true, maxRetries: 3, retryDelay: 180 }).catch(() => {});
}
