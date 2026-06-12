using UnityEngine;

namespace SuperSausageBoy.Player
{
    /// <summary>
    /// Resolves movement against the world using raycasts and reports collision
    /// state (above/below/left/right). This is the "Controller2D" layer: it takes
    /// a desired velocity*dt and moves the transform, clamping against geometry.
    /// </summary>
    public class Controller2D : RaycastController2D
    {
        [Header("Slopes")]
        public float maxSlopeAngle = 60f;

        public CollisionInfo collisions;

        public struct CollisionInfo
        {
            public bool above, below;
            public bool left, right;
            public bool climbingSlope, descendingSlope;
            public float slopeAngle, slopeAngleOld;
            public int faceDir; // 1 = right, -1 = left

            public void Reset()
            {
                above = below = false;
                left = right = false;
                climbingSlope = descendingSlope = false;
                slopeAngleOld = slopeAngle;
                slopeAngle = 0f;
            }
        }

        protected override void Start()
        {
            base.Start();
            collisions.faceDir = 1;
        }

        /// <summary>Move by a delta (already multiplied by deltaTime).</summary>
        public void Move(Vector2 moveAmount)
        {
            UpdateRaycastOrigins();
            collisions.Reset();

            if (moveAmount.x != 0f)
                collisions.faceDir = (int)Mathf.Sign(moveAmount.x);

            HorizontalCollisions(ref moveAmount);
            if (moveAmount.y != 0f)
                VerticalCollisions(ref moveAmount);

            transform.Translate(moveAmount);

            // Keep the physics world's collider positions in sync with the
            // transform so the next frame's raycasts are accurate. Needed both
            // in play mode (we don't use a Rigidbody2D) and in edit-mode tests.
            Physics2D.SyncTransforms();
        }

        void HorizontalCollisions(ref Vector2 moveAmount)
        {
            float directionX = collisions.faceDir;
            float rayLength = Mathf.Abs(moveAmount.x) + SkinWidth;

            // Even with no horizontal input, cast a tiny ray to detect walls (for wall slide).
            if (Mathf.Abs(moveAmount.x) < SkinWidth)
                rayLength = 2f * SkinWidth;

            for (int i = 0; i < horizontalRayCount; i++)
            {
                Vector2 rayOrigin = (directionX == -1)
                    ? raycastOrigins.bottomLeft
                    : raycastOrigins.bottomRight;
                rayOrigin += Vector2.up * (horizontalRaySpacing * i);

                RaycastHit2D hit = Physics2D.Raycast(
                    rayOrigin, Vector2.right * directionX, rayLength, collisionMask);

                Debug.DrawRay(rayOrigin, Vector2.right * directionX * rayLength, Color.red);

                if (hit)
                {
                    if (hit.distance == 0f) continue;

                    moveAmount.x = (hit.distance - SkinWidth) * directionX;
                    rayLength = hit.distance;

                    collisions.left = directionX == -1;
                    collisions.right = directionX == 1;
                }
            }
        }

        void VerticalCollisions(ref Vector2 moveAmount)
        {
            float directionY = Mathf.Sign(moveAmount.y);
            float rayLength = Mathf.Abs(moveAmount.y) + SkinWidth;

            for (int i = 0; i < verticalRayCount; i++)
            {
                Vector2 rayOrigin = (directionY == -1)
                    ? raycastOrigins.bottomLeft
                    : raycastOrigins.topLeft;
                rayOrigin += Vector2.right * (verticalRaySpacing * i + moveAmount.x);

                RaycastHit2D hit = Physics2D.Raycast(
                    rayOrigin, Vector2.up * directionY, rayLength, collisionMask);

                Debug.DrawRay(rayOrigin, Vector2.up * directionY * rayLength, Color.red);

                if (hit)
                {
                    // One-way platforms: only collide when moving down onto them.
                    if (hit.collider.CompareTag("OneWayPlatform"))
                    {
                        if (directionY == 1 || hit.distance == 0f) continue;
                    }

                    moveAmount.y = (hit.distance - SkinWidth) * directionY;
                    rayLength = hit.distance;

                    collisions.below = directionY == -1;
                    collisions.above = directionY == 1;
                }
            }
        }
    }
}
