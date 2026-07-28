using UnityEngine;

// Central audio hub. One instance lives in SampleScene (single-scene project).
public class AudioManager : MonoBehaviour
{
    public static AudioManager instance { get; private set; }

    [Header("Sources")]
    [SerializeField] private AudioSource _sfxSource;   // one-shots via PlayOneShot (overlaps OK)
    [SerializeField] private AudioSource _bgmSource;    // looping music

    [Header("BGM")]
    [SerializeField] private AudioClip _defaultBgm;
    [SerializeField, Range(0f, 1f)] private float _bgmVolume = 0.6f;
    [SerializeField, Range(0f, 1f)] private float _sfxVolume = 1f;

    [Header("Named SFX (for UnityEvents / inspector)")]
    [SerializeField] private NamedClip[] _library;     // key -> clip, for PlaySfxByName

    [System.Serializable]
    public struct NamedClip { public string key; public AudioClip clip; }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        // Single-scene project: no DontDestroyOnLoad needed (add later if multi-scene).
    }

    private void Start()
    {
        if (_defaultBgm != null) PlayBgm(_defaultBgm);
    }

    // ---- Code API (SFX) ----
    public void PlaySfx(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null || _sfxSource == null) return;
        _sfxSource.PlayOneShot(clip, _sfxVolume * volumeScale);   // PlayOneShot overlaps, no cutoff
    }

    // ---- Event/inspector API (SFX) ----
    // Button.onClick / UnityEvent can bind these (string or AudioClip arg, or bake concrete
    // wrappers). Looks up _library by key so designers wire by name in the inspector.
    public void PlaySfxByName(string key)
    {
        for (int i = 0; i < (_library?.Length ?? 0); i++)
            if (_library[i].key == key) { PlaySfx(_library[i].clip); return; }
    }
    // Overload UnityEvent<AudioClip> can also target directly:
    public void PlaySfxClip(AudioClip clip) => PlaySfx(clip);

    // ---- BGM ----
    public void PlayBgm(AudioClip clip)
    {
        if (_bgmSource == null || clip == null) return;
        if (_bgmSource.clip == clip && _bgmSource.isPlaying) return;
        _bgmSource.clip = clip;
        _bgmSource.loop = true;
        _bgmSource.volume = _bgmVolume;
        _bgmSource.Play();
    }
    public void StopBgm() { if (_bgmSource != null) _bgmSource.Stop(); }
    public void SetBgmVolume(float v) { _bgmVolume = Mathf.Clamp01(v); if (_bgmSource) _bgmSource.volume = _bgmVolume; }
}
