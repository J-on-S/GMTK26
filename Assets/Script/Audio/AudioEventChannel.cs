using System;
using UnityEngine;

[CreateAssetMenu(menuName = "AudioEventChannel")]
[Serializable]
public class AudioEventChannel : ScriptableObject
{
    public delegate AudioMaster.PlayingClip PlayDelegate(Audio clip);
    public delegate AudioMaster.PlayingClip FadeInDelegate(Audio clip, float duration);

    public event PlayDelegate Played;
    public event Action<Audio> Stopped;
    public event Action<float> LevelSet;
    public event Action<Audio, float> FadeOut;
    public event FadeInDelegate FadeIn;
    public event Action<Audio, Audio, float> CrossFaded;
    public event Action<AudioMaster.PlayingClip> StoppedSpecific;
    public event Action<AudioMaster.PlayingClip, float> FadeOutSpecific;
    public event Action<AudioMaster.PlayingClip, Audio, float> CrossFadedSpecific;
    public event Action<AudioMaster.PlayingClip> Paused;
    public event Action<AudioMaster.PlayingClip> Resumed;
    public event Action<AudioMaster.PlayingClip, float> FadePaused;
    public event Action<AudioMaster.PlayingClip, float> FadeResumed;


    /// <summary>
    /// Plays an audio clip, returns null if no AudioMaster is found in the scene or the event channel is not connected to one.
    /// </summary>
    /// <param name="clip">Audio clip (ScriptableObject)</param>
    /// <returns></returns>
    public AudioMaster.PlayingClip Play(Audio clip) => Played?.Invoke(clip);

    /// <summary>
    /// Sets the volume level of the AudioMixer
    /// </summary>
    /// <param name="level">volume</param>
    public void SetLevel(float level) => LevelSet?.Invoke(level);

    /// <summary>
    /// Stop a specific playingClip. 
    /// </summary>
    /// <param name="playingClip"></param>
    public void Stop(AudioMaster.PlayingClip playingClip) => StoppedSpecific?.Invoke(playingClip);

    /// <summary>
    /// Stops a specific audio clip that may be playing, or all playing-clips if parameter is null or not specified
    /// </summary>
    /// <param name="clip">Audio clip</param>
    public void Stop(Audio clip = null) => Stopped?.Invoke(clip);

    /// <summary>
    /// Fade-out a playing audio clip for a certain amout of time and then stop it. 
    /// </summary>
    /// <param name="clip">Audio clip to fade-out</param>
    /// <param name="duration">fade-out duration</param>
    public void FadeStop(Audio clip = null, float duration = 1) => FadeOut?.Invoke(clip, duration);

    /// <summary>
    /// Fade out all currently playing audio clips
    /// </summary>
    /// <param name="duration">Duration of fade-out</param>
    public void FadeAll(float duration = 1)
    {
        FadeOut?.Invoke(null, duration);
    }
        

    /// <summary>
    /// Fade-out a playing audio clip for a certain amout of time and then stop it. 
    /// </summary>
    /// <param name="clip">Specific PlayingClip clip to fade-out</param>
    /// <param name="duration">fade-out duration</param>
    public void FadeStop(AudioMaster.PlayingClip clip, float duration = 1) => FadeOutSpecific?.Invoke(clip, duration);

    /// <summary>
    /// Play an audioclip with a fade-in. Returns null if no audiomaster or no connected audiomaster
    /// </summary>
    /// <param name="clip">Clip to Play</param>
    /// <param name="duration">duration of fade</param>
    public AudioMaster.PlayingClip FadeStart(Audio clip, float duration = 1) => FadeIn?.Invoke(clip, duration);

    /// <summary>
    /// Cross-Fade between two audio clips 
    /// </summary>
    /// <param name="newAudio">new Audio Clip to play</param>
    /// <param name="oldAudio">old Audio Clip to fade from (If it doesn't exist, fade out all currently playing audios)</param>
    /// <param name="duration">duration of fade</param>
    public void CrossFade(Audio newAudio, Audio oldAudio = null, float duration = 1) => CrossFaded?.Invoke(oldAudio, newAudio, duration);

    /// <summary>
    /// Cross-Fade between two audio clips 
    /// </summary>
    /// <param name="newAudio">new Audio Clip to play</param>
    /// <param name="oldAudio">specific PlayingClip to fade from</param>
    /// <param name="duration">duration of fade</param>
    public void CrossFade(Audio newAudio, AudioMaster.PlayingClip oldAudio, float duration = 1) => CrossFadedSpecific?.Invoke(oldAudio, newAudio, duration);

    /// <summary>
    /// Pause a specific playing clip immediately.
    /// </summary>
    public void Pause(AudioMaster.PlayingClip clip) => Paused?.Invoke(clip);

    /// <summary>
    /// Resume a paused playing clip immediately.
    /// </summary>
    public void Resume(AudioMaster.PlayingClip clip) => Resumed?.Invoke(clip);

    /// <summary>
    /// Fade out a playing clip then pause it.
    /// </summary>
    public void FadePause(AudioMaster.PlayingClip clip, float duration = 1) => FadePaused?.Invoke(clip, duration);

    /// <summary>
    /// Resume a paused clip with a fade in.
    /// </summary>
    public void FadeResume(AudioMaster.PlayingClip clip, float duration = 1) => FadeResumed?.Invoke(clip, duration);
}
