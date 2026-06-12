using UnityEngine;
using UnityEngine.SceneManagement;

namespace SuperSausageBoy.Level
{
    /// <summary>
    /// Drives level flow: tracks the current level, advances on completion, and
    /// owns the run stats (time, deaths) that the HUD reads. Persists across
    /// scene loads as a singleton.
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        public static LevelManager Instance { get; private set; }

        [Tooltip("Scene names in play order. Index 0 = Level 1.")]
        public string[] levelScenes = {
            "Level1", "Level2", "Level3", "Level4", "Level5"
        };
        public string winScene = "Win";

        public int CurrentLevelIndex { get; private set; }
        public int Deaths { get; private set; }
        public float LevelTime { get; private set; }
        public int TotalDeaths { get; private set; }

        bool _levelActive;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Update()
        {
            if (_levelActive) LevelTime += Time.deltaTime;
        }

        public void RegisterLevelStart()
        {
            Deaths = 0;
            LevelTime = 0f;
            _levelActive = true;
        }

        public void RegisterDeath()
        {
            Deaths++;
            TotalDeaths++;
        }

        public void CompleteLevel()
        {
            _levelActive = false;
            CurrentLevelIndex++;

            if (CurrentLevelIndex >= levelScenes.Length)
            {
                if (!string.IsNullOrEmpty(winScene) && SceneExists(winScene))
                    SceneManager.LoadScene(winScene);
                else
                    CurrentLevelIndex = 0; // loop for now
            }
            else
            {
                SceneManager.LoadScene(levelScenes[CurrentLevelIndex]);
            }
        }

        public void RestartCurrentLevel()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        static bool SceneExists(string name)
        {
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == name)
                    return true;
            }
            return false;
        }
    }
}
