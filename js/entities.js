// ══════════════════════════════════════════════════════════════
// Mushmire — entities.js
// Enemies, friendly creatures, item drops, particles.
// ══════════════════════════════════════════════════════════════
import { ENEMY_TYPES, ENEMY_ART, CREATURE_ART, SPRITE_COLORS, px, biomeAt } from './data.js';
import { TILE } from './player.js';

// prebuilt sprites
export const ENEMY_SPRITES = {};
export const CREATURE_SPRITES = {};
export function buildEntitySprites() {
  for (const k in ENEMY_ART) ENEMY_SPRITES[k] = px(ENEMY_ART[k], SPRITE_COLORS);
  for (const k in CREATURE_ART) CREATURE_SPRITES[k] = px(CREATURE_ART[k], SPRITE_COLORS);
}

function collidesWorld(world, x, y, w, h) {
  const x0 = Math.floor(x / TILE), x1 = Math.floor((x + w - 0.01) / TILE);
  const y0 = Math.floor(y / TILE), y1 = Math.floor((y + h - 0.01) / TILE);
  for (let ty = y0; ty <= y1; ty++)
    for (let tx = x0; tx <= x1; tx++)
      if (world.solidAt(tx, ty)) return true;
  return false;
}

function moveGround(e, world, dt) {
  e.vy = Math.min(e.vy + 640 * dt, 380);
  // x
  const dx = e.vx * dt;
  if (!collidesWorld(world, e.x + dx, e.y, e.w, e.h)) { e.x += dx; e.blockedX = false; }
  else {
    // step-up one tile
    if (!collidesWorld(world, e.x + dx, e.y - TILE, e.w, e.h)) { e.x += dx; e.y -= TILE; e.blockedX = false; }
    else e.blockedX = true;
  }
  // y
  const dy = e.vy * dt;
  if (!collidesWorld(world, e.x, e.y + dy, e.w, e.h)) { e.y += dy; e.grounded = false; }
  else {
    if (e.vy > 0) e.grounded = true;
    e.vy = 0;
  }
}

// ══════════════ ENEMY ══════════════
export class Enemy {
  constructor(type, x, y) {
    this.type = type;
    const d = ENEMY_TYPES[type];
    this.hp = d.hp; this.dmg = d.dmg; this.speed = d.speed; this.fly = d.fly;
    this.x = x; this.y = y;
    this.w = 11; this.h = 9;
    if (!d.fly) { this.h = 9; }
    this.vx = 0; this.vy = 0;
    this.facing = 1;
    this.hurtT = 0;
    this.bobT = Math.random() * 6;
    this.dead = false;
    this.grounded = false;
    this.blockedX = false;
    this.jumpCd = 0;
  }

  update(dt, world, player, particles) {
    this.bobT += dt;
    if (this.hurtT > 0) this.hurtT -= dt;
    const pc = player.center();
    const mx = this.x + this.w / 2, my = this.y + this.h / 2;
    const dx = pc.x - mx, dy = pc.y - my;
    const dist = Math.hypot(dx, dy);
    this.facing = dx >= 0 ? 1 : -1;

    if (this.fly) {
      // ghosts drift through everything, weaving as they come
      const sway = Math.sin(this.bobT * 2.4) * 14;
      if (this.hurtT <= 0.4 || this.hurtT <= 0) {
        this.vx += (Math.sign(dx) * this.speed - this.vx) * Math.min(1, dt * 2);
        this.vy += ((Math.sign(dy) * this.speed * 0.6 + sway) - this.vy) * Math.min(1, dt * 2);
      }
      this.x += this.vx * dt; this.y += this.vy * dt;
    } else {
      if (this.hurtT <= 0) this.vx = Math.sign(dx) * this.speed;
      this.jumpCd -= dt;
      moveGround(this, world, dt);
      if (this.blockedX && this.grounded && this.jumpCd <= 0) {
        this.vy = -230; this.jumpCd = 0.8;
      }
      // stalker pounce
      if (this.type === 'stalker' && this.grounded && dist < 90 && Math.abs(dy) < 40 && this.jumpCd <= 0) {
        this.vy = -180; this.vx = Math.sign(dx) * this.speed * 2.2; this.jumpCd = 1.6;
      }
    }

    // touch damage
    if (dist < 14 && player.iframes <= 0 && !player.dead) {
      if (player.damage(this.dmg, Math.sign(dx) * -1)) {
        for (let i = 0; i < 6; i++) particles.spawn(pc.x, pc.y, '#e05656');
      }
    }
  }

