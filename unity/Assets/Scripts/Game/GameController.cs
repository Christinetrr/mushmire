// ══════════════════════════════════════════════════════════════
// Mushmire — GameController.cs
// The conductor: world taps (pet > strike > use/place/mine),
// combat, night spawning, ambient creatures, custom weapons,
// autosave, death & respawn. Port of the web main.js Game class.
// ══════════════════════════════════════════════════════════════
using System.Collections.Generic;
using UnityEngine;

namespace Mushmire
{
    public class CustomWeapon
    {
        public string name;
        public int dmg;
        public Color[] grid = new Color[256];   // 16×16, alpha 0 = empty
        public Sprite sprite;
    }

    public class GameController : MonoBehaviour
    {
        public WorldData world;
        public WorldRenderer worldRenderer;
        public PlayerController player;
        public TouchGestures input;
        public DayNight dayNight;
        public ParticleFX fx;
        public Camera cam;

        public readonly List<Enemy> enemies = new();
        public readonly List<Creature> creatures = new();
        public readonly List<DropItem> drops = new();
        public readonly List<CustomWeapon> customWeapons = new();

        public System.Action OnInventoryChanged;
        public System.Action<string> OnToast;
        public System.Action OnPlayerDied;
        public bool uiModalOpen;
        public bool running;

        Rng rng;
        float spawnTimer, holdTimer, autosaveTimer;
        Transform entityRoot;

        const float Reach = 4.6f;

        public void Init(WorldData w, WorldRenderer wr, PlayerController p, TouchGestures inp,
            DayNight dn, ParticleFX particles, Camera c)
        {
            world = w; worldRenderer = wr; player = p; input = inp; dayNight = dn; fx = particles; cam = c;
            rng = new Rng((uint)System.DateTime.Now.Ticks);
            entityRoot = new GameObject("Entities").transform;
            entityRoot.SetParent(transform, false);

            player.OnJump += () => GameAudio.I.Play("jump", 0.5f);
            player.OnDoubleJump += () => { GameAudio.I.Play("doubleJump", 0.5f); fx.Burst(player.transform.position, GameData.SandBright, 8); };
            player.OnRoll += () => GameAudio.I.Play("roll", 0.5f);
            player.OnHurt += () => GameAudio.I.Play("hurt", 0.7f);
            player.OnDied += () => { GameAudio.I.Play("death"); OnPlayerDied?.Invoke(); };
        }

        public void StartNewGame(CharacterLook look)
        {
            ClearEntities();
            customWeapons.Clear();
            player.inventory = new Inventory();
            player.gameObject.SetActive(true);
            world.Generate((uint)Random.Range(1, int.MaxValue));
            worldRenderer.ReloadAll();
            player.look = look;
            player.inventory.Add("wood_pick", 1);
            player.inventory.Add("wood_sword", 1);
            player.inventory.Add("torch", 5);
            player.Respawn();
            player.iframes = 0;
            dayNight.clock = 30;
            foreach (var spot in world.templeSpots)
                for (int i = 0; i < 2; i++)
                {
                    string kind = rng.Chance(0.5f) ? "cat" : "catOrange";
                    var pos = new Vector2(spot.x + 2 + i * 3 + 0.5f, world.UnityY(spot.y - 1));
                    creatures.Add(Creature.Spawn(kind, pos, false, entityRoot));
                }
            running = true;
            OnInventoryChanged?.Invoke();
            OnToast?.Invoke("welcome to Mushmire, " +
                (string.IsNullOrEmpty(look.name) ? (look.preset == "mei" ? "Mei" : "Khai") : look.name) + " ✦");
        }

