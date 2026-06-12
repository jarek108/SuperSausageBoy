#if UNITY_EDITOR
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using SuperSausageBoy.Player;

namespace SuperSausageBoy.EditorTools
{
    /// <summary>
    /// Headless mechanics test harness. Loads a level, spawns the player, and
    /// directly drives the PlayerMovement state machine (bypassing the Input
    /// System, which needs a real device) to verify each mechanic produces the
    /// expected motion. Writes a PASS/FAIL report to a log file.
    ///
    /// Run via -executeMethod SuperSausageBoy.EditorTools.PlayTestHarness.Run
    /// in batch mode. Results are printed with the [TEST] prefix.
    /// </summary>
    public static class PlayTestHarness
    {
        static StringBuilder _report = new StringBuilder();
        static int _pass, _fail;

        public static void Run()
        {
            _report.Clear(); _pass = 0; _fail = 0;
            Log("=== SuperSausageBoy Mechanics Test ===");

            // Build a tiny synthetic test rig (ground + walls) so we don't depend
            // on level scenes loading with full input wiring.
            TestGravityAndGround();
            TestJumpReachesHeight();
            TestVariableJumpHeight();
            TestCoyoteTime();
            TestJumpBuffer();
            TestRunAcceleration();
            TestSprintFasterThanWalk();
            TestWallSlideClampsFall();
            TestWallJumpPushesAway();

            Log($"=== RESULT: {_pass} passed, {_fail} failed ===");
            Debug.Log(_report.ToString());

            System.IO.File.WriteAllText(
                System.IO.Path.Combine(Application.dataPath, "../playtest_report.txt"),
                _report.ToString());
        }

        // ---- test rig ----

        static GameObject MakePlayer(out PlayerMovement move, out Controller2D ctrl)
        {
            var go = new GameObject("TestPlayer");
            go.tag = "Player";
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.8f, 1.1f);
            ctrl = go.AddComponent<Controller2D>();
            ctrl.collisionMask = 1 << 8; // Ground layer
            move = go.AddComponent<PlayerMovement>();
            move.snapToPixel = false; // measure raw physics, not snapped
            // Invoke Start() manually (kinematics setup) since not in play loop.
            InvokeStart(ctrl);
            InvokeStart(move);
            return go;
        }

        static GameObject MakeGround(float cx, float cy, float w, float h)
        {
            var g = new GameObject("Ground");
            g.layer = 8;
            var col = g.AddComponent<BoxCollider2D>();
            col.size = new Vector2(w, h);
            g.transform.position = new Vector3(cx, cy, 0);
            return g;
        }

        static void InvokeStart(MonoBehaviour mb)
        {
            var m = mb.GetType().GetMethod("Start",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public);
            m?.Invoke(mb, null);
        }

