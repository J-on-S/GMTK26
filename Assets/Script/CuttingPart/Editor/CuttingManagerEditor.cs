using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CuttingManager))]
public class CuttingManagerEditor : Editor
{
    private const string KeyPrefix = "CuttingManagerEditor.";
    private const string InlineKey = KeyPrefix + "showInline";

    // startAngle/endAngle are not here: they are per-cut geometry drawn with the target above.
    private static readonly string[] TuningFields =
    {
        "cameraFOV", "scalpelAngleLead", "guideLineWidth", "guideHoverLength", "guideResolution",
        "cameraPreset", "curvePreset", "scalpelSurfacePreset",
    };

      private static readonly string[] HardwareFields =
    {
        "scalpelFollow",
    };


    private void OnDisable()
    {
        if (CutPreview.IsRunning && CutPreview.Active == target as CuttingManager)
        {
            CutPreview.Stop();
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var manager = (CuttingManager)target;

        DrawWiringStatus(manager);

        DrawTargetSection();
        DrawPlaySection();
        DrawCameraTravelSection();
        DrawSoundSection();
        DrawSeveredPieceSection(manager);
        DrawFinisherSection();
        DrawHardwareSection();
        DrawPreviewSection(manager);
        DrawLiveStateSection(manager);

        serializedObject.ApplyModifiedProperties();

        DrawActions(manager);
    }

    // ---- sections -------------------------------------------------------------------------------

    private void DrawTargetSection()
    {
        using (var section = new Section("target", "What this cut is", true, NameOf("GameObjectBeingCut", "no body")))
        {
            if (!section.Open) return;

            Draw("GameObjectBeingCut");
            Draw("loopGuide");

            // identity: what the piece IS -- which also names it -- and what it takes to cut it.
            Draw("bodyPartType");
            Draw("requiredTool");

            // geometry, so it sits with the target rather than in the tuning block: it is fixed
            // by where this cut's plane is.
            Draw("startAngle");
            Draw("endAngle");
            Draw("orbitAngleOffset");

            // framing is geometry too -- an orbit radius and an angleOffset only mean anything against
            // THIS cut's ring -- so it sits with the target rather than with the tuning below.
            Draw("cameraOrbitPreset");
            Draw("scalpelOrbitPreset");
        }
    }

    private void DrawPlaySection()
    {
        using (var section = new Section("play", "How it plays", true, null))
        {
            if (!section.Open) return;

            DrawTuning();
        }
    }

    private void DrawCameraTravelSection()
    {
        SerializedProperty enter = serializedObject.FindProperty("enterTravelTime");
        SerializedProperty exit = serializedObject.FindProperty("exitTravelTime");
        string summary = enter != null && exit != null
            ? $"in {enter.floatValue:0.##}s, out {exit.floatValue:0.##}s"
            : null;

        using (var section = new Section("travel", "Camera travel", false, summary))
        {
            if (!section.Open) return;
            Draw("enterTravelTime");
            Draw("exitTravelTime");
            Draw("travelEase");
        }
    }

    private void DrawSoundSection()
    {
        using (var section = new Section("sound", "Sound", false, NameOf("soundPreset", "silent")))
        {
            if (!section.Open) return;
            Draw("soundPreset");
        }
    }

    /// <summary>Draws the severed-piece tuning, which lives on the <see cref="SeveredPiece"/> sibling now, not the manager.</summary>
    /// <remarks>Drawn through a nested SerializedObject so this one inspector still edits both components,
    /// rather than making the author select the sibling to reach a handful of fields.</remarks>
    private void DrawSeveredPieceSection(CuttingManager manager)
    {
        SeveredPiece piece = manager.SeveredPieceOutfitter;

        using (var section = new Section("severed", "Severed piece", false))
        {
            if (!section.Open) return;

            if (piece == null)
            {
                EditorGUILayout.HelpBox("No SeveredPiece component. It is required and normally added automatically; re-select this object to add it.", MessageType.Warning);
                return;
            }

            var pieceObject = new SerializedObject(piece);
            pieceObject.Update();
            EditorGUILayout.PropertyField(pieceObject.FindProperty("health"));
            EditorGUILayout.PropertyField(pieceObject.FindProperty("audioPreset"));
            EditorGUILayout.PropertyField(pieceObject.FindProperty("positionOffset"));
            EditorGUILayout.PropertyField(pieceObject.FindProperty("rotationOffset"));
            EditorGUILayout.PropertyField(pieceObject.FindProperty("holdScaleMultiplier"));
            pieceObject.ApplyModifiedProperties();
        }
    }

    private void DrawFinisherSection()
    {
        using (var section = new Section("finisher", "Finisher", false, NameOf("finisher", "none")))
        {
            if (!section.Open) return;

            SerializedProperty finisher = serializedObject.FindProperty("finisher");
            EditorGUILayout.PropertyField(finisher);
            if (finisher.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("Optional. Without one the cut splices and quits the instant progress hits 1, with no close-up.", MessageType.None);
            }
        }
    }

    /// <summary>Draws the hardware slots this cut drives, with how many are filled on the header.</summary>
    private void DrawHardwareSection()
    {
        int filled = 0;
        for (int i = 0; i < HardwareFields.Length; i++)
        {
            SerializedProperty property = serializedObject.FindProperty(HardwareFields[i]);
            if (property != null && property.objectReferenceValue != null) filled++;
        }

        using (var section = new Section("hardware", "Camera, scalpel and speed", false, $"{filled}/{HardwareFields.Length} set"))
        {
            if (!section.Open) return;

            for (int i = 0; i < HardwareFields.Length; i++)
            {
                Draw(HardwareFields[i]);
            }
        }
    }

    private void DrawLiveStateSection(CuttingManager manager)
    {
        // enumValueIndex is -1 on a mixed multi-selection, which would index out of the names array
        SerializedProperty phase = serializedObject.FindProperty("phase");
        string summary = phase != null && phase.enumValueIndex >= 0 && phase.enumValueIndex < phase.enumDisplayNames.Length
            ? phase.enumDisplayNames[phase.enumValueIndex]
            : null;

        using (var section = new Section("state", "Live state", false, summary))
        {
            if (!section.Open) return;

            using (new EditorGUI.DisabledScope(true))
            {
                Draw("phase");
                Draw("currentAngle");
                EditorGUILayout.FloatField("Current Progress", Application.isPlaying ? manager.currentProgress : 0f);
            }
        }
    }

    // ---- pieces ---------------------------------------------------------------------------------

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

    /// <summary>What an object field points at, for a section header; <paramref name="whenEmpty"/> when it points at nothing.</summary>
    private string NameOf(string fieldName, string whenEmpty)
    {
        SerializedProperty property = serializedObject.FindProperty(fieldName);
        if (property == null) return whenEmpty;
        return property.objectReferenceValue != null ? property.objectReferenceValue.name : whenEmpty;
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

      
            serializedObject.Update();
        }
    }


