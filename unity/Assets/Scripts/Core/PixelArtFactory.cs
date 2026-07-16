// ══════════════════════════════════════════════════════════════
// Mushmire — PixelArtFactory.cs
// Every sprite in the game is generated here at runtime:
// tiles (32px, 3 variants), decor overlays, cave walls, clouds,
// characters, enemies, creatures, item icons, UI textures.
// No imported binary assets — the art ships as code.
// ══════════════════════════════════════════════════════════════
using System.Collections.Generic;
using UnityEngine;

namespace Mushmire
{
    public static class PixelArtFactory
    {
        public const int TilePx = 32;                  // tile resolution (web was 16 — doubled for detail)
        public const float PPU = 32f;                  // 1 tile = 1 world unit

        static readonly Dictionary<string, Sprite> cache = new();

        // ── shared palette (port of SPRITE_COLORS) ──
        public static readonly Dictionary<char, Color32> Pal = new()
        {
            ['k'] = C("#1a1216"), ['w'] = C("#f5f0e8"), ['r'] = C("#e05656"), ['o'] = C("#e8933a"),
            ['y'] = C("#f2c14e"), ['g'] = C("#4e9e4a"), ['G'] = C("#2e6b44"), ['b'] = C("#4a7ac9"),
            ['p'] = C("#b48cf2"), ['P'] = C("#7a4fa3"), ['t'] = C("#e2c290"), ['T'] = C("#a8875c"),
            ['n'] = C("#7d5a3e"), ['N'] = C("#5e4025"), ['s'] = C("#8d8d98"), ['S'] = C("#55555e"),
            ['f'] = C("#e8933a"), ['F'] = C("#c9722a"), ['m'] = C("#d98fb5"), ['e'] = C("#8fe0c9"),
            ['h'] = C("#f2ddb0"), ['d'] = C("#3d3550"), ['L'] = C("#a3d468"), ['l'] = C("#8fbf5a"),
            ['q'] = C("#e87b8f"), ['z'] = C("#66d9e8"), ['x'] = C("#c9a468"),
            ['c'] = C("#c2c2cc"),   // light gray (craft icon)
        };

        static Color32 C(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

        // ── texture helpers ──
        public static Texture2D NewTex(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var clear = new Color32[w * h];
            t.SetPixels32(clear);
            return t;
        }

        public static Sprite ToSprite(Texture2D t, float ppu = PPU, float pivotX = 0.5f, float pivotY = 0.5f)
        {
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(pivotX, pivotY), ppu);
        }

        static void Px(Texture2D t, int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= t.width || y >= t.height) return;
            t.SetPixel(x, y, c);
        }

        static void Rect(Texture2D t, int x, int y, int w, int h, Color32 c)
        {
            for (int yy = y; yy < y + h; yy++)
                for (int xx = x; xx < x + w; xx++)
                    Px(t, xx, yy, c);
        }

        static void Blend(Texture2D t, int x, int y, int w, int h, Color c)
        {
            for (int yy = y; yy < y + h; yy++)
                for (int xx = x; xx < x + w; xx++)
                {
                    if (xx < 0 || yy < 0 || xx >= t.width || yy >= t.height) continue;
                    var old = t.GetPixel(xx, yy);
                    t.SetPixel(xx, yy, Color.Lerp(old, new Color(c.r, c.g, c.b, old.a > 0 ? old.a : c.a), c.a));
                }
        }

