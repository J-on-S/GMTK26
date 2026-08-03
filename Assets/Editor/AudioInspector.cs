using UnityEditor;
using UnityEngine;

/// <summary>Adds Play/Stop auditioning to an <see cref="Audio"/> asset, plus the pitch it is actually sounding at.</summary>
/// <remarks>The audition itself lives in <see cref="AudioPreview"/>, shared with the Audio Set inspector so both sound identical and only one hidden source exists.</remarks>
[CustomEditor(typeof(Audio))]
public class AudioInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        Audio audio = (Audio)target;

        EditorGUILayout.Space();

        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = audio.AudioClip != null;
            if (GUILayout.Button(AudioPreview.IsPlaying(audio) ? "Restart" : "Play"))
            {
                AudioPreview.Play(audio);
            }
            GUI.enabled = AudioPreview.IsPlaying(audio);
            if (GUILayout.Button("Stop"))
            {
                AudioPreview.Stop();
            }
            GUI.enabled = true;
        }

        AudioSource source = AudioPreview.Source;

        if (AudioPreview.IsPlaying(audio) && source != null && source.clip != null)
        {
            AudioPreview.TickPitch(audio);

            float length = source.clip.length;
            float time = source.time;
            Rect r = EditorGUILayout.GetControlRect();
            EditorGUI.ProgressBar(r, length > 0 ? time / length : 0f, $"{time:0.00}s / {length:0.00}s");

            // show the pitch actually sounding, not the authored one: with PitchVariation the two
            // differ, and per-loop randomization is otherwise invisible while auditioning.
            EditorGUILayout.LabelField("Playing at pitch", source.pitch.ToString("0.000"));
            Repaint();
        }
    }
}
