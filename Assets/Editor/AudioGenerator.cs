using UnityEngine;
using UnityEditor;
using System.IO;

public static class AudioGeneratorSimple
{
    [MenuItem("Assets/Create/Audio From Clip", false, 2000)]
    public static void CreateAudioFromClip()
    {
        foreach (Object obj in Selection.objects)
        {
            string path = AssetDatabase.GetAssetPath(obj);

            // Only process AudioClips
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
                continue;

            CreateFor(clip);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Audio assets created.");
    }

    /// <summary>Creates one <see cref="Audio"/> asset wrapping <paramref name="clip"/>, next to the clip, with a unique name. Shared by the create menu and the AudioClip inspector button.</summary>
    /// <returns>The created asset, already written to disk.</returns>
    public static Audio CreateFor(AudioClip clip)
    {
        string path = AssetDatabase.GetAssetPath(clip);
        string folder = Path.GetDirectoryName(path);

        // next to the clip, unique so a second call never overwrites the first
        string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(folder, clip.name + ".asset"));

        Audio audioAsset = ScriptableObject.CreateInstance<Audio>();
        audioAsset.AudioClip = clip;
        audioAsset.Volume = 1f;
        audioAsset.Loop = false;
        audioAsset.Pan = 0f;

        AssetDatabase.CreateAsset(audioAsset, assetPath);
        AssetDatabase.SaveAssets();
        return audioAsset;
    }

    [MenuItem("Assets/Create/Audio From Clip", true)]
    static bool Validate()
    {
        if (Selection.objects == null || Selection.objects.Length == 0)
            return false;

        foreach (Object obj in Selection.objects)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (AssetDatabase.LoadAssetAtPath<AudioClip>(path) == null)
                return false;
        }

        return true;
    }
}