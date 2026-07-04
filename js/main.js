// ══════════════════════════════════════════════════════════════
// Mushmire — main.js
// Boot, screens, game loop, and the glue between all systems.
// ══════════════════════════════════════════════════════════════
import { buildTileTextures, buildItemIcons, ITEMS, ITEM_ICONS, T, TILE_DEFS, ENEMY_TYPES, biomeAt, mulberry32 } from './data.js';
import { World } from './world.js';
import { Player, TILE } from './player.js';
import { Enemy, Creature, Drop, Particles, buildEntitySprites, trySpawnEnemy, despawnFar } from './entities.js';
import { Input } from './input.js';
import { Renderer } from './render.js';
import { UI } from './ui.js';
import { Forge, customWeaponCanvas } from './designer.js';
import { hasSave, saveGame, loadGame } from './save.js';

const $ = id => document.getElementById(id);
const show = id => { document.querySelectorAll('.screen').forEach(s => s.classList.remove('visible')); $(id).classList.add('visible'); };

// ══════════════ GAME ══════════════
class Game {
  constructor() {
    this.world = new World();
    this.player = null;
    this.enemies = [];
    this.creatures = [];
    this.drops = [];
    this.particles = new Particles();
    this.customWeapons = [];
    this._customIconCache = {};
    this.clock = 30;                 // seconds of game time, starts mid-morning
    this.cycleLen = 480;             // 8-minute day
    this.rng = mulberry32((Math.random() * 1e9) | 0);
    this.spawnTimer = 0;
    this.holdTimer = 0;
    this.hudTimer = 0;
    this.autosaveTimer = 0;
    this.tapMarker = null;
    this.running = false;
  }

  get tod() { return (this.clock / this.cycleLen) % 1; }
  isNight() { return this.tod >= 0.58 && this.tod < 0.97; }

  customIcon(id) {
    if (!this._customIconCache[id]) {
      const w = this.customWeapons[+id.split(':')[1]];
      if (!w) return null;
      this._customIconCache[id] = customWeaponCanvas(w);
    }
    return this._customIconCache[id];
  }
  heldIcon() {
    const s = this.player.selected;
    if (!s) return null;
    return s.id.startsWith('custom:') ? this.customIcon(s.id) : ITEM_ICONS[s.id];
  }
  selectedDamage() {
    const s = this.player.selected;
    if (!s) return 1;
    if (s.id.startsWith('custom:')) return this.customWeapons[+s.id.split(':')[1]]?.dmg || 1;
    return ITEMS[s.id]?.dmg || 1;
  }

  // ── start paths ──
  newGame(look) {
    this.world.generate((Math.random() * 1e9) | 0);
    this.player = new Player(look);
    this.player.respawn(this.world);
    this.player.iframes = 0;
    this.enemies = []; this.drops = []; this.creatures = [];
    this.clock = 30;
    this.customWeapons = [];
    this._customIconCache = {};
    // temple cats
    for (const spot of this.world.templeSpots) {
      for (let i = 0; i < 2; i++) {
        const kind = this.rng() < 0.5 ? 'cat' : 'catOrange';
        this.creatures.push(new Creature(kind, (spot.x + 2 + i * 3) * TILE, (spot.y - 2) * TILE));
      }
    }
    this.begin();
    this.ui.toast('welcome to Mushmire, ' + (look.name || (look.preset === 'mei' ? 'Mei' : 'Khai')) + ' ✦');
  }

  continueGame(data) {
    this.world.deserialize(data.world);
    this.player = new Player(data.look);
    Object.assign(this.player, {
      x: data.player.x, y: data.player.y, hp: data.player.hp, sel: data.player.sel,
    });
    this.player.slots = data.player.slots;
    this.clock = data.clock || 30;
    this.customWeapons = data.customWeapons || [];
    this._customIconCache = {};
    this.creatures = (data.creatures || []).map(c => {
      const cr = new Creature(c.kind, c.x, c.y, { follows: c.follows });
      cr.homeX = c.homeX ?? c.x;
      return cr;
    });
    this.begin();
    this.ui.toast('the empire remembers you ✦');
  }

