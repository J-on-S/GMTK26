using UnityEditor;
using UnityEngine;

/// <summary>Inspector for <see cref="CameraFollow"/>: wiring on top, then the framing as one dropdown per category.</summary>
/// <remarks>
/// With a preset assigned the framing fields are copies the preset overwrites, so they are drawn
/// greyed out and edits go to the asset instead. Without one they stay editable, and the button
/// lifts them into a new preset -- the migration path for a follow that was hand-tuned in a scene
/// before presets existed.
/// </remarks>
[CustomEditor(typeof(CameraFollow))]
public class CameraFollowEditor : Editor
{
    /// <summary>Drawn above the categories. Only values worth setting by hand; the rest carry [HideInInspector] and an explicit PropertyField would draw them anyway, which is why they are absent from this list rather than merely attributed.</summary>
    private static readonly string[] WiringFields =
    {
        "rotationSpeed",
    };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var follow = (CameraFollow)target;

        bool driven = serializedObject.FindProperty("preset").objectReferenceValue != null;

        EditorGUILayout.LabelField("Wiring", EditorStyles.boldLabel);
        for (int i = 0; i < WiringFields.Length; i++)
        {
            SerializedProperty property = serializedObject.FindProperty(WiringFields[i]);
            if (property != null)
            {
                EditorGUILayout.PropertyField(property);
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Framing", EditorStyles.boldLabel);
        CameraFollowCategoryDrawer.DrawGroups(serializedObject, "CameraFollowEditor", driven);

        serializedObject.ApplyModifiedProperties();

        if (!driven)
        {
            EditorGUILayout.Space();
            if (GUILayout.Button("Save this framing as a preset"))
            {
                CreatePresetFrom(follow);
            }
        }
    }

    /// <summary>Lifts a hand-tuned follow's values into a new preset asset and assigns it, so nothing has to be retyped.</summary>
    private static void CreatePresetFrom(CameraFollow follow)
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Save camera follow preset",
            $"{follow.name} Framing",
            "asset",
            "Where should this framing live?");
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        var preset = ScriptableObject.CreateInstance<CameraFollowPreset>();
        preset.CopyFrom(follow);

        AssetDatabase.CreateAsset(preset, path);
        AssetDatabase.SaveAssets();

        Undo.RecordObject(follow, "Assign camera follow preset");
        follow.preset = preset;
        EditorUtility.SetDirty(follow);
    }
}
