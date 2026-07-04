// ══════════════════════════════════════════════════════════════
// Mushmire — world.js
// Procedural world generation, tile access, serialization.
// ══════════════════════════════════════════════════════════════
import { T, WORLD_W, WORLD_H, biomeAt, mulberry32, isSolid, isLiquid, TILE_DEFS } from './data.js';

export const SEA_LEVEL = 104;

export class World {
  constructor() {
    this.w = WORLD_W; this.h = WORLD_H;
    this.tiles = new Uint8Array(this.w * this.h);
    this.heights = new Int16Array(this.w);
    this.damage = new Map();        // 'x,y' → accumulated mining damage
    this.spawn = { x: 450, y: 90 };
    this.templeSpots = [];          // for cat spawning
  }

  get(x, y) {
    if (y < 0) return T.AIR;
    if (x < 0 || x >= this.w || y >= this.h) return T.STONE;
    return this.tiles[y * this.w + x];
  }
  set(x, y, id) {
    if (x < 0 || x >= this.w || y < 0 || y >= this.h) return;
    this.tiles[y * this.w + x] = id;
  }
  solidAt(x, y) { return isSolid(this.get(x, y)); }
  liquidAt(x, y) { return isLiquid(this.get(x, y)); }

  surfaceAt(tx) {
    for (let y = 0; y < this.h; y++) if (isSolid(this.get(tx, y))) return y;
    return this.h - 1;
  }

