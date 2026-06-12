using UnityEngine;

namespace SuperSausageBoy.Hazards
{
    /// <summary>
    /// Static spike / salt hazard. Kills on contact. Simplest hazard type used
    /// for floors, ceilings, and walls.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Spike : MonoBehaviour
    {
        void Awake()
        {
            GetComponent<Collider2D>().isTrigger = true;
            if (!CompareTag("Hazard")) tag = "Hazard";
        }
    }
}
