using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Inspector for <see cref="CuttingManager"/> that shows only the knobs a given cut still needs.</summary>
/// <remarks>
/// Invariant: inline tuning is drawn only while no preset is assigned, so the fields on screen are
/// always the ones in effect.
/// <para>Invariant: missing wiring is reported before entry rather than surfacing as a null
/// reference on the first frame of the cut.</para>
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
        "cameraFOV", "scalpelAngleLead", "guideLineWidth",
        "cameraPreset", "curvePreset", "scalpelSurfacePreset",
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
        Draw("GameObjectBeingCut");
        Draw("loopGuide");

        // identity: what the piece is called and what it takes to cut it. Per-cut, never from a preset.
        
        Draw("bodyPartType");
        Draw("requiredToolName");

        // geometry, so it sits with the target rather than in the tuning block: it is fixed by
        // where this cut's plane is, and a preset must never move it.
        Draw("startAngle");
        Draw("endAngle");

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

        // not in the inline block: no preset carries it, so it applies either way.
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Guide lines", EditorStyles.boldLabel);
        Draw("showGuideLinesInPlay");

        // not in the inline block: no preset carries the travel, so these apply either way.
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Camera travel", EditorStyles.boldLabel);
        Draw("enterTravelTime");
        Draw("exitTravelTime");
        Draw("travelEase");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Sound", EditorStyles.boldLabel);
        Draw("soundPreset");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Severed Piece", EditorStyles.boldLabel);
        Draw("severedPieceAudioPreset");
        Draw("SeveredPieceHealth");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Finisher", EditorStyles.boldLabel);
        SerializedProperty finisher = serializedObject.FindProperty("finisher");
        EditorGUILayout.PropertyField(finisher);
        if (finisher.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox("Optional. Without one the cut splices and quits the instant progress hits 1, with no close-up.", MessageType.None);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Hardware", EditorStyles.boldLabel);
        DrawHardware();

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(true))
        {
            Draw("phase");
            Draw("currentAngle");
            EditorGUILayout.FloatField("Current Progress", Application.isPlaying ? manager.currentProgress : 0f);
        }

        serializedObject.ApplyModifiedProperties();

        DrawPreview(manager);
        DrawActions(manager);
    }

    /// <summary>Draws one field by name, reporting it in the inspector instead of throwing when the name is stale.</summary>
    /// <remarks>Every field here is named as a string, so a rename on the manager turns into a null
    /// property and <c>PropertyField</c> throws, taking the whole inspector with it. Saying which name
    /// went missing costs one branch and names the fix.</remarks>
    private void Draw(string fieldName)
    {
        SerializedProperty property = serializedObject.FindProperty(fieldName);
        if (property == null)
        {
            EditorGUILayout.HelpBox($"CuttingManager has no field '{fieldName}'. It was renamed or removed; update CuttingManagerEditor.", MessageType.Error);
            return;
        }
        EditorGUILayout.PropertyField(property);
    }

    /// <summary>Lists everything this cut still needs before it can run, next to the button that usually fills it in.</summary>
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
                Draw(InlineTuningFields[i]);
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

    /// <summary>Draws the hardware slots this cut drives, folded away once they are wired.</summary>
    private void DrawHardware()
    {
        int filled = 0;
        for (int i = 0; i < HardwareFields.Length; i++)
        {
            SerializedProperty property = serializedObject.FindProperty(HardwareFields[i]);
            if (property != null && property.objectReferenceValue != null) filled++;
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
            Draw(HardwareFields[i]);
        }
        EditorGUI.indentLevel--;
    }

    /// <summary>Draws the edit-mode preview controls for the camera's sweep from start angle to end.</summary>
    private void DrawPreview(CuttingManager manager)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Preview is edit-mode only. The game is driving the camera.", MessageType.None);
            return;
        }

        bool running = CutPreview.IsRunning && CutPreview.Active == manager;

        if (!running)
        {
            // anything else previewing is writing the same camera transform
            if (EditorCameraClaim.IsClaimed)
            {
                EditorGUILayout.HelpBox($"'{EditorCameraClaim.HolderName()}' has the camera. Starting this one stops it.", MessageType.None);
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
            new GUIContent("Sweep speed", "Degrees per second. Fixed rather than the player's variable travel, so two passes frame identically."),
            CutPreview.Speed);

        // scrubbing pauses, so a bad spot can be held still and tuned
        float scrubbed = EditorGUILayout.Slider("Angle", CutPreview.Angle, manager.StartAngle, manager.EndAngle);
        if (!Mathf.Approximately(scrubbed, CutPreview.Angle))
        {
            CutPreview.ScrubTo(scrubbed);
        }

        EditorGUILayout.HelpBox("Editing the CameraFollowPreset while this runs reshapes the orbit live. The camera is put back on Stop.", MessageType.None);
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

    /// <summary>Writes this manager's inline values into a new preset asset and assigns it, doing nothing when the save is cancelled.</summary>
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
        preset.scalpelFollowPreset = manager.scalpelSurfacePreset;
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
