using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class AudioMaster : MonoBehaviour
{
    public AudioEventChannel eventChannel;

    public AudioMixer AudioMixer;

    private readonly List<PlayingClip> PlayingClips = new();
    private readonly Stack<AudioSource> sourcePool = new();

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
        public Audio Clip;
        public AudioSource Source;
    }

    private void OnEnable()
    {
        DontDestroyOnLoad(this);

        eventChannel.Played += Play;
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
                Stop(PlayingClips[i]);
            }
        }
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
        source.panStereo = 0f;
        source.pitch = 1f;
        source.outputAudioMixerGroup = null;
        sourcePool.Push(source);
    }

    private PlayingClip Play(Audio clip)
    {
        AudioSource source = AcquireSource();

        source.volume = clip.Volume;
        source.clip = clip.AudioClip;
        source.loop = clip.Loop;
        source.panStereo = clip.Pan;
        source.pitch = clip.GetRandomizedPitch();
        source.outputAudioMixerGroup = MixerGroup;

        source.Play();

        PlayingClip pClip = new(clip, source);
        PlayingClips.Add(pClip);

        return PlayingClips[^1];
    }

    private PlayingClip FadeStart(Audio clip, float duration)
    {
        AudioSource source = AcquireSource();

        source.volume = 0;
        source.clip = clip.AudioClip;
        source.loop = clip.Loop;
        source.panStereo = clip.Pan;
        source.pitch = clip.GetRandomizedPitch();
        source.outputAudioMixerGroup = MixerGroup;

        source.Play();

        PlayingClip pClip = new(clip, source);
        PlayingClips.Add(pClip);

        pClip.FadeCoroutine = StartCoroutine(FadeInCoroutine(pClip, duration));
        return PlayingClips[^1];
    }

    private System.Collections.IEnumerator FadeInCoroutine(PlayingClip clip, float duration)
    {
        clip.CurrentFade = PlayingClip.FadeState.FadingIn;
        clip.Source.volume = 0;

        float t = 0;
        while (t < duration)
        {

            t += Time.unscaledDeltaTime;
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

    private void Stop(PlayingClip clip)
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

    private PlayingClip? GetPlayingClip(Audio clip)
    {
        if (clip == null) return null;
        foreach (var pClip in PlayingClips)
        {
            if (pClip.Clip == clip) return pClip;
        }

        return null;
    }
}
