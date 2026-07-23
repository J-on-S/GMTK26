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

            string folder = Path.GetDirectoryName(path);
            string assetPath = Path.Combine(folder, clip.name + ".asset");

            // Avoid overwrite
            assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

            Audio audioAsset = ScriptableObject.CreateInstance<Audio>();
            audioAsset.AudioClip = clip;
            audioAsset.Volume = 1f;
            audioAsset.Loop = false;
            audioAsset.Pan = 0f;

            AssetDatabase.CreateAsset(audioAsset, assetPath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Audio assets created.");
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