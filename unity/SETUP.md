# Mushmire — Unity port (native iOS)

The complete game lives in code: **zero binary assets**. Every sprite, sound and
the music are generated at runtime, and an editor script configures the project
(URP 2D lighting, landscape iOS settings, the scene) the first time you open it.

## What you do (one time, ~15 min + downloads)

1. **Install Unity Hub** — https://unity.com/download
2. Open Unity Hub → sign in (free Unity account).
3. Install an editor: **Unity 6 (6000.x LTS)**. In the install options, tick
   **iOS Build Support**.
4. Unity Hub → **Add project from disk** → select this `unity/` folder → open it.
   First open takes a few minutes (package import). The setup script runs by
   itself; when it finishes you'll see *"Mushmire ✦ project setup complete"* in
   the Console. (If anything goes wrong: menu **Mushmire → Setup Project**.)
5. Open `Assets/Scenes/Main.unity`, press **Play** — the title screen should
   appear with the floating spores.

Then tell Claude "Unity is installed" — from that point the compile/fix/build
loop can run over the command line, ending in an Xcode project you open once,
select your (free) Apple ID team, plug in the iPhone, and press Run.

## Getting it on the iPhone

- Xcode → open `unity/Builds/iOS/Unity-iPhone.xcodeproj`
- Signing & Capabilities → Team: your Apple ID (free works; app re-signs weekly)
- Plug in iPhone → select it as target → **Run**.
- For Richard to install it without your Mac: Apple Developer Program ($99/yr)
  + TestFlight.

## Layout

```
Assets/Scripts/Core      GameData (tiles/items/recipes), PixelArtFactory (all sprites)
Assets/Scripts/World     WorldData (grid+gen+save), WorldRenderer (tilemaps, lights)
Assets/Scripts/Player    PlayerController (physics), Inventory
Assets/Scripts/Entities  Enemy, Creature, DropItem, ParticleFX
Assets/Scripts/Systems   DayNight, GameAudio (synth SFX+music), TouchGestures, SaveSystem
Assets/Scripts/UI        TitleScreen (palette+spores preserved), HUD, Panels (pack/craft/forge)
Assets/Scripts/Game      Bootstrap (assembles everything), GameController (game loop)
Assets/Editor            MushmireSetup (self-config), BuildScript (CLI iOS build)
```

## Known TODOs for the port

- Weapon forge: drawing works; *importing a photo* needs an iOS gallery plugin
  (e.g. NativeGallery) — deliberately left out of v1.
- First compile hasn't run yet (no Unity on the dev machine when this was
  written) — expect a short fix-up pass once the editor is installed.
