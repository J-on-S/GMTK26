using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class AudioMaster : MonoBehaviour
{
    public AudioEventChannel eventChannel;

    public AudioMixer AudioMixer;

    private readonly List<PlayingClip> PlayingClips = new();
    private readonly Stack<AudioSource> sourcePool = new();

    public IReadOnlyList<PlayingClip> ActiveClips => PlayingClips;

    public AudioMixerGroup MixerGroup;

    private static AudioMaster instance = null;

    public void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    public struct PlayOptions
    {
        public float StartTime;
        public float PlayLength;
        public float Delay;
        public Action<bool> OnEnded;
        public Action OnLoopStarted;
    }

    public class PlayingClip
    {
        public enum FadeState { None, FadingIn, FadingOut, FadingPause, FadingResume }

        public PlayingClip(Audio clip, AudioSource source)
        {
            Clip = clip;
            Source = source;
            CurrentFade = FadeState.None;
        }

        public FadeState CurrentFade;
        public Coroutine FadeCoroutine;
        public bool IsPaused;

        /// <summary>The clip actually on the source. Always a leaf: an <see cref="AudioSet"/> has been resolved by the time this is set.</summary>
        public Audio Clip;

        /// <summary>What the caller asked for, which is the same as <see cref="Clip"/> unless a composite picked it. Kept for display only.</summary>
        public Audio Requested;

        public AudioSource Source;

        public Action<bool> OnEnded;
        public Action OnLoopStarted;

        /// <summary>Playhead position at the last frame, in samples. A drop means the loop wrapped.</summary>
        public int LastSamples;
    }

    private void OnEnable()
    {
        DontDestroyOnLoad(this);

        eventChannel.Played += Play;
        eventChannel.PlayedWithOptions += Play;
        eventChannel.FadeInWithOptions += FadeStart;
        eventChannel.Stopped += Stop;
        eventChannel.StoppedSpecific += Stop;
        eventChannel.LevelSet += SetLevel;
        eventChannel.FadeOut += FadeStop;
        eventChannel.FadeIn += FadeStart;
        eventChannel.CrossFaded += CrossFade;
        eventChannel.FadeOutSpecific += FadeStop;
        eventChannel.CrossFadedSpecific += CrossFade;
        eventChannel.Paused += Pause;
        eventChannel.Resumed += Resume;
        eventChannel.FadePaused += FadePause;
        eventChannel.FadeResumed += FadeResume;
    }

    private void OnDisable()
    {
        eventChannel.Played -= Play;
        eventChannel.PlayedWithOptions -= Play;
        eventChannel.FadeInWithOptions -= FadeStart;
        eventChannel.Stopped -= Stop;
        eventChannel.StoppedSpecific -= Stop;
        eventChannel.LevelSet -= SetLevel;
        eventChannel.FadeOut -= FadeStop;
        eventChannel.FadeIn -= FadeStart;
        eventChannel.CrossFaded -= CrossFade;
        eventChannel.FadeOutSpecific -= FadeStop;
        eventChannel.CrossFadedSpecific -= CrossFade;
        eventChannel.Paused -= Pause;
        eventChannel.Resumed -= Resume;
        eventChannel.FadePaused -= FadePause;
        eventChannel.FadeResumed -= FadeResume;
    }

    private void Update()
    {
        for (int i = PlayingClips.Count - 1; i >= 0; i--)
        {
            if (!PlayingClips[i].Source.isPlaying && !PlayingClips[i].IsPaused && Application.isFocused)
            {
                StopInternal(PlayingClips[i], true);
                continue;
            }

            TickLoop(PlayingClips[i]);
        }
    }

    /// <summary>Gives a looping clip a fresh random pitch each time it wraps, so a short loop stops reading as the same sample repeating.</summary>
    /// <remarks>
    /// A looping AudioSource never reports a stop, so the wrap has to be spotted from the playhead:
    /// <c>timeSamples</c> climbs to the clip length then drops back near zero. A frame long enough to
    /// span more than one wrap re-rolls once, which is inaudible at any sane clip length.
    /// </remarks>
    private void TickLoop(PlayingClip clip)
    {
        if (clip.IsPaused || clip.Clip == null || !clip.Source.loop)
        {
            return;
        }

        int samples = clip.Source.timeSamples;
        if (samples < clip.LastSamples)
        {
            if (clip.Clip.WantsPerLoopPitch)
            {
                clip.Source.pitch = clip.Clip.GetRandomizedPitch();
            }
            clip.OnLoopStarted?.Invoke();
        }
        clip.LastSamples = samples;
    }
    private AudioSource AcquireSource()
    {
        while (sourcePool.Count > 0)
        {
            var pooled = sourcePool.Pop();
            if (pooled != null) return pooled;
        }
        return gameObject.AddComponent<AudioSource>();
    }

    private void ReleaseSource(AudioSource source)
    {
        if (source == null) return;
        source.Stop();
        source.clip = null;
        source.loop = false;
        source.volume = 1f;
        // The inspector's solo/mute writes here; a pooled source must not carry it into its next clip.
        source.mute = false;
        source.panStereo = 0f;
        source.pitch = 1f;
        source.outputAudioMixerGroup = null;
        sourcePool.Push(source);
    }

    private AudioSource StartSource(Audio clip, float volume, PlayOptions options)
    {
        AudioSource source = AcquireSource();

        source.volume = volume;
        source.clip = clip.AudioClip;
        source.loop = clip.Loop;
        source.panStereo = clip.Pan;
        source.pitch = clip.GetRandomizedPitch();
        source.outputAudioMixerGroup = MixerGroup;

        if (clip.AudioClip != null && options.StartTime > 0f)
        {
            source.time = Mathf.Clamp(options.StartTime, 0f, Mathf.Max(0f, clip.AudioClip.length - 0.001f));
        }

        double startDsp = AudioSettings.dspTime + Mathf.Max(0f, options.Delay);

        if (options.Delay > 0f)
        {
            source.PlayScheduled(startDsp);
        }
        else
        {
            source.Play();
        }

        if (options.PlayLength > 0f)
        {
            source.SetScheduledEndTime(startDsp + options.PlayLength);
        }

        return source;
    }

    private PlayingClip Play(Audio clip) => Play(clip, default);

    private PlayingClip Play(Audio clip, PlayOptions options)
    {
        Audio requested = clip;
        clip = Resolve(clip);
        if (clip == null) return null;

        AudioSource source = StartSource(clip, clip.Volume, options);

        PlayingClip pClip = new(clip, source)
        {
            Requested = requested,
            OnEnded = options.OnEnded,
            OnLoopStarted = options.OnLoopStarted,
        };
        PlayingClips.Add(pClip);

        return pClip;
    }

    /// <summary>Turns what the caller asked for into the clip that will actually play, picking a variant when handed an <see cref="AudioSet"/>.</summary>
    /// <remarks>
    /// Done once, here, rather than wherever a field is read: a composite picks anew on every call, so
    /// resolving twice would put one variant's clip on the source and another variant's volume beside it.
    /// </remarks>
    private static Audio Resolve(Audio clip) => clip != null ? clip.GetAudio() : null;

    private PlayingClip FadeStart(Audio clip, float duration) => FadeStart(clip, duration, default);

    private PlayingClip FadeStart(Audio clip, float duration, PlayOptions options)
    {
        Audio requested = clip;
        clip = Resolve(clip);
        if (clip == null) return null;

        AudioSource source = StartSource(clip, 0f, options);

        PlayingClip pClip = new(clip, source)
        {
            Requested = requested,
            OnEnded = options.OnEnded,
            OnLoopStarted = options.OnLoopStarted,
        };
        PlayingClips.Add(pClip);

        pClip.FadeCoroutine = StartCoroutine(FadeInCoroutine(pClip, duration));
        return pClip;
    }

    private System.Collections.IEnumerator FadeInCoroutine(PlayingClip clip, float duration)
    {
        clip.CurrentFade = PlayingClip.FadeState.FadingIn;
        clip.Source.volume = 0;

        float t = 0;
        while (t < duration)
        {

            // Don't allow a framerate drop below 50FPS to affect the fade in
            t += Mathf.Clamp(Time.unscaledDeltaTime, 0.00f, 0.02f);
            // Exponential to match human hearing???
            clip.Source.volume = clip.Clip.Volume * (t / duration);

            yield return null;
        }

        clip.Source.volume = clip.Clip.Volume;
        clip.CurrentFade = PlayingClip.FadeState.None;
        clip.FadeCoroutine = null;
    }

    private void Stop(Audio clip)
    {
        if (clip == null)
        {
            StopAll();
            return;
        }

        if (GetPlayingClip(clip) is PlayingClip pClip)
        {
            Stop(pClip);
        }
    }

    private void StopAll()
    {
        var copy = new List<PlayingClip>(PlayingClips);
        foreach (PlayingClip pClip in copy)
        {
            Stop(pClip);
        }
    }

    private void Stop(PlayingClip clip) => StopInternal(clip, false);

    private void StopInternal(PlayingClip clip, bool completed)
    {
        if (!PlayingClips.Contains(clip)) return;

        if (clip.FadeCoroutine != null)
        {
            StopCoroutine(clip.FadeCoroutine);
            clip.FadeCoroutine = null;
        }
        clip.CurrentFade = PlayingClip.FadeState.None;

        PlayingClips.Remove(clip);

        ReleaseSource(clip.Source);

        Action<bool> ended = clip.OnEnded;
        clip.OnEnded = null;
        ended?.Invoke(completed);
    }
    private void FadeStop(Audio clip, float duration = 1)
    {
        if (clip == null)
        {
            FadeStopAll(duration);
            return;
        }

        if (GetPlayingClip(clip) is PlayingClip pClip)
        {
            FadeStop(pClip, duration);
        }
    }

    private void FadeStopAll(float duration)
    {
        foreach (PlayingClip pClip in PlayingClips)
        {
            FadeStop(pClip, duration);
        }
    }

    private void FadeStop(PlayingClip clip, float duration)
    {
        if (clip.CurrentFade == PlayingClip.FadeState.FadingOut) return;

        if (!PlayingClips.Contains(clip)) return;

        if (clip.FadeCoroutine != null)
        {
            StopCoroutine(clip.FadeCoroutine);
            clip.FadeCoroutine = null;
        }

        clip.FadeCoroutine = StartCoroutine(FadeOutCoroutine(clip, duration));
    }
    private System.Collections.IEnumerator FadeOutCoroutine(PlayingClip clip, float duration)
    {
        clip.CurrentFade = PlayingClip.FadeState.FadingOut;
        float startVolume = clip.Source.volume;
        float t = 0;
        while (t < duration)
        {

            t += Time.unscaledDeltaTime;
            clip.Source.volume = Mathf.Lerp(startVolume, 0, t / duration);
            yield return null;
        }

        clip.FadeCoroutine = null;
        Stop(clip);
    }

    private void CrossFade(Audio curr, Audio next, float duration)
    {
        if (GetPlayingClip(curr) is PlayingClip pClip)
        {
            CrossFade(pClip, next, duration);
            return;
        }

        FadeStopAll(duration);
        FadeStart(next, duration);
    }

    private void CrossFade(PlayingClip curr, Audio next, float duration)
    {
        if (PlayingClips.Contains(curr)) FadeStop(curr, duration);
        FadeStart(next, duration);
    }

    private void Pause(PlayingClip clip)
    {
        if (clip == null || clip.IsPaused || !PlayingClips.Contains(clip)) return;

        if (clip.FadeCoroutine != null)
        {
            StopCoroutine(clip.FadeCoroutine);
            clip.FadeCoroutine = null;
        }
        clip.CurrentFade = PlayingClip.FadeState.None;

        clip.IsPaused = true;
        clip.Source.Pause();
    }

    private void Resume(PlayingClip clip)
    {
        if (clip == null || !clip.IsPaused || !PlayingClips.Contains(clip)) return;
        clip.IsPaused = false;
        clip.Source.UnPause();
    }

    private void FadePause(PlayingClip clip, float duration)
    {
        if (clip == null || clip.IsPaused || !PlayingClips.Contains(clip)) return;

        if (clip.FadeCoroutine != null)
        {
            StopCoroutine(clip.FadeCoroutine);
            clip.FadeCoroutine = null;
        }

        clip.FadeCoroutine = StartCoroutine(FadePauseCoroutine(clip, duration));
    }

    private System.Collections.IEnumerator FadePauseCoroutine(PlayingClip clip, float duration)
    {
        clip.CurrentFade = PlayingClip.FadeState.FadingPause;
        float startVolume = clip.Source.volume;
        float t = 0;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            clip.Source.volume = Mathf.Lerp(startVolume, 0, t / duration);
            yield return null;
        }
        clip.Source.volume = 0;
        clip.Source.Pause();
        clip.IsPaused = true;
        clip.CurrentFade = PlayingClip.FadeState.None;
        clip.FadeCoroutine = null;
    }

    private void FadeResume(PlayingClip clip, float duration)
    {
        if (clip == null || !clip.IsPaused || !PlayingClips.Contains(clip)) return;

        if (clip.FadeCoroutine != null)
        {
            StopCoroutine(clip.FadeCoroutine);
            clip.FadeCoroutine = null;
        }

        clip.FadeCoroutine = StartCoroutine(FadeResumeCoroutine(clip, duration));
    }

    private System.Collections.IEnumerator FadeResumeCoroutine(PlayingClip clip, float duration)
    {
        clip.CurrentFade = PlayingClip.FadeState.FadingResume;
        clip.IsPaused = false;
        clip.Source.UnPause();
        clip.Source.volume = 0;

        float t = 0;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            clip.Source.volume = Mathf.Lerp(0, clip.Clip.Volume, t / duration);
            yield return null;
        }
        clip.Source.volume = clip.Clip.Volume;
        clip.CurrentFade = PlayingClip.FadeState.None;
        clip.FadeCoroutine = null;
    }

    private void SetLevel(float level)
    {
        AudioMixer.SetFloat(MixerGroup.name, level);
    }

    /// <summary>The playing clip a caller means when it names <paramref name="clip"/>.</summary>
    /// <remarks>
    /// Matches through <see cref="Audio.Contains"/>, not by reference: a caller that started a
    /// <see cref="AudioSet"/> names the set when it stops or fades it, while what is playing is the leaf
    /// variant the set picked. Also matches the leaf itself, so a direct reference still works.
    /// </remarks>
    private PlayingClip GetPlayingClip(Audio clip)
    {
        if (clip == null) return null;
        foreach (var pClip in PlayingClips)
        {
            if (clip.Contains(pClip.Clip)) return pClip;
        }

        return null;
    }
}
