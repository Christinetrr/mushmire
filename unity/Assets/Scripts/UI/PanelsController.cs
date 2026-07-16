// ══════════════════════════════════════════════════════════════
// Mushmire — PanelsController.cs
// Modal panels: character select, manual, inventory, crafting,
// weapon forge (draw your own weapon), death screen.
// ══════════════════════════════════════════════════════════════
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mushmire
{
    public class PanelsController : MonoBehaviour
    {
        GameController game;
        Canvas canvas;
        RectTransform charPanel, manualPanel, invPanel, craftPanel, forgePanel, deathPanel, menuPanel;

        public System.Action<CharacterLook> onBegin;
        public System.Action onRespawn;
        public System.Action onManualClosed;
        public System.Action onReturnToMenu;

        CharacterLook draft = new();
        Image previewMei, previewKhai;
        readonly Dictionary<string, (Image glow, Outline outline)> charCardFx = new();

        ForgeGrid forge;

        static readonly Color PanelBg = new(0.10f, 0.07f, 0.15f, 0.96f);

        public void Build(GameController g)
        {
            game = g;
            canvas = UiFactory.RootCanvas("PanelsCanvas", 60);   // above title (50) and HUD (20)
            canvas.transform.SetParent(transform, false);
            var root = canvas.GetComponent<RectTransform>();

            charPanel = BuildCharacterSelect(root);
            manualPanel = BuildManual(root);
            invPanel = BuildInventory(root);
            craftPanel = BuildCraft(root);
            forgePanel = BuildForge(root);
            deathPanel = BuildDeath(root);
            menuPanel = BuildMenu(root);
            CloseAll();
        }

        public void CloseAll()
        {
            charPanel.gameObject.SetActive(false);
            manualPanel.gameObject.SetActive(false);
            invPanel.gameObject.SetActive(false);
            craftPanel.gameObject.SetActive(false);
            forgePanel.gameObject.SetActive(false);
            deathPanel.gameObject.SetActive(false);
            menuPanel.gameObject.SetActive(false);
            if (game != null) game.uiModalOpen = false;
        }

        void Open(RectTransform panel)
        {
            CloseAll();
            panel.gameObject.SetActive(true);
            game.uiModalOpen = true;
        }

        RectTransform Backdrop(RectTransform root, string name)
        {
            var back = UiFactory.Panel(root, name, new Color(0.05f, 0.03f, 0.09f, 0.72f));
            UiFactory.Fill(back);
            return back;
        }

        RectTransform Card(RectTransform back, Vector2 size)
        {
            var card = UiFactory.RoundedPanel(back, "card", PanelBg);
            UiFactory.Place(card, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            var frame = card.gameObject.AddComponent<Outline>();
            frame.effectColor = GameData.Mist;
            frame.effectDistance = new Vector2(2, 2);
            return card;
        }

        void CloseButton(RectTransform card, System.Action extra = null)
        {
            var b = UiFactory.TextButton(card, "close", "Close", new Color(0, 0, 0, 0.3f), GameData.SandCol,
                () => { CloseAll(); extra?.Invoke(); }, 22);
            UiFactory.Place(b.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0, 46), new Vector2(220, 56));
        }

        // ══════════ CHARACTER SELECT ══════════
        RectTransform BuildCharacterSelect(RectTransform root)
        {
            var back = Backdrop(root, "charSelect");
            var card = Card(back, new Vector2(940, 620));

            var title = UiFactory.Label(card, "title", "✦ Who wanders into Mushmire? ✦", 28, GameData.SandBright, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -36), new Vector2(900, 44));

            // back to the title screen (it's still behind this modal)
            var backBtn = UiFactory.TextButton(card, "back", "‹ Back", new Color(0, 0, 0, 0.3f),
                GameData.SandCol, () => CloseAll(), 20);
            UiFactory.Place(backBtn.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(78, -40), new Vector2(116, 46));
            var backFrame = backBtn.gameObject.AddComponent<Outline>();
            backFrame.effectColor = GameData.Mist;
            backFrame.effectDistance = new Vector2(2, 2);

            // preset cards (below the title, golden glow marks the chosen one)
            previewMei = MakeCharCard(card, "mei", "Mei", new Vector2(-160, 116));
            previewKhai = MakeCharCard(card, "khai", "Khai", new Vector2(160, 116));

            // color swatch rows
            string[][] palettes =
            {
                new[] { "#1c1216", "#3d2b1e", "#5e4025", "#8a5a2e", "#b48cf2", "#e05656" },   // hair
                new[] { "#f2d5b0", "#e8b98a", "#d9a06b", "#b97f4e", "#96612f", "#7a4b22" },   // skin
                new[] { "#7a4fa3", "#2e6b44", "#4a7ac9", "#e05656", "#e2c290", "#3d3550" },   // top
                new[] { "#3d3550", "#5e4025", "#2e4a3a", "#333944", "#7a4fa3", "#a8875c" },   // legs
            };
            string[] partNames = { "hair", "skin", "top", "legs" };
            for (int row = 0; row < 4; row++)
            {
                var label = UiFactory.Label(card, "lbl" + row, partNames[row], 20, GameData.SandDim, TextAnchor.MiddleRight);
                UiFactory.Place(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-330, -60 - row * 50), new Vector2(110, 40));
                for (int i = 0; i < palettes[row].Length; i++)
                {
                    string part = partNames[row], hex = palettes[row][i];
                    var sw = UiFactory.TextButton(card, $"sw{row}_{i}", "", GameData.Hex(hex), Color.clear, () =>
                    {
                        var col = GameData.Hex(hex);
                        if (part == "hair") draft.hair = col;
                        if (part == "skin") draft.skin = col;
                        if (part == "top") draft.top = col;
                        if (part == "legs") draft.legs = col;
                        RefreshPreviews();
                    });
                    UiFactory.Place(sw.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f),
                        new Vector2(-230 + i * 54, -60 - row * 50), new Vector2(44, 40));
                }
            }

            // name (optional) — right of the swatch rows
            var nameLbl = UiFactory.Label(card, "nameLbl", "name (optional)", 20, GameData.SandDim);
            UiFactory.Place(nameLbl.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(245, -78), new Vector2(300, 34));
            var nameHolder = UiFactory.RoundedPanel(card, "nameHolder", new Color(0, 0, 0, 0.4f));
            UiFactory.Place(nameHolder, new Vector2(0.5f, 0.5f), new Vector2(245, -128), new Vector2(300, 56));
            var nameFrame = nameHolder.gameObject.AddComponent<Outline>();
            nameFrame.effectColor = GameData.Mist;
            nameFrame.effectDistance = new Vector2(2, 2);
            var nameInput = nameHolder.gameObject.AddComponent<InputField>();
            var nameText = UiFactory.Label(nameHolder, "text", "", 22, GameData.SandBright);
            UiFactory.Fill(nameText.rectTransform);
            var namePh = UiFactory.Label(nameHolder, "ph", "who are you?", 22, GameData.SandDim,
                TextAnchor.MiddleCenter, FontStyle.Italic);
            UiFactory.Fill(namePh.rectTransform);
            nameInput.textComponent = nameText;
            nameInput.placeholder = namePh;
            nameInput.characterLimit = 14;
            nameInput.onValueChanged.AddListener(v => draft.name = v);

            var begin = UiFactory.TextButton(card, "begin", "Begin the Journey", GameData.SandCol,
                GameData.Hex("#2c2013"), () =>
                {
                    CloseAll();
                    onBegin?.Invoke(draft);
                }, 28);
            UiFactory.Place(begin.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0, 42), new Vector2(420, 64));

            RefreshPreviews();
            return back;
        }

        Image MakeCharCard(RectTransform card, string preset, string label, Vector2 pos)
        {
            // golden halo behind the card, shown when this preset is the chosen one
            var glow = UiFactory.Sprite(card, "glow_" + preset, PixelArtFactory.Decor("glow"),
                new Color(GameData.SandCol.r, GameData.SandCol.g, GameData.SandCol.b, 0.6f));
            UiFactory.Place(glow.rectTransform, new Vector2(0.5f, 0.5f), pos, new Vector2(300, 310));
            glow.raycastTarget = false;

            var holder = UiFactory.RoundedPanel(card, "card_" + preset, new Color(0, 0, 0, 0.3f));
            UiFactory.Place(holder, new Vector2(0.5f, 0.5f), pos, new Vector2(220, 230));
            var frame = holder.gameObject.AddComponent<Outline>();
            frame.effectColor = GameData.Mist;
            frame.effectDistance = new Vector2(2, 2);
            charCardFx[preset] = (glow, frame);
            var btn = holder.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(() =>
            {
                draft.preset = preset;
                if (preset == "khai")
                {
                    draft.hair = GameData.Hex("#14100c"); draft.skin = GameData.Hex("#d9a06b");
                    draft.top = GameData.Hex("#2e6b44"); draft.legs = GameData.Hex("#5e4025");
                }
                else
                {
                    draft.hair = GameData.Hex("#1c1216"); draft.skin = GameData.Hex("#e8b98a");
                    draft.top = GameData.Hex("#7a4fa3"); draft.legs = GameData.Hex("#3d3550");
                }
                GameAudio.I.Play("uiTap", 0.4f);
                RefreshPreviews();
            });
            var img = UiFactory.Sprite(holder, "preview", null);
            UiFactory.Place(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 18), new Vector2(110, 180));
            var name = UiFactory.Label(holder, "name", label, 22, GameData.SandBright, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.Place(name.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(200, 34));
            return img;
        }

        void RefreshPreviews()
        {
            var meiLook = draft.preset == "mei" ? draft : new CharacterLook { preset = "mei" };
            var khaiLook = draft.preset == "khai" ? draft : new CharacterLook
            {
                preset = "khai",
                hair = GameData.Hex("#14100c"), skin = GameData.Hex("#d9a06b"),
                top = GameData.Hex("#2e6b44"), legs = GameData.Hex("#5e4025"),
            };
            previewMei.sprite = PixelArtFactory.CharacterSprite(meiLook, 0);
            previewKhai.sprite = PixelArtFactory.CharacterSprite(khaiLook, 0);

            // golden highlight follows the chosen preset
            foreach (var kv in charCardFx)
            {
                bool sel = kv.Key == draft.preset;
                kv.Value.glow.enabled = sel;
                kv.Value.outline.effectColor = sel ? GameData.SandBright : GameData.Mist;
            }
        }

        public void ShowCharacterSelect() => Open(charPanel);

        // ══════════ MANUAL ══════════
        RectTransform BuildManual(RectTransform root)
        {
            var back = Backdrop(root, "manual");
            var card = Card(back, new Vector2(960, 660));
            var title = UiFactory.Label(card, "title", "✦ Traveler's Manual ✦", 30, GameData.SandBright, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -44), new Vector2(900, 50));

            string body =
                "MOVING\n" +
                "drag the joystick to walk — push it to the edge to sprint\n" +
                "▲ jump · tap ▲ again mid-air = double jump\n" +
                "▼ duck · double-tap ▼ = roll (dodges hits!)\n\n" +
                "TOUCHING THE WORLD\n" +
                "Everything else is one tap: tap a block with a pickaxe to mine it\n" +
                "(hold to keep digging) · tap an empty spot to build · tap an enemy\n" +
                "to strike it · tap a cat to pet it. Important.\n\n" +
                "SURVIVING THE NIGHT\n" +
                "At sunset each realm breathes out its own spirits. Craft torches,\n" +
                "build walls, or fight back. Dawn dissolves them all.\n\n" +
                "MAKING THINGS\n" +
                "PACK shows your things. CRAFT turns wood, stone and stranger\n" +
                "materials into tools, blocks and creature charms. FORGE lets you\n" +
                "draw your own weapon pixel by pixel and make it real.\n\n" +
                "THE FOUR REALMS\n" +
                "Sunken Dunes · Verdant Tropics · Glass Ocean · Lantern Coast\n\n" +
                "Mushmire saves itself as you play. Your empire remains.";
            var txt = UiFactory.Label(card, "body", body, 21, GameData.Hex("#d8cbe8"), TextAnchor.UpperLeft);
            UiFactory.Place(txt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(880, 480));

            CloseButton(card, () => onManualClosed?.Invoke());
            return back;
        }

        public void ShowManual() => Open(manualPanel);

        // ══════════ INVENTORY ══════════
        Image[] invIcons; Text[] invCounts; Image[] invFrames;

        RectTransform BuildInventory(RectTransform root)
        {
            var back = Backdrop(root, "inventory");
            var card = Card(back, new Vector2(880, 620));
            var title = UiFactory.Label(card, "title", "Pack", 30, GameData.SandBright, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -44), new Vector2(400, 50));

            invIcons = new Image[24]; invCounts = new Text[24]; invFrames = new Image[24];
            for (int i = 0; i < 24; i++)
            {
                int idx = i;
                var slot = UiFactory.RoundedPanel(card, "slot" + i, new Color(0, 0, 0, 0.35f));
                var slotFrame = slot.gameObject.AddComponent<Outline>();
                slotFrame.effectColor = GameData.Mist;
                slotFrame.effectDistance = new Vector2(2, 2);
                // hotbar (first 6) hovers centered above the rest of the pack
                Vector2 pos = i < 6
                    ? new Vector2((i - 2.5f) * 96, 158)
                    : new Vector2(((i - 6) % 9 - 4f) * 92, 34 - ((i - 6) / 9) * 96);
                UiFactory.Place(slot, new Vector2(0.5f, 0.5f), pos, new Vector2(86, 86));
                invFrames[i] = slot.GetComponent<Image>();
                var btn = slot.gameObject.AddComponent<Button>();
                btn.onClick.AddListener(() =>
                {
                    var inv = game.player.inventory;
                    if (inv.slots[idx] == null && idx >= 6) return;
                    (inv.slots[inv.sel], inv.slots[idx]) = (inv.slots[idx], inv.slots[inv.sel]);
                    GameAudio.I.Play("uiTap", 0.4f);
                    game.OnInventoryChanged?.Invoke();
                    RefreshInventory();
                });
                invIcons[i] = UiFactory.Sprite(slot, "icon", null);
                UiFactory.Place(invIcons[i].rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(62, 62));
                invCounts[i] = UiFactory.Label(slot, "count", "", 18, GameData.SandBright, TextAnchor.LowerRight, FontStyle.Bold);
                UiFactory.Fill(invCounts[i].rectTransform);
                invCounts[i].rectTransform.offsetMax = new Vector2(-6, -2);
            }
            var hbLabel = UiFactory.Label(card, "hbLabel", "hotbar", 16, GameData.SandDim);
            UiFactory.Place(hbLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 215), new Vector2(200, 26));
            var hint = UiFactory.Label(card, "hint", "tap an item to swap it into your hotbar hand",
                17, GameData.SandDim);
            UiFactory.Place(hint.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 104), new Vector2(800, 30));
            CloseButton(card);
            return back;
        }

        void RefreshInventory()
        {
            var inv = game.player.inventory;
            for (int i = 0; i < 24; i++)
            {
                var s = inv.slots[i];
                invFrames[i].color = i < 6 ? new Color(0.16f, 0.11f, 0.24f, 0.7f) : new Color(0, 0, 0, 0.35f);
                invIcons[i].enabled = s != null;
                if (s != null) invIcons[i].sprite = game.IconFor(s.id);
                invCounts[i].text = s != null && s.count > 1 ? s.count.ToString() : "";
            }
        }

        public void ShowInventory() { Open(invPanel); RefreshInventory(); }

        // ══════════ CRAFTING ══════════
        RectTransform craftListRoot;

        RectTransform BuildCraft(RectTransform root)
        {
            var back = Backdrop(root, "craft");
            var card = Card(back, new Vector2(920, 660));
            var title = UiFactory.Label(card, "title", "Craft", 30, GameData.SandBright, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -44), new Vector2(400, 50));

            // scroll area (canonical hierarchy: ScrollRect → viewport w/ mask → content)
            var scrollGO = UiFactory.Panel(card, "scrollView", new Color(0, 0, 0, 0.2f));
            UiFactory.Place(scrollGO, new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(840, 470));
            var scroll = scrollGO.gameObject.AddComponent<ScrollRect>();

            var viewportRT = UiFactory.Group(scrollGO, "viewport");
            UiFactory.Fill(viewportRT);
            viewportRT.gameObject.AddComponent<RectMask2D>();
            var vpImg = viewportRT.gameObject.AddComponent<Image>();   // catches drags on empty space
            vpImg.color = new Color(0, 0, 0, 0.01f);

            craftListRoot = UiFactory.Group(viewportRT, "content");
            craftListRoot.anchorMin = new Vector2(0, 1);
            craftListRoot.anchorMax = new Vector2(1, 1);
            craftListRoot.pivot = new Vector2(0.5f, 1);
            craftListRoot.anchoredPosition = Vector2.zero;

            scroll.viewport = viewportRT;
            scroll.content = craftListRoot;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.scrollSensitivity = 35;   // default (1) makes the mouse wheel feel dead

            CloseButton(card);
            return back;
        }

        bool NearBench()
        {
            var pc = game.player.Center;
            int cx = Mathf.FloorToInt(pc.x), cy = game.world.GridY(pc.y);
            for (int y = cy - 5; y <= cy + 5; y++)
                for (int x = cx - 7; x <= cx + 7; x++)
                    if (game.world.Get(x, y) == Tile.Workbench) return true;
            return false;
        }

        void RefreshCraft()
        {
            var keepScroll = craftListRoot.anchoredPosition;   // stay put when re-listing after a craft
            foreach (Transform child in craftListRoot) Destroy(child.gameObject);
            bool bench = NearBench();
            var inv = game.player.inventory;
            float y = -8;
            string lastCat = "";
            foreach (var r in GameData.Recipes)
            {
                if (r.category != lastCat)
                {
                    lastCat = r.category;
                    var cat = UiFactory.Label(craftListRoot, "cat", r.category, 22, GameData.Glow, TextAnchor.MiddleLeft, FontStyle.Bold);
                    UiFactory.Place(cat.rectTransform, new Vector2(0.5f, 1), new Vector2(0, y - 18), new Vector2(780, 36));
                    y -= 44;
                }
                bool can = inv.HasAll(r.cost) && (!r.bench || bench);
                // web aesthetic: rounded row with mist border, whole row dims when locked
                var row = UiFactory.RoundedPanel(craftListRoot, "row", new Color(0, 0, 0, 0.30f));
                var rowFrame = row.gameObject.AddComponent<Outline>();
                rowFrame.effectColor = GameData.Mist;
                rowFrame.effectDistance = new Vector2(2, 2);
                var rowGroup = row.gameObject.AddComponent<CanvasGroup>();
                rowGroup.alpha = can ? 1f : 0.45f;
                UiFactory.Place(row, new Vector2(0.5f, 1), new Vector2(0, y - 39), new Vector2(800, 74));

                var icon = UiFactory.Sprite(row, "icon", game.IconFor(r.output));
                UiFactory.Place(icon.rectTransform, new Vector2(0, 0.5f), new Vector2(44, 0), new Vector2(52, 52));

                var def = GameData.Items[r.output];
                var name = UiFactory.Label(row, "name", def.name + (r.count > 1 ? " ×" + r.count : ""),
                    22, GameData.SandBright, TextAnchor.MiddleLeft, FontStyle.Bold);
                UiFactory.Place(name.rectTransform, new Vector2(0, 0.5f), new Vector2(330, 14), new Vector2(500, 32));

                var parts = new List<string>();
                foreach (var (id, n) in r.cost)
                    parts.Add($"{GameData.Items[id].name} ×{n} ({inv.Count(id)})");
                if (r.bench && !bench) parts.Add("needs workbench nearby");
                var needs = UiFactory.Label(row, "needs", string.Join(" · ", parts), 16, GameData.SandDim, TextAnchor.MiddleLeft);
                UiFactory.Place(needs.rectTransform, new Vector2(0, 0.5f), new Vector2(330, -14), new Vector2(500, 26));

                var btn = UiFactory.TextButton(row, "craft", "Craft",
                    can ? GameData.SandCol : GameData.Mist, can ? GameData.Hex("#2c2013") : new Color(0.6f, 0.6f, 0.6f),
                    () =>
                    {
                        if (!inv.HasAll(r.cost) || (r.bench && !NearBench())) return;
                        inv.PayAll(r.cost);
                        inv.Add(r.output, r.count);
                        GameAudio.I.Play("craft");
                        game.OnToast?.Invoke($"crafted {def.name}" + (r.count > 1 ? " ×" + r.count : "") + " ✦");
                        game.OnInventoryChanged?.Invoke();
                        RefreshCraft();
                    }, 20);
                UiFactory.Place(btn.GetComponent<RectTransform>(), new Vector2(1, 0.5f), new Vector2(-70, 0), new Vector2(110, 52));
                y -= 82;
            }
            craftListRoot.sizeDelta = new Vector2(0, -y + 20);
            craftListRoot.anchoredPosition = keepScroll;
        }

        public void ShowCraft()
        {
            Open(craftPanel);
            RefreshCraft();
            craftListRoot.anchoredPosition = Vector2.zero;   // fresh open starts at the top
        }

        // ══════════ FORGE ══════════
        Text forgeInfo;
        string forgeName = "";

        RectTransform BuildForge(RectTransform root)
        {
            var back = Backdrop(root, "forge");
            var card = Card(back, new Vector2(960, 640));
            var title = UiFactory.Label(card, "title", "Weapon Forge — draw it, then forge it", 28,
                GameData.SandBright, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -42), new Vector2(900, 48));

            // paint grid
            var gridHolder = UiFactory.Panel(card, "gridHolder", new Color(0.14f, 0.10f, 0.21f, 1));
            UiFactory.Place(gridHolder, new Vector2(0.5f, 0.5f), new Vector2(-210, 10), new Vector2(400, 400));
            forge = gridHolder.gameObject.AddComponent<ForgeGrid>();
            forge.Init(() => UpdateForgeInfo());

            // palette
            string[] pal =
            {
                "#1a1216", "#f5f0e8", "#e2c290", "#a8875c", "#b48cf2", "#7a4fa3", "#e05656", "#e8933a",
                "#f2c14e", "#4e9e4a", "#2e7d5b", "#4a7ac9", "#66d9e8", "#d98fb5", "#8d8d98", "#7d5a3e",
            };
            for (int i = 0; i < pal.Length; i++)
            {
                string hex = pal[i];
                var sw = UiFactory.TextButton(card, "pal" + i, "", GameData.Hex(hex), Color.clear,
                    () => forge.paint = GameData.Hex(hex));
                UiFactory.Place(sw.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f),
                    new Vector2(55 + (i % 4) * 58, 150 - (i / 4) * 58), new Vector2(48, 48));
            }
            var eraser = UiFactory.TextButton(card, "eraser", "Eraser", GameData.Mist, GameData.SandBright,
                () => forge.paint = Color.clear, 20);
            UiFactory.Place(eraser.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(110, -100), new Vector2(140, 50));
            var clear = UiFactory.TextButton(card, "clear", "Clear", GameData.Mist, GameData.SandBright,
                () => { forge.Clear(); UpdateForgeInfo(); }, 20);
            UiFactory.Place(clear.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(268, -100), new Vector2(140, 50));

            // name field
            var nameHolder = UiFactory.Panel(card, "nameHolder", new Color(0, 0, 0, 0.4f));
            UiFactory.Place(nameHolder, new Vector2(0.5f, 0.5f), new Vector2(190, -168), new Vector2(390, 54));
            var input = nameHolder.gameObject.AddComponent<InputField>();
            var nameText = UiFactory.Label(nameHolder, "text", "", 22, GameData.SandBright, TextAnchor.MiddleCenter);
            UiFactory.Fill(nameText.rectTransform);
            var placeholder = UiFactory.Label(nameHolder, "ph", "weapon name…", 22, GameData.SandDim, TextAnchor.MiddleCenter, FontStyle.Italic);
            UiFactory.Fill(placeholder.rectTransform);
            input.textComponent = nameText;
            input.placeholder = placeholder;
            input.characterLimit = 18;
            input.onValueChanged.AddListener(v => forgeName = v);

            forgeInfo = UiFactory.Label(card, "info", "", 18, GameData.SandDim);
            UiFactory.Place(forgeInfo.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 116), new Vector2(900, 30));

            var make = UiFactory.TextButton(card, "make", "Forge It", GameData.SandCol, GameData.Hex("#2c2013"),
                MakeWeapon, 26);
            UiFactory.Place(make.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(-130, 52), new Vector2(260, 64));
            CloseButton(card);
            var cb = card.Find("close").GetComponent<RectTransform>();
            UiFactory.Place(cb, new Vector2(0.5f, 0), new Vector2(150, 52), new Vector2(220, 64));
            return back;
        }

        static readonly (string, int)[] ForgeCost = { ("wood", 5), ("stone", 5), ("essence", 2) };
        static int WeaponDamage(int px) => Mathf.Min(16, 5 + px / 18);

        void UpdateForgeInfo()
        {
            int n = forge.PixelCount();
            var inv = game.player.inventory;
            string cost = $"cost: wood ×5 ({inv.Count("wood")}) · stone ×5 ({inv.Count("stone")}) · night essence ×2 ({inv.Count("essence")})";
            forgeInfo.text = n == 0 ? "draw your weapon, then forge it — " + cost
                : $"damage {WeaponDamage(n)} · " + cost;
        }

        void MakeWeapon()
        {
            int n = forge.PixelCount();
            if (n < 6) { game.OnToast?.Invoke("draw a bit more of it first"); return; }
            var inv = game.player.inventory;
            if (!inv.HasAll(ForgeCost)) { game.OnToast?.Invoke("need: wood ×5, stone ×5, night essence ×2"); return; }
            inv.PayAll(ForgeCost);
            var w = new CustomWeapon
            {
                name = string.IsNullOrWhiteSpace(forgeName) ? "Nameless Blade" : forgeName.Trim(),
                dmg = WeaponDamage(n),
                grid = (Color[])forge.grid.Clone(),
            };
            w.sprite = ForgePanelHelper.SpriteFromGrid(w.grid);
            game.customWeapons.Add(w);
            inv.Add("custom:" + (game.customWeapons.Count - 1), 1);
            GameAudio.I.Play("forge");
            game.OnToast?.Invoke($"⚔ {w.name} forged! damage {w.dmg}");
            game.OnInventoryChanged?.Invoke();
            forge.Clear();
            CloseAll();
            game.Save();
        }

        public void ShowForge() { Open(forgePanel); UpdateForgeInfo(); }

        // ══════════ MENU (pause) ══════════
        RectTransform BuildMenu(RectTransform root)
        {
            var back = Backdrop(root, "menu");
            var card = Card(back, new Vector2(520, 520));

            var title = UiFactory.Label(card, "title", "✦ Menu ✦", 30, GameData.SandBright,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -44), new Vector2(400, 46));

            // volume
            var volLbl = UiFactory.Label(card, "volLbl", "volume", 21, GameData.SandDim, TextAnchor.MiddleLeft);
            UiFactory.Place(volLbl.rectTransform, new Vector2(0.5f, 1), new Vector2(-95, -104), new Vector2(220, 34));
            var volSlider = UiFactory.MakeSlider(card, "volSlider", PlayerPrefs.GetFloat("mm_volume", 0.8f), v =>
            {
                GameAudio.I.SetVolume(v);
                PlayerPrefs.SetFloat("mm_volume", v);
            });
            UiFactory.Place((RectTransform)volSlider.transform, new Vector2(0.5f, 1), new Vector2(0, -148), new Vector2(360, 44));

            // control sensitivity
            var senLbl = UiFactory.Label(card, "senLbl", "control sensitivity", 21, GameData.SandDim, TextAnchor.MiddleLeft);
            UiFactory.Place(senLbl.rectTransform, new Vector2(0.5f, 1), new Vector2(-46, -204), new Vector2(320, 34));
            var senSlider = UiFactory.MakeSlider(card, "senSlider", PlayerPrefs.GetFloat("mm_sens", 0.5f), v =>
            {
                game.input.sensitivity = v;
                PlayerPrefs.SetFloat("mm_sens", v);
            });
            UiFactory.Place((RectTransform)senSlider.transform, new Vector2(0.5f, 1), new Vector2(0, -248), new Vector2(360, 44));

            // actions
            var resume = UiFactory.TextButton(card, "resume", "Resume", GameData.SandCol, GameData.Hex("#2c2013"),
                () => CloseAll(), 24);
            UiFactory.Place(resume.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0, 138), new Vector2(300, 60));

            var toTitle = UiFactory.TextButton(card, "toTitle", "Return to Main Menu", new Color(0, 0, 0, 0.3f),
                GameData.SandCol, () =>
                {
                    CloseAll();
                    onReturnToMenu?.Invoke();
                }, 22);
            UiFactory.Place(toTitle.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0, 64), new Vector2(300, 56));
            var frame = toTitle.gameObject.AddComponent<Outline>();
            frame.effectColor = GameData.Mist;
            frame.effectDistance = new Vector2(2, 2);
            var saveHint = UiFactory.Label(card, "saveHint", "your world is saved when you leave", 16, GameData.SandDim);
            UiFactory.Place(saveHint.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(500, 28));

            return back;
        }

        public void ShowMenu() => Open(menuPanel);

        // ══════════ DEATH ══════════
        RectTransform BuildDeath(RectTransform root)
        {
            var back = Backdrop(root, "death");
            var card = Card(back, new Vector2(700, 320));
            var msg = UiFactory.Label(card, "msg", "the spores carry you home…", 32, GameData.Glow,
                TextAnchor.MiddleCenter, FontStyle.Italic);
            UiFactory.Place(msg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 50), new Vector2(600, 60));
            var rise = UiFactory.TextButton(card, "rise", "Rise Again", GameData.SandCol, GameData.Hex("#2c2013"),
                () => { CloseAll(); onRespawn?.Invoke(); }, 28);
            UiFactory.Place(rise.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(340, 70));
            return back;
        }

        public void ShowDeath() => Open(deathPanel);
    }

    // ══════════ paintable 16×16 forge grid ══════════
    public class ForgeGrid : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        public Color[] grid = new Color[256];
        public Color paint = GameData.SandCol;
        RawImage view;
        Texture2D tex;
        System.Action onChanged;

        public void Init(System.Action changed)
        {
            onChanged = changed;
            tex = new Texture2D(16, 16, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            Clear();
            var go = new GameObject("view");
            go.transform.SetParent(transform, false);
            view = go.AddComponent<RawImage>();
            view.texture = tex;
            UiFactory.Fill(view.rectTransform);
        }

        public void Clear()
        {
            for (int i = 0; i < 256; i++) grid[i] = Color.clear;
            Redraw();
        }

        public int PixelCount()
        {
            int n = 0;
            foreach (var c in grid) if (c.a > 0.1f) n++;
            return n;
        }

        void Redraw()
        {
            // checkerboard backdrop under transparent cells
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    var c = grid[y * 16 + x];
                    if (c.a < 0.1f)
                        c = (x + y) % 2 == 0 ? new Color(0.14f, 0.10f, 0.21f, 1) : new Color(0.18f, 0.13f, 0.27f, 1);
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();
        }

        void PaintAt(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)transform, e.position, e.pressEventCamera, out var local);
            var rect = ((RectTransform)transform).rect;
            int x = Mathf.FloorToInt((local.x - rect.xMin) / rect.width * 16);
            int y = Mathf.FloorToInt((local.y - rect.yMin) / rect.height * 16);
            if (x < 0 || x >= 16 || y < 0 || y >= 16) return;
            grid[y * 16 + x] = paint;
            Redraw();
            onChanged?.Invoke();
        }

        public void OnPointerDown(PointerEventData e) => PaintAt(e);
        public void OnDrag(PointerEventData e) => PaintAt(e);
    }

    public static class ForgePanelHelper
    {
        public static Sprite SpriteFromGrid(Color[] grid)
        {
            var t = PixelArtFactory.NewTex(16, 16);
            for (int i = 0; i < 256; i++)
                if (grid[i].a > 0.1f) t.SetPixel(i % 16, i / 16, grid[i]);
            return PixelArtFactory.ToSprite(t);
        }
    }
}
