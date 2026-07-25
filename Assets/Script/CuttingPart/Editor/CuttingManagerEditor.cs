using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Inspector for <see cref="CuttingManager"/> that shows only the knobs a given cut actually still needs.</summary>
/// <remarks>
/// With a <see cref="CutMinigamePreset"/> assigned the seven inline tuning fields are redundant, and
/// the hardware slots are noise once they are wired. Hiding both behind foldouts is what makes the
/// component read as "what am I cutting, and how does it feel" instead of a wall of slots. It also
/// reports what is still unwired, which used to surface only as an NRE on entering the minigame.
/// </remarks>
[CustomEditor(typeof(CuttingManager))]
public class CuttingManagerEditor : Editor
{
    private const string OverridesKey = "CuttingManagerEditor.showOverrides";
    private const string InlineKey = "CuttingManagerEditor.showInline";

    // startAngle/endAngle are not here: they are per-cut geometry drawn with the target above,
    // not tuning a preset can supply.
    private static readonly string[] InlineTuningFields =
    {
        "cameraFOV", "scalpelAngleLead",
        "cameraPreset", "curvePreset", "ScalpelFollowLoopPreset",
    };

    // sceneCamera and speedDriver are absent on purpose: the manager finds the camera in the
    // scene and provisions the shared driver itself, so neither is a serialized property.
    private static readonly string[] HardwareFields =
    {
        "moveCamera", "scalpelFollow",
    };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var manager = (CuttingManager)target;

        DrawWiringStatus(manager);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("What this cut is", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("GameObjectBeingCut"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("loopGuide"));

        // geometry, so it sits with the target rather than in the tuning block: it is fixed by
        // where this cut's plane is, and a preset must never move it.
        EditorGUILayout.PropertyField(serializedObject.FindProperty("startAngle"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("endAngle"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("How it plays", EditorStyles.boldLabel);
        SerializedProperty preset = serializedObject.FindProperty("minigamePreset");
        EditorGUILayout.PropertyField(preset);

        if (preset.objectReferenceValue == null)
        {
            DrawInlineTuning(manager);
        }
        else
        {
            EditorGUILayout.HelpBox("Tuning comes from the preset. Edit the asset to change it, here or in the Project window.", MessageType.None);
            DrawEmbeddedPreset(preset);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Sound", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("soundPreset"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Hardware", EditorStyles.boldLabel);
        DrawHardware();

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("currentAngle"));
            EditorGUILayout.FloatField("Current Progress", Application.isPlaying ? manager.currentProgress : 0f);
        }

        serializedObject.ApplyModifiedProperties();

        DrawPreview(manager);
        DrawActions(manager);
    }

    /// <summary>The box at the top: everything this cut still needs before it can run, and the one button that usually fixes it.</summary>
    private void DrawWiringStatus(CuttingManager manager)
    {
        List<string> missing = manager.MissingWiring();
        if (missing.Count == 0)
        {
            EditorGUILayout.HelpBox("Wired and ready.", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("Not runnable yet, missing:\n• " + string.Join("\n• ", missing), MessageType.Warning);
        }

        if (GUILayout.Button("Auto-wire"))
        {
            Undo.RecordObject(manager, "Auto-wire cut");
            manager.AutoWire();
            EditorUtility.SetDirty(manager);
        }
    }

    /// <summary>Inline tuning, shown only while no preset is assigned, plus the button that turns it into one.</summary>
    private void DrawInlineTuning(CuttingManager manager)
    {
        bool show = SessionState.GetBool(InlineKey, true);
        show = EditorGUILayout.Foldout(show, "Inline tuning (no preset assigned)", true);
        SessionState.SetBool(InlineKey, show);

        if (show)
        {
            EditorGUI.indentLevel++;
            for (int i = 0; i < InlineTuningFields.Length; i++)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty(InlineTuningFields[i]));
            }

            if (GUILayout.Button("Save these as a preset asset"))
            {
                // apply first: the button can be clicked in the same frame a field was edited.
                serializedObject.ApplyModifiedProperties();
                CreatePresetFrom(manager);
            }
            EditorGUI.indentLevel--;
        }
    }

