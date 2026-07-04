// ══════════════════════════════════════════════════════════════
// Mushmire — player.js
// Physics, movement states, health, inventory.
// ══════════════════════════════════════════════════════════════
import { ITEMS } from './data.js';
import { SEA_LEVEL } from './world.js';

export const TILE = 16;

const WALK = 88, SPRINT = 150, DUCK_SPEED = 42;
const JUMP_V = -238, GRAV = 640, MAX_FALL = 380;
const ROLL_SPEED = 215, ROLL_TIME = 0.34;
const SWIM_GRAV = 130, SWIM_UP = -120, MAX_SINK = 70;

export class Player {
  constructor(look) {
    this.look = look; // { preset:'mei'|'khai', hair, skin, top, legs, name }
    this.x = 0; this.y = 0;
    this.vx = 0; this.vy = 0;
    this.w = 9; this.hFull = 21; this.hDuck = 13;
    this.h = this.hFull;
    this.facing = 1;
    this.grounded = false;
    this.inWater = false;
    this.breath = 1;               // 0..1
    this.ducking = false;
    this.sprinting = false;
    this.usedDoubleJump = false;
    this.rollT = 0;                // >0 while rolling
    this.rollDir = 1;
    this.iframes = 0;
    this.swingT = 0;               // >0 while swinging
    this.swingDur = 0.28;
    this.hpMax = 10; this.hp = 10;
    this.animT = 0;
    this.dead = false;

    this.slots = new Array(24).fill(null);   // {id, count}
    this.sel = 0;                            // hotbar index 0..5
    this.add('wood_pick', 1);
    this.add('wood_sword', 1);
    this.add('torch', 5);
  }

  get selected() { return this.slots[this.sel]; }
  itemDef(slot) {
    if (!slot) return null;
    return slot.id.startsWith('custom:') ? null : ITEMS[slot.id];
  }

  // ── inventory ──
  add(id, n = 1) {
    const max = id.startsWith('custom:') ? 1 : (ITEMS[id]?.stack || 99);
    for (let i = 0; i < this.slots.length && n > 0; i++) {
      const s = this.slots[i];
      if (s && s.id === id && s.count < max) {
        const take = Math.min(n, max - s.count);
        s.count += take; n -= take;
      }
    }
    for (let i = 0; i < this.slots.length && n > 0; i++) {
      if (!this.slots[i]) {
        const take = Math.min(n, max);
        this.slots[i] = { id, count: take }; n -= take;
      }
    }
    return n === 0;
  }
  count(id) {
    let c = 0;
    for (const s of this.slots) if (s && s.id === id) c += s.count;
    return c;
  }
  remove(id, n = 1) {
    for (let i = 0; i < this.slots.length && n > 0; i++) {
      const s = this.slots[i];
      if (s && s.id === id) {
        const take = Math.min(n, s.count);
        s.count -= take; n -= take;
        if (s.count <= 0) this.slots[i] = null;
      }
    }
  }
  hasAll(cost) { for (const id in cost) if (this.count(id) < cost[id]) return false; return true; }
  payAll(cost) { for (const id in cost) this.remove(id, cost[id]); }

