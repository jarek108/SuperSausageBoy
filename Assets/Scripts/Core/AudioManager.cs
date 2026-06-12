using UnityEngine;

namespace SuperSausageBoy.Core
{
    /// <summary>
    /// Central audio hub: persistent singleton that plays looping background
    /// music (with a simple crossfade) and fires one-shot sound effects.
    ///
    /// Clips are assigned in the inspector (wired by the GameBuilder editor tool).
    /// Call <see cref="PlaySfx"/> from gameplay events and <see cref="PlayMusic"/>
    /// on scene/state changes. SFX use a pooled set of AudioSources so overlapping
    /// effects (e.g. rapid deaths) don't cut each other off.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        public enum Sfx { Jump, Land, Death, WallSlide, Goal }

        [Header("SFX clips")]
        public AudioClip jumpClip;
        public AudioClip landClip;
        public AudioClip deathClip;
        public AudioClip wallSlideClip;
        public AudioClip goalClip;

        [Header("Music clips")]
        public AudioClip levelMusic;
        public AudioClip introMusic;
        public AudioClip winMusic;

        [Header("Mix")]
        [Range(0f, 1f)] public float musicVolume = 0.55f;
        [Range(0f, 1f)] public float sfxVolume = 0.9f;
        [Tooltip("Crossfade duration between music tracks (sec).")]
        public float musicFade = 0.8f;
        [Tooltip("Number of pooled SFX voices.")]
        public int sfxVoices = 8;

        AudioSource _musicA;
        AudioSource _musicB;
        bool _musicAActive;
        AudioSource[] _sfxPool;
        int _sfxIndex;

        // wall-slide loop state (it's continuous, not a one-shot)
        AudioSource _wallSlideSource;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _musicA = CreateSource("MusicA", loop: true);
            _musicB = CreateSource("MusicB", loop: true);
            _sfxPool = new AudioSource[Mathf.Max(1, sfxVoices)];
            for (int i = 0; i < _sfxPool.Length; i++)
                _sfxPool[i] = CreateSource($"Sfx{i}", loop: false);

            _wallSlideSource = CreateSource("WallSlide", loop: true);
            _wallSlideSource.volume = 0f;
        }

        AudioSource CreateSource(string name, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = loop;
            src.spatialBlend = 0f; // 2D
            return src;
        }

        // ---------------- Music ----------------

        public void PlayMusic(AudioClip clip)
        {
            if (clip == null) return;
            AudioSource next = _musicAActive ? _musicB : _musicA;
            AudioSource prev = _musicAActive ? _musicA : _musicB;

            if (next.clip == clip && next.isPlaying) return; // already playing

            next.clip = clip;
            next.volume = 0f;
            next.Play();
            _musicAActive = !_musicAActive;

            StopAllCoroutines();
            StartCoroutine(Crossfade(prev, next));
        }

        System.Collections.IEnumerator Crossfade(AudioSource from, AudioSource to)
        {
            float t = 0f;
            float fromStart = from != null ? from.volume : 0f;
            while (t < musicFade)
            {
                t += Time.unscaledDeltaTime;
                float k = musicFade > 0f ? t / musicFade : 1f;
                if (from != null) from.volume = Mathf.Lerp(fromStart, 0f, k);
                to.volume = Mathf.Lerp(0f, musicVolume, k);
                yield return null;
            }
            to.volume = musicVolume;
            if (from != null) { from.Stop(); from.clip = null; }
        }

        // ---------------- SFX ----------------

        public void PlaySfx(Sfx sfx, float volumeScale = 1f, float pitchVariance = 0.06f)
        {
            AudioClip clip = ClipFor(sfx);
            if (clip == null) return;

            AudioSource src = _sfxPool[_sfxIndex];
            _sfxIndex = (_sfxIndex + 1) % _sfxPool.Length;

            src.pitch = 1f + Random.Range(-pitchVariance, pitchVariance);
            src.PlayOneShot(clip, sfxVolume * volumeScale);
        }

        /// <summary>Continuous wall-slide hiss; call each frame with sliding state.</summary>
        public void SetWallSliding(bool sliding)
        {
            if (wallSlideClip == null) return;
            if (sliding)
            {
                if (!_wallSlideSource.isPlaying)
                {
                    _wallSlideSource.clip = wallSlideClip;
                    _wallSlideSource.Play();
                }
                _wallSlideSource.volume = Mathf.MoveTowards(
                    _wallSlideSource.volume, sfxVolume * 0.5f, Time.deltaTime * 4f);
            }
            else
            {
                _wallSlideSource.volume = Mathf.MoveTowards(
                    _wallSlideSource.volume, 0f, Time.deltaTime * 6f);
                if (_wallSlideSource.volume <= 0.001f && _wallSlideSource.isPlaying)
                    _wallSlideSource.Stop();
            }
        }

        AudioClip ClipFor(Sfx sfx) => sfx switch
        {
            Sfx.Jump => jumpClip,
            Sfx.Land => landClip,
            Sfx.Death => deathClip,
            Sfx.WallSlide => wallSlideClip,
            Sfx.Goal => goalClip,
            _ => null,
        };
    }
}
