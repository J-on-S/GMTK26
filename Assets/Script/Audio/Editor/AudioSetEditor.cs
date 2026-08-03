using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Test bench for an <see cref="AudioSet"/>: audition what its picker hands back, one clip at a time or as a
/// stream, without building a scene around it.
/// </summary>
/// <remarks>
/// <para>
/// Two playback paths, because there is only an <see cref="AudioMaster"/> in Play mode. In Play mode the set
/// goes through <see cref="AudioEventChannel"/> exactly as gameplay would -- the picked clip shows up in the
/// master's Playing list, with its fades, mixer group and solo/mute. In Edit mode it falls back to
/// <see cref="AudioPreview"/>, which plays the clip straight and has none of that.
/// </para>
/// <para>
/// Everything here is a test knob, not asset data: the settings live in <see cref="SessionState"/> keyed by
/// the asset's GUID, so they follow the asset around the editor without being saved into it.
/// </para>
/// </remarks>
[CustomEditor(typeof(AudioSet))]
public class AudioSetEditor : Editor
{
    private const int RollCount = 20;
    private const int RecentShown = 8;
    private const int MaxNesting = 8;
    private const float MinInterval = 0.01f;

    private SerializedProperty variantsProperty;
    private SerializedProperty pickProperty;
    private SerializedProperty avoidRepeatProperty;

    // ---- test state (not serialized into the asset) ----
    private bool autoPlay;
    private AudioRepeatMode autoMode = AudioRepeatMode.OnEnd;
    private float interval = 1f;

    /// <summary>Random amount added to or taken off each interval, matching <see cref="AudioRepeater"/>'s own variation.</summary>
    private float intervalVariation;

    /// <summary>When the next auto-play clip is due, on the <see cref="EditorApplication.timeSinceStartup"/> clock.</summary>
    private double nextDueAt;

    /// <summary>True while auto-play is waiting for the current clip to finish rather than for a time to arrive.</summary>
    private bool waitingForEnd;

    private bool subscribed;

    /// <summary>The Play-mode handle for the clip this inspector started, so its progress can be shown and it can be stopped.</summary>
    private AudioMaster.PlayingClip playing;

    /// <summary>Set from the channel's OnEnded callback: a Play-mode clip cannot be polled once the master has dropped it.</summary>
    private bool playingEnded = true;

    /// <summary>The clip the last play actually picked -- read back from the master in Play mode, resolved locally in Edit mode.</summary>
    private Audio lastLeaf;

    private readonly List<Audio> recent = new();
    private readonly List<KeyValuePair<Audio, int>> rollResults = new();

    private AudioSet Set => (AudioSet)target;

    public override bool RequiresConstantRepaint() =>
        Application.isPlaying || autoPlay || AudioPreview.IsPlayingAnything;

    private void OnEnable()
    {
        variantsProperty = serializedObject.FindProperty("variants");
        pickProperty = serializedObject.FindProperty("pick");
        avoidRepeatProperty = serializedObject.FindProperty("avoidRepeat");

        LoadSettings();
        SetTicking(autoPlay);
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private void OnDisable()
    {
        // A deselected inspector must not leave a ticker firing clips in the background.
        SetTicking(false);
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
    }

    /// <summary>Auto-play never straddles the Play-mode boundary: the handles and the AudioMaster on the far side are not the ones it started with.</summary>
    private void OnPlayModeChanged(PlayModeStateChange change)
    {
        StopAuto();
        playing = null;
        playingEnded = true;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField("Script", MonoScript.FromScriptableObject(Set), typeof(MonoScript), false);
        }

        // Only the set's own fields: the inherited leaf fields (clip, volume, pitch, loop...) are unused
        // here, since the picked variant's own values are what play.
        EditorGUILayout.PropertyField(variantsProperty, true);
        EditorGUILayout.PropertyField(pickProperty);

        using (new EditorGUI.DisabledScope(Set.Pick != AudioPick.Random))
        {
            EditorGUILayout.PropertyField(avoidRepeatProperty);
        }

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        DrawPathHelp();
        DrawTransport();
        DrawAutoPlay();
        DrawNowPlaying();
        DrawRollResults();
        DrawRecent();
    }

    // ---- sections ------------------------------------------------------------------------------

    private void DrawPathHelp()
    {
        if (Application.isPlaying)
        {
            if (AudioEventChannel.Instance == null)
            {
                EditorGUILayout.HelpBox("No AudioEventChannel at Resources/AudioEventChannel — nothing can play.", MessageType.Error);
            }
            return;
        }
    }

    private void DrawTransport()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Play next", GUILayout.Height(24f))) PlayOnce();

