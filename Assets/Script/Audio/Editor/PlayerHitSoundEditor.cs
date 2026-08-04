using UnityEditor;
using UnityEngine;

/// <summary>
/// Greys out the inline sound list while an Audio Set is assigned, so the fields that are being ignored
/// cannot be mistaken for the ones being played.
/// </summary>
[CustomEditor(typeof(PlayerHitSound))]
public class PlayerHitSoundEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty soundSet = serializedObject.FindProperty("soundSet");
        SerializedProperty channel = serializedObject.FindProperty("channel");

        EditorGUILayout.PropertyField(soundSet);
        EditorGUILayout.PropertyField(channel);

        bool driven = soundSet.objectReferenceValue != null;

        if (driven)
        {
            EditorGUILayout.HelpBox($"Playing from '{soundSet.objectReferenceValue.name}'. The list below is ignored — clear the field above to use it again.", MessageType.Info);
        }

        using (new EditorGUI.DisabledScope(driven))
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("hitSounds"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("decider"));
        }

        serializedObject.ApplyModifiedProperties();
    }
}
