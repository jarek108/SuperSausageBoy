using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;

namespace SuperSausageBoy.Tests
{
    /// <summary>
    /// "Console health" gate as PlayMode tests: loads each scene, runs it for a
    /// short while, and asserts (a) exactly one AudioListener is present (catches
    /// the "no audio listener in scene" warning), and (b) no warnings/errors/
    /// exceptions are logged at runtime. This is the automated "check the console
    /// before shipping" step.
    /// </summary>
    [TestFixture]
    public class SceneHealthTests
    {
        static readonly string[] SceneNames =
        {
            "Story", "Level1", "Level2", "Level3", "Level4", "Level5", "Win",
        };

        readonly List<string> _logs = new List<string>();

        void OnLog(string condition, string stack, LogType type)
        {
            if (type == LogType.Warning || type == LogType.Error ||
                type == LogType.Exception || type == LogType.Assert)
            {
                _logs.Add($"{type}: {condition}");
            }
        }

        [UnityTest]
        public IEnumerator EveryScene_HasExactlyOneAudioListener()
        {
            foreach (var name in SceneNames)
            {
                SceneManager.LoadScene(name);
                yield return null;
                yield return null;

                var listeners = Object.FindObjectsOfType<AudioListener>(true);
                Assert.AreEqual(1, listeners.Length,
                    $"Scene '{name}' should have exactly 1 AudioListener, found {listeners.Length}.");
            }
        }

        [UnityTest]
        public IEnumerator EveryScene_RuntimeConsoleIsClean()
        {
            var problems = new List<string>();

            foreach (var name in SceneNames)
            {
                _logs.Clear();
                Application.logMessageReceived += OnLog;

                SceneManager.LoadScene(name);
                // Run the scene for a short slice of game time.
                float t = 0f;
                while (t < 0.5f) { t += Time.deltaTime; yield return null; }

                Application.logMessageReceived -= OnLog;

                foreach (var l in _logs)
                    problems.Add($"[{name}] {l}");
            }

            Assert.IsEmpty(problems,
                "Runtime console had warnings/errors:\n" + string.Join("\n", problems));
        }
    }
}
