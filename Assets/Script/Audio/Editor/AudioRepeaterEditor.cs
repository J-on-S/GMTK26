using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Audition a <see cref="AudioRepeater"/> from its inspector: hear the rhythm its mode, interval and
/// variation actually produce, without entering Play mode or building a scene around it.
/// </summary>
/// <remarks>
/// <para>
/// Two playback paths, because there is only an <see cref="AudioMaster"/> in Play mode. In Play mode the
/// component drives itself -- the buttons call its own <see cref="AudioRepeater.Play"/> and
/// <see cref="AudioRepeater.Stop"/>, so what you hear is the component, not a stand-in. In Edit mode this
/// re-runs the same schedule off the editor clock and plays each pick through <see cref="AudioPreview"/>,
/// which goes straight to an AudioSource with no fades, mixer group or solo/mute.
/// </para>
/// <para>
/// The Edit-mode schedule is not a second copy of the timing rules: the gaps come from
/// <see cref="AudioRepeater.NextGap"/> and the wait/period choice from
/// <see cref="AudioRepeater.WaitsForClipEnd"/>, so the two cannot drift apart. Only the clock differs --
/// <see cref="EditorApplication.update"/> instead of <see cref="Time.time"/>.
/// </para>
/// <para>
/// Preview is a transport control, not asset data. Nothing here is written to the component; it stops when
/// the inspector is deselected or the Play-mode boundary is crossed.
/// </para>
/// </remarks>
[CustomEditor(typeof(AudioRepeater))]
[CanEditMultipleObjects]
public class AudioRepeaterEditor : Editor
{
    private const int RecentShown = 8;

    /// <summary>Nesting limit for the loop scan, matching <see cref="AudioSet"/>'s own.</summary>
    private const int MaxNesting = 8;

    private SerializedProperty soundProperty;
    private SerializedProperty modeProperty;
    private SerializedProperty intervalProperty;
    private SerializedProperty intervalVariationProperty;
    private SerializedProperty playOnStartProperty;
    private SerializedProperty stopClipOnStopProperty;

    /// <summary>True while the Edit-mode ticker is driving repeats. Play mode uses the component's own flag instead.</summary>
    private bool previewing;

    /// <summary>When the next Edit-mode repeat is due, on the <see cref="EditorApplication.timeSinceStartup"/> clock.</summary>
    private double nextDueAt;

    /// <summary>True while the Edit-mode schedule is waiting for the current clip to finish rather than for a time.</summary>
    private bool waitingForEnd;

    private bool subscribed;

    /// <summary>The leaf clip the last pick resolved to, for the now-playing readout and the loop check.</summary>
    private Audio lastLeaf;

    private readonly List<Audio> recent = new();

    private AudioRepeater Repeater => (AudioRepeater)target;

    public override bool RequiresConstantRepaint() =>
        previewing || Application.isPlaying || AudioPreview.IsPlayingAnything;

    private void OnEnable()
    {
        soundProperty = serializedObject.FindProperty("sound");
        modeProperty = serializedObject.FindProperty("mode");
        intervalProperty = serializedObject.FindProperty("interval");
        intervalVariationProperty = serializedObject.FindProperty("intervalVariation");
        playOnStartProperty = serializedObject.FindProperty("playOnStart");
        stopClipOnStopProperty = serializedObject.FindProperty("stopClipOnStop");

        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private void OnDisable()
    {
        // A deselected inspector must not leave a ticker firing clips in the background.
        StopPreview();
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
    }

    /// <summary>Preview never straddles the Play-mode boundary: the AudioSource and the master on the far side are not the ones it started with.</summary>
    private void OnPlayModeChanged(PlayModeStateChange change) => StopPreview();

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(soundProperty);
        EditorGUILayout.PropertyField(modeProperty);

        // On end takes the next clip the moment the current one finishes, so neither knob applies to it.
        using (new EditorGUI.DisabledScope(modeProperty.hasMultipleDifferentValues
                                           || modeProperty.enumValueIndex == (int)AudioRepeatMode.OnEnd))
        {
            EditorGUILayout.PropertyField(intervalProperty);
            EditorGUILayout.PropertyField(intervalVariationProperty);
        }

        EditorGUILayout.PropertyField(playOnStartProperty);
        EditorGUILayout.PropertyField(stopClipOnStopProperty);

        serializedObject.ApplyModifiedProperties();

        if (serializedObject.isEditingMultipleObjects) return;

        EditorGUILayout.Space();
        DrawPathHelp();
        DrawTransport();
        DrawNowPlaying();
        DrawRecent();
    }

