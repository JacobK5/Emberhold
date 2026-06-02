import { attractionSpeed, clamp, depositAmount, distance, nearest, normalize, waveStats } from "./core.js";
import { BUILD_DEFS, COLORS, ENEMY_PROFILES, HERO_PROFILES, TAU } from "./config.js";

const TUNING_DEFAULTS = {
  enemyHealthMult:    0.88,
  enemySpeedMult:     1,
  enemyDamageMult:    1,
  enemyCountMult:     0.9,
  goldRewardMult:     1.3,
  heroDamageMult:     1,
  heroSpeedMult:      1,
  heroRangeMult:      1,
  heroFireSpeedMult:  1,
  towerDamageMult:    1,
  towerFireSpeedMult: 1,
  mineSpeedMult:      1,
  depositSpeedMult:   1,
};

const TUNING = { ...TUNING_DEFAULTS };

const TUNING_TABS = [
  { id: "hero",    label: "HERO" },
  { id: "enemies", label: "ENEMIES" },
  { id: "towers",  label: "TOWERS" },
  { id: "economy", label: "ECONOMY" },
  { id: "data",    label: "IMPORT / EXPORT" },
];

const TUNING_DEFS = [
  { key: "heroDamageMult",     tab: "hero",    label: "Hero Damage",      min: 0.5,  max: 4,   step: 0.05, hint: "Scales all hero shot damage" },
  { key: "heroSpeedMult",      tab: "hero",    label: "Hero Speed",       min: 0.5,  max: 2,   step: 0.05, hint: "Movement speed multiplier" },
  { key: "heroRangeMult",      tab: "hero",    label: "Hero Range",       min: 0.5,  max: 3,   step: 0.1,  hint: "Auto-fire and coin attraction range" },
  { key: "heroFireSpeedMult",  tab: "hero",    label: "Hero Fire Speed",  min: 0.5,  max: 4,   step: 0.05, hint: "Shots per second — higher is faster" },
  { key: "enemyHealthMult",    tab: "enemies", label: "Enemy Health",     min: 0.25, max: 3,   step: 0.05, hint: "Scales all enemy HP at spawn" },
  { key: "enemySpeedMult",     tab: "enemies", label: "Enemy Speed",      min: 0.4,  max: 2,   step: 0.05, hint: "Scales all enemy movement speed" },
  { key: "enemyDamageMult",    tab: "enemies", label: "Enemy Damage",     min: 0.25, max: 3,   step: 0.05, hint: "Scales all enemy hit and attack damage" },
  { key: "enemyCountMult",     tab: "enemies", label: "Enemy Count",      min: 0.25, max: 3,   step: 0.05, hint: "Enemies spawned per wave" },
  { key: "goldRewardMult",     tab: "enemies", label: "Gold per Kill",    min: 0.5,  max: 4,   step: 0.1,  hint: "Gold coin drops per defeated enemy" },
  { key: "towerDamageMult",    tab: "towers",  label: "Tower Damage",     min: 0.5,  max: 4,   step: 0.1,  hint: "Applies to all towers and cannons" },
  { key: "towerFireSpeedMult", tab: "towers",  label: "Tower Fire Speed", min: 0.5,  max: 4,   step: 0.1,  hint: "Higher = faster firing cadence" },
  { key: "mineSpeedMult",      tab: "economy", label: "Mine Drop Rate",   min: 0.25, max: 6,   step: 0.25, hint: "How often mines produce gold" },
  { key: "depositSpeedMult",   tab: "economy", label: "Deposit Speed",    min: 0.25, max: 4,   step: 0.25, hint: "Rate of investing gold into build pads" },
];

const canvas = document.querySelector("#game-canvas");
const ctx = canvas.getContext("2d");
const ui = {
  gold: document.querySelector("#gold"),
  wave: document.querySelector("#wave"),
  waveDetail: document.querySelector("#wave-detail"),
  stage: document.querySelector("#stage"),
  bestWave: document.querySelector("#best-wave"),
  heroHealth: document.querySelector("#hero-health"),
  heroLabel: document.querySelector("#hero-label"),
  heroXp: document.querySelector("#hero-xp"),
  heroSwitch: document.querySelector("#hero-switch"),
  heroLevel: document.querySelector("#hero-level"),
  keepHealth: document.querySelector("#keep-health"),
  keepHealthText: document.querySelector("#keep-health-text"),
  abilityStatus: document.querySelector("#ability-status"),
  ability: document.querySelector("#ability-button"),
  dashStatus: document.querySelector("#dash-status"),
  dash: document.querySelector("#dash-button"),
  intro: document.querySelector("#intro"),
  toast: document.querySelector("#toast"),
  bossBanner: document.querySelector("#boss-banner"),
  gameOver: document.querySelector("#game-over"),
  gameOverStats: document.querySelector("#game-over-stats"),
  missionTitle: document.querySelector("#mission-title"),
  missionDetail: document.querySelector("#mission-detail"),
  boonSummary: document.querySelector("#boon-summary"),
  pause: document.querySelector("#pause-button"),
  sound: document.querySelector("#sound-button"),
  restart: document.querySelector("#restart-button"),
  boonDraft: document.querySelector("#boon-draft"),
  settings: document.querySelector("#settings"),
  settingsResume: document.querySelector("#settings-resume"),
  settingsTabs: document.querySelector("#settings-tabs"),
  settingsBody: document.querySelector("#settings-body"),
};

const keys = new Set();
let state;
let lastFrame = performance.now();
let nextId = 1;
let toastTimer = 0;
let introTimer = 9;
let pointerTarget = null;
let audioContext = null;
let soundEnabled = false;
let lastPickupTone = 0;

function createState() {
  const savedBestWave = Number.parseInt(localStorage.getItem("emberhold-best-wave") ?? "1", 10);
  const hero = {
    x: 0, y: 48, radius: 12, speed: 150, health: 100, maxHealth: 100,
    damage: 14, fireRate: 0.56, shotTimer: 0, range: 245, level: 1,
    xp: 0, nextXp: 8,
    facing: { x: 0, y: -1 }, invulnerable: 0, abilityCooldown: 0,
    volleyCooldown: 8, volleyDamage: 1.2, dashCooldown: 0, overdrive: 0,
    kind: "ranger",
  };
  return {
    hero,
    camera: { x: 0, y: 0 },
    keep: { x: 0, y: 0, radius: 34, health: 260, maxHealth: 260 },
    stage: 1,
    gold: 10,
    wave: 1,
    spawning: null,
    betweenWaves: 0,
    waveBonusPending: false,
    elapsed: 0,
    kills: 0,
    completed: new Set(),
    pads: BUILD_DEFS.map((definition) => ({ ...definition, invested: 0 })),
    enemies: [],
    projectiles: [],
    drops: [],
    particles: [],
    floaters: [],
    towers: [],
    mines: [],
    banners: [],
    armories: [],
    shrines: [],
    lodges: [],
    barricades: [],
    heroes: new Set(["ranger"]),
    towerPower: 1,
    boons: [],
    shake: 0,
    paused: false,
    over: false,
    upgradeBreak: false,
    bestWave: Number.isFinite(savedBestWave) ? Math.max(1, savedBestWave) : 1,
  };
}

function playTone(frequency, duration = 0.08, type = "square", volume = 0.035) {
  if (!soundEnabled || !audioContext) return;
  const oscillator = audioContext.createOscillator();
  const gain = audioContext.createGain();
  oscillator.type = type;
  oscillator.frequency.value = frequency;
  gain.gain.setValueAtTime(volume, audioContext.currentTime);
  gain.gain.exponentialRampToValueAtTime(0.001, audioContext.currentTime + duration);
  oscillator.connect(gain);
  gain.connect(audioContext.destination);
  oscillator.start();
  oscillator.stop(audioContext.currentTime + duration);
}

function resize() {
  const rect = canvas.getBoundingClientRect();
  const ratio = window.devicePixelRatio || 1;
  canvas.width = Math.round(rect.width * ratio);
  canvas.height = Math.round(rect.height * ratio);
  ctx.setTransform(ratio, 0, 0, ratio, 0, 0);
}

const view = () => ({ x: canvas.clientWidth / 2, y: canvas.clientHeight / 2 });
const worldToScreen = (x, y) => ({ x: x - state.camera.x + view().x, y: y - state.camera.y + view().y });

function fortHalfSize(stage = state.stage) {
  return 142 + (stage - 1) * 82;
}

function roamLimit() {
  return fortHalfSize() + 260;
}

function padIsAvailable(pad) {
  return pad.stage <= state.stage && (!pad.requires || state.completed.has(pad.requires));
}

function wallRects() {
  const half = fortHalfSize();
  const thickness = 14;
  const gate = 55;
  return [
    { x: -half, y: -half, w: half - gate, h: thickness },
    { x: gate, y: -half, w: half - gate, h: thickness },
    { x: -half, y: half - thickness, w: half - gate, h: thickness },
    { x: gate, y: half - thickness, w: half - gate, h: thickness },
    { x: -half, y: -half, w: thickness, h: half - gate },
    { x: -half, y: gate, w: thickness, h: half - gate },
    { x: half - thickness, y: -half, w: thickness, h: half - gate },
    { x: half - thickness, y: gate, w: thickness, h: half - gate },
  ];
}