  begin() {
    show('screen-game');
    this.ui.refreshHotbar();
    this.ui.refreshHUD();
    this.running = true;
  }

  save() {
    if (this.player) saveGame(this);
  }

  // ── world interaction (taps) ──
  handleTap(cssX, cssY) {
    if (this.ui.anyModalOpen() || this.player.dead) return;
    const w = this.renderer.toWorld(cssX, cssY);
    const p = this.player, pc = p.center();
    const tx = Math.floor(w.x / TILE), ty = Math.floor(w.y / TILE);
    const dist = Math.hypot(w.x - pc.x, w.y - pc.y);
    const REACH = TILE * 4.6;

    // 1. pet a cat (always allowed if close enough)
    for (const c of this.creatures) {
      if (Math.abs(w.x - (c.x + c.w / 2)) < 14 && Math.abs(w.y - (c.y + c.h / 2)) < 14 && dist < REACH * 1.4) {
        c.pet(this.particles);
        if (c.kind.startsWith('cat')) this.ui.toast('purrrr… you feel blessed');
        return;
      }
    }
    // 2. strike an enemy near the tap
    for (const e of this.enemies) {
      if (Math.abs(w.x - (e.x + e.w / 2)) < 16 && Math.abs(w.y - (e.y + e.h / 2)) < 16 && dist < REACH) {
        this.attack(Math.sign(e.x - p.x) || p.facing);
        return;
      }
    }
    if (dist > REACH) return;

    const s = p.selected;
    const def = s && !s.id.startsWith('custom:') ? ITEMS[s.id] : null;

    // 3. use / place / mine depending on held item
    if (def && def.type === 'use') {
      if (p.hp < p.hpMax) {
        p.heal(def.heal); p.remove(s.id, 1);
        this.particles.burst(pc.x, pc.y, '#e87b8f', 8);
        this.ui.toast('you feel mended ✦');
        this.ui.refreshHotbar();
      } else this.ui.toast('already at full health');
      return;
    }
    if (def && def.type === 'charm') {
      const kind = def.creature;
      this.creatures.push(new Creature(kind, w.x, w.y - 8, { follows: true }));
      p.remove(s.id, 1);
      this.particles.burst(w.x, w.y, '#b48cf2', 12);
      this.ui.toast('a friend answers the charm ✦');
      this.ui.refreshHotbar();
      return;
    }
    if (def && def.type === 'block') {
      // don't place inside yourself
      const px0 = Math.floor(p.x / TILE), px1 = Math.floor((p.x + p.w - 0.01) / TILE);
      const py0 = Math.floor(p.y / TILE), py1 = Math.floor((p.y + p.h - 0.01) / TILE);
      const inMe = tx >= px0 && tx <= px1 && ty >= py0 && ty <= py1 && TILE_DEFS[def.tile].solid;
      if (!inMe && this.world.canPlace(tx, ty)) {
        this.world.set(tx, ty, def.tile);
        p.remove(s.id, 1);
        this.ui.refreshHotbar();
      }
      return;
    }
    if (def && def.type === 'tool') {
      this.mineAt(tx, ty, def);
      return;
    }
    // weapon / custom / empty hand → swing toward tap
    this.attack(Math.sign(w.x - pc.x) || p.facing);
  }

  mineAt(tx, ty, def) {
    const res = this.world.mine(tx, ty, def.power, def.tier);
    if (!res) return;
    this.tapMarker = { tx, ty, t: 0.25 };
    if (res.blocked) { this.ui.toast('this needs a stronger pick'); return; }
    const midX = tx * TILE + 8, midY = ty * TILE + 8;
    if (res.broken) {
      this.particles.burst(midX, midY, '#a8875c', 6);
      if (res.drop) this.drops.push(new Drop(res.drop, 1, midX, midY));
    } else {
      this.particles.spawn(midX, midY, '#8d8d98');
    }
  }

