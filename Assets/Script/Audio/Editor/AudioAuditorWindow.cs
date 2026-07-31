using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

/// <summary>Audits the project's audio: raw clips no <see cref="Audio"/> asset wraps, <see cref="Audio"/>
/// assets nothing references, and source clips imported more than once.</summary>
/// <remarks>
/// A manual tool: scanning walks every scene, prefab and asset, so it runs on a button press rather than
/// continuously. "Used" is by static reference (a serialized link from a scanned root), so audio only
/// ever reached from a runtime-spawned object it cannot see may read as unused -- keep the "prefabs count
/// as roots" toggle on to cover the common case of spawned prefabs.
/// </remarks>
public class AudioAuditorWindow : EditorWindow
{
    [MenuItem("Tools/Audio/Audio Auditor")]
    private static void Open()
    {
        GetWindow<AudioAuditorWindow>("Audio Auditor").minSize = new Vector2(360, 400);
    }

    // ---- filters ----
    private readonly Dictionary<string, bool> sceneRoots = new();   // scene path -> included as a "used" root
    private bool includePrefabs = true;
    private readonly Dictionary<AudioType, bool> typeFilter = new()
    {
        { AudioType.SFX, true }, { AudioType.Music, true }, { AudioType.UISFX, true }, { AudioType.Dialogue, true },
    };

    // ---- results ----
    private List<AudioClip> clipsWithoutAudio = new();
    private List<Audio> unusedAudio = new();
    private List<List<AudioClip>> duplicateClips = new();
    private bool scanned;

    private Vector2 scroll;
    private bool showFilters = true, showOrphans = true, showUnused = true, showDupes = true;

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("Audio Auditor", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Scan the project for audio problems. 'Used' is by static reference from the selected scenes (and prefabs, if enabled).", MessageType.None);

        DrawFilters();

        if (GUILayout.Button("Scan", GUILayout.Height(28)))
        {
            Scan();
        }

        if (scanned)
        {
            DrawOrphans();
            DrawUnused();
            DrawDuplicates();
        }

        EditorGUILayout.EndScrollView();
    }

    // ---- filter UI -----------------------------------------------------------------------------

