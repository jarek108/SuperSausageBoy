using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

namespace SuperSausageBoy.Story
{
    /// <summary>
    /// Plays the animated intro: a sequence of still story panels, each shown with
    /// a subtle Ken-Burns drift + crossfade, while its narration line is spoken
    /// (TTS WAV) and a caption is shown. Background intro music plays underneath.
    ///
    /// Advances automatically when a panel's narration finishes (plus a small
    /// pad), or immediately on Submit/Jump. Any time the player presses the skip
    /// key, the whole intro is skipped and the first level loads.
    /// </summary>
    public class StoryController : MonoBehaviour
    {
        [System.Serializable]
        public class Panel
        {
            public Sprite image;
            [TextArea] public string caption;
            public AudioClip narration;
        }

        [Header("Content")]
        public Panel[] panels;

        [Header("Refs")]
        public Image displayImage;     // full-screen image
        public Text captionText;       // caption at the bottom
        public AudioSource voiceSource;
        public CanvasGroup fader;      // for fade-in/out between panels

        [Header("Music")]
        public AudioClip introMusic;
        public AudioSource musicSource;
        [Range(0f, 1f)] public float musicVolume = 0.4f;

        [Header("Flow")]
        [Tooltip("Scene to load when the intro ends.")]
        public string nextScene = "Level1";
        [Tooltip("Seconds to hold a panel after its narration finishes.")]
        public float postNarrationPad = 0.6f;
        [Tooltip("Fade duration between panels (sec).")]
        public float fadeTime = 0.6f;
        [Tooltip("Fallback panel duration if a narration clip is missing.")]
        public float fallbackPanelTime = 3.5f;

        [Header("Ken-Burns")]
        [Tooltip("How much the image scales over a panel (slow zoom).")]
        public float zoom = 0.08f;

        int _index;
        bool _advanceRequested;
        bool _skipRequested;
        bool _running;

        void Start()
        {
            if (musicSource != null && introMusic != null)
            {
                musicSource.clip = introMusic;
                musicSource.loop = true;
                musicSource.volume = musicVolume;
                musicSource.Play();
            }
            StartCoroutine(RunSequence());
        }

        // Bound via PlayerInput (Submit / Jump = advance; Cancel = skip),
        // but also works with direct keyboard polling for the headless build.
        public void OnAdvance(InputAction.CallbackContext ctx) { if (ctx.performed) _advanceRequested = true; }
        public void OnSkip(InputAction.CallbackContext ctx) { if (ctx.performed) _skipRequested = true; }

        void Update()
        {
            // Lightweight polling fallback so the intro is controllable even
            // without the full input wiring.
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)
                    _advanceRequested = true;
                if (kb.escapeKey.wasPressedThisFrame)
                    _skipRequested = true;
            }
        }

        System.Collections.IEnumerator RunSequence()
        {
            _running = true;
            if (fader != null) fader.alpha = 0f;

            for (_index = 0; _index < panels.Length; _index++)
            {
                if (_skipRequested) break;
                yield return ShowPanel(panels[_index]);
            }

            // Fade to black, then load the first level.
            yield return Fade(0f, 1f);
            _running = false;

            if (!string.IsNullOrEmpty(nextScene))
                SceneManager.LoadScene(nextScene);
        }

        System.Collections.IEnumerator ShowPanel(Panel panel)
        {
            // Set content.
            if (displayImage != null) displayImage.sprite = panel.image;
            if (captionText != null) captionText.text = panel.caption;

            // Fade in.
            yield return Fade(1f, 0f); // alpha of the black overlay: 1->0 = reveal

            // Start narration.
            float duration = fallbackPanelTime;
            if (voiceSource != null && panel.narration != null)
            {
                voiceSource.clip = panel.narration;
                voiceSource.Play();
                duration = panel.narration.length + postNarrationPad;
            }

            // Hold (with Ken-Burns drift), watching for advance/skip.
            float t = 0f;
            Vector3 baseScale = displayImage != null ? Vector3.one : Vector3.one;
            while (t < duration)
            {
                if (_advanceRequested) { _advanceRequested = false; break; }
                if (_skipRequested) break;

                if (displayImage != null)
                {
                    float k = duration > 0f ? t / duration : 1f;
                    displayImage.transform.localScale = baseScale * (1f + zoom * k);
                }
                t += Time.deltaTime;
                yield return null;
            }

            if (voiceSource != null) voiceSource.Stop();

            // Fade out (reveal black) before the next panel.
            yield return Fade(0f, 1f);

            if (displayImage != null) displayImage.transform.localScale = Vector3.one;
        }

        // Lerp the black overlay's alpha. from/to are alpha values [0..1].
        System.Collections.IEnumerator Fade(float from, float to)
        {
            if (fader == null) yield break;
            float t = 0f;
            while (t < fadeTime)
            {
                t += Time.deltaTime;
                fader.alpha = Mathf.Lerp(from, to, fadeTime > 0f ? t / fadeTime : 1f);
                yield return null;
            }
            fader.alpha = to;
        }

        public bool IsRunning => _running;
    }
}
