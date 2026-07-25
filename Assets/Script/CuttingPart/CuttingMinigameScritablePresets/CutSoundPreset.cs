using UnityEngine;

/// <summary>The channel and clips a cutting minigame plays through.</summary>
/// <remarks>
/// Its own asset rather than fields on <see cref="CutMinigamePreset"/>: the sounds are the same for
/// every cut while the feel of each cut is not, so keeping them apart means adding a cut with a new
/// difficulty does not mean re-picking the audio.
/// </remarks>
[CreateAssetMenu(fileName = "CutSoundPreset", menuName = "Cutting/Cut Sound Preset")]
public class CutSoundPreset : ScriptableObject
{
    [Tooltip("Channel the clips are played on. Must be the one the scene's AudioMaster listens to, or they play into nothing.")]
    public AudioEventChannel channel;

    [Tooltip("Looped while the cut is actually travelling, stopped the moment it stalls. Set Loop on the clip asset, otherwise it plays once and never restarts.")]
    public Audio cutSound;

    [Tooltip("One-shot fired when the cut completes and the body part comes away.")]
    public Audio tearSound;

    [Tooltip("Travel speed (deg/sec) above which the cut counts as cutting and the loop plays. Keeps the sound off while the scalpel is parked.")]
    public float cutSoundSpeedThreshold = 0.5f;
}