    private void DrawFilters()
    {
        showFilters = EditorGUILayout.BeginFoldoutHeaderGroup(showFilters, "Filters");
        if (showFilters)
        {
            EditorGUI.indentLevel++;

            includePrefabs = EditorGUILayout.Toggle(new GUIContent("Prefabs count as used", "Treat every prefab as a root when deciding what is used, so audio only referenced by a runtime-spawned prefab is not flagged unused."), includePrefabs);

            EditorGUILayout.LabelField("Audio types shown in 'unused'", EditorStyles.miniBoldLabel);
            foreach (AudioType type in System.Enum.GetValues(typeof(AudioType)).Cast<AudioType>())
            {
                typeFilter[type] = EditorGUILayout.ToggleLeft(type.ToString(), typeFilter.TryGetValue(type, out bool on) && on);
            }

            EditorGUILayout.LabelField("Scenes searched for references", EditorStyles.miniBoldLabel);
            SyncSceneList();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("All", EditorStyles.miniButton)) SetAllScenes(true);
                if (GUILayout.Button("None", EditorStyles.miniButton)) SetAllScenes(false);
            }
            foreach (string path in sceneRoots.Keys.ToList())
            {
                sceneRoots[path] = EditorGUILayout.ToggleLeft(Path.GetFileNameWithoutExtension(path), sceneRoots[path]);
            }

            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void SyncSceneList()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Scene"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!sceneRoots.ContainsKey(path)) sceneRoots[path] = true;
        }
    }

    private void SetAllScenes(bool on)
    {
        foreach (string key in sceneRoots.Keys.ToList()) sceneRoots[key] = on;
    }

    // ---- scan ----------------------------------------------------------------------------------

    private void Scan()
    {
        SyncSceneList();

        List<Audio> allAudio = LoadAll<Audio>("t:Audio");
        List<string> allClipPaths = AssetDatabase.FindAssets("t:AudioClip")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => !string.IsNullOrEmpty(p))
            .Distinct()
            .ToList();

        ScanOrphans(allAudio, allClipPaths);
        ScanUnused(allAudio);
        ScanDuplicates(allClipPaths);

        scanned = true;
    }

    /// <summary>Raw clips no Audio asset points at.</summary>
    private void ScanOrphans(List<Audio> allAudio, List<string> allClipPaths)
    {
        var wrapped = new HashSet<string>();
        foreach (Audio audio in allAudio)
        {
            if (audio.AudioClip == null) continue;
            string path = AssetDatabase.GetAssetPath(audio.AudioClip);
            if (!string.IsNullOrEmpty(path)) wrapped.Add(path);
        }

        clipsWithoutAudio = allClipPaths
            .Where(p => !wrapped.Contains(p))
            .Select(AssetDatabase.LoadAssetAtPath<AudioClip>)
            .Where(c => c != null)
            .ToList();
    }

    /// <summary>Audio assets no selected root references, of a shown type.</summary>
    private void ScanUnused(List<Audio> allAudio)
    {
        var roots = new List<string>();
        roots.AddRange(sceneRoots.Where(kv => kv.Value).Select(kv => kv.Key));
        if (includePrefabs)
        {
            roots.AddRange(AssetDatabase.FindAssets("t:Prefab").Select(AssetDatabase.GUIDToAssetPath));
        }

        var referenced = new HashSet<string>();
        foreach (string root in roots.Distinct())
        {
            foreach (string dep in AssetDatabase.GetDependencies(root, true))
            {
                referenced.Add(dep);
            }
        }

        unusedAudio = allAudio
            .Where(a => typeFilter.TryGetValue(a.Type, out bool on) && on)
            .Where(a => !referenced.Contains(AssetDatabase.GetAssetPath(a)))
            .ToList();
    }

    /// <summary>Source clip files with byte-identical content, grouped.</summary>
    private void ScanDuplicates(List<string> allClipPaths)
    {
        var byHash = new Dictionary<string, List<string>>();
        foreach (string path in allClipPaths)
        {
            string hash = FileHash(path);
            if (hash == null) continue;
            if (!byHash.TryGetValue(hash, out List<string> group))
            {
                group = new List<string>();
                byHash[hash] = group;
            }
            group.Add(path);
        }

        duplicateClips = byHash.Values
            .Where(g => g.Count > 1)
            .Select(g => g.Select(AssetDatabase.LoadAssetAtPath<AudioClip>).Where(c => c != null).ToList())
            .Where(g => g.Count > 1)
            .ToList();
    }

    private static string FileHash(string assetPath)
    {
        try
        {
            using var sha = SHA256.Create();
            using FileStream stream = File.OpenRead(assetPath);
            return System.Convert.ToBase64String(sha.ComputeHash(stream));
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static List<T> LoadAll<T>(string filter) where T : Object
    {
        return AssetDatabase.FindAssets(filter)
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where(a => a != null)
            .ToList();
    }

    // ---- result UI -----------------------------------------------------------------------------

    private void DrawOrphans()
    {
        EditorGUILayout.Space(4);
        showOrphans = EditorGUILayout.BeginFoldoutHeaderGroup(showOrphans, $"Clips with no Audio object ({clipsWithoutAudio.Count})");
        if (showOrphans)
        {
            if (clipsWithoutAudio.Count == 0)
            {
                EditorGUILayout.HelpBox("Every AudioClip is wrapped by an Audio asset.", MessageType.Info);
            }
            else
            {
                foreach (AudioClip clip in clipsWithoutAudio) DrawAsset(clip);
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawUnused()
    {
        EditorGUILayout.Space(4);
        showUnused = EditorGUILayout.BeginFoldoutHeaderGroup(showUnused, $"Unused Audio ({unusedAudio.Count})");
        if (showUnused)
        {
            if (unusedAudio.Count == 0)
            {
                EditorGUILayout.HelpBox("Every shown Audio asset is referenced by a selected root.", MessageType.Info);
            }
            else
            {
                foreach (Audio audio in unusedAudio)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(audio.Type.ToString(), GUILayout.Width(70));
                        DrawAsset(audio);
                    }
                }
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawDuplicates()
    {
        EditorGUILayout.Space(4);
        showDupes = EditorGUILayout.BeginFoldoutHeaderGroup(showDupes, $"Duplicate imported clips ({duplicateClips.Count} groups)");
        if (showDupes)
        {
            if (duplicateClips.Count == 0)
            {
                EditorGUILayout.HelpBox("No two clip files share identical content.", MessageType.Info);
            }
            else
            {
                for (int i = 0; i < duplicateClips.Count; i++)
                {
                    EditorGUILayout.LabelField($"Group {i + 1} — {duplicateClips[i].Count} copies", EditorStyles.miniBoldLabel);
                    EditorGUI.indentLevel++;
                    foreach (AudioClip clip in duplicateClips[i]) DrawAsset(clip);
                    EditorGUI.indentLevel--;
                }
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private static void DrawAsset(Object asset)
    {
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField(asset, asset != null ? asset.GetType() : typeof(Object), false);
        }
    }
}
