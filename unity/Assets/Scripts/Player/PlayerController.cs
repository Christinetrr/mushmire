// ══════════════════════════════════════════════════════════════
// Mushmire — PlayerController.cs
// Kinematic tile physics (port of web player.js, y-up):
// walk/sprint, jump/double-jump, duck, roll (i-frames), swim,
// breath, auto step-up on 1-tile ledges.
// ══════════════════════════════════════════════════════════════
using UnityEngine;

namespace Mushmire
{
    public class PlayerController : MonoBehaviour
    {
        const float Walk = 5.5f, Sprint = 9.4f, DuckSpeed = 2.6f;
        const float JumpV = 14.9f, Grav = 40f, MaxFall = 23.8f;
        const float RollSpeed = 13.4f, RollTime = 0.34f;
        const float SwimGrav = 8.1f, SwimUp = 7.5f, MaxSink = 4.4f;

        public WorldData world;
        public CharacterLook look;
        public Inventory inventory = new();

        [HideInInspector] public Vector2 vel;
        [HideInInspector] public int facing = 1;
        [HideInInspector] public bool grounded, inWater, ducking, sprinting, dead;
        [HideInInspector] public float breath = 1, iframes, rollT, swingT;
        public const float SwingDur = 0.28f;
        [HideInInspector] public bool usedDoubleJump;
        [HideInInspector] public int hp = 10, hpMax = 10;

        public float WBody = 0.56f;
        public float HFull = 1.31f, HDuck = 0.81f;
        public float HBody => ducking ? HDuck : HFull;

        SpriteRenderer body;
        SpriteRenderer held;
        float animClock, drownT;
        int rollDir = 1;

        public System.Action OnHurt, OnJump, OnDoubleJump, OnRoll, OnDied;

        public void Init(WorldData w, CharacterLook lk)
        {
            world = w; look = lk;
            body = gameObject.AddComponent<SpriteRenderer>();
            body.sortingOrder = 10;
            body.sprite = PixelArtFactory.CharacterSprite(look, 0);

            var heldGO = new GameObject("Held");
            heldGO.transform.SetParent(transform, false);
            heldGO.transform.localPosition = new Vector3(0.42f, 0.62f, 0);
            heldGO.transform.localScale = Vector3.one * 0.75f;
            held = heldGO.AddComponent<SpriteRenderer>();
            held.sortingOrder = 11;
        }

        public Vector2 Center => (Vector2)transform.position + new Vector2(0, HBody / 2);

        public void Respawn()
        {
            // clear any decoration (trunk, leaves, bamboo…) overlapping the spawn
            // column — protects old saves whose spawn landed inside a tree
            for (int dy = -2; dy <= 4; dy++)
            {
                int gy = world.spawn.y + dy;
                Tile t = world.Get(world.spawn.x, gy);
                if (t != Tile.Air && t != Tile.Water && !GameData.Solid(t))
                    world.Set(world.spawn.x, gy, Tile.Air);
            }
            transform.position = new Vector3(world.spawn.x + 0.5f, world.UnityY(world.spawn.y), 0);
            hp = hpMax; dead = false; vel = Vector2.zero; breath = 1; iframes = 2;
            EnsureNotBuried();
        }

        // if the body overlaps solid tiles (stale save position, changed world…),
        // lift straight up, or failing that pop to a nearby column's surface
        public void EnsureNotBuried()
        {
            var p = transform.position;
            if (!Collides(p.x - WBody / 2, p.y, WBody, HFull)) return;
            for (int dy = 1; dy <= 14; dy++)
                if (TryPlace(p.x, p.y + dy)) return;
            for (int dx = 1; dx <= 5; dx++)
                foreach (int dir in new[] { -1, 1 })
                {
                    int tx = Mathf.FloorToInt(p.x) + dx * dir;
                    int surf = world.SurfaceAt(tx);
                    if (TryPlace(tx + 0.5f, world.UnityY(surf) + 1.02f)) return;
                }
        }

        bool TryPlace(float x, float y)
        {
            if (Collides(x - WBody / 2, y, WBody, HFull)) return false;
            transform.position = new Vector3(x, y, 0);
            vel = Vector2.zero;
            return true;
        }

        // ── collision vs tile grid ──
        bool Collides(float x, float y, float w, float h)
        {
            int x0 = Mathf.FloorToInt(x), x1 = Mathf.FloorToInt(x + w - 0.001f);
            int y0 = Mathf.FloorToInt(y), y1 = Mathf.FloorToInt(y + h - 0.001f);
            for (int ty = y0; ty <= y1; ty++)
                for (int tx = x0; tx <= x1; tx++)
                    if (world.SolidAt(tx, world.H - 1 - ty)) return true;
            return false;
        }

