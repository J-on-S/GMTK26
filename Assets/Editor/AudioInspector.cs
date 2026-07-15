using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Audio))]
public class AudioInspector : Editor
{
    private static GameObject previewHost;
    private static AudioSource previewSource;
    private static Audio previewTarget;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        Audio audio = (Audio)target;

        EditorGUILayout.Space();

        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = audio.AudioClip != null;
            if (GUILayout.Button(IsPlaying(audio) ? "Restart" : "Play"))
            {
                PlayPreview(audio);
            }
            GUI.enabled = IsPlaying(audio);
            if (GUILayout.Button("Stop"))
            {
                StopPreview();
            }
            GUI.enabled = true;
        }

        if (IsPlaying(audio) && previewSource != null && previewSource.clip != null)
        {
            float length = previewSource.clip.length;
            float time = previewSource.time;
            Rect r = EditorGUILayout.GetControlRect();
            EditorGUI.ProgressBar(r, length > 0 ? time / length : 0f, $"{time:0.00}s / {length:0.00}s");
            Repaint();
        }
    }

    private static bool IsPlaying(Audio audio)
    {
        return previewSource != null
            && previewSource.isPlaying
            && previewTarget == audio;
    }

    private static void PlayPreview(Audio audio)
    {
        EnsureHost();

        previewTarget = audio;
        previewSource.clip = audio.AudioClip;
        previewSource.volume = audio.Volume;
        previewSource.panStereo = audio.Pan;
        previewSource.pitch = audio.Pitch;
        previewSource.loop = audio.Loop;
        previewSource.Play();
    }

    private static void StopPreview()
    {
        if (previewSource != null) previewSource.Stop();
        previewTarget = null;
    }

    private static void EnsureHost()
    {
        if (previewHost != null && previewSource != null) return;

        previewHost = new GameObject("~AudioPreview")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        previewSource = previewHost.AddComponent<AudioSource>();
        previewSource.playOnAwake = false;
    }

    private void OnDisable()
    {
        // Keep the host alive across selection changes so previews can finish.
        // Only destroy when Unity tears down the editor (domain reload handles it).
    }
}
