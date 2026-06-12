using UnityEngine;

namespace SuperSausageBoy.Player
{
    /// <summary>
    /// Procedural squash &amp; stretch for the player sprite. Adds "juice" by
    /// deforming the visual transform (NOT the collider) based on motion:
    ///  - Stretching vertically while moving fast up/down through the air.
    ///  - A punchy squash on landing, springing back to neutral.
    ///
    /// Attach to the sprite child (the thing with the SpriteRenderer), and point
    /// <see cref="movement"/> at the PlayerMovement on the parent. Volume is
    /// conserved (x scales inversely to y) so the sausage never looks bloated.
    /// </summary>
    public class SquashStretch : MonoBehaviour
    {
        [Tooltip("The movement source. If null, searched on the parent at Start.")]
        public PlayerMovement movement;

        [Header("Air stretch")]
        [Tooltip("Velocity at which stretch reaches its maximum.")]
        public float maxStretchVelocity = 18f;
        [Tooltip("How much the sprite can stretch (0.3 = up to +30% on Y).")]
        public float stretchAmount = 0.3f;

        [Header("Landing squash")]
        [Tooltip("How much the sprite squashes on a hard landing (0.4 = -40% Y).")]
        public float landSquash = 0.4f;
        [Tooltip("Spring stiffness returning scale to neutral.")]
        public float springStiffness = 220f;
        [Tooltip("Spring damping — higher settles faster with less bounce.")]
        public float springDamping = 14f;

        Vector3 _baseScale;
        bool _wasGroundedLastFrame;

        // spring state for the landing pop (scalar on Y, mirrored on X)
        float _squashOffset;   // current deformation (negative = squashed)
        float _squashVelocity; // spring velocity

        void Start()
        {
            _baseScale = transform.localScale;
            if (movement == null)
                movement = GetComponentInParent<PlayerMovement>();
        }

        void LateUpdate()
        {
            if (movement == null) return;

            bool grounded = movement.Controller != null && movement.Controller.collisions.below;
            float vy = movement.Velocity.y;

            // Detect a landing: airborne last frame, grounded now.
            if (grounded && !_wasGroundedLastFrame)
            {
                // Kick the spring proportional to how hard we hit.
                float impact = Mathf.Clamp01(Mathf.Abs(_lastAirVelY) / maxStretchVelocity);
                _squashOffset = -landSquash * impact;
                _squashVelocity = 0f;
            }
            if (!grounded) _lastAirVelY = vy;

            // Integrate the landing spring back toward neutral (0).
            float accel = (-springStiffness * _squashOffset) - (springDamping * _squashVelocity);
            _squashVelocity += accel * Time.deltaTime;
            _squashOffset += _squashVelocity * Time.deltaTime;

            // Continuous air stretch from vertical speed (only while airborne).
            float airStretch = 0f;
            if (!grounded)
            {
                float n = Mathf.Clamp01(Mathf.Abs(vy) / maxStretchVelocity);
                airStretch = n * stretchAmount;
            }

            // Combine: positive = taller/thinner, negative = shorter/wider.
            float yFactor = 1f + airStretch + _squashOffset;
            yFactor = Mathf.Max(0.2f, yFactor);
            float xFactor = 1f / yFactor; // conserve volume

            transform.localScale = new Vector3(
                _baseScale.x * xFactor,
                _baseScale.y * yFactor,
                _baseScale.z);

            _wasGroundedLastFrame = grounded;
        }

        float _lastAirVelY;
    }
}