function barricadeRect(barricade) {
  return barricade.side % 2 === 0
    ? { x: barricade.x - 27, y: barricade.y - 7, w: 54, h: 14 }
    : { x: barricade.x - 7, y: barricade.y - 27, w: 14, h: 54 };
}

function circleTouchesRect(entity, rect) {
  const nearestX = clamp(entity.x, rect.x, rect.x + rect.w);
  const nearestY = clamp(entity.y, rect.y, rect.y + rect.h);
  return Math.hypot(entity.x - nearestX, entity.y - nearestY) <= entity.radius + 0.5;
}

function circleOverlapsRect(entity, rect) {
  const nearestX = clamp(entity.x, rect.x, rect.x + rect.w);
  const nearestY = clamp(entity.y, rect.y, rect.y + rect.h);
  return Math.hypot(entity.x - nearestX, entity.y - nearestY) < entity.radius - 0.1;
}

function resolveCircleRects(entity, rects) {
  for (const rect of rects) {
    const nearestX = clamp(entity.x, rect.x, rect.x + rect.w);
    const nearestY = clamp(entity.y, rect.y, rect.y + rect.h);
    const dx = entity.x - nearestX;
    const dy = entity.y - nearestY;
    const overlap = entity.radius - Math.hypot(dx, dy);
    if (overlap <= 0) continue;
    if (dx || dy) {
      const direction = normalize(dx, dy);
      entity.x += direction.x * overlap;
      entity.y += direction.y * overlap;
      continue;
    }
    const distances = [
      { amount: Math.abs(entity.x - rect.x), x: rect.x - entity.radius, y: entity.y },
      { amount: Math.abs(rect.x + rect.w - entity.x), x: rect.x + rect.w + entity.radius, y: entity.y },
      { amount: Math.abs(entity.y - rect.y), x: entity.x, y: rect.y - entity.radius },
      { amount: Math.abs(rect.y + rect.h - entity.y), x: entity.x, y: rect.y + rect.h + entity.radius },
    ];
    const nearestEdge = distances.sort((a, b) => a.amount - b.amount)[0];
    entity.x = nearestEdge.x;
    entity.y = nearestEdge.y;
  }
}

function solidRects() {
  return [...wallRects(), ...state.barricades.filter((barricade) => barricade.health > 0).map(barricadeRect)];
}

function moveWithCollisions(entity, dx, dy) {
  const steps = Math.max(1, Math.ceil(Math.hypot(dx, dy) / 6));
  for (let step = 0; step < steps; step += 1) {
    entity.x += dx / steps;
    entity.y += dy / steps;
    resolveCircleRects(entity, solidRects());
  }
}

function addParticles(x, y, color, count = 8, speed = 48) {
  for (let i = 0; i < count; i += 1) {
    const angle = Math.random() * TAU;
    const velocity = (0.35 + Math.random() * 0.65) * speed;
    state.particles.push({
      x, y, vx: Math.cos(angle) * velocity, vy: Math.sin(angle) * velocity,
      color, life: 0.35 + Math.random() * 0.38, maxLife: 0.73, size: 1.5 + Math.random() * 3,
    });
  }
}

function kickShake(amount) {
  state.shake = Math.max(state.shake, amount);
}

function heroProfile() {
  return HERO_PROFILES[state.hero.kind];
}

function switchHero() {
  if (!state.heroes.has("warden") || state.over) return;
  state.hero.kind = state.hero.kind === "ranger" ? "warden" : "ranger";
  addParticles(state.hero.x, state.hero.y, "#f3c878", 14, 68);
  showToast(`${heroProfile().name} DEPLOYED`);
  updateUi();
}

function addFloater(x, y, text, color = "#f4d78d") {
  state.floaters.push({ x, y, text, color, life: 1, maxLife: 1 });
}

function showToast(message) {
  ui.toast.textContent = message;
  ui.toast.classList.add("show");
  toastTimer = 2.2;
}

function spawnDrop(x, y, value, fromMine = false) {
  state.drops.push({
    id: nextId++, x, y, value, fromMine, radius: fromMine ? 7 : 6,
    kind: "gold", life: fromMine ? 24 : 14, bob: Math.random() * TAU,
  });
}

function spawnEmber(x, y) {
  state.drops.push({
    id: nextId++, x, y, value: 0, fromMine: false, radius: 9,
    kind: "ember", life: 20, bob: Math.random() * TAU,
  });
}

function spawnEnemy(elite = false) {
  const half = Math.max(fortHalfSize() + 165, Math.min(canvas.clientWidth, canvas.clientHeight) * 0.55);
  const side = Math.floor(Math.random() * 4);
  const spread = (Math.random() - 0.5) * half * 1.7;
  let x = spread;
  let y = -half;
  if (side === 1) { x = half; y = spread; }
  if (side === 2) { x = spread; y = half; }
  if (side === 3) { x = -half; y = spread; }
  const gateOffset = fortHalfSize() - 8;
  const gates = [
    { x: 0, y: -gateOffset },
    { x: gateOffset, y: 0 },
    { x: 0, y: gateOffset },
    { x: -gateOffset, y: 0 },
  ];
  const stats = waveStats(state.wave);
  const roll = Math.random();
  const kind = elite ? "elite" : state.wave >= 4 && roll < 0.18 ? "brute" : state.wave >= 2 && roll < 0.46 ? "runner" : "raider";
  const profile = ENEMY_PROFILES[kind];
  state.enemies.push({
    id: nextId++, x, y, radius: profile.radius,
    health: stats.health * profile.health * TUNING.enemyHealthMult,
    maxHealth: stats.health * profile.health * TUNING.enemyHealthMult,
    speed: stats.speed * profile.speed * TUNING.enemySpeedMult,
    damage: Math.ceil(stats.damage * profile.damage * TUNING.enemyDamageMult),
    reward: Math.ceil(stats.reward * profile.reward * TUNING.goldRewardMult),
    elite, kind, hitTimer: 0, attackTimer: 0, dead: false, gate: gates[side], side, inside: false,
  });
}

function startWave() {
  const stats = waveStats(state.wave);
  state.spawning = {
    remaining: Math.max(1, Math.round(stats.count * TUNING.enemyCountMult)) + (stats.elite ? 1 : 0),
    timer: 0,
    interval: stats.interval,
    elitePending: stats.elite,
  };
  if (stats.elite) {
    ui.bossBanner.classList.add("show");
    setTimeout(() => ui.bossBanner.classList.remove("show"), 1800);
  }
}

function aimAhead(source, target, projectileSpeed) {
  if (!target.speed || target.speed <= 0) return target;
  const heading = normalize(
    target.inside ? state.keep.x - target.x : target.gate.x - target.x,
    target.inside ? state.keep.y - target.y : target.gate.y - target.y,
  );
  const evx = heading.x * target.speed;
  const evy = heading.y * target.speed;
  const dpx = target.x - source.x;
  const dpy = target.y - source.y;
  const a = evx * evx + evy * evy - projectileSpeed * projectileSpeed;
  const b = 2 * (dpx * evx + dpy * evy);
  const c = dpx * dpx + dpy * dpy;
  if (Math.abs(a) < 0.001) {
    const t = b !== 0 ? Math.max(0, -c / b) : 0;
    return { x: target.x + evx * t, y: target.y + evy * t };
  }
  const disc = b * b - 4 * a * c;
  if (disc < 0) return target;
  const t = Math.max(0, (-b - Math.sqrt(disc)) / (2 * a));
  return { x: target.x + evx * t, y: target.y + evy * t };
}

function fireProjectile(source, target, options = {}) {
  const direction = normalize(target.x - source.x, target.y - source.y);
  state.projectiles.push({
    id: nextId++, x: source.x, y: source.y,
    vx: direction.x * (options.speed ?? 390),
    vy: direction.y * (options.speed ?? 390),
    damage: options.damage ?? state.hero.damage,
    life: options.life ?? 1.25,
    radius: options.radius ?? 3,
    color: options.color ?? "#f7df9a",
    splash: options.splash ?? 0,
    source: options.source ?? "hero",
  });
}

function damageEnemy(enemy, damage, splash = 0) {
  if (enemy.dead) return;
  enemy.health -= damage;
  enemy.hitTimer = 0.12;
  addParticles(enemy.x, enemy.y, enemy.elite ? COLORS.elite : "#cf6b52", 3, 34);
  if (splash) {
    for (const other of state.enemies) {
      if (other !== enemy && !other.dead && distance(enemy, other) < splash) {
        damageEnemy(other, damage * 0.45);
      }
    }
  }
  if (enemy.health > 0) return;
  enemy.dead = true;
  state.kills += 1;
  grantHeroXp(enemy.elite ? 4 : enemy.kind === "brute" ? 2 : 1);
  addParticles(enemy.x, enemy.y, enemy.elite ? "#f2a552" : "#bd5d48", enemy.elite ? 18 : 10, 72);
  for (let i = 0; i < enemy.reward; i += 1) {
    spawnDrop(enemy.x + (Math.random() - 0.5) * 18, enemy.y + (Math.random() - 0.5) * 18, 1);
  }
  if (enemy.elite) spawnEmber(enemy.x, enemy.y);
}