  // ── physics ──
  update(dt, input, world) {
    if (this.dead) return;
    this.animT += dt;

    const cx = Math.floor((this.x + this.w / 2) / TILE);
    const cy = Math.floor((this.y + this.h / 2) / TILE);
    this.inWater = world.liquidAt(cx, cy);

    // breath
    const headTile = world.get(Math.floor((this.x + this.w / 2) / TILE), Math.floor((this.y + 3) / TILE));
    const headUnder = headTile === 8; // T.WATER
    if (headUnder) {
      this.breath -= dt / 9;
      if (this.breath <= 0) { this.breath = 0; this._drownT = (this._drownT || 0) + dt; if (this._drownT > 1) { this._drownT = 0; this.damage(1, 0); } }
    } else this.breath = Math.min(1, this.breath + dt * 0.6);

    // ducking (not while swimming or rolling)
    const wantDuck = input.down && this.grounded && this.rollT <= 0 && !this.inWater;
    if (wantDuck && !this.ducking) { this.ducking = true; this.y += this.hFull - this.hDuck; this.h = this.hDuck; }
    if (!wantDuck && this.ducking) {
      // only stand if there's headroom
      if (!this.collides(world, this.x, this.y - (this.hFull - this.hDuck), this.w, this.hFull)) {
        this.ducking = false; this.y -= this.hFull - this.hDuck; this.h = this.hFull;
      }
    }

    // roll
    if (input.rollPressed && this.grounded && this.rollT <= 0 && !this.ducking) {
      this.rollT = ROLL_TIME;
      this.rollDir = input.left ? -1 : input.right ? 1 : this.facing;
      this.iframes = Math.max(this.iframes, ROLL_TIME + 0.08);
    }

    // horizontal
    let target = 0;
    this.sprinting = input.sprint && (input.left || input.right) && !this.ducking;
    const spd = this.ducking ? DUCK_SPEED : this.sprinting ? SPRINT : WALK;
    if (input.left) { target = -spd; this.facing = -1; }
    if (input.right) { target = spd; this.facing = 1; }
    if (this.rollT > 0) {
      this.rollT -= dt;
      target = this.rollDir * ROLL_SPEED;
      this.facing = this.rollDir;
    }
    const accel = this.grounded ? 900 : 560;
    if (this.inWater) target *= 0.65;
    if (target > this.vx) this.vx = Math.min(target, this.vx + accel * dt);
    else if (target < this.vx) this.vx = Math.max(target, this.vx - accel * dt);
    else {
      const fric = this.grounded ? 700 : 120;
      if (this.vx > 0) this.vx = Math.max(0, this.vx - fric * dt);
      else this.vx = Math.min(0, this.vx + fric * dt);
    }

    // vertical
    if (this.inWater) {
      this.vy += SWIM_GRAV * dt;
      if (input.jumpPressed || (input.jump && this.vy > -40)) this.vy = SWIM_UP;
      if (this.vy > MAX_SINK) this.vy = MAX_SINK;
      this.usedDoubleJump = false;
    } else {
      this.vy += GRAV * dt;
      if (this.vy > MAX_FALL) this.vy = MAX_FALL;
      if (input.jumpPressed) {
        if (this.grounded && !this.ducking) {
          this.vy = JUMP_V; this.grounded = false;
        } else if (!this.grounded && !this.usedDoubleJump) {
          this.vy = JUMP_V * 0.88; this.usedDoubleJump = true;
          this.spawnPoof = true; // render hook
        }
      }
    }

    // integrate + collide
    this.moveAxis(world, this.vx * dt, 0);
    const wasFalling = this.vy > 0;
    const hitGround = this.moveAxis(world, 0, this.vy * dt);
    this.grounded = hitGround && wasFalling;
    if (this.grounded) this.usedDoubleJump = false;

    if (this.iframes > 0) this.iframes -= dt;
    if (this.swingT > 0) this.swingT -= dt;

    // fell out of the world
    if (this.y > world.h * TILE + 200) this.damage(99, 0);
  }

  collides(world, x, y, w, h) {
    const x0 = Math.floor(x / TILE), x1 = Math.floor((x + w - 0.01) / TILE);
    const y0 = Math.floor(y / TILE), y1 = Math.floor((y + h - 0.01) / TILE);
    for (let ty = y0; ty <= y1; ty++)
      for (let tx = x0; tx <= x1; tx++)
        if (world.solidAt(tx, ty)) return true;
    return false;
  }

  // returns true if movement was blocked
  moveAxis(world, dx, dy) {
    let blocked = false;
    const step = 4;
    while (dx !== 0 || dy !== 0) {
      const sx = Math.abs(dx) > step ? Math.sign(dx) * step : dx;
      const sy = Math.abs(dy) > step ? Math.sign(dy) * step : dy;
      dx -= sx; dy -= sy;
      if (!this.collides(world, this.x + sx, this.y + sy, this.w, this.h)) {
        this.x += sx; this.y += sy;
        continue;
      }
      // walking into a low ledge: auto-climb up to one tile
      if (sx !== 0 && this.grounded) {
        let stepped = false;
        for (let k = 4; k <= TILE; k += 4) {
          if (!this.collides(world, this.x + sx, this.y - k, this.w, this.h)) {
            this.x += sx; this.y -= k; stepped = true; break;
          }
        }
        if (stepped) continue;
      }
      // pixel steps for a snug fit against the obstacle
      const px = Math.sign(sx), py = Math.sign(sy);
      for (let i = 0; i < Math.max(Math.abs(sx), Math.abs(sy)); i++) {
        if (px && !this.collides(world, this.x + px, this.y, this.w, this.h)) this.x += px;
        else if (py && !this.collides(world, this.x, this.y + py, this.w, this.h)) this.y += py;
        else break;
      }
      blocked = true;
      if (sy !== 0) this.vy = 0;
      if (sx !== 0 && this.rollT <= 0) this.vx = 0;
      dx = 0; dy = 0;
    }
    return blocked;
  }

  swing() {
    if (this.swingT > 0) return false;
    this.swingT = this.swingDur;
    return true;
  }

  damage(n, fromDir) {
    if (this.iframes > 0 || this.dead) return false;
    this.hp -= n;
    this.iframes = 0.8;
    this.vx = fromDir * 130;
    this.vy = -120;
    if (this.hp <= 0) { this.hp = 0; this.dead = true; }
    return true;
  }
  heal(n) { this.hp = Math.min(this.hpMax, this.hp + n); }

  respawn(world) {
    this.x = world.spawn.x * TILE;
    this.y = world.spawn.y * TILE;
    this.hp = this.hpMax;
    this.dead = false;
    this.vx = this.vy = 0;
    this.breath = 1;
    this.iframes = 2;
  }

  center() { return { x: this.x + this.w / 2, y: this.y + this.h / 2 }; }
}