  attack(dir) {
    const p = this.player;
    if (!p.swing()) return;
    p.facing = dir || p.facing;
    const pc = p.center();
    const dmg = this.selectedDamage();
    let hitAny = false;
    for (const e of this.enemies) {
      const ex = e.x + e.w / 2, ey = e.y + e.h / 2;
      const inX = p.facing > 0 ? ex > pc.x - 4 && ex < pc.x + 30 : ex < pc.x + 4 && ex > pc.x - 30;
      if (inX && Math.abs(ey - pc.y) < 22) {
        e.hit(dmg, p.facing, this.particles);
        hitAny = true;
      }
    }
    if (hitAny && navigator.vibrate) navigator.vibrate(10);
  }

  // ── per-frame update ──
  update(dt) {
    const p = this.player, input = this.input;
    this.clock += dt;

    if (!this.ui.anyModalOpen()) {
      p.update(dt, input, this.world);
      if (input.attackPressed) this.attack(p.facing);
      if (input.worldTap) this.handleTap(input.worldTap.x, input.worldTap.y);
      // hold-to-mine
      if (input.worldHold) {
        this.holdTimer -= dt;
        if (this.holdTimer <= 0) {
          this.holdTimer = 0.16;
          const s = p.selected;
          const def = s && !s.id.startsWith('custom:') ? ITEMS[s.id] : null;
          if (def && def.type === 'tool') {
            const w = this.renderer.toWorld(input.worldHold.x, input.worldHold.y);
            const pc = p.center();
            if (Math.hypot(w.x - pc.x, w.y - pc.y) <= TILE * 4.6) {
              this.mineAt(Math.floor(w.x / TILE), Math.floor(w.y / TILE), def);
            }
          }
        }
      } else this.holdTimer = 0;
    }
    input.consume();

    if (p.spawnPoof) { p.spawnPoof = false; this.particles.burst(p.x + p.w / 2, p.y + p.h, '#f2ddb0', 8); }

    // entities
    for (const e of this.enemies) e.update(dt, this.world, p, this.particles);
    for (let i = this.enemies.length - 1; i >= 0; i--) {
      const e = this.enemies[i];
      if (e.dead) {
        const def = ENEMY_TYPES[e.type];
        const mx = e.x + e.w / 2, my = e.y + e.h / 2;
        this.particles.burst(mx, my, '#b48cf2', 10);
        if (def && def.drop) this.drops.push(new Drop(def.drop, def.dropN || 1, mx, my));
        this.enemies.splice(i, 1);
      }
    }
    for (const c of this.creatures) c.update(dt, this.world, p, this.particles);
    for (const d of this.drops) d.update(dt, this.world, p);
    this.drops = this.drops.filter(d => !d.dead);
    this.particles.update(dt);

    // night spawner / dawn cleanse
    this.spawnTimer -= dt;
    if (this.spawnTimer <= 0) {
      this.spawnTimer = 2.4;
      trySpawnEnemy(this.enemies, this.world, p, this.isNight(), this.rng);
      despawnFar(this.enemies, p);
      this.manageAmbient();
    }
    if (!this.isNight() && this.enemies.length) {
      // dawn: spirits dissolve
      for (const e of this.enemies) this.particles.burst(e.x + 5, e.y + 5, '#f2ddb0', 4);
      this.enemies.length = 0;
    }

    if (this.tapMarker) this.tapMarker.t -= dt;

    // death
    if (p.dead && !this._deathShown) {
      this._deathShown = true;
      $('modal-death').classList.add('visible');
    }

    // HUD + autosave
    this.hudTimer -= dt;
    if (this.hudTimer <= 0) { this.hudTimer = 0.3; this.ui.refreshHUD(); }
    this.autosaveTimer += dt;
    if (this.autosaveTimer > 25) { this.autosaveTimer = 0; this.save(); }
  }