function damageBarricade(barricade, damage) {
  if (barricade.health <= 0) return;
  barricade.health = Math.max(0, barricade.health - damage);
  kickShake(4);
  addParticles(barricade.x, barricade.y, "#b98a59", 5, 38);
  addFloater(barricade.x, barricade.y - 18, `-${damage}`, "#efb36c");
  if (barricade.health > 0) return;
  const pad = state.pads.find((candidate) => candidate.type === "barricade" && candidate.side === barricade.side);
  if (pad) {
    state.completed.delete(pad.id);
    pad.invested = 0;
    pad.depositCarry = 0;
    pad.dwell = 0;
  }
  showToast("BARRICADE BREACHED");
}

function grantHeroXp(amount) {
  const hero = state.hero;
  hero.xp += amount;
  while (hero.xp >= hero.nextXp) {
    hero.xp -= hero.nextXp;
    hero.nextXp += 4;
    hero.level += 1;
    hero.damage += 1.5;
    hero.maxHealth += 6;
    hero.health = Math.min(hero.maxHealth, hero.health + 12);
    hero.fireRate = Math.max(0.22, hero.fireRate - 0.012);
    addParticles(hero.x, hero.y, "#ffd36c", 18, 75);
    playTone(520, 0.12, "triangle", 0.055);
    setTimeout(() => playTone(780, 0.18, "triangle", 0.05), 90);
    showToast(`HERO LEVEL ${hero.level}`);
  }
}

function shootVolley() {
  const hero = state.hero;
  const profile = heroProfile();
  if (hero.abilityCooldown > 0 || state.paused || state.over) return;
  hero.abilityCooldown = hero.volleyCooldown;
  const baseAngle = Math.atan2(hero.facing.y, hero.facing.x);
  for (let i = -3; i <= 3; i += 1) {
    const angle = baseAngle + i * 0.16;
    fireProjectile(hero, { x: hero.x + Math.cos(angle), y: hero.y + Math.sin(angle) }, {
      damage: hero.damage * hero.volleyDamage * profile.damage, speed: 470, life: 1.45, radius: 4, color: "#ffd46f",
    });
  }
  addParticles(hero.x, hero.y, "#ffd46f", 16, 62);
  kickShake(4);
  playTone(210, 0.16, "sawtooth", 0.055);
  showToast("EMBER VOLLEY");
}

function completePad(pad) {
  state.completed.add(pad.id);
  addParticles(pad.x, pad.y, "#f2c766", 22, 92);
  switch (pad.type) {
    case "tower":
      state.towers.push({ id: pad.id, x: pad.x, y: pad.y, type: "tower", level: 1, cooldown: 0, range: 230, damage: 10, rate: 0.88 });
      break;
    case "cannon":
      state.towers.push({ id: pad.id, x: pad.x, y: pad.y, type: "cannon", level: 1, cooldown: 0, range: 260, damage: 31, rate: 1.95 });
      break;
    case "ballista":
      state.towers.push({ id: pad.id, x: pad.x, y: pad.y, type: "ballista", level: 1, cooldown: 0, range: 310, damage: 24, rate: 1.35 });
      break;
    case "tower-upgrade": {
      const tower = state.towers.find((candidate) => candidate.id === pad.requires);
      if (tower) {
        tower.level += 1;
        tower.damage *= 1.72;
        tower.rate *= 0.84;
        tower.range += 18;
      }
      break;
    }
    case "mine":
      state.mines.push({ x: pad.x, y: pad.y, timer: 0, interval: pad.interval ?? 2.6 });
      break;
    case "banner":
      state.banners.push({ x: pad.x, y: pad.y, range: 142 });
      break;
    case "armory":
      state.armories.push({ x: pad.x, y: pad.y });
      state.towerPower *= 1.35;
      break;
    case "shrine":
      state.shrines.push({ x: pad.x, y: pad.y });
      state.hero.volleyCooldown = 5.8;
      state.hero.volleyDamage = 1.55;
      break;
    case "hero":
      state.lodges.push({ x: pad.x, y: pad.y });
      state.heroes.add("warden");
      switchHero();
      break;
    case "barricade": {
      const existing = state.barricades.find((barricade) => barricade.side === pad.side);
      if (existing) {
        existing.health = existing.maxHealth;
      } else {
        state.barricades.push({ x: pad.x, y: pad.y, side: pad.side, health: 180, maxHealth: 180 });
      }
      break;
    }
    case "training":
      state.hero.level += 1;
      state.hero.damage += pad.id === "training" ? 6 : 10;
      state.hero.fireRate = Math.max(0.25, state.hero.fireRate - 0.09);
      state.hero.range += 12;
      break;
    case "repair":
      state.keep.maxHealth += 100;
      state.keep.health = state.keep.maxHealth;
      break;
    case "expand":
      state.stage += 1;
      state.keep.maxHealth += 80;
      state.keep.health = Math.min(state.keep.maxHealth, state.keep.health + 80);
      break;
  }
  kickShake(6);
  playTone(440, 0.16, "triangle", 0.06);
  setTimeout(() => playTone(660, 0.2, "triangle", 0.05), 100);
  showToast(`${pad.label} COMPLETE`);
}

function towerBuff(tower) {
  return state.banners.some((banner) => distance(tower, banner) < banner.range) ? 1.42 : 1;
}

function dash() {
  const hero = state.hero;
  if (hero.dashCooldown > 0 || state.paused || state.over) return;
  const roam = roamLimit();
  moveWithCollisions(hero, hero.facing.x * 96, hero.facing.y * 96);
  hero.x = clamp(hero.x, -roam, roam);
  hero.y = clamp(hero.y, -roam, roam);
  hero.dashCooldown = 2.4;
  addParticles(hero.x, hero.y, "#d5ebc5", 12, 84);
  kickShake(3);
  playTone(180, 0.1, "sawtooth", 0.035);
  showToast("FRONTIER DASH");
}

function updateHero(dt) {
  const hero = state.hero;
  const profile = heroProfile();
  let mx = 0;
  let my = 0;
  if (keys.has("KeyW") || keys.has("ArrowUp")) my -= 1;
  if (keys.has("KeyS") || keys.has("ArrowDown")) my += 1;
  if (keys.has("KeyA") || keys.has("ArrowLeft")) mx -= 1;
  if (keys.has("KeyD") || keys.has("ArrowRight")) mx += 1;
  const movement = normalize(mx, my);
  let desired = movement;
  if (movement.x || movement.y) pointerTarget = null;
  else if (pointerTarget) {
    desired = normalize(pointerTarget.x - hero.x, pointerTarget.y - hero.y);
    if (distance(hero, pointerTarget) < 6) pointerTarget = null;
  }
  if (desired.x || desired.y) {
    hero.facing = desired;
    moveWithCollisions(hero, desired.x * hero.speed * profile.speed * TUNING.heroSpeedMult * dt, desired.y * hero.speed * profile.speed * TUNING.heroSpeedMult * dt);
  }
  const roam = roamLimit();
  hero.x = clamp(hero.x, -roam, roam);
  hero.y = clamp(hero.y, -roam, roam);
  resolveCircleRects(hero, solidRects());
  const cameraDeadzone = Math.min(150, Math.max(88, Math.min(canvas.clientWidth, canvas.clientHeight) * 0.22));
  const cameraLimit = Math.max(0, roam - cameraDeadzone);
  const cameraTargetX = clamp(Math.abs(hero.x) > cameraDeadzone ? hero.x - Math.sign(hero.x) * cameraDeadzone : 0, -cameraLimit, cameraLimit);
  const cameraTargetY = clamp(Math.abs(hero.y) > cameraDeadzone ? hero.y - Math.sign(hero.y) * cameraDeadzone : 0, -cameraLimit, cameraLimit);
  state.camera.x += (cameraTargetX - state.camera.x) * Math.min(1, dt * 4.5);
  state.camera.y += (cameraTargetY - state.camera.y) * Math.min(1, dt * 4.5);
  hero.invulnerable = Math.max(0, hero.invulnerable - dt);
  hero.abilityCooldown = Math.max(0, hero.abilityCooldown - dt);
  hero.dashCooldown = Math.max(0, hero.dashCooldown - dt);
  hero.overdrive = Math.max(0, hero.overdrive - dt);
  hero.shotTimer -= dt;
  if (hero.shotTimer <= 0) {
    const target = nearest(hero, state.enemies, (enemy) => !enemy.dead && distance(hero, enemy) <= hero.range * profile.range * TUNING.heroRangeMult);
    if (target) {
      fireProjectile(hero, aimAhead(hero, target, 390), { damage: hero.damage * profile.damage * TUNING.heroDamageMult, color: hero.kind === "warden" ? "#b9d9bd" : "#f7df9a" });
      hero.facing = normalize(target.x - hero.x, target.y - hero.y);
      hero.shotTimer = (hero.fireRate / TUNING.heroFireSpeedMult) * profile.rate * (hero.overdrive > 0 ? 0.58 : 1);
    }
  }
  for (const pad of state.pads) {
    if (!padIsAvailable(pad) || state.completed.has(pad.id)) continue;
    if (distance(hero, pad) < 30 && state.gold > 0) {
      pad.dwell = (pad.dwell ?? 0) + dt;
      if (pad.dwell < 0.5) continue;
      pad.depositCarry = (pad.depositCarry ?? 0) + 19 * TUNING.depositSpeedMult * dt;
      const amount = depositAmount(state.gold, pad.cost - pad.invested, 1, pad.depositCarry);
      pad.depositCarry -= amount;
      pad.invested += amount;
      state.gold -= amount;
      if (amount) addParticles(pad.x, pad.y, COLORS.gold, 2, 24);
      if (pad.invested >= pad.cost) completePad(pad);
    } else {
      pad.dwell = 0;
    }
  }
  for (const drop of state.drops) {
    if (distance(hero, drop) < 24) {
      drop.collected = true;
      if (drop.kind === "ember") {
        hero.overdrive = 10;
        addParticles(drop.x, drop.y, "#ff8b52", 16, 75);
        addFloater(drop.x, drop.y - 8, "OVERDRIVE", "#ffb064");
        showToast("EMBER SIGIL  -  HERO OVERDRIVE");
        playTone(330, 0.18, "sawtooth", 0.05);
      } else {
        state.gold += drop.value;
        addParticles(drop.x, drop.y, COLORS.gold, 5, 43);
        addFloater(drop.x, drop.y - 8, `+${drop.value}`, "#ffd66b");
      }
      if (performance.now() - lastPickupTone > 70) {
        playTone(720, 0.055, "sine", 0.025);
        lastPickupTone = performance.now();
      }
    }
  }
}

