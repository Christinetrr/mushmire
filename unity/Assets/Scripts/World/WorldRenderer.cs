// ══════════════════════════════════════════════════════════════
// Mushmire — WorldRenderer.cs
// Streams the tile grid into Unity Tilemaps around the camera:
// wall layer (cave backdrop), main tiles (3 variants), edge
// overlays (grass wrap / shading / tufts), cracks, Light2D tiles.
// ══════════════════════════════════════════════════════════════
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.Rendering.Universal;

namespace Mushmire
{
    public class WorldRenderer : MonoBehaviour
    {
        public WorldData world;
        public Camera cam;

        Tilemap wallMap, waterMap, mainMap, sideMap, topMap, crackMap;
        readonly Dictionary<(Tile, int), UnityEngine.Tilemaps.Tile> tileAssets = new();
        readonly Dictionary<string, UnityEngine.Tilemaps.Tile> decorTiles = new();
        readonly Dictionary<(int, bool), WaterAnimTile> waterTiles = new();
        readonly Dictionary<(int, int), Light2D> lightObjs = new();
        UnityEngine.Tilemaps.Tile[] crackTiles;

        const int Chunk = 16;
        readonly HashSet<(int, int)> loadedChunks = new();
        Transform lightRoot;

        public void Init(WorldData w, Camera c)
        {
            world = w; cam = c;
            world.OnTileChanged += OnTileChanged;

            var gridGO = new GameObject("WorldGrid");
            gridGO.transform.SetParent(transform, false);
            var grid = gridGO.AddComponent<Grid>();
            grid.cellSize = Vector3.one;

            wallMap = MakeMap(gridGO, "Walls", -20);
            waterMap = MakeMap(gridGO, "Water", -10);
            mainMap = MakeMap(gridGO, "Tiles", 0);
            sideMap = MakeMap(gridGO, "EdgeSides", 1);
            topMap = MakeMap(gridGO, "EdgeTops", 2);
            crackMap = MakeMap(gridGO, "Cracks", 3);

            lightRoot = new GameObject("TileLights").transform;
            lightRoot.SetParent(transform, false);

            BuildCrackTiles();
        }

        Tilemap MakeMap(GameObject parent, string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var map = go.AddComponent<Tilemap>();
            var rend = go.AddComponent<TilemapRenderer>();
            rend.sortingOrder = order;
            rend.mode = TilemapRenderer.Mode.Chunk;
            return map;
        }

        UnityEngine.Tilemaps.Tile TileAsset(Tile t, int variant)
        {
            if (tileAssets.TryGetValue((t, variant), out var a)) return a;
            a = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
            a.sprite = PixelArtFactory.TileSprite(t, variant);
            a.colliderType = UnityEngine.Tilemaps.Tile.ColliderType.None;
            tileAssets[(t, variant)] = a;
            return a;
        }

        UnityEngine.Tilemaps.Tile DecorTile(string key)
        {
            if (decorTiles.TryGetValue(key, out var a)) return a;
            a = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
            a.sprite = PixelArtFactory.Decor(key);
            a.colliderType = UnityEngine.Tilemaps.Tile.ColliderType.None;
            decorTiles[key] = a;
            return a;
        }

        WaterAnimTile WaterTile(int band, bool surface)
        {
            if (waterTiles.TryGetValue((band, surface), out var a)) return a;
            a = ScriptableObject.CreateInstance<WaterAnimTile>();
            a.frames = PixelArtFactory.WaterFrames(band, surface);
            a.speed = surface ? 6f : 1.6f;
            waterTiles[(band, surface)] = a;
            return a;
        }

        void BuildCrackTiles()
        {
            crackTiles = new UnityEngine.Tilemaps.Tile[3];
            for (int i = 0; i < 3; i++)
            {
                var t = PixelArtFactory.NewTex(32, 32);
                var rng = new Rng((uint)(31337 + i));
                int marks = 6 + i * 7;
                var dark = new Color32(10, 8, 14, 200);
                for (int j = 0; j < marks; j++)
                {
                    int x = rng.Range(3, 28), y = rng.Range(3, 28);
                    for (int s = 0; s < 4 + i * 2; s++)
                    {
                        t.SetPixel(x, y, dark);
                        x += rng.Range(-1, 2); y += rng.Range(-1, 2);
                    }
                }
                var asset = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
                asset.sprite = PixelArtFactory.ToSprite(t);
                asset.colliderType = UnityEngine.Tilemaps.Tile.ColliderType.None;
                crackTiles[i] = asset;
            }
        }

        void LateUpdate()
        {
            if (world == null || cam == null) return;
            StreamChunks();
        }

