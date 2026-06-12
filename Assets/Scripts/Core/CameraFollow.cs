using UnityEngine;

namespace SuperSausageBoy.Core
{
    /// <summary>
    /// Simple smoothed camera follow with a small dead zone, designed to play
    /// nicely with the Pixel Perfect Camera (low damping, snaps via PPC).
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        public float smoothTime = 0.12f;
        public Vector2 offset = new Vector2(0f, 1.5f);
        public Vector2 deadZone = new Vector2(1.5f, 1f);

        Vector3 _vel;

        void LateUpdate()
        {
            if (target == null) return;

            Vector3 desired = transform.position;
            Vector2 delta = (Vector2)target.position + offset - (Vector2)transform.position;

            if (Mathf.Abs(delta.x) > deadZone.x)
                desired.x = target.position.x + offset.x - Mathf.Sign(delta.x) * deadZone.x;
            if (Mathf.Abs(delta.y) > deadZone.y)
                desired.y = target.position.y + offset.y - Mathf.Sign(delta.y) * deadZone.y;

            desired.z = -10f;
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref _vel, smoothTime);
        }
    }
}