function updateEnemies(dt) {
  for (const enemy of state.enemies) {
    if (enemy.dead) continue;
    enemy.hitTimer = Math.max(0, enemy.hitTimer - dt);
    enemy.attackTimer -= dt;
    const barricade = enemy.inside ? state.barricades.find((candidate) => candidate.side === enemy.side && candidate.health > 0) : null;
    const target = enemy.inside ? barricade ?? state.keep : enemy.gate;
    const direction = normalize(target.x - enemy.x, target.y - enemy.y);
    if (!enemy.inside && distance(enemy, enemy.gate) < 12) {
      enemy.inside = true;
    } else if (barricade && circleTouchesRect(enemy, barricadeRect(barricade))) {
      if (enemy.attackTimer <= 0) {
        enemy.attackTimer = 0.85;
        damageBarricade(barricade, enemy.damage);
      }
    } else if (enemy.inside && distance(enemy, state.keep) <= enemy.radius + state.keep.radius) {
      if (enemy.attackTimer <= 0) {
        enemy.attackTimer = 0.85;
        state.keep.health -= enemy.damage;
        kickShake(7);
        addParticles(target.x, target.y, "#d37455", 8, 54);
        addFloater(target.x, target.y - 30, `-${enemy.damage}`, "#ef896c");
      }
    } else {
      moveWithCollisions(enemy, direction.x * enemy.speed * dt, direction.y * enemy.speed * dt);
    }
    if (distance(enemy, state.hero) < enemy.radius + state.hero.radius && state.hero.invulnerable <= 0) {
      state.hero.health -= Math.max(4, enemy.damage * 0.65);
      state.hero.invulnerable = 0.75;
      addParticles(state.hero.x, state.hero.y, "#d9795c", 8, 58);
    }
  }
}

function updateProjectiles(dt) {
  for (const projectile of state.projectiles) {
    projectile.x += projectile.vx * dt;
    projectile.y += projectile.vy * dt;
    projectile.life -= dt;
    const hit = state.enemies.find((enemy) => !enemy.dead && distance(projectile, enemy) < projectile.radius + enemy.radius);
    if (hit) {
      projectile.life = 0;
      damageEnemy(hit, projectile.damage, projectile.splash);
    }
  }
}

function updateTowers(dt) {
  for (const tower of state.towers) {
    tower.cooldown -= dt;
    if (tower.cooldown > 0) continue;
    const buff = towerBuff(tower);
    const target = nearest(tower, state.enemies, (enemy) => !enemy.dead && distance(tower, enemy) <= tower.range);
    if (!target) continue;
    const projSpeed = tower.type === "cannon" ? 275 : tower.type === "ballista" ? 480 : 340;
    fireProjectile(tower, aimAhead(tower, target, projSpeed), {
      damage: tower.damage * buff * state.towerPower * TUNING.towerDamageMult,
      speed: projSpeed,
      radius: tower.type === "cannon" ? 6 : tower.type === "ballista" ? 4 : 3,
      color: tower.type === "cannon" ? "#ec8b4d" : tower.type === "ballista" ? "#d4e8aa" : "#cfdfb2",
      splash: tower.type === "cannon" ? 58 : 0,
      source: tower.type,
    });
    tower.cooldown = tower.rate / buff / TUNING.towerFireSpeedMult;
  }
}

function updateMines(dt) {
  for (const mine of state.mines) {
    mine.timer -= dt;
    if (mine.timer > 0) continue;
    mine.timer = mine.interval / TUNING.mineSpeedMult;
    for (let i = 0; i < 2; i++) spawnDrop(mine.x + (Math.random() - 0.5) * 30, mine.y + 10 + i * 10, 2, true);
    addParticles(mine.x, mine.y + 10, COLORS.gold, 6, 30);
  }
}

function updateWave(dt) {
  if (!state.spawning && state.enemies.every((enemy) => enemy.dead)) {
    if (state.waveBonusPending) {
      const clearedWave = state.wave;
      state.waveBonusPending = false;
      state.wave += 1;
      const upgradeBreak = clearedWave % 5 === 0;
      state.betweenWaves = upgradeBreak ? 10 : 2.8;
      state.upgradeBreak = upgradeBreak;
      const bonus = 2 + Math.floor(clearedWave / 2);
      for (let i = 0; i < bonus; i += 1) {
        spawnDrop((Math.random() - 0.5) * 25, 46 + (Math.random() - 0.5) * 16, 1, true);
      }
      if (state.completed.has("repair-yard") && state.keep.health < state.keep.maxHealth) {
        const restored = Math.min(36, state.keep.maxHealth - state.keep.health);
        state.keep.health += restored;
        addParticles(state.keep.x, state.keep.y, "#a8bd6e", 10, 42);
        addFloater(state.keep.x, state.keep.y - 36, `+${restored} REPAIR`, "#b9d782");
      }
      showToast(`WAVE ${clearedWave} HELD  +${bonus} GOLD`);
      if (clearedWave % 3 === 0) showBoonDraft();
    }
    state.betweenWaves -= dt;
    if (state.betweenWaves <= 0) { state.upgradeBreak = false; startWave(); }
    return;
  }
  if (!state.spawning) return;
  state.spawning.timer -= dt;
  if (state.spawning.timer <= 0 && state.spawning.remaining > 0) {
    const elite = state.spawning.elitePending;
    spawnEnemy(elite);
    state.spawning.elitePending = false;
    state.spawning.remaining -= 1;
    state.spawning.timer = state.spawning.interval;
  }
  if (state.spawning.remaining <= 0) {
    state.spawning = null;
    state.waveBonusPending = true;
  }
}

function updateEffects(dt) {
  for (const drop of state.drops) {
    drop.life -= dt;
    drop.bob += dt * 4;
    const heroDistance = distance(state.hero, drop);
    const attractionRange = state.hero.range * heroProfile().range * TUNING.heroRangeMult;
    if (drop.kind === "gold" && heroDistance < attractionRange && heroDistance > 1) {
      const direction = normalize(state.hero.x - drop.x, state.hero.y - drop.y);
      const speed = attractionSpeed(heroDistance, attractionRange);
      drop.x += direction.x * speed * dt;
      drop.y += direction.y * speed * dt;
    }
  }
  for (const particle of state.particles) {
    particle.x += particle.vx * dt;
    particle.y += particle.vy * dt;
    particle.vx *= 0.94;
    particle.vy *= 0.94;
    particle.life -= dt;
  }
  for (const floater of state.floaters) {
    floater.y -= 23 * dt;
    floater.life -= dt;
  }
  state.enemies = state.enemies.filter((enemy) => !enemy.dead);
  state.projectiles = state.projectiles.filter((projectile) => projectile.life > 0);
  state.drops = state.drops.filter((drop) => !drop.collected && drop.life > 0);
  state.particles = state.particles.filter((particle) => particle.life > 0);
  state.floaters = state.floaters.filter((floater) => floater.life > 0);
  state.shake = Math.max(0, state.shake - dt * 22);
  toastTimer -= dt;
  if (toastTimer <= 0) ui.toast.classList.remove("show");
  introTimer -= dt;
  if (introTimer <= 0) ui.intro.classList.add("hidden");
}

function update(dt) {
  if (state.paused || state.over) return;
  state.elapsed += dt;
  updateWave(dt);
  updateHero(dt);
  updateEnemies(dt);
  updateProjectiles(dt);
  updateTowers(dt);
  updateMines(dt);
  updateEffects(dt);
  if (state.keep.health <= 0 || state.hero.health <= 0) endGame();
  updateUi();
}

function endGame() {
  state.over = true;
  state.bestWave = Math.max(state.bestWave, state.wave);
  localStorage.setItem("emberhold-best-wave", String(state.bestWave));
  ui.gameOver.classList.add("show");
  ui.gameOverStats.textContent = `You reached wave ${state.wave}, built ${state.completed.size} improvements, and defeated ${state.kills} raiders.`;
  ui.restart.focus();
}