  // ─────────── GENERATION ───────────
  generate(seed = 12345) {
    const rng = mulberry32(seed);
    const { w, h } = this;
    this.tiles.fill(T.AIR);

    // 1. surface heightmap
    const raw = new Float32Array(w);
    const ph1 = rng() * 99, ph2 = rng() * 99, ph3 = rng() * 99;
    for (let x = 0; x < w; x++) {
      const b = biomeAt(x).key;
      let base = 108, amp = 8;
      if (b === 'desert') { base = 106; amp = 10; }
      if (b === 'tropic') { base = 104; amp = 12; }
      if (b === 'ocean') { base = 148; amp = 6; }     // sea floor
      if (b === 'lantern') { base = 105; amp = 8; }
      raw[x] = base
        + Math.sin(x * 0.045 + ph1) * amp
        + Math.sin(x * 0.11 + ph2) * amp * 0.4
        + Math.sin(x * 0.021 + ph3) * amp * 0.7;
    }
    // ocean islands
    const islands = [665 + rng() * 30 | 0, 780 + rng() * 40 | 0];
    for (const ix of islands) {
      for (let x = ix - 14; x < ix + 14; x++) {
        if (x < 610 || x >= 890) continue;
        const d = Math.abs(x - ix) / 14;
        raw[x] = Math.min(raw[x], SEA_LEVEL - 4 + d * d * 52);
      }
    }
    // smooth (esp. biome borders)
    for (let pass = 0; pass < 3; pass++) {
      for (let x = 1; x < w - 1; x++) raw[x] = (raw[x - 1] + raw[x] * 2 + raw[x + 1]) / 4;
    }
    for (let x = 0; x < w; x++) this.heights[x] = Math.round(raw[x]);

    // 2. strata
    for (let x = 0; x < w; x++) {
      const b = biomeAt(x).key;
      const surf = this.heights[x];
      for (let y = surf; y < h; y++) {
        const depth = y - surf;
        let t = T.STONE;
        if (b === 'desert') t = depth < 7 ? T.SAND : depth < 26 ? T.SANDSTONE : T.STONE;
        else if (b === 'ocean') t = depth < 5 ? T.SAND : depth < 14 ? T.CLAY : T.STONE;
        else t = depth === 0 ? T.GRASS : depth < 8 ? T.DIRT : T.STONE;
        this.set(x, y, t);
      }
      // water fill for ocean & low pockets near it
      if (b === 'ocean') {
        for (let y = SEA_LEVEL; y < surf; y++) this.set(x, y, T.WATER);
      }
    }
    // beach edges into the ocean biome neighbours
    for (let x = 570; x < 630; x++) this.paintSurface(x, T.SAND, 4);
    for (let x = 880; x < 930; x++) this.paintSurface(x, T.SAND, 3);

    // 3. caves (random walkers through the deep)
    const caveCount = 26;
    for (let i = 0; i < caveCount; i++) {
      let cx = rng() * w, cy = 130 + rng() * 70, ang = rng() * Math.PI * 2;
      const steps = 60 + rng() * 120;
      for (let s = 0; s < steps; s++) {
        const r = 1.6 + rng() * 1.8;
        this.carve(cx | 0, cy | 0, r);
        ang += (rng() - 0.5) * 0.9;
        cx += Math.cos(ang) * 2; cy += Math.sin(ang) * 1.2;
        if (cy < 118) cy = 118; if (cy > h - 6) cy = h - 6;
      }
    }

    // 4. ores
    this.scatterOre(rng, T.COPPER, 300, 600, 90, 118, 200);   // tropic copper, shallow-deep
    this.scatterOre(rng, T.COPPER, 0, 1200, 130, 200, 260);   // copper everywhere deep
    this.scatterOre(rng, T.GOLDORE, 0, 300, 115, 195, 170);   // desert gold
    this.scatterOre(rng, T.JADE, 900, 1200, 120, 200, 170);   // lantern jade
    this.scatterOre(rng, T.JADE, 600, 900, 160, 205, 60);     // a little under the sea

    // 5. decoration per biome
    let lastTreeX = -9;
    for (let x = 2; x < w - 2; x++) {
      const b = biomeAt(x).key;
      const surf = this.heights[x];
      const ground = this.get(x, surf);
      if (!isSolid(ground)) continue;
      const above = surf - 1;
      if (this.get(x, above) !== T.AIR && this.get(x, above) !== T.WATER) continue;

      if (b === 'desert' && ground === T.SAND && rng() < 0.045) this.growCactus(rng, x, above);
      if (b === 'tropic' && ground === T.GRASS) {
        if (rng() < 0.09 && x - lastTreeX >= 4) { this.growTree(rng, x, above, 4 + (rng() * 4 | 0)); lastTreeX = x; }
        else if (rng() < 0.06) this.set(x, above, T.FLOWER);
      }
      if (b === 'ocean') {
        if (this.get(x, above) === T.WATER && ground === T.SAND) {
          if (rng() < 0.10) this.growKelp(rng, x, above);
          else if (rng() < 0.06) this.growCoral(rng, x, surf);
          else if (rng() < 0.05) this.set(x, above, T.SHELL);
        }
        if (this.get(x, above) === T.AIR && ground === T.SAND && rng() < 0.15 && x - lastTreeX >= 5) {
          this.growTree(rng, x, above, 5 + (rng() * 3 | 0)); // island palms
          lastTreeX = x;
        }
      }
      if (b === 'lantern' && ground === T.GRASS) {
        if (rng() < 0.08) this.growBamboo(rng, x, above);
        else if (rng() < 0.05) this.set(x, above, T.FLOWER);
        else if (rng() < 0.03 && x - lastTreeX >= 4) { this.growTree(rng, x, above, 3 + (rng() * 3 | 0)); lastTreeX = x; }
      }
    }

    // 6. structures
    this.buildRuin(rng, 60 + rng() * 60 | 0);
    this.buildRuin(rng, 180 + rng() * 60 | 0);
    this.buildTemple(rng, 950 + rng() * 40 | 0);
    this.buildTemple(rng, 1060 + rng() * 60 | 0);

    // 7. spawn point: tropic, on solid ground
    const sx = 440 + (rng() * 30 | 0);
    this.spawn = { x: sx, y: this.surfaceAt(sx) - 3 };
  }

