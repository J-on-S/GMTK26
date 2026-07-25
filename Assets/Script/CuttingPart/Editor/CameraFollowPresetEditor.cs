using UnityEditor;
using UnityEngine;

/// <summary>Draws a <see cref="CameraFollowPreset"/> as one collapsible dropdown per tuning category.</summary>
/// <remarks>Twenty-four flat fields is a wall; grouped, the asset reads as five decisions. The groups come from <see cref="CameraFollowCategories"/>, shared with the component's inspector so both look the same.</remarks>
[CustomEditor(typeof(CameraFollowPreset))]
public class CameraFollowPresetEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        CameraFollowCategoryDrawer.DrawGroups(serializedObject, "CameraFollowPresetEditor", false);
        serializedObject.ApplyModifiedProperties();
    }
}

/// <summary>Shared group drawing for the CameraFollow component and its preset asset.</summary>
public static class CameraFollowCategoryDrawer
{
    /// <summary>Draws every category as a foldout. Foldout state is per key, so the component and the asset remember their own.</summary>
    /// <param name="serialized">Object holding the tuning properties.</param>
    /// <param name="stateKey">Prefix for the remembered open/closed state.</param>
    /// <param name="readOnly">Grey the fields out -- used when a preset is driving them.</param>
    public static void DrawGroups(SerializedObject serialized, string stateKey, bool readOnly)
    {
        var groups = CameraFollowCategories.All;
        for (int g = 0; g < groups.Length; g++)
        {
            string key = $"{stateKey}.{groups[g].Title}";
            bool open = SessionState.GetBool(key, g == 0);
            open = EditorGUILayout.Foldout(open, groups[g].Title, true, EditorStyles.foldoutHeader);
            SessionState.SetBool(key, open);

            if (!open)
            {
                continue;
            }

            EditorGUI.indentLevel++;
            using (new EditorGUI.DisabledScope(readOnly))
            {
                string[] fields = groups[g].Fields;
                for (int f = 0; f < fields.Length; f++)
                {
                    SerializedProperty property = serialized.FindProperty(fields[f]);
                    // a renamed or removed field should show up as a visible gap, not a crash
                    if (property == null)
                    {
                        EditorGUILayout.LabelField(fields[f], "missing");
                        continue;
                    }
                    EditorGUILayout.PropertyField(property);
                }
            }
            EditorGUI.indentLevel--;
        }
    }
}