function setPaused(paused, force = false) {
  if (state.over) return;
  if (!paused && !force && ui.boonDraft.classList.contains("show")) return;
  state.paused = paused;
  ui.pause.textContent = state.paused ? "RESUME" : "PAUSE";
}

function showBoonDraft() {
  setPaused(true);
  ui.boonDraft.classList.add("show");
  ui.boonDraft.querySelector("button").focus();
}

function chooseBoon(kind) {
  if (!ui.boonDraft.classList.contains("show")) return;
  if (kind === "ranger") state.hero.damage *= 1.18;
  if (kind === "keep") {
    state.keep.maxHealth += 70;
    state.keep.health += 70;
  }
  if (kind === "tower") state.towerPower *= 1.22;
  state.boons.push(kind);
  ui.boonDraft.classList.remove("show");
  setPaused(false, true);
  canvas.focus();
  showToast(`${kind.toUpperCase()} BOON CLAIMED`);
}

let activeSettingsTab = "hero";

function openSettings() {
  if (state.over || ui.boonDraft.classList.contains("show")) return;
  setPaused(true);
  ui.settings.classList.add("show");
  buildSettingsPanel();
}

function closeSettings() {
  ui.settings.classList.remove("show");
  setPaused(false, true);
  canvas.focus();
}

function buildSettingsPanel() {
  ui.settingsTabs.innerHTML = TUNING_TABS.map((t) =>
    `<button type="button" data-stab="${t.id}"${t.id === activeSettingsTab ? ' class="active"' : ""}>${t.label}</button>`
  ).join("");
  showSettingsTab(activeSettingsTab);
}

function showSettingsTab(tabId) {
  if (tabId === "data") { renderDataTab(); return; }
  const defs = TUNING_DEFS.filter((d) => d.tab === tabId);
  ui.settingsBody.innerHTML = defs.map((def) =>
    `<div class="tune-row">
      <label class="tune-label" for="tune-${def.key}">${def.label}</label>
      <span class="tune-value" id="tune-out-${def.key}">×${TUNING[def.key].toFixed(2)}</span>
      <input class="tune-slider" type="range" id="tune-${def.key}" data-key="${def.key}"
        min="${def.min}" max="${def.max}" step="${def.step}" value="${TUNING[def.key]}">
      <small class="tune-hint">${def.hint}</small>
    </div>`
  ).join("");
  ui.settingsBody.querySelectorAll(".tune-slider").forEach((slider) => {
    slider.addEventListener("input", () => {
      const key = slider.dataset.key;
      TUNING[key] = parseFloat(slider.value);
      const out = document.getElementById(`tune-out-${key}`);
      if (out) out.textContent = `×${TUNING[key].toFixed(2)}`;
    });
  });
}

function renderDataTab() {
  ui.settingsBody.innerHTML = `<div class="data-tab">
    <p class="tune-hint" style="margin:0 0 10px">Paste a previously exported block into the box then click <b>IMPORT</b>. Changes apply immediately.</p>
    <div class="data-actions">
      <button type="button" id="settings-export">EXPORT / COPY</button>
      <button type="button" id="settings-import">IMPORT FROM TEXT</button>
      <button type="button" id="settings-reset">RESET DEFAULTS</button>
    </div>
    <textarea id="settings-json" class="settings-json" rows="16" spellcheck="false">${JSON.stringify(TUNING, null, 2)}</textarea>
  </div>`;
  document.getElementById("settings-export").addEventListener("click", () => {
    const json = JSON.stringify(TUNING, null, 2);
    document.getElementById("settings-json").value = json;
    navigator.clipboard?.writeText(json).catch(() => {});
    showToast("SETTINGS COPIED TO CLIPBOARD");
  });
  document.getElementById("settings-import").addEventListener("click", () => {
    try {
      const parsed = JSON.parse(document.getElementById("settings-json").value);
      for (const key of Object.keys(TUNING_DEFAULTS)) {
        if (typeof parsed[key] === "number") TUNING[key] = parsed[key];
      }
      showToast("SETTINGS IMPORTED");
    } catch {
      showToast("INVALID JSON — CHECK FORMAT");
    }
  });
  document.getElementById("settings-reset").addEventListener("click", () => {
    Object.assign(TUNING, TUNING_DEFAULTS);
    document.getElementById("settings-json").value = JSON.stringify(TUNING, null, 2);
    showToast("DEFAULTS RESTORED");
  });
}

function updateUi() {
  ui.gold.textContent = state.gold;
  ui.wave.textContent = state.wave;
  const remainingRaiders = state.enemies.length + (state.spawning?.remaining ?? 0);
  ui.waveDetail.textContent = waveStats(state.wave).elite
    ? "ELITE WAVE"
    : remainingRaiders
      ? `${remainingRaiders} RAIDERS`
      : "WAVE CLEAR";
  ui.stage.textContent = state.stage;
  ui.bestWave.textContent = state.bestWave;
  ui.heroLevel.textContent = `LV ${state.hero.level}`;
  ui.heroLabel.textContent = heroProfile().name;
  ui.heroXp.textContent = `XP ${state.hero.xp}/${state.hero.nextXp}`;
  ui.heroSwitch.textContent = heroProfile().initial;
  ui.heroHealth.style.width = `${clamp(state.hero.health / state.hero.maxHealth * 100, 0, 100)}%`;
  const keepPercent = clamp(state.keep.health / state.keep.maxHealth * 100, 0, 100);
  ui.keepHealth.style.width = `${keepPercent}%`;
  ui.keepHealthText.textContent = `${Math.ceil(keepPercent)}%`;
  ui.abilityStatus.textContent = state.hero.abilityCooldown > 0 ? `${state.hero.abilityCooldown.toFixed(1)}S` : "READY";
  ui.dashStatus.textContent = state.hero.dashCooldown > 0 ? `${state.hero.dashCooldown.toFixed(1)}S` : "READY";
  ui.ability.disabled = state.hero.abilityCooldown > 0;
  ui.dash.disabled = state.hero.dashCooldown > 0;
  const boonCounts = {};
  for (const boon of state.boons) boonCounts[boon] = (boonCounts[boon] ?? 0) + 1;
  const boonLabels = [["ranger", "HERO"], ["keep", "KEEP"], ["tower", "TOWER"]]
    .filter(([kind]) => boonCounts[kind])
    .map(([kind, label]) => `${label} +${boonCounts[kind]}`);
  ui.boonSummary.textContent = boonLabels.length ? `BOONS: ${boonLabels.join("  /  ")}` : "NO FRONTIER BOONS YET";
  const nextPad = state.pads.find((pad) => padIsAvailable(pad) && !state.completed.has(pad.id));
  const nearbyPad = state.pads.find((pad) => padIsAvailable(pad) && !state.completed.has(pad.id) && distance(state.hero, pad) < 54);
  if (state.upgradeBreak && !state.spawning && state.betweenWaves > 0) {
    ui.missionTitle.textContent = "UPGRADE BREAK";
    ui.missionDetail.textContent = `${Math.ceil(state.betweenWaves)}s — build before the raiders return.`;
  } else if (state.gold === 0 && state.kills === 0) {
    ui.missionTitle.textContent = "DEFEAT RAIDERS";
    ui.missionDetail.textContent = "Your bow fires automatically. Collect the gold they drop.";
  } else if (state.drops.length && state.gold < 8) {
    ui.missionTitle.textContent = "COLLECT GOLD";
    ui.missionDetail.textContent = "Move over the bright coins before they disappear.";
  } else if (nearbyPad) {
    ui.missionTitle.textContent = `INVEST IN ${nearbyPad.label}`;
    ui.missionDetail.textContent = (nearbyPad.dwell ?? 0) < 0.5
      ? `${nearbyPad.cost - nearbyPad.invested} gold remaining. Hold position briefly to begin investing.`
      : `${nearbyPad.cost - nearbyPad.invested} gold remaining. Stay on the glowing pad to build.`;
  } else if (nextPad) {
    ui.missionTitle.textContent = `BUILD ${nextPad.label}`;
    ui.missionDetail.textContent = `Stand on a glowing ${nextPad.short} pad to invest gold. ${nextPad.cost - nextPad.invested} remaining.`;
  } else {
    ui.missionTitle.textContent = "HOLD THE LINE";
    ui.missionDetail.textContent = "Your fortress is fully deployed. Survive as long as you can.";
  }
}

function drawCircle(x, y, radius, fill, stroke, width = 1) {
  ctx.beginPath();
  ctx.arc(x, y, radius, 0, TAU);
  ctx.fillStyle = fill;
  ctx.fill();
  if (stroke) {
    ctx.lineWidth = width;
    ctx.strokeStyle = stroke;
    ctx.stroke();
  }
}

