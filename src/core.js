export const clamp = (value, min, max) => Math.max(min, Math.min(max, value));

export const distance = (a, b) => Math.hypot(a.x - b.x, a.y - b.y);

export function normalize(x, y) {
  const length = Math.hypot(x, y);
  return length ? { x: x / length, y: y / length } : { x: 0, y: 0 };
}

export function nearest(origin, items, predicate = () => true) {
  let result = null;
  let best = Infinity;
  for (const item of items) {
    if (!predicate(item)) continue;
    const nextDistance = distance(origin, item);
    if (nextDistance < best) {
      best = nextDistance;
      result = item;
    }
  }
  return result;
}

export function waveStats(wave) {
  const tier = Math.floor((wave - 1) / 5);
  return {
    count: 4 + Math.floor(wave * 1.55),
    health: 22 + wave * 6 + tier * 12,
    speed: Math.min(46 + wave * 1.25, 89),
    damage: 5 + Math.floor(wave / 3),
    interval: Math.max(0.34, 0.88 - wave * 0.018),
    reward: 2 + Math.floor(wave / 4),
    elite: wave % 5 === 0,
  };
}

export function depositAmount(gold, remainingCost, rate, dt) {
  return clamp(Math.min(gold, remainingCost, Math.floor(rate * dt)), 0, remainingCost);
}

export function attractionSpeed(distanceToHero, maxDistance = 112) {
  if (distanceToHero <= 0 || distanceToHero >= maxDistance) return 0;
  return 42 + (maxDistance - distanceToHero) * 2.15;
}
