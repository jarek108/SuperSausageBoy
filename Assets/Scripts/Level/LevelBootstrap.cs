using UnityEngine;
using SuperSausageBoy.Player;

namespace SuperSausageBoy.Level
{
    /// <summary>
    /// Wires a scene's player to the LevelManager: registers level start and
    /// counts deaths. Place one in each level scene.
    /// </summary>
    public class LevelBootstrap : MonoBehaviour
    {
        public PlayerHealth player;

        void Start()
        {
            if (LevelManager.Instance != null)
                LevelManager.Instance.RegisterLevelStart();

            if (player == null)
                player = FindObjectOfType<PlayerHealth>();

            if (player != null)
                player.OnDeath += () => LevelManager.Instance?.RegisterDeath();
        }
    }
}
