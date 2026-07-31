using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AudioMaster))]
public class AudioMasterEditor : Editor
{
    private static int _typeFilter = ~0;

    private class Group
    {
        public int Count;
        public AudioMaster.PlayingClip Rep;
        public bool AnyFading;
        public bool AnyPaused;
    }

    public override bool RequiresConstantRepaint() => Application.isPlaying;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        DrawTypeFilter();

        EditorGUILayout.Space();

        if (!Application.isPlaying) return;

        DrawPlayingList((AudioMaster)target);
    }

    private static void DrawTypeFilter()
    {
        EditorGUILayout.LabelField("Filter by type", EditorStyles.miniBoldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            foreach (AudioType type in (AudioType[])Enum.GetValues(typeof(AudioType)))
            {
                int bit = 1 << (int)type;
                bool on = (_typeFilter & bit) != 0;
                bool now = GUILayout.Toggle(on, TypeLabel(type), EditorStyles.miniButton);
                if (now != on)
                {
                    _typeFilter = now ? _typeFilter | bit : _typeFilter & ~bit;
                }
            }
        }
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
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel, GUILayout.MinWidth(80));
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(TypeLabel(audio.Type), EditorStyles.miniLabel, GUILayout.Width(60));

                if (g.AnyPaused) Badge("PAUSED");
                if (g.AnyFading) Badge(g.Rep.CurrentFade.ToString());
            }

            AudioSource src = g.Rep != null ? g.Rep.Source : null;
            if (src == null) return;

            if (src.clip != null)
            {
                float len = src.clip.length;
                float t = src.time;
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(), len > 0f ? t / len : 0f, $"{t:0.00}s / {len:0.00}s");
            }

            EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(GUILayout.Height(14f)), Mathf.Clamp01(src.volume), $"vol {src.volume:0.00}");
        }
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
