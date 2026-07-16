// ══════════════════════════════════════════════════════════════
// Mushmire — TouchGestures.cs
// Input state shared by on-screen buttons (wired by the HUD) and
// a keyboard fallback for editor testing. Double-tap gestures:
// ◀/▶ ×2 = sprint · ⬆ mid-air = double jump · ⬇ ×2 = roll.
// ══════════════════════════════════════════════════════════════
using UnityEngine;

namespace Mushmire
{
    public class TouchGestures : MonoBehaviour
    {
        const float DoubleTapWindow = 0.28f;

        public bool left, right, jump, down, sprint, attackHeld;
        public bool jumpPressed, rollPressed, attackPressed;    // edges, consumed each frame

        public Vector2? worldTap;      // screen px, one-shot
        public Vector2? worldHold;     // screen px while a finger stays on the world

        readonly System.Collections.Generic.Dictionary<string, float> lastTapAt = new();
        bool joyActive;

        // 0..1, set from the menu panel: higher = joystick reacts to smaller pushes
        public float sensitivity = 0.5f;
        float DeadZone => Mathf.Lerp(0.35f, 0.10f, sensitivity);
        float SprintPoint => Mathf.Lerp(0.95f, 0.55f, sensitivity);

        // driven by the virtual joystick every frame while touched
        public void JoystickAxis(float x, bool active)
        {
            if (!active)
            {
                if (joyActive) { left = right = false; sprint = false; joyActive = false; }
                return;
            }
            joyActive = true;
            left = x < -DeadZone;
            right = x > DeadZone;
            sprint = Mathf.Abs(x) > SprintPoint;   // push toward the edge to sprint
        }

        public void Consume()
        {
            jumpPressed = rollPressed = attackPressed = false;
            worldTap = null;
        }

        public void Press(string key)
        {
            float now = Time.unscaledTime;
            bool dbl = lastTapAt.TryGetValue(key, out var t) && now - t < DoubleTapWindow;
            lastTapAt[key] = now;
            switch (key)
            {
                case "left": left = true; if (dbl) sprint = true; break;
                case "right": right = true; if (dbl) sprint = true; break;
                case "jump": jump = true; jumpPressed = true; break;
                case "down": down = true; if (dbl) rollPressed = true; break;
                case "attack": attackPressed = true; attackHeld = true; break;
            }
        }

        public void Release(string key)
        {
            switch (key)
            {
                case "left": left = false; if (!right) sprint = false; break;
                case "right": right = false; if (!left) sprint = false; break;
                case "jump": jump = false; break;
                case "down": down = false; break;
                case "attack": attackHeld = false; break;
            }
        }

        void Update()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            // keyboard fallback for desktop testing
            Check(KeyCode.LeftArrow, "left"); Check(KeyCode.A, "left");
            Check(KeyCode.RightArrow, "right"); Check(KeyCode.D, "right");
            Check(KeyCode.UpArrow, "jump"); Check(KeyCode.W, "jump"); Check(KeyCode.Space, "jump");
            Check(KeyCode.DownArrow, "down"); Check(KeyCode.S, "down");
            Check(KeyCode.K, "attack"); Check(KeyCode.X, "attack");
            if (Input.GetKeyDown(KeyCode.LeftShift)) sprint = true;
            if (Input.GetKeyUp(KeyCode.LeftShift)) sprint = false;

            if (Input.GetMouseButtonDown(0) && !UIOver()) { worldTap = Input.mousePosition; worldHold = Input.mousePosition; }
            else if (Input.GetMouseButton(0) && worldHold.HasValue) worldHold = Input.mousePosition;
            if (Input.GetMouseButtonUp(0)) worldHold = null;
#endif
            // touch: first finger not over UI = world pointer
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                if (UIOver(t.fingerId)) continue;
                if (t.phase == TouchPhase.Began) { worldTap = t.position; worldHold = t.position; }
                else if ((t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary) && worldHold.HasValue)
                    worldHold = t.position;
                else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                    worldHold = null;
                break;
            }
        }

        void Check(KeyCode k, string key)
        {
            if (Input.GetKeyDown(k)) Press(key);
            if (Input.GetKeyUp(k)) Release(key);
        }

        static bool UIOver(int fingerId = -1)
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null) return false;
            return fingerId >= 0 ? es.IsPointerOverGameObject(fingerId) : es.IsPointerOverGameObject();
        }
    }
}