        void StreamChunks()
        {
            float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
            Vector3 c = cam.transform.position;
            int cx0 = Mathf.FloorToInt((c.x - halfW - 8) / Chunk);
            int cx1 = Mathf.FloorToInt((c.x + halfW + 8) / Chunk);
            // unity y → grid ty (inverted)
            int ty0 = Mathf.Max(0, world.GridY(c.y + halfH + 8));
            int ty1 = Mathf.Min(world.H - 1, world.GridY(c.y - halfH - 8));
            int cy0 = ty0 / Chunk, cy1 = ty1 / Chunk;

            for (int cy = cy0; cy <= cy1; cy++)
                for (int cxi = cx0; cxi <= cx1; cxi++)
                {
                    if (cxi < 0 || cxi * Chunk >= world.W) continue;
                    if (loadedChunks.Contains((cxi, cy))) continue;
                    loadedChunks.Add((cxi, cy));
                    LoadChunk(cxi, cy);
                }

            // unload far chunks occasionally
            if (Time.frameCount % 120 == 0)
            {
                var toRemove = new List<(int, int)>();
                foreach (var ch in loadedChunks)
                    if (Mathf.Abs(ch.Item1 - (cx0 + cx1) / 2) > 8 || Mathf.Abs(ch.Item2 - (cy0 + cy1) / 2) > 6)
                        toRemove.Add(ch);
                foreach (var ch in toRemove) { UnloadChunk(ch.Item1, ch.Item2); loadedChunks.Remove(ch); }
            }
        }

        void LoadChunk(int cx, int cy)
        {
            for (int ty = cy * Chunk; ty < (cy + 1) * Chunk && ty < world.H; ty++)
                for (int tx = cx * Chunk; tx < (cx + 1) * Chunk && tx < world.W; tx++)
                    RefreshCell(tx, ty);
        }

        void UnloadChunk(int cx, int cy)
        {
            for (int ty = cy * Chunk; ty < (cy + 1) * Chunk && ty < world.H; ty++)
                for (int tx = cx * Chunk; tx < (cx + 1) * Chunk && tx < world.W; tx++)
                {
                    var pos = CellPos(tx, ty);
                    wallMap.SetTile(pos, null);
                    waterMap.SetTile(pos, null);
                    mainMap.SetTile(pos, null);
                    sideMap.SetTile(pos, null);
                    topMap.SetTile(pos, null);
                    crackMap.SetTile(pos, null);
                    RemoveLight(tx, ty);
                }
        }

        Vector3Int CellPos(int tx, int ty) => new(tx, world.H - 1 - ty, 0);

        void OnTileChanged(int tx, int ty)
        {
            if (!loadedChunks.Contains((tx / Chunk, ty / Chunk))) return;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    RefreshCell(tx + dx, ty + dy);
        }