        bool MoveAxis(float dx, float dy)
        {
            bool blocked = false;
            var p = transform.position;
            float px = p.x - WBody / 2, py = p.y;
            const float step = 0.25f;
            while (dx != 0 || dy != 0)
            {
                float sx = Mathf.Abs(dx) > step ? Mathf.Sign(dx) * step : dx;
                float sy = Mathf.Abs(dy) > step ? Mathf.Sign(dy) * step : dy;
                dx -= sx; dy -= sy;
                if (!Collides(px + sx, py + sy, WBody, HBody)) { px += sx; py += sy; continue; }
                // auto step-up on 1-tile ledges (the fix that made the web version playable)
                if (sx != 0 && grounded)
                {
                    bool stepped = false;
                    for (float k = 0.25f; k <= 1.01f; k += 0.25f)
                        if (!Collides(px + sx, py + k, WBody, HBody)) { px += sx; py += k; stepped = true; break; }
                    if (stepped) continue;
                }
                // snug pixel fit
                float ux = Mathf.Sign(sx) * 0.03f, uy = Mathf.Sign(sy) * 0.03f;
                for (int i = 0; i < 9; i++)
                {
                    if (sx != 0 && !Collides(px + ux, py, WBody, HBody)) px += ux;
                    else if (sy != 0 && !Collides(px, py + uy, WBody, HBody)) py += uy;
                    else break;
                }
                blocked = true;
                if (sy != 0) vel.y = 0;
                if (sx != 0 && rollT <= 0) vel.x = 0;
                dx = 0; dy = 0;
            }
            transform.position = new Vector3(px + WBody / 2, py, 0);
            return blocked;
        }

        public void Tick(float dt, TouchGestures input)
        {
            if (dead) return;
            animClock += dt;

            var pos = transform.position;
            int gcx = Mathf.FloorToInt(pos.x);
            inWater = world.LiquidAt(gcx, world.GridY(pos.y + HBody * 0.5f));
            bool headUnder = world.LiquidAt(gcx, world.GridY(pos.y + HBody - 0.15f));

            if (headUnder)
            {
                breath -= dt / 9f;
                if (breath <= 0)
                {
                    breath = 0; drownT += dt;
                    if (drownT > 1f) { drownT = 0; Damage(1, 0); }
                }
            }
            else breath = Mathf.Min(1, breath + dt * 0.6f);

            // duck
            bool wantDuck = input.down && grounded && rollT <= 0 && !inWater;
            if (wantDuck && !ducking) ducking = true;
            if (!wantDuck && ducking)
            {
                if (!Collides(pos.x - WBody / 2, pos.y, WBody, HFull)) ducking = false;
            }

            // roll
            if (input.rollPressed && grounded && rollT <= 0 && !ducking)
            {
                rollT = RollTime;
                rollDir = input.left ? -1 : input.right ? 1 : facing;
                iframes = Mathf.Max(iframes, RollTime + 0.08f);
                OnRoll?.Invoke();
            }

            // horizontal
            float target = 0;
            sprinting = input.sprint && (input.left || input.right) && !ducking;
            float spd = ducking ? DuckSpeed : sprinting ? Sprint : Walk;
            if (input.left) { target = -spd; facing = -1; }
            if (input.right) { target = spd; facing = 1; }
            if (rollT > 0)
            {
                rollT -= dt;
                target = rollDir * RollSpeed;
                facing = rollDir;
            }
            float accel = grounded ? 56f : 35f;
            if (inWater) target *= 0.65f;
            if (target > vel.x) vel.x = Mathf.Min(target, vel.x + accel * dt);
            else if (target < vel.x) vel.x = Mathf.Max(target, vel.x - accel * dt);
            else
            {
                float fric = grounded ? 44f : 7.5f;
                vel.x = Mathf.MoveTowards(vel.x, 0, fric * dt);
            }

            // vertical
            if (inWater)
            {
                vel.y -= SwimGrav * dt;
                if (input.jumpPressed || (input.jump && vel.y < 2.5f)) vel.y = SwimUp;
                if (vel.y < -MaxSink) vel.y = -MaxSink;
                usedDoubleJump = false;
            }
            else
            {
                vel.y -= Grav * dt;
                if (vel.y < -MaxFall) vel.y = -MaxFall;
                if (input.jumpPressed)
                {
                    if (grounded && !ducking) { vel.y = JumpV; grounded = false; OnJump?.Invoke(); }
                    else if (!grounded && !usedDoubleJump)
                    {
                        vel.y = JumpV * 0.88f; usedDoubleJump = true; OnDoubleJump?.Invoke();
                    }
                }
            }

            MoveAxis(vel.x * dt, 0);
            bool wasFalling = vel.y < 0;
            bool hitGround = MoveAxis(0, vel.y * dt);
            grounded = hitGround && wasFalling;
            if (grounded) usedDoubleJump = false;

            if (iframes > 0) iframes -= dt;
            if (swingT > 0) swingT -= dt;

            if (transform.position.y < -15) Damage(99, 0);
            UpdateVisual();
        }

