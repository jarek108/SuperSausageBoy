using UnityEngine;

namespace SuperSausageBoy.Core
{
    /// <summary>
    /// Tiny per-scene helper that tells the persistent AudioManager which music
    /// track to play when a scene loads. Because AudioManager is a DontDestroyOnLoad
    /// singleton, the starter (instantiated fresh each scene) simply requests the
    /// right track; the crossfade avoids restarting the same track unnecessarily.
    /// </summary>
    public class LevelAudioStarter : MonoBehaviour
    {
        public enum Track { Level, Intro, Win }
        public Track track = Track.Level;

        void Start()
        {
            var am = AudioManager.Instance;
            if (am == null) return;

            switch (track)
            {
                case Track.Level: am.PlayMusic(am.levelMusic); break;
                case Track.Intro: am.PlayMusic(am.introMusic); break;
                case Track.Win:   am.PlayMusic(am.winMusic);   break;
            }
        }
    }
}