    // ---- sections ------------------------------------------------------------------------------

    private void DrawPathHelp()
    {
        if (Repeater.Sound == null)
        {
            EditorGUILayout.HelpBox("No sound assigned — there is nothing to preview.", MessageType.Info);
            return;
        }

        if (Application.isPlaying)
        {
            if (AudioEventChannel.Instance == null)
            {
                EditorGUILayout.HelpBox("No AudioEventChannel at Resources/AudioEventChannel — nothing can play.", MessageType.Error);
            }
            return;
        }

        EditorGUILayout.HelpBox("Edit mode: clips are previewed straight, without the AudioMaster. No fades, mixer group or solo/mute. Enter Play mode to hear the real path.", MessageType.None);

        if (Repeater.WaitsForClipEnd && AnyLoops(Repeater.Sound, MaxNesting))
        {
            EditorGUILayout.HelpBox("A looping clip never ends, so this mode would stall on it. The repeater leaves a looping pick playing and stops scheduling; the preview does the same.", MessageType.Warning);
        }
    }

    private void DrawTransport()
    {
        bool running = IsRunning();

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(Repeater.Sound == null || running))
            {
                if (GUILayout.Button(new GUIContent("Preview", "Repeats using this component's own mode, interval and variation."), GUILayout.Height(24f)))
                {
                    StartPreview();
                }
            }

