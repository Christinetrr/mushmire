// ══════════════════════════════════════════════════════════════
// Mushmire — WorldData.cs
// Tile grid, generation (port of web world.js), mining, saving.
// Grid coordinates: (tx, ty) with ty = 0 at the TOP (web parity).
// Unity conversion: a cell's bottom-left = (tx, WorldH - 1 - ty).
// ══════════════════════════════════════════════════════════════
using System.Collections.Generic;
using UnityEngine;

namespace Mushmire
{
    public class WorldData
    {
        public readonly int W = GameData.WorldW, H = GameData.WorldH;
        public byte[] tiles;
        public int[] heights;                       // original surface row per column
        public readonly Dictionary<(int, int), float> damage = new();
        public Vector2Int spawn;                    // grid coords
        public readonly List<Vector2Int> templeSpots = new();

        public System.Action<int, int> OnTileChanged;   // (tx, ty)

        public WorldData()
        {
            tiles = new byte[W * H];
            heights = new int[W];
        }

        // ── access ──
        public Tile Get(int tx, int ty)
        {
            if (ty < 0) return Tile.Air;
            if (tx < 0 || tx >= W || ty >= H) return Tile.Stone;
            return (Tile)tiles[ty * W + tx];
        }
        public void Set(int tx, int ty, Tile t, bool notify = true)
        {
            if (tx < 0 || tx >= W || ty < 0 || ty >= H) return;
            tiles[ty * W + tx] = (byte)t;
            if (notify) OnTileChanged?.Invoke(tx, ty);
        }
        public bool SolidAt(int tx, int ty) => GameData.Solid(Get(tx, ty));
        public bool LiquidAt(int tx, int ty) => Get(tx, ty) == Tile.Water;

        public int SurfaceAt(int tx)
        {
            for (int y = 0; y < H; y++) if (SolidAt(tx, y)) return y;
            return H - 1;
        }

        // ── Unity-space helpers (y up) ──
        public int GridY(float unityY) => H - 1 - Mathf.FloorToInt(unityY);
        public float UnityY(int ty) => H - 1 - ty;                        // bottom edge of cell
        public bool SolidAtUnity(float x, float y) => SolidAt(Mathf.FloorToInt(x), GridY(y));
        public bool LiquidAtUnity(float x, float y) => LiquidAt(Mathf.FloorToInt(x), GridY(y));

