// ══════════════════════════════════════════════════════════════
// Mushmire — Bootstrap.cs
// The only component in the scene. Builds the entire game at
// runtime: systems, world, player, UI. Code-first by design so
// the project has no fragile serialized assets.
// ══════════════════════════════════════════════════════════════
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Mushmire
{
    public class Bootstrap : MonoBehaviour
    {
        Camera cam;
        WorldData world;
        WorldRenderer worldRenderer;
        PlayerController player;
        GameController game;
        TouchGestures gestures;
        DayNight dayNight;
        ParticleFX fx;
        GameAudio audioSys;
        TitleScreen title;
        HudController hud;
        PanelsController panels;

        void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            GameData.Build();
            UiFactory.EnsureEventSystem();

            // ── camera ──
            cam = Camera.main;
            if (cam == null)
            {
                var camGO = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGO.AddComponent<Camera>();
            }
            cam.orthographic = true;
            cam.orthographicSize = 6.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = GameData.Ink2;
            cam.transform.position = new Vector3(450, 110, -10);
            if (cam.GetComponent<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();

            // ── global light (URP 2D) ──
            var lightGO = new GameObject("GlobalLight");
            var globalLight = lightGO.AddComponent<Light2D>();
            globalLight.lightType = Light2D.LightType.Global;
            globalLight.intensity = 1f;

            // ── systems ──
            audioSys = gameObject.AddComponent<GameAudio>();
            audioSys.Init();
            gestures = gameObject.AddComponent<TouchGestures>();
            fx = new GameObject("Particles").AddComponent<ParticleFX>();

            world = new WorldData();
            worldRenderer = new GameObject("WorldRenderer").AddComponent<WorldRenderer>();
            worldRenderer.Init(world, cam);

            dayNight = new GameObject("DayNight").AddComponent<DayNight>();
            dayNight.Init(cam, world, globalLight);

            var playerGO = new GameObject("Player");
            player = playerGO.AddComponent<PlayerController>();
            player.Init(world, new CharacterLook());
            // small aura so the player is never pitch black
            var auraGO = new GameObject("PlayerAura");
            auraGO.transform.SetParent(playerGO.transform, false);
            auraGO.transform.localPosition = new Vector3(0, 0.7f, 0);
            var aura = auraGO.AddComponent<Light2D>();
            aura.lightType = Light2D.LightType.Point;
            aura.pointLightOuterRadius = 4.5f;
            aura.intensity = 0.55f;
            aura.color = new Color(0.95f, 0.9f, 0.85f);

            game = gameObject.AddComponent<GameController>();
            game.Init(world, worldRenderer, player, gestures, dayNight, fx, cam);

            // ── UI ──
            hud = new GameObject("HUD").AddComponent<HudController>();
            hud.Build(game, gestures);
            panels = new GameObject("Panels").AddComponent<PanelsController>();
            panels.Build(game);
            title = new GameObject("Title").AddComponent<TitleScreen>();
            title.Build(SaveSystem.HasSave());

            hud.Show(false);

            // ── flow wiring ──
            title.onManual = () => panels.ShowManual();
            title.onNewJourney = () => panels.ShowCharacterSelect();
            title.onContinue = () =>
            {
                var dto = SaveSystem.Load();
                if (dto == null) return;
                title.Show(false);
                hud.Show(true);
                game.ContinueGame(dto);
                hud.Refresh();
            };
            panels.onBegin = look =>
            {
                title.Show(false);
                hud.Show(true);
                game.StartNewGame(look);
                hud.Refresh();
            };
            panels.onRespawn = () => game.Respawn();
            game.OnPlayerDied += () => panels.ShowDeath();

            hud.onPack = () => panels.ShowInventory();
            hud.onCraft = () => panels.ShowCraft();
            hud.onForge = () => panels.ShowForge();
            hud.onMenu = () => panels.ShowMenu();
            panels.onReturnToMenu = () =>
            {
                game.StopToMenu();
                hud.Show(false);
                title.SetContinueVisible(SaveSystem.HasSave());
                title.Show(true);
            };

            // ── saved settings ──
            audioSys.SetVolume(PlayerPrefs.GetFloat("mm_volume", 0.8f));
            gestures.sensitivity = PlayerPrefs.GetFloat("mm_sens", 0.5f);
        }
    }
}
