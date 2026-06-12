using UnityEngine;
using UnityEngine.InputSystem;

namespace SuperSausageBoy.Player
{
    /// <summary>
    /// Super Sausage Boy movement: tight precision-platformer feel inspired by
    /// the Super Meat Boy genre. Implements run with accel/friction, variable
    /// jump height, coyote time, jump buffering, air control, wall slide, wall
    /// jump, and a sprint modifier. Uses the kinematic Controller2D for collision.
    ///
    /// All tuning values are baselines [EST] meant to be tweaked by feel — there
    /// are no published SMB constants (it used a custom engine).
    /// </summary>
    [RequireComponent(typeof(Controller2D))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Run")]
        [Tooltip("Top horizontal speed when walking (units/sec).")]
        public float moveSpeed = 9f;
        [Tooltip("Top horizontal speed when sprinting (units/sec).")]
        public float sprintSpeed = 14f;
        [Tooltip("Time to reach target speed on the ground.")]
        public float accelTimeGrounded = 0.06f;
        [Tooltip("Time to reach target speed in the air.")]
        public float accelTimeAirborne = 0.12f;

        [Header("Jump")]
        [Tooltip("Apex height of a full jump (units).")]
        public float maxJumpHeight = 3.5f;
        [Tooltip("Apex height when the button is tapped (min jump).")]
        public float minJumpHeight = 1.0f;
        [Tooltip("Time from ground to jump apex (sec). Controls gravity feel.")]
        public float timeToJumpApex = 0.36f;
        [Tooltip("Gravity multiplier while falling — >1 makes the descent snappier than the rise (classic precision-platformer feel).")]
        public float fallGravityMultiplier = 1.7f;
        [Tooltip("Extra gravity multiplier while rising but NOT holding jump — gives weighty, responsive short hops.")]
        public float lowJumpGravityMultiplier = 2.0f;

        [Header("Assist (game feel)")]
        [Tooltip("Grace period to still jump after leaving a ledge.")]
        public float coyoteTime = 0.1f;
        [Tooltip("How early a jump press is remembered before landing.")]
        public float jumpBufferTime = 0.1f;

        [Header("Wall")]
        [Tooltip("Max slide speed when hugging a wall (units/sec, downward).")]
        public float wallSlideSpeedMax = 3f;
        [Tooltip("Horizontal + vertical impulse for jumping off a wall.")]
        public Vector2 wallJumpClimb = new Vector2(8f, 16f);   // toward/up when pushing into wall
        public Vector2 wallJumpOff = new Vector2(10f, 14f);    // away when neutral
        public Vector2 wallLeap = new Vector2(16f, 16f);       // big leap when pushing away
        [Tooltip("Brief lock on horizontal input after a wall jump so the leap reads.")]
        public float wallStickTime = 0.12f;

        // --- runtime state ---
        float _gravity;
        float _maxJumpVelocity;
        float _minJumpVelocity;
        Vector2 _velocity;
        float _velocityXSmoothing;

        float _coyoteCounter;
        float _jumpBufferCounter;
        float _timeToWallUnstick;

        Controller2D _controllerCached;
        Controller2D _controller
        {
            get
            {
                if (_controllerCached == null) _controllerCached = GetComponent<Controller2D>();
                return _controllerCached;
            }
        }
        Vector2 _input;
        bool _sprintHeld;
        bool _jumpHeld;

        // exposed for animation/feedback hooks
        public Vector2 Velocity => _velocity;
        public bool IsWallSliding { get; private set; }
        public int FacingDir { get; private set; } = 1;
        public Controller2D Controller => _controller;

        // feedback events (subscribed by juice/audio systems)
        public System.Action OnJumped;
        public System.Action<float> OnLanded;   // arg: impact speed (abs vertical velocity)
        bool _wasGroundedLastFrame;
        float _lastAirVelocityY;

        // sub-pixel accumulation for crisp pixel-perfect movement
        [Header("Pixel Perfect")]
        [Tooltip("Pixels per unit; movement is snapped to whole pixels to avoid jitter.")]
        public int pixelsPerUnit = 16;
        public bool snapToPixel = true;
        Vector2 _subPixelRemainder;

        void Start()
        {
            // Kinematics: derive gravity & jump velocities from desired height/time.
            // h = (g * t^2) / 2  ->  g = 2h / t^2 ;  v = g * t
            _gravity = -(2f * maxJumpHeight) / (timeToJumpApex * timeToJumpApex);
            _maxJumpVelocity = Mathf.Abs(_gravity) * timeToJumpApex;
            _minJumpVelocity = Mathf.Sqrt(2f * Mathf.Abs(_gravity) * minJumpHeight);
        }

        // Input System message callbacks (PlayerInput "Send Messages" / Invoke Unity Events)
        public void OnMove(InputAction.CallbackContext ctx) => _input.x = ctx.ReadValue<float>();
        public void OnSprint(InputAction.CallbackContext ctx) => _sprintHeld = ctx.ReadValueAsButton();

        public void OnJump(InputAction.CallbackContext ctx)
        {
            if (ctx.performed) { _jumpBufferCounter = jumpBufferTime; _jumpHeld = true; }  // buffer the press
            if (ctx.canceled) { OnJumpReleased(); _jumpHeld = false; }                     // variable height
        }

        void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>
        /// Core per-frame integration. Public-ish via TickTest so the headless
        /// test harness can drive it with a fixed dt in edit mode.
        /// </summary>
        void Tick(float dt)
        {
            if (_input.x != 0f) FacingDir = (int)Mathf.Sign(_input.x);

            UpdateWallSlideState();
            HandleTimers(dt);
            HandleJumpFromBuffer();

            ApplyHorizontal(dt);
            ApplyGravity(dt);
            HandleWallStick(dt);

            // Track the airborne fall speed so we know landing impact below.
            if (!_controller.collisions.below) _lastAirVelocityY = _velocity.y;

            Vector2 delta = _velocity * dt;
            delta = ApplySubPixelSnap(delta);

            _controller.Move(delta);

            // Zero vertical velocity when hitting floor/ceiling.
            if (_controller.collisions.above || _controller.collisions.below)
            {
                if (_controller.collisions.below) _subPixelRemainder.y = 0f;
                _velocity.y = 0f;
            }

            // Landing detection: airborne last frame, grounded now.
            bool groundedNow = _controller.collisions.below;
            if (groundedNow && !_wasGroundedLastFrame)
                OnLanded?.Invoke(Mathf.Abs(_lastAirVelocityY));
            _wasGroundedLastFrame = groundedNow;
        }

        void ApplyHorizontal(float dt)
        {
            float targetSpeed = (_sprintHeld ? sprintSpeed : moveSpeed) * _input.x;
            float accelTime = _controller.collisions.below ? accelTimeGrounded : accelTimeAirborne;
            // IMPORTANT: pass dt explicitly. The default SmoothDamp overload uses
            // Time.deltaTime, which is 0 in batch/edit-mode tests (freezing motion).
            _velocity.x = Mathf.SmoothDamp(
                _velocity.x, targetSpeed, ref _velocityXSmoothing, accelTime,
                Mathf.Infinity, dt);
        }

        void ApplyGravity(float dt)
        {
            // Asymmetric gravity for a snappy, weighty arc (precision-platformer feel):
            //  - falling: multiply gravity so the descent is faster than the ascent.
            //  - rising without holding jump: heavier gravity for crisp short hops.
            float gravityScale = 1f;
            if (_velocity.y < 0f)
                gravityScale = fallGravityMultiplier;
            else if (_velocity.y > 0f && !_jumpHeld)
                gravityScale = lowJumpGravityMultiplier;

            _velocity.y += _gravity * gravityScale * dt;

            if (IsWallSliding && _velocity.y < -wallSlideSpeedMax)
                _velocity.y = -wallSlideSpeedMax;
        }

        void UpdateWallSlideState()
        {
            int wallDirX = _controller.collisions.left ? -1 : 1;
            bool touchingWall = _controller.collisions.left || _controller.collisions.right;
            IsWallSliding = touchingWall && !_controller.collisions.below && _velocity.y < 0f;
        }

        void HandleTimers(float dt)
        {
            // coyote time
            if (_controller.collisions.below) _coyoteCounter = coyoteTime;
            else _coyoteCounter -= dt;

            // jump buffer
            _jumpBufferCounter -= dt;
        }

        void HandleJumpFromBuffer()
        {
            if (_jumpBufferCounter <= 0f) return;

            // Wall jump takes priority when sliding.
            if (IsWallSliding)
            {
                DoWallJump();
                _jumpBufferCounter = 0f;
                return;
            }

            // Grounded or within coyote window.
            if (_coyoteCounter > 0f)
            {
                _velocity.y = _maxJumpVelocity;
                _coyoteCounter = 0f;
                _jumpBufferCounter = 0f;
                OnJumped?.Invoke();
            }
        }

        void DoWallJump()
        {
            int wallDirX = _controller.collisions.left ? -1 : 1;
            _timeToWallUnstick = wallStickTime;

            if (_input.x == 0f)
            {
                // neutral: hop off the wall
                _velocity.x = -wallDirX * wallJumpOff.x;
                _velocity.y = wallJumpOff.y;
            }
            else if (Mathf.Sign(_input.x) == wallDirX)
            {
                // pushing into wall: small climb
                _velocity.x = -wallDirX * wallJumpClimb.x;
                _velocity.y = wallJumpClimb.y;
            }
            else
            {
                // pushing away: big leap
                _velocity.x = -wallDirX * wallLeap.x;
                _velocity.y = wallLeap.y;
            }
            _velocityXSmoothing = 0f;
            OnJumped?.Invoke();
        }

        void OnJumpReleased()
        {
            // Variable jump height: cut upward velocity if released early.
            if (_velocity.y > _minJumpVelocity)
                _velocity.y = _minJumpVelocity;
        }

        void HandleWallStick(float dt)
        {
            // After a wall jump, briefly ignore horizontal input so the leap arcs.
            if (_timeToWallUnstick > 0f)
            {
                _velocityXSmoothing = 0f;
                _timeToWallUnstick -= dt;
                _velocity.x = Mathf.MoveTowards(_velocity.x, 0f, 0f); // keep impulse
            }
        }

        Vector2 ApplySubPixelSnap(Vector2 delta)
        {
            if (!snapToPixel) return delta;

            // Accumulate desired sub-pixel movement, only emit whole pixels.
            float unit = 1f / pixelsPerUnit;
            _subPixelRemainder += delta;

            float snappedX = Mathf.Round(_subPixelRemainder.x / unit) * unit;
            float snappedY = Mathf.Round(_subPixelRemainder.y / unit) * unit;

            _subPixelRemainder.x -= snappedX;
            _subPixelRemainder.y -= snappedY;

            return new Vector2(snappedX, snappedY);
        }

        public void ResetMotion()
        {
            _velocity = Vector2.zero;
            _velocityXSmoothing = 0f;
            _subPixelRemainder = Vector2.zero;
            _coyoteCounter = 0f;
            _jumpBufferCounter = 0f;
            _timeToWallUnstick = 0f;
            _jumpHeld = false;
            IsWallSliding = false;
        }

        // ----------------------------------------------------------------
        // Test hooks: let the headless harness drive the controller with a
        // fixed dt and synthetic input, bypassing the Input System.
        // ----------------------------------------------------------------
        public void TickTest(float dt) => Tick(dt);
        public void SetTestInput(float moveX, bool sprint, bool _unusedJump)
        {
            _input.x = moveX;
            _sprintHeld = sprint;
        }
        public void PressJump() { _jumpBufferCounter = jumpBufferTime; _jumpHeld = true; }
        public void ReleaseJump() { OnJumpReleased(); _jumpHeld = false; }
    }
}