        // ══════════════ GENERATION (port of web world.js) ══════════════
        public void Generate(uint seed)
        {
            var rng = new Rng(seed);
            System.Array.Clear(tiles, 0, tiles.Length);
            templeSpots.Clear();

            // 1. surface heightmap
            var raw = new float[W];
            float ph1 = rng.Next() * 99, ph2 = rng.Next() * 99, ph3 = rng.Next() * 99;
            for (int x = 0; x < W; x++)
            {
                string b = GameData.BiomeAt(x);
                float baseH = 108, amp = 8;
                if (b == "desert") { baseH = 106; amp = 10; }
                if (b == "tropic") { baseH = 104; amp = 12; }
                if (b == "ocean") { baseH = 148; amp = 6; }
                if (b == "lantern") { baseH = 105; amp = 8; }
                raw[x] = baseH
                    + Mathf.Sin(x * 0.045f + ph1) * amp
                    + Mathf.Sin(x * 0.11f + ph2) * amp * 0.4f
                    + Mathf.Sin(x * 0.021f + ph3) * amp * 0.7f;
            }
            // ocean islands
            int[] islands = { 665 + rng.Range(0, 30), 780 + rng.Range(0, 40) };
            foreach (int ix in islands)
                for (int x = ix - 14; x < ix + 14; x++)
                {
                    if (x < 610 || x >= 890) continue;
                    float d = Mathf.Abs(x - ix) / 14f;
                    raw[x] = Mathf.Min(raw[x], GameData.SeaLevel - 4 + d * d * 52);
                }
            for (int pass = 0; pass < 3; pass++)
                for (int x = 1; x < W - 1; x++)
                    raw[x] = (raw[x - 1] + raw[x] * 2 + raw[x + 1]) / 4;
            for (int x = 0; x < W; x++) heights[x] = Mathf.RoundToInt(raw[x]);

            // 2. strata + ocean water
            for (int x = 0; x < W; x++)
            {
                string b = GameData.BiomeAt(x);
                int surf = heights[x];
                for (int y = surf; y < H; y++)
                {
                    int depth = y - surf;
                    Tile t;
                    if (b == "desert") t = depth < 7 ? Tile.Sand : depth < 26 ? Tile.Sandstone : Tile.Stone;
                    else if (b == "ocean") t = depth < 5 ? Tile.Sand : depth < 14 ? Tile.Clay : Tile.Stone;
                    else t = depth == 0 ? Tile.Grass : depth < 8 ? Tile.Dirt : Tile.Stone;
                    Set(x, y, t, false);
                }
                // fill the sea — including the border shoulders that the terrain
                // smoothing pulls below sea level, so the shoreline meets the water
                if (b == "ocean" || (x >= 555 && x < 945))
                    for (int y = GameData.SeaLevel; y < surf; y++) Set(x, y, Tile.Water, false);
            }
            for (int x = 570; x < 630; x++) PaintSurface(x, Tile.Sand, 4);
            for (int x = 880; x < 930; x++) PaintSurface(x, Tile.Sand, 3);

            // 3. caves
            for (int i = 0; i < 26; i++)
            {
                float cx = rng.Next() * W, cy = 130 + rng.Next() * 70, ang = rng.Next() * Mathf.PI * 2;
                int steps = 60 + rng.Range(0, 120);
                for (int s = 0; s < steps; s++)
                {
                    Carve((int)cx, (int)cy, 1.6f + rng.Next() * 1.8f);
                    ang += (rng.Next() - 0.5f) * 0.9f;
                    cx += Mathf.Cos(ang) * 2; cy += Mathf.Sin(ang) * 1.2f;
                    cy = Mathf.Clamp(cy, 118, H - 6);
                }
            }

            // 4. ores
            ScatterOre(rng, Tile.Copper, 300, 600, 90, 118, 200);
            ScatterOre(rng, Tile.Copper, 0, 1200, 130, 200, 260);
            ScatterOre(rng, Tile.GoldOre, 0, 300, 115, 195, 170);
            ScatterOre(rng, Tile.Jade, 900, 1200, 120, 200, 170);
            ScatterOre(rng, Tile.Jade, 600, 900, 160, 205, 60);

            // 5. decoration
            int lastTreeX = -9;
            for (int x = 2; x < W - 2; x++)
            {
                string b = GameData.BiomeAt(x);
                int surf = heights[x];
                Tile ground = Get(x, surf);
                if (!GameData.Solid(ground)) continue;
                int above = surf - 1;
                Tile aboveT = Get(x, above);
                if (aboveT != Tile.Air && aboveT != Tile.Water) continue;

                if (b == "desert" && ground == Tile.Sand && rng.Chance(0.045f)) GrowCactus(rng, x, above);
                if (b == "tropic" && ground == Tile.Grass)
                {
                    if (rng.Chance(0.09f) && x - lastTreeX >= 4) { GrowTree(x, above, 4 + rng.Range(0, 4)); lastTreeX = x; }
                    else if (rng.Chance(0.06f)) Set(x, above, Tile.Flower, false);
                }
                if (b == "ocean")
                {
                    if (aboveT == Tile.Water && ground == Tile.Sand)
                    {
                        if (rng.Chance(0.10f)) GrowKelp(rng, x, above);
                        else if (rng.Chance(0.06f)) GrowCoral(rng, x, surf);
                        else if (rng.Chance(0.05f)) Set(x, above, Tile.Shell, false);
                    }
                    if (aboveT == Tile.Air && ground == Tile.Sand && rng.Chance(0.15f) && x - lastTreeX >= 5)
                    { GrowTree(x, above, 5 + rng.Range(0, 3)); lastTreeX = x; }
                }
                if (b == "lantern" && ground == Tile.Grass)
                {
                    if (rng.Chance(0.08f)) GrowBamboo(rng, x, above);
                    else if (rng.Chance(0.05f)) Set(x, above, Tile.Flower, false);
                    else if (rng.Chance(0.03f) && x - lastTreeX >= 4) { GrowTree(x, above, 3 + rng.Range(0, 3)); lastTreeX = x; }
                }
            }

            // 6. structures
            BuildRuin(rng, 60 + rng.Range(0, 60));
            BuildRuin(rng, 180 + rng.Range(0, 60));
            BuildTemple(rng, 950 + rng.Range(0, 40));
            BuildTemple(rng, 1060 + rng.Range(0, 60));

            // 7. spawn (tropic) — pick a column with clear sky above the surface,
            // so you don't wake up inside a tree
            int sx = 450;
            bool found = false;
            for (int x = 438; x < 520 && !found; x++)
            {
                int surf = SurfaceAt(x);
                bool clear = true;
                for (int dy = 1; dy <= 5 && clear; dy++)
                    if (Get(x, surf - dy) != Tile.Air) clear = false;
                // margin: no trunk hugging either shoulder
                if (clear && Get(x - 1, surf - 1) != Tile.Trunk && Get(x + 1, surf - 1) != Tile.Trunk)
                { sx = x; found = true; }
            }
            if (!found)
            {
                int surf = SurfaceAt(sx);
                for (int dy = 1; dy <= 5; dy++) Set(sx, surf - dy, Tile.Air, false);
            }
            spawn = new Vector2Int(sx, SurfaceAt(sx) - 3);
        }

