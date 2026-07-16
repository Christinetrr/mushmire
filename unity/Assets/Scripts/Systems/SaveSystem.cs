// ══════════════════════════════════════════════════════════════
// Mushmire — SaveSystem.cs
// JSON persistence to Application.persistentDataPath.
// ══════════════════════════════════════════════════════════════
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Mushmire
{
    [System.Serializable]
    public class SaveDTO
    {
        public int version = 1;
        public string tilesRle;
        public int[] heights;
        public int spawnX, spawnY;
        public List<int> templeX = new(), templeY = new();

        public string preset, charName;
        public string hair, skin, top, legs;

        public float px, py;
        public int hp, sel;
        public List<string> slotIds = new();
        public List<int> slotCounts = new();

        public float clock;
        public List<CustomWeaponDTO> customWeapons = new();
        public List<CreatureDTO> creatures = new();
    }

    [System.Serializable]
    public class CustomWeaponDTO
    {
        public string weaponName;
        public int dmg;
        public List<string> grid = new();   // 256 entries, "" = empty, else "#rrggbb"
    }

    [System.Serializable]
    public class CreatureDTO
    {
        public string kind;
        public float x, y, homeX;
        public bool follows;
    }

    public static class SaveSystem
    {
        static string Path => System.IO.Path.Combine(Application.persistentDataPath, "mushmire_save.json");

        public static bool HasSave() => File.Exists(Path);

        public static void Save(SaveDTO dto)
        {
            try { File.WriteAllText(Path, JsonUtility.ToJson(dto)); }
            catch (System.Exception e) { Debug.LogWarning("save failed: " + e.Message); }
        }

        public static SaveDTO Load()
        {
            try
            {
                if (!File.Exists(Path)) return null;
                return JsonUtility.FromJson<SaveDTO>(File.ReadAllText(Path));
            }
            catch { return null; }
        }

        public static void Clear()
        {
            try { if (File.Exists(Path)) File.Delete(Path); } catch { }
        }
    }
}
