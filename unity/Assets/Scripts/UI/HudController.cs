// ══════════════════════════════════════════════════════════════
// Mushmire — HudController.cs
// In-game overlay: hearts, day/biome chips, hotbar, touch
// controls (◀ ▶ ⬇ ⚔ ⬆ + pack/craft/forge), breath bar, toast.
// ══════════════════════════════════════════════════════════════
using UnityEngine;
using UnityEngine.UI;

namespace Mushmire
{
    public class HudController : MonoBehaviour
    {
        GameController game;
        TouchGestures gestures;
        Canvas canvas;

        Image[] hearts;
        Image[] breathDots;
        Text dayText, biomeText, toastText;
        Image clockIcon;
        Image[] hotbarFrames = new Image[6];
        Image[] hotbarIcons = new Image[6];
        Text[] hotbarCounts = new Text[6];
        Image[] hotbarGlows = new Image[6];
        Outline[] hotbarOutlines = new Outline[6];
        Image underwaterTint;
        float toastT;

        public System.Action onPack, onCraft, onForge, onMenu;

        public void Build(GameController g, TouchGestures input)
        {
            game = g; gestures = input;
            canvas = UiFactory.RootCanvas("HudCanvas", 20);
            canvas.transform.SetParent(transform, false);
            var root = canvas.GetComponent<RectTransform>();

            // underwater tint — created first so it sits behind every HUD element
            underwaterTint = UiFactory.Panel(root, "underwater", new Color(24 / 255f, 64 / 255f, 128 / 255f, 0.22f)).GetComponent<Image>();
            UiFactory.Fill(underwaterTint.rectTransform);
            underwaterTint.raycastTarget = false;
            underwaterTint.enabled = false;

            // ── hearts (top-left) ──
            hearts = new Image[5];
            var heartSprite = HeartSprite();
            for (int i = 0; i < 5; i++)
            {
                hearts[i] = UiFactory.Sprite(root, "heart" + i, heartSprite, GameData.Blood);
                UiFactory.Place(hearts[i].rectTransform, new Vector2(0, 1), new Vector2(40 + i * 42, -36), new Vector2(36, 36));
            }
            // breath bubbles under hearts
            breathDots = new Image[8];
            for (int i = 0; i < 8; i++)
            {
                breathDots[i] = UiFactory.Sprite(root, "breath" + i, PixelArtFactory.Decor("disc"), new Color(0.7f, 0.86f, 1f, 0.9f));
                UiFactory.Place(breathDots[i].rectTransform, new Vector2(0, 1), new Vector2(34 + i * 22, -74), new Vector2(14, 14));
                breathDots[i].enabled = false;
            }

            // ── chips: one row of small pills top-right, like the web HUD ──
            var dayChip = UiFactory.RoundedPanel(root, "dayChip", new Color(0.10f, 0.07f, 0.15f, 0.7f));
            UiFactory.Place(dayChip, new Vector2(1, 1), new Vector2(-292, -32), new Vector2(122, 34));
            dayText = UiFactory.Label(dayChip, "day", "Day 1", 17, GameData.SandBright);
            UiFactory.Fill(dayText.rectTransform);
            dayText.rectTransform.offsetMin = new Vector2(16, 0);
            clockIcon = UiFactory.Sprite(dayChip, "clockIcon", PixelArtFactory.Decor("disc"), GameData.Hex("#f7e8b0"));
            UiFactory.Place(clockIcon.rectTransform, new Vector2(0, 0.5f), new Vector2(17, 0), new Vector2(17, 17));

            var biomeChip = UiFactory.RoundedPanel(root, "biomeChip", new Color(0.10f, 0.07f, 0.15f, 0.7f));
            UiFactory.Place(biomeChip, new Vector2(1, 1), new Vector2(-119, -32), new Vector2(208, 34));
            biomeText = UiFactory.Label(biomeChip, "biome", "", 17, GameData.SandBright);
            UiFactory.Fill(biomeText.rectTransform);

            // ── hotbar (top-center) — web proportions, rounded slots, halo on selection ──
            hotbarGlows = new Image[6];
            hotbarOutlines = new Outline[6];
            for (int i = 0; i < 6; i++)
            {
                int idx = i;
                float x = (i - 2.5f) * 64;
                // halo glow behind the selected slot (created first = renders behind)
                hotbarGlows[i] = UiFactory.Sprite(root, "hbGlow" + i, PixelArtFactory.Decor("glow"),
                    new Color(GameData.SandCol.r, GameData.SandCol.g, GameData.SandCol.b, 0.55f));
                UiFactory.Place(hotbarGlows[i].rectTransform, new Vector2(0.5f, 1), new Vector2(x, -44), new Vector2(96, 96));
                hotbarGlows[i].raycastTarget = false;

                var frame = UiFactory.RoundedPanel(root, "hb" + i, new Color(0.10f, 0.07f, 0.15f, 0.75f));
                UiFactory.Place(frame, new Vector2(0.5f, 1), new Vector2(x, -44), new Vector2(58, 58));
                hotbarFrames[i] = frame.GetComponent<Image>();
                hotbarOutlines[i] = frame.gameObject.AddComponent<Outline>();
                hotbarOutlines[i].effectColor = GameData.Mist;
                hotbarOutlines[i].effectDistance = new Vector2(2, 2);
                var btn = frame.gameObject.AddComponent<Button>();
                btn.onClick.AddListener(() =>
                {
                    game.player.inventory.sel = idx;
                    GameAudio.I.Play("uiTap", 0.4f);
                    Refresh();
                });
                hotbarIcons[i] = UiFactory.Sprite(frame, "icon", null);
                UiFactory.Place(hotbarIcons[i].rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(42, 42));
                hotbarCounts[i] = UiFactory.Label(frame, "count", "", 15, GameData.SandBright, TextAnchor.LowerRight, FontStyle.Bold);
                UiFactory.Fill(hotbarCounts[i].rectTransform);
                hotbarCounts[i].rectTransform.offsetMax = new Vector2(-5, -2);
            }

            // ── touch controls: joystick left · jump + duck right ──
            // (attack/mine/grab/pet all happen by tapping things in the world)
            var joyGO = new GameObject("Joystick", typeof(RectTransform));
            joyGO.transform.SetParent(root, false);
            var joy = joyGO.AddComponent<VirtualJoystick>();
            UiFactory.Place((RectTransform)joyGO.transform, new Vector2(0, 0), new Vector2(160, 150), new Vector2(190, 190));
            joy.Init(gestures);

            MakeCtl(root, "▲", "jump", new Vector2(1, 0), new Vector2(-100, 130), 120);
            MakeCtl(root, "▼", "down", new Vector2(1, 0), new Vector2(-235, 100), 88);

            // ── pixel side buttons: red backpack (pack), gray hammer (craft), pencil (forge) ──
            string[] packArt =
            {
                "....rrrr....",
                "...r....r...",
                "..rrrrrrrr..",
                ".rrrrrrrrrr.",
                ".rrkrrrrkrr.",
                ".rrkrrrrkrr.",
                ".rrrrrrrrrr.",
                ".rrrhhhhrrr.",
                ".rrrhhhhrrr.",
                ".rrrrrrrrrr.",
                "..rrrrrrrr..",
            };
            string[] craftArt =   // hammer crossed with wrench
            {
                "cccc......c..c",
                "cccc......cccc",
                "ccnn.......cc.",
                "..nn......cc..",
                "...nn....cc...",
                "....nn..cc....",
                ".....nncc.....",
                ".....ccnn.....",
                "....cc..nn....",
                "...cc....nn...",
                "..cc......nn..",
                ".cc........nn.",
                "cc..........nn",
            };
            string[] forgeArt =
            {
                "..........mm",
                ".........myy",
                "........myyy",
                ".......myyy.",
                "......myyy..",
                ".....myyy...",
                "....myyy....",
                "...nyyy.....",
                "..nnyy......",
                ".knn........",
                "kk..........",
            };
            MakeIconBtn(root, "pack", packArt, -108, () => onPack?.Invoke());
            MakeIconBtn(root, "craft", craftArt, -184, () => onCraft?.Invoke());
            MakeIconBtn(root, "forge", forgeArt, -260, () => onForge?.Invoke());

            // ── pixel menu button (top-left corner, under the hearts) ──
            // sand frame + dark slate body: the frame pops at night, the body pops by day
            string[] menuArt =
            {
                "tttttttttt",
                "tddddddddt",
                "tdhhhhhhdt",
                "tddddddddt",
                "tdhhhhhhdt",
                "tddddddddt",
                "tdhhhhhhdt",
                "tddddddddt",
                "tttttttttt",
            };
            var menuGO = new GameObject("menuBtn", typeof(RectTransform));
            menuGO.transform.SetParent(root, false);
            var menuImg = menuGO.AddComponent<Image>();
            menuImg.sprite = PixelArtFactory.SpriteFromMap(menuArt, 2, true);
            menuImg.preserveAspect = true;
            UiFactory.Place(menuImg.rectTransform, new Vector2(0, 1), new Vector2(36, -108), new Vector2(56, 46));
            var menuBtn = menuGO.AddComponent<Button>();
            menuBtn.targetGraphic = menuImg;
            menuBtn.onClick.AddListener(() => { GameAudio.I.Play("uiTap", 0.5f); onMenu?.Invoke(); });

            // ── toast ──
            toastText = UiFactory.Label(root, "toast", "", 24, GameData.SandBright);
            UiFactory.Place(toastText.rectTransform, new Vector2(0.5f, 0.65f), Vector2.zero, new Vector2(900, 50));
            toastText.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(0, -2);

            game.OnInventoryChanged += Refresh;
            game.OnToast += Toast;
            Refresh();
        }

