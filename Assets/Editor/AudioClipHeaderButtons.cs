using UnityEditor;
using UnityEngine;

/// <summary>
/// Adds a button to the header of every AudioClip's inspector: "Create Audio" when no <see cref="Audio"/>
/// asset wraps the clip yet, or "Go to Audio" pinging the one that does. Non-destructive -- it draws into
/// the header via <see cref="Editor.finishedDefaultHeaderGUI"/>, so Unity's own import-settings UI is left
/// untouched below it.
/// </summary>
[InitializeOnLoad]
public static class AudioClipHeaderButtons
{
    // FindAssets over every Audio asset is too costly to run each repaint, so the last lookup is cached
    // per clip and only refreshed when the inspected clip changes or the entry goes stale.
    private static AudioClip _cachedClip;
    private static Audio _cachedAudio;
    private static double _cachedAt;
    private const double CacheSeconds = 1.0;

    static AudioClipHeaderButtons()
    {
        Editor.finishedDefaultHeaderGUI += OnHeaderGUI;
    }

    private static void OnHeaderGUI(Editor editor)
    {
        // one clip at a time: the header is a single asset's, and a multi-select edits several importers.
        if (editor.targets.Length != 1 || editor.target is not AudioClip clip) return;

        // an imported file, not a clip embedded in some other asset
        if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(clip))) return;

        Audio existing = FindAudioForClip(clip);

        if (existing != null)
        {
            if (GUILayout.Button($"Go to Audio ({existing.name})"))
            {
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
            }
        }
        else if (GUILayout.Button("Create Audio from this clip"))
        {
            Audio created = AudioGeneratorSimple.CreateFor(clip);

            // seed the cache so the header flips to "Go to Audio" on the very next repaint
            _cachedClip = clip;
            _cachedAudio = created;
            _cachedAt = EditorApplication.timeSinceStartup;

            Selection.activeObject = created;
            EditorGUIUtility.PingObject(created);
        }
    }

    /// <summary>The first <see cref="Audio"/> asset whose clip is <paramref name="clip"/>, or null. Cached per clip so the header isn't scanning every Audio asset each repaint.</summary>
    private static Audio FindAudioForClip(AudioClip clip)
    {
        double now = EditorApplication.timeSinceStartup;

        // a cached, non-stale hit survives if its target still points at this clip: an Audio whose clip
        // was reassigned, or that was deleted, must not keep answering for it.
        if (_cachedClip == clip && now - _cachedAt < CacheSeconds
            && (_cachedAudio == null || _cachedAudio.AudioClip == clip))
        {
            return _cachedAudio;
        }

        Audio found = null;
        foreach (string guid in AssetDatabase.FindAssets("t:Audio"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Audio a = AssetDatabase.LoadAssetAtPath<Audio>(path);
            if (a != null && a.AudioClip == clip)
            {
                found = a;
                break;
            }
        }

        _cachedClip = clip;
        _cachedAudio = found;
        _cachedAt = now;
        return found;
    }
}