        public void ContinueGame(SaveDTO dto)
        {
            ClearEntities();
            customWeapons.Clear();
            player.inventory = new Inventory();
            player.gameObject.SetActive(true);
            world.DeserializeTiles(dto.tilesRle);
            // repair older saves: flood the dry shoreline trenches at the ocean borders
            for (int x = 555; x < 945; x++)
            {
                if (x >= 600 && x < 900) continue;   // ocean proper was always filled
                int surf = world.SurfaceAt(x);
                for (int y = GameData.SeaLevel; y < surf; y++)
                    if (world.Get(x, y) == Tile.Air) world.Set(x, y, Tile.Water, false);
            }
            worldRenderer.ReloadAll();
            world.heights = dto.heights;
            world.spawn = new Vector2Int(dto.spawnX, dto.spawnY);
            world.templeSpots.Clear();
            for (int i = 0; i < dto.templeX.Count; i++)
                world.templeSpots.Add(new Vector2Int(dto.templeX[i], dto.templeY[i]));

            player.look = new CharacterLook
            {
                preset = dto.preset, name = dto.charName,
                hair = GameData.Hex(dto.hair), skin = GameData.Hex(dto.skin),
                top = GameData.Hex(dto.top), legs = GameData.Hex(dto.legs),
            };
            player.transform.position = new Vector3(dto.px, dto.py, 0);
            player.EnsureNotBuried();
            player.hp = dto.hp;
            player.inventory.sel = dto.sel;
            for (int i = 0; i < dto.slotIds.Count && i < player.inventory.slots.Length; i++)
                player.inventory.slots[i] = string.IsNullOrEmpty(dto.slotIds[i]) ? null
                    : new InvSlot { id = dto.slotIds[i], count = dto.slotCounts[i] };

            foreach (var cw in dto.customWeapons)
            {
                var w = new CustomWeapon { name = cw.weaponName, dmg = cw.dmg };
                for (int i = 0; i < 256 && i < cw.grid.Count; i++)
                    w.grid[i] = string.IsNullOrEmpty(cw.grid[i]) ? Color.clear : GameData.Hex(cw.grid[i]);
                w.sprite = ForgePanelHelper.SpriteFromGrid(w.grid);
                customWeapons.Add(w);
            }
            foreach (var cd in dto.creatures)
            {
                var c = Creature.Spawn(cd.kind, new Vector2(cd.x, cd.y), cd.follows, entityRoot);
                c.homeX = cd.homeX;
                creatures.Add(c);
            }
            dayNight.clock = dto.clock;
            running = true;
            OnInventoryChanged?.Invoke();
            OnToast?.Invoke("the empire remembers you ✦");
        }

        public void Save()
        {
            if (!running) return;
            var dto = new SaveDTO
            {
                tilesRle = world.SerializeTiles(),
                heights = world.heights,
                spawnX = world.spawn.x, spawnY = world.spawn.y,
                preset = player.look.preset, charName = player.look.name,
                hair = "#" + ColorUtility.ToHtmlStringRGB(player.look.hair),
                skin = "#" + ColorUtility.ToHtmlStringRGB(player.look.skin),
                top = "#" + ColorUtility.ToHtmlStringRGB(player.look.top),
                legs = "#" + ColorUtility.ToHtmlStringRGB(player.look.legs),
                px = player.transform.position.x, py = player.transform.position.y,
                hp = player.hp, sel = player.inventory.sel,
                clock = dayNight.clock,
            };
            foreach (var spot in world.templeSpots) { dto.templeX.Add(spot.x); dto.templeY.Add(spot.y); }
            foreach (var s in player.inventory.slots)
            {
                dto.slotIds.Add(s?.id ?? "");
                dto.slotCounts.Add(s?.count ?? 0);
            }
            foreach (var w in customWeapons)
            {
                var cw = new CustomWeaponDTO { weaponName = w.name, dmg = w.dmg };
                foreach (var c in w.grid)
                    cw.grid.Add(c.a < 0.1f ? "" : "#" + ColorUtility.ToHtmlStringRGB(c));
                dto.customWeapons.Add(cw);
            }
            foreach (var c in creatures)
            {
                if (c == null) continue;
                if (!c.kind.StartsWith("cat") && !c.follows) continue;
                dto.creatures.Add(new CreatureDTO
                {
                    kind = c.kind, x = c.transform.position.x, y = c.transform.position.y,
                    homeX = c.homeX, follows = c.follows,
                });
            }
            SaveSystem.Save(dto);
        }

        // ── selection helpers ──
        public int SelectedDamage()
        {
            var s = player.inventory.Selected;
            if (s == null) return 1;
            if (s.id.StartsWith("custom:"))
            {
                int i = int.Parse(s.id.Substring(7));
                return i < customWeapons.Count ? customWeapons[i].dmg : 1;
            }
            return GameData.Items.TryGetValue(s.id, out var d) ? d.dmg : 1;
        }

        public Sprite IconFor(string id)
        {
            if (id.StartsWith("custom:"))
            {
                int i = int.Parse(id.Substring(7));
                return i < customWeapons.Count ? customWeapons[i].sprite : null;
            }
            return PixelArtFactory.ItemIcon(id);
        }

