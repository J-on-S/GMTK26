using UnityEngine;

/// <summary>
/// Plays one sound over and over while it is running: footsteps, a dripping tap, a machine ticking.
/// </summary>
/// <remarks>
/// <para>
/// The sound is a plain <see cref="Audio"/>, so an <see cref="AudioSet"/> can be dropped in and every
/// repeat picks a different variant -- which is what keeps a repeating sound from reading as one sample on
/// a timer. Nothing here knows which variant it got; the <see cref="AudioMaster"/> resolves it.
/// </para>
/// <para>
/// Callers drive it with <see cref="Play"/> and <see cref="Stop"/>; both are safe to call every frame, so a
/// caller can simply say "moving" or "not moving" without tracking state itself.
/// </para>
/// </remarks>
public class AudioRepeater : MonoBehaviour
{
    /// <summary>Floor on the scheduled gap, so a zero interval cannot fire a clip every frame.</summary>
    /// <remarks>Public so the inspector's preview clamps the same way, and a rhythm auditioned there is one the game can actually produce.</remarks>
    public const float MinInterval = 0.02f;

    [Tooltip("What to play. An Audio Set here picks a different variant on every repeat.")]
    [SerializeField] private Audio sound;

    [Tooltip("When the next repeat fires: as soon as the clip ends, after a gap once it ends, or on a fixed period regardless of clip length.")]
    [SerializeField] private AudioRepeatMode mode = AudioRepeatMode.OnEndGap;

    [Tooltip("Seconds of gap, or the period in Every interval mode.")]
    [Min(0f)][SerializeField] private float interval = 0.3f;

    [Tooltip("Random amount added to or taken off each interval, in seconds. Keeps a repeating sound off a metronome.")]
    [Min(0f)][SerializeField] private float intervalVariation = 0f;

    [Tooltip("Start repeating as soon as this object starts.")]
    [SerializeField] private bool playOnStart = false;

    [Tooltip("Stopping also cuts the clip that is playing. Off, the last one is left to finish, which is usually what you want for one-shots like footsteps.")]
    [SerializeField] private bool stopClipOnStop = false;

    /// <summary>The clip currently playing, or null between repeats. Cleared by the channel's ended callback.</summary>
    private AudioMaster.PlayingClip playing;

    /// <summary>True while the next repeat is waiting on the current clip finishing rather than on a time.</summary>
    private bool waitingForEnd;

    /// <summary>When the next repeat is due, on the <see cref="Time.time"/> clock.</summary>
    private float nextDueAt;

    /// <summary>Whether this is currently repeating. A clip may still be sounding after this goes false.</summary>
    public bool IsRepeating { get; private set; }

    public Audio Sound
    {
        get => sound;
        set => sound = value;
    }

    /// <summary>When the next repeat fires. Read by the inspector preview so it schedules the way this does.</summary>
    public AudioRepeatMode Mode => mode;

    /// <summary>The clip this repeater has on the channel right now, or null between repeats.</summary>
    /// <remarks>Exposed for the inspector's now-playing readout in Play mode. Callers must not hold it across repeats -- it is replaced every time one fires and nulled when the clip ends.</remarks>
    public AudioMaster.PlayingClip Playing => playing;

    /// <summary>Whether the current mode schedules the next repeat off the end of the clip rather than off the clock.</summary>
    public bool WaitsForClipEnd => WaitsForEnd();

    /// <summary>The one shared channel. Not a serialized field: there is a single channel in the project, and a per-object copy of it is only ever a way to point half the scene at the wrong one.</summary>
    private static AudioEventChannel Channel => AudioEventChannel.Instance;

    private void Start()
    {
        if (playOnStart) Play();
    }

    private void OnDisable() => Stop();

    /// <summary>Starts repeating, firing the first clip immediately. Does nothing if already repeating, so it is safe to call every frame.</summary>
    [ContextMenu("Play")]
    public void Play()
    {
        if (IsRepeating) return;

        IsRepeating = true;
        waitingForEnd = false;
        nextDueAt = Time.time;
    }

    /// <summary>Stops repeating. The clip already playing is left to finish unless Stop Clip On Stop is set. Safe to call every frame.</summary>
    [ContextMenu("Stop")]
    public void Stop()
    {
        if (!IsRepeating && playing == null) return;

        IsRepeating = false;
        waitingForEnd = false;

        if (stopClipOnStop && playing != null)
        {
            AudioEventChannel c = Channel;
            if (c != null) c.Stop(playing);
            playing = null;
        }
    }

    private void Update()
    {
        if (!IsRepeating) return;

        if (waitingForEnd)
        {
            if (playing != null) return;

            waitingForEnd = false;
            nextDueAt = Time.time + (mode == AudioRepeatMode.OnEndGap ? NextGap() : 0f);
        }

        if (Time.time < nextDueAt) return;

        PlayOnce();
        if (!IsRepeating) return; // PlayOnce gave up, or the clip turned out to be looping

        if (WaitsForEnd()) waitingForEnd = true;
        else nextDueAt = Time.time + Mathf.Max(MinInterval, NextGap());
    }

    private void PlayOnce()
    {
        AudioEventChannel c = Channel;

        if (sound == null || c == null)
        {
            Debug.LogWarning($"{name}: no sound assigned, or no AudioEventChannel at Resources/AudioEventChannel, so there is nothing to repeat.", this);
            Stop();
            return;
        }

        playing = c.Play(sound, new AudioMaster.PlayOptions { OnEnded = OnClipEnded });

        if (playing == null)
        {
            Debug.LogWarning($"{name}: no AudioMaster is listening on the channel, so nothing played.", this);
            Stop();
            return;
        }

        // A looping clip never ends, so repeating it would stack copy on copy until the scene is a wall of
        // noise. Let this one keep playing and stop scheduling more.
        if (playing.Clip != null && playing.Clip.Loop)
        {
            Debug.LogWarning($"{name}: '{playing.Clip.name}' is a looping clip, so it is left playing instead of being repeated.", this);
            IsRepeating = false;
            waitingForEnd = false;
        }
    }

    private void OnClipEnded(bool completed) => playing = null;

    private bool WaitsForEnd() => mode != AudioRepeatMode.EveryInterval;

    /// <summary>One interval with its random variation applied, never below zero. Rolled per gap, so no two are the same.</summary>
    /// <remarks>Public so the inspector preview rolls its gaps here rather than reimplementing the rule -- the rhythm auditioned is then the same code that produces the rhythm in game.</remarks>
    public float NextGap() =>
        intervalVariation > 0f
            ? Mathf.Max(0f, interval + Random.Range(-intervalVariation, intervalVariation))
            : interval;
}