        void MakeCtl(RectTransform root, string glyph, string key, Vector2 anchor, Vector2 pos, float size)
        {
            // clean translucent circle
            var go = new GameObject("ctl_" + key);
            go.transform.SetParent(root, false);
            var img = go.AddComponent<Image>();
            img.sprite = PixelArtFactory.Decor("disc");
            img.color = new Color(0.10f, 0.07f, 0.15f, 0.48f);
            UiFactory.Place(img.rectTransform, anchor, pos, new Vector2(size, size));
            var label = UiFactory.Label(go.transform, "glyph", glyph, (int)(size * 0.38f),
                new Color(GameData.SandBright.r, GameData.SandBright.g, GameData.SandBright.b, 0.85f));
            UiFactory.Fill(label.rectTransform);
            var hb = go.AddComponent<HoldButton>();
            hb.gestures = gestures; hb.key = key; hb.visual = img;
        }

        void MakeIconBtn(RectTransform root, string name, string[] art, float y, System.Action onClick)
        {
            // bare pixel icon — an invisible padded image keeps the tap target generous
            var holder = new GameObject("btn_" + name, typeof(RectTransform));
            holder.transform.SetParent(root, false);
            var hit = holder.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            UiFactory.Place(hit.rectTransform, new Vector2(1, 1), new Vector2(-58, y), new Vector2(72, 72));
            var icon = UiFactory.Sprite(holder.transform, "icon", PixelArtFactory.SpriteFromMap(art, 2, true));
            UiFactory.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(52, 52));
            icon.raycastTarget = false;
            var btn = holder.AddComponent<Button>();
            btn.targetGraphic = icon;
            btn.onClick.AddListener(() => { GameAudio.I.Play("uiTap", 0.5f); onClick(); });
        }