    private void DrawTuning()
    {
        bool show = SessionState.GetBool(InlineKey, true);
        show = EditorGUILayout.Foldout(show, "Tuning", true);
        SessionState.SetBool(InlineKey, show);

        if (!show) return;

        EditorGUI.indentLevel++;
        for (int i = 0; i < TuningFields.Length; i++)
        {
            Draw(TuningFields[i]);
        }
        EditorGUI.indentLevel--;
    }

    /// <summary>Draws the edit-mode preview controls for the camera's sweep from start angle to end.</summary>
    private void DrawPreviewSection(CuttingManager manager)
    {
        bool running = !Application.isPlaying && CutPreview.IsRunning && CutPreview.Active == manager;

        using (var section = new Section("preview", "Preview", true, running ? "running" : null))
        {
            if (!section.Open) return;

            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Preview is edit-mode only. The game is driving the camera.", MessageType.None);
                return;
            }

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
            float scrubbed = EditorGUILayout.Slider("Angle", CutPreview.Angle, manager.startAngle, manager.endAngle);
            if (!Mathf.Approximately(scrubbed, CutPreview.Angle))
            {
                CutPreview.ScrubTo(scrubbed);
            }

            EditorGUILayout.HelpBox("Editing the CameraFollowPreset while this runs reshapes the orbit live. The camera is put back on Stop.", MessageType.None);
        }
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

        // no play-mode gate: the preview mesh is built in edit mode too, which is where authoring wants it
        using (new EditorGUI.DisabledScope(manager.GameObjectBeingCut == null))
        {
            if (GUILayout.Button(new GUIContent(
                    "Copy the lower hull",
                    "Spawns a standalone copy of the piece this cut would sever, leaving the body whole.")))
            {
                manager.CopyLowerHull();
            }
        }
    }

  
    private readonly struct Section : IDisposable
    {
        public readonly bool Open;

        /// <param name="summary">Shown greyed after the title, so a closed section still says what it is set to. Optional.</param>
        public Section(string key, string title, bool openByDefault, string summary = null)
        {
            string stateKey = KeyPrefix + key;
            bool wasOpen = SessionState.GetBool(stateKey, openByDefault);

            string label = string.IsNullOrEmpty(summary) ? title : $"{title}   ({summary})";
            Open = EditorGUILayout.BeginFoldoutHeaderGroup(wasOpen, label);

            if (Open != wasOpen)
            {
                SessionState.SetBool(stateKey, Open);
            }

            if (Open)
            {
                EditorGUI.indentLevel++;
            }
        }

        public void Dispose()
        {
            if (Open)
            {
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.Space(2f);
        }
    }
}
