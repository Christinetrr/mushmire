// ══════════════════════════════════════════════════════════════
// Mushmire — render.js
// Sky, parallax, tiles, entities, player, lighting.
// ══════════════════════════════════════════════════════════════
import { T, TILE_DEFS, TILE_TEX, ITEM_ICONS, biomeAt } from './data.js';
import { ENEMY_SPRITES, CREATURE_SPRITES } from './entities.js';
import { TILE } from './player.js';
import { SEA_LEVEL } from './world.js';

const lerp = (a, b, t) => a + (b - a) * t;
function lerpColor(c1, c2, t) {
  const p = c => [parseInt(c.slice(1, 3), 16), parseInt(c.slice(3, 5), 16), parseInt(c.slice(5, 7), 16)];
  const [r1, g1, b1] = p(c1), [r2, g2, b2] = p(c2);
  return `rgb(${lerp(r1, r2, t) | 0},${lerp(g1, g2, t) | 0},${lerp(b1, b2, t) | 0})`;
}

// time-of-day keyframes: [frac, topColor, bottomColor, light]
const SKY_KEYS = [
  [0.00, '#4a3a68', '#e2c290', 0.55],   // dawn
  [0.06, '#6ec3e8', '#cfe8d9', 1.0],    // morning
  [0.45, '#6ec3e8', '#d8e8c9', 1.0],    // afternoon
  [0.55, '#7a4fa3', '#e8933a', 0.75],   // sunset
  [0.63, '#16101f', '#241a36', 0.16],   // night
  [0.92, '#16101f', '#2d2244', 0.16],   // late night
  [0.97, '#4a3a68', '#a8875c', 0.4],    // pre-dawn
  [1.00, '#4a3a68', '#e2c290', 0.55],
];
export function skyAt(tod) {
  for (let i = 0; i < SKY_KEYS.length - 1; i++) {
    const a = SKY_KEYS[i], b = SKY_KEYS[i + 1];
    if (tod >= a[0] && tod <= b[0]) {
      const t = (tod - a[0]) / (b[0] - a[0] || 1);
      return { top: lerpColor(a[1], b[1], t), bottom: lerpColor(a[2], b[2], t), light: lerp(a[3], b[3], t) };
    }
  }
  return { top: '#000', bottom: '#000', light: 1 };
}

export class Renderer {
  constructor(canvas) {
    this.canvas = canvas;
    this.ctx = canvas.getContext('2d');
    this.cam = { x: 0, y: 0 };
    this.zoom = 2.2;
    this.lightCanvas = document.createElement('canvas');
    this.resize();
    window.addEventListener('resize', () => this.resize());
    window.addEventListener('orientationchange', () => setTimeout(() => this.resize(), 250));
  }

  resize() {
    const dpr = Math.min(window.devicePixelRatio || 1, 2);
    this.cssW = window.innerWidth; this.cssH = window.innerHeight;
    this.canvas.width = this.cssW * dpr;
    this.canvas.height = this.cssH * dpr;
    this.dpr = dpr;
    // aim for ~26 tiles visible across
    const tilePx = Math.max(22, Math.min(46, this.cssW / 26));
    this.zoom = tilePx / TILE;
    this.lightCanvas.width = Math.ceil(this.cssW / 4);
    this.lightCanvas.height = Math.ceil(this.cssH / 4);
    this.ctx.imageSmoothingEnabled = false;
  }

  // css px → world px
  toWorld(cssX, cssY) {
    return { x: this.cam.x + cssX / this.zoom, y: this.cam.y + cssY / this.zoom };
  }

