// ══════════════════════════════════════════════════════════════
// Mushmire — ui.js
// HUD, hotbar, inventory, crafting, toasts.
// ══════════════════════════════════════════════════════════════
import { ITEMS, ITEM_ICONS, RECIPES, T, biomeAt } from './data.js';
import { TILE } from './player.js';

const $ = id => document.getElementById(id);

export class UI {
  constructor(game) {
    this.game = game;
    this.toastT = null;
    this.hotbarEls = [];
    this.buildHotbar();

    $('btn-pack').addEventListener('click', () => this.toggleModal('modal-inventory'));
    $('btn-craft').addEventListener('click', () => this.toggleModal('modal-craft'));
    $('btn-forge').addEventListener('click', () => this.toggleModal('modal-forge'));
    document.querySelectorAll('.modal-close').forEach(b =>
      b.addEventListener('click', e => e.target.closest('.modal').classList.remove('visible')));
  }

  anyModalOpen() {
    return !!document.querySelector('.modal.visible');
  }
  toggleModal(id) {
    const m = $(id);
    const was = m.classList.contains('visible');
    document.querySelectorAll('.modal').forEach(x => x.classList.remove('visible'));
    if (!was) {
      m.classList.add('visible');
      if (id === 'modal-inventory') this.renderInventory();
      if (id === 'modal-craft') this.renderCraft();
      if (id === 'modal-forge') this.game.forge.open();
    }
  }
  closeModals() { document.querySelectorAll('.modal').forEach(x => x.classList.remove('visible')); }

  iconCanvas(id) {
    const c = document.createElement('canvas');
    c.width = 16; c.height = 16;
    const src = id.startsWith('custom:') ? this.game.customIcon(id) : ITEM_ICONS[id];
    if (src) c.getContext('2d').drawImage(src, 0, 0, 16, 16);
    return c;
  }
  itemName(id) {
    if (id.startsWith('custom:')) {
      const w = this.game.customWeapons[+id.split(':')[1]];
      return w ? w.name : '???';
    }
    return ITEMS[id]?.name || id;
  }

  // ── hotbar ──
  buildHotbar() {
    const bar = $('hotbar');
    bar.innerHTML = '';
    this.hotbarEls = [];
    for (let i = 0; i < 6; i++) {
      const el = document.createElement('div');
      el.className = 'hb-slot';
      el.addEventListener('pointerdown', e => {
        e.preventDefault();
        this.game.player.sel = i;
        this.refreshHotbar();
      });
      bar.appendChild(el);
      this.hotbarEls.push(el);
    }
    this.refreshHotbar();
  }
  refreshHotbar() {
    const p = this.game.player;
    if (!p) return;
    for (let i = 0; i < 6; i++) {
      const el = this.hotbarEls[i];
      el.classList.toggle('selected', p.sel === i);
      el.innerHTML = '';
      const s = p.slots[i];
      if (s) {
        el.appendChild(this.iconCanvas(s.id));
        if (s.count > 1) {
          const c = document.createElement('span');
          c.className = 'slot-count'; c.textContent = s.count;
          el.appendChild(c);
        }
      }
    }
  }

  // ── inventory ──
  renderInventory() {
    const grid = $('inv-grid');
    grid.innerHTML = '';
    const p = this.game.player;
    p.slots.forEach((s, i) => {
      const el = document.createElement('div');
      el.className = 'inv-slot';
      if (s) {
        el.appendChild(this.iconCanvas(s.id));
        if (s.count > 1) {
          const c = document.createElement('span');
          c.className = 'slot-count'; c.textContent = s.count;
          el.appendChild(c);
        }
        el.addEventListener('pointerdown', e => {
          e.preventDefault();
          // swap with selected hotbar slot
          const tmp = p.slots[p.sel];
          p.slots[p.sel] = p.slots[i];
          p.slots[i] = tmp;
          this.refreshHotbar();
          this.renderInventory();
          this.toast(this.itemName(p.slots[p.sel].id) + ' in hand');
        });
      }
      grid.appendChild(el);
    });
  }

  // ── crafting ──
  nearBench() {
    const p = this.game.player, w = this.game.world;
    const cx = Math.floor(p.center().x / TILE), cy = Math.floor(p.center().y / TILE);
    for (let y = cy - 5; y <= cy + 5; y++)
      for (let x = cx - 7; x <= cx + 7; x++)
        if (w.get(x, y) === T.WORKBENCH) return true;
    return false;
  }
  renderCraft() {
    const list = $('craft-list');
    list.innerHTML = '';
    const p = this.game.player;
    const bench = this.nearBench();
    let lastCat = '';
    for (const r of RECIPES) {
      if (r.cat !== lastCat) {
        lastCat = r.cat;
        const h = document.createElement('h3');
        h.textContent = r.cat;
        list.appendChild(h);
      }
      const can = p.hasAll(r.cost) && (!r.bench || bench);
      const row = document.createElement('div');
      row.className = 'craft-row' + (can ? '' : ' locked');
      const needs = Object.entries(r.cost)
        .map(([id, n]) => `${ITEMS[id].name} ×${n} (${p.count(id)})`)
        .join(' · ') + (r.bench && !bench ? ' · needs workbench nearby' : '');
      row.appendChild(this.iconCanvas(r.out));
      const info = document.createElement('div');
      info.className = 'craft-info';
      info.innerHTML = `<div class="craft-title">${ITEMS[r.out].name}${r.n > 1 ? ' ×' + r.n : ''}</div>
        <div class="craft-needs">${needs}</div>`;
      row.appendChild(info);
      const btn = document.createElement('button');
      btn.className = 'craft-btn';
      btn.textContent = 'Craft';
      btn.addEventListener('click', () => {
        if (!p.hasAll(r.cost) || (r.bench && !this.nearBench())) return;
        p.payAll(r.cost);
        p.add(r.out, r.n);
        this.toast(`crafted ${ITEMS[r.out].name}${r.n > 1 ? ' ×' + r.n : ''} ✦`);
        this.refreshHotbar();
        this.renderCraft();
      });
      row.appendChild(btn);
      list.appendChild(row);
    }
  }

  // ── HUD ──
  refreshHUD() {
    const g = this.game, p = g.player;
    // hearts (2 hp per heart)
    let s = '';
    for (let i = 0; i < p.hpMax / 2; i++) {
      const v = p.hp - i * 2;
      s += v >= 2 ? '❤️' : v >= 1 ? '🧡' : '🖤';
    }
    $('hud-hearts').textContent = s;
    // clock
    $('clock-icon').textContent = g.isNight() ? '☾' : '☀';
    $('clock-text').textContent = 'Day ' + (Math.floor(g.clock / g.cycleLen) + 1);
    // biome
    $('hud-biome').textContent = biomeAt(Math.floor(p.center().x / TILE)).label;
  }

  toast(msg, ms = 1800) {
    const t = $('toast');
    t.textContent = msg;
    t.classList.add('show');
    clearTimeout(this.toastT);
    this.toastT = setTimeout(() => t.classList.remove('show'), ms);
  }
}