  paintSurface(x, tile, depth) {
    const surf = this.heights[x];
    for (let y = surf; y < surf + depth; y++) {
      if (isSolid(this.get(x, y))) this.set(x, y, tile);
    }
  }
  carve(cx, cy, r) {
    for (let y = cy - r | 0; y <= cy + r; y++)
      for (let x = cx - r | 0; x <= cx + r; x++) {
        if ((x - cx) ** 2 + (y - cy) ** 2 <= r * r && this.get(x, y) !== T.WATER) this.set(x, y, T.AIR);
      }
  }
  scatterOre(rng, ore, x0, x1, y0, y1, count) {
    for (let i = 0; i < count; i++) {
      const x = x0 + rng() * (x1 - x0) | 0, y = y0 + rng() * (y1 - y0) | 0;
      for (let j = 0; j < 4; j++) {
        const ox = x + (rng() * 3 | 0) - 1, oy = y + (rng() * 3 | 0) - 1;
        const cur = this.get(ox, oy);
        if (cur === T.STONE || cur === T.SANDSTONE) this.set(ox, oy, ore);
      }
    }
  }
  growTree(rng, x, topY, height) {
    for (let i = 0; i < height; i++) this.set(x, topY - i, T.TRUNK);
    const ly = topY - height;
    // rounded canopy, rows from crown down: widths 3,5,7,5
    const rows = [[-3, 1], [-2, 2], [-1, 3], [0, 2]];
    for (const [dy, r] of rows)
      for (let dx = -r; dx <= r; dx++) {
        if (this.get(x + dx, ly + dy) === T.AIR) this.set(x + dx, ly + dy, T.LEAVES);
      }
  }
  growCactus(rng, x, topY) {
    const height = 1 + (rng() * 3 | 0);
    for (let i = 0; i < height; i++) this.set(x, topY - i, T.CACTUS);
  }
  growKelp(rng, x, bottomY) {
    const height = 2 + (rng() * 4 | 0);
    for (let i = 0; i < height; i++) {
      if (this.get(x, bottomY - i) !== T.WATER) break;
      this.set(x, bottomY - i, T.KELP);
    }
  }
  growCoral(rng, x, surfY) {
    for (let dx = -1; dx <= 1; dx++)
      for (let dy = 0; dy <= 1; dy++) {
        if (rng() < 0.6 && this.get(x + dx, surfY - dy) === T.WATER) this.set(x + dx, surfY - dy, T.CORAL);
      }
  }
  growBamboo(rng, x, topY) {
    const height = 3 + (rng() * 5 | 0);
    for (let i = 0; i < height; i++) {
      if (this.get(x, topY - i) !== T.AIR) break;
      this.set(x, topY - i, T.BAMBOO);
    }
  }

  // half-buried desert ruin
  buildRuin(rng, cx) {
    const surf = this.heights[cx];
    const wdt = 8 + (rng() * 5 | 0);
    for (let dx = -wdt / 2 | 0; dx <= wdt / 2; dx++) {
      const x = cx + dx;
      const colH = 2 + (rng() * 4 | 0);
      for (let i = 0; i < colH; i++) {
        if (rng() < 0.75) this.set(x, surf - 1 - i + (rng() * 2 | 0), T.SANDSTONE);
      }
    }
  }

