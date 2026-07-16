// ══════════════════════════════════════════════════════════════
// Mushmire — BuildScript.cs (Editor only)
// CLI entry point for headless builds:
//   Unity -batchmode -quit -projectPath unity \
//         -executeMethod Mushmire.EditorTools.BuildScript.BuildIOS
// Produces an Xcode project in unity/Builds/iOS.
// ══════════════════════════════════════════════════════════════
using UnityEditor;
using UnityEngine;

namespace Mushmire.EditorTools
{
    public static class BuildScript
    {
        public static void BuildIOS()
        {
            MushmireSetup.Run();
            var report = BuildPipeline.BuildPlayer(
                new[] { "Assets/Scenes/Main.unity" },
                "Builds/iOS",
                BuildTarget.iOS,
                BuildOptions.None);
            Debug.Log("Mushmire iOS build: " + report.summary.result +
                " · " + report.summary.totalErrors + " errors");
            if (report.summary.totalErrors > 0) EditorApplication.Exit(1);
        }
    }
}