        public void RefreshCell(int tx, int ty)
        {
            if (tx < 0 || tx >= world.W || ty < 0 || ty >= world.H) return;
            var pos = CellPos(tx, ty);
            Tile id = world.Get(tx, ty);
            var def = GameData.Tiles[id];

            // ── wall backdrop: deep-sea blue behind water, earth behind caves ──
            // (surface water skips the backdrop — its wavy top must show sky)
            bool touchesWater = world.Get(tx - 1, ty) == Tile.Water || world.Get(tx + 1, ty) == Tile.Water ||
                world.Get(tx, ty - 1) == Tile.Water || world.Get(tx, ty + 1) == Tile.Water;
            bool surfaceWater = id == Tile.Water && world.Get(tx, ty - 1) != Tile.Water;
            if ((id == Tile.Water && !surfaceWater) ||
                (id != Tile.Water && !def.solid && ty >= GameData.SeaLevel && touchesWater))
            {
                // water body, plus submerged decor cells (kelp, shells) in the sea
                wallMap.SetTile(pos, DecorTile("wallWater" + ((tx * 5 + ty * 11) % 2)));
            }
            else if (!def.solid && ty > world.heights[tx])
            {
                int depth = ty - world.heights[tx];
                string biome = GameData.BiomeAt(tx);
                string wall = depth > 10 ? "wallStone"
                    : biome == "desert" ? "wallSand" : biome == "ocean" ? "wallClay" : "wallDirt";
                wallMap.SetTile(pos, DecorTile(wall + ((tx * 5 + ty * 11) % 2)));
            }
            else wallMap.SetTile(pos, null);

            // ── water ──
            if (id == Tile.Water)
            {
                int band = (ty - GameData.SeaLevel) / 10;
                bool surface = world.Get(tx, ty - 1) != Tile.Water;
                waterMap.SetTile(pos, WaterTile(band, surface));
            }
            else waterMap.SetTile(pos, null);

            // ── main tile ──
            if (id == Tile.Air || id == Tile.Water)
                mainMap.SetTile(pos, null);
            else
                mainMap.SetTile(pos, TileAsset(id, (tx * 7 + ty * 13) % 3));

            // ── edge overlays ──
            string side = null, top = null;
            if (def.solid)
            {
                bool upOpen = !world.SolidAt(tx, ty - 1);
                bool dnOpen = !world.SolidAt(tx, ty + 1);
                bool lOpen = !world.SolidAt(tx - 1, ty);
                bool rOpen = !world.SolidAt(tx + 1, ty);
                if (id == Tile.Grass)
                {
                    if (lOpen) side = "grassL";
                    else if (rOpen) side = "grassR";
                    if (dnOpen) top = "grassB";
                }
                else
                {
                    if (lOpen) side = "shadeL";
                    else if (rOpen) side = "shadeR";
                    if (dnOpen) top = "shadeB";
                }
                if (upOpen) top = "lightTop";
            }
            else
            {
                // this open cell hosts a tuft/pebble if the solid tile below has an exposed top
                Tile below = world.Get(tx, ty + 1);
                if (id == Tile.Air)
                {
                    uint h = (uint)((tx * 2654435761) ^ ((ty + 1) * 97));
                    if (below == Tile.Grass && h % 7 < 4) top = "tuft" + (h % 3);
                    else if (below == Tile.Sand && h % 9 < 3) top = "pebble" + (h % 2);
                }
            }
            // roots under tree trunks
            if (id == Tile.Trunk && world.SolidAt(tx, ty + 1)) side = "roots";

            sideMap.SetTile(pos, side != null ? DecorTile(side) : null);
            topMap.SetTile(pos, top != null ? DecorTile(top) : null);

            // ── cracks ──
            if (world.damage.TryGetValue((tx, ty), out var dmg) && def.hp > 0)
            {
                float frac = dmg / def.hp;
                crackMap.SetTile(pos, crackTiles[Mathf.Clamp((int)(frac * 3), 0, 2)]);
            }
            else crackMap.SetTile(pos, null);

            // ── tile light ──
            if (def.light > 0)
            {
                if (!lightObjs.ContainsKey((tx, ty)))
                {
                    var go = new GameObject($"light{tx}_{ty}");
                    go.transform.SetParent(lightRoot, false);
                    go.transform.position = new Vector3(tx + 0.5f, world.UnityY(ty) + 0.5f, 0);
                    var l = go.AddComponent<Light2D>();
                    l.lightType = Light2D.LightType.Point;
                    l.pointLightOuterRadius = def.light * 6f;
                    l.pointLightInnerRadius = 0.4f;
                    l.intensity = 1.15f;
                    l.color = id switch
                    {
                        Tile.Torch => new Color(1f, 0.72f, 0.42f),
                        Tile.Lantern => new Color(1f, 0.78f, 0.5f),
                        Tile.GoldTile => new Color(1f, 0.85f, 0.5f),
                        Tile.Flower => new Color(0.8f, 0.63f, 1f),
                        _ => Color.white,
                    };
                    lightObjs[(tx, ty)] = l;
                    go.AddComponent<LightFlicker>().baseIntensity = l.intensity;
                }
            }
            else RemoveLight(tx, ty);
        }

        void RemoveLight(int tx, int ty)
        {
            if (lightObjs.TryGetValue((tx, ty), out var l))
            {
                if (l != null) Destroy(l.gameObject);
                lightObjs.Remove((tx, ty));
            }
        }

        // wipe everything and re-stream — call after Generate()/Continue load
        public void ReloadAll()
        {
            wallMap.ClearAllTiles();
            waterMap.ClearAllTiles();
            mainMap.ClearAllTiles();
            sideMap.ClearAllTiles();
            topMap.ClearAllTiles();
            crackMap.ClearAllTiles();
            foreach (var l in lightObjs.Values) if (l != null) Destroy(l.gameObject);
            lightObjs.Clear();
            loadedChunks.Clear();
        }
    }

    // TileBase with native tilemap sprite animation; per-column phase offset
    // makes the wave crest travel along the water surface
    public class WaterAnimTile : TileBase
    {
        public Sprite[] frames;
        public float speed = 4f;

        public override void GetTileData(Vector3Int pos, ITilemap tm, ref UnityEngine.Tilemaps.TileData data)
        {
            data.sprite = frames[0];
            data.colliderType = UnityEngine.Tilemaps.Tile.ColliderType.None;
            data.color = Color.white;
        }

        public override bool GetTileAnimationData(Vector3Int pos, ITilemap tm, ref TileAnimationData data)
        {
            data.animatedSprites = frames;
            data.animationSpeed = speed;
            float period = frames.Length / speed;
            data.animationStartTime = Mathf.Repeat(pos.x * 0.13f, period);
            return true;
        }
    }

    public class LightFlicker : MonoBehaviour
    {
        public float baseIntensity = 1f;
        Light2D l;
        float seed;
        void Awake() { l = GetComponent<Light2D>(); seed = transform.position.x * 2.7f; }
        void Update()
        {
            if (l != null) l.intensity = baseIntensity * (1f + 0.08f * Mathf.Sin(Time.time * 11f + seed));
        }
    }
}