  // golden pagoda temple
  buildTemple(rng, cx) {
    // flatten a pad
    let surf = Math.min(this.heights[cx - 8], this.heights[cx + 8], this.heights[cx]);
    const half = 8;
    for (let x = cx - half; x <= cx + half; x++) {
      for (let y = surf; y < surf + 6; y++) if (!isSolid(this.get(x, y))) this.set(x, y, T.DIRT);
      for (let y = surf - 12; y < surf; y++) if (this.get(x, y) !== T.AIR) this.set(x, y, T.AIR);
      this.heights[x] = surf;
    }
    const floor = surf - 1;
    // floor slab
    for (let x = cx - half; x <= cx + half; x++) this.set(x, floor + 1, T.TEMPLE);
    // columns
    for (const px of [cx - half + 1, cx + half - 1]) {
      for (let i = 0; i < 6; i++) this.set(px, floor - i, T.TEMPLE);
    }
    // roofs, pagoda tiers
    const roofY = floor - 6;
    for (let x = cx - half - 1; x <= cx + half + 1; x++) this.set(x, roofY, T.GOLDTILE);
    for (let x = cx - half + 2; x <= cx + half - 2; x++) this.set(x, roofY - 1, T.TEMPLE);
    for (let x = cx - half + 3; x <= cx + half - 3; x++) this.set(x, roofY - 2, T.GOLDTILE);
    for (let x = cx - 2; x <= cx + 2; x++) this.set(x, roofY - 3, T.GOLDTILE);
    this.set(cx, roofY - 4, T.GOLDTILE);
    // little golden shrine statue
    this.set(cx, floor, T.GOLDTILE); this.set(cx, floor - 1, T.GOLDTILE);
    // hanging lanterns
    this.set(cx - 4, roofY + 1, T.LANTERN);
    this.set(cx + 4, roofY + 1, T.LANTERN);
    this.templeSpots.push({ x: cx, y: floor });
  }

  // ─────────── MINING / PLACING ───────────
  // returns { broken, drop } — power = pickaxe strength
  mine(x, y, power, tier) {
    const id = this.get(x, y);
    const d = TILE_DEFS[id];
    if (!d || id === T.AIR || id === T.WATER) return null;
    if ((d.tier || 0) > tier) return { blocked: true };
    const key = x + ',' + y;
    const dmg = (this.damage.get(key) || 0) + power;
    if (dmg >= (d.hp || 1)) {
      this.damage.delete(key);
      // ocean: broken tiles under sea level flood back with water
      const flood = y >= SEA_LEVEL && biomeAt(x).key === 'ocean' &&
        (this.get(x, y - 1) === T.WATER || this.get(x - 1, y) === T.WATER || this.get(x + 1, y) === T.WATER);
      this.set(x, y, flood ? T.WATER : T.AIR);
      // grass under a removed tile stays dirt-looking; convert exposed dirt→grass later (cosmetic, skip)
      return { broken: true, drop: d.drop };
    }
    this.damage.set(key, dmg);
    return { broken: false, frac: dmg / (d.hp || 1) };
  }

  canPlace(x, y) {
    const cur = this.get(x, y);
    if (cur !== T.AIR && cur !== T.WATER) return false;
    // must touch something solid or liquid-adjacent-solid
    return isSolid(this.get(x - 1, y)) || isSolid(this.get(x + 1, y)) ||
      isSolid(this.get(x, y - 1)) || isSolid(this.get(x, y + 1));
  }

  // ─────────── SAVE / LOAD ───────────
  serialize() {
    const bytes = [];
    let run = 1;
    const t = this.tiles;
    for (let i = 1; i <= t.length; i++) {
      if (i < t.length && t[i] === t[i - 1] && run < 255) run++;
      else { bytes.push(run, t[i - 1]); run = 1; }
    }
    let bin = '';
    const arr = new Uint8Array(bytes);
    for (let i = 0; i < arr.length; i += 4096) {
      bin += String.fromCharCode.apply(null, arr.subarray(i, i + 4096));
    }
    return {
      rle: btoa(bin),
      spawn: this.spawn,
      templeSpots: this.templeSpots,
      heights: Array.from(this.heights),
    };
  }
  deserialize(data) {
    const bin = atob(data.rle);
    let i = 0, pos = 0;
    while (i < bin.length && pos < this.tiles.length) {
      const run = bin.charCodeAt(i), val = bin.charCodeAt(i + 1);
      this.tiles.fill(val, pos, pos + run);
      pos += run; i += 2;
    }
    this.spawn = data.spawn;
    this.templeSpots = data.templeSpots || [];
    if (data.heights) this.heights.set(data.heights);
  }
}