  draw(game) {
    const { world, player, tod } = game;
    const g = this.ctx;
    const dpr = this.dpr, zoom = this.zoom;
    const vw = this.cssW / zoom, vh = this.cssH / zoom;   // viewport in world px

    // camera follows player
    const pc = player.center();
    this.cam.x = Math.max(0, Math.min(world.w * TILE - vw, pc.x - vw / 2));
    this.cam.y = Math.max(0, Math.min(world.h * TILE - vh, pc.y - vh * 0.52));

    g.setTransform(dpr, 0, 0, dpr, 0, 0);
    const sky = skyAt(tod);

    // ── sky ──
    const grad = g.createLinearGradient(0, 0, 0, this.cssH);
    grad.addColorStop(0, sky.top); grad.addColorStop(1, sky.bottom);
    g.fillStyle = grad;
    g.fillRect(0, 0, this.cssW, this.cssH);

    // sun / moon arc
    const isDay = tod < 0.58 || tod > 0.99;
    const arcT = isDay ? tod / 0.58 : (tod - 0.6) / 0.38;
    const ax = arcT * this.cssW;
    const ay = this.cssH * 0.55 - Math.sin(arcT * Math.PI) * this.cssH * 0.42;
    if (isDay) {
      g.fillStyle = '#f7e8b0';
      g.shadowColor = '#f2c14e'; g.shadowBlur = 30;
      g.beginPath(); g.arc(ax, ay, 18, 0, 7); g.fill();
      g.shadowBlur = 0;
    } else {
      g.fillStyle = '#e8e4f2';
      g.shadowColor = '#b48cf2'; g.shadowBlur = 24;
      g.beginPath(); g.arc(ax, ay, 14, 0, 7); g.fill();
      g.shadowBlur = 0;
      g.fillStyle = sky.top;
      g.beginPath(); g.arc(ax + 6, ay - 3, 12, 0, 7); g.fill();
      // stars
      g.fillStyle = 'rgba(245,240,232,0.8)';
      for (let i = 0; i < 40; i++) {
        const sx = ((i * 173.3 + 40) % this.cssW);
        const sy = ((i * 97.7 + 20) % (this.cssH * 0.6));
        const tw = 0.4 + 0.6 * Math.abs(Math.sin(game.clock * 0.8 + i));
        g.globalAlpha = tw * 0.8;
        g.fillRect(sx, sy, 2, 2);
      }
      g.globalAlpha = 1;
    }

    // ── parallax silhouettes ──
    const biome = biomeAt(Math.floor(pc.x / TILE));
    this.drawParallax(g, sky, biome.key, 0.25, 0.62, 'rgba(0,0,0,0.14)');
    this.drawParallax(g, sky, biome.key, 0.5, 0.72, 'rgba(0,0,0,0.20)');

    // ── world tiles ──
    g.setTransform(dpr * zoom, 0, 0, dpr * zoom, -this.cam.x * dpr * zoom, -this.cam.y * dpr * zoom);
    const x0 = Math.max(0, Math.floor(this.cam.x / TILE) - 1);
    const x1 = Math.min(world.w - 1, Math.ceil((this.cam.x + vw) / TILE) + 1);
    const y0 = Math.max(0, Math.floor(this.cam.y / TILE) - 1);
    const y1 = Math.min(world.h - 1, Math.ceil((this.cam.y + vh) / TILE) + 1);

    // water pass: merge each row into spans so translucency has no seams
    for (let ty = y0; ty <= y1; ty++) {
      const depth = Math.min(1, Math.max(0, (ty - SEA_LEVEL) / 40));
      g.fillStyle = `rgba(${46 - depth * 20 | 0},${100 - depth * 40 | 0},${170 - depth * 50 | 0},${0.66 + depth * 0.2})`;
      let run = -1;
      for (let tx = x0; tx <= x1 + 1; tx++) {
        const isW = tx <= x1 && world.tiles[ty * world.w + tx] === T.WATER;
        if (isW && run < 0) run = tx;
        else if (!isW && run >= 0) {
          g.fillRect(run * TILE, ty * TILE, (tx - run) * TILE, TILE + 0.7);
          run = -1;
        }
      }
      // surface shimmer
      g.fillStyle = 'rgba(190,225,255,0.65)';
      for (let tx = x0; tx <= x1; tx++) {
        if (world.tiles[ty * world.w + tx] === T.WATER && world.get(tx, ty - 1) !== T.WATER) {
          g.fillRect(tx * TILE, ty * TILE + Math.sin(game.clock * 2 + tx * 0.8) * 1.2, TILE, 2);
        }
      }
    }

    this.lights = [];
    for (let ty = y0; ty <= y1; ty++) {
      for (let tx = x0; tx <= x1; tx++) {
        const id = world.tiles[ty * world.w + tx];
        if (id === T.AIR || id === T.WATER) continue;
        const tex = TILE_TEX[id];
        if (tex) g.drawImage(tex, tx * TILE, ty * TILE, TILE, TILE);
        const light = TILE_DEFS[id]?.light;
        if (light) this.lights.push({ x: tx * TILE + 8, y: ty * TILE + 8, r: light * 90 });
      }
    }
    // mining cracks
    g.fillStyle = 'rgba(0,0,0,0.45)';
    for (const [key, dmg] of world.damage) {
      const [tx, ty] = key.split(',').map(Number);
      if (tx < x0 || tx > x1 || ty < y0 || ty > y1) continue;
      const d = TILE_DEFS[world.get(tx, ty)];
      if (!d || !d.hp) continue;
      const frac = dmg / d.hp;
      for (let i = 0; i < frac * 6; i++) {
        const sx = tx * TILE + ((i * 5.3) % 12) + 2, sy = ty * TILE + ((i * 7.7) % 12) + 2;
        g.fillRect(sx, sy, 2, 1); g.fillRect(sx + 1, sy + 1, 1, 2);
      }
    }

    // ── drops ──
    for (const d of game.drops) {
      const icon = d.id.startsWith('custom:') ? game.customIcon(d.id) : ITEM_ICONS[d.id];
      if (icon) g.drawImage(icon, d.x - 5, d.y - 5 + Math.sin(game.clock * 3 + d.x) * 1.5, 10, 10);
    }

    // ── creatures ──
    for (const c of game.creatures) {
      const spr = CREATURE_SPRITES[c.kind];
      if (!spr) continue;
      g.save();
      const bob = c.floaty ? Math.sin(c.bobT * 2) * 2 : 0;
      g.translate(c.x + c.w / 2, c.y + c.h / 2 + bob);
      if (c.facing < 0) g.scale(-1, 1);
      const sh = c.state === 'sit' ? spr.height * 0.8 : spr.height;
      g.drawImage(spr, -spr.width / 2, -sh + c.h / 2, spr.width, sh);
      g.restore();
      if (c.kind === 'firefly' || c.kind === 'spriteWisp') {
        this.lights.push({ x: c.x, y: c.y, r: 26 });
      }
    }

    // ── enemies ──
    for (const e of game.enemies) {
      const spr = ENEMY_SPRITES[e.type];
      if (!spr) continue;
      g.save();
      const bob = e.fly ? Math.sin(e.bobT * 3) * 2.5 : 0;
      g.translate(e.x + e.w / 2, e.y + e.h / 2 + bob);
      if (e.facing < 0) g.scale(-1, 1);
      if (e.hurtT > 0.3) g.globalAlpha = 0.6;
      if (e.fly) g.globalAlpha *= 0.88;
      g.drawImage(spr, -spr.width / 2, -spr.height / 2, spr.width, spr.height);
      g.restore();
      g.globalAlpha = 1;
      if (e.type === 'paperLantern') this.lights.push({ x: e.x + 5, y: e.y + 5, r: 46 });
    }

    // ── player ──
    if (!player.dead) this.drawPlayer(g, game);

    // ── particles ──
    for (const p of game.particles.list) {
      g.globalAlpha = Math.min(1, p.life * 2);
      g.fillStyle = p.color;
      g.fillRect(p.x - 1, p.y - 1, 2, 2);
    }
    g.globalAlpha = 1;

    // ── tap target marker ──
    if (game.tapMarker && game.tapMarker.t > 0) {
      g.strokeStyle = 'rgba(242,221,176,' + Math.min(1, game.tapMarker.t * 3) + ')';
      g.lineWidth = 1;
      g.strokeRect(game.tapMarker.tx * TILE + 1, game.tapMarker.ty * TILE + 1, TILE - 2, TILE - 2);
    }

    // ── lighting / darkness ──
    this.drawDarkness(game, sky, x0, x1, y0, y1);

    // ── underwater tint + breath ──
    g.setTransform(dpr, 0, 0, dpr, 0, 0);
    if (player.inWater) {
      g.fillStyle = 'rgba(24,64,128,0.22)';
      g.fillRect(0, 0, this.cssW, this.cssH);
    }
    if (player.breath < 1) {
      const px = (pc.x - this.cam.x) * zoom, py = (pc.y - this.cam.y) * zoom;
      const n = Math.ceil(player.breath * 8);
      for (let i = 0; i < n; i++) {
        g.fillStyle = 'rgba(180,220,255,0.9)';
        g.beginPath(); g.arc(px - 28 + i * 8, py - 34, 2.6, 0, 7); g.fill();
      }
    }
  }

