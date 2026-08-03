using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AudioMaster))]
public class AudioMasterEditor : Editor
{
    private static int _typeFilter = ~0;

    /// <summary>Solo/mute state, keyed by the <see cref="Audio"/> asset so it survives a clip stopping and starting again.</summary>
    /// <remarks>
    /// Isolation is applied through <see cref="AudioSource.mute"/> rather than volume: the fade coroutines in
    /// <see cref="AudioMaster"/> own <c>volume</c> and overwrite it every frame, so anything written there would
    /// be stomped mid-fade. <c>mute</c> is a separate channel nothing else touches.
    /// </remarks>
    private static readonly HashSet<Audio> _soloed = new();
    private static readonly HashSet<Audio> _muted = new();
    private static int _soloTypes;
    private static int _mutedTypes;

    private static readonly AudioType[] Types = (AudioType[])Enum.GetValues(typeof(AudioType));
    private const float TypeColumnWidth = 62f;
    private const float RowLabelWidth = 46f;

    private class Group
    {
        public int Count;
        public AudioMaster.PlayingClip Rep;
        public bool AnyFading;
        public bool AnyPaused;
    }

    public override bool RequiresConstantRepaint() => Application.isPlaying;

    /// <summary>Isolation is only enforced while this inspector is drawing, so drop it when the inspector goes away rather than leaving sources silently muted with no visible cause.</summary>
    private void OnDisable()
    {
        if (target is AudioMaster master) ClearSourceMutes(master);
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        DrawTypeMatrix();

        EditorGUILayout.Space();

        if (!Application.isPlaying) return;

        var master = (AudioMaster)target;
        ApplyIsolation(master);
        DrawPlayingList(master);
    }

    private static bool AnySolo => _soloTypes != 0 || _soloed.Count > 0;

    private static bool IsSilenced(Audio audio)
    {
        int bit = 1 << (int)audio.Type;
        if (_muted.Contains(audio) || (_mutedTypes & bit) != 0) return true;
        return AnySolo && !_soloed.Contains(audio) && (_soloTypes & bit) == 0;
    }

    /// <summary>Pushes the current solo/mute state onto every live source, including ones hidden by the view filter.</summary>
    private static void ApplyIsolation(AudioMaster master)
    {
        foreach (AudioMaster.PlayingClip pc in master.ActiveClips)
        {
            if (pc?.Clip == null || pc.Source == null) continue;
            pc.Source.mute = IsSilenced(pc.Clip);
        }
    }

    private static void ClearSourceMutes(AudioMaster master)
    {
        if (!Application.isPlaying || master == null) return;

        foreach (AudioMaster.PlayingClip pc in master.ActiveClips)
        {
            if (pc?.Source != null) pc.Source.mute = false;
        }
    }

    private void DrawTypeMatrix()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Types", EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                if (AnySolo || _muted.Count > 0 || _mutedTypes != 0)
                {
                    if (GUILayout.Button("Clear isolation", EditorStyles.miniButton, GUILayout.Width(110f)))
                    {
                        ClearIsolation();
                    }
                }
            }

            _typeFilter = DrawTypeRow("Show", _typeFilter);
            _soloTypes = DrawTypeRow("Solo", _soloTypes);
            _mutedTypes = DrawTypeRow("Mute", _mutedTypes);
        }
    }

    private static int DrawTypeRow(string label, int mask)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(label, EditorStyles.miniLabel, GUILayout.Width(RowLabelWidth));

            foreach (AudioType type in Types)
            {
                int bit = 1 << (int)type;
                bool on = (mask & bit) != 0;
                bool now = GUILayout.Toggle(on, TypeLabel(type), EditorStyles.miniButton, GUILayout.Width(TypeColumnWidth));
                if (now != on) mask = now ? mask | bit : mask & ~bit;
            }

            GUILayout.FlexibleSpace();
        }

        return mask;
    }

    private void ClearIsolation()
    {
        _soloed.Clear();
        _muted.Clear();
        _soloTypes = 0;
        _mutedTypes = 0;
        if (target is AudioMaster master) ClearSourceMutes(master);
    }

    private void DrawPlayingList(AudioMaster master)
    {
        var groups = new Dictionary<Audio, Group>();
        var order = new List<Audio>();

        foreach (AudioMaster.PlayingClip pc in master.ActiveClips)
        {
            if (pc == null || pc.Clip == null || !PassesFilter(pc.Clip)) continue;

            if (!groups.TryGetValue(pc.Clip, out Group g))
            {
                g = new Group { Rep = pc };
                groups.Add(pc.Clip, g);
                order.Add(pc.Clip);
            }

            g.Count++;
            g.AnyFading |= pc.CurrentFade != AudioMaster.PlayingClip.FadeState.None;
            g.AnyPaused |= pc.IsPaused;
        }

        EditorGUILayout.LabelField($"Playing ({order.Count})", EditorStyles.boldLabel);

        if (order.Count == 0)
        {
            EditorGUILayout.LabelField(" ", "nothing playing");
            return;
        }

        foreach (Audio audio in order)
        {
            DrawRow(audio, groups[audio]);
        }
    }

    private static void DrawRow(Audio audio, Group g)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                string title = g.Count > 1 ? $"{audio.name}  ×{g.Count}" : audio.name;

                // The row is the leaf clip, since that is what plays and what solo/mute acts on. When a
                // composite picked it, say so -- otherwise an AudioSet's clips look like they came from nowhere.
                Audio from = g.Rep != null ? g.Rep.Requested : null;
                if (from != null && from != audio) title += $"  (from {from.name})";

                EditorGUILayout.LabelField(title, EditorStyles.boldLabel, GUILayout.MinWidth(80));
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(TypeLabel(audio.Type), EditorStyles.miniLabel, GUILayout.Width(60));

                if (g.AnyPaused) Badge("PAUSED");
                if (g.AnyFading) Badge(g.Rep.CurrentFade.ToString());

                DrawSetToggle(_soloed, audio, "S", "Solo: silence every other clip.");
                DrawSetToggle(_muted, audio, "M", "Mute this clip.");
            }

            AudioSource src = g.Rep != null ? g.Rep.Source : null;
            if (src == null) return;

            if (src.clip != null)
            {
                float len = src.clip.length;
                float t = src.time;
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(), len > 0f ? t / len : 0f, $"{t:0.00}s / {len:0.00}s");
            }

            // The bar keeps reading the clip's real volume while muted, so a fade stays legible under isolation.
            string volLabel = IsSilenced(audio) ? $"vol {src.volume:0.00}  (silenced)" : $"vol {src.volume:0.00}";
            EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(GUILayout.Height(14f)), Mathf.Clamp01(src.volume), volLabel);
        }
    }

    private static void DrawSetToggle(HashSet<Audio> set, Audio audio, string label, string tooltip)
    {
        bool on = set.Contains(audio);
        bool now = GUILayout.Toggle(on, new GUIContent(label, tooltip), EditorStyles.miniButton, GUILayout.Width(22f));
        if (now == on) return;

        if (now) set.Add(audio);
        else set.Remove(audio);
    }

    private static void Badge(string text)
    {
        GUIContent content = new(text);
        Rect r = GUILayoutUtility.GetRect(content, EditorStyles.miniButton, GUILayout.ExpandWidth(false));
        GUI.Label(r, content, EditorStyles.miniButton);
    }

    private static bool PassesFilter(Audio audio) => (_typeFilter & (1 << (int)audio.Type)) != 0;

    private static string TypeLabel(AudioType type) => type == AudioType.UISFX ? "UI SFX" : type.ToString();
}
