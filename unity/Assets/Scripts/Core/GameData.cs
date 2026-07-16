// ══════════════════════════════════════════════════════════════
// Mushmire — GameData.cs
// Tiles, items, recipes, biomes, enemies, deterministic RNG.
// Direct port of the web version's data.js (minus rendering).
// ══════════════════════════════════════════════════════════════
using System.Collections.Generic;
using UnityEngine;

namespace Mushmire
{
    // ── deterministic RNG (mulberry32 port — keeps world-gen parity with web builds) ──
    public class Rng
    {
        uint a;
        public Rng(uint seed) { a = seed; }
        public float Next()
        {
            unchecked
            {
                a += 0x6D2B79F5u;
                uint t = a;
                t = (t ^ (t >> 15)) * (1u | t);
                t ^= t + (t ^ (t >> 7)) * (61u | t);
                return ((t ^ (t >> 14)) / 4294967296f);
            }
        }
        public int Range(int minIncl, int maxExcl) => minIncl + (int)(Next() * (maxExcl - minIncl));
        public bool Chance(float p) => Next() < p;
    }

    public enum Tile : byte
    {
        Air = 0, Dirt, Grass, Sand, Sandstone, Stone, Trunk, Leaves, Water,
        Planks, Clay, Coral, Kelp, Temple, GoldTile, Jade, Copper, GoldOre,
        Torch, Workbench, Cactus, Flower, Bamboo, Lantern, Shell,
    }

    public class TileDef
    {
        public string name;
        public bool solid, liquid;
        public int hp = 1;              // pickaxe hits to break
        public int tier;                // min pickaxe tier
        public string drop;             // item id or null
        public Color32 baseCol, speckle, extra;   // extra = ore vein / brick mortar / grass top
        public float light;             // 0 = none; >0 emits (radius scale)
        public bool ore, brick;
        public Color32 grassTop;
        public bool hasGrassTop;
    }

    public class ItemDef
    {
        public string id, name;
        public ItemType type;
        public Tile tile = Tile.Air;    // for blocks
        public int tier;                // pickaxe gate tier
        public float power;             // mining speed
        public int dmg = 1;
        public int heal;
        public string creature;         // for charms
        public int stack = 99;
    }

    public enum ItemType { Block, Tool, Weapon, Mat, Use, Charm }

    public class Recipe
    {
        public string output; public int count = 1;
        public (string id, int n)[] cost;
        public bool bench;
        public string category;
    }

    public class EnemyDef
    {
        public string id, name, biome;
        public int hp, dmg;
        public float speed;
        public bool fly;
        public string drop; public int dropN = 1;
    }

    public static class GameData
    {
        public const int WorldW = 1200, WorldH = 220, SeaLevel = 104;

        public static readonly Dictionary<Tile, TileDef> Tiles = new();
        public static readonly Dictionary<string, ItemDef> Items = new();
        public static readonly List<Recipe> Recipes = new();
        public static readonly Dictionary<string, EnemyDef> Enemies = new();

        // theme palette — mirrors the web game exactly (per Christine: keep these)
        public static readonly Color Ink = Hex("#1a1226");
        public static readonly Color Ink2 = Hex("#241a36");
        public static readonly Color Mist = Hex("#4a3a68");
        public static readonly Color SandCol = Hex("#e2c290");
        public static readonly Color SandBright = Hex("#f2ddb0");
        public static readonly Color SandDim = Hex("#a8875c");
        public static readonly Color Glow = Hex("#b48cf2");
        public static readonly Color Blood = Hex("#e05656");

        public static Color Hex(string h)
        {
            ColorUtility.TryParseHtmlString(h, out var c);
            return c;
        }
        static Color32 H32(string h) => (Color32)Hex(h);

        public static string BiomeAt(int tx) =>
            tx < 300 ? "desert" : tx < 600 ? "tropic" : tx < 900 ? "ocean" : "lantern";

        public static string BiomeLabel(string key) => key switch
        {
            "desert" => "Sunken Dunes",
            "tropic" => "Verdant Tropics",
            "ocean" => "Glass Ocean",
            _ => "Lantern Coast",
        };

        static bool _built;
        public static void Build()
        {
            if (_built) return;
            _built = true;
            BuildTiles();
            BuildItems();
            BuildRecipes();
            BuildEnemies();
        }

        static void TileEntry(Tile t, string name, bool solid, int hp, int tier, string drop,
            string baseC, string spkC, float light = 0, bool ore = false, bool brick = false,
            string extraC = null, string grassTop = null, bool liquid = false)
        {
            Tiles[t] = new TileDef
            {
                name = name, solid = solid, liquid = liquid, hp = hp, tier = tier, drop = drop,
                baseCol = baseC != null ? H32(baseC) : default,
                speckle = spkC != null ? H32(spkC) : default,
                extra = extraC != null ? H32(extraC) : default,
                light = light, ore = ore, brick = brick,
                hasGrassTop = grassTop != null,
                grassTop = grassTop != null ? H32(grassTop) : default,
            };
        }

