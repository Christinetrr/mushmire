# 🍄 Mushmire

*A 2D sandbox action-adventure RPG.*
*To: my baby Richard ♥*

Two editions live in this repo:
- **Web edition** (this folder) — instant play in any browser, including iPhone Safari.
- **Unity edition** (`unity/`) — the native iOS port with URP 2D lighting and
  synthesized sound. See [`unity/SETUP.md`](unity/SETUP.md) to get it running.

Mushmire (the **Mush Empire**) is a Terraria-inspired, pixel-art sandbox with its own mystical
personality — four hand-tuned biomes, day/night survival, crafting, building, creature charms,
and a Weapon Forge where you literally draw your own weapons into existence.

Everything is vanilla JavaScript + HTML5 canvas. No build step, no dependencies, no backend.
Saves live in the browser (localStorage) and survive closing the tab.

## Play it on your iPhone

1. On the Mac, from this folder:
   ```
   npm start        # (or: python3 -m http.server 8000)
   ```
2. On the iPhone (same Wi-Fi), open Safari and go to:
   ```
   http://<your-mac-ip>:8000
   ```
   (Find the IP with `ipconfig getifaddr en0` — e.g. `http://10.0.0.129:8000`)
3. Turn the phone **landscape**. For true fullscreen, tap Share → **Add to Home Screen**
   and launch it from the icon.

To play from anywhere (no Mac needed), push this folder to GitHub and enable GitHub Pages —
it's a fully static site.

On the Mac itself it also plays fine in any browser: arrows/WASD move, Space jumps,
S ducks, Shift sprints, K/X attacks, mouse clicks the world.

## The Four Realms

| Realm | Character | Night brings… |
|---|---|---|
| 🏜 Sunken Dunes | cactus, sandstone, buried ruins, gold | Dune Shades, Gilded Scarabs |
| 🌴 Verdant Tropics | your gentle starting home; trees & glowblooms | Jungle Stalkers, Sporelings |
| 🌊 Glass Ocean | swim, hold your breath, coral, kelp, islands | Tide Wisps, Moonshell Crabs |
| 🏮 Lantern Coast | bamboo, jade, golden temples… and cats | Hungry Ghosts, Stray Lanterns |

## Touch controls

- ◀ ▶ walk · **double-tap and hold** to sprint
- ⬆ jump · tap ⬆ again mid-air to **double jump**
- ⬇ duck · **double-tap** ⬇ to roll (grants dodge i-frames)
- ⚔ swing your held item
- **Tap the world** to mine (pickaxe), place (block), use a charm, or strike an enemy.
  Hold to keep digging. Tap a cat to pet it (important).

## Systems

- **Day/night cycle** (8 min) — each biome exhales its own spirits at night; dawn dissolves them.
- **Mining tiers** — wood → stone → copper → jade picks; jade ore needs a copper pick or better.
- **Crafting** — at hand or near a workbench: tools, weapons, blocks, torches, silk lanterns,
  healing salve, and creature charms (cat, scarab, sprite) that summon loyal companions.
- **Weapon Forge (✎)** — draw a weapon on a 16×16 grid or import any image; pay wood, stone
  and night essence and it's forged as a real usable weapon (damage scales with your design).
- **Character design** — two presets, Mei and Khai, plus full hair/skin/outfit color control.
- **Autosave** — every 25 s and whenever the tab hides; “Continue Journey” resumes exactly
  where the empire left off.

## Code map

```
index.html        screens, HUD, modals, touch buttons
css/style.css     mystical/sand theme
js/data.js        tiles, items, recipes, enemies, pixel art
js/world.js       procedural generation + RLE save format
js/player.js      physics & inventory
js/entities.js    enemy AI, cats & pets, drops, particles
js/input.js       touch gestures + keyboard
js/render.js      sky, parallax, tiles, lighting, player art
js/ui.js          HUD, inventory, crafting
js/designer.js    the Weapon Forge
js/save.js        localStorage persistence
js/main.js        boot, screens, game loop
```
