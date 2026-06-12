using UnityEngine;

namespace SuperSausageBoy.Hazards
{
    /// <summary>
    /// A rotating saw blade hazard. Optionally patrols between two points.
    /// Kills Super Sausage Boy on contact (via the "Hazard" tag + trigger).
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class SawBlade : MonoBehaviour
    {
        [Header("Spin")]
        public float spinSpeed = 720f; // degrees/sec

        [Header("Patrol (optional)")]
        public bool patrol = false;
        public Vector2 patrolOffset = new Vector2(4f, 0f);
        public float patrolSpeed = 3f;

        Vector3 _startPos;
        Transform _visual;

        void Awake()
        {
            _startPos = transform.position;
            _visual = transform.childCount > 0 ? transform.GetChild(0) : transform;
            GetComponent<Collider2D>().isTrigger = true;
            if (!CompareTag("Hazard")) tag = "Hazard";
        }

        void Update()
        {
            _visual.Rotate(0f, 0f, -spinSpeed * Time.deltaTime);

            if (patrol)
            {
                float t = Mathf.PingPong(Time.time * patrolSpeed, 1f);
                transform.position = _startPos + (Vector3)(patrolOffset * t);
            }
        }
    }
}
