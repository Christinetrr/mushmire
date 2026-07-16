// ══════════════════════════════════════════════════════════════
// Mushmire — UiFactory.cs
// Small helpers to build the whole UGUI interface from code,
// plus the press-and-hold button used by the touch controls.
// ══════════════════════════════════════════════════════════════
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mushmire
{
    public static class UiFactory
    {
        public static Font DefaultFont;

        public static Canvas RootCanvas(string name, int order)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1334, 750);
            scaler.matchWidthOrHeight = 0.5f;   // balanced — survives any aspect ratio
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        public static Font Font()
        {
            if (DefaultFont == null)
                DefaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return DefaultFont;
        }

        public static RectTransform Panel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            return go.GetComponent<RectTransform>();
        }

        public static RectTransform Group(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<RectTransform>();
        }

        public static Image Sprite(Transform parent, string name, UnityEngine.Sprite sprite, Color? tint = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = tint ?? Color.white;
            img.preserveAspect = true;
            return img;
        }

        public static Text Label(Transform parent, string name, string text, int size, Color color,
            TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = Font();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        public static Button TextButton(Transform parent, string name, string text, Color bg, Color fg,
            System.Action onClick, int fontSize = 30)
        {
            var rt = Panel(parent, name, bg);
            Rounded(rt.GetComponent<Image>());
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = rt.GetComponent<Image>();
            var label = Label(rt, "label", text, fontSize, fg, TextAnchor.MiddleCenter, FontStyle.Bold);
            Fill(label.rectTransform);
            if (onClick != null)
                btn.onClick.AddListener(() => { GameAudio.I?.Play("uiTap", 0.5f); onClick(); });
            return btn;
        }

        // rect helpers
        public static void Fill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }
        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchor; rt.anchorMax = anchor;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
        }

        // horizontal 0..1 slider in the mushmire style (dark track, sand fill, square handle)
        public static Slider MakeSlider(Transform parent, string name, float initial, System.Action<float> onChange)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var slider = go.AddComponent<Slider>();

            var bg = Panel(go.transform, "bg", new Color(0, 0, 0, 0.55f));
            bg.anchorMin = new Vector2(0, 0.5f); bg.anchorMax = new Vector2(1, 0.5f);
            bg.sizeDelta = new Vector2(0, 14); bg.anchoredPosition = Vector2.zero;

            var fillArea = Group(go.transform, "fillArea");
            fillArea.anchorMin = new Vector2(0, 0.5f); fillArea.anchorMax = new Vector2(1, 0.5f);
            fillArea.sizeDelta = new Vector2(-8, 14); fillArea.anchoredPosition = Vector2.zero;
            var fill = Panel(fillArea, "fill", GameData.SandCol);
            Fill(fill);

            var handleArea = Group(go.transform, "handleArea");
            Fill(handleArea);
            handleArea.offsetMin = new Vector2(18, 0); handleArea.offsetMax = new Vector2(-18, 0);
            var handle = Panel(handleArea, "handle", GameData.SandBright);
            handle.sizeDelta = new Vector2(26, 30);
            var frame = handle.gameObject.AddComponent<Outline>();
            frame.effectColor = new Color(0.17f, 0.12f, 0.24f, 1);
            frame.effectDistance = new Vector2(3, 3);

            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = initial;
            slider.onValueChanged.AddListener(v => onChange(v));
            return slider;
        }

        // web-style border-radius via the 9-slice pill sprite
        public static void Rounded(Image img)
        {
            img.sprite = PixelArtFactory.Pill();
            img.type = Image.Type.Sliced;
        }

        public static RectTransform RoundedPanel(Transform parent, string name, Color color)
        {
            var rt = Panel(parent, name, color);
            Rounded(rt.GetComponent<Image>());
            return rt;
        }
    }

    // touch button that reports press & release to TouchGestures
    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public TouchGestures gestures;
        public string key;
        public Image visual;
        static readonly Color Idle = new(0.10f, 0.07f, 0.15f, 0.48f);
        static readonly Color Pressed = new(0.89f, 0.76f, 0.56f, 0.5f);

        public void OnPointerDown(PointerEventData e)
        {
            gestures.Press(key);
            if (visual != null) visual.color = Pressed;
        }
        public void OnPointerUp(PointerEventData e)
        {
            gestures.Release(key);
            if (visual != null) visual.color = Idle;
        }
        public void OnPointerExit(PointerEventData e)
        {
            gestures.Release(key);
            if (visual != null) visual.color = Idle;
        }
    }

    // left-thumb virtual joystick: drag to walk, push to the edge to sprint
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        TouchGestures gestures;
        Image baseImg, knob;
        const float Radius = 78f;

        public void Init(TouchGestures g)
        {
            gestures = g;
            baseImg = gameObject.AddComponent<Image>();
            baseImg.sprite = PixelArtFactory.Decor("disc");
            baseImg.color = new Color(0.10f, 0.07f, 0.15f, 0.40f);
            var knobGO = new GameObject("knob");
            knobGO.transform.SetParent(transform, false);
            knob = knobGO.AddComponent<Image>();
            knob.sprite = PixelArtFactory.Decor("disc");
            knob.color = new Color(GameData.SandCol.r, GameData.SandCol.g, GameData.SandCol.b, 0.55f);
            knob.raycastTarget = false;
            UiFactory.Place(knob.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(76, 76));
        }

        void Track(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)transform, e.position, e.pressEventCamera, out var local);
            var v = Vector2.ClampMagnitude(local, Radius);
            knob.rectTransform.anchoredPosition = v;
            gestures.JoystickAxis(v.x / Radius, true);
        }

        public void OnPointerDown(PointerEventData e) => Track(e);
        public void OnDrag(PointerEventData e) => Track(e);
        public void OnPointerUp(PointerEventData e)
        {
            knob.rectTransform.anchoredPosition = Vector2.zero;
            gestures.JoystickAxis(0, false);
        }
    }
}
