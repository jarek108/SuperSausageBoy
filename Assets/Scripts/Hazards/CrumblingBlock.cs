using UnityEngine;

namespace SuperSausageBoy.Hazards
{
    /// <summary>
    /// Crumbling block: starts solid, breaks shortly after the player stands on
    /// it, then respawns after a delay. A classic precision-platformer hazard
    /// that forces continuous movement.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class CrumblingBlock : MonoBehaviour
    {
        [Tooltip("Time after first touch before it breaks.")]
        public float crumbleDelay = 0.4f;
        [Tooltip("Time before it reforms.")]
        public float respawnDelay = 2f;

        Collider2D _col;
        SpriteRenderer _sprite;
        bool _triggered;
        Color _baseColor;

        void Awake()
        {
            _col = GetComponent<Collider2D>();
            _sprite = GetComponent<SpriteRenderer>();
            _baseColor = _sprite.color;
        }

        void OnCollisionEnter2D(Collision2D c) => TryTrigger(c.collider);
        void OnTriggerEnter2D(Collider2D c) => TryTrigger(c);

        void TryTrigger(Collider2D other)
        {
            if (_triggered) return;
            if (!other.CompareTag("Player")) return;
            _triggered = true;
            Invoke(nameof(Break), crumbleDelay);
        }

        void Break()
        {
            _col.enabled = false;
            _sprite.enabled = false;
            Invoke(nameof(Reform), respawnDelay);
        }

        void Reform()
        {
            _col.enabled = true;
            _sprite.enabled = true;
            _sprite.color = _baseColor;
            _triggered = false;
        }

        void Update()
        {
            // Flash/shake warning while crumbling.
            if (_triggered && _col.enabled)
            {
                float blink = Mathf.PingPong(Time.time * 12f, 1f);
                _sprite.color = Color.Lerp(_baseColor, Color.red, blink);
            }
        }
    }
}