        static void BuildTiles()
        {
            TileEntry(Tile.Air, "air", false, 1, 0, null, null, null);
            TileEntry(Tile.Water, "Water", false, 1, 0, null, null, null, liquid: true);
            TileEntry(Tile.Dirt, "Dirt", true, 2, 0, "dirt", "#6b4a32", "#7d5a3e");
            TileEntry(Tile.Grass, "Grass", true, 2, 0, "dirt", "#6b4a32", "#7d5a3e", grassTop: "#4e9e4a");
            TileEntry(Tile.Sand, "Sand", true, 2, 0, "sand", "#e2c290", "#d1ad74");
            TileEntry(Tile.Sandstone, "Sandstone", true, 4, 0, "sandstone", "#c9a468", "#b58e52", brick: true, extraC: "#a8875c");
            TileEntry(Tile.Stone, "Stone", true, 4, 0, "stone", "#7a7a85", "#8d8d98");
            TileEntry(Tile.Trunk, "Tree", false, 3, 0, "wood", "#7d5a3e", "#6b4a32");
            TileEntry(Tile.Leaves, "Leaves", false, 1, 0, null, "#3d8c46", "#4ea355");
            TileEntry(Tile.Planks, "Planks", true, 3, 0, "planks", "#a3764a", "#8f6539", brick: true, extraC: "#8f6539");
            TileEntry(Tile.Clay, "Mud", true, 2, 0, "clay", "#8a5f4d", "#79523f");
            TileEntry(Tile.Coral, "Coral", true, 3, 0, "coral", "#e87b8f", "#f2a0b0");
            TileEntry(Tile.Kelp, "Kelp", false, 1, 0, "kelp", "#2e7d5b", "#3d9c72");
            TileEntry(Tile.Temple, "Temple Brick", true, 5, 0, "temple_brick", "#9c8a6e", "#8a785c", brick: true, extraC: "#7d6c52");
            TileEntry(Tile.GoldTile, "Gilded Tile", true, 5, 0, "gold_tile", "#d9a833", "#f2c14e", light: 0.35f, brick: true, extraC: "#b8891f");
            TileEntry(Tile.Jade, "Jade Ore", true, 7, 2, "jade", "#7a7a85", "#4ec98a", ore: true, extraC: "#3aa86e");
            TileEntry(Tile.Copper, "Copper Ore", true, 5, 1, "copper", "#7a7a85", "#c97b4e", ore: true, extraC: "#b5643a");
            TileEntry(Tile.GoldOre, "Gold Ore", true, 6, 1, "goldore", "#7a7a85", "#e8c04e", ore: true, extraC: "#d9a833");
            TileEntry(Tile.Torch, "Torch", false, 1, 0, "torch", null, null, light: 1f);
            TileEntry(Tile.Workbench, "Workbench", false, 2, 0, "workbench", null, null);
            TileEntry(Tile.Cactus, "Cactus", false, 2, 0, "cactus", "#4e8c4a", "#5da356");
            TileEntry(Tile.Flower, "Glowbloom", false, 1, 0, "petal", null, null, light: 0.25f);
            TileEntry(Tile.Bamboo, "Bamboo", false, 2, 0, "bamboo", "#8fbf5a", "#a3d468");
            TileEntry(Tile.Lantern, "Silk Lantern", false, 1, 0, "lantern", null, null, light: 1.2f);
            TileEntry(Tile.Shell, "Moon Shell", false, 1, 0, "shell", null, null);
        }

        public static bool Solid(Tile t) => Tiles.TryGetValue(t, out var d) && d.solid;
        public static bool Liquid(Tile t) => t == Tile.Water;

        static void Item(string id, string name, ItemType type, Tile tile = Tile.Air,
            int tier = 0, float power = 0, int dmg = 1, int heal = 0, string creature = null, int stack = 99)
        {
            Items[id] = new ItemDef
            {
                id = id, name = name, type = type, tile = tile, tier = tier,
                power = power, dmg = dmg, heal = heal, creature = creature, stack = stack,
            };
        }

