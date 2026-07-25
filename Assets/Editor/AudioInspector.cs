using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Audio))]
public class AudioInspector : Editor
{
    private static GameObject previewHost;
    private static AudioSource previewSource;
    private static Audio previewTarget;

    /// <summary>Preview playhead at the last repaint, in samples. A drop means the loop wrapped.</summary>
    private static int previewLastSamples;

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
            TickPreviewPitch(audio);

            float length = previewSource.clip.length;
            float time = previewSource.time;
            Rect r = EditorGUILayout.GetControlRect();
            EditorGUI.ProgressBar(r, length > 0 ? time / length : 0f, $"{time:0.00}s / {length:0.00}s");

            // show the pitch actually sounding, not the authored one: with PitchVariation the two
            // differ, and per-loop randomization is otherwise invisible while auditioning.
            EditorGUILayout.LabelField("Playing at pitch", previewSource.pitch.ToString("0.000"));
            Repaint();
        }
    }

    /// <summary>Re-rolls the preview's pitch each time a looping clip wraps, so the inspector auditions the same variation the game will play.</summary>
    /// <remarks>
    /// A looping AudioSource never reports a stop, so the wrap is spotted from the playhead dropping
    /// back toward zero, the same way <see cref="AudioMaster"/> does it at runtime. This only ticks
    /// while the inspector is repainting, which is exactly when the preview is being listened to.
    /// </remarks>
    private static void TickPreviewPitch(Audio audio)
    {
        if (!audio.WantsPerLoopPitch)
        {
            return;
        }

        int samples = previewSource.timeSamples;
        if (samples < previewLastSamples)
        {
            previewSource.pitch = audio.GetRandomizedPitch();
        }
        previewLastSamples = samples;
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
        // randomized, not the raw Pitch: the preview should sound like the game, and pressing
        // Play repeatedly is how you audition the spread PitchVariation gives you.
        previewSource.pitch = audio.GetRandomizedPitch();
        previewSource.loop = audio.Loop;
        previewLastSamples = 0;
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
