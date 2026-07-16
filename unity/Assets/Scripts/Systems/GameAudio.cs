// ══════════════════════════════════════════════════════════════
// Mushmire — GameAudio.cs
// Every sound is synthesized at boot — soft chip-tone SFX and two
// generative ambient loops (day: plucked pentatonic; night: low
// drone with sparse bells). No audio files, mystical by design.
// ══════════════════════════════════════════════════════════════
using System.Collections.Generic;
using UnityEngine;

namespace Mushmire
{
    public class GameAudio : MonoBehaviour
    {
        const int SR = 22050;
        readonly Dictionary<string, AudioClip> clips = new();
        AudioSource sfxSrc, musicSrc;
        AudioClip dayLoop, nightLoop;
        bool nightPlaying;

        public static GameAudio I { get; private set; }

        public void Init()
        {
            I = this;
            sfxSrc = gameObject.AddComponent<AudioSource>();
            sfxSrc.playOnAwake = false;
            musicSrc = gameObject.AddComponent<AudioSource>();
            musicSrc.playOnAwake = false;
            musicSrc.loop = true;
            musicSrc.volume = 0.30f;
            BuildSfx();
            dayLoop = ComposeDay();
            nightLoop = ComposeNight();
        }

        public void Play(string name, float vol = 1f)
        {
            if (clips.TryGetValue(name, out var c)) sfxSrc.PlayOneShot(c, vol);
        }

        // master volume 0..1 (menu slider); AudioSource.volume scales PlayOneShot too
        public void SetVolume(float v)
        {
            sfxSrc.volume = v;
            musicSrc.volume = 0.30f * v;
        }

        public void SetNight(bool night)
        {
            if (musicSrc.clip == null || night != nightPlaying)
            {
                nightPlaying = night;
                musicSrc.clip = night ? nightLoop : dayLoop;
                musicSrc.Play();
            }
        }

        // ── synth primitives ──
        static float[] Buf(float seconds) => new float[(int)(SR * seconds)];

        static AudioClip Clip(string name, float[] data)
        {
            var c = AudioClip.Create(name, data.Length, 1, SR, false);
            c.SetData(data, 0);
            return c;
        }

        static void Tone(float[] b, float t0, float dur, float f0, float f1, float amp,
            int wave = 0, float attack = 0.005f)
        {
            int start = (int)(t0 * SR), len = (int)(dur * SR);
            float phase = 0;
            for (int i = 0; i < len && start + i < b.Length; i++)
            {
                float t = i / (float)len;
                float f = Mathf.Lerp(f0, f1, t);
                phase += f / SR;
                float env = Mathf.Min(1, i / (attack * SR)) * (1 - t) * (1 - t);
                float s = wave switch
                {
                    0 => Mathf.Sin(phase * Mathf.PI * 2),                                   // sine
                    1 => Mathf.Sign(Mathf.Sin(phase * Mathf.PI * 2)) * 0.6f,                // square
                    2 => (Mathf.PingPong(phase * 2, 1f) * 2 - 1),                           // triangle
                    _ => (phase % 1f) * 2 - 1,                                              // saw
                };
                b[start + i] += s * env * amp;
            }
        }

        static void Noise(float[] b, float t0, float dur, float amp, float lowpass = 0.3f, uint seed = 1)
        {
            int start = (int)(t0 * SR), len = (int)(dur * SR);
            var rng = new Rng(seed);
            float last = 0;
            for (int i = 0; i < len && start + i < b.Length; i++)
            {
                float t = i / (float)len;
                float n = rng.Next() * 2 - 1;
                last += (n - last) * lowpass;
                b[start + i] += last * (1 - t) * (1 - t) * amp;
            }
        }

        static void Normalize(float[] b, float peak = 0.85f)
        {
            float max = 0.0001f;
            foreach (var s in b) max = Mathf.Max(max, Mathf.Abs(s));
            float k = peak / max;
            if (k < 1) for (int i = 0; i < b.Length; i++) b[i] *= k;
        }