  manageAmbient() {
    const p = this.player;
    const biome = biomeAt(Math.floor(p.center().x / TILE)).key;
    // fireflies at night in tropic & lantern
    const fireflies = this.creatures.filter(c => c.kind === 'firefly');
    if (this.isNight() && (biome === 'tropic' || biome === 'lantern')) {
      if (fireflies.length < 5) {
        const c = new Creature('firefly', p.x + (this.rng() - 0.5) * 300, p.y - 40 + this.rng() * 60);
        c.homeX = c.x;
        this.creatures.push(c);
      }
    } else if (fireflies.length) {
      this.creatures = this.creatures.filter(c => c.kind !== 'firefly');
    }
    // fish in the ocean
    const fish = this.creatures.filter(c => c.kind === 'fishy');
    if (biome === 'ocean') {
      if (fish.length < 4) {
        const fx = p.x + (this.rng() - 0.5) * 400;
        const tx = Math.floor(fx / TILE);
        const ty = Math.floor((this.world.surfaceAt(tx) + 104) / 2) - 2; // between sea level & floor
        if (this.world.liquidAt(tx, ty)) {
          const c = new Creature('fishy', fx, ty * TILE);
          c.homeX = fx;
          this.creatures.push(c);
        }
      }
    } else if (fish.length) {
      this.creatures = this.creatures.filter(c => c.kind !== 'fishy');
    }
    // cull faraway ambient
    this.creatures = this.creatures.filter(c =>
      c.follows || c.kind.startsWith('cat') || Math.abs(c.x - p.x) < 600);
  }
}

// ══════════════ CHARACTER PREVIEW ══════════════
const PRESETS = {
  mei: { preset: 'mei', hair: '#1c1216', skin: '#e8b98a', top: '#7a4fa3', legs: '#3d3550' },
  khai: { preset: 'khai', hair: '#14100c', skin: '#d9a06b', top: '#2e6b44', legs: '#5e4025' },
};

function drawCharPreview(canvas, look) {
  const g = canvas.getContext('2d');
  g.clearRect(0, 0, canvas.width, canvas.height);
  g.imageSmoothingEnabled = false;
  const S = 5; // pixel scale
  g.save();
  g.translate(canvas.width / 2, canvas.height / 2 + 4);
  g.scale(S, S);
  const R = (x, y, w, h, c) => { g.fillStyle = c; g.fillRect(x, y, w, h); };
  // 21-tall standing pose, mirrors drawPlayer
  const top = -11;
  R(-4, top + 13, 3, 8, look.legs); R(1, top + 13, 3, 8, look.legs);
  R(-4, top + 7, 9, 6, look.top);
  R(-4.5, top + 8, 2, 5, look.top);
  R(2.5, top + 8, 2, 5, look.skin);
  R(-3.5, top, 8, 7, look.skin);
  R(-3.5, top - 1, 8, 3, look.hair);
  R(-4.5, top, 2, 4, look.hair);
  if (look.preset === 'mei') { R(-5, top + 1, 2, 13, look.hair); R(3.5, top + 1, 1, 3, look.hair); }
  else R(-4, top - 3, 3, 3, look.hair);
  R(1.5, top + 3, 1.4, 1.6, '#241a1a'); R(-1.2, top + 3, 1.4, 1.6, '#241a1a');
  g.globalAlpha = 0.5; R(2.6, top + 4.6, 1.4, 1, '#e87b8f'); g.globalAlpha = 1;
  g.restore();
}

// ══════════════ TITLE FX (drifting spores) ══════════════
function startTitleFx() {
  const c = $('title-fx');
  const g = c.getContext('2d');
  const spores = [];
  function loop() {
    if (!$('screen-title').classList.contains('visible')) return;
    c.width = window.innerWidth; c.height = window.innerHeight;
    if (!spores.length) {
      for (let i = 0; i < 36; i++) spores.push({
        x: Math.random() * c.width, y: Math.random() * c.height,
        r: 1 + Math.random() * 2.5, s: 8 + Math.random() * 18,
        hue: Math.random() < 0.55 ? '#e2c290' : '#b48cf2',
        ph: Math.random() * 7,
      });
    }
    const t = performance.now() / 1000;
    for (const s of spores) {
      s.y -= s.s * 0.016;
      if (s.y < -5) { s.y = c.height + 5; s.x = Math.random() * c.width; }
      const x = s.x + Math.sin(t + s.ph) * 14;
      g.globalAlpha = 0.35 + 0.3 * Math.sin(t * 2 + s.ph);
      g.fillStyle = s.hue;
      g.beginPath(); g.arc(x, s.y, s.r, 0, 7); g.fill();
    }
    g.globalAlpha = 1;
    requestAnimationFrame(loop);
  }
  loop();
}

