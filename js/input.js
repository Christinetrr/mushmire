// ══════════════════════════════════════════════════════════════
// Mushmire — input.js
// Touch buttons (with double-tap gestures) + keyboard fallback
// + world-tap tracking on the canvas.
// ══════════════════════════════════════════════════════════════

const DOUBLE_TAP_MS = 280;

export class Input {
  constructor(canvas) {
    this.left = false; this.right = false;
    this.jump = false; this.down = false;
    this.sprint = false;                    // latched by double-tap-hold on ◀/▶
    this.jumpPressed = false;               // edge, consumed each frame
    this.rollPressed = false;               // edge (double-tap ⬇)
    this.attackPressed = false;             // edge
    this.attackHeld = false;

    this.worldTap = null;                   // {x,y} css px — one-shot
    this.worldHold = null;                  // {x,y} while finger stays down

    this._lastTapAt = {};                   // btn key → timestamp
    this._canvasPointer = null;

    this.bindButton('ctl-left', 'left', true);
    this.bindButton('ctl-right', 'right', true);
    this.bindButton('ctl-jump', 'jump');
    this.bindButton('ctl-down', 'down');
    this.bindButton('ctl-attack', 'attack');
    this.bindCanvas(canvas);
    this.bindKeyboard();
  }

  consume() {
    this.jumpPressed = false;
    this.rollPressed = false;
    this.attackPressed = false;
    this.worldTap = null;
  }

  press(key) {
    const now = performance.now();
    const dbl = now - (this._lastTapAt[key] || -9999) < DOUBLE_TAP_MS;
    this._lastTapAt[key] = now;
    switch (key) {
      case 'left': this.left = true; if (dbl) this.sprint = true; break;
      case 'right': this.right = true; if (dbl) this.sprint = true; break;
      case 'jump': this.jump = true; this.jumpPressed = true; break;
      case 'down': this.down = true; if (dbl) this.rollPressed = true; break;
      case 'attack': this.attackPressed = true; this.attackHeld = true; break;
    }
  }
  release(key) {
    switch (key) {
      case 'left': this.left = false; if (!this.right) this.sprint = false; break;
      case 'right': this.right = false; if (!this.left) this.sprint = false; break;
      case 'jump': this.jump = false; break;
      case 'down': this.down = false; break;
      case 'attack': this.attackHeld = false; break;
    }
  }

  bindButton(elId, key, sprintable = false) {
    const el = document.getElementById(elId);
    if (!el) return;
    const down = e => {
      e.preventDefault();
      el.classList.add('pressed');
      this.press(key);
      if (sprintable && this.sprint) el.classList.add('sprinting');
    };
    const up = e => {
      e.preventDefault();
      el.classList.remove('pressed', 'sprinting');
      this.release(key);
    };
    el.addEventListener('pointerdown', down);
    el.addEventListener('pointerup', up);
    el.addEventListener('pointercancel', up);
    el.addEventListener('pointerleave', up);
    el.addEventListener('contextmenu', e => e.preventDefault());
  }

  bindCanvas(canvas) {
    canvas.addEventListener('pointerdown', e => {
      e.preventDefault();
      if (this._canvasPointer !== null) return;
      this._canvasPointer = e.pointerId;
      const p = { x: e.clientX, y: e.clientY };
      this.worldTap = p;
      this.worldHold = { ...p };
    });
    canvas.addEventListener('pointermove', e => {
      if (e.pointerId !== this._canvasPointer || !this.worldHold) return;
      this.worldHold.x = e.clientX; this.worldHold.y = e.clientY;
    });
    const end = e => {
      if (e.pointerId !== this._canvasPointer) return;
      this._canvasPointer = null;
      this.worldHold = null;
    };
    canvas.addEventListener('pointerup', end);
    canvas.addEventListener('pointercancel', end);
    canvas.addEventListener('contextmenu', e => e.preventDefault());
  }

  bindKeyboard() {
    const keymap = k => ({
      arrowleft: 'left', a: 'left',
      arrowright: 'right', d: 'right',
      arrowup: 'jump', w: 'jump', ' ': 'jump',
      arrowdown: 'down', s: 'down',
      k: 'attack', x: 'attack',
    })[k];
    window.addEventListener('keydown', e => {
      if (e.repeat) return;
      const key = keymap(e.key.toLowerCase());
      if (key) { e.preventDefault(); this.press(key); }
      if (e.key === 'Shift') this.sprint = true;
    });
    window.addEventListener('keyup', e => {
      const key = keymap(e.key.toLowerCase());
      if (key) this.release(key);
      if (e.key === 'Shift') this.sprint = false;
    });
  }
}