        Sprite HeartSprite()
        {
            string[] art =
            {
                ".rr..rr.",
                "rrrrrrrr",
                "rrrrrrrr",
                "rrrrrrrr",
                ".rrrrrr.",
                "..rrrr..",
                "...rr...",
            };
            return PixelArtFactory.SpriteFromMap(art, 2);
        }

        public void Refresh()
        {
            var inv = game.player.inventory;
            for (int i = 0; i < 6; i++)
            {
                var s = inv.slots[i];
                bool sel = inv.sel == i;
                hotbarGlows[i].enabled = sel;                                        // the halo
                hotbarOutlines[i].effectColor = sel ? GameData.SandBright : GameData.Mist;
                hotbarFrames[i].color = new Color(0.10f, 0.07f, 0.15f, 0.75f);
                hotbarIcons[i].enabled = s != null;
                if (s != null) hotbarIcons[i].sprite = game.IconFor(s.id);
                hotbarCounts[i].text = s != null && s.count > 1 ? s.count.ToString() : "";
            }
            // update held sprite on the character
            var selSlot = inv.Selected;
            game.player.SetHeldSprite(selSlot != null ? game.IconFor(selSlot.id) : null);
        }

        public void Toast(string msg)
        {
            toastText.text = msg;
            toastT = 1.8f;
        }

        void Update()
        {
            if (game == null || !game.running) return;
            // hearts (2 hp per heart)
            for (int i = 0; i < 5; i++)
            {
                int v = game.player.hp - i * 2;
                hearts[i].color = v >= 2 ? GameData.Blood : v >= 1 ? GameData.Hex("#e8933a") : new Color(0.15f, 0.12f, 0.18f, 0.8f);
            }
            // breath
            int dots = game.player.breath < 1 ? Mathf.CeilToInt(game.player.breath * 8) : 0;
            for (int i = 0; i < 8; i++) breathDots[i].enabled = game.player.breath < 1 && i < dots;
            underwaterTint.enabled = game.player.inWater;

            dayText.text = "Day " + game.dayNight.Day;
            clockIcon.color = game.dayNight.IsNight ? GameData.Hex("#e8e4f2") : GameData.Hex("#f7e8b0");
            biomeText.text = GameData.BiomeLabel(GameData.BiomeAt(Mathf.FloorToInt(game.player.Center.x)));

            if (toastT > 0)
            {
                toastT -= Time.deltaTime;
                var c = toastText.color; c.a = Mathf.Clamp01(toastT * 2.5f);
                toastText.color = c;
            }
        }

        public void Show(bool visible) => canvas.gameObject.SetActive(visible);
    }
}
