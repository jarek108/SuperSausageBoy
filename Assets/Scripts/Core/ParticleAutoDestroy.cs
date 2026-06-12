using UnityEngine;

namespace SuperSausageBoy.Core
{
    /// <summary>
    /// Destroys a one-shot ParticleSystem GameObject once it has finished
    /// emitting and all particles have died. Keeps the scene clean after juice
    /// bursts (death splat, landing dust) without manual lifetime management.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class ParticleAutoDestroy : MonoBehaviour
    {
        ParticleSystem _ps;

        void Awake() => _ps = GetComponent<ParticleSystem>();

        void Update()
        {
            if (_ps != null && !_ps.IsAlive(true))
                Destroy(gameObject);
        }
    }
}
