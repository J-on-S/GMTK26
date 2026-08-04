using UnityEngine;

/// <summary>
/// Declares which track this scene wants as background music. Data only -- it never starts, stops or owns
/// a clip; <see cref="SceneMusicPlayer"/> reads the declaration once the scene has finished loading.
/// </summary>
/// <remarks>
/// Deliberately passive. An earlier version played its own track and tried to hand it to the next scene
/// through statics, which cannot work: Unity destroys the old scene before the new one's <c>OnEnable</c>
/// runs, so the hand-off always arrived after the outgoing component had already stopped the clip, and
/// the same music restarted from zero on every load. Owning the clip in something that outlives the load
/// removes the hand-off entirely.
/// </remarks>
public class PlaySceneMusic : MonoBehaviour
{
    [Tooltip("Track to play for this scene. Leave empty and the previous scene's music fades out to silence.")]
    [SerializeField] private Audio music;

    [Tooltip("Fade-in time when this track starts, in seconds. Unused when the previous scene was already playing it.")]
    [SerializeField] private float fadeInDuration = 2f;

    [Tooltip("Fade-out time for the outgoing track this scene replaces, in seconds.")]
    [SerializeField] private float fadeOutDuration = 2f;

    public Audio Music => music;
    public float FadeIn => fadeInDuration;
    public float FadeOut => fadeOutDuration;

    /// <remarks>
    /// <c>OnEnable</c> rather than <c>Awake</c> so a disabled object declares nothing, which is how a scene
    /// asks for silence without losing the track it had assigned. Runs before <c>sceneLoaded</c>, so the
    /// player always sees the request for the scene it is evaluating.
    /// </remarks>
    private void OnEnable()
    {
        if (music == null || music.AudioClip == null)
        {
            Debug.LogWarning($"{name}: no music track assigned, so this scene has no background music.", this);
            return;
        }

        SceneMusicPlayer.Request(this);
    }

    // Null-checked rather than null-propagated: Instance is a UnityEngine.Object, so `?.` would skip the
    // lifetime check and call into a destroyed player.

    /// <summary>Silences the scene's music while the game is paused. Wired from PauseMenu's onGamePause.</summary>
    public void Pause()
    {
        if (SceneMusicPlayer.Instance != null) SceneMusicPlayer.Instance.FadePause();
    }

    /// <summary>Brings the scene's music back. Wired from PauseMenu's onGameResume.</summary>
    public void Resume()
    {
        if (SceneMusicPlayer.Instance != null) SceneMusicPlayer.Instance.FadeResume();
    }
}