        // ── main loop ──
        void Update()
        {
            if (!running) return;
            float dt = Mathf.Min(Time.deltaTime, 0.033f);

            if (!uiModalOpen)
            {
                player.Tick(dt, input);
                if (input.attackPressed) Attack(player.facing);
                if (input.worldTap.HasValue) HandleTap(input.worldTap.Value);
                if (input.worldHold.HasValue)
                {
                    holdTimer -= dt;
                    if (holdTimer <= 0)
                    {
                        holdTimer = 0.16f;
                        HoldMine(input.worldHold.Value);
                    }
                }
                else holdTimer = 0;
            }
            input.Consume();

            // entities
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                var e = enemies[i];
                e.Tick(dt, player, fx);
                if (e.dead)
                {
                    Vector2 pos = e.transform.position;
                    fx.Burst(pos, GameData.Glow, 10);
                    GameAudio.I.Play("ghost", 0.4f);
                    if (e.def.drop != null)
                        drops.Add(DropItem.Spawn(e.def.drop, e.def.dropN, pos, entityRoot, IconFor(e.def.drop)));
                    Destroy(e.gameObject);
                    enemies.RemoveAt(i);
                }
            }
            foreach (var c in creatures) if (c != null) c.Tick(dt, player, fx);
            for (int i = drops.Count - 1; i >= 0; i--)
            {
                int before = player.inventory.Count(drops[i].id);
                drops[i].Tick(dt, player);
                if (drops[i].dead)
                {
                    if (player.inventory.Count(drops[i].id) != before)
                    {
                        GameAudio.I.Play("pickup", 0.5f);
                        OnInventoryChanged?.Invoke();
                    }
                    Destroy(drops[i].gameObject);
                    drops.RemoveAt(i);
                }
            }

            // day/night + spawning
            var pc = player.Center;
            int ptx = Mathf.FloorToInt(pc.x);
            dayNight.Tick(dt, pc, GameData.BiomeAt(ptx));
            GameAudio.I.SetNight(dayNight.IsNight);

            spawnTimer -= dt;
            if (spawnTimer <= 0)
            {
                spawnTimer = 2.4f;
                TrySpawnEnemy();
                ManageAmbient();
                DespawnFar();
            }
            if (!dayNight.IsNight && enemies.Count > 0)
            {
                foreach (var e in enemies)
                {
                    fx.Burst(e.transform.position, GameData.SandBright, 4);
                    Destroy(e.gameObject);
                }
                enemies.Clear();
            }

            // camera follow
            var camPos = cam.transform.position;
            float halfW = cam.orthographicSize * cam.aspect;
            float targetX = Mathf.Clamp(pc.x, halfW, world.W - halfW);
            float targetY = Mathf.Clamp(pc.y + 1.5f, cam.orthographicSize, world.H);
            cam.transform.position = Vector3.Lerp(camPos, new Vector3(targetX, targetY, -10), Mathf.Min(1, dt * 10));

            autosaveTimer += dt;
            if (autosaveTimer > 25) { autosaveTimer = 0; Save(); }
        }

        // ── world interaction ──
        Vector2 ScreenToWorld(Vector2 screen) => cam.ScreenToWorldPoint(screen);