        public bool Swing()
        {
            if (swingT > 0) return false;
            swingT = SwingDur;
            return true;
        }

        public bool Damage(int n, float fromDir)
        {
            if (iframes > 0 || dead) return false;
            hp -= n;
            iframes = 0.8f;
            vel = new Vector2(fromDir * 8f, 7.5f);
            OnHurt?.Invoke();
            if (hp <= 0) { hp = 0; dead = true; OnDied?.Invoke(); }
            return true;
        }
        public void Heal(int n) => hp = Mathf.Min(hpMax, hp + n);

        public void SetHeldSprite(Sprite s) => held.sprite = s;

        void UpdateVisual()
        {
            int frame = 0;
            bool walking = Mathf.Abs(vel.x) > 0.8f && grounded;
            if (ducking) frame = 3;
            else if (!grounded) frame = 4;
            else if (walking) frame = (int)(animClock * (sprinting ? 12 : 8)) % 2 == 0 ? 1 : 2;
            body.sprite = PixelArtFactory.CharacterSprite(look, frame);
            body.flipX = facing < 0;
            held.flipX = facing < 0;
            held.transform.localPosition = new Vector3(0.42f * facing, ducking ? 0.35f : 0.62f, 0);

            // swing arc
            if (swingT > 0)
            {
                float f = 1 - swingT / SwingDur;
                held.transform.localRotation = Quaternion.Euler(0, 0, facing * (70 - f * 160));
            }
            else held.transform.localRotation = Quaternion.identity;

            // roll spin
            if (rollT > 0)
                body.transform.localRotation = Quaternion.Euler(0, 0, -rollDir * (1 - rollT / RollTime) * 360f);
            else
                body.transform.localRotation = Quaternion.identity;

            // i-frame blink
            bool blink = iframes > 0 && (int)(Time.time * 12) % 2 == 0;
            var c = body.color; c.a = blink ? 0.45f : 1f;
            body.color = c; held.color = c;
        }
    }

    // ══════════════ INVENTORY ══════════════
    [System.Serializable]
    public class InvSlot { public string id; public int count; }

    public class Inventory
    {
        public InvSlot[] slots = new InvSlot[24];
        public int sel;

        public InvSlot Selected => slots[sel];

        public bool Add(string id, int n = 1)
        {
            int max = id.StartsWith("custom:") ? 1 : (GameData.Items.TryGetValue(id, out var d) ? d.stack : 99);
            for (int i = 0; i < slots.Length && n > 0; i++)
            {
                var s = slots[i];
                if (s != null && s.id == id && s.count < max)
                {
                    int take = Mathf.Min(n, max - s.count);
                    s.count += take; n -= take;
                }
            }
            for (int i = 0; i < slots.Length && n > 0; i++)
            {
                if (slots[i] == null)
                {
                    int take = Mathf.Min(n, max);
                    slots[i] = new InvSlot { id = id, count = take };
                    n -= take;
                }
            }
            return n == 0;
        }

        public int Count(string id)
        {
            int c = 0;
            foreach (var s in slots) if (s != null && s.id == id) c += s.count;
            return c;
        }

        public void Remove(string id, int n = 1)
        {
            for (int i = 0; i < slots.Length && n > 0; i++)
            {
                var s = slots[i];
                if (s != null && s.id == id)
                {
                    int take = Mathf.Min(n, s.count);
                    s.count -= take; n -= take;
                    if (s.count <= 0) slots[i] = null;
                }
            }
        }

        public bool HasAll((string id, int n)[] cost)
        {
            foreach (var (id, n) in cost) if (Count(id) < n) return false;
            return true;
        }
        public void PayAll((string id, int n)[] cost)
        {
            foreach (var (id, n) in cost) Remove(id, n);
        }
    }
}
