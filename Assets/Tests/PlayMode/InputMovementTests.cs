using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using SuperSausageBoy.Player;

namespace SuperSausageBoy.Tests
{
    /// <summary>
    /// PlayMode integration tests for the REAL Input System path — the layer the
    /// edit-mode PlayTestHarness bypasses. These run in actual play mode (where
    /// PlayerInput + action composites resolve correctly), simulate a virtual
    /// keyboard via InputTestFixture, and assert the character actually moves.
    ///
    /// This suite is what would have caught the original shipping bug: PlayerInput
    /// set to InvokeCSharpEvents with nothing subscribed to onActionTriggered, so
    /// keyboard input produced no movement.
    ///
    /// NOTE: in -batchmode the play loop runs at a tiny, variable deltaTime
    /// (~0.001s), so we hold inputs for a fixed amount of *game time* (accumulating
    /// Time.deltaTime) rather than a frame count.
    ///
    /// Run headless:
    ///   Unity -runTests -testPlatform PlayMode -testResults results.xml
    /// </summary>
    [TestFixture]
    public class InputMovementTests : InputTestFixture
    {
        Keyboard _keyboard;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
        }

        IEnumerator LoadLevel1()
        {
            SceneManager.LoadScene("Level1");
            yield return null; // allow Awake/OnEnable
            yield return null; // allow Start
        }

        static PlayerMovement FindPlayer() => Object.FindObjectOfType<PlayerMovement>();

        // Hold for a fixed amount of GAME time, returning the max |horizontal vel|
        // and the peak Y seen during the hold.
        static IEnumerator Hold(PlayerMovement move, float seconds,
                                System.Action<float> onMaxVx = null,
                                System.Action<float> onPeakY = null)
        {
            float t = 0f, maxVx = 0f, peakY = float.NegativeInfinity;
            while (t < seconds)
            {
                maxVx = Mathf.Max(maxVx, Mathf.Abs(move.Velocity.x));
                peakY = Mathf.Max(peakY, move.transform.position.y);
                t += Time.deltaTime;
                yield return null;
            }
            onMaxVx?.Invoke(maxVx);
            onPeakY?.Invoke(peakY);
        }

        [UnityTest]
        public IEnumerator RightArrow_MovesPlayerRight()
        {
            yield return LoadLevel1();
            var move = FindPlayer();
            Assert.IsNotNull(move, "Player not found in Level1");

            float startX = move.transform.position.x;
            Press(_keyboard.rightArrowKey);
            yield return Hold(move, 0.6f);
            Release(_keyboard.rightArrowKey);

            float endX = move.transform.position.x;
            Assert.Greater(endX, startX + 0.5f,
                $"Right arrow should move player right (startX={startX:0.00}, endX={endX:0.00})");
        }

        [UnityTest]
        public IEnumerator LeftArrow_MovesPlayerLeft()
        {
            yield return LoadLevel1();
            var move = FindPlayer();
            Assert.IsNotNull(move);

            float startX = move.transform.position.x;
            Press(_keyboard.leftArrowKey);
            yield return Hold(move, 0.6f);
            Release(_keyboard.leftArrowKey);

            float endX = move.transform.position.x;
            Assert.Less(endX, startX - 0.5f,
                $"Left arrow should move player left (startX={startX:0.00}, endX={endX:0.00})");
        }

        [UnityTest]
        public IEnumerator AD_Keys_AlsoMovePlayer()
        {
            yield return LoadLevel1();
            var move = FindPlayer();
            Assert.IsNotNull(move);

            float startX = move.transform.position.x;
            Press(_keyboard.dKey);
            yield return Hold(move, 0.6f);
            Release(_keyboard.dKey);

            Assert.Greater(move.transform.position.x, startX + 0.5f,
                "D key should move player right");
        }

        [UnityTest]
        public IEnumerator Space_TriggersJump()
        {
            yield return LoadLevel1();
            var move = FindPlayer();
            Assert.IsNotNull(move);

            // Let the player settle on the ground first (wait until grounded or
            // up to ~1.5s of game time).
            float settle = 0f;
            while (!move.Controller.collisions.below && settle < 1.5f)
            {
                settle += Time.deltaTime;
                yield return null;
            }
            float groundY = move.transform.position.y;

            Press(_keyboard.spaceKey);
            // Hold briefly then release; track the peak height during the arc.
            float peakY = groundY;
            yield return Hold(move, 0.5f, onPeakY: y => peakY = y);
            Release(_keyboard.spaceKey);

            Assert.Greater(peakY, groundY + 0.5f,
                $"Space should make the player jump (groundY={groundY:0.00}, peakY={peakY:0.00})");
        }
    }
}