        void HandleTap(Vector2 screen)
        {
            if (player.dead) return;
            Vector2 wp = ScreenToWorld(screen);
            Vector2 pc = player.Center;
            float dist = Vector2.Distance(wp, pc);

            // 1. pet a creature
            foreach (var c in creatures)
            {
                if (c == null) continue;
                if (Vector2.Distance(wp, c.transform.position) < 0.9f && dist < Reach * 1.4f)
                {
                    c.Pet(fx);
                    GameAudio.I.Play("purr", 0.8f);
                    if (c.kind.StartsWith("cat")) OnToast?.Invoke("purrrr… you feel blessed");
                    return;
                }
            }
            // 2. strike an enemy near the tap
            foreach (var e in enemies)
            {
                if (Vector2.Distance(wp, e.transform.position) < 1.0f && dist < Reach)
                {
                    Attack((int)Mathf.Sign(e.transform.position.x - pc.x));
                    return;
                }
            }
            if (dist > Reach) return;

            int tx = Mathf.FloorToInt(wp.x), ty = world.GridY(wp.y);
            var s = player.inventory.Selected;
            ItemDef def = s != null && !s.id.StartsWith("custom:") && GameData.Items.ContainsKey(s.id)
                ? GameData.Items[s.id] : null;

            if (def != null && def.type == ItemType.Use)
            {
                if (player.hp < player.hpMax)
                {
                    player.Heal(def.heal);
                    player.inventory.Remove(s.id, 1);
                    fx.Burst(pc, new Color(0.91f, 0.48f, 0.56f), 8);
                    GameAudio.I.Play("heal");
                    OnToast?.Invoke("you feel mended ✦");
                    OnInventoryChanged?.Invoke();
                }
                else OnToast?.Invoke("already at full health");
                return;
            }
            if (def != null && def.type == ItemType.Charm)
            {
                creatures.Add(Creature.Spawn(def.creature, wp + Vector2.up * 0.5f, true, entityRoot));
                player.inventory.Remove(s.id, 1);
                fx.Burst(wp, GameData.Glow, 12);
                GameAudio.I.Play("charm");
                OnToast?.Invoke("a friend answers the charm ✦");
                OnInventoryChanged?.Invoke();
                return;
            }
            if (def != null && def.type == ItemType.Block)
            {
                // don't place a solid block inside yourself
                bool solid = GameData.Tiles[def.tile].solid;
                var ppos = player.transform.position;
                bool inMe = solid &&
                    wp.x > ppos.x - player.WBody && wp.x < ppos.x + player.WBody &&
                    wp.y > ppos.y - 0.1f && wp.y < ppos.y + player.HBody + 0.1f;
                if (!inMe && world.CanPlace(tx, ty))
                {
                    world.Set(tx, ty, def.tile);
                    player.inventory.Remove(s.id, 1);
                    GameAudio.I.Play("place", 0.6f);
                    OnInventoryChanged?.Invoke();
                }
                return;
            }
            if (def != null && def.type == ItemType.Tool)
            {
                MineAt(tx, ty, def);
                return;
            }
            Attack((int)Mathf.Sign(wp.x - pc.x));
        }

        void HoldMine(Vector2 screen)
        {
            var s = player.inventory.Selected;
            if (s == null || s.id.StartsWith("custom:")) return;
            if (!GameData.Items.TryGetValue(s.id, out var def) || def.type != ItemType.Tool) return;
            Vector2 wp = ScreenToWorld(screen);
            if (Vector2.Distance(wp, player.Center) > Reach) return;
            MineAt(Mathf.FloorToInt(wp.x), world.GridY(wp.y), def);
        }

        void MineAt(int tx, int ty, ItemDef def)
        {
            var res = world.Mine(tx, ty, def.power, def.tier);
            if (res == null) return;
            if (res.blocked) { OnToast?.Invoke("this needs a stronger pick"); return; }
            Vector2 mid = new(tx + 0.5f, world.UnityY(ty) + 0.5f);
            if (res.broken)
            {
                fx.Burst(mid, GameData.SandDim, 6);
                GameAudio.I.Play("break", 0.6f);
                if (res.drop != null)
                    drops.Add(DropItem.Spawn(res.drop, 1, mid, entityRoot, IconFor(res.drop)));
            }
            else
            {
                fx.Spawn(mid, new Color(0.55f, 0.55f, 0.6f));
                GameAudio.I.Play("dig", 0.45f);
            }
        }

        public void Attack(int dir)
        {
            if (!player.Swing()) return;
            player.facing = dir != 0 ? dir : player.facing;
            GameAudio.I.Play("swing", 0.5f);
            Vector2 pc = player.Center;
            int dmg = SelectedDamage();
            foreach (var e in enemies)
            {
                Vector2 ep = e.transform.position;
                bool inX = player.facing > 0
                    ? ep.x > pc.x - 0.25f && ep.x < pc.x + 1.9f
                    : ep.x < pc.x + 0.25f && ep.x > pc.x - 1.9f;
                if (inX && Mathf.Abs(ep.y - pc.y) < 1.4f)
                {
                    e.Hit(dmg, player.facing, fx);
                    GameAudio.I.Play("hit", 0.6f);
                }
            }
        }

        public void Respawn()
        {
            player.Respawn();
            foreach (var e in enemies) Destroy(e.gameObject);
            enemies.Clear();
        }

