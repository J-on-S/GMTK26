using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds an <see cref="AudioSet"/> out of the <see cref="Audio"/> assets currently selected in the Project
/// window -- the usual way a set is born, from four takes of the same footstep sitting side by side.
/// </summary>
public static class AudioSetCreator
{
    private const string MenuPath = "Assets/Create/Audio Set from Selection";

    [MenuItem(MenuPath, true)]
    private static bool ValidateCreate() => SelectedAudio().Count > 0;

    [MenuItem(MenuPath, false, 101)]
    private static void Create()
    {
        List<Audio> selected = SelectedAudio();
        if (selected.Count == 0) return;

        var set = ScriptableObject.CreateInstance<AudioSet>();

        // variants is private, so it is filled the way the inspector would rather than by reflection
        var serialized = new SerializedObject(set);
        SerializedProperty variants = serialized.FindProperty("variants");
        variants.arraySize = selected.Count;
        for (int i = 0; i < selected.Count; i++)
        {
            variants.GetArrayElementAtIndex(i).objectReferenceValue = selected[i];
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();

        string path = AssetDatabase.GenerateUniqueAssetPath($"{FolderOf(selected[0])}/{SuggestName(selected)}.asset");

        // CreateAsset over AssetDatabase.CreateAsset: this drops the new asset into the Project window
        // already in rename mode, which is where you want to be right after making one.
        ProjectWindowUtil.CreateAsset(set, path);
    }

    /// <summary>The selected Audio assets, in the order the Project window has them.</summary>
    private static List<Audio> SelectedAudio()
    {
        var found = new List<Audio>();

        foreach (Object obj in Selection.GetFiltered(typeof(Audio), SelectionMode.Assets))
        {
            if (obj is Audio audio) found.Add(audio);
        }

        return found;
    }

    private static string FolderOf(Object asset)
    {
        string path = AssetDatabase.GetAssetPath(asset);
        string folder = System.IO.Path.GetDirectoryName(path);
        return string.IsNullOrEmpty(folder) ? "Assets" : folder.Replace('\\', '/');
    }

    /// <summary>A name from what the clips have in common: four "footstep1..4" become "footstepSet".</summary>
    /// <remarks>Falls back to the plain default when the names share nothing, since a wrong guess is worse than none.</remarks>
    private static string SuggestName(List<Audio> selected)
    {
        string prefix = selected[0].name;

        for (int i = 1; i < selected.Count; i++)
        {
            prefix = CommonPrefix(prefix, selected[i].name);
            if (prefix.Length == 0) break;
        }

        // trailing take numbers and separators are not part of the name the set should carry
        prefix = Regex.Replace(prefix, @"[\s_\-.]*\d*$", "");

        return prefix.Length >= 2 ? prefix + "Set" : "AudioSet";
    }

    private static string CommonPrefix(string a, string b)
    {
        int max = Mathf.Min(a.Length, b.Length);
        int i = 0;
        while (i < max && a[i] == b[i]) i++;
        return a.Substring(0, i);
    }
}