            using (new EditorGUI.DisabledScope(!IsPlayingSomething()))
            {
                if (GUILayout.Button("Stop", GUILayout.Height(24f))) StopPlaying();
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent($"Roll ×{RollCount}", $"Picks {RollCount} times without playing, and counts what came up. Advances the In Order cursor."), EditorStyles.miniButton))
            {
                Roll();
            }

            if (GUILayout.Button(new GUIContent("Reset order", "Puts the In Order cursor back to the first variant."), EditorStyles.miniButton))
            {
                Set.ResetOrder();
                rollResults.Clear();
            }
        }

        Audio next = Set.Peek();
        string nextLabel = Set.Pick == AudioPick.InOrder
            ? (next != null ? next.name : "—")
            : "any (Random)";
        EditorGUILayout.LabelField("Next up", nextLabel, EditorStyles.miniLabel);
    }

    private void DrawAutoPlay()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Auto-play", EditorStyles.miniBoldLabel);

            bool wasOn = autoPlay;
            autoPlay = EditorGUILayout.ToggleLeft("Keep playing", autoPlay);

            using (new EditorGUI.DisabledScope(!autoPlay))
            {
                autoMode = (AudioRepeatMode)EditorGUILayout.EnumPopup("Mode", autoMode);

                using (new EditorGUI.DisabledScope(autoMode == AudioRepeatMode.OnEnd))
                {
                    interval = Mathf.Max(0f, EditorGUILayout.FloatField(
                        new GUIContent("Interval (s)", "Gap after a clip ends, or the period between starts in Every interval mode."),
                        interval));

                    intervalVariation = Mathf.Max(0f, EditorGUILayout.FloatField(
                        new GUIContent("± Variation (s)", "Random amount added to or taken off each interval. Same knob as a AudioRepeater's, so the rhythm you audition is the rhythm you can build."),
                        intervalVariation));
                }
            }

            if (autoPlay && autoMode != AudioRepeatMode.EveryInterval && AnyVariantLoops(Set, MaxNesting))
            {
                EditorGUILayout.HelpBox("A looping clip never ends, so waiting for the end would stall on it. Looping picks fall back to the interval.", MessageType.Warning);
            }

            if (autoPlay != wasOn)
            {
                if (autoPlay) StartAuto();
                else StopAuto();
            }

            if (GUI.changed) SaveSettings();
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
        EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(), length > 0f ? time / length : 0f, $"{label}  {time:0.00}s / {length:0.00}s");
    }

    private void DrawRollResults()
    {
        if (rollResults.Count == 0) return;

        EditorGUILayout.LabelField($"Last {RollCount} picks", EditorStyles.miniBoldLabel);
        foreach (KeyValuePair<Audio, int> entry in rollResults)
        {
            string name = entry.Key != null ? entry.Key.name : "(empty)";
            EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(GUILayout.Height(14f)), entry.Value / (float)RollCount, $"{name}  ×{entry.Value}");
        }
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

    // ---- playing -------------------------------------------------------------------------------

    /// <summary>Plays one pick, through the channel in Play mode and the editor preview otherwise.</summary>
    private void PlayOnce()
    {
        if (Application.isPlaying)
        {
            AudioEventChannel channel = AudioEventChannel.Instance;
            if (channel == null) return;

            playingEnded = false;

            // The set itself is handed over, not a locally picked clip: that is the path the game takes,
            // so the master's own resolve is what runs and its Playing list credits the set. The clip it
            // landed on is read back off the handle.
            playing = channel.Play(Set, new AudioMaster.PlayOptions { OnEnded = OnClipEnded });

            if (playing == null)
            {
                playingEnded = true;
                Debug.LogWarning($"AudioSet '{Set.name}': no AudioMaster is listening on the channel, so nothing played.", Set);
                StopAuto();
                return;
            }

            PushRecent(playing.Clip);
            lastLeaf = playing.Clip;
            return;
        }

        Audio leaf = Set.GetAudio();
        if (leaf == null || leaf.AudioClip == null)
        {
            StopAuto();
            return;
        }

        lastLeaf = leaf;
        PushRecent(leaf);
        AudioPreview.Play(leaf);
    }

    private void StopPlaying()
    {
        if (Application.isPlaying)
        {
            AudioEventChannel channel = AudioEventChannel.Instance;
            if (playing != null && channel != null) channel.Stop(playing);
            playing = null;
            playingEnded = true;
            return;
        }

        AudioPreview.Stop();
    }

    private void OnClipEnded(bool completed)
    {
        playingEnded = true;
        playing = null;
    }

    private bool IsPlayingSomething() =>
        Application.isPlaying ? playing != null && !playingEnded : AudioPreview.IsPlaying(lastLeaf);

    private AudioSource CurrentSource()
    {
        if (Application.isPlaying) return playing?.Source;
        return AudioPreview.IsPlaying(lastLeaf) ? AudioPreview.Source : null;
    }

    private void PushRecent(Audio clip)
    {
        recent.Add(clip);
        if (recent.Count > RecentShown) recent.RemoveAt(0);
    }

    private void Roll()
    {
        var counts = new Dictionary<Audio, int>();
        var order = new List<Audio>();
        int empties = 0;

        for (int i = 0; i < RollCount; i++)
        {
            Audio picked = Set.GetAudio();

            // a null pick is an empty list slot, and a Dictionary will not take it as a key
            if (picked == null)
            {
                empties++;
                continue;
            }

            if (!counts.ContainsKey(picked))
            {
                counts[picked] = 0;
                order.Add(picked);
            }
            counts[picked]++;
        }

        rollResults.Clear();
        foreach (Audio clip in order) rollResults.Add(new KeyValuePair<Audio, int>(clip, counts[clip]));
        if (empties > 0) rollResults.Add(new KeyValuePair<Audio, int>(null, empties));
    }

    // ---- auto-play -----------------------------------------------------------------------------

    private void StartAuto()
    {
        autoPlay = true;
        waitingForEnd = false;
        nextDueAt = EditorApplication.timeSinceStartup;
        SetTicking(true);
        SaveSettings();
    }

    private void StopAuto()
    {
        autoPlay = false;
        waitingForEnd = false;
        SetTicking(false);
        SaveSettings();
    }

    private void SetTicking(bool on)
    {
        if (on == subscribed) return;

        if (on) EditorApplication.update += Tick;
        else EditorApplication.update -= Tick;

        subscribed = on;
    }

    /// <summary>Drives auto-play off the editor's own clock.</summary>
    /// <remarks>
    /// <see cref="EditorApplication.update"/> is throttled while the editor is unfocused, so intervals
    /// drift when you are in another window. Fine for auditioning, not a timing reference.
    /// </remarks>
    private void Tick()
    {
        if (!autoPlay || target == null)
        {
            SetTicking(false);
            return;
        }

        double now = EditorApplication.timeSinceStartup;

        if (waitingForEnd)
        {
            if (IsPlayingSomething()) return;

            waitingForEnd = false;
            nextDueAt = now + (autoMode == AudioRepeatMode.OnEndGap ? NextGap() : 0d);
        }

        if (now < nextDueAt) return;

        PlayOnce();

        if (!autoPlay) return; // PlayOnce gave up (nothing to play, or no master listening)

        if (WaitsForEnd()) waitingForEnd = true;
        else nextDueAt = now + Mathf.Max(MinInterval, NextGap());

        Repaint();
    }

    /// <summary>One interval with its random variation applied, never below zero. Rolled per gap, so no two are the same.</summary>
    private float NextGap() =>
        intervalVariation > 0f
            ? Mathf.Max(0f, interval + Random.Range(-intervalVariation, intervalVariation))
            : Mathf.Max(0f, interval);

    /// <summary>Whether the current mode should wait for the clip to finish. A looping pick never does, or it would wait forever.</summary>
    private bool WaitsForEnd()
    {
        if (autoMode == AudioRepeatMode.EveryInterval) return false;
        return lastLeaf == null || !lastLeaf.Loop;
    }

    private static bool AnyVariantLoops(AudioSet set, int depth)
    {
        if (set == null || depth <= 0) return false;

        foreach (Audio variant in set.Variants)
        {
            if (variant == null) continue;

            bool loops = variant is AudioSet nested
                ? AnyVariantLoops(nested, depth - 1)
                : variant.Loop;

            if (loops) return true;
        }

        return false;
    }

    // ---- settings ------------------------------------------------------------------------------

    private string KeyPrefix => $"AudioSetEditor.{AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(target))}.";

    private void LoadSettings()
    {
        autoPlay = SessionState.GetBool(KeyPrefix + "auto", false);
        autoMode = (AudioRepeatMode)SessionState.GetInt(KeyPrefix + "mode", (int)AudioRepeatMode.OnEnd);
        interval = SessionState.GetFloat(KeyPrefix + "interval", 1f);
        intervalVariation = SessionState.GetFloat(KeyPrefix + "variation", 0f);
    }

    private void SaveSettings()
    {
        SessionState.SetBool(KeyPrefix + "auto", autoPlay);
        SessionState.SetInt(KeyPrefix + "mode", (int)autoMode);
        SessionState.SetFloat(KeyPrefix + "interval", interval);
        SessionState.SetFloat(KeyPrefix + "variation", intervalVariation);
    }
}
