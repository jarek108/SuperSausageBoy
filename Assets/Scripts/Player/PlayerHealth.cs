using UnityEngine;
using UnityEngine.InputSystem;

namespace SuperSausageBoy.Player
{
    /// <summary>
    /// Handles Super Sausage Boy's death and INSTANT respawn (the keystone SMB
    /// mechanic). Death is a state reset, never a scene reload, so the retry loop
    /// is sub-0.25s. Leaves a persistent grease splat decal where he died.
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        [Header("Respawn")]
        public Transform spawnPoint;
        [Tooltip("Delay before respawn (keep tiny for the SMB instant-retry feel).")]
        public float respawnDelay = 0.05f;

        [Header("Feedback")]
        public GameObject greaseSplatPrefab;
        public GameObject deathBurstPrefab;

        PlayerMovement _movement;
        SpriteRenderer _sprite;
        PlayerInput _playerInput;
        bool _dead;

        public System.Action OnDeath;
        public System.Action OnRespawn;

        void Awake()
        {
            _movement = GetComponent<PlayerMovement>();
            _sprite = GetComponentInChildren<SpriteRenderer>();
        }

        void OnEnable()
        {
            // Subscribe to the Restart action via the shared dispatcher (see the
            // note in PlayerMovement: InvokeCSharpEvents requires explicit wiring).
            _playerInput = GetComponent<PlayerInput>();
            if (_playerInput != null)
                _playerInput.onActionTriggered += OnActionTriggered;
        }

        void OnDisable()
        {
            if (_playerInput != null)
                _playerInput.onActionTriggered -= OnActionTriggered;
        }

        void OnActionTriggered(InputAction.CallbackContext ctx)
        {
            if (ctx.action.name == "Restart") OnRestart(ctx);
        }

        void Start()
        {
            if (spawnPoint != null)
                transform.position = spawnPoint.position;
        }

        public void Die()
        {
            if (_dead) return;
            _dead = true;

            // grease splat decal (persists)
            if (greaseSplatPrefab != null)
                Instantiate(greaseSplatPrefab, transform.position, Quaternion.identity);
            if (deathBurstPrefab != null)
                Instantiate(deathBurstPrefab, transform.position, Quaternion.identity);

            OnDeath?.Invoke();

            if (_sprite != null) _sprite.enabled = false;
            Invoke(nameof(Respawn), respawnDelay);
        }

        void Respawn()
        {
            _dead = false;
            if (spawnPoint != null)
                transform.position = spawnPoint.position;
            _movement.ResetMotion();
            if (_sprite != null) _sprite.enabled = true;
            OnRespawn?.Invoke();
        }

        // Manual restart (R key) bound via PlayerInput.
        public void OnRestart(InputAction.CallbackContext ctx)
        {
            if (ctx.performed) Die();
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Hazard"))
                Die();
        }
    }
}