  hit(dmg, dir, particles) {
    this.hp -= dmg;
    this.hurtT = 0.5;
    this.vx = dir * 160;
    this.vy = -120;
    const mx = this.x + this.w / 2, my = this.y + this.h / 2;
    for (let i = 0; i < 5; i++) particles.spawn(mx, my, '#b48cf2');
    if (this.hp <= 0) this.dead = true;
  }
}

// ══════════════ FRIENDLY CREATURES ══════════════
// kind: 'cat' | 'catOrange' | 'scarabPet' | 'spriteWisp' | 'firefly' | 'fishy'
export class Creature {
  constructor(kind, x, y, opts = {}) {
    this.kind = kind;
    this.x = x; this.y = y;
    this.vx = 0; this.vy = 0;
    this.w = kind.startsWith('cat') ? 13 : 8;
    this.h = kind.startsWith('cat') ? 8 : 6;
    this.facing = 1;
    this.follows = !!opts.follows;       // charm pets follow the player
    this.homeX = x;                       // wild cats stay near home
    this.stateT = 0;
    this.state = 'idle';
    this.bobT = Math.random() * 7;
    this.petT = 0;                        // >0 shows hearts
    this.grounded = false;
    this.blockedX = false;
    this.floaty = kind === 'spriteWisp' || kind === 'firefly' || kind === 'fishy';
  }

  update(dt, world, player, particles) {
    this.bobT += dt;
    if (this.petT > 0) {
      this.petT -= dt;
      if (Math.random() < dt * 6) particles.spawn(this.x + this.w / 2, this.y - 3, '#e87b8f', -30);
    }

    if (this.floaty) {
      // orbit near player (pets) or wander (wild)
      const anchor = this.follows ? player.center() : { x: this.homeX, y: this.y };
      const tx = anchor.x + Math.sin(this.bobT * 0.9) * 26;
      const ty = anchor.y - 14 + Math.cos(this.bobT * 1.3) * 10;
      this.x += (tx - this.x) * Math.min(1, dt * 2.2);
      this.y += (ty - this.y) * Math.min(1, dt * 2.2);
      this.facing = tx > this.x ? 1 : -1;
      return;
    }

    // ground creatures (cats, scarab)
    this.stateT -= dt;
    const pc = player.center();
    const mx = this.x + this.w / 2;

    if (this.follows) {
      const dx = pc.x - mx;
      if (Math.abs(dx) > 500) { this.x = pc.x; this.y = pc.y - 20; this.vx = 0; }
      else if (Math.abs(dx) > 34) { this.vx = Math.sign(dx) * 55; this.facing = Math.sign(dx); this.state = 'walk'; }
      else { this.vx = 0; this.state = 'idle'; }
    } else {
      if (this.stateT <= 0) {
        this.stateT = 1.5 + Math.random() * 3;
        const roll = Math.random();
        if (roll < 0.45) { this.state = 'sit'; this.vx = 0; }
        else if (roll < 0.7) { this.state = 'idle'; this.vx = 0; }
        else {
          this.state = 'walk';
          const dir = mx > this.homeX + 60 ? -1 : mx < this.homeX - 60 ? 1 : (Math.random() < 0.5 ? -1 : 1);
          this.vx = dir * 28; this.facing = dir;
        }
      }
    }
    moveGround(this, world, dt);
    if (this.blockedX && this.grounded) this.vy = -170;
  }