        // ── SFX ──
        void BuildSfx()
        {
            void Add(string name, System.Action<float[]> gen, float dur)
            {
                var b = Buf(dur);
                gen(b);
                Normalize(b);
                clips[name] = Clip(name, b);
            }

            Add("jump", b => Tone(b, 0, 0.14f, 300, 520, 0.5f, 1), 0.16f);
            Add("doubleJump", b => { Tone(b, 0, 0.09f, 380, 560, 0.45f, 1); Tone(b, 0.07f, 0.1f, 560, 760, 0.4f, 1); }, 0.2f);
            Add("roll", b => Noise(b, 0, 0.22f, 0.5f, 0.12f, 7), 0.24f);
            Add("dig", b => { Noise(b, 0, 0.07f, 0.6f, 0.4f, 11); Tone(b, 0, 0.06f, 160, 90, 0.3f); }, 0.1f);
            Add("break", b => { Noise(b, 0, 0.16f, 0.7f, 0.5f, 13); Tone(b, 0, 0.12f, 120, 60, 0.5f); }, 0.2f);
            Add("place", b => Tone(b, 0, 0.06f, 220, 180, 0.5f, 2), 0.08f);
            Add("swing", b => Noise(b, 0, 0.12f, 0.35f, 0.6f, 17), 0.14f);
            Add("hit", b => { Tone(b, 0, 0.12f, 210, 130, 0.6f, 1); Noise(b, 0, 0.08f, 0.3f, 0.5f, 19); }, 0.15f);
            Add("hurt", b => Tone(b, 0, 0.2f, 200, 110, 0.6f, 3), 0.22f);
            Add("pickup", b => { Tone(b, 0, 0.07f, 700, 900, 0.4f); Tone(b, 0.06f, 0.09f, 1050, 1400, 0.35f); }, 0.18f);
            Add("craft", b => { Tone(b, 0, 0.1f, 523, 523, 0.4f, 2); Tone(b, 0.09f, 0.14f, 784, 784, 0.4f, 2); }, 0.26f);
            Add("forge", b => { Tone(b, 0, 0.1f, 880, 830, 0.5f); Tone(b, 0.12f, 0.16f, 880, 820, 0.45f); Noise(b, 0, 0.05f, 0.2f, 0.8f, 23); }, 0.32f);
            Add("purr", b =>
            {
                for (float t = 0; t < 0.55f; t += 0.045f) Tone(b, t, 0.04f, 42, 40, 0.5f, 2);
            }, 0.6f);
            Add("heal", b => { Tone(b, 0, 0.1f, 523, 523, 0.35f); Tone(b, 0.08f, 0.1f, 659, 659, 0.35f); Tone(b, 0.16f, 0.16f, 784, 784, 0.35f); }, 0.36f);
            Add("ghost", b => Noise(b, 0, 0.5f, 0.35f, 0.06f, 29), 0.5f);
            Add("death", b => { Tone(b, 0, 0.22f, 392, 392, 0.4f, 2); Tone(b, 0.2f, 0.24f, 311, 311, 0.4f, 2); Tone(b, 0.42f, 0.4f, 233, 220, 0.4f, 2); }, 0.9f);
            Add("charm", b => { Tone(b, 0, 0.09f, 659, 700, 0.35f); Tone(b, 0.08f, 0.1f, 880, 920, 0.3f); Tone(b, 0.16f, 0.2f, 1174, 1250, 0.3f); }, 0.4f);
            Add("uiTap", b => Tone(b, 0, 0.04f, 600, 500, 0.35f, 2), 0.05f);
            Add("splash", b => Noise(b, 0, 0.28f, 0.5f, 0.18f, 31), 0.3f);
        }

        // ── music: generative loops ──
        AudioClip ComposeDay()
        {
            // gentle pentatonic plucks over a soft pad — 16s loop
            var b = Buf(16f);
            float[] scale = { 261.6f, 293.7f, 329.6f, 392.0f, 440.0f, 523.3f };   // C pentatonic
            var rng = new Rng(20260701);
            for (float t = 0; t < 15.4f; t += 0.5f)
            {
                if (rng.Chance(0.55f))
                {
                    float f = scale[rng.Range(0, scale.Length)];
                    Tone(b, t, 0.9f, f, f, 0.16f, 0, 0.01f);
                    Tone(b, t, 0.9f, f * 2, f * 2, 0.05f, 0, 0.01f);   // shimmer octave
                }
            }
            // warm pad root+fifth, very quiet
            for (float t = 0; t < 16f; t += 4f)
            {
                Tone(b, t, 4.2f, 130.8f, 130.8f, 0.05f, 0, 1.2f);
                Tone(b, t, 4.2f, 196.0f, 196.0f, 0.04f, 0, 1.4f);
            }
            Normalize(b, 0.6f);
            return Clip("dayLoop", b);
        }

        AudioClip ComposeNight()
        {
            // low mystical drone + sparse bells — 18s loop
            var b = Buf(18f);
            for (float t = 0; t < 18f; t += 6f)
            {
                Tone(b, t, 6.4f, 55f, 55f, 0.10f, 0, 2.0f);
                Tone(b, t, 6.4f, 82.4f, 82.4f, 0.06f, 0, 2.4f);
            }
            float[] bells = { 523.3f, 587.3f, 784.0f, 880.0f };
            var rng = new Rng(11111);
            for (float t = 1; t < 17f; t += 2.2f)
            {
                if (rng.Chance(0.6f))
                {
                    float f = bells[rng.Range(0, bells.Length)];
                    Tone(b, t, 1.6f, f, f * 0.995f, 0.07f, 0, 0.005f);
                }
            }
            Normalize(b, 0.5f);
            return Clip("nightLoop", b);
        }
    }
}
