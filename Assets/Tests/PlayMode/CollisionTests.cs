using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using SuperSausageBoy.Player;
using SuperSausageBoy.Level;

namespace SuperSausageBoy.Tests
{
    /// <summary>
    /// PlayMode reproduction + regression tests for the trigger/collision bugs:
    ///   - Touching a Hazard (saw/spike) must kill the player.
    ///   - Reaching the Goal must complete the level.
    ///   - Falling below the level must kill the player (fall death).
    ///
    /// These load the REAL Level1 scene and use the real player instance, so they
    /// exercise the actual physics requirement: a Rigidbody2D on the player is
    /// needed for OnTriggerEnter2D to fire with the kinematic raycast controller.
    /// Written to FAIL on the buggy build and PASS once fixed.
    /// </summary>
    [TestFixture]
    public class CollisionTests
    {
        readonly System.Collections.Generic.List<GameObject> _spawned = new();

        IEnumerator LoadLevel1AndGetPlayer(System.Action<PlayerHealth> onReady)
        {
            SceneManager.LoadScene("Level1");
            yield return null;
            yield return null;
            var health = Object.FindObjectOfType<PlayerHealth>();
            Assert.IsNotNull(health, "Player not found in Level1.");
            // Disable the movement so the player stays where we put it.
            var move = health.GetComponent<PlayerMovement>();
            if (move != null) move.enabled = false;
            onReady(health);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned) if (go != null) Object.Destroy(go);
            _spawned.Clear();
        }

        GameObject MakeTrigger(string name, string tag, Vector3 pos)
        {
            var go = new GameObject(name);
            if (!string.IsNullOrEmpty(tag)) go.tag = tag;
            go.transform.position = pos;
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(2f, 2f);
            _spawned.Add(go);
            return go;
        }

        // ---- 1. Hazard kills the player ----
        [UnityTest]
        public IEnumerator TouchingHazard_KillsPlayer()
        {
            PlayerHealth health = null;
            yield return LoadLevel1AndGetPlayer(h => health = h);

            bool died = false;
            health.OnDeath += () => died = true;

            // Drop a hazard trigger right on top of the player.
            MakeTrigger("TEST_Hazard", "Hazard", health.transform.position);
            Physics2D.SyncTransforms();

            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();
            yield return null;

            Assert.IsTrue(died, "Touching a Hazard trigger should kill the player.");
        }

        // ---- 2. Goal completes the level ----
        [UnityTest]
        public IEnumerator ReachingGoal_CompletesLevel()
        {
            PlayerHealth health = null;
            yield return LoadLevel1AndGetPlayer(h => health = h);

            var lm = LevelManager.Instance;
            Assert.IsNotNull(lm, "LevelManager should exist in Level1.");
            int before = lm.CurrentLevelIndex;

            // Place a Goal trigger overlapping the player.
            var goalGo = new GameObject("TEST_Goal");
            goalGo.transform.position = health.transform.position;
            var gcol = goalGo.AddComponent<BoxCollider2D>();
            gcol.size = new Vector2(2f, 2f);
            goalGo.AddComponent<Goal>(); // Goal.Awake forces isTrigger
            _spawned.Add(goalGo);
            Physics2D.SyncTransforms();

            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();

            Assert.Greater(lm.CurrentLevelIndex, before,
                "Reaching the Goal should advance the level.");
        }

        // ---- 3. Falling below the level kills the player ----
        [UnityTest]
        public IEnumerator FallingBelowLevel_KillsPlayer()
        {
            PlayerHealth health = null;
            yield return LoadLevel1AndGetPlayer(h => health = h);

            bool died = false;
            health.OnDeath += () => died = true;

            // Teleport far below any geometry to simulate falling off the world.
            health.transform.position = new Vector3(0, -500f, 0);
            Physics2D.SyncTransforms();

            float t = 0f;
            while (t < 1.5f && !died) { t += Time.deltaTime; yield return null; }

            Assert.IsTrue(died, "Falling far below the level should kill the player.");
        }
    }
}
