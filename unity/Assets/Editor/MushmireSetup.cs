// ══════════════════════════════════════════════════════════════
// Mushmire — MushmireSetup.cs (Editor only)
// Self-configures the project on first open: URP with 2D
// renderer, the Main scene (camera + Bootstrap), build settings,
// and iOS player settings (landscape, bundle id).
// Re-run any time via menu: Mushmire → Setup Project.
// ══════════════════════════════════════════════════════════════
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Mushmire.EditorTools
{
    public static class MushmireSetup
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string SettingsDir = "Assets/Settings";
        const string PipelinePath = "Assets/Settings/MushmireURP.asset";
        const string RendererPath = "Assets/Settings/MushmireRenderer2D.asset";

        [InitializeOnLoadMethod]
        static void AutoSetup()
        {
            // defer until the editor is fully loaded
            EditorApplication.delayCall += () =>
            {
                if (System.IO.File.Exists(ScenePath)) return;   // already configured
                Run();
            };
        }

        [MenuItem("Mushmire/Setup Project")]
        public static void Run()
        {
            try
            {
                SetupURP();
                SetupScene();
                SetupPlayerSettings();
                Debug.Log("Mushmire ✦ project setup complete. Open Assets/Scenes/Main.unity and press Play.");
            }
            catch (System.Exception e)
            {
                Debug.LogError("Mushmire setup hit a snag: " + e.Message +
                    "\nFallback: create a URP Asset (with 2D Renderer) via Assets → Create → Rendering, " +
                    "assign it in Project Settings → Graphics, then run Mushmire → Setup Project again.");
            }
        }

        static void SetupURP()
        {
            if (!System.IO.Directory.Exists(SettingsDir))
                System.IO.Directory.CreateDirectory(SettingsDir);

            var rendererData = AssetDatabase.LoadAssetAtPath<Renderer2DData>(RendererPath);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<Renderer2DData>();
                AssetDatabase.CreateAsset(rendererData, RendererPath);
            }
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            AssetDatabase.SaveAssets();
        }

        static void SetupScene()
        {
            if (!System.IO.Directory.Exists("Assets/Scenes"))
                System.IO.Directory.CreateDirectory("Assets/Scenes");

            if (!System.IO.File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                var camGO = new GameObject("Main Camera") { tag = "MainCamera" };
                var cam = camGO.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 6.5f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = GameData.Hex("#241a36");
                camGO.transform.position = new Vector3(450, 110, -10);
                camGO.AddComponent<AudioListener>();

                var boot = new GameObject("Bootstrap");
                boot.AddComponent<Bootstrap>();

                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static void SetupPlayerSettings()
        {
            PlayerSettings.productName = "Mushmire";
            PlayerSettings.companyName = "Christine";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.christine.mushmire");

            // landscape only — the empire is wide
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.statusBarHidden = true;

            PlayerSettings.iOS.targetOSVersionString = "13.0";
            PlayerSettings.iOS.requiresFullScreen = true;
        }
    }
}