        void PaintSurface(int x, Tile tile, int depth)
        {
            int surf = heights[x];
            for (int y = surf; y < surf + depth; y++)
                if (SolidAt(x, y)) Set(x, y, tile, false);
        }
        void Carve(int cx, int cy, float r)
        {
            for (int y = (int)(cy - r); y <= cy + r; y++)
                for (int x = (int)(cx - r); x <= cx + r; x++)
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r && Get(x, y) != Tile.Water)
                        Set(x, y, Tile.Air, false);
        }
        void ScatterOre(Rng rng, Tile ore, int x0, int x1, int y0, int y1, int count)
        {
            for (int i = 0; i < count; i++)
            {
                int x = x0 + rng.Range(0, x1 - x0), y = y0 + rng.Range(0, y1 - y0);
                for (int j = 0; j < 4; j++)
                {
                    int ox = x + rng.Range(0, 3) - 1, oy = y + rng.Range(0, 3) - 1;
                    Tile cur = Get(ox, oy);
                    if (cur == Tile.Stone || cur == Tile.Sandstone) Set(ox, oy, ore, false);
                }
            }
        }
        void GrowTree(int x, int topY, int height)
        {
            for (int i = 0; i < height; i++) Set(x, topY - i, Tile.Trunk, false);
            int ly = topY - height;
            (int dy, int r)[] rows = { (-3, 1), (-2, 2), (-1, 3), (0, 2) };
            foreach (var (dy, r) in rows)
                for (int dx = -r; dx <= r; dx++)
                    if (Get(x + dx, ly + dy) == Tile.Air) Set(x + dx, ly + dy, Tile.Leaves, false);
        }
        void GrowCactus(Rng rng, int x, int topY)
        {
            int height = 1 + rng.Range(0, 3);
            for (int i = 0; i < height; i++) Set(x, topY - i, Tile.Cactus, false);
        }
        void GrowKelp(Rng rng, int x, int bottomY)
        {
            int height = 2 + rng.Range(0, 4);
            for (int i = 0; i < height; i++)
            {
                if (Get(x, bottomY - i) != Tile.Water) break;
                Set(x, bottomY - i, Tile.Kelp, false);
            }
        }
        void GrowCoral(Rng rng, int x, int surfY)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = 0; dy <= 1; dy++)
                    if (rng.Chance(0.6f) && Get(x + dx, surfY - dy) == Tile.Water)
                        Set(x + dx, surfY - dy, Tile.Coral, false);
        }
        void GrowBamboo(Rng rng, int x, int topY)
        {
            int height = 3 + rng.Range(0, 5);
            for (int i = 0; i < height; i++)
            {
                if (Get(x, topY - i) != Tile.Air) break;
                Set(x, topY - i, Tile.Bamboo, false);
            }
        }
        void BuildRuin(Rng rng, int cx)
        {
            int surf = heights[cx];
            int wdt = 8 + rng.Range(0, 5);
            for (int dx = -wdt / 2; dx <= wdt / 2; dx++)
            {
                int colH = 2 + rng.Range(0, 4);
                for (int i = 0; i < colH; i++)
                    if (rng.Chance(0.75f)) Set(cx + dx, surf - 1 - i + rng.Range(0, 2), Tile.Sandstone, false);
            }
        }
        void BuildTemple(Rng rng, int cx)
        {
            int surf = Mathf.Min(heights[cx - 8], Mathf.Min(heights[cx + 8], heights[cx]));
            const int half = 8;
            for (int x = cx - half; x <= cx + half; x++)
            {
                for (int y = surf; y < surf + 6; y++) if (!SolidAt(x, y)) Set(x, y, Tile.Dirt, false);
                for (int y = surf - 12; y < surf; y++) if (Get(x, y) != Tile.Air) Set(x, y, Tile.Air, false);
                heights[x] = surf;
            }
            int floor = surf - 1;
            for (int x = cx - half; x <= cx + half; x++) Set(x, floor + 1, Tile.Temple, false);
            foreach (int px in new[] { cx - half + 1, cx + half - 1 })
                for (int i = 0; i < 6; i++) Set(px, floor - i, Tile.Temple, false);
            int roofY = floor - 6;
            for (int x = cx - half - 1; x <= cx + half + 1; x++) Set(x, roofY, Tile.GoldTile, false);
            for (int x = cx - half + 2; x <= cx + half - 2; x++) Set(x, roofY - 1, Tile.Temple, false);
            for (int x = cx - half + 3; x <= cx + half - 3; x++) Set(x, roofY - 2, Tile.GoldTile, false);
            for (int x = cx - 2; x <= cx + 2; x++) Set(x, roofY - 3, Tile.GoldTile, false);
            Set(cx, roofY - 4, Tile.GoldTile, false);
            Set(cx, floor, Tile.GoldTile, false); Set(cx, floor - 1, Tile.GoldTile, false);
            Set(cx - 4, roofY + 1, Tile.Lantern, false);
            Set(cx + 4, roofY + 1, Tile.Lantern, false);
            templeSpots.Add(new Vector2Int(cx, floor));
        }

        // ══════════════ MINING / PLACING ══════════════
        public class MineResult { public bool broken, blocked; public float frac; public string drop; }

        public MineResult Mine(int tx, int ty, float power, int tier)
        {
            Tile id = Get(tx, ty);
            if (id == Tile.Air || id == Tile.Water) return null;
            var d = GameData.Tiles[id];
            if (d.tier > tier) return new MineResult { blocked = true };
            var key = (tx, ty);
            float dmg = (damage.TryGetValue(key, out var cur) ? cur : 0) + power;
            if (dmg >= d.hp)
            {
                damage.Remove(key);
                bool flood = ty >= GameData.SeaLevel && GameData.BiomeAt(tx) == "ocean" &&
                    (Get(tx, ty - 1) == Tile.Water || Get(tx - 1, ty) == Tile.Water || Get(tx + 1, ty) == Tile.Water);
                Set(tx, ty, flood ? Tile.Water : Tile.Air);
                return new MineResult { broken = true, drop = d.drop };
            }
            damage[key] = dmg;
            OnTileChanged?.Invoke(tx, ty);
            return new MineResult { frac = dmg / d.hp };
        }

        public bool CanPlace(int tx, int ty)
        {
            Tile cur = Get(tx, ty);
            if (cur != Tile.Air && cur != Tile.Water) return false;
            return SolidAt(tx - 1, ty) || SolidAt(tx + 1, ty) || SolidAt(tx, ty - 1) || SolidAt(tx, ty + 1);
        }

        // ══════════════ SAVE ══════════════
        public string SerializeTiles()
        {
            var bytes = new List<byte>(40000);
            int run = 1;
            for (int i = 1; i <= tiles.Length; i++)
            {
                if (i < tiles.Length && tiles[i] == tiles[i - 1] && run < 255) run++;
                else { bytes.Add((byte)run); bytes.Add(tiles[i - 1]); run = 1; }
            }
            return System.Convert.ToBase64String(bytes.ToArray());
        }
        public void DeserializeTiles(string b64)
        {
            var data = System.Convert.FromBase64String(b64);
            int pos = 0;
            for (int i = 0; i < data.Length && pos < tiles.Length; i += 2)
            {
                int run = data[i]; byte val = data[i + 1];
                for (int j = 0; j < run && pos < tiles.Length; j++) tiles[pos++] = val;
            }
        }
    }
}
