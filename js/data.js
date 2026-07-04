// ══════════════════════════════════════════════════════════════
// Mushmire — data.js
// Tiles, items, recipes, enemies, biomes, pixel-art helpers.
// ══════════════════════════════════════════════════════════════

// ── seeded RNG ──
export function mulberry32(seed) {
  let a = seed >>> 0;
  return function () {
    a |= 0; a = (a + 0x6D2B79F5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

// ── pixel-art builder: array of strings + color map → canvas ──
export function px(art, colors, scale = 1) {
  const h = art.length, w = art[0].length;
  const c = document.createElement('canvas');
  c.width = w * scale; c.height = h * scale;
  const g = c.getContext('2d');
  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      const ch = art[y][x];
      if (ch === '.' || ch === ' ') continue;
      g.fillStyle = colors[ch] || '#f0f';
      g.fillRect(x * scale, y * scale, scale, scale);
    }
  }
  return c;
}

// ══════════════ TILES ══════════════
export const T = {
  AIR: 0, DIRT: 1, GRASS: 2, SAND: 3, SANDSTONE: 4, STONE: 5,
  TRUNK: 6, LEAVES: 7, WATER: 8, PLANKS: 9, CLAY: 10, CORAL: 11,
  KELP: 12, TEMPLE: 13, GOLDTILE: 14, JADE: 15, COPPER: 16,
  GOLDORE: 17, TORCH: 18, WORKBENCH: 19, CACTUS: 20, FLOWER: 21,
  BAMBOO: 22, LANTERN: 23, SHELL: 24,
};

// hp = pickaxe hits to break · tier = min pickaxe tier · drop = item id
export const TILE_DEFS = {
  [T.AIR]:       { name: 'air', solid: false },
  [T.DIRT]:      { name: 'Dirt', solid: true, hp: 2, tier: 0, drop: 'dirt', base: '#6b4a32', spk: '#7d5a3e' },
  [T.GRASS]:     { name: 'Grass', solid: true, hp: 2, tier: 0, drop: 'dirt', base: '#6b4a32', spk: '#7d5a3e', top: '#4e9e4a' },
  [T.SAND]:      { name: 'Sand', solid: true, hp: 2, tier: 0, drop: 'sand', base: '#e2c290', spk: '#d1ad74' },
  [T.SANDSTONE]: { name: 'Sandstone', solid: true, hp: 4, tier: 0, drop: 'sandstone', base: '#c9a468', spk: '#b58e52', brick: '#a8875c' },
  [T.STONE]:     { name: 'Stone', solid: true, hp: 4, tier: 0, drop: 'stone', base: '#7a7a85', spk: '#8d8d98' },
  [T.TRUNK]:     { name: 'Tree', solid: false, hp: 3, tier: 0, drop: 'wood', base: '#7d5a3e', spk: '#6b4a32' },
  [T.LEAVES]:    { name: 'Leaves', solid: false, hp: 1, tier: 0, drop: null, base: '#3d8c46', spk: '#4ea355' },
  [T.WATER]:     { name: 'Water', solid: false, liquid: true },
  [T.PLANKS]:    { name: 'Planks', solid: true, hp: 3, tier: 0, drop: 'planks', base: '#a3764a', spk: '#8f6539', brick: '#8f6539' },
  [T.CLAY]:      { name: 'Mud', solid: true, hp: 2, tier: 0, drop: 'clay', base: '#8a5f4d', spk: '#79523f' },
  [T.CORAL]:     { name: 'Coral', solid: true, hp: 3, tier: 0, drop: 'coral', base: '#e87b8f', spk: '#f2a0b0' },
  [T.KELP]:      { name: 'Kelp', solid: false, hp: 1, tier: 0, drop: 'kelp', base: '#2e7d5b', spk: '#3d9c72' },
  [T.TEMPLE]:    { name: 'Temple Brick', solid: true, hp: 5, tier: 0, drop: 'temple_brick', base: '#9c8a6e', spk: '#8a785c', brick: '#7d6c52' },
  [T.GOLDTILE]:  { name: 'Gilded Tile', solid: true, hp: 5, tier: 0, drop: 'gold_tile', base: '#d9a833', spk: '#f2c14e', brick: '#b8891f', light: 0.35 },
  [T.JADE]:      { name: 'Jade Ore', solid: true, hp: 7, tier: 2, drop: 'jade', base: '#7a7a85', spk: '#4ec98a', ore: '#3aa86e' },
  [T.COPPER]:    { name: 'Copper Ore', solid: true, hp: 5, tier: 1, drop: 'copper', base: '#7a7a85', spk: '#c97b4e', ore: '#b5643a' },
  [T.GOLDORE]:   { name: 'Gold Ore', solid: true, hp: 6, tier: 1, drop: 'goldore', base: '#7a7a85', spk: '#e8c04e', ore: '#d9a833' },
  [T.TORCH]:     { name: 'Torch', solid: false, hp: 1, tier: 0, drop: 'torch', light: 1 },
  [T.WORKBENCH]: { name: 'Workbench', solid: false, hp: 2, tier: 0, drop: 'workbench' },
  [T.CACTUS]:    { name: 'Cactus', solid: false, hp: 2, tier: 0, drop: 'cactus', base: '#4e8c4a', spk: '#5da356' },
  [T.FLOWER]:    { name: 'Glowbloom', solid: false, hp: 1, tier: 0, drop: 'petal', light: 0.25 },
  [T.BAMBOO]:    { name: 'Bamboo', solid: false, hp: 2, tier: 0, drop: 'bamboo', base: '#8fbf5a', spk: '#a3d468' },
  [T.LANTERN]:   { name: 'Silk Lantern', solid: false, hp: 1, tier: 0, drop: 'lantern', light: 1.2 },
  [T.SHELL]:     { name: 'Shell', solid: false, hp: 1, tier: 0, drop: 'shell' },
};

export const isSolid = id => !!TILE_DEFS[id]?.solid;
export const isLiquid = id => !!TILE_DEFS[id]?.liquid;

// ── generate 16×16 tile textures ──
export const TILE_TEX = {};
export function buildTileTextures() {
  const rng = mulberry32(777);
  for (const idStr in TILE_DEFS) {
    const id = +idStr, d = TILE_DEFS[id];
    if (id === T.AIR || id === T.WATER) continue;
    const c = document.createElement('canvas');
    c.width = 16; c.height = 16;
    const g = c.getContext('2d');

    // decorative tiles get bespoke art
    if (id === T.TORCH) { TILE_TEX[id] = px(TORCH_ART, SPRITE_COLORS); continue; }
    if (id === T.LANTERN) { TILE_TEX[id] = px(LANTERN_ART, SPRITE_COLORS); continue; }
    if (id === T.FLOWER) { TILE_TEX[id] = px(FLOWER_ART, SPRITE_COLORS); continue; }
    if (id === T.WORKBENCH) { TILE_TEX[id] = px(BENCH_ART, SPRITE_COLORS); continue; }
    if (id === T.SHELL) { TILE_TEX[id] = px(SHELL_ART, SPRITE_COLORS); continue; }
    if (id === T.CACTUS) { TILE_TEX[id] = px(CACTUS_ART, SPRITE_COLORS); continue; }
    if (id === T.KELP) { TILE_TEX[id] = px(KELP_ART, SPRITE_COLORS); continue; }
    if (id === T.BAMBOO) { TILE_TEX[id] = px(BAMBOO_ART, SPRITE_COLORS); continue; }
    if (id === T.TRUNK) { TILE_TEX[id] = px(TRUNK_ART, SPRITE_COLORS); continue; }

    g.fillStyle = d.base; g.fillRect(0, 0, 16, 16);
    // speckle
    for (let i = 0; i < 26; i++) {
      g.fillStyle = d.spk;
      g.fillRect((rng() * 16) | 0, (rng() * 16) | 0, 1 + (rng() < 0.3 ? 1 : 0), 1);
    }
    // ore veins
    if (d.ore) {
      for (let i = 0; i < 5; i++) {
        g.fillStyle = d.ore;
        const ox = 2 + rng() * 11 | 0, oy = 2 + rng() * 11 | 0;
        g.fillRect(ox, oy, 2, 2); g.fillRect(ox + 1, oy - 1, 1, 1);
      }
    }
    // brick pattern
    if (d.brick) {
      g.fillStyle = d.brick;
      g.fillRect(0, 5, 16, 1); g.fillRect(0, 11, 16, 1);
      g.fillRect(4, 0, 1, 5); g.fillRect(11, 6, 1, 5); g.fillRect(6, 12, 1, 4);
    }
    // grass top
    if (d.top) {
      g.fillStyle = d.top; g.fillRect(0, 0, 16, 3);
      g.fillStyle = '#5db858';
      for (let i = 0; i < 16; i += 2) if (rng() < 0.6) g.fillRect(i, 3, 1, 1);
    }
    // subtle edge shading
    g.fillStyle = 'rgba(0,0,0,0.14)'; g.fillRect(0, 15, 16, 1); g.fillRect(15, 0, 1, 16);
    g.fillStyle = 'rgba(255,255,255,0.10)'; g.fillRect(0, 0, 16, 1);
    TILE_TEX[id] = c;
  }
}

// ══════════════ SPRITE ART ══════════════
export const SPRITE_COLORS = {
  // generic
  k: '#1a1216', w: '#f5f0e8', r: '#e05656', o: '#e8933a', y: '#f2c14e',
  g: '#4e9e4a', G: '#2e6b44', b: '#4a7ac9', p: '#b48cf2', P: '#7a4fa3',
  t: '#e2c290', T: '#a8875c', n: '#7d5a3e', N: '#5e4025', s: '#8d8d98',
  S: '#55555e', f: '#e8933a', F: '#c9722a', m: '#d98fb5', e: '#8fe0c9',
  h: '#f2ddb0', d: '#3d3550', L: '#a3d468', l: '#8fbf5a', q: '#e87b8f',
  z: '#66d9e8', x: '#c9a468',
};

const TORCH_ART = [
  '................', '................', '................', '......yy........',
  '......yy........', '.....yffy.......', '.....yffy.......', '......ff........',
  '......nn........', '......nn........', '......nn........', '......nn........',
  '......nn........', '......nn........', '................', '................',
];
const LANTERN_ART = [
  '................', '......NN........', '.....qqqq.......', '....qyyyyq......',
  '....qyhhyq......', '....qyhhyq......', '....qyyyyq......', '.....qqqq.......',
  '......yy........', '................', '................', '................',
  '................', '................', '................', '................',
];
const FLOWER_ART = [
  '................', '................', '................', '................',
  '................', '......m.........', '.....mpm........', '....mphpm.......',
  '.....mpm........', '......m.........', '......G.........', '......G.........',
  '.....GG.........', '......G.........', '................', '................',
];
const BENCH_ART = [
  '................', '................', '................', '................',
  '................', '................', '................', '................',
  'nnnnnnnnnnnnnnnn', 'nNNNNNNNNNNNNNNn', '.nn..........nn.', '.nn..........nn.',
  '.nn..........nn.', '.nn..........nn.', '.nn..........nn.', '................',
];
const SHELL_ART = [
  '................', '................', '................', '................',
  '................', '................', '................', '................',
  '................', '......mm........', '.....mhhm.......', '....mhwhhm......',
  '....mhhwhm......', '.....mmmm.......', '................', '................',
];
const CACTUS_ART = [
  '................', '.....gg.........', '.....gg.........', '.....gg.........',
  '.gg..gg..gg.....', '.gg..gg..gg.....', '.gg..gg..gg.....', '.gggggg..gg.....',
  '.....gggggg.....', '.....gg.........', '.....gg.........', '.....gg.........',
  '.....gg.........', '.....gg.........', '.....gg.........', '.....gg.........',
];
const KELP_ART = [
  '......G.........', '.....GG.........', '.....G..........', '......G.........',
  '......GG........', '.....GG.........', '.....G..........', '......G.........',
  '......GG........', '.....GG.........', '.....G..........', '......G.........',
  '......GG........', '.....GG.........', '......G.........', '......G.........',
];
const TRUNK_ART = [
  '.....nnnnnn.....', '.....nNnnnn.....', '.....nnnnNn.....', '.....nnnnnn.....',
  '.....nNnnnn.....', '.....nnnnnn.....', '.....nnnNnn.....', '.....nnnnnn.....',
  '.....nnnnnN.....', '.....nNnnnn.....', '.....nnnnnn.....', '.....nnnNnn.....',
  '.....nnnnnn.....', '.....nnnnnn.....', '.....nNnnnn.....', '.....nnnnnn.....',
];
const BAMBOO_ART = [
  '.....ll.........', '.....ll.....L...', '.....llLL.......', '.....ll.........',
  '....Llll........', '.....ll.........', '.....ll.........', '.....llL........',
  '.....ll..L......', '....Lll.........', '.....ll.........', '.....ll.........',
  '.....llL........', '.....ll.........', '....Lll.........', '.....ll.........',
];

// ── enemy / creature art (12–14 px wide) ──
export const ENEMY_ART = {
  duneShade: [
    '....tttt....', '...tttttt...', '..tthtthtt..', '..tthtthtt..',
    '..tttttttt..', '...tttttt...', '..tttttttt..', '.t.tttttt.t.',
    '...t.tt.t...', '....t..t....',
  ],
  scarab: [
    '............', '............', '............', '...kk..kk...',
    '..kxxxxxxk..', '.kxxkxxkxxk.', '.kxxxxxxxxk.', '..kxxxxxxk..',
    '.k.k.kk.k.k.', '............',
  ],
  stalker: [
    '............', '.kk.........', '.kkk....kkk.', '.kpkkkkkkkk.',
    '.kkkkkkkkkk.', '..kkkkkkkk..', '..kk....kk..', '..k......k..',
    '............', '............',
  ],
  sporeling: [
    '....pppp....', '..pppppppp..', '.pphpppphpp.', '.pppppppppp.',
    '...whhhhw...', '...whkkhw...', '...whhhhw...', '....w..w....',
    '............', '............',
  ],
  tideWisp: [
    '....zzzz....', '...zzzzzz...', '..zzwzzwzz..', '..zzzzzzzz..',
    '...zzzzzz...', '..zzzzzzzz..', '...z.zz.z...', '....z..z....',
    '......z.....', '............',
  ],
  crabby: [
    '............', '............', '.qq......qq.', '.q..q..q..q.',
    '..qqqqqqqq..', '.qqkqqqqkqq.', '..qqqqqqqq..', '..q.q..q.q..',
    '............', '............',
  ],
  hungryGhost: [
    '....eeee....', '...eeeeee...', '..eekeekee..', '..eeeeeeee..',
    '..eee..eee..', '..eeeeeeee..', '...eeeeee...', '..ee.ee.ee..',
    '...e.ee.e...', '....e..e....',
  ],
  paperLantern: [
    '.....kk.....', '....rrrr....', '...ryyyyr...', '...ryhhyr...',
    '...rykkyr...', '...ryhhyr...', '...ryyyyr...', '....rrrr....',
    '.....yy.....', '............',
  ],
};

export const CREATURE_ART = {
  cat: [
    '..............', '.k.k..........', '.kkk..........', '.kok.......kk.',
    '.kkkkkkkkkkk..', '.kkkkkkkkkk...', '.kkkkkkkkkk...', '.k..k..k..k...',
  ],
  catOrange: [
    '..............', '.f.f..........', '.fff..........', '.fkf.......ff.',
    '.fffffffffff..', '.ffwwwwwfff...', '.ffwwwwwfff...', '.f..f..f..f...',
  ],
  firefly: [
    '.ww.', 'wyyw', 'wyyw', '.ww.',
  ],
  scarabPet: [
    '...kk..kk...', '..kyyyyyyk..', '.kyykyykyyk.', '..kyyyyyyk..',
    '.k.k.kk.k.k.', '............',
  ],
  spriteWisp: [
    '..pp..', '.phhp.', '.phhp.', '..pp..', '.p..p.',
  ],
  fishy: [
    '........', '..zzz..z', '.zkzzzzz', '..zzz..z', '........',
  ],
};

// ── tool / weapon / item icon art ──
const PICK_ART = t => [
  '................', '....' + t + t + t + t + t + t + '......', '...' + t + '......' + t + '.....',
  '...' + t + '.......' + t + '....', '..........nn....', '.........nn.....',
  '........nn......', '.......nn.......', '......nn........', '.....nn.........',
  '....nn..........', '...nn...........', '..nn............', '.nn.............',
  '................', '................',
];
const SWORD_ART = t => [
  '..........' + t + t + '..', '.........' + t + t + t + '..', '........' + t + t + t + '...',
  '.......' + t + t + t + '....', '......' + t + t + t + '.....', '.....' + t + t + t + '......',
  '....' + t + t + t + '.......', '...' + t + t + t + '........', '..n' + t + t + '.........',
  '.nnn............', 'nn.nn...........', 'n...............', '................',
  '................', '................', '................',
];

export function toolArt(kind, tierChar) {
  return kind === 'pick' ? PICK_ART(tierChar) : SWORD_ART(tierChar);
}

// ══════════════ ITEMS ══════════════
// type: block | tool | weapon | mat | use | charm
export const ITEMS = {
  dirt:        { name: 'Dirt', type: 'block', tile: T.DIRT, stack: 99 },
  sand:        { name: 'Sand', type: 'block', tile: T.SAND, stack: 99 },
  stone:       { name: 'Stone', type: 'block', tile: T.STONE, stack: 99 },
  sandstone:   { name: 'Sandstone', type: 'block', tile: T.SANDSTONE, stack: 99 },
  clay:        { name: 'Mud', type: 'block', tile: T.CLAY, stack: 99 },
  planks:      { name: 'Planks', type: 'block', tile: T.PLANKS, stack: 99 },
  coral:       { name: 'Coral', type: 'block', tile: T.CORAL, stack: 99 },
  temple_brick:{ name: 'Temple Brick', type: 'block', tile: T.TEMPLE, stack: 99 },
  gold_tile:   { name: 'Gilded Tile', type: 'block', tile: T.GOLDTILE, stack: 99 },
  torch:       { name: 'Torch', type: 'block', tile: T.TORCH, stack: 99 },
  lantern:     { name: 'Silk Lantern', type: 'block', tile: T.LANTERN, stack: 99 },
  workbench:   { name: 'Workbench', type: 'block', tile: T.WORKBENCH, stack: 99 },

  wood:        { name: 'Wood', type: 'mat', stack: 99 },
  kelp:        { name: 'Kelp', type: 'mat', stack: 99 },
  cactus:      { name: 'Cactus Flesh', type: 'mat', stack: 99 },
  petal:       { name: 'Glowbloom Petal', type: 'mat', stack: 99 },
  bamboo:      { name: 'Bamboo', type: 'mat', stack: 99 },
  shell:       { name: 'Moon Shell', type: 'mat', stack: 99 },
  copper:      { name: 'Copper Chunk', type: 'mat', stack: 99 },
  goldore:     { name: 'Gold Nugget', type: 'mat', stack: 99 },
  jade:        { name: 'Raw Jade', type: 'mat', stack: 99 },
  essence:     { name: 'Night Essence', type: 'mat', stack: 99 },

  wood_pick:   { name: 'Wood Pick', type: 'tool', tier: 1, power: 1, dmg: 2, stack: 1, artChar: 'n' },
  stone_pick:  { name: 'Stone Pick', type: 'tool', tier: 2, power: 1.6, dmg: 3, stack: 1, artChar: 's' },
  copper_pick: { name: 'Copper Pick', type: 'tool', tier: 3, power: 2.4, dmg: 4, stack: 1, artChar: 'F' },
  jade_pick:   { name: 'Jade Pick', type: 'tool', tier: 4, power: 3.4, dmg: 5, stack: 1, artChar: 'G' },

  wood_sword:  { name: 'Wood Sword', type: 'weapon', dmg: 4, stack: 1, artChar: 'n' },
  stone_sword: { name: 'Stone Sword', type: 'weapon', dmg: 6, stack: 1, artChar: 's' },
  copper_sword:{ name: 'Copper Sword', type: 'weapon', dmg: 9, stack: 1, artChar: 'F' },
  jade_sword:  { name: 'Jade Sword', type: 'weapon', dmg: 13, stack: 1, artChar: 'G' },

  salve:       { name: 'Healing Salve', type: 'use', heal: 6, stack: 20 },
  cat_charm:   { name: 'Cat Charm', type: 'charm', creature: 'cat', stack: 20 },
  scarab_charm:{ name: 'Scarab Charm', type: 'charm', creature: 'scarabPet', stack: 20 },
  sprite_charm:{ name: 'Sprite Charm', type: 'charm', creature: 'spriteWisp', stack: 20 },
};

// icon canvases, built once
export const ITEM_ICONS = {};
export function buildItemIcons() {
  for (const id in ITEMS) {
    const it = ITEMS[id];
    if (it.type === 'block' && TILE_TEX[it.tile]) { ITEM_ICONS[id] = TILE_TEX[it.tile]; continue; }
    if (it.type === 'tool') { ITEM_ICONS[id] = px(toolArt('pick', it.artChar), SPRITE_COLORS); continue; }
    if (it.type === 'weapon') { ITEM_ICONS[id] = px(toolArt('sword', it.artChar), SPRITE_COLORS); continue; }
    // materials & misc: simple bespoke glyphs
    const c = document.createElement('canvas'); c.width = 16; c.height = 16;
    const g = c.getContext('2d');
    const blob = (color, hi) => {
      g.fillStyle = color; g.beginPath(); g.arc(8, 9, 5, 0, 7); g.fill();
      g.fillStyle = hi; g.fillRect(6, 6, 2, 2);
    };
    switch (id) {
      case 'wood': g.fillStyle = '#7d5a3e'; g.fillRect(3, 5, 10, 7); g.fillStyle = '#5e4025'; g.fillRect(3, 7, 10, 1); g.fillRect(3, 10, 10, 1); break;
      case 'kelp': g.fillStyle = '#2e7d5b'; g.fillRect(7, 2, 2, 12); g.fillRect(5, 4, 2, 2); g.fillRect(9, 8, 2, 2); break;
      case 'cactus': blob('#4e8c4a', '#6db868'); break;
      case 'petal': blob('#d98fb5', '#f2ddb0'); break;
      case 'bamboo': g.fillStyle = '#8fbf5a'; g.fillRect(6, 2, 3, 12); g.fillStyle = '#6b9c3e'; g.fillRect(6, 5, 3, 1); g.fillRect(6, 9, 3, 1); break;
      case 'shell': blob('#d98fb5', '#f5f0e8'); break;
      case 'copper': blob('#b5643a', '#c97b4e'); break;
      case 'goldore': blob('#d9a833', '#f2c14e'); break;
      case 'jade': blob('#3aa86e', '#4ec98a'); break;
      case 'essence': blob('#b48cf2', '#e0ccff'); break;
      case 'salve': g.fillStyle = '#e05656'; g.fillRect(5, 6, 6, 8); g.fillStyle = '#f2a0b0'; g.fillRect(6, 7, 2, 2); g.fillStyle = '#a8875c'; g.fillRect(6, 4, 4, 2); break;
      case 'cat_charm': ITEM_ICONS[id] = px(CREATURE_ART.catOrange, SPRITE_COLORS); continue;
      case 'scarab_charm': ITEM_ICONS[id] = px(CREATURE_ART.scarabPet, SPRITE_COLORS); continue;
      case 'sprite_charm': ITEM_ICONS[id] = px(CREATURE_ART.spriteWisp, SPRITE_COLORS, 2); continue;
      default: blob('#8d8d98', '#c0c0c8');
    }
    ITEM_ICONS[id] = c;
  }
}

// ══════════════ RECIPES ══════════════
// bench: requires standing near a workbench
export const RECIPES = [
  { out: 'planks', n: 4, cost: { wood: 1 }, cat: 'Blocks' },
  { out: 'workbench', n: 1, cost: { planks: 8 }, cat: 'Blocks' },
  { out: 'torch', n: 3, cost: { wood: 1, petal: 1 }, cat: 'Blocks' },
  { out: 'sandstone', n: 1, cost: { sand: 2 }, bench: true, cat: 'Blocks' },
  { out: 'temple_brick', n: 2, cost: { stone: 1, sand: 1 }, bench: true, cat: 'Blocks' },
  { out: 'gold_tile', n: 1, cost: { goldore: 1, stone: 1 }, bench: true, cat: 'Blocks' },
  { out: 'lantern', n: 1, cost: { bamboo: 3, petal: 2, shell: 1 }, bench: true, cat: 'Blocks' },

  { out: 'wood_pick', n: 1, cost: { wood: 8 }, cat: 'Tools' },
  { out: 'wood_sword', n: 1, cost: { wood: 7 }, cat: 'Tools' },
  { out: 'stone_pick', n: 1, cost: { stone: 8, wood: 4 }, bench: true, cat: 'Tools' },
  { out: 'stone_sword', n: 1, cost: { stone: 6, wood: 3 }, bench: true, cat: 'Tools' },
  { out: 'copper_pick', n: 1, cost: { copper: 8, wood: 4 }, bench: true, cat: 'Tools' },
  { out: 'copper_sword', n: 1, cost: { copper: 6, wood: 3 }, bench: true, cat: 'Tools' },
  { out: 'jade_pick', n: 1, cost: { jade: 8, bamboo: 4 }, bench: true, cat: 'Tools' },
  { out: 'jade_sword', n: 1, cost: { jade: 6, bamboo: 3 }, bench: true, cat: 'Tools' },

  { out: 'salve', n: 1, cost: { petal: 3, kelp: 1 }, cat: 'Charms & Cures' },
  { out: 'cat_charm', n: 1, cost: { bamboo: 2, essence: 3 }, bench: true, cat: 'Charms & Cures' },
  { out: 'scarab_charm', n: 1, cost: { sand: 5, essence: 3 }, bench: true, cat: 'Charms & Cures' },
  { out: 'sprite_charm', n: 1, cost: { petal: 2, essence: 3 }, bench: true, cat: 'Charms & Cures' },
];

// ══════════════ BIOMES ══════════════
export const WORLD_W = 1200, WORLD_H = 220;
export const BIOMES = [
  { key: 'desert', label: '🏜 Sunken Dunes', x0: 0, x1: 300 },
  { key: 'tropic', label: '🌴 Verdant Tropics', x0: 300, x1: 600 },
  { key: 'ocean', label: '🌊 Glass Ocean', x0: 600, x1: 900 },
  { key: 'lantern', label: '🏮 Lantern Coast', x0: 900, x1: 1200 },
];
export function biomeAt(tx) {
  for (const b of BIOMES) if (tx >= b.x0 && tx < b.x1) return b;
  return BIOMES[tx < 0 ? 0 : BIOMES.length - 1];
}

// ══════════════ ENEMIES ══════════════
export const ENEMY_TYPES = {
  duneShade:    { name: 'Dune Shade', biome: 'desert', hp: 14, dmg: 2, speed: 22, fly: true, drop: 'essence', dropN: 2 },
  scarab:       { name: 'Gilded Scarab', biome: 'desert', hp: 8, dmg: 1, speed: 55, fly: false, drop: 'essence', dropN: 1 },
  stalker:      { name: 'Jungle Stalker', biome: 'tropic', hp: 20, dmg: 3, speed: 42, fly: false, drop: 'essence', dropN: 2 },
  sporeling:    { name: 'Sporeling', biome: 'tropic', hp: 8, dmg: 1, speed: 30, fly: false, drop: 'essence', dropN: 1 },
  tideWisp:     { name: 'Tide Wisp', biome: 'ocean', hp: 12, dmg: 2, speed: 26, fly: true, drop: 'essence', dropN: 2 },
  crabby:       { name: 'Moonshell Crab', biome: 'ocean', hp: 16, dmg: 2, speed: 24, fly: false, drop: 'shell', dropN: 1 },
  hungryGhost:  { name: 'Hungry Ghost', biome: 'lantern', hp: 18, dmg: 3, speed: 24, fly: true, drop: 'essence', dropN: 3 },
  paperLantern: { name: 'Stray Lantern', biome: 'lantern', hp: 10, dmg: 2, speed: 30, fly: true, drop: 'essence', dropN: 2 },
};

// forge palette (mystic + sand pops)
export const FORGE_PALETTE = [
  '#1a1216', '#f5f0e8', '#e2c290', '#a8875c',
  '#b48cf2', '#7a4fa3', '#e05656', '#e8933a',
  '#f2c14e', '#4e9e4a', '#2e7d5b', '#4a7ac9',
  '#66d9e8', '#d98fb5', '#8d8d98', '#7d5a3e',
];
