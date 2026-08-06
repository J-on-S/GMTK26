using UnityEditor;
using UnityEngine;

/// <summary>
/// The editor's one audition source: plays an <see cref="Audio"/> asset outside Play mode, with the volume,
/// pan, loop and randomized pitch it would play with in game.
/// </summary>
/// <remarks>
/// <para>
/// Shared by <see cref="AudioInspector"/> and <see cref="AudioSetEditor"/> so auditioning sounds the same
/// wherever it is started from, and so only one hidden host object ever exists.
/// </para>
/// <para>
/// This bypasses <see cref="AudioMaster"/> entirely -- there is none outside Play mode -- so fades, pausing,
/// the mixer group and the master's solo/mute do not apply here. In Play mode, callers should go through
/// <see cref="AudioEventChannel"/> instead and leave this alone.
/// </para>
/// </remarks>
public static class AudioPreview
{
    private static GameObject host;
    private static AudioSource source;
    private static Audio target;

    /// <summary>Playhead position at the last tick, in samples. A drop means the loop wrapped.</summary>
    private static int lastSamples;

    public static AudioSource Source => source;

    /// <summary>The asset currently being auditioned, or null.</summary>
    public static Audio Target => target;

    public static bool IsPlaying(Audio audio) => source != null && source.isPlaying && target == audio;

    public static bool IsPlayingAnything => source != null && source.isPlaying;

    /// <summary>Starts (or restarts) an audition of <paramref name="audio"/>.</summary>
    public static void Play(Audio audio)
    {
        if (audio == null || audio.AudioClip == null) return;

        EnsureHost();

        target = audio;
        source.clip = audio.AudioClip;
        source.volume = audio.Volume;
        source.panStereo = audio.Pan;
        // randomized, not the raw Pitch: the preview should sound like the game, and pressing
        // Play repeatedly is how you audition the spread PitchVariation gives you.
        source.pitch = audio.GetRandomizedPitch();
        source.loop = audio.Loop;
        lastSamples = 0;
        source.Play();
    }

    public static void Stop()
    {
        if (source != null) source.Stop();
        target = null;
    }

    /// <summary>Re-rolls the pitch each time a looping clip wraps, so the audition matches what the game will play.</summary>
    /// <remarks>
    /// A looping AudioSource never reports a stop, so the wrap is spotted from the playhead dropping back
    /// toward zero, the same way <see cref="AudioMaster"/> does it at runtime. Callers tick this
    /// from their repaint, which is exactly when the preview is being listened to.
    /// </remarks>
    public static void TickPitch(Audio audio)
    {
        if (audio == null || !audio.WantsPerLoopPitch || source == null) return;

        int samples = source.timeSamples;
        if (samples < lastSamples)
        {
            source.pitch = audio.GetRandomizedPitch();
        }
        lastSamples = samples;
    }

    private static void EnsureHost()
    {
        if (host != null && source != null) return;

        host = new GameObject("~AudioPreview")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        source = host.AddComponent<AudioSource>();
        source.playOnAwake = false;
    }
}