function drawGround() {
  const { width, height } = canvas.getBoundingClientRect();
  ctx.fillStyle = COLORS.grass;
  ctx.fillRect(0, 0, width, height);
  ctx.save();
  ctx.globalAlpha = 0.2;
  ctx.fillStyle = COLORS.grassDark;
  for (let x = -20; x < width + 40; x += 42) {
    for (let y = -20; y < height + 40; y += 42) {
      const wobble = ((x * 17 + y * 13) % 19) - 9;
      ctx.beginPath();
      ctx.arc(x + wobble, y - wobble, 2, 0, TAU);
      ctx.fill();
    }
  }
  ctx.restore();
  const center = worldToScreen(0, 0);
  ctx.fillStyle = COLORS.pathEdge;
  ctx.fillRect(center.x - 27, 0, 54, height);
  ctx.fillRect(0, center.y - 27, width, 54);
  ctx.fillStyle = COLORS.path;
  ctx.fillRect(center.x - 21, 0, 42, height);
  ctx.fillRect(0, center.y - 21, width, 42);
}

function drawWalls() {
  const center = worldToScreen(0, 0);
  for (const rect of wallRects()) {
    ctx.fillStyle = COLORS.wallDark;
    ctx.fillRect(center.x + rect.x - 2, center.y + rect.y + 3, rect.w + 4, rect.h + 2);
    ctx.fillStyle = COLORS.wall;
    ctx.fillRect(center.x + rect.x, center.y + rect.y, rect.w, rect.h);
    ctx.fillStyle = "#c6ad7d";
    const horizontal = rect.w > rect.h;
    const length = horizontal ? rect.w : rect.h;
    for (let offset = 17; offset < length - 3; offset += 17) {
      ctx.fillRect(center.x + rect.x + (horizontal ? offset : 3), center.y + rect.y + (horizontal ? 3 : offset), horizontal ? 9 : 8, horizontal ? 5 : 9);
    }
  }
}

function drawBarricades() {
  for (const barricade of state.barricades) {
    if (barricade.health <= 0) continue;
    const rect = barricadeRect(barricade);
    const point = worldToScreen(rect.x, rect.y);
    ctx.fillStyle = "#584535";
    ctx.fillRect(point.x - 2, point.y + 3, rect.w + 4, rect.h + 2);
    ctx.fillStyle = "#b48152";
    ctx.fillRect(point.x, point.y, rect.w, rect.h);
    ctx.fillStyle = "#e0b66d";
    const horizontal = rect.w > rect.h;
    const length = horizontal ? rect.w : rect.h;
    for (let offset = 4; offset < length; offset += 13) {
      ctx.fillRect(point.x + (horizontal ? offset : 4), point.y + (horizontal ? 4 : offset), horizontal ? 7 : 6, horizontal ? 6 : 7);
    }
    const healthWidth = 42;
    const center = worldToScreen(barricade.x, barricade.y);
    ctx.fillStyle = "rgba(19, 25, 24, .85)";
    ctx.fillRect(center.x - healthWidth / 2, center.y - 20, healthWidth, 4);
    ctx.fillStyle = "#b9cc78";
    ctx.fillRect(center.x - healthWidth / 2, center.y - 20, healthWidth * barricade.health / barricade.maxHealth, 4);
  }
}

function drawKeep() {
  const point = worldToScreen(0, 0);
  ctx.fillStyle = "#4d4033";
  ctx.fillRect(point.x - 26, point.y - 23, 52, 51);
  ctx.fillStyle = "#91744f";
  ctx.fillRect(point.x - 30, point.y - 31, 18, 20);
  ctx.fillRect(point.x + 12, point.y - 31, 18, 20);
  ctx.fillStyle = "#ba9662";
  ctx.fillRect(point.x - 25, point.y - 18, 50, 43);
  ctx.fillStyle = "#59412f";
  ctx.fillRect(point.x - 8, point.y + 4, 16, 21);
  ctx.fillStyle = "#e2b554";
  ctx.fillRect(point.x - 3, point.y - 12, 6, 6);
}

function drawPads() {
  const now = performance.now() / 1000;
  for (const pad of state.pads) {
    if (!padIsAvailable(pad) || state.completed.has(pad.id)) continue;
    const point = worldToScreen(pad.x, pad.y);
    const progress = pad.invested / pad.cost;
    const pulse = 1 + Math.sin(now * 3 + pad.x) * 0.07;
    ctx.save();
    ctx.translate(point.x, point.y);
    ctx.scale(pulse, pulse);
    drawCircle(0, 0, 22, "rgba(35, 50, 47, .83)", "rgba(241, 194, 96, .58)", 2);
    ctx.beginPath();
    ctx.arc(0, 0, 16, -Math.PI / 2, -Math.PI / 2 + TAU * progress);
    ctx.strokeStyle = "#f0bd58";
    ctx.lineWidth = 4;
    ctx.stroke();
    ctx.strokeStyle = "rgba(239, 198, 108, .24)";
    ctx.lineWidth = 1;
    ctx.beginPath();
    ctx.arc(0, 0, 28, 0, TAU);
    ctx.stroke();
    ctx.restore();
    ctx.textAlign = "center";
    ctx.font = "700 9px Inter";
    ctx.fillStyle = "#f4d78d";
    ctx.fillText(pad.short, point.x, point.y - 31);
    ctx.font = "700 10px Inter";
    ctx.fillStyle = progress ? "#f3d17c" : "#d6bd82";
    ctx.fillText(`${pad.invested}/${pad.cost}`, point.x, point.y + 4);
  }
}

function drawTower(tower) {
  const point = worldToScreen(tower.x, tower.y);
  if (tower.type === "cannon") {
    drawCircle(point.x, point.y, 17, "#70503b", "#c49a62", 2);
    ctx.strokeStyle = "#d27d48";
    ctx.lineWidth = 7;
    ctx.beginPath();
    ctx.moveTo(point.x, point.y);
    ctx.lineTo(point.x + 15, point.y - 12);
    ctx.stroke();
  } else if (tower.type === "ballista") {
    ctx.fillStyle = "#4a4035";
    ctx.fillRect(point.x - 11, point.y - 10, 22, 22);
    ctx.fillStyle = "#8c7252";
    ctx.fillRect(point.x - 14, point.y - 14, 8, 8);
    ctx.fillRect(point.x + 6, point.y - 14, 8, 8);
    ctx.strokeStyle = "#b89050";
    ctx.lineWidth = 4;
    ctx.beginPath();
    ctx.moveTo(point.x, point.y + 8);
    ctx.lineTo(point.x, point.y - 13);
    ctx.stroke();
    ctx.fillStyle = "#ddc07a";
    ctx.beginPath();
    ctx.moveTo(point.x - 9, point.y - 3);
    ctx.lineTo(point.x, point.y - 16);
    ctx.lineTo(point.x + 9, point.y - 3);
    ctx.fill();
  } else {
    ctx.fillStyle = "#6b573e";
    ctx.fillRect(point.x - 13, point.y - 13, 26, 27);
    ctx.fillStyle = "#b08b59";
    ctx.fillRect(point.x - 16, point.y - 17, 9, 9);
    ctx.fillRect(point.x + 7, point.y - 17, 9, 9);
    ctx.fillStyle = "#d3b16f";
    ctx.fillRect(point.x - 3, point.y - 7, 6, 11);
  }
  if (towerBuff(tower) > 1) {
    ctx.strokeStyle = "rgba(230, 194, 90, .42)";
    ctx.lineWidth = 2;
    ctx.beginPath();
    ctx.arc(point.x, point.y, 23 + Math.sin(performance.now() / 180) * 2, 0, TAU);
    ctx.stroke();
  }
  if (tower.level > 1) {
    ctx.fillStyle = "#f2c664";
    ctx.textAlign = "center";
    ctx.font = "800 10px Inter";
    ctx.fillText("II", point.x, point.y - 24);
  }
}

