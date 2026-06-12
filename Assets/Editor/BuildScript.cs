#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SuperSausageBoy.EditorTools
{
    /// <summary>
    /// Builds standalone players. Run via -executeMethod in batch mode.
    ///   BuildLinux   -> Builds/Linux/SuperSausageBoy.x86_64
    ///   BuildWindows -> Builds/Windows/SuperSausageBoy.exe (needs Windows module)
    /// </summary>
    public static class BuildScript
    {
        static string[] Scenes => new[]
        {
            "Assets/Scenes/Level1.unity",
            "Assets/Scenes/Level2.unity",
            "Assets/Scenes/Level3.unity",
            "Assets/Scenes/Level4.unity",
            "Assets/Scenes/Level5.unity",
            "Assets/Scenes/Win.unity",
        };

        public static void BuildLinux()
        {
            string dir = Path.Combine(Application.dataPath, "../Builds/Linux");
            Directory.CreateDirectory(dir);
            var opts = new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = Path.Combine(dir, "SuperSausageBoy.x86_64"),
                target = BuildTarget.StandaloneLinux64,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log($"[SSB] Linux build result: {report.summary.result}, " +
                      $"size={report.summary.totalSize} bytes, " +
                      $"errors={report.summary.totalErrors}");
        }

        public static void BuildWindows()
        {
            string dir = Path.Combine(Application.dataPath, "../Builds/Windows");
            Directory.CreateDirectory(dir);
            var opts = new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = Path.Combine(dir, "SuperSausageBoy.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log($"[SSB] Windows build result: {report.summary.result}, " +
                      $"errors={report.summary.totalErrors}");
        }
    }
}
#endif
