using UnityEngine;

namespace SuperSausageBoy.Level
{
    /// <summary>
    /// Hot Bun Girl — the level goal. When Super Sausage Boy reaches her, the
    /// level is complete and we advance to the next.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Goal : MonoBehaviour
    {
        public GameObject rescueEffectPrefab;
        bool _reached;

        void Awake()
        {
            GetComponent<Collider2D>().isTrigger = true;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (_reached) return;
            if (!other.CompareTag("Player")) return;
            _reached = true;

            if (rescueEffectPrefab != null)
                Instantiate(rescueEffectPrefab, transform.position, Quaternion.identity);

            LevelManager.Instance?.CompleteLevel();
        }
    }
}