function drawStructures() {
  for (const mine of state.mines) {
    const point = worldToScreen(mine.x, mine.y);
    ctx.fillStyle = "#5e4e3e";
    ctx.fillRect(point.x - 17, point.y - 13, 34, 28);
    ctx.fillStyle = "#2b302e";
    ctx.beginPath();
    ctx.arc(point.x, point.y + 1, 10, Math.PI, 0);
    ctx.fill();
    drawCircle(point.x + 13, point.y - 10, 4, COLORS.gold);
  }
  for (const tower of state.towers) drawTower(tower);
  for (const banner of state.banners) {
    const point = worldToScreen(banner.x, banner.y);
    ctx.fillStyle = "rgba(222, 179, 90, .065)";
    ctx.beginPath();
    ctx.arc(point.x, point.y, banner.range, 0, TAU);
    ctx.fill();
    ctx.strokeStyle = "rgba(222, 179, 90, .18)";
    ctx.lineWidth = 1;
    ctx.stroke();
    ctx.strokeStyle = "#d6b370";
    ctx.lineWidth = 3;
    ctx.beginPath();
    ctx.moveTo(point.x, point.y + 16);
    ctx.lineTo(point.x, point.y - 20);
    ctx.stroke();
    ctx.fillStyle = "#a64c3e";
    ctx.beginPath();
    ctx.moveTo(point.x + 2, point.y - 18);
    ctx.lineTo(point.x + 20, point.y - 12);
    ctx.lineTo(point.x + 2, point.y - 3);
    ctx.fill();
  }
  for (const armory of state.armories) {
    const point = worldToScreen(armory.x, armory.y);
    ctx.fillStyle = "#6e5b49";
    ctx.fillRect(point.x - 17, point.y - 13, 34, 27);
    ctx.fillStyle = "#b48a58";
    ctx.fillRect(point.x - 20, point.y - 17, 40, 8);
    ctx.fillStyle = "#d0aa69";
    ctx.fillRect(point.x - 3, point.y - 8, 6, 18);
    ctx.fillRect(point.x - 10, point.y - 1, 20, 5);
  }
  for (const shrine of state.shrines) {
    const point = worldToScreen(shrine.x, shrine.y);
    drawCircle(point.x, point.y, 16, "#554737", "#d4a65b", 2);
    ctx.fillStyle = "#ed7945";
    ctx.beginPath();
    ctx.moveTo(point.x, point.y - 13);
    ctx.quadraticCurveTo(point.x + 13, point.y + 2, point.x, point.y + 11);
    ctx.quadraticCurveTo(point.x - 12, point.y + 2, point.x, point.y - 13);
    ctx.fill();
    ctx.fillStyle = "#ffd36e";
    ctx.beginPath();
    ctx.arc(point.x, point.y + 2, 4, 0, TAU);
    ctx.fill();
  }
  for (const lodge of state.lodges) {
    const point = worldToScreen(lodge.x, lodge.y);
    ctx.fillStyle = "#6d5540";
    ctx.fillRect(point.x - 17, point.y - 8, 34, 23);
    ctx.fillStyle = "#ad704e";
    ctx.beginPath();
    ctx.moveTo(point.x - 22, point.y - 8);
    ctx.lineTo(point.x, point.y - 25);
    ctx.lineTo(point.x + 22, point.y - 8);
    ctx.fill();
    ctx.fillStyle = "#d6b06b";
    ctx.fillRect(point.x - 4, point.y + 1, 8, 14);
  }
}

function drawDrops() {
  for (const drop of state.drops) {
    const point = worldToScreen(drop.x, drop.y + Math.sin(drop.bob) * 3);
    if (drop.kind === "ember") {
      ctx.save();
      ctx.translate(point.x, point.y);
      ctx.rotate(Math.PI / 4);
      ctx.fillStyle = "#e76f42";
      ctx.fillRect(-7, -7, 14, 14);
      ctx.strokeStyle = "#ffc36e";
      ctx.lineWidth = 2;
      ctx.strokeRect(-7, -7, 14, 14);
      ctx.restore();
    } else {
      drawCircle(point.x, point.y, drop.radius, "#d8842d", "#ffd064", 2);
      ctx.fillStyle = "#ffd064";
      ctx.fillRect(point.x - 1, point.y - 4, 2, 8);
    }
  }
}

function isOffscreen(entity, margin = 18) {
  const width = canvas.clientWidth;
  const height = canvas.clientHeight;
  const point = worldToScreen(entity.x, entity.y);
  return point.x < margin || point.x > width - margin || point.y < margin || point.y > height - margin;
}

function edgePoint(entity, margin = 24) {
  const screen = worldToScreen(entity.x, entity.y);
  const center = { x: canvas.clientWidth / 2, y: canvas.clientHeight / 2 };
  const dx = screen.x - center.x;
  const dy = screen.y - center.y;
  const scale = Math.min((center.x - margin) / Math.max(1, Math.abs(dx)), (center.y - margin) / Math.max(1, Math.abs(dy)));
  return { x: center.x + dx * scale, y: center.y + dy * scale };
}

function offscreenGoldDrops() {
  return state.drops.filter((drop) => {
    return drop.kind === "gold" && isOffscreen(drop);
  });
}

function drawLootBeacon() {
  const offscreen = offscreenGoldDrops();
  if (!offscreen.length) return;
  const target = nearest(state.hero, offscreen);
  const point = edgePoint(target);
  ctx.save();
  ctx.translate(point.x, point.y);
  ctx.rotate(Math.PI / 4);
  ctx.fillStyle = "rgba(21, 30, 32, .9)";
  ctx.fillRect(-13, -13, 26, 26);
  ctx.strokeStyle = "#f3bd4d";
  ctx.lineWidth = 2;
  ctx.strokeRect(-13, -13, 26, 26);
  ctx.restore();
  ctx.fillStyle = "#ffd064";
  ctx.textAlign = "center";
  ctx.font = "800 10px Inter";
  ctx.fillText(`${offscreen.length}`, point.x, point.y + 4);
}

function drawKeepBeacon() {
  if (!isOffscreen(state.keep, 34)) return;
  const point = edgePoint(state.keep, 28);
  drawCircle(point.x, point.y, 14, "rgba(21, 30, 32, .92)", "#b8c987", 2);
  ctx.fillStyle = "#d8e6a8";
  ctx.textAlign = "center";
  ctx.font = "800 12px Inter";
  ctx.fillText("K", point.x, point.y + 4);
}

function drawEnemies() {
  for (const enemy of state.enemies) {
    const point = worldToScreen(enemy.x, enemy.y);
    drawCircle(point.x + 2, point.y + 4, enemy.radius, "rgba(22, 31, 29, .25)");
    const enemyColor = enemy.kind === "runner" ? "#cc704b" : enemy.kind === "brute" ? "#88453e" : enemy.elite ? COLORS.elite : COLORS.enemy;
    drawCircle(point.x, point.y, enemy.radius, enemy.hitTimer ? "#f4b06e" : enemyColor, COLORS.enemyDark, 2);
    ctx.fillStyle = "#2d2726";
    ctx.fillRect(point.x - enemy.radius * 0.55, point.y - 2, 3, 3);
    ctx.fillRect(point.x + enemy.radius * 0.3, point.y - 2, 3, 3);
    if (enemy.elite || enemy.health < enemy.maxHealth) {
      ctx.fillStyle = "#362726";
      ctx.fillRect(point.x - 17, point.y - enemy.radius - 9, 34, 4);
      ctx.fillStyle = enemy.elite ? "#e0994f" : "#c15d4d";
      ctx.fillRect(point.x - 17, point.y - enemy.radius - 9, 34 * enemy.health / enemy.maxHealth, 4);
    }
  }
}

function drawHero() {
  const hero = state.hero;
  const point = worldToScreen(hero.x, hero.y);
  ctx.save();
  ctx.translate(point.x, point.y);
  const angle = Math.atan2(hero.facing.y, hero.facing.x);
  ctx.rotate(angle + Math.PI / 2);
  ctx.globalAlpha = hero.invulnerable && Math.floor(hero.invulnerable * 12) % 2 ? 0.35 : 1;
  ctx.fillStyle = "rgba(15, 24, 23, .28)";
  ctx.beginPath();
  ctx.ellipse(2, 7, 13, 8, 0, 0, TAU);
  ctx.fill();
  ctx.fillStyle = heroProfile().cloak;
  ctx.beginPath();
  ctx.moveTo(0, -11);
  ctx.lineTo(12, 14);
  ctx.lineTo(-12, 14);
  ctx.fill();
  drawCircle(0, -4, 8, COLORS.hero, "#6c4d38", 2);
  ctx.strokeStyle = "#e3bb6a";
  ctx.lineWidth = 2;
  ctx.beginPath();
  ctx.arc(9, -2, 7, -1.6, 1.6);
  ctx.stroke();
  ctx.restore();
  if (hero.abilityCooldown <= 0) {
    ctx.strokeStyle = "rgba(255, 208, 102, .5)";
    ctx.lineWidth = 2;
    ctx.beginPath();
    ctx.arc(point.x, point.y, 18 + Math.sin(performance.now() / 160) * 2, 0, TAU);
    ctx.stroke();
  }
  if (hero.overdrive > 0) {
    ctx.strokeStyle = "rgba(255, 135, 75, .65)";
    ctx.lineWidth = 2;
    ctx.beginPath();
    ctx.arc(point.x, point.y, 23 + Math.sin(performance.now() / 90) * 3, 0, TAU);
    ctx.stroke();
  }
}

function drawPointerTarget() {
  if (!pointerTarget) return;
  const point = worldToScreen(pointerTarget.x, pointerTarget.y);
  ctx.strokeStyle = "rgba(241, 201, 110, .7)";
  ctx.lineWidth = 2;
  ctx.beginPath();
  ctx.arc(point.x, point.y, 10 + Math.sin(performance.now() / 130) * 2, 0, TAU);
  ctx.stroke();
  ctx.beginPath();
  ctx.moveTo(point.x - 4, point.y);
  ctx.lineTo(point.x + 4, point.y);
  ctx.moveTo(point.x, point.y - 4);
  ctx.lineTo(point.x, point.y + 4);
  ctx.stroke();
}

function drawProjectiles() {
  for (const projectile of state.projectiles) {
    const point = worldToScreen(projectile.x, projectile.y);
    drawCircle(point.x, point.y, projectile.radius, projectile.color);
  }
}

function drawEffects() {
  for (const particle of state.particles) {
    const point = worldToScreen(particle.x, particle.y);
    ctx.globalAlpha = clamp(particle.life / particle.maxLife, 0, 1);
    drawCircle(point.x, point.y, particle.size, particle.color);
  }
  ctx.globalAlpha = 1;
  ctx.textAlign = "center";
  ctx.font = "700 11px Inter";
  for (const floater of state.floaters) {
    const point = worldToScreen(floater.x, floater.y);
    ctx.globalAlpha = floater.life / floater.maxLife;
    ctx.fillStyle = floater.color;
    ctx.fillText(floater.text, point.x, point.y);
  }
  ctx.globalAlpha = 1;
}