  drawParallax(g, sky, biomeKey, depth, horizon, shade) {
    const colors = {
      desert: '#c9a468', tropic: '#3d6b46', ocean: '#4a7a9c', lantern: '#6b5a7a',
    };
    g.fillStyle = lerpColor(colors[biomeKey] || '#555', sky.top.startsWith('rgb') ? '#333333' : sky.top, 0.45);
    const yBase = this.cssH * horizon;
    const off = this.cam.x * depth;
    g.beginPath();
    g.moveTo(0, this.cssH);
    for (let sx = 0; sx <= this.cssW; sx += 16) {
      const wx = sx + off;
      const y = yBase - Math.abs(Math.sin(wx * 0.006) * 60 + Math.sin(wx * 0.017) * 24) * (0.5 + depth);
      g.lineTo(sx, y);
    }
    g.lineTo(this.cssW, this.cssH);
    g.closePath();
    g.fill();
    g.fillStyle = shade;
    g.fill();
  }

  drawPlayer(g, game) {
    const p = game.player;
    const L = p.look;
    const t = game.clock;
    g.save();
    const cx = p.x + p.w / 2, cy = p.y + p.h / 2;
    g.translate(cx, cy);
    if (p.facing < 0) g.scale(-1, 1);
    if (p.rollT > 0) {
      g.rotate((1 - p.rollT / 0.34) * Math.PI * 2);
    }
    if (p.iframes > 0 && Math.floor(t * 12) % 2 === 0) g.globalAlpha = 0.45;

    const duck = p.ducking;
    const H = p.h;
    const top = -H / 2;
    const walking = Math.abs(p.vx) > 12 && p.grounded;
    const step = walking ? Math.sin(t * (p.sprinting ? 16 : 11)) : 0;

    const R = (x, y, w, h, c) => { g.fillStyle = c; g.fillRect(x, y, w, h); };

    // legs
    const legH = duck ? 3 : 8;
    const legY = top + H - legH;
    R(-4 + step * 1.5, legY, 3, legH, L.legs);
    R(1 - step * 1.5, legY, 3, legH, L.legs);
    // torso
    const torsoH = duck ? 4 : 7;
    const torsoY = legY - torsoH;
    R(-4, torsoY, 9, torsoH, L.top);
    // back arm
    R(-4.5 + (walking ? -step : 0), torsoY + 1, 2, 5, L.top);
    // head
    const headY = torsoY - 7;
    R(-3.5, headY, 8, 7, L.skin);
    // hair
    R(-3.5, headY - 1, 8, 3, L.hair);              // cap
    R(-4.5, headY, 2, 4, L.hair);                  // back of head (behind when facing right… flipped ok)
    if (L.preset === 'mei') {
      R(-5, headY + 1, 2, duck ? 8 : 13, L.hair);  // long hair down the back
      R(3.5, headY + 1, 1, 3, L.hair);             // front strand
    } else {
      R(-4, headY - 3, 3, 3, L.hair);              // little top-knot bun
    }
    // face: eyes toward facing dir
    R(1.5, headY + 3, 1.4, 1.6, '#241a1a');
    R(-1.2, headY + 3, 1.4, 1.6, '#241a1a');
    // blush
    g.globalAlpha *= 0.5; R(2.6, headY + 4.6, 1.4, 1, '#e87b8f'); g.globalAlpha = p.iframes > 0 && Math.floor(t * 12) % 2 === 0 ? 0.45 : 1;

    // front arm + held item
    const swingFrac = p.swingT > 0 ? 1 - p.swingT / p.swingDur : -1;
    if (swingFrac >= 0) {
      const ang = -1.9 + swingFrac * 2.6;
      g.save();
      g.translate(2, torsoY + 2);
      g.rotate(ang);
      R(-1, 0, 2, 5, L.skin);
      const icon = game.heldIcon();
      if (icon) g.drawImage(icon, -2, -14, 14, 14);
      g.restore();
    } else {
      R(2.5 + (walking ? step : 0), torsoY + 1, 2, 5, L.skin);
      const icon = game.heldIcon();
      if (icon) g.drawImage(icon, 3, torsoY - 2, 10, 10);
    }
    g.restore();
    g.globalAlpha = 1;
  }

