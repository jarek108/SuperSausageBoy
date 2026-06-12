using UnityEngine;
using SuperSausageBoy.Core;

namespace SuperSausageBoy.Player
{
    /// <summary>
    /// Glue between gameplay events and the "juice" systems (audio + screen
    /// shake). Keeps PlayerMovement / PlayerHealth free of presentation concerns.
    /// Subscribes to jump / land / wall-slide / death and translates them into
    /// AudioManager calls and ScreenShake trauma.
    /// </summary>
    [RequireComponent(typeof(PlayerMovement))]
    public class PlayerFeedback : MonoBehaviour
    {
        [Tooltip("Vertical impact speed at/above which a landing counts as 'hard' (shake + louder thud).")]
        public float hardLandingSpeed = 12f;
        [Tooltip("Screen shake trauma added on death.")]
        public float deathTrauma = 0.7f;
        [Tooltip("Screen shake trauma added on a hard landing.")]
        public float hardLandingTrauma = 0.25f;

        [Tooltip("Dust burst spawned at the feet on landing.")]
        public GameObject landingDustPrefab;
        [Tooltip("Minimum impact speed to spawn landing dust.")]
        public float dustMinSpeed = 4f;

        PlayerMovement _movement;
        PlayerHealth _health;

        void Awake()
        {
            _movement = GetComponent<PlayerMovement>();
            _health = GetComponent<PlayerHealth>();
        }

        void OnEnable()
        {
            if (_movement == null) _movement = GetComponent<PlayerMovement>();
            if (_health == null) _health = GetComponent<PlayerHealth>();

            if (_movement != null)
            {
                _movement.OnJumped += HandleJump;
                _movement.OnLanded += HandleLand;
            }
            if (_health != null)
                _health.OnDeath += HandleDeath;
        }

        void OnDisable()
        {
            if (_movement != null)
            {
                _movement.OnJumped -= HandleJump;
                _movement.OnLanded -= HandleLand;
            }
            if (_health != null)
                _health.OnDeath -= HandleDeath;
        }

        void Update()
        {
            // Wall-slide is a continuous state, so drive it every frame.
            if (_movement != null && AudioManager.Instance != null)
                AudioManager.Instance.SetWallSliding(_movement.IsWallSliding);
        }

        void HandleJump()
        {
            AudioManager.Instance?.PlaySfx(AudioManager.Sfx.Jump);
        }

        void HandleLand(float impactSpeed)
        {
            float scale = Mathf.Clamp(impactSpeed / hardLandingSpeed, 0.4f, 1.2f);
            AudioManager.Instance?.PlaySfx(AudioManager.Sfx.Land, scale);

            if (impactSpeed >= hardLandingSpeed)
                ScreenShake.Instance?.AddTrauma(hardLandingTrauma);

            // Kick up dust at the feet for anything but the gentlest touchdown.
            if (landingDustPrefab != null && impactSpeed >= dustMinSpeed)
            {
                Vector3 feet = transform.position;
                var col = GetComponent<Collider2D>();
                if (col != null) feet.y = col.bounds.min.y;
                Instantiate(landingDustPrefab, feet, Quaternion.identity);
            }
        }

        void HandleDeath()
        {
            AudioManager.Instance?.PlaySfx(AudioManager.Sfx.Death);
            ScreenShake.Instance?.AddTrauma(deathTrauma);
        }
    }
}