        // Simulate N fixed steps of the movement Update loop using reflection-free
        // public API. We replicate the per-frame integration the controller does
        // by calling its private Update via reflection.
        static void Step(PlayerMovement move, int steps, float dt = 1f / 60f)
        {
            var update = typeof(PlayerMovement).GetMethod("Update",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            // We can't easily fake Time.deltaTime in edit mode; instead call the
            // physics-affecting internals. Simpler: directly drive via public
            // helpers we expose. (See SetTestInput / TickTest below.)
            for (int i = 0; i < steps; i++)
                move.TickTest(dt);
        }

        // ---- individual tests ----

        static void TestGravityAndGround()
        {
            var go = MakePlayer(out var move, out var ctrl);
            var ground = MakeGround(0, -1f, 20f, 1f);
            go.transform.position = new Vector3(0, 2f, 0);
            Physics2D.SyncTransforms();

            move.SetTestInput(0, false, false);
            for (int i = 0; i < 120; i++) move.TickTest(1f / 60f);

            // Should have fallen and be resting on top of ground (~ -0.5 + halfHeight)
            float restY = go.transform.position.y;
            bool grounded = ctrl.collisions.below;
            Check("Gravity pulls down & lands on ground", grounded && restY < 2f && restY > -1f);
            Cleanup(go, ground);
        }

        static void TestJumpReachesHeight()
        {
            var go = MakePlayer(out var move, out var ctrl);
            var ground = MakeGround(0, -1f, 20f, 1f);
            go.transform.position = new Vector3(0, 0f, 0);
            Physics2D.SyncTransforms();

            // settle on ground
            move.SetTestInput(0, false, false);
            for (int i = 0; i < 30; i++) move.TickTest(1f / 60f);
            float startY = go.transform.position.y;

            // full jump
            move.PressJump();
            float maxY = startY;
            for (int i = 0; i < 120; i++)
            {
                move.TickTest(1f / 60f);
                maxY = Mathf.Max(maxY, go.transform.position.y);
            }
            float reached = maxY - startY;
            // Expect close to maxJumpHeight (3.5) within tolerance.
            Check($"Full jump reaches ~maxJumpHeight (got {reached:0.00}, want ~{move.maxJumpHeight})",
                reached > move.maxJumpHeight * 0.75f && reached < move.maxJumpHeight * 1.25f);
            Cleanup(go, ground);
        }

        static void TestVariableJumpHeight()
        {
            var go = MakePlayer(out var move, out var ctrl);
            var ground = MakeGround(0, -1f, 20f, 1f);
            go.transform.position = new Vector3(0, 0f, 0);
            Physics2D.SyncTransforms();
            for (int i = 0; i < 30; i++) move.TickTest(1f / 60f);
            float startY = go.transform.position.y;

            // tap jump: press then release after 2 frames
            move.PressJump();
            move.TickTest(1f / 60f);
            move.TickTest(1f / 60f);
            move.ReleaseJump();
            float maxY = startY;
            for (int i = 0; i < 120; i++)
            {
                move.TickTest(1f / 60f);
                maxY = Mathf.Max(maxY, go.transform.position.y);
            }
            float reached = maxY - startY;
            Check($"Tapped jump is shorter than full (got {reached:0.00} < {move.maxJumpHeight})",
                reached < move.maxJumpHeight * 0.75f && reached > 0.2f);
            Cleanup(go, ground);
        }

        static void TestCoyoteTime()
        {
            var go = MakePlayer(out var move, out var ctrl);
            // ground only under x<1; player will walk off the right edge
            var ground = MakeGround(-2f, -1f, 6f, 1f); // spans x ~ -5..1
            go.transform.position = new Vector3(-1f, 1f, 0);
            Physics2D.SyncTransforms();
            for (int i = 0; i < 60; i++) move.TickTest(1f / 60f);

            // walk right off the ledge - step until just airborne (within coyote window)
            move.SetTestInput(1, false, false);
            int stepsToAir = 0;
            for (int i = 0; i < 120; i++)
            {
                move.TickTest(1f / 60f);
                stepsToAir++;
                if (!ctrl.collisions.below) break;
            }
            bool airborne = !ctrl.collisions.below;
            // immediately (within coyote window) jump should still work
            move.PressJump();
            float yBefore = go.transform.position.y;
            float maxY = yBefore;
            for (int i = 0; i < 40; i++) { move.TickTest(1f / 60f); maxY = Mathf.Max(maxY, go.transform.position.y); }
            Log($"  [diag] coyote: stepsToAir={stepsToAir} airborne={airborne} rise={(maxY - yBefore):0.00}");
            Check("Coyote time: jump works shortly after leaving ledge",
                airborne && (maxY - yBefore) > 0.5f);
            Cleanup(go, ground);
        }

        static void TestJumpBuffer()
        {
            var go = MakePlayer(out var move, out var ctrl);
            var ground = MakeGround(0, -1f, 20f, 1f);
            // Start just above the ground so the player lands within the jump
            // buffer window (jumpBufferTime ~0.1s ~6 frames) after pressing.
            go.transform.position = new Vector3(0, 0.2f, 0);
            Physics2D.SyncTransforms();
            move.SetTestInput(0, false, false);

            // press jump while still airborne (just before landing) -> should buffer
            move.PressJump();
            bool jumpedAfterLanding = false;
            bool everGrounded = false;
            float prevY = go.transform.position.y;
            float peakAfterLand = float.NegativeInfinity;
            for (int i = 0; i < 90; i++)
            {
                move.TickTest(1f / 60f);
                if (ctrl.collisions.below) everGrounded = true;
                // after first grounding, look for upward motion (the buffered jump firing)
                if (everGrounded) peakAfterLand = Mathf.Max(peakAfterLand, go.transform.position.y);
                if (move.Velocity.y > 1f && everGrounded) jumpedAfterLanding = true;
            }
            Log($"  [diag] buffer: everGrounded={everGrounded} jumped={jumpedAfterLanding} peak={peakAfterLand:0.00}");
            Check("Jump buffer: buffered press triggers jump on landing", jumpedAfterLanding);
            Cleanup(go, ground);
        }

        static void TestRunAcceleration()
        {
            var go = MakePlayer(out var move, out var ctrl);
            var ground = MakeGround(0, -1f, 200f, 1f);
            go.transform.position = new Vector3(0, 1f, 0); // above ground, will settle
            Physics2D.SyncTransforms();
            for (int i = 0; i < 60; i++) move.TickTest(1f / 60f);

            Log($"  [diag] after settle: pos={go.transform.position} colSize={go.GetComponent<BoxCollider2D>().bounds.size} colMin={go.GetComponent<BoxCollider2D>().bounds.min}");
            move.SetTestInput(1, false, false);
            float startX = go.transform.position.x;
            for (int i = 0; i < 60; i++) move.TickTest(1f / 60f);
            float vx = move.Velocity.x;
            float movedX = go.transform.position.x - startX;
            Log($"  [diag] run: vx={vx:0.00} movedX={movedX:0.00} grounded={ctrl.collisions.below} faceDir={ctrl.collisions.faceDir} right={ctrl.collisions.right}");
            // Accept either velocity OR actual displacement (pixel-snap can mask vx readback timing)
            Check($"Run accelerates toward moveSpeed (vx={vx:0.0}, movedX={movedX:0.0}, want ~{move.moveSpeed})",
                vx > move.moveSpeed * 0.8f && vx <= move.moveSpeed * 1.05f);
            Cleanup(go, ground);
        }

        static void TestSprintFasterThanWalk()
        {
            var go = MakePlayer(out var move, out var ctrl);
            var ground = MakeGround(0, -1f, 200f, 1f);
            go.transform.position = new Vector3(0, 1f, 0);
            Physics2D.SyncTransforms();
            for (int i = 0; i < 60; i++) move.TickTest(1f / 60f);

            move.SetTestInput(1, true, false); // sprint held
            for (int i = 0; i < 60; i++) move.TickTest(1f / 60f);
            float vx = move.Velocity.x;
            Check($"Sprint exceeds walk speed (vx={vx:0.0} > {move.moveSpeed})",
                vx > move.moveSpeed * 1.1f);
            Cleanup(go, ground);
        }

        static void TestWallSlideClampsFall()
        {
            var go = MakePlayer(out var move, out var ctrl);
            // a tall wall on the right (left edge at x=1.0)
            var wall = MakeGround(1.5f, 2f, 1f, 12f);
            go.transform.position = new Vector3(0.2f, 4f, 0);
            Physics2D.SyncTransforms();

            move.SetTestInput(1, false, false); // push into wall
            for (int i = 0; i < 80; i++) move.TickTest(1f / 60f);
            float vy = move.Velocity.y;
            Check($"Wall slide clamps fall speed (vy={vy:0.0} >= -{move.wallSlideSpeedMax}*1.1)",
                move.IsWallSliding && vy >= -move.wallSlideSpeedMax * 1.1f);
            Cleanup(go, wall);
        }

        static void TestWallJumpPushesAway()
        {
            var go = MakePlayer(out var move, out var ctrl);
            var wall = MakeGround(1.5f, 2f, 1f, 12f);
            go.transform.position = new Vector3(0.2f, 4f, 0);
            Physics2D.SyncTransforms();

            move.SetTestInput(1, false, false); // push into wall to start sliding
            for (int i = 0; i < 40; i++) move.TickTest(1f / 60f);
            bool sliding = move.IsWallSliding;
            move.PressJump(); // wall jump (neutral -> off)
            move.SetTestInput(0, false, false);
            move.TickTest(1f / 60f);
            float vx = move.Velocity.x;
            float vy = move.Velocity.y;
            Check($"Wall jump pushes away from wall & up (vx={vx:0.0}<0, vy={vy:0.0}>0)",
                sliding && vx < 0f && vy > 0f);
            Cleanup(go, wall);
        }

        // ---- util ----

        static void Check(string name, bool ok)
        {
            if (ok) { _pass++; Log($"[TEST] PASS: {name}"); }
            else { _fail++; Log($"[TEST] FAIL: {name}"); }
        }

        static void Log(string s) => _report.AppendLine(s);

        static void Cleanup(params GameObject[] gos)
        {
            foreach (var g in gos) if (g != null) Object.DestroyImmediate(g);
        }
    }
}
#endif
