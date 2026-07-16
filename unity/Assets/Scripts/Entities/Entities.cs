// ══════════════════════════════════════════════════════════════
// Mushmire — Entities.cs
// Enemy AI (8 biome night spirits), friendly creatures (cats,
// charm pets, fireflies, fish), and item drops. Port of web
// entities.js in Unity space (y up).
// ══════════════════════════════════════════════════════════════
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Mushmire
{
    // shared grid movement for small ground critters
    public static class CritterPhysics
    {
        public static bool Collides(WorldData w, float x, float y, float bw, float bh)
        {
            int x0 = Mathf.FloorToInt(x), x1 = Mathf.FloorToInt(x + bw - 0.001f);
            int y0 = Mathf.FloorToInt(y), y1 = Mathf.FloorToInt(y + bh - 0.001f);
            for (int ty = y0; ty <= y1; ty++)
                for (int tx = x0; tx <= x1; tx++)
                    if (w.SolidAt(tx, w.H - 1 - ty)) return true;
            return false;
        }

        // returns (grounded, blockedX); mutates pos/vel
        public static (bool, bool) MoveGround(WorldData w, ref Vector2 pos, ref Vector2 vel, float bw, float bh, float dt)
        {
            bool grounded = false, blockedX = false;
            vel.y = Mathf.Max(vel.y - 40f * dt, -23.8f);
            float dx = vel.x * dt;
            if (!Collides(w, pos.x + dx, pos.y, bw, bh)) pos.x += dx;
            else if (!Collides(w, pos.x + dx, pos.y + 1f, bw, bh)) { pos.x += dx; pos.y += 1f; }
            else blockedX = true;
            float dy = vel.y * dt;
            if (!Collides(w, pos.x, pos.y + dy, bw, bh)) pos.y += dy;
            else { if (vel.y < 0) grounded = true; vel.y = 0; }
            return (grounded, blockedX);
        }
    }

    // ══════════════ ENEMY ══════════════
    public class Enemy : MonoBehaviour
    {
        public EnemyDef def;
        public int hp;
        public bool dead;
        Vector2 vel;
        int facing = 1;
        float hurtT, bobT, jumpCd;
        bool grounded, blockedX;
        SpriteRenderer sr;
        const float BW = 0.7f, BH = 0.56f;

        public static Enemy Spawn(string type, Vector2 pos, Transform parent)
        {
            var go = new GameObject("enemy:" + type);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var e = go.AddComponent<Enemy>();
            e.def = GameData.Enemies[type];
            e.hp = e.def.hp;
            e.sr = go.AddComponent<SpriteRenderer>();
            e.sr.sprite = PixelArtFactory.SpriteFromMap(PixelArtFactory.EnemyArt[type], 2);
            e.sr.sortingOrder = 9;
            e.bobT = Random.value * 6f;
            if (type == "paperLantern")
            {
                var l = go.AddComponent<Light2D>();
                l.lightType = Light2D.LightType.Point;
                l.pointLightOuterRadius = 3f;
                l.color = new Color(1f, 0.5f, 0.3f);
                l.intensity = 0.9f;
            }
            return e;
        }

        public void Tick(float dt, PlayerController player, ParticleFX fx)
        {
            bobT += dt;
            if (hurtT > 0) hurtT -= dt;
            Vector2 pc = player.Center;
            Vector2 me = transform.position;
            Vector2 delta = pc - me;
            float dist = delta.magnitude;
            facing = delta.x >= 0 ? 1 : -1;

            if (def.fly)
            {
                float sway = Mathf.Sin(bobT * 2.4f) * 0.9f;
                if (hurtT <= 0.4f)
                {
                    vel.x = Mathf.Lerp(vel.x, Mathf.Sign(delta.x) * def.speed, Mathf.Min(1, dt * 2));
                    vel.y = Mathf.Lerp(vel.y, Mathf.Sign(delta.y) * def.speed * 0.6f + sway, Mathf.Min(1, dt * 2));
                }
                me += vel * dt;
                transform.position = me;
                sr.color = new Color(1, 1, 1, 0.88f);
            }
            else
            {
                if (hurtT <= 0) vel.x = Mathf.Sign(delta.x) * def.speed;
                jumpCd -= dt;
                Vector2 pos = me - new Vector2(BW / 2, 0);
                (grounded, blockedX) = CritterPhysics.MoveGround(player.world, ref pos, ref vel, BW, BH, dt);
                if (blockedX && grounded && jumpCd <= 0) { vel.y = 14.4f; jumpCd = 0.8f; }
                if (def.id == "stalker" && grounded && dist < 5.6f && Mathf.Abs(delta.y) < 2.5f && jumpCd <= 0)
                { vel.y = 11.3f; vel.x = Mathf.Sign(delta.x) * def.speed * 2.2f; jumpCd = 1.6f; }
                transform.position = pos + new Vector2(BW / 2, 0);
            }

            sr.flipX = facing < 0;
            transform.localPosition += new Vector3(0, def.fly ? Mathf.Sin(bobT * 3f) * 0.15f * dt : 0, 0);

            // touch damage
            if (dist < 0.9f && player.iframes <= 0 && !player.dead)
            {
                if (player.Damage(def.dmg, -Mathf.Sign(delta.x)))
                    fx.Burst(pc, GameData.Blood, 6);
            }
        }

        public void Hit(int dmg, int dir, ParticleFX fx)
        {
            hp -= dmg;
            hurtT = 0.5f;
            vel = new Vector2(dir * 10f, 7.5f);
            fx.Burst(transform.position, GameData.Glow, 5);
            if (hp <= 0) dead = true;
        }
    }

    // ══════════════ FRIENDLY CREATURE ══════════════
    public class Creature : MonoBehaviour
    {
        public string kind;
        public bool follows, floaty;
        public float homeX;
        Vector2 vel;
        int facing = 1;
        float stateT, bobT, petT;
        public string state = "idle";
        bool grounded, blockedX;
        SpriteRenderer sr;
        float BW = 0.85f, BH = 0.5f;

        public static Creature Spawn(string kind, Vector2 pos, bool follows, Transform parent)
        {
            var go = new GameObject("creature:" + kind);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var c = go.AddComponent<Creature>();
            c.kind = kind;
            c.follows = follows;
            c.homeX = pos.x;
            c.floaty = kind is "spriteWisp" or "firefly" or "fishy";
            c.sr = go.AddComponent<SpriteRenderer>();
            c.sr.sprite = PixelArtFactory.SpriteFromMap(PixelArtFactory.CreatureArt[kind], 2, !c.floaty);
            c.sr.sortingOrder = 8;
            c.bobT = Random.value * 7f;
            if (!kind.StartsWith("cat")) { c.BW = 0.5f; c.BH = 0.38f; }
            if (kind is "firefly" or "spriteWisp")
            {
                var l = go.AddComponent<Light2D>();
                l.lightType = Light2D.LightType.Point;
                l.pointLightOuterRadius = 1.7f;
                l.intensity = 0.8f;
                l.color = kind == "firefly" ? new Color(1f, 0.94f, 0.6f) : new Color(0.8f, 0.63f, 1f);
            }
            return c;
        }

        public void Tick(float dt, PlayerController player, ParticleFX fx)
        {
            bobT += dt;
            if (petT > 0)
            {
                petT -= dt;
                if (Random.value < dt * 6) fx.Spawn((Vector2)transform.position + new Vector2(0, 0.4f), new Color(0.91f, 0.48f, 0.56f));
            }

            if (floaty)
            {
                Vector2 anchor = follows ? player.Center : new Vector2(homeX, transform.position.y);
                float tx = anchor.x + Mathf.Sin(bobT * 0.9f) * 1.6f;
                float ty = anchor.y + 0.9f + Mathf.Cos(bobT * 1.3f) * 0.6f;
                var p = transform.position;
                p.x = Mathf.Lerp(p.x, tx, Mathf.Min(1, dt * 2.2f));
                p.y = Mathf.Lerp(p.y, ty, Mathf.Min(1, dt * 2.2f));
                transform.position = p;
                sr.flipX = tx < p.x;
                return;
            }

            stateT -= dt;
            Vector2 pc = player.Center;
            float mx = transform.position.x;

            if (follows)
            {
                float dx = pc.x - mx;
                if (Mathf.Abs(dx) > 31f) { transform.position = pc + Vector2.up * 1.2f; vel = Vector2.zero; }
                else if (Mathf.Abs(dx) > 2.1f) { vel.x = Mathf.Sign(dx) * 3.4f; facing = (int)Mathf.Sign(dx); state = "walk"; }
                else { vel.x = 0; state = "idle"; }
            }
            else if (stateT <= 0)
            {
                stateT = 1.5f + Random.value * 3f;
                float roll = Random.value;
                if (roll < 0.45f) { state = "sit"; vel.x = 0; }
                else if (roll < 0.7f) { state = "idle"; vel.x = 0; }
                else
                {
                    state = "walk";
                    int dir = mx > homeX + 4 ? -1 : mx < homeX - 4 ? 1 : (Random.value < 0.5f ? -1 : 1);
                    vel.x = dir * 1.75f; facing = dir;
                }
            }
            Vector2 pos = (Vector2)transform.position - new Vector2(BW / 2, 0);
            (grounded, blockedX) = CritterPhysics.MoveGround(player.world, ref pos, ref vel, BW, BH, dt);
            if (blockedX && grounded) vel.y = 10.6f;
            transform.position = pos + new Vector2(BW / 2, 0);
            sr.flipX = facing < 0;
            // sitting cats squash a little
            sr.transform.localScale = state == "sit" ? new Vector3(1, 0.8f, 1) : Vector3.one;
        }

        public void Pet(ParticleFX fx)
        {
            petT = 1.6f;
            for (int i = 0; i < 4; i++)
                fx.Spawn((Vector2)transform.position + new Vector2(0, 0.5f), new Color(0.91f, 0.48f, 0.56f));
        }
    }

    // ══════════════ DROP ══════════════
    public class DropItem : MonoBehaviour
    {
        public string id;
        public int n;
        public bool dead;
        Vector2 vel;
        float t;

        public static DropItem Spawn(string id, int n, Vector2 pos, Transform parent, Sprite icon)
        {
            var go = new GameObject("drop:" + id);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * 0.55f;
            var d = go.AddComponent<DropItem>();
            d.id = id; d.n = n;
            d.vel = new Vector2((Random.value - 0.5f) * 3.7f, 5.6f + Random.value * 2.5f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = icon;
            sr.sortingOrder = 7;
            return d;
        }

        public void Tick(float dt, PlayerController player)
        {
            t += dt;
            Vector2 pc = player.Center;
            Vector2 me = transform.position;
            Vector2 delta = pc - me;
            float dist = delta.magnitude;

            if (dist < 2.1f && t > 0.4f)
            {
                vel = delta.normalized * 11.9f;
                me += vel * dt;
            }
            else
            {
                vel.y = Mathf.Max(vel.y - 31f * dt, -18.7f);
                if (!CritterPhysics.Collides(player.world, me.x - 0.2f, me.y - 0.2f + vel.y * dt, 0.4f, 0.4f)) me.y += vel.y * dt;
                else vel.y = 0;
                if (!CritterPhysics.Collides(player.world, me.x - 0.2f + vel.x * dt, me.y - 0.2f, 0.4f, 0.4f)) me.x += vel.x * dt;
                vel.x *= 0.92f;
            }
            // gentle hover bob
            transform.position = me;
            if (dist < 0.75f && t > 0.4f)
            {
                if (player.inventory.Add(id, n)) dead = true;
            }
            if (t > 90) dead = true;
        }
    }

    // ══════════════ PARTICLES ══════════════
    public class ParticleFX : MonoBehaviour
    {
        struct P { public Vector2 pos, vel; public float life; public Color col; public Transform tr; public SpriteRenderer sr; }
        readonly System.Collections.Generic.List<P> live = new();
        readonly System.Collections.Generic.Stack<(Transform, SpriteRenderer)> pool = new();

        public void Spawn(Vector2 pos, Color col, float vyBias = 0)
        {
            if (live.Count > 200) return;
            Transform tr; SpriteRenderer sr;
            if (pool.Count > 0) { (tr, sr) = pool.Pop(); tr.gameObject.SetActive(true); }
            else
            {
                var go = new GameObject("p");
                go.transform.SetParent(transform, false);
                sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = PixelArtFactory.Decor("pixel");
                sr.sortingOrder = 15;
                tr = go.transform;
                tr.localScale = Vector3.one * 0.8f;
            }
            sr.color = col;
            tr.position = pos;
            live.Add(new P
            {
                pos = pos,
                vel = new Vector2((Random.value - 0.5f) * 5.6f, (Random.value - 0.3f) * 5.6f + vyBias),
                life = 0.5f + Random.value * 0.5f,
                col = col, tr = tr, sr = sr,
            });
        }

        public void Burst(Vector2 pos, Color col, int n)
        {
            for (int i = 0; i < n; i++) Spawn(pos, col);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var p = live[i];
                p.pos += p.vel * dt;
                p.vel.y -= 7.5f * dt;
                p.life -= dt;
                p.tr.position = p.pos;
                var c = p.col; c.a = Mathf.Clamp01(p.life * 2);
                p.sr.color = c;
                if (p.life <= 0)
                {
                    p.tr.gameObject.SetActive(false);
                    pool.Push((p.tr, p.sr));
                    live.RemoveAt(i);
                }
                else live[i] = p;
            }
        }
    }
}