        static void BuildItems()
        {
            Item("dirt", "Dirt", ItemType.Block, Tile.Dirt);
            Item("sand", "Sand", ItemType.Block, Tile.Sand);
            Item("stone", "Stone", ItemType.Block, Tile.Stone);
            Item("sandstone", "Sandstone", ItemType.Block, Tile.Sandstone);
            Item("clay", "Mud", ItemType.Block, Tile.Clay);
            Item("planks", "Planks", ItemType.Block, Tile.Planks);
            Item("coral", "Coral", ItemType.Block, Tile.Coral);
            Item("temple_brick", "Temple Brick", ItemType.Block, Tile.Temple);
            Item("gold_tile", "Gilded Tile", ItemType.Block, Tile.GoldTile);
            Item("torch", "Torch", ItemType.Block, Tile.Torch);
            Item("lantern", "Silk Lantern", ItemType.Block, Tile.Lantern);
            Item("workbench", "Workbench", ItemType.Block, Tile.Workbench);

            Item("wood", "Wood", ItemType.Mat);
            Item("kelp", "Kelp", ItemType.Mat);
            Item("cactus", "Cactus Flesh", ItemType.Mat);
            Item("petal", "Glowbloom Petal", ItemType.Mat);
            Item("bamboo", "Bamboo", ItemType.Mat);
            Item("shell", "Moon Shell", ItemType.Mat);
            Item("copper", "Copper Chunk", ItemType.Mat);
            Item("goldore", "Gold Nugget", ItemType.Mat);
            Item("jade", "Raw Jade", ItemType.Mat);
            Item("essence", "Night Essence", ItemType.Mat);

            Item("wood_pick", "Wood Pick", ItemType.Tool, tier: 1, power: 1f, dmg: 2, stack: 1);
            Item("stone_pick", "Stone Pick", ItemType.Tool, tier: 2, power: 1.6f, dmg: 3, stack: 1);
            Item("copper_pick", "Copper Pick", ItemType.Tool, tier: 3, power: 2.4f, dmg: 4, stack: 1);
            Item("jade_pick", "Jade Pick", ItemType.Tool, tier: 4, power: 3.4f, dmg: 5, stack: 1);

            Item("wood_sword", "Wood Sword", ItemType.Weapon, dmg: 4, stack: 1);
            Item("stone_sword", "Stone Sword", ItemType.Weapon, dmg: 6, stack: 1);
            Item("copper_sword", "Copper Sword", ItemType.Weapon, dmg: 9, stack: 1);
            Item("jade_sword", "Jade Sword", ItemType.Weapon, dmg: 13, stack: 1);

            Item("salve", "Healing Salve", ItemType.Use, heal: 6, stack: 20);
            Item("cat_charm", "Cat Charm", ItemType.Charm, creature: "cat", stack: 20);
            Item("scarab_charm", "Scarab Charm", ItemType.Charm, creature: "scarabPet", stack: 20);
            Item("sprite_charm", "Sprite Charm", ItemType.Charm, creature: "spriteWisp", stack: 20);
        }

        static void Rec(string output, int count, string category, bool bench, params (string, int)[] cost)
            => Recipes.Add(new Recipe { output = output, count = count, category = category, bench = bench, cost = cost });

        static void BuildRecipes()
        {
            Rec("planks", 4, "Blocks", false, ("wood", 1));
            Rec("workbench", 1, "Blocks", false, ("planks", 8));
            Rec("torch", 3, "Blocks", false, ("wood", 1), ("petal", 1));
            Rec("sandstone", 1, "Blocks", true, ("sand", 2));
            Rec("temple_brick", 2, "Blocks", true, ("stone", 1), ("sand", 1));
            Rec("gold_tile", 1, "Blocks", true, ("goldore", 1), ("stone", 1));
            Rec("lantern", 1, "Blocks", true, ("bamboo", 3), ("petal", 2), ("shell", 1));

            Rec("wood_pick", 1, "Tools", false, ("wood", 8));
            Rec("wood_sword", 1, "Tools", false, ("wood", 7));
            Rec("stone_pick", 1, "Tools", true, ("stone", 8), ("wood", 4));
            Rec("stone_sword", 1, "Tools", true, ("stone", 6), ("wood", 3));
            Rec("copper_pick", 1, "Tools", true, ("copper", 8), ("wood", 4));
            Rec("copper_sword", 1, "Tools", true, ("copper", 6), ("wood", 3));
            Rec("jade_pick", 1, "Tools", true, ("jade", 8), ("bamboo", 4));
            Rec("jade_sword", 1, "Tools", true, ("jade", 6), ("bamboo", 3));

            Rec("salve", 1, "Charms & Cures", false, ("petal", 3), ("kelp", 1));
            Rec("cat_charm", 1, "Charms & Cures", true, ("bamboo", 2), ("essence", 3));
            Rec("scarab_charm", 1, "Charms & Cures", true, ("sand", 5), ("essence", 3));
            Rec("sprite_charm", 1, "Charms & Cures", true, ("petal", 2), ("essence", 3));
        }

        static void Enemy(string id, string name, string biome, int hp, int dmg, float speed, bool fly, string drop, int dropN)
            => Enemies[id] = new EnemyDef { id = id, name = name, biome = biome, hp = hp, dmg = dmg, speed = speed, fly = fly, drop = drop, dropN = dropN };

        static void BuildEnemies()
        {
            Enemy("duneShade", "Dune Shade", "desert", 14, 2, 1.4f, true, "essence", 2);
            Enemy("scarab", "Gilded Scarab", "desert", 8, 1, 3.4f, false, "essence", 1);
            Enemy("stalker", "Jungle Stalker", "tropic", 20, 3, 2.6f, false, "essence", 2);
            Enemy("sporeling", "Sporeling", "tropic", 8, 1, 1.9f, false, "essence", 1);
            Enemy("tideWisp", "Tide Wisp", "ocean", 12, 2, 1.6f, true, "essence", 2);
            Enemy("crabby", "Moonshell Crab", "ocean", 16, 2, 1.5f, false, "shell", 1);
            Enemy("hungryGhost", "Hungry Ghost", "lantern", 18, 3, 1.5f, true, "essence", 3);
            Enemy("paperLantern", "Stray Lantern", "lantern", 10, 2, 1.9f, true, "essence", 2);
        }
    }
}
