// ══════════════════════════════════════════════════════════════
// Mushmire — DayNight.cs
// The 8-minute day: sky gradient, sun/moon, stars, clouds,
// parallax hills, and the global Light2D that darkens the world.
// Colors ported from the web version's SKY_KEYS.
// ══════════════════════════════════════════════════════════════
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Mushmire
{
    public class DayNight : MonoBehaviour
    {
        public const float CycleLen = 480f;
        public float clock = 30f;

        public float Tod => (clock / CycleLen) % 1f;
        public bool IsNight => Tod >= 0.58f && Tod < 0.97f;
        public int Day => (int)(clock / CycleLen) + 1;

        // [frac, top, bottom, light]
        static readonly (float f, Color top, Color bot, float light)[] Keys =
        {
            (0.00f, GameData.Hex("#4a3a68"), GameData.Hex("#e2c290"), 0.55f),
            (0.06f, GameData.Hex("#6ec3e8"), GameData.Hex("#cfe8d9"), 1.0f),
            (0.45f, GameData.Hex("#6ec3e8"), GameData.Hex("#d8e8c9"), 1.0f),
            (0.55f, GameData.Hex("#7a4fa3"), GameData.Hex("#e8933a"), 0.75f),
            (0.63f, GameData.Hex("#16101f"), GameData.Hex("#241a36"), 0.16f),
            (0.92f, GameData.Hex("#16101f"), GameData.Hex("#2d2244"), 0.16f),
            (0.97f, GameData.Hex("#4a3a68"), GameData.Hex("#a8875c"), 0.4f),
            (1.00f, GameData.Hex("#4a3a68"), GameData.Hex("#e2c290"), 0.55f),
        };

        public Camera cam;
        Light2D globalLight;
        SpriteRenderer skyTop;              // gradient tint sprite over camera bg
        SpriteRenderer sun, moon, sunGlow, moonGlow;
        SpriteRenderer[] stars;
        SpriteRenderer[] clouds;
        SpriteRenderer[] hillsNear, hillsFar;
        WorldData world;

        public (Color top, Color bot, float light) SkyNow()
        {
            float tod = Tod;
            for (int i = 0; i < Keys.Length - 1; i++)
            {
                if (tod >= Keys[i].f && tod <= Keys[i + 1].f)
                {
                    float t = (tod - Keys[i].f) / Mathf.Max(0.0001f, Keys[i + 1].f - Keys[i].f);
                    return (Color.Lerp(Keys[i].top, Keys[i + 1].top, t),
                            Color.Lerp(Keys[i].bot, Keys[i + 1].bot, t),
                            Mathf.Lerp(Keys[i].light, Keys[i + 1].light, t));
                }
            }
            return (Color.black, Color.black, 1);
        }

        public void Init(Camera c, WorldData w, Light2D global)
        {
            cam = c; world = w; globalLight = global;

            SpriteRenderer Sprite(string name, string decorKey, int order, Transform parent, float scale = 1)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = PixelArtFactory.Decor(decorKey);
                sr.sortingOrder = order;
                go.transform.localScale = Vector3.one * scale;
                return sr;
            }

            // sky gradient: big tinted quad glued to the camera
            skyTop = Sprite("skyGrad", "vgrad", -100, cam.transform);
            skyTop.transform.localPosition = new Vector3(0, 0, 50);

            sunGlow = Sprite("sunGlow", "glow", -95, cam.transform, 14);
            sun = Sprite("sun", "disc", -94, cam.transform, 2.4f);
            moonGlow = Sprite("moonGlow", "glow", -95, cam.transform, 10);
            moon = Sprite("moon", "disc", -94, cam.transform, 1.9f);
            sunGlow.color = new Color(0.95f, 0.76f, 0.31f, 0.5f);
            sun.color = GameData.Hex("#f7e8b0");
            moonGlow.color = new Color(0.71f, 0.55f, 0.95f, 0.45f);
            moon.color = GameData.Hex("#e8e4f2");

            stars = new SpriteRenderer[40];
            for (int i = 0; i < stars.Length; i++)
            {
                stars[i] = Sprite("star" + i, "pixel", -96, cam.transform, 0.5f);
                stars[i].color = new Color(0.96f, 0.94f, 0.91f, 0.8f);
            }
            clouds = new SpriteRenderer[6];
            for (int i = 0; i < clouds.Length; i++)
            {
                clouds[i] = Sprite("cloud" + i, "cloud" + (i % 3), -93, cam.transform, 2f);
            }
            // parallax hills: soft sine silhouettes built as sprites
            hillsFar = BuildHills("hillsFar", -92, 4, 0.10f);
            hillsNear = BuildHills("hillsNear", -91, 4, 0.22f);
        }

        SpriteRenderer[] BuildHills(string name, int order, int count, float shade)
        {
            var arr = new SpriteRenderer[count];
            var rng = new Rng((uint)(name.Length * 7717));
            for (int i = 0; i < count; i++)
            {
                var t = PixelArtFactory.NewTex(160, 90);
                // one soft hill dome per texture
                int peak = 60 + rng.Range(0, 28);
                for (int x = 0; x < 160; x++)
                {
                    float k = Mathf.Sin(x / 159f * Mathf.PI);
                    int h = (int)(peak * (0.35f + 0.65f * k * k));
                    // white pixels — SpriteRenderer.color multiplies, so the tint provides the hue
                    for (int y = 0; y < h; y++) t.SetPixel(x, y, Color.white);
                }
                var go = new GameObject(name + i);
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = PixelArtFactory.ToSprite(t, 16, 0.5f, 0f);
                sr.sortingOrder = order;
                arr[i] = sr;
            }
            return arr;
        }

        public void Tick(float dt, Vector2 playerPos, string biomeKey)
        {
            clock += dt;
            var (top, bot, light) = SkyNow();

            cam.backgroundColor = bot;
            skyTop.color = top;
            // stretch gradient quad over the whole view
            float h2 = cam.orthographicSize, w2 = h2 * cam.aspect;
            skyTop.transform.localScale = new Vector3(w2 * 2 / 0.125f, h2 * 2 / 4f, 1);

            // celestial arc
            float tod = Tod;
            bool isDay = tod < 0.58f || tod > 0.99f;
            float arcT = isDay ? tod / 0.58f : (tod - 0.6f) / 0.38f;
            float ax = (arcT - 0.5f) * w2 * 1.8f;
            float ay = -h2 * 0.1f + Mathf.Sin(arcT * Mathf.PI) * h2 * 0.85f;
            sun.transform.localPosition = sunGlow.transform.localPosition = new Vector3(ax, ay, 40);
            moon.transform.localPosition = moonGlow.transform.localPosition = new Vector3(ax, ay, 40);
            sun.enabled = sunGlow.enabled = isDay;
            moon.enabled = moonGlow.enabled = !isDay;

            // stars
            float starA = Mathf.Clamp01((0.45f - light) * 4f);
            for (int i = 0; i < stars.Length; i++)
            {
                var s = stars[i];
                if (starA <= 0.01f) { s.enabled = false; continue; }
                s.enabled = true;
                float sx = ((i * 173.3f + 40) % (w2 * 2)) - w2;
                float sy = ((i * 97.7f + 20) % (h2 * 1.4f)) - h2 * 0.2f;
                s.transform.localPosition = new Vector3(sx, sy, 45);
                float tw = 0.4f + 0.6f * Mathf.Abs(Mathf.Sin(clock * 0.8f + i));
                s.color = new Color(0.96f, 0.94f, 0.91f, starA * tw * 0.8f);
            }

            // clouds drift, fade at night
            float cloudA = 0.6f * Mathf.Clamp01((light - 0.25f) / 0.5f);
            for (int i = 0; i < clouds.Length; i++)
            {
                var c = clouds[i];
                c.color = new Color(1, 1, 1, cloudA);
                float span = w2 * 2 + 14;
                float x = ((i * 13.7f + clock * (0.25f + (i % 3) * 0.15f) - playerPos.x * 0.06f) % span + span) % span - span / 2;
                float y = h2 * 0.25f + ((i * 4.1f) % (h2 * 0.6f));
                c.transform.localPosition = new Vector3(x, y, 42);
            }

            // parallax hills follow camera with depth factors
            Color hillCol = biomeKey switch
            {
                "desert" => GameData.Hex("#8a7350"),
                "tropic" => GameData.Hex("#2f5238"),
                "ocean" => GameData.Hex("#33566e"),
                _ => GameData.Hex("#4a3f56"),
            };
            PlaceHills(hillsFar, 0.25f, hillCol * 0.8f + top * 0.2f, playerPos, -4f);
            PlaceHills(hillsNear, 0.5f, hillCol * 0.55f + top * 0.12f, playerPos, -7f);

            // global light
            if (globalLight != null)
            {
                globalLight.intensity = Mathf.Lerp(0.10f, 1f, light);
                globalLight.color = Color.Lerp(new Color(0.62f, 0.55f, 0.85f), Color.white, light);
            }
        }

        void PlaceHills(SpriteRenderer[] hills, float depth, Color col, Vector2 playerPos, float yOff)
        {
            float w2 = cam.orthographicSize * cam.aspect;
            float span = 10f * hills.Length;
            for (int i = 0; i < hills.Length; i++)
            {
                var h = hills[i];
                h.color = new Color(col.r, col.g, col.b, 1);
                float baseX = i * 10f;
                float x = Mathf.Repeat(baseX - playerPos.x * depth, span) - span / 2 + cam.transform.position.x;
                h.transform.position = new Vector3(x, cam.transform.position.y + yOff, 30);
            }
        }
    }
}