function draw() {
  ctx.save();
  if (state.shake > 0) ctx.translate((Math.random() - 0.5) * state.shake, (Math.random() - 0.5) * state.shake);
  drawGround();
  drawWalls();
  drawBarricades();
  drawPads();
  drawKeep();
  drawStructures();
  drawDrops();
  drawLootBeacon();
  drawKeepBeacon();
  drawEnemies();
  drawProjectiles();
  drawPointerTarget();
  drawHero();
  drawEffects();
  ctx.restore();
  if (state.paused && !state.over && !ui.settings.classList.contains("show")) {
    ctx.fillStyle = "rgba(11, 17, 19, .56)";
    ctx.fillRect(0, 0, canvas.clientWidth, canvas.clientHeight);
    ctx.fillStyle = "#efd18a";
    ctx.textAlign = "center";
    ctx.font = "800 34px Barlow Condensed";
    ctx.fillText("FORT COMMAND PAUSED", canvas.clientWidth / 2, canvas.clientHeight / 2);
  }
}

function frame(now) {
  const dt = Math.min(0.05, (now - lastFrame) / 1000);
  lastFrame = now;
  update(dt);
  draw();
  requestAnimationFrame(frame);
}

function restart() {
  state = createState();
  keys.clear();
  pointerTarget = null;
  toastTimer = 0;
  introTimer = 7;
  ui.toast.classList.remove("show");
  ui.bossBanner.classList.remove("show");
  ui.gameOver.classList.remove("show");
  ui.boonDraft.classList.remove("show");
  ui.settings.classList.remove("show");
  ui.intro.classList.remove("hidden");
  ui.pause.textContent = "PAUSE";
  updateUi();
  canvas.focus();
}

window.addEventListener("resize", resize);
window.addEventListener("keydown", (event) => {
  if (["ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight", "Space"].includes(event.code)) event.preventDefault();
  keys.add(event.code);
  if (["KeyW", "KeyA", "KeyS", "KeyD", "ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight"].includes(event.code)) {
    ui.intro.classList.add("hidden");
  }
  if (!event.repeat && event.code === "Space") shootVolley();
  if (!event.repeat && ["ShiftLeft", "ShiftRight"].includes(event.code)) dash();
  if (!event.repeat && event.code === "KeyP") setPaused(!state.paused);
  if (!event.repeat && event.code === "Escape") {
    if (ui.settings.classList.contains("show")) closeSettings();
    else setPaused(!state.paused);
  }
  if (!event.repeat && event.code === "KeyH") switchHero();
});
window.addEventListener("keyup", (event) => keys.delete(event.code));
document.addEventListener("visibilitychange", () => {
  if (document.hidden) setPaused(true);
});
canvas.addEventListener("pointerdown", (event) => {
  const rect = canvas.getBoundingClientRect();
  pointerTarget = {
    x: event.clientX - rect.left - view().x + state.camera.x,
    y: event.clientY - rect.top - view().y + state.camera.y,
  };
  ui.intro.classList.add("hidden");
});
ui.pause.addEventListener("click", () => {
  if (ui.settings.classList.contains("show")) closeSettings();
  else openSettings();
});
ui.settingsResume.addEventListener("click", closeSettings);
ui.settingsTabs.addEventListener("click", (e) => {
  const btn = e.target.closest("[data-stab]");
  if (!btn) return;
  ui.settingsTabs.querySelectorAll("[data-stab]").forEach((b) => b.classList.remove("active"));
  btn.classList.add("active");
  activeSettingsTab = btn.dataset.stab;
  showSettingsTab(activeSettingsTab);
});
ui.ability.addEventListener("click", shootVolley);
ui.dash.addEventListener("click", dash);
ui.heroSwitch.addEventListener("click", switchHero);
ui.sound.addEventListener("click", async () => {
  audioContext ??= new AudioContext();
  if (audioContext.state === "suspended") await audioContext.resume();
  soundEnabled = !soundEnabled;
  ui.sound.textContent = `SOUND: ${soundEnabled ? "ON" : "OFF"}`;
  playTone(480, 0.08, "triangle", 0.05);
});
ui.restart.addEventListener("click", restart);
document.querySelectorAll("[data-boon]").forEach((button) => {
  button.addEventListener("click", () => chooseBoon(button.dataset.boon));
});

window.__emberhold = {
  snapshot: () => ({
    wave: state.wave, stage: state.stage, gold: state.gold, kills: state.kills,
    over: state.over, paused: state.paused, bestWave: state.bestWave,
    heroHealth: state.hero.health, keepHealth: state.keep.health, keepMaxHealth: state.keep.maxHealth, enemies: state.enemies.length,
    hero: { x: Math.round(state.hero.x), y: Math.round(state.hero.y) },
    camera: { x: Math.round(state.camera.x), y: Math.round(state.camera.y) },
    heroLevel: state.hero.level,
    heroXp: state.hero.xp, heroDamage: state.hero.damage, heroKind: state.hero.kind, overdrive: state.hero.overdrive,
    volleyCooldown: state.hero.volleyCooldown,
    abilityCooldown: state.hero.abilityCooldown, drops: state.drops.length,
    dashCooldown: state.hero.dashCooldown,
    completed: [...state.completed], towers: state.towers.length, mines: state.mines.length,
    towerLevels: state.towers.map((tower) => ({ id: tower.id, level: tower.level })),
    banners: state.banners.length, armories: state.armories.length, shrines: state.shrines.length, heroes: [...state.heroes],
    barricades: state.barricades.map((barricade) => ({ side: barricade.side, health: barricade.health, maxHealth: barricade.maxHealth })),
    towerPower: state.towerPower, boons: [...state.boons],
  }),
  addGold: (amount = 100) => { state.gold += amount; updateUi(); },
  grantXp: grantHeroXp,
  damageKeep: (amount) => { state.keep.health = Math.max(0, state.keep.health - amount); },
  spawnEmber: () => spawnEmber(state.hero.x, state.hero.y),
  spawnGold: (x = state.hero.x + 100, y = state.hero.y) => spawnDrop(x, y, 1),
  goldDrops: () => state.drops.filter((drop) => drop.kind === "gold").map((drop) => ({ x: drop.x, y: drop.y })),
  padInvestment: (padId) => state.pads.find((pad) => pad.id === padId)?.invested,
  offscreenGold: () => offscreenGoldDrops().length,
  offscreenKeep: () => isOffscreen(state.keep, 34),
  spawnGateRaider: () => {
    spawnEnemy(false);
    const enemy = state.enemies.at(-1);
    enemy.x = 0;
    enemy.y = -fortHalfSize() - 35;
    enemy.gate = { x: 0, y: -fortHalfSize() + 8 };
    enemy.side = 0;
    enemy.health = 9999;
    enemy.maxHealth = 9999;
    enemy.speed = 220;
  },
  spawnWallRaider: () => {
    spawnEnemy(false);
    const enemy = state.enemies.at(-1);
    enemy.x = 80;
    enemy.y = -fortHalfSize() - 35;
    enemy.gate = { x: 80, y: 0 };
    enemy.side = 0;
    enemy.health = 9999;
    enemy.maxHealth = 9999;
    enemy.speed = 220;
    return enemy.id;
  },
  enemyPosition: (id) => {
    const enemy = state.enemies.find((candidate) => candidate.id === id);
    return enemy ? { x: enemy.x, y: enemy.y } : null;
  },
  heroOverlapsSolid: () => solidRects().some((rect) => circleOverlapsRect(state.hero, rect)),
  damageBarricade: (side, amount) => {
    const barricade = state.barricades.find((candidate) => candidate.side === side);
    if (barricade) damageBarricade(barricade, amount);
  },
  dash,
  setFacing: (x, y) => { state.hero.facing = normalize(x, y); },
  testSplash: () => {
    const before = state.kills;
    const makeTarget = (x) => ({
      id: nextId++, x, y: 400, radius: 11, health: 8, maxHealth: 8, speed: 0,
      damage: 0, reward: 1, elite: false, kind: "raider", hitTimer: 0,
      attackTimer: 0, dead: false, gate: { x: 0, y: 0 }, inside: true,
    });
    const first = makeTarget(350);
    state.enemies.push(first, makeTarget(370));
    damageEnemy(first, 20, 50);
    return state.kills - before;
  },
  switchHero,
  offerBoon: showBoonDraft,
  moveHero: (x, y) => { state.hero.x = x; state.hero.y = y; },
  clearWave: () => {
    state.enemies.forEach((enemy) => { enemy.dead = true; });
    state.spawning = null;
    state.waveBonusPending = true;
  },
  lose: () => {
    state.keep.health = 0;
    endGame();
  },
  complete: (padId) => {
    const pad = state.pads.find((candidate) => candidate.id === padId);
    if (pad && padIsAvailable(pad) && !state.completed.has(pad.id)) {
      pad.invested = pad.cost;
      completePad(pad);
    }
  },
  restart,
};

restart();
resize();
requestAnimationFrame(frame);