  drawDarkness(game, sky, x0, x1, y0, y1) {
    const dark = 1 - sky.light;
    const lc = this.lightCanvas, lg = lc.getContext('2d');
    const zoom = this.zoom / 4; // light canvas is 1/4 res

    // underground is always dark-ish regardless of time
    lg.setTransform(1, 0, 0, 1, 0, 0);
    lg.globalCompositeOperation = 'source-over';
    lg.clearRect(0, 0, lc.width, lc.height);

    // sky darkness
    lg.fillStyle = `rgba(8,5,16,${dark * 0.82})`;
    lg.fillRect(0, 0, lc.width, lc.height);
    // cave darkness gradient: deeper = darker even at day
    const world = game.world;
    const surfWy = (game.world.heights[Math.floor(game.player.center().x / TILE)] || 100) * TILE;
    const caveTopCss = (surfWy + TILE * 6 - this.cam.y) * this.zoom / 4;
    const gradC = lg.createLinearGradient(0, caveTopCss, 0, caveTopCss + 260);
    gradC.addColorStop(0, 'rgba(8,5,16,0)');
    gradC.addColorStop(1, 'rgba(8,5,16,0.93)');
    lg.fillStyle = gradC;
    lg.fillRect(0, Math.max(0, caveTopCss), lc.width, lc.height);

    // punch lights
    lg.globalCompositeOperation = 'destination-out';
    const punch = (wx, wy, r, strength = 1) => {
      const sx = (wx - this.cam.x) * zoom, sy = (wy - this.cam.y) * zoom;
      const sr = r * zoom;
      const grad = lg.createRadialGradient(sx, sy, 0, sx, sy, sr);
      grad.addColorStop(0, `rgba(0,0,0,${strength})`);
      grad.addColorStop(0.6, `rgba(0,0,0,${strength * 0.55})`);
      grad.addColorStop(1, 'rgba(0,0,0,0)');
      lg.fillStyle = grad;
      lg.fillRect(sx - sr, sy - sr, sr * 2, sr * 2);
    };
    const pc = game.player.center();
    punch(pc.x, pc.y, 70, 0.95);                       // player aura
    for (const l of this.lights) punch(l.x, l.y, l.r, 1);

    const g = this.ctx;
    g.setTransform(this.dpr, 0, 0, this.dpr, 0, 0);
    g.imageSmoothingEnabled = true;
    g.drawImage(lc, 0, 0, this.cssW, this.cssH);
    g.imageSmoothingEnabled = false;

    // warm torch glow tint pass (subtle)
    if (dark > 0.3) {
      g.globalCompositeOperation = 'overlay';
      g.fillStyle = 'rgba(90,60,140,0.12)';
      g.fillRect(0, 0, this.cssW, this.cssH);
      g.globalCompositeOperation = 'source-over';
    }
  }
}