  pet(particles) {
    this.petT = 1.6;
    for (let i = 0; i < 4; i++) particles.spawn(this.x + this.w / 2, this.y - 4, '#e87b8f', -40);
  }
}

// ══════════════ DROPS ══════════════
export class Drop {
  constructor(id, n, x, y) {
    this.id = id; this.n = n;
    this.x = x; this.y = y;
    this.vx = (Math.random() - 0.5) * 60;
    this.vy = -90 - Math.random() * 40;
    this.t = 0;
    this.dead = false;
  }
  update(dt, world, player) {
    this.t += dt;
    const pc = player.center();
    const dx = pc.x - this.x, dy = pc.y - this.y;
    const dist = Math.hypot(dx, dy);
    if (dist < 34 && this.t > 0.4) {
      // magnet
      this.vx = dx / dist * 190; this.vy = dy / dist * 190;
      this.x += this.vx * dt; this.y += this.vy * dt;
    } else {
      this.vy = Math.min(this.vy + 500 * dt, 300);
      if (!collidesWorld(world, this.x - 3, this.y - 3 + this.vy * dt, 6, 6)) this.y += this.vy * dt;
      else this.vy = 0;
      if (!collidesWorld(world, this.x - 3 + this.vx * dt, this.y - 3, 6, 6)) this.x += this.vx * dt;
      this.vx *= 0.92;
    }
    if (dist < 12 && this.t > 0.4) {
      if (player.add(this.id, this.n)) this.dead = true;
    }
    if (this.t > 90) this.dead = true;
  }
}

// ══════════════ PARTICLES ══════════════
export class Particles {
  constructor() { this.list = []; }
  spawn(x, y, color, vyBias = 0) {
    if (this.list.length > 220) return;
    this.list.push({
      x, y, color,
      vx: (Math.random() - 0.5) * 90,
      vy: (Math.random() - 0.7) * 90 + vyBias,
      life: 0.5 + Math.random() * 0.5,
    });
  }
  burst(x, y, color, n) { for (let i = 0; i < n; i++) this.spawn(x, y, color); }
  update(dt) {
    for (let i = this.list.length - 1; i >= 0; i--) {
      const p = this.list[i];
      p.x += p.vx * dt; p.y += p.vy * dt;
      p.vy += 120 * dt;
      p.life -= dt;
      if (p.life <= 0) this.list.splice(i, 1);
    }
  }
}

// ══════════════ NIGHT SPAWNER ══════════════
export function trySpawnEnemy(enemies, world, player, isNight, rng) {
  if (!isNight || player.dead) return;
  const near = enemies.filter(e => Math.abs(e.x - player.x) < 60 * TILE).length;
  if (near >= 5) return;
  const side = rng() < 0.5 ? -1 : 1;
  const tx = Math.floor(player.x / TILE) + side * (24 + rng() * 14 | 0);
  if (tx < 2 || tx >= world.w - 2) return;
  const biome = biomeAt(tx).key;
  const pool = Object.keys(ENEMY_TYPES).filter(k => ENEMY_TYPES[k].biome === biome);
  if (!pool.length) return;
  const type = pool[rng() * pool.length | 0];
  const def = ENEMY_TYPES[type];
  let ty = world.surfaceAt(tx) - 2;
  if (def.fly) ty -= 4 + rng() * 6;
  // don't spawn ground enemies in open water
  if (!def.fly && world.liquidAt(tx, ty)) return;
  enemies.push(new Enemy(type, tx * TILE, ty * TILE));
}

export function despawnFar(enemies, player) {
  for (let i = enemies.length - 1; i >= 0; i--) {
    if (Math.abs(enemies[i].x - player.x) > 90 * TILE) enemies.splice(i, 1);
  }
}