    /// <summary>Draws the assigned preset's own fields inline, so tuning does not cost a trip to the Project window.</summary>
    private void DrawEmbeddedPreset(SerializedProperty presetProperty)
    {
        var asset = (CutMinigamePreset)presetProperty.objectReferenceValue;
        var presetObject = new SerializedObject(asset);
        presetObject.Update();

        EditorGUI.indentLevel++;
        SerializedProperty iterator = presetObject.GetIterator();
        bool enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (iterator.propertyPath == "m_Script") continue;
            EditorGUILayout.PropertyField(iterator, true);
        }
        EditorGUI.indentLevel--;

        presetObject.ApplyModifiedProperties();
    }

    /// <summary>The hardware slots this cut drives, folded away so they stay out of the way once wired.</summary>
    private void DrawHardware()
    {
        int filled = 0;
        for (int i = 0; i < HardwareFields.Length; i++)
        {
            if (serializedObject.FindProperty(HardwareFields[i]).objectReferenceValue != null) filled++;
        }

        bool show = SessionState.GetBool(OverridesKey, false);
        show = EditorGUILayout.Foldout(show, $"Camera, scalpel and speed ({filled}/{HardwareFields.Length} set)", true);
        SessionState.SetBool(OverridesKey, show);

        if (!show)
        {
            return;
        }

        EditorGUI.indentLevel++;
        for (int i = 0; i < HardwareFields.Length; i++)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(HardwareFields[i]));
        }
        EditorGUI.indentLevel--;
    }

    /// <summary>Edit-mode preview: sweep the cut's camera from start to end angle and back, live-tunable.</summary>
    private void DrawPreview(CuttingManager manager)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Preview is edit-mode only; the game is driving the camera.", MessageType.None);
            return;
        }

        bool running = CutPreview.IsRunning && CutPreview.Active == manager;

        if (!running)
        {
            // another manager's preview would be fighting for the same camera
            if (CutPreview.IsRunning)
            {
                EditorGUILayout.HelpBox($"'{CutPreview.Active.name}' is previewing. Starting this one stops it.", MessageType.None);
            }

            if (GUILayout.Button("Start preview"))
            {
                CutPreview.Start(manager);
            }
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(CutPreview.Playing ? "Pause" : "Play"))
            {
                CutPreview.SetPlaying(!CutPreview.Playing);
            }
            if (GUILayout.Button("Stop"))
            {
                CutPreview.Stop();
                return;
            }
        }

        CutPreview.Speed = EditorGUILayout.FloatField(
            new GUIContent("Sweep speed", "Degrees per second. Constant on purpose -- a repeatable sweep, not the player's variable travel."),
            CutPreview.Speed);

        // scrubbing pauses, so a bad spot can be held still and tuned
        float scrubbed = EditorGUILayout.Slider("Angle", CutPreview.Angle, manager.StartAngle, manager.EndAngle);
        if (!Mathf.Approximately(scrubbed, CutPreview.Angle))
        {
            CutPreview.ScrubTo(scrubbed);
        }

        EditorGUILayout.HelpBox("Editing the CameraFollowPreset while this runs reshapes the orbit live. The camera is put back when you press Stop.", MessageType.None);
    }

    private void DrawActions(CuttingManager manager)
    {
        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(!Application.isPlaying || !manager.canEnterMinigame()))
        {
            if (GUILayout.Button("Enter minigame"))
            {
                manager.EnterMinigame();
            }
        }
    }

    /// <summary>Writes this manager's inline values into a new preset asset and assigns it, so an existing hand-tuned cut migrates without retyping.</summary>
    private static void CreatePresetFrom(CuttingManager manager)
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Save cut preset",
            $"{manager.name} Preset",
            "asset",
            "Where should this cut's tuning live?");
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        var preset = ScriptableObject.CreateInstance<CutMinigamePreset>();
        // startAngle/endAngle are not copied: they stay on the manager, so a preset built from
        // this cut can be handed to another one without dragging this cut's geometry along.
        preset.cameraFOV = manager.cameraFOV;
        preset.scalpelAngleLead = manager.scalpelAngleLead;
        preset.cameraPreset = manager.cameraPreset;
        preset.curvePreset = manager.curvePreset;
        preset.scalpelFollowPreset = manager.ScalpelFollowLoopPreset;
        if (manager.loopGuide != null)
        {
            preset.curveWidth = manager.loopGuide.curveWidth;
            preset.curveHoverLength = manager.loopGuide.curveHoverLength;
        }

        AssetDatabase.CreateAsset(preset, path);
        AssetDatabase.SaveAssets();

        Undo.RecordObject(manager, "Assign cut preset");
        manager.minigamePreset = preset;
        EditorUtility.SetDirty(manager);
    }
}
