// ══════════════════════════════════════════════════════════════
// Mushmire — TitleScreen.cs
// The homepage. Palette and floating-orb (spore) aesthetic are
// preserved EXACTLY from the web version by request:
// bg #0d0916→#3d2c5e glow · logo #f2ddb0 · accent #b48cf2
// · spores in sand & violet · "To: my baby Richard ♥".
// ══════════════════════════════════════════════════════════════
using UnityEngine;
using UnityEngine.UI;

namespace Mushmire
{
    public class TitleScreen : MonoBehaviour
    {
        public System.Action onNewJourney, onContinue, onManual;
        Canvas canvas;
        UnityEngine.UI.Button continueBtn;

        public void Build(bool hasSave)
        {
            canvas = UiFactory.RootCanvas("TitleCanvas", 50);
            canvas.transform.SetParent(transform, false);
            var root = canvas.GetComponent<RectTransform>();

            // background: deep night + violet glow rising from the bottom (matches web radial)
            var bg = UiFactory.Panel(root, "bg", GameData.Hex("#0d0916"));
            UiFactory.Fill(bg);
            var glow = UiFactory.Sprite(bg, "glow", PixelArtFactory.Decor("glow"), GameData.Hex("#3d2c5e"));
            UiFactory.Place(glow.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, -200), new Vector2(2600, 1500));
            glow.preserveAspect = false;

            // floating spores (the orbs — do not change)
            for (int i = 0; i < 36; i++)
            {
                var spore = UiFactory.Sprite(bg, "spore" + i, PixelArtFactory.Decor("glow"));
                float r = 6 + Random.value * 14;
                UiFactory.Place(spore.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(r, r));
                var fl = spore.gameObject.AddComponent<SporeFloat>();
                fl.Init(spore, Random.value < 0.55f ? GameData.SandCol : GameData.Glow);
            }

            // crest 🍄 substitute: little pixel mushroom
            var crest = UiFactory.Sprite(root, "crest", MushroomSprite());
            UiFactory.Place(crest.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 240), new Vector2(72, 72));

            var logo = UiFactory.Label(root, "logo", "M U S H M I R E", 110, GameData.SandBright,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.Place(logo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 140), new Vector2(1400, 140));
            var sh = logo.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0.42f, 0.33f, 0.22f, 0.9f);
            sh.effectDistance = new Vector2(0, -6);
            var outline = logo.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(GameData.Glow.r, GameData.Glow.g, GameData.Glow.b, 0.35f);
            outline.effectDistance = new Vector2(2, 2);

            var sub = UiFactory.Label(root, "sub", "—  t h e   M u s h   E m p i r e   a w a i t s  —",
                24, GameData.Glow);
            UiFactory.Place(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 55), new Vector2(1200, 40));

            continueBtn = MakeSandButton(root, "Continue Journey", -40, () => onContinue?.Invoke());
            MakeSandButton(root, "New Journey", -130, () => onNewJourney?.Invoke());
            MakeGhostButton(root, "How to Play", -220, () => onManual?.Invoke());
            SetContinueVisible(hasSave);

            var dedication = UiFactory.Label(root, "dedication", "To: my baby Richard ♥", 22,
                GameData.SandDim, TextAnchor.MiddleCenter, FontStyle.Italic);
            UiFactory.Place(dedication.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -300), new Vector2(800, 40));
        }

        public void SetContinueVisible(bool visible) => continueBtn.gameObject.SetActive(visible);

        UnityEngine.UI.Button MakeSandButton(RectTransform root, string text, float y, System.Action onClick)
        {
            var b = UiFactory.TextButton(root, text, text, GameData.SandCol, GameData.Hex("#2c2013"), onClick, 30);
            UiFactory.Place(b.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(420, 74));
            return b;
        }
        void MakeGhostButton(RectTransform root, string text, float y, System.Action onClick)
        {
            var b = UiFactory.TextButton(root, text, text, new Color(0, 0, 0, 0.25f), GameData.SandCol, onClick, 26);
            UiFactory.Place(b.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(420, 66));
            var frame = b.gameObject.AddComponent<Outline>();
            frame.effectColor = GameData.Mist;
            frame.effectDistance = new Vector2(2, 2);
        }

        Sprite MushroomSprite()
        {
            string[] art =
            {
                "....rrrr....",
                "..rrwwrrrr..",
                ".rrwwrrrrrr.",
                ".rrrrrrwwrr.",
                ".rrrrrrrrrr.",
                "....hhhh....",
                "....hhhh....",
                "....hhhh....",
                "...hhhhhh...",
            };
            return PixelArtFactory.SpriteFromMap(art, 2);
        }

        public void Show(bool visible) => canvas.gameObject.SetActive(visible);
    }

    // drifts a spore upward with sway + twinkle, wrapping at the top
    public class SporeFloat : MonoBehaviour
    {
        Image img;
        Color baseCol;
        float speed, phase, sway, baseX;
        RectTransform rt;

        public void Init(Image image, Color col)
        {
            img = image;
            baseCol = col;
            rt = img.rectTransform;
            speed = 18 + Random.value * 36;
            phase = Random.value * 7;
            sway = 12 + Random.value * 18;
            baseX = (Random.value - 0.5f) * 1600;
            rt.anchoredPosition = new Vector2(baseX, (Random.value - 0.5f) * 800);
        }

        void Update()
        {
            var p = rt.anchoredPosition;
            p.y += speed * Time.deltaTime;
            if (p.y > 420) { p.y = -420; baseX = (Random.value - 0.5f) * 1600; }
            p.x = baseX + Mathf.Sin(Time.time + phase) * sway;
            rt.anchoredPosition = p;
            var c = baseCol;
            c.a = 0.35f + 0.3f * Mathf.Sin(Time.time * 2 + phase);
            img.color = c;
        }
    }
}
