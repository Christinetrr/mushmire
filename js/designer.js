// ══════════════════════════════════════════════════════════════
// Mushmire — designer.js
// The Weapon Forge: draw a weapon pixel-by-pixel (or import an
// image), pay materials, and it becomes a real weapon.
// ══════════════════════════════════════════════════════════════
import { FORGE_PALETTE } from './data.js';

const $ = id => document.getElementById(id);
const GRID = 16;

export const FORGE_COST = { wood: 5, stone: 5, essence: 2 };

export function weaponDamage(pixelCount) {
  return Math.min(16, 5 + Math.floor(pixelCount / 18));
}

export class Forge {
  constructor(game) {
    this.game = game;
    this.grid = new Array(GRID * GRID).fill(null);   // null | '#rrggbb'
    this.color = FORGE_PALETTE[2];
    this.erasing = false;
    this.canvas = $('forge-canvas');
    this.ctx = this.canvas.getContext('2d');

    // palette swatches
    const pal = $('forge-palette');
    FORGE_PALETTE.forEach((c, i) => {
      const sw = document.createElement('div');
      sw.className = 'forge-swatch' + (c === this.color ? ' selected' : '');
      sw.style.background = c;
      sw.addEventListener('pointerdown', e => {
        e.preventDefault();
        this.color = c; this.erasing = false;
        this.refreshSwatches();
      });
      pal.appendChild(sw);
    });

    $('forge-eraser').addEventListener('click', () => { this.erasing = true; this.refreshSwatches(); });
    $('forge-clear').addEventListener('click', () => { this.grid.fill(null); this.redraw(); });
    $('forge-import').addEventListener('change', e => this.importImage(e));
    $('forge-make').addEventListener('click', () => this.make());

    // draw handlers
    let drawing = false;
    const paint = e => {
      const r = this.canvas.getBoundingClientRect();
      const x = Math.floor((e.clientX - r.left) / r.width * GRID);
      const y = Math.floor((e.clientY - r.top) / r.height * GRID);
      if (x < 0 || x >= GRID || y < 0 || y >= GRID) return;
      this.grid[y * GRID + x] = this.erasing ? null : this.color;
      this.redraw();
    };
    this.canvas.addEventListener('pointerdown', e => { e.preventDefault(); drawing = true; paint(e); });
    this.canvas.addEventListener('pointermove', e => { if (drawing) { e.preventDefault(); paint(e); } });
    window.addEventListener('pointerup', () => drawing = false);
    this.redraw();
  }

  refreshSwatches() {
    document.querySelectorAll('.forge-swatch').forEach((sw, i) => {
      sw.classList.toggle('selected', !this.erasing && FORGE_PALETTE[i] === this.color);
    });
    $('forge-eraser').style.outline = this.erasing ? '2px solid #f2ddb0' : 'none';
  }

  open() {
    this.redraw();
    this.updateCost();
  }

  pixelCount() { return this.grid.filter(Boolean).length; }

  updateCost() {
    const p = this.game.player;
    if (!p) return;
    const n = this.pixelCount();
    const parts = Object.entries(FORGE_COST)
      .map(([id, c]) => `${id} ×${c} (${p.count(id)})`).join(' · ');
    $('forge-cost').textContent = n === 0
      ? 'draw your weapon, then forge it — cost: ' + parts
      : `damage: ${weaponDamage(n)} · cost: ${parts}`;
  }

  redraw() {
    const g = this.ctx;
    g.clearRect(0, 0, 256, 256);
    const cell = 256 / GRID;
    for (let i = 0; i < this.grid.length; i++) {
      const c = this.grid[i];
      if (!c) continue;
      g.fillStyle = c;
      g.fillRect((i % GRID) * cell, Math.floor(i / GRID) * cell, cell, cell);
    }
    this.updateCost();
  }

  importImage(e) {
    const file = e.target.files && e.target.files[0];
    if (!file) return;
    const url = URL.createObjectURL(file);
    const img = new Image();
    img.onload = () => {
      const off = document.createElement('canvas');
      off.width = GRID; off.height = GRID;
      const og = off.getContext('2d');
      og.imageSmoothingEnabled = true;
      // fit-center the image into the grid
      const s = Math.min(GRID / img.width, GRID / img.height);
      const dw = img.width * s, dh = img.height * s;
      og.drawImage(img, (GRID - dw) / 2, (GRID - dh) / 2, dw, dh);
      const data = og.getImageData(0, 0, GRID, GRID).data;
      for (let i = 0; i < GRID * GRID; i++) {
        const a = data[i * 4 + 3];
        if (a < 100) { this.grid[i] = null; continue; }
        const r = data[i * 4], g2 = data[i * 4 + 1], b = data[i * 4 + 2];
        // drop near-white backgrounds from photos
        if (r > 240 && g2 > 240 && b > 240) { this.grid[i] = null; continue; }
        this.grid[i] = '#' + [r, g2, b].map(v => v.toString(16).padStart(2, '0')).join('');
      }
      URL.revokeObjectURL(url);
      this.redraw();
      this.game.ui.toast('image woven into the forge ✦');
    };
    img.onerror = () => this.game.ui.toast("couldn't read that image");
    img.src = url;
    e.target.value = '';
  }

  make() {
    const game = this.game, p = game.player;
    const n = this.pixelCount();
    if (n < 6) { game.ui.toast('draw a bit more of it first'); return; }
    if (!p.hasAll(FORGE_COST)) { game.ui.toast('need: wood ×5, stone ×5, night essence ×2'); return; }
    const name = ($('forge-name').value.trim() || 'Nameless Blade');
    p.payAll(FORGE_COST);
    const idx = game.customWeapons.length;
    game.customWeapons.push({ name, grid: this.grid.slice(), dmg: weaponDamage(n) });
    p.add('custom:' + idx, 1);
    game.ui.toast(`⚔ ${name} forged! damage ${weaponDamage(n)}`);
    game.ui.refreshHotbar();
    game.ui.closeModals();
    this.grid.fill(null);
    $('forge-name').value = '';
    this.redraw();
    game.save();
  }
}

// build a 16×16 icon canvas from a stored grid
export function customWeaponCanvas(weapon) {
  const c = document.createElement('canvas');
  c.width = GRID; c.height = GRID;
  const g = c.getContext('2d');
  for (let i = 0; i < weapon.grid.length; i++) {
    if (!weapon.grid[i]) continue;
    g.fillStyle = weapon.grid[i];
    g.fillRect(i % GRID, Math.floor(i / GRID), 1, 1);
  }
  return c;
}
