using UnityEngine;

namespace SuperSausageBoy.Core
{
    /// <summary>
    /// Lightweight trauma-based screen shake. Attach to the camera (alongside
    /// CameraFollow). Other systems call <see cref="AddTrauma"/> on impactful
    /// events (death, hard landing). Trauma decays over time and shake intensity
    /// scales with trauma^2 so small bumps stay subtle while big hits punch.
    ///
    /// Applies as a positional offset in LateUpdate AFTER CameraFollow has moved
    /// the camera (script execution + LateUpdate ordering keeps it additive; we
    /// store and restore the base position each frame so shake never accumulates).
    /// </summary>
    [DefaultExecutionOrder(100)] // run after CameraFollow's LateUpdate
    public class ScreenShake : MonoBehaviour
    {
        [Tooltip("Maximum positional offset (units) at full trauma.")]
        public float maxOffset = 0.6f;
        [Tooltip("Maximum rotation (degrees) at full trauma.")]
        public float maxRoll = 3f;
        [Tooltip("How fast trauma decays back to zero (per second).")]
        public float decay = 1.6f;
        [Tooltip("Noise frequency — higher = jitterier shake.")]
        public float frequency = 22f;

        float _trauma;
        float _seed;

        public static ScreenShake Instance { get; private set; }

        void Awake()
        {
            Instance = this;
            _seed = Random.value * 100f;
        }

        /// <summary>Add trauma in [0..1]. Clamped; stacks across events.</summary>
        public void AddTrauma(float amount)
        {
            _trauma = Mathf.Clamp01(_trauma + amount);
        }

        void LateUpdate()
        {
            if (_trauma <= 0f) return;

            float shake = _trauma * _trauma; // perceptual curve
            float t = Time.time * frequency;

            // Perlin noise centered at 0 for smooth, organic motion.
            float offsetX = (Mathf.PerlinNoise(_seed, t) * 2f - 1f) * maxOffset * shake;
            float offsetY = (Mathf.PerlinNoise(_seed + 1f, t) * 2f - 1f) * maxOffset * shake;
            float roll = (Mathf.PerlinNoise(_seed + 2f, t) * 2f - 1f) * maxRoll * shake;

            transform.position += new Vector3(offsetX, offsetY, 0f);
            transform.rotation = Quaternion.Euler(0f, 0f, roll);

            _trauma = Mathf.Max(0f, _trauma - decay * Time.deltaTime);
            if (_trauma <= 0f)
                transform.rotation = Quaternion.identity;
        }
    }
}