            using (new EditorGUI.DisabledScope(!running))
            {
                if (GUILayout.Button("Stop", GUILayout.Height(24f))) StopPreview();
            }
        }

        using (new EditorGUI.DisabledScope(Repeater.Sound == null))
        {
            if (GUILayout.Button(new GUIContent("Play one", "Fires a single repeat, so you can hear what one pick sounds like."), EditorStyles.miniButton))
            {
                PlayOnce();
            }
        }

        EditorGUILayout.LabelField("Schedule", ScheduleSummary(), EditorStyles.miniLabel);
    }

    /// <summary>Plain-language reading of what the current mode and knobs will do, so the rhythm is legible before it is heard.</summary>
    private string ScheduleSummary()
    {
        float interval = intervalProperty.floatValue;
        float variation = intervalVariationProperty.floatValue;
        string spread = variation > 0f ? $" ±{variation:0.##}s" : "";

        switch ((AudioRepeatMode)modeProperty.enumValueIndex)
        {
            case AudioRepeatMode.OnEnd:
                return "back to back, no gap";
            case AudioRepeatMode.OnEndGap:
                return $"clip, then {interval:0.##}s{spread}";
            default:
                return $"one every {Mathf.Max(AudioRepeater.MinInterval, interval):0.##}s{spread}, clip length ignored";
        }
    }

    private void DrawNowPlaying()
    {
        AudioSource source = CurrentSource();
        if (source == null || source.clip == null || !source.isPlaying) return;

        if (!Application.isPlaying) AudioPreview.TickPitch(lastLeaf);

        string label = lastLeaf != null ? lastLeaf.name : source.clip.name;
        float length = source.clip.length;
        float time = source.time;
        EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(), length > 0f ? time / length : 0f,
            $"{label}  {time:0.00}s / {length:0.00}s");
    }

    private void DrawRecent()
    {
        if (recent.Count == 0) return;

        EditorGUILayout.LabelField("Recently played", EditorStyles.miniBoldLabel);
        for (int i = recent.Count - 1; i >= 0; i--)
        {
            EditorGUILayout.LabelField(" ", recent[i] != null ? recent[i].name : "(empty)", EditorStyles.miniLabel);
        }
    }

    // ---- transport -----------------------------------------------------------------------------

    /// <summary>Whether repeats are being fired right now, by whichever path this mode uses.</summary>
    private bool IsRunning() => Application.isPlaying ? Repeater.IsRepeating : previewing;

    private void StartPreview()
    {
        if (Application.isPlaying)
        {
            // the component itself, so the preview button and the game take the identical path
            Repeater.Play();
            return;
        }

        previewing = true;
        waitingForEnd = false;
        nextDueAt = EditorApplication.timeSinceStartup;
        SetTicking(true);
    }

    private void StopPreview()
    {
        if (Application.isPlaying)
        {
            if (target != null) Repeater.Stop();
        }
        else if (AudioPreview.IsPlaying(lastLeaf))
        {
            // only our own audition: the preview source is shared, and another inspector may be using it
            AudioPreview.Stop();
        }

        previewing = false;
        waitingForEnd = false;
        SetTicking(false);
    }

    /// <summary>Fires one repeat: through the channel in Play mode, through the editor preview otherwise.</summary>
    private void PlayOnce()
    {
        if (Application.isPlaying)
        {
            AudioEventChannel channel = AudioEventChannel.Instance;
            if (channel == null || Repeater.Sound == null) return;

            // the authored Audio is handed over whole, never a locally picked variant: the master's own
            // resolve is what runs, so an AudioSet is credited to the set in its Playing list.
            AudioMaster.PlayingClip clip = channel.Play(Repeater.Sound);
            if (clip == null)
            {
                Debug.LogWarning($"{Repeater.name}: no AudioMaster is listening on the channel, so nothing played.", Repeater);
                return;
            }

            lastLeaf = clip.Clip;
            PushRecent(clip.Clip);
            return;
        }

        Audio leaf = Repeater.Sound != null ? Repeater.Sound.GetAudio() : null;
        if (leaf == null || leaf.AudioClip == null)
        {
            StopPreview();
            return;
        }

        lastLeaf = leaf;
        PushRecent(leaf);
        AudioPreview.Play(leaf);
    }

    private AudioSource CurrentSource()
    {
        if (Application.isPlaying) return Repeater.Playing?.Source;
        return AudioPreview.IsPlaying(lastLeaf) ? AudioPreview.Source : null;
    }

    private void PushRecent(Audio clip)
    {
        recent.Add(clip);
        if (recent.Count > RecentShown) recent.RemoveAt(0);
    }

    // ---- edit-mode schedule --------------------------------------------------------------------

    private void SetTicking(bool on)
    {
        if (on == subscribed) return;

        if (on) EditorApplication.update += Tick;
        else EditorApplication.update -= Tick;

        subscribed = on;
    }

    /// <summary>Runs <see cref="AudioRepeater"/>'s schedule off the editor clock.</summary>
    /// <remarks>
    /// Step for step the same as the component's Update, with "is a clip still sounding" answered by
    /// <see cref="AudioPreview"/> instead of the channel's ended callback. <see cref="EditorApplication.update"/>
    /// is throttled while the editor is unfocused, so intervals drift when you are in another window --
    /// fine for auditioning, not a timing reference.
    /// </remarks>
    private void Tick()
    {
        if (!previewing || target == null || Application.isPlaying)
        {
            SetTicking(false);
            return;
        }

        double now = EditorApplication.timeSinceStartup;

        if (waitingForEnd)
        {
            if (AudioPreview.IsPlaying(lastLeaf)) return;

            waitingForEnd = false;
            nextDueAt = now + (Repeater.Mode == AudioRepeatMode.OnEndGap ? Repeater.NextGap() : 0d);
        }

        if (now < nextDueAt) return;

        PlayOnce();
        if (!previewing) return; // PlayOnce gave up: nothing to play

        // A looping clip never ends, so scheduling more would stack copy on copy. The component leaves it
        // playing and stops repeating; match that rather than stalling forever on the wait.
        if (lastLeaf != null && lastLeaf.Loop)
        {
            previewing = false;
            waitingForEnd = false;
            SetTicking(false);
            Repaint();
            return;
        }

        if (Repeater.WaitsForClipEnd) waitingForEnd = true;
        else nextDueAt = now + Mathf.Max(AudioRepeater.MinInterval, Repeater.NextGap());

        Repaint();
    }

    /// <summary>Whether anything reachable from <paramref name="audio"/> is a looping clip.</summary>
    /// <remarks>Walks nested <see cref="AudioSet"/>s, since the loop that would stall the schedule may sit several variants down.</remarks>
    private static bool AnyLoops(Audio audio, int depth)
    {
        if (audio == null || depth <= 0) return false;

        if (audio is AudioSet set)
        {
            foreach (Audio variant in set.Variants)
            {
                if (AnyLoops(variant, depth - 1)) return true;
            }
            return false;
        }

        return audio.Loop;
    }
}
