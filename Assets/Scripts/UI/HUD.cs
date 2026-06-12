using UnityEngine;
using UnityEngine.UI;
using SuperSausageBoy.Level;

namespace SuperSausageBoy.UI
{
    /// <summary>
    /// In-game HUD: shows the level timer and death count, reading from the
    /// LevelManager. Uses uGUI Text (simplest reliable runtime UI).
    /// </summary>
    public class HUD : MonoBehaviour
    {
        public Text timerText;
        public Text deathText;
        public Text levelText;

        void Update()
        {
            var lm = LevelManager.Instance;
            if (lm == null) return;

            if (timerText != null)
                timerText.text = $"TIME {lm.LevelTime:0.00}";
            if (deathText != null)
                deathText.text = $"DEATHS {lm.Deaths}";
            if (levelText != null)
                levelText.text = $"LEVEL {lm.CurrentLevelIndex + 1}";
        }
    }
}
