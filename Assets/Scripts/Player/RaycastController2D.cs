using UnityEngine;

namespace SuperSausageBoy.Player
{
    /// <summary>
    /// Kinematic raycast-based collision controller (Sebastian Lague style).
    /// Casts rays from a BoxCollider2D's bounds to detect collisions and
    /// resolve movement, giving frame-perfect precision platformer control
    /// without relying on Unity's dynamic physics (which jitters).
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class RaycastController2D : MonoBehaviour
    {
        [Header("Collision")]
        public LayerMask collisionMask;

        [Tooltip("How far inside the collider edge rays start, to avoid edge cases.")]
        public const float SkinWidth = 0.015f;

        [Tooltip("Spacing target between rays (world units). More rays = more accurate.")]
        public const float RaySpacing = 0.15f;

        [HideInInspector] public int horizontalRayCount;
        [HideInInspector] public int verticalRayCount;
        [HideInInspector] public float horizontalRaySpacing;
        [HideInInspector] public float verticalRaySpacing;

        protected BoxCollider2D _collider;
        protected BoxCollider2D Collider
        {
            get
            {
                if (_collider == null) _collider = GetComponent<BoxCollider2D>();
                return _collider;
            }
        }
        public RaycastOrigins raycastOrigins;

        public struct RaycastOrigins
        {
            public Vector2 topLeft, topRight;
            public Vector2 bottomLeft, bottomRight;
        }

        protected virtual void Awake()
        {
            _collider = GetComponent<BoxCollider2D>();
        }

        protected virtual void Start()
        {
            CalculateRaySpacing();
        }

        public void UpdateRaycastOrigins()
        {
            Bounds bounds = Collider.bounds;
            bounds.Expand(SkinWidth * -2f);

            raycastOrigins.bottomLeft = new Vector2(bounds.min.x, bounds.min.y);
            raycastOrigins.bottomRight = new Vector2(bounds.max.x, bounds.min.y);
            raycastOrigins.topLeft = new Vector2(bounds.min.x, bounds.max.y);
            raycastOrigins.topRight = new Vector2(bounds.max.x, bounds.max.y);
        }

        public void CalculateRaySpacing()
        {
            Bounds bounds = Collider.bounds;
            bounds.Expand(SkinWidth * -2f);

            float boundsWidth = bounds.size.x;
            float boundsHeight = bounds.size.y;

            horizontalRayCount = Mathf.RoundToInt(boundsHeight / RaySpacing);
            verticalRayCount = Mathf.RoundToInt(boundsWidth / RaySpacing);

            horizontalRayCount = Mathf.Clamp(horizontalRayCount, 2, int.MaxValue);
            verticalRayCount = Mathf.Clamp(verticalRayCount, 2, int.MaxValue);

            horizontalRaySpacing = bounds.size.y / (horizontalRayCount - 1);
            verticalRaySpacing = bounds.size.x / (verticalRayCount - 1);
        }
    }
}
