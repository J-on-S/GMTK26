using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Owns the single background-music track and carries it across scene loads.
/// </summary>
/// <remarks>
/// Lives on the AudioManager prefab, which <c>SceneBootstrapInjector</c> puts in every scene and which
/// <see cref="AudioMaster"/> already marks <c>DontDestroyOnLoad</c>, so one instance outlives every load.
///
/// Invariant: this is the only thing that starts or stops scene music. A scene states what it wants
/// through <see cref="PlaySceneMusic"/> and never touches a clip. That is what lets a repeated track
/// survive a scene change -- the dying scene has nothing to stop, so when the next scene asks for the
/// same <see cref="Audio"/> asset the clip is simply left playing.
/// </remarks>
public class SceneMusicPlayer : MonoBehaviour
{
    public static SceneMusicPlayer Instance { get; private set; }

    [Tooltip("Fade-out used when a scene declares no music at all, in seconds. A scene that does declare music sets the crossfade length itself.")]
    [SerializeField] private float defaultFadeOut = 2f;

    /// <summary>What the most recently loaded scene asked for.</summary>
    /// <remarks>
    /// Static so a declaration can register before this component's own <c>Awake</c> has run -- component
    /// order within the first scene is not defined.
    ///
    /// Never cleared explicitly. A destroyed declaration compares equal to null through Unity's lifetime
    /// check, so a scene carrying no <see cref="PlaySceneMusic"/> reads as "no music" the instant the
    /// previous scene's declaration is torn down.
    /// </remarks>
    private static PlaySceneMusic _request;

    private AudioMaster.PlayingClip _playing;
    private Audio _playingAudio;

    /// <summary>Declares the track a scene wants. Called by that scene's <see cref="PlaySceneMusic"/>.</summary>
    public static void Request(PlaySceneMusic declaration) => _request = declaration;

    private void Awake()
    {
        // Mirrors AudioMaster's guard on the same GameObject, so whichever of the two runs first discards
        // the duplicate and Instance never ends up pointing at an object that is about to be destroyed.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        Instance = null;
    }

    /// <remarks>
    /// <c>sceneLoaded</c> never fires for the scene the game boots into, so the first evaluation happens
    /// here: <c>Start</c> runs after every <c>Awake</c> and <c>OnEnable</c> in that scene, so the
    /// declaration has already registered. <see cref="Apply"/> is idempotent, so a freshly created player
    /// that gets both callbacks is harmless.
    /// </remarks>
    private void Start() => Apply();

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply();

    /// <summary>Brings the playing track in line with what the current scene declared.</summary>
    private void Apply()
    {
        AudioEventChannel channel = AudioEventChannel.Instance;
        if (channel == null) return;

        Audio wanted = _request != null ? _request.Music : null;

        // Already playing exactly this: leave the clip untouched. No fade, no restart, no gap.
        if (wanted == _playingAudio && _playing != null) return;

        if (_playing != null)
        {
            channel.FadeStop(_playing, _request != null ? _request.FadeOut : defaultFadeOut);
            _playing = null;
            _playingAudio = null;
        }

        if (wanted == null) return;

        // Clear the reference when the track ends on its own, so a later scene asking for the same Audio
        // starts it again instead of matching a clip that is no longer playing.
        AudioMaster.PlayingClip started = null;
        started = channel.FadeStart(wanted, _request.FadeIn, new AudioMaster.PlayOptions
        {
            OnEnded = _ =>
            {
                if (_playing != started) return;
                _playing = null;
                _playingAudio = null;
            },
        });

        if (started == null)
        {
            Debug.LogWarning($"{name}: no AudioMaster is listening on the channel, so the scene music did not start.", this);
            return;
        }

        _playing = started;
        _playingAudio = wanted;
    }

    public void Pause()
    {
        if (_playing == null || AudioEventChannel.Instance == null) return;
        AudioEventChannel.Instance.Pause(_playing);
    }

    public void Resume()
    {
        if (_playing == null || AudioEventChannel.Instance == null) return;
        AudioEventChannel.Instance.Resume(_playing);
    }

    public void FadePause(float duration = 1f)
    {
        if (_playing == null || AudioEventChannel.Instance == null) return;
        AudioEventChannel.Instance.FadePause(_playing, duration);
    }

    public void FadeResume(float duration = 1f)
    {
        if (_playing == null || AudioEventChannel.Instance == null) return;
        AudioEventChannel.Instance.FadeResume(_playing, duration);
    }
}