        // adds a 1px dark outline around opaque pixels — the "finished sprite" look
        public static void Outline(Texture2D t, Color32 col)
        {
            int w = t.width, h = t.height;
            var src = t.GetPixels32();
            var dst = (Color32[])src.Clone();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (src[y * w + x].a > 10) continue;
                    bool nearSolid =
                        (x > 0 && src[y * w + x - 1].a > 10) || (x < w - 1 && src[y * w + x + 1].a > 10) ||
                        (y > 0 && src[(y - 1) * w + x].a > 10) || (y < h - 1 && src[(y + 1) * w + x].a > 10);
                    if (nearSolid) dst[y * w + x] = col;
                }
            t.SetPixels32(dst);
        }

        // string-map art → texture. Rows are top-down; '.' or ' ' = transparent.
        public static Texture2D FromMap(string[] art, int scale = 2, bool outline = true)
        {
            int aw = art[0].Length, ah = art.Length;
            var t = NewTex(aw * scale, ah * scale);
            for (int y = 0; y < ah; y++)
                for (int x = 0; x < aw; x++)
                {
                    char ch = art[y][x];
                    if (ch == '.' || ch == ' ') continue;
                    var col = Pal.TryGetValue(ch, out var c) ? c : new Color32(255, 0, 255, 255);
                    Rect(t, x * scale, (ah - 1 - y) * scale, scale, scale, col);
                }
            if (outline) Outline(t, new Color32(20, 14, 24, 255));
            return t;
        }

        public static Sprite SpriteFromMap(string[] art, int scale = 2, bool outline = true, float ppu = PPU)
        {
            var key = art[0] + art[art.Length - 1] + scale + outline + art.Length;
            if (cache.TryGetValue(key, out var s)) return s;
            s = ToSprite(FromMap(art, scale, outline), ppu);
            cache[key] = s;
            return s;
        }

        // ══════════════ TILE TEXTURES (32px, 3 variants) ══════════════
        static readonly Dictionary<(Tile, int), Sprite> tileSprites = new();

        public static Sprite TileSprite(Tile tile, int variant)
        {
            variant %= 3;
            if (tileSprites.TryGetValue((tile, variant), out var s)) return s;
            s = BuildTileSprite(tile, variant);
            tileSprites[(tile, variant)] = s;
            return s;
        }

        static Sprite BuildTileSprite(Tile tile, int v)
        {
            // bespoke decoration tiles
            string[] map = tile switch
            {
                Tile.Torch => TorchArt, Tile.Lantern => LanternArt, Tile.Flower => FlowerArt,
                Tile.Workbench => BenchArt, Tile.Shell => ShellArt, Tile.Cactus => CactusArt,
                Tile.Kelp => KelpArt, Tile.Bamboo => BambooArt, Tile.Trunk => TrunkArt,
                _ => null,
            };
            if (map != null) return ToSprite(FromMap(map, 2, tile != Tile.Kelp && tile != Tile.Bamboo));

            var d = GameData.Tiles[tile];
            var rng = new Rng((uint)(777 + (int)tile * 131 + v * 977));
            var t = NewTex(TilePx, TilePx);
            Rect(t, 0, 0, TilePx, TilePx, d.baseCol);

            if (tile == Tile.Leaves)
            {
                for (int i = 0; i < 90; i++)
                    Rect(t, rng.Range(0, 31), rng.Range(0, 31), rng.Range(2, 5), 2,
                        rng.Chance(0.5f) ? d.speckle : C("#2e6b38"));
                for (int i = 0; i < 12; i++)
                    Blend(t, rng.Range(0, 30), rng.Range(18, 30), 2, 2, new Color(1, 1, 1, 0.12f));
                Blend(t, 0, 0, TilePx, 4, new Color(0, 0, 0, 0.22f));
                return ToSprite(t);
            }

            // fine speckle at two grain sizes
            for (int i = 0; i < 55; i++)
                Rect(t, rng.Range(0, TilePx), rng.Range(0, TilePx), rng.Chance(0.3f) ? 3 : 2, rng.Chance(0.25f) ? 2 : 1, d.speckle);
            if (!d.brick && !d.ore)
                for (int i = 0; i < 6; i++)
                    Blend(t, rng.Range(1, 26), rng.Range(1, 26), rng.Range(3, 7), rng.Range(2, 4),
                        rng.Chance(0.5f) ? new Color(0, 0, 0, 0.10f) : new Color(1, 1, 1, 0.07f));

            if (d.ore)
            {
                for (int i = 0; i < 6; i++)
                {
                    int ox = rng.Range(3, 25), oy = rng.Range(3, 25);
                    Rect(t, ox, oy, 4, 4, d.extra);
                    Rect(t, ox + 2, oy + 3, 2, 2, d.extra);
                    Rect(t, ox, oy + 3, 2, 1, new Color32(255, 255, 255, 90)); // glint
                }
            }
            if (d.brick)
            {
                var mortar = d.extra;
                Rect(t, 0, 10, TilePx, 2, mortar); Rect(t, 0, 22, TilePx, 2, mortar);
                int o = v * 6;
                Rect(t, (8 + o) % 30, 22, 2, 10, mortar);
                Rect(t, (22 + o) % 30, 12, 2, 10, mortar);
                Rect(t, (12 + o) % 30, 0, 2, 10, mortar);
                // beveled brick highlights
                Blend(t, 0, 24, TilePx, 2, new Color(1, 1, 1, 0.08f));
                Blend(t, 0, 12, TilePx, 2, new Color(1, 1, 1, 0.08f));
            }
            if (d.hasGrassTop)
            {
                Rect(t, 0, TilePx - 6, TilePx, 6, d.grassTop);
                for (int i = 0; i < TilePx; i += 3)
                {
                    if (rng.Chance(0.6f)) Rect(t, i, TilePx - 7, 2, 2, C("#5db858"));
                    if (rng.Chance(0.5f)) Rect(t, i + 1, TilePx - 3, 1, 3, C("#3d8c3e"));
                }
            }
            // soft edge shading baked in
            Blend(t, 0, 0, TilePx, 2, new Color(0, 0, 0, 0.14f));
            Blend(t, TilePx - 2, 0, 2, TilePx, new Color(0, 0, 0, 0.14f));
            Blend(t, 0, TilePx - 2, TilePx, 2, new Color(1, 1, 1, 0.10f));
            return ToSprite(t);
        }

        // ══════════════ DECOR (edges, walls, clouds…) ══════════════
        static readonly Dictionary<string, Sprite> decor = new();

        public static Sprite Decor(string key)
        {
            if (decor.Count == 0) BuildDecor();
            return decor.TryGetValue(key, out var s) ? s : null;
        }

        static void BuildDecor()
        {
            var rng = new Rng(4242);

            Sprite Mk(string key, int w, int h, System.Action<Texture2D> fn, float pivotY = 0.5f)
            {
                var t = NewTex(w, h);
                fn(t);
                var s = ToSprite(t, PPU, 0.5f, pivotY);
                decor[key] = s;
                return s;
            }

            // grass wrap on exposed sides (terraria signature)
            Mk("grassL", TilePx, TilePx, t =>
            {
                Rect(t, 0, 0, 4, TilePx, C("#4e9e4a"));
                for (int y = 0; y < TilePx; y += 3)
                    if (rng.Chance(0.7f)) Rect(t, 4, y, 2, rng.Range(2, 5), C("#5db858"));
                for (int y = 2; y < TilePx; y += 6) Rect(t, 2, y, 2, 2, C("#2e6b44"));
            });
            Mk("grassR", TilePx, TilePx, t =>
            {
                Rect(t, TilePx - 4, 0, 4, TilePx, C("#4e9e4a"));
                for (int y = 0; y < TilePx; y += 3)
                    if (rng.Chance(0.7f)) Rect(t, TilePx - 6, y, 2, rng.Range(2, 5), C("#5db858"));
                for (int y = 2; y < TilePx; y += 6) Rect(t, TilePx - 4, y, 2, 2, C("#2e6b44"));
            });
            Mk("grassB", TilePx, TilePx, t =>
            {
                Rect(t, 0, 0, TilePx, 4, C("#3d8c46"));
                for (int x = 2; x < TilePx; x += 6) if (rng.Chance(0.6f)) Rect(t, x, 4, 2, 2, C("#2e6b44"));
            });
            // shaded edges + sun-kissed top
            Mk("shadeL", TilePx, TilePx, t => { for (int i = 0; i < 10; i++) Blend(t, i, 0, 1, TilePx, new Color(0, 0, 0, 0.24f * (1 - i / 10f))); });
            Mk("shadeR", TilePx, TilePx, t => { for (int i = 0; i < 10; i++) Blend(t, TilePx - 1 - i, 0, 1, TilePx, new Color(0, 0, 0, 0.24f * (1 - i / 10f))); });
            Mk("shadeB", TilePx, TilePx, t => { for (int i = 0; i < 12; i++) Blend(t, 0, i, TilePx, 1, new Color(0, 0, 0, 0.32f * (1 - i / 12f))); });
            Mk("lightTop", TilePx, TilePx, t => { for (int i = 0; i < 10; i++) Blend(t, 0, TilePx - 1 - i, TilePx, 1, new Color(1f, 0.96f, 0.86f, 0.26f * (1 - i / 10f))); });

            // cave walls
            void Wall(string key, string baseC, string spkC)
            {
                for (int v = 0; v < 2; v++)
                {
                    var r2 = new Rng((uint)(99 + key.Length * 17 + v * 313));
                    Mk(key + v, TilePx, TilePx, t =>
                    {
                        Rect(t, 0, 0, TilePx, TilePx, C(baseC));
                        for (int i = 0; i < 30; i++)
                            Rect(t, r2.Range(0, TilePx), r2.Range(0, TilePx), 3, 2, C(spkC));
                        Blend(t, 0, 14, TilePx, 2, new Color(0, 0, 0, 0.22f));
                        Blend(t, r2.Range(0, 16), 16, 2, 16, new Color(0, 0, 0, 0.22f));
                        Blend(t, 16 + r2.Range(0, 14), 0, 2, 14, new Color(0, 0, 0, 0.22f));
                    });
                }
            }
            Wall("wallDirt", "#3a2a1d", "#463322");
            Wall("wallStone", "#32323a", "#3d3d46");
            Wall("wallSand", "#63512f", "#705d38");
            Wall("wallClay", "#452f26", "#523a2e");
            Wall("wallWater", "#17324f", "#1d3d5e");   // deep-sea backdrop behind water

            // clouds
            for (int i = 0; i < 3; i++)
            {
                int w = 56 + i * 16, h = 20 + i * 3, n = 6 + i * 2;
                var r3 = new Rng((uint)(500 + i));
                Mk("cloud" + i, w, h, t =>
                {
                    for (int j = 0; j < n; j++)
                    {
                        int cx = 8 + r3.Range(0, w - 16), cy = h / 2 + r3.Range(-3, 3), rad = 4 + r3.Range(0, h / 2);
                        for (int y = 0; y < h; y++)
                            for (int x = 0; x < w; x++)
                                if ((x - cx) * (x - cx) + (y - cy) * (y - cy) * 3 < rad * rad && y > h / 5)
                                    Px(t, x, y, new Color32(255, 255, 255, 235));
                    }
                });
            }

            // grass tufts, pebbles, tree roots — full-tile canvases with the art
            // at the BOTTOM: tilemaps place sprites by cell center, so custom
            // bottom pivots ride half a tile too high (the "floating grass" bug)
            for (int i = 0; i < 3; i++)
            {
                var r4 = new Rng((uint)(700 + i));
                Mk("tuft" + i, TilePx, TilePx, t =>
                {
                    for (int j = 0; j < 9; j++)
                    {
                        int x = 2 + r4.Range(0, TilePx - 4), hgt = 4 + r4.Range(0, 7);
                        Rect(t, x, 0, 2, hgt, r4.Chance(0.5f) ? C("#5db858") : C("#4e9e4a"));
                    }
                    if (r4.Chance(0.4f)) Rect(t, 4 + r4.Range(0, 24), 8, 2, 2, C("#d98fb5"));
                });
            }
            for (int i = 0; i < 2; i++)
            {
                var r5 = new Rng((uint)(800 + i));
                Mk("pebble" + i, TilePx, TilePx, t =>
                {
                    for (int j = 0; j < 3 + r5.Range(0, 3); j++)
                        Rect(t, 4 + r5.Range(0, 24), r5.Range(0, 4), 4, 2, r5.Chance(0.5f) ? C("#c9a468") : C("#a8875c"));
                });
            }
            Mk("roots", TilePx, TilePx, t =>
            {
                Rect(t, 8, 0, 16, 10, C("#6b4a32"));
                Rect(t, 4, 0, 24, 4, C("#6b4a32"));
                Rect(t, 4, 0, 4, 2, C("#5e4025"));
                Rect(t, 24, 0, 4, 2, C("#5e4025"));
                Rect(t, 14, 2, 2, 6, C("#5e4025"));
            });

            // soft radial glow disc (for sun, moon, torch halo, spores)
            Mk("glow", 64, 64, t =>
            {
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 64; x++)
                    {
                        float dd = Vector2.Distance(new(x, y), new(32, 32)) / 32f;
                        if (dd < 1) Px(t, x, y, new Color(1, 1, 1, Mathf.Pow(1 - dd, 2f)));
                    }
            });
            Mk("disc", 32, 32, t =>
            {
                for (int y = 0; y < 32; y++)
                    for (int x = 0; x < 32; x++)
                        if (Vector2.Distance(new(x, y), new(16, 16)) < 15) Px(t, x, y, Color.white);
            });
            Mk("pixel", 4, 4, t => Rect(t, 0, 0, 4, 4, Color.white));
            // vertical white→transparent gradient (sky tinting)
            Mk("vgrad", 4, 128, t =>
            {
                for (int y = 0; y < 128; y++)
                    Rect(t, 0, y, 4, 1, new Color(1, 1, 1, y / 127f));
            });
        }

        // water: animation frames — a rolling waterline on surface tiles (wave
        // height varies per frame + per column phase), faint shimmer below
        static readonly Dictionary<(int, bool), Sprite[]> waterFrames = new();
        public static Sprite[] WaterFrames(int band, bool surface)
        {
            band = Mathf.Clamp(band, 0, 3);
            if (waterFrames.TryGetValue((band, surface), out var cached)) return cached;
            float d = band / 3f;
            var water = new Color((46 - d * 20) / 255f, (100 - d * 40) / 255f, (170 - d * 50) / 255f, 0.74f + d * 0.18f);
            int n = surface ? 8 : 4;
            var frames = new Sprite[n];
            for (int f = 0; f < n; f++)
            {
                var t = NewTex(TilePx, TilePx);
                if (surface)
                {
                    // opaque body premixed with the deep backdrop; wavy transparent top
                    Color deep = C("#17324f");
                    var body = Color.Lerp(deep, new Color(water.r, water.g, water.b, 1f), water.a);
                    var crest = new Color(0.78f, 0.92f, 1f, 0.92f);
                    for (int x = 0; x < TilePx; x++)
                    {
                        int h = 2 + Mathf.RoundToInt((Mathf.Sin((x / (float)TilePx + f / (float)n) * Mathf.PI * 2) + 1f) * 1.5f);
                        int top = TilePx - 1 - h;
                        for (int y = 0; y <= top; y++) Px(t, x, y, body);
                        Px(t, x, top, crest);
                        Px(t, x, top - 1, crest);
                    }
                }
                else
                {
                    Rect(t, 0, 0, TilePx, TilePx, water);
                    // drifting shimmer specks
                    var rng = new Rng((uint)(555 + band * 31 + f * 7));
                    for (int i = 0; i < 5; i++)
                        Blend(t, rng.Range(1, TilePx - 4), rng.Range(1, TilePx - 2), 3, 1, new Color(1, 1, 1, 0.08f));
                }
                frames[f] = ToSprite(t);
            }
            waterFrames[(band, surface)] = frames;
            return frames;
        }

        // 9-sliceable rounded rectangle (the web version's border-radius look)
        static Sprite pillSprite;
        public static Sprite Pill()
        {
            if (pillSprite != null) return pillSprite;
            const int S = 24, R = 9;
            var t = NewTex(S, S);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = Mathf.Max(Mathf.Abs(x - (S - 1) / 2f) - ((S - 1) / 2f - R), 0);
                    float dy = Mathf.Max(Mathf.Abs(y - (S - 1) / 2f) - ((S - 1) / 2f - R), 0);
                    if (dx * dx + dy * dy <= R * R + 0.5f) Px(t, x, y, Color.white);
                }
            t.Apply();
            pillSprite = Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(11, 11, 11, 11));
            return pillSprite;
        }

        // ══════════════ CHARACTERS ══════════════
        // frames: 0 idle · 1/2 walk · 3 duck · 4 jump
        public static Sprite CharacterSprite(CharacterLook look, int frame)
        {
            string key = $"char{look.preset}{ColorUtility.ToHtmlStringRGB(look.hair)}{ColorUtility.ToHtmlStringRGB(look.skin)}{ColorUtility.ToHtmlStringRGB(look.top)}{ColorUtility.ToHtmlStringRGB(look.legs)}f{frame}";
            if (cache.TryGetValue(key, out var s)) return s;

            bool duck = frame == 3;
            int H = duck ? 28 : 44, W = 24;                 // 2px art scale on 12×22 design
            var t = NewTex(W, H);
            Color32 hair = look.hair, skin = look.skin, top = look.top, legs = look.legs;
            int step = frame == 1 ? 2 : frame == 2 ? -2 : 0;
            if (frame == 4) step = 3;

            int legH = duck ? 6 : 16;
            // legs + boots
            Rect(t, 4 + step, 0, 6, legH, legs);
            Rect(t, 14 - step, 0, 6, legH, legs);
            Rect(t, 4 + step, 0, 6, 4, new Color32(40, 30, 40, 255));
            Rect(t, 14 - step, 0, 6, 4, new Color32(40, 30, 40, 255));
            // torso
            int torsoH = duck ? 8 : 14, torsoY = legH;
            Rect(t, 3, torsoY, 18, torsoH, top);
            Blend(t, 3, torsoY, 18, 2, new Color(0, 0, 0, 0.16f));
            // back arm
            Rect(t, 1, torsoY + 2, 4, 10 - (duck ? 4 : 0), top);
            // head
            int headY = torsoY + torsoH;
            Rect(t, 4, headY, 16, 14, skin);
            // hair cap
            Rect(t, 4, headY + 10, 16, 6, hair);
            Rect(t, 2, headY + 4, 4, 10, hair);
            if (look.preset == "mei")
            {
                Rect(t, 0, duck ? 4 : headY - 24, 4, duck ? headY + 6 : 26, hair);  // long hair down back
                Rect(t, 19, headY + 6, 2, 6, hair);                                  // front strand
            }
            else
            {
                Rect(t, 3, headY + 14, 6, 5, hair);                                  // top-knot bun
            }
            // face
            Rect(t, 15, headY + 5, 3, 3, new Color32(36, 26, 26, 255));
            Rect(t, 10, headY + 5, 3, 3, new Color32(36, 26, 26, 255));
            Blend(t, 17, headY + 2, 3, 2, new Color(0.91f, 0.48f, 0.56f, 0.5f));     // blush
            Blend(t, 5, headY + 13, 6, 2, new Color(1, 1, 1, 0.22f));                // hair shine
            // front arm
            Rect(t, 17 + (frame == 4 ? 2 : step / 2), torsoY + 2, 4, 10 - (duck ? 4 : 0), skin);

            Outline(t, new Color32(20, 14, 24, 255));
            var sp = ToSprite(t, PPU, 0.5f, 0f);       // pivot at feet
            cache[key] = sp;
            return sp;
        }

        // ══════════════ ART MAPS (ported from web data.js) ══════════════
        public static readonly Dictionary<string, string[]> EnemyArt = new()
        {
            ["duneShade"] = new[] {
                "....tttt....", "...tttttt...", "..tthtthtt..", "..tthtthtt..",
                "..tttttttt..", "...tttttt...", "..tttttttt..", ".t.tttttt.t.",
                "...t.tt.t...", "....t..t....",
            },
            ["scarab"] = new[] {
                "............", "............", "............", "...kk..kk...",
                "..kxxxxxxk..", ".kxxkxxkxxk.", ".kxxxxxxxxk.", "..kxxxxxxk..",
                ".k.k.kk.k.k.", "............",
            },
            ["stalker"] = new[] {
                "............", ".kk.........", ".kkk....kkk.", ".kpkkkkkkkk.",
                ".kkkkkkkkkk.", "..kkkkkkkk..", "..kk....kk..", "..k......k..",
                "............", "............",
            },
            ["sporeling"] = new[] {
                "....pppp....", "..pppppppp..", ".pphpppphpp.", ".pppppppppp.",
                "...whhhhw...", "...whkkhw...", "...whhhhw...", "....w..w....",
                "............", "............",
            },
            ["tideWisp"] = new[] {
                "....zzzz....", "...zzzzzz...", "..zzwzzwzz..", "..zzzzzzzz..",
                "...zzzzzz...", "..zzzzzzzz..", "...z.zz.z...", "....z..z....",
                "......z.....", "............",
            },
            ["crabby"] = new[] {
                "............", "............", ".qq......qq.", ".q..q..q..q.",
                "..qqqqqqqq..", ".qqkqqqqkqq.", "..qqqqqqqq..", "..q.q..q.q..",
                "............", "............",
            },
            ["hungryGhost"] = new[] {
                "....eeee....", "...eeeeee...", "..eekeekee..", "..eeeeeeee..",
                "..eee..eee..", "..eeeeeeee..", "...eeeeee...", "..ee.ee.ee..",
                "...e.ee.e...", "....e..e....",
            },
            ["paperLantern"] = new[] {
                ".....kk.....", "....rrrr....", "...ryyyyr...", "...ryhhyr...",
                "...rykkyr...", "...ryhhyr...", "...ryyyyr...", "....rrrr....",
                ".....yy.....", "............",
            },
        };

        public static readonly Dictionary<string, string[]> CreatureArt = new()
        {
            ["cat"] = new[] {
                "..............", ".k.k..........", ".kkk..........", ".kok.......kk.",
                ".kkkkkkkkkkk..", ".kkkkkkkkkk...", ".kkkkkkkkkk...", ".k..k..k..k...",
            },
            ["catOrange"] = new[] {
                "..............", ".f.f..........", ".fff..........", ".fkf.......ff.",
                ".fffffffffff..", ".ffwwwwwfff...", ".ffwwwwwfff...", ".f..f..f..f...",
            },
            ["firefly"] = new[] { ".ww.", "wyyw", "wyyw", ".ww." },
            ["scarabPet"] = new[] {
                "...kk..kk...", "..kyyyyyyk..", ".kyykyykyyk.", "..kyyyyyyk..",
                ".k.k.kk.k.k.", "............",
            },
            ["spriteWisp"] = new[] { "..pp..", ".phhp.", ".phhp.", "..pp..", ".p..p." },
            ["fishy"] = new[] { "........", "..zzz..z", ".zkzzzzz", "..zzz..z", "........" },
        };

        static readonly string[] TorchArt = {
            "................", "................", "................", "......yy........",
            "......yy........", ".....yffy.......", ".....yffy.......", "......ff........",
            "......nn........", "......nn........", "......nn........", "......nn........",
            "......nn........", "......nn........", "................", "................",
        };
        static readonly string[] LanternArt = {
            "................", "......NN........", ".....qqqq.......", "....qyyyyq......",
            "....qyhhyq......", "....qyhhyq......", "....qyyyyq......", ".....qqqq.......",
            "......yy........", "................", "................", "................",
            "................", "................", "................", "................",
        };
        static readonly string[] FlowerArt = {
            "................", "................", "................", "................",
            "................", "......m.........", ".....mpm........", "....mphpm.......",
            ".....mpm........", "......m.........", "......G.........", "......G.........",
            ".....GG.........", "......G.........", "................", "................",
        };
        static readonly string[] BenchArt = {
            "................", "................", "................", "................",
            "................", "................", "................", "................",
            "nnnnnnnnnnnnnnnn", "nNNNNNNNNNNNNNNn", ".nn..........nn.", ".nn..........nn.",
            ".nn..........nn.", ".nn..........nn.", ".nn..........nn.", "................",
        };
        static readonly string[] ShellArt = {
            "................", "................", "................", "................",
            "................", "................", "................", "................",
            "................", "......mm........", ".....mhhm.......", "....mhwhhm......",
            "....mhhwhm......", ".....mmmm.......", "................", "................",
        };
        static readonly string[] CactusArt = {
            "................", ".....gg.........", ".....gg.........", ".....gg.........",
            ".gg..gg..gg.....", ".gg..gg..gg.....", ".gg..gg..gg.....", ".gggggg..gg.....",
            ".....gggggg.....", ".....gg.........", ".....gg.........", ".....gg.........",
            ".....gg.........", ".....gg.........", ".....gg.........", ".....gg.........",
        };
        static readonly string[] KelpArt = {
            "......G.........", ".....GG.........", ".....G..........", "......G.........",
            "......GG........", ".....GG.........", ".....G..........", "......G.........",
            "......GG........", ".....GG.........", ".....G..........", "......G.........",
            "......GG........", ".....GG.........", "......G.........", "......G.........",
        };
        static readonly string[] BambooArt = {
            ".....ll.........", ".....ll.....L...", ".....llLL.......", ".....ll.........",
            "....Llll........", ".....ll.........", ".....ll.........", ".....llL........",
            ".....ll..L......", "....Lll.........", ".....ll.........", ".....ll.........",
            ".....llL........", ".....ll.........", "....Lll.........", ".....ll.........",
        };
        static readonly string[] TrunkArt = {
            ".....nnnnnn.....", ".....nNnnnn.....", ".....nnnnNn.....", ".....nnnnnn.....",
            ".....nNnnnn.....", ".....nnnnnn.....", ".....nnnNnn.....", ".....nnnnnn.....",
            ".....nnnnnN.....", ".....nNnnnn.....", ".....nnnnnn.....", ".....nnnNnn.....",
            ".....nnnnnn.....", ".....nnnnnn.....", ".....nNnnnn.....", ".....nnnnnn.....",
        };

        // ══════════════ ITEM ICONS ══════════════
        public static Sprite ItemIcon(string id)
        {
            string key = "icon:" + id;
            if (cache.TryGetValue(key, out var s)) return s;

            if (!GameData.Items.TryGetValue(id, out var def)) return null;
            Sprite result;
            if (def.type == ItemType.Block)
                result = TileSprite(def.tile, 0);
            else if (def.type == ItemType.Tool)
                result = SpriteFromMap(PickArt(TierChar(id)), 2);
            else if (def.type == ItemType.Weapon)
                result = SpriteFromMap(SwordArt(TierChar(id)), 2);
            else if (id == "cat_charm") result = SpriteFromMap(CreatureArt["catOrange"], 2);
            else if (id == "scarab_charm") result = SpriteFromMap(CreatureArt["scarabPet"], 2);
            else if (id == "sprite_charm") result = SpriteFromMap(CreatureArt["spriteWisp"], 4);
            else result = MatIcon(id);
            cache[key] = result;
            return result;
        }

        static char TierChar(string id) =>
            id.StartsWith("wood") ? 'n' : id.StartsWith("stone") ? 's' : id.StartsWith("copper") ? 'F' : 'G';

        static string[] PickArt(char c) => new[] {
            "................",
            $"....{c}{c}{c}{c}{c}{c}......",
            $"...{c}......{c}.....",
            $"...{c}.......{c}....",
            "..........nn....", ".........nn.....", "........nn......", ".......nn.......",
            "......nn........", ".....nn.........", "....nn..........", "...nn...........",
            "..nn............", ".nn.............", "................", "................",
        };
        static string[] SwordArt(char c) => new[] {
            "................",
            $".............{c}{c}.",
            $"............{c}{c}{c}.",
            $"...........{c}{c}{c}..",
            $"..........{c}{c}{c}...",
            $".........{c}{c}{c}....",
            $"........{c}{c}{c}.....",
            $".......{c}{c}{c}......",
            $"......{c}{c}{c}.......",
            "...NNNNNNN......",   // crossguard
            "....nnn.........",   // grip
            "...nnn..........",
            "..nnn...........",
            "..NN............",   // pommel
            "................",
            "................",
        };

        static Sprite MatIcon(string id)
        {
            var t = NewTex(16, 16);
            void Blob(string col, string hi)
            {
                for (int y = 0; y < 16; y++)
                    for (int x = 0; x < 16; x++)
                        if (Vector2.Distance(new(x, y), new(8, 7)) < 5.2f) Px(t, x, y, C(col));
                Rect(t, 6, 8, 2, 2, C(hi));
            }
            switch (id)
            {
                case "wood": Rect(t, 3, 4, 10, 7, C("#7d5a3e")); Rect(t, 3, 6, 10, 1, C("#5e4025")); Rect(t, 3, 9, 10, 1, C("#5e4025")); break;
                case "kelp": Rect(t, 7, 2, 2, 12, C("#2e7d5b")); Rect(t, 5, 10, 2, 2, C("#3d9c72")); Rect(t, 9, 6, 2, 2, C("#3d9c72")); break;
                case "cactus": Blob("#4e8c4a", "#6db868"); break;
                case "petal": Blob("#d98fb5", "#f2ddb0"); break;
                case "bamboo": Rect(t, 6, 2, 3, 12, C("#8fbf5a")); Rect(t, 6, 6, 3, 1, C("#6b9c3e")); Rect(t, 6, 10, 3, 1, C("#6b9c3e")); break;
                case "shell": Blob("#d98fb5", "#f5f0e8"); break;
                case "copper": Blob("#b5643a", "#c97b4e"); break;
                case "goldore": Blob("#d9a833", "#f2c14e"); break;
                case "jade": Blob("#3aa86e", "#4ec98a"); break;
                case "essence": Blob("#b48cf2", "#e0ccff"); break;
                case "salve": Rect(t, 5, 2, 6, 8, C("#e05656")); Rect(t, 6, 7, 2, 2, C("#f2a0b0")); Rect(t, 6, 10, 4, 2, C("#a8875c")); break;
                default: Blob("#8d8d98", "#c0c0c8"); break;
            }
            Outline(t, new Color32(20, 14, 24, 255));
            return ToSprite(t);
        }
    }

    [System.Serializable]
    public class CharacterLook
    {
        public string preset = "mei";
        public Color hair = GameData.Hex("#1c1216");
        public Color skin = GameData.Hex("#e8b98a");
        public Color top = GameData.Hex("#7a4fa3");
        public Color legs = GameData.Hex("#3d3550");
        public string name = "";
    }
}