        // menu → save, freeze, and hand the screen back to the title
        public void StopToMenu()
        {
            Save();
            running = false;
            uiModalOpen = false;
            ClearEntities();
            player.gameObject.SetActive(false);
        }

        void ClearEntities()
        {
            foreach (var e in enemies) if (e != null) Destroy(e.gameObject);
            enemies.Clear();
            foreach (var c in creatures) if (c != null) Destroy(c.gameObject);
            creatures.Clear();
            foreach (var d in drops) if (d != null) Destroy(d.gameObject);
            drops.Clear();
        }

        // ── spawning ──
        void TrySpawnEnemy()
        {
            if (!dayNight.IsNight || player.dead) return;
            int near = 0;
            foreach (var e in enemies)
                if (Mathf.Abs(e.transform.position.x - player.transform.position.x) < 60) near++;
            if (near >= 5) return;
            int side = rng.Chance(0.5f) ? -1 : 1;
            int tx = Mathf.FloorToInt(player.transform.position.x) + side * (24 + rng.Range(0, 14));
            if (tx < 2 || tx >= world.W - 2) return;
            string biome = GameData.BiomeAt(tx);
            var pool = new List<string>();
            foreach (var kv in GameData.Enemies) if (kv.Value.biome == biome) pool.Add(kv.Key);
            if (pool.Count == 0) return;
            string type = pool[rng.Range(0, pool.Count)];
            var def = GameData.Enemies[type];
            int ty = world.SurfaceAt(tx) - 2;
            if (def.fly) ty -= 4 + rng.Range(0, 6);
            if (!def.fly && world.LiquidAt(tx, ty)) return;
            enemies.Add(Enemy.Spawn(type, new Vector2(tx + 0.5f, world.UnityY(ty)), entityRoot));
        }

        void DespawnFar()
        {
            for (int i = enemies.Count - 1; i >= 0; i--)
                if (Mathf.Abs(enemies[i].transform.position.x - player.transform.position.x) > 90)
                {
                    Destroy(enemies[i].gameObject);
                    enemies.RemoveAt(i);
                }
        }

        void ManageAmbient()
        {
            var ppos = player.transform.position;
            string biome = GameData.BiomeAt(Mathf.FloorToInt(ppos.x));

            int fireflies = 0, fish = 0;
            foreach (var c in creatures)
            {
                if (c == null) continue;
                if (c.kind == "firefly") fireflies++;
                if (c.kind == "fishy") fish++;
            }
            if (dayNight.IsNight && (biome == "tropic" || biome == "lantern"))
            {
                if (fireflies < 5)
                {
                    var pos = new Vector2(ppos.x + (rng.Next() - 0.5f) * 19, ppos.y + 2 + rng.Next() * 4);
                    creatures.Add(Creature.Spawn("firefly", pos, false, entityRoot));
                }
            }
            else if (fireflies > 0) RemoveAmbient("firefly");

            if (biome == "ocean")
            {
                if (fish < 4)
                {
                    float fxp = ppos.x + (rng.Next() - 0.5f) * 25;
                    int tx = Mathf.FloorToInt(fxp);
                    int ty = (world.SurfaceAt(tx) + GameData.SeaLevel) / 2 - 2;
                    if (world.LiquidAt(tx, ty))
                        creatures.Add(Creature.Spawn("fishy", new Vector2(fxp, world.UnityY(ty)), false, entityRoot));
                }
            }
            else if (fish > 0) RemoveAmbient("fishy");

            // cull faraway ambient
            for (int i = creatures.Count - 1; i >= 0; i--)
            {
                var c = creatures[i];
                if (c == null) { creatures.RemoveAt(i); continue; }
                if (c.follows || c.kind.StartsWith("cat")) continue;
                if (Mathf.Abs(c.transform.position.x - ppos.x) > 38)
                {
                    Destroy(c.gameObject);
                    creatures.RemoveAt(i);
                }
            }
        }

        void RemoveAmbient(string kind)
        {
            for (int i = creatures.Count - 1; i >= 0; i--)
                if (creatures[i] != null && creatures[i].kind == kind)
                {
                    Destroy(creatures[i].gameObject);
                    creatures.RemoveAt(i);
                }
        }

        public void SpawnCharmCreature(string kind, Vector2 pos)
            => creatures.Add(Creature.Spawn(kind, pos, true, entityRoot));

        void OnApplicationPause(bool paused) { if (paused) Save(); }
        void OnApplicationQuit() { Save(); }
    }
}
