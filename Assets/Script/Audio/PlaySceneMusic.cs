using UnityEngine;
using UnityEngine.SceneManagement;


public class PlaySceneMusic : MonoBehaviour
{
    //[Tooltip("Channel this scene's music plays and stops through. Same asset the AudioMaster listens on.")]
    //[SerializeField] private AudioEventChannel channel;

    [Tooltip("Track to play for this scene.")]
    [SerializeField] private Audio music;

    [Tooltip("Fade-in time when the scene's music starts, in seconds.")]
    [SerializeField] private float fadeInDuration = 2f;

    [Tooltip("Fade-out time when the scene changes or this is disabled, in seconds.")]
    [SerializeField] private float fadeOutDuration = 2f;

    [Tooltip("Fade the track in when this object is enabled.")]
    [SerializeField] private bool playOnEnable = true;

    // The one scene track currently playing, the Audio it is, and which component owns it. Static so a new
    // scene's component can adopt a matching track the previous scene started.
    private static AudioMaster.PlayingClip _current;
    private static Audio _currentAudio;
    private static PlaySceneMusic _owner;

    /// <summary>The clip this component is responsible for, so it fades out exactly its own track.</summary>
    private AudioMaster.PlayingClip _playing;

    /// <summary>True only while a live other component owns the reigning track. A destroyed owner owns nothing.</summary>
    /// <remarks>The <c>_owner != null</c> half is what makes this survive a scene reload: a destroyed
    /// component still compares unequal to <c>this</c>, so without it every reload left the old track
    /// looking adopted -- never faded out, never cleared from <see cref="_current"/>, and stacked under
    /// the next scene's music.</remarks>
    private bool AdoptedByOther =>
        _playing == _current && _owner != null && _owner != this;

    private void OnEnable()
    {
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        if (playOnEnable) Play();
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        Stop();
    }

    /// <summary>Fades the scene's track in, or adopts it unbroken when the same track is already playing.</summary>
    public void Play()
    {
        if (_playing != null) return;

        if (music == null || music.AudioClip == null)
        {
            Debug.LogWarning($"{name}: no channel or music track assigned, so this scene has no background music.", this);
            return;
        }

        // same track carried over from the previous scene: take it as-is and keep it going, no fade.
        if (_currentAudio == music && _current != null)
        {
            _playing = _current;
            _owner = this;
            return;
        }

        _playing = AudioEventChannel.Instance.FadeStart(music, fadeInDuration);
        if (_playing == null)
        {
            Debug.LogWarning($"{name}: no AudioMaster is listening on the channel, so the scene music did not start.", this);
            return;
        }

        _current = _playing;
        _currentAudio = music;
        _owner = this;
    }

    /// <summary>Fades this scene's track out -- unless the next scene has already adopted it, in which case it is left playing.</summary>
    public void Stop()
    {
        if (_playing == null) return;

        // the reigning clip was taken over by another scene's component: it owns it now, leave it playing.
        if (!AdoptedByOther)
        {
            if (AudioEventChannel.Instance != null) AudioEventChannel.Instance.FadeStop(_playing, fadeOutDuration);
            if (_current == _playing)
            {
                _current = null;
                _currentAudio = null;
                _owner = null;
            }
        }

        _playing = null;
    }

    public void Resume()
            {
        if (_playing == null) return;

        // the reigning clip was taken over by another scene's component: it owns it now, leave it playing.
        if (!AdoptedByOther)
        {
            AudioEventChannel.Instance.Resume(_playing);
        }
    }
    
    public void Pause()
    {
        if (_playing == null) return;
        
        if (!AdoptedByOther)
        {
            AudioEventChannel.Instance.Pause(_playing);
        }
    }  

    /// <summary>Leaving this scene silences its music. Skips this scene's own activation, so the fade-in is not cut the instant it starts.</summary>
    private void OnActiveSceneChanged(Scene from, Scene to)
    {
        if (to == gameObject.scene) return; // our scene just became active -- do not stop what OnEnable just started
        Stop();
    }
}
