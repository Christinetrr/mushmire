// ══════════════════════════════════════════════════════════════
// Mushmire — save.js
// Persistence: the empire survives a closed tab.
// ══════════════════════════════════════════════════════════════
const KEY = 'mushmire_save_v1';

export function hasSave() {
  try { return !!localStorage.getItem(KEY); } catch { return false; }
}

export function saveGame(game) {
  try {
    const p = game.player;
    const data = {
      v: 1,
      world: game.world.serialize(),
      look: p.look,
      player: { x: p.x, y: p.y, hp: p.hp, slots: p.slots, sel: p.sel },
      clock: game.clock,
      customWeapons: game.customWeapons,
      creatures: game.creatures
        .filter(c => c.kind.startsWith('cat') || c.follows)
        .map(c => ({ kind: c.kind, x: c.x, y: c.y, follows: c.follows, homeX: c.homeX })),
    };
    localStorage.setItem(KEY, JSON.stringify(data));
    return true;
  } catch (e) {
    console.warn('save failed', e);
    return false;
  }
}

export function loadGame() {
  try {
    const raw = localStorage.getItem(KEY);
    return raw ? JSON.parse(raw) : null;
  } catch { return null; }
}

export function clearSave() {
  try { localStorage.removeItem(KEY); } catch { /* */ }
}