// ══════════════ BOOT ══════════════
function boot() {
  buildTileTextures();
  buildItemIcons();
  buildEntitySprites();

  const game = new Game();
  const canvas = $('game');
  game.renderer = new Renderer(canvas);
  game.input = new Input(canvas);
  game.ui = new UI(game);
  game.forge = new Forge(game);
  window.game = game; // for curious tinkerers

  // prevent iOS pinch/double-tap zoom
  document.addEventListener('gesturestart', e => e.preventDefault());
  document.addEventListener('dblclick', e => e.preventDefault());

  // ── title wiring ──
  if (hasSave()) $('btn-continue').classList.remove('hidden');
  startTitleFx();

  $('btn-manual').addEventListener('click', () => { $('screen-manual').classList.add('visible'); });
  $('btn-manual-close').addEventListener('click', () => { $('screen-manual').classList.remove('visible'); });

  $('btn-new').addEventListener('click', () => {
    show('screen-character');
    refreshPreviews();
  });
  $('btn-continue').addEventListener('click', () => {
    const data = loadGame();
    if (data) game.continueGame(data);
    else { $('btn-continue').classList.add('hidden'); game.ui.toast('no save found'); }
  });

  // ── character select wiring ──
  let selPreset = 'mei';
  const cards = document.querySelectorAll('.char-card');
  function applyPresetToInputs() {
    const pr = PRESETS[selPreset];
    $('cc-hair').value = pr.hair; $('cc-skin').value = pr.skin;
    $('cc-top').value = pr.top; $('cc-legs').value = pr.legs;
  }
  function currentLook() {
    return {
      preset: selPreset,
      hair: $('cc-hair').value, skin: $('cc-skin').value,
      top: $('cc-top').value, legs: $('cc-legs').value,
      name: $('cc-name').value.trim(),
    };
  }
  function refreshPreviews() {
    cards.forEach(card => {
      const key = card.dataset.char;
      const look = key === selPreset ? currentLook() : PRESETS[key];
      drawCharPreview(card.querySelector('.char-preview'), { ...look, preset: key });
    });
  }
  cards.forEach(card => card.addEventListener('click', () => {
    selPreset = card.dataset.char;
    cards.forEach(c => c.classList.toggle('selected', c === card));
    applyPresetToInputs();
    refreshPreviews();
  }));
  ['cc-hair', 'cc-skin', 'cc-top', 'cc-legs'].forEach(id =>
    $(id).addEventListener('input', refreshPreviews));
  applyPresetToInputs();

  $('btn-begin').addEventListener('click', () => game.newGame(currentLook()));

  // ── death / respawn ──
  $('btn-respawn').addEventListener('click', () => {
    $('modal-death').classList.remove('visible');
    game._deathShown = false;
    game.player.respawn(game.world);
    game.enemies.length = 0;
  });

  // ── save on leave ──
  window.addEventListener('pagehide', () => game.running && game.save());
  document.addEventListener('visibilitychange', () => {
    if (document.hidden && game.running) game.save();
  });

  // ── main loop ──
  let last = performance.now();
  function frame(now) {
    requestAnimationFrame(frame);
    const dt = Math.min(0.033, (now - last) / 1000);
    last = now;
    if (!game.running || !game.player) return;
    game.update(dt);
    game.renderer.draw(game);
  }
  requestAnimationFrame(frame);
}

boot();
