export const TAU = Math.PI * 2;

export const BUILD_DEFS = [
  { id: "tower-west",     type: "tower",        stage: 1, x: -116, y:  -58, cost:  20, label: "ARCHER TOWER",    short: "TOWER" },
  { id: "ballista-ne",    type: "ballista",     stage: 1, x:  116, y:  -58, cost:  40, label: "BALLISTA",        short: "BLST" },
  { id: "mine-sw",        type: "mine",         stage: 1, x: -100, y:   80, cost:  30, label: "GOLD MINE",       short: "MINE",   interval: 2.8 },
  { id: "training",       type: "training",     stage: 1, x:    0, y:  112, cost:  45, label: "BOW TRAINING",    short: "TRAIN" },
  { id: "barricade-north", type: "barricade",   stage: 1, x:    0, y:  -78, cost:  28, label: "NORTH BARRICADE", short: "WALL",   side: 0 },
  { id: "barricade-east",  type: "barricade",   stage: 1, x:   78, y:    0, cost:  28, label: "EAST BARRICADE",  short: "WALL",   side: 1 },
  { id: "barricade-south", type: "barricade",   stage: 1, x:    0, y:   78, cost:  28, label: "SOUTH BARRICADE", short: "WALL",   side: 2 },
  { id: "barricade-west",  type: "barricade",   stage: 1, x:  -78, y:    0, cost:  28, label: "WEST BARRICADE",  short: "WALL",   side: 3 },
  { id: "expand-2",       type: "expand",       stage: 1, x:    0, y: -137, cost:  70, label: "EXPAND WALLS",    short: "EXPAND" },
  { id: "tower-north",    type: "tower",        stage: 2, x: -155, y: -128, cost:  55, label: "ARCHER TOWER",    short: "TOWER" },
  { id: "tower-west-2",   type: "tower-upgrade",stage: 2, x: -116, y:  -58, cost:  70, label: "TOWER LEVEL II",  short: "UPGRADE", requires: "tower-west" },
  { id: "ballista-ne-2",  type: "tower-upgrade",stage: 2, x:  116, y:  -58, cost:  80, label: "BALLISTA LVL II", short: "UPGRADE", requires: "ballista-ne" },
  { id: "ballista-east",  type: "ballista",     stage: 2, x:  185, y: -165, cost:  80, label: "BALLISTA",        short: "BLST" },
  { id: "banner",         type: "banner",       stage: 2, x:  144, y: -100, cost:  75, label: "WAR BANNER",      short: "BUFF" },
  { id: "mine-west",      type: "mine",         stage: 2, x: -170, y:   95, cost:  90, label: "DEEP MINE",       short: "MINE+",  interval: 2.0 },
  { id: "repair-yard",    type: "repair",       stage: 2, x:  173, y:  104, cost:  95, label: "REPAIR YARD",     short: "REPAIR" },
  { id: "expand-3",       type: "expand",       stage: 2, x:    0, y: -224, cost: 170, label: "EXPAND WALLS",    short: "EXPAND" },
  { id: "tower-east",     type: "tower",        stage: 3, x:  220, y:   25, cost: 125, label: "ARCHER TOWER",    short: "TOWER" },
  { id: "cannon-south",   type: "cannon",       stage: 3, x:  -15, y:  214, cost: 165, label: "FIRE CANNON",     short: "CANNON" },
  { id: "banner-west",    type: "banner",       stage: 3, x: -234, y:  -20, cost: 140, label: "WAR BANNER",      short: "BUFF" },
  { id: "armory",         type: "armory",       stage: 3, x:  234, y:  126, cost: 205, label: "IRON ARMORY",     short: "ARMORY" },
  { id: "training-2",     type: "training",     stage: 3, x:  133, y:  194, cost: 190, label: "ELITE TRAINING",  short: "TRAIN+" },
  { id: "expand-4",       type: "expand",       stage: 3, x:    0, y: -303, cost: 340, label: "EXPAND WALLS",    short: "EXPAND" },
  { id: "cannon-north",   type: "cannon",       stage: 4, x:  266, y: -187, cost: 280, label: "FIRE CANNON",     short: "CANNON" },
  { id: "mine-south",     type: "mine",         stage: 4, x: -227, y:  213, cost: 240, label: "DEEP MINE",       short: "MINE+",  interval: 1.6 },
  { id: "cannon-south-2", type: "tower-upgrade",stage: 4, x:  -15, y:  214, cost: 290, label: "CANNON LEVEL II", short: "UPGRADE", requires: "cannon-south" },
  { id: "tower-far-west", type: "tower",        stage: 4, x: -303, y: -132, cost: 215, label: "ARCHER TOWER",    short: "TOWER" },
  { id: "ember-shrine",   type: "shrine",       stage: 4, x:  112, y: -264, cost: 310, label: "EMBER SHRINE",    short: "SHRINE" },
  { id: "warden-lodge",   type: "hero",         stage: 4, x: -116, y: -274, cost: 360, label: "WARDEN LODGE",    short: "HERO" },
];

export const COLORS = {
  grass: "#34493f",
  grassDark: "#2a3d36",
  path: "#7b684e",
  pathEdge: "#5d513e",
  wall: "#9d8962",
  wallDark: "#665b48",
  gold: "#f3bd4d",
  fire: "#ed7443",
  enemy: "#b45142",
  enemyDark: "#66332f",
  elite: "#d48b49",
  hero: "#d6b46c",
  heroCloak: "#3d6c65",
  ink: "#1e2928",
};

export const HERO_PROFILES = {
  ranger: { name: "ASH, RANGER", initial: "A", damage: 1, rate: 1, range: 1, speed: 1, cloak: COLORS.heroCloak },
  warden: { name: "MIRA, WARDEN", initial: "M", damage: 1.48, rate: 1.28, range: 0.86, speed: 0.92, cloak: "#765348" },
};

export const ENEMY_PROFILES = {
  raider: { health: 1, speed: 1, damage: 1, reward: 1, radius: 11 },
  runner: { health: 0.72, speed: 1.38, damage: 0.82, reward: 1, radius: 9 },
  brute: { health: 2.1, speed: 0.72, damage: 1.65, reward: 2, radius: 15 },
  elite: { health: 3.5, speed: 0.82, damage: 2, reward: 5, radius: 17 },
};
