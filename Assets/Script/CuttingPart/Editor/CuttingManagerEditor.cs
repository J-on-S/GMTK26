using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Inspector for <see cref="CuttingManager"/> that shows only the knobs a given cut still needs.</summary>
/// <remarks>
/// Invariant: inline tuning is drawn only while no preset is assigned, so the fields on screen are
/// always the ones in effect.
/// <para>Invariant: missing wiring is reported before entry rather than surfacing as a null
/// reference on the first frame of the cut.</para>
/// <para>Invariant: this editor is the only thing that lays the component out. The manager carries no
/// <c>[Header]</c> attributes, because one would be drawn a second time inside the section that already
/// names it.</para>
/// <para>The body is a stack of collapsible sections. A cut has far more tuning than any one authoring
/// pass touches, so each section states in its own header what it is set to -- the answer is usually
/// there without opening it, and what is open stays open while the session lasts.</para>
/// </remarks>
[CustomEditor(typeof(CuttingManager))]
public class CuttingManagerEditor : Editor
{
    private const string KeyPrefix = "CuttingManagerEditor.";
    private const string InlineKey = KeyPrefix + "showInline";

    // startAngle/endAngle are not here: they are per-cut geometry drawn with the target above,
    // not tuning a preset can supply.
    private static readonly string[] InlineTuningFields =
    {
        "cameraFOV", "scalpelAngleLead", "guideLineWidth", "guideHoverLength", "guideResolution",
        "cameraPreset", "curvePreset", "scalpelSurfacePreset",
    };

    // sceneCamera and speedDriver are absent on purpose: the manager finds the camera in the
    // scene and provisions the shared driver itself, so neither is a serialized property.
    private static readonly string[] HardwareFields =
    {
        "moveCamera", "scalpelFollow",
    };

    /// <summary>Stops this cut's preview when the inspector is torn down, so deselecting it puts the camera and rig back.</summary>
    /// <remarks>
    /// The preview is a static system on <c>EditorApplication.update</c>, not owned by this editor:
    /// without this, selecting another object leaves it running with the game camera claimed at an
    /// orbit pose and nothing ever puts it back until a domain reload. Only this cut's own preview is
    /// stopped -- another cut's is left alone.
    /// </remarks>
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
        DrawPlaySection(manager);
        DrawCameraTravelSection();
        DrawSoundSection();
        DrawSeveredPieceSection();
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

            // identity: what the piece is called and what it takes to cut it. Per-cut, never from a preset.
            Draw("itemName");
            Draw("bodyPartType");
            Draw("requiredToolName");

            // geometry, so it sits with the target rather than in the tuning block: it is fixed by
            // where this cut's plane is, and a preset must never move it.
            Draw("startAngle");
            Draw("endAngle");
        }
    }

    private void DrawPlaySection(CuttingManager manager)
    {
        SerializedProperty preset = serializedObject.FindProperty("minigamePreset");
        string summary = preset != null && preset.objectReferenceValue != null
            ? preset.objectReferenceValue.name
            : "inline";

        using (var section = new Section("play", "How it plays", true, summary))
        {
            if (!section.Open) return;

            EditorGUILayout.PropertyField(preset);

            if (preset.objectReferenceValue == null)
            {
                DrawInlineTuning(manager);
                return;
            }

            EditorGUILayout.HelpBox("Tuning comes from the preset. Edit the asset to change it, here or in the Project window.", MessageType.None);
            DrawEmbeddedPreset(preset);
        }
    }

    // not in the inline block: no preset carries the travel, so these apply either way.
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

    private void DrawSeveredPieceSection()
    {
        using (var section = new Section("severed", "Severed piece", false))
        {
            if (!section.Open) return;
            Draw("severedPieceAudioPreset");
            Draw("SeveredPieceHealth");
            Draw("severedPiecePositionOffset");
            Draw("severedPieceRotationOffset");
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

            // AutoWire wrote straight to the component, so the serializedObject snapshot taken at the top
            // of OnInspectorGUI is now stale: without this resync the fields below still draw the old
            // (empty) values and the ApplyModifiedProperties at the end pushes them back, so the wiring
            // reads as reverted -- most visibly on a prefab, where it looks like the edit would not save.
            serializedObject.Update();
        }
    }

    /// <summary>Inline tuning, shown only while no preset is assigned, plus the button that turns it into one.</summary>
    /// <remarks>A plain foldout, not a header group: this one is nested inside a section, and header
    /// groups cannot nest.</remarks>
    private void DrawInlineTuning(CuttingManager manager)
    {
        bool show = SessionState.GetBool(InlineKey, true);
        show = EditorGUILayout.Foldout(show, "Inline tuning (no preset assigned)", true);
        SessionState.SetBool(InlineKey, show);

        if (!show) return;

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

            // CreatePresetFrom assigned minigamePreset straight on the component; resync so the snapshot
            // matches, or the ApplyModifiedProperties at the end of OnInspectorGUI clears the assignment
            // back and the preset appears not to have been set.
            serializedObject.Update();
        }
        EditorGUI.indentLevel--;
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
            float scrubbed = EditorGUILayout.Slider("Angle", CutPreview.Angle, manager.StartAngle, manager.EndAngle);
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

        // read off the manager, not the guide: the guide's copies are pushed outputs, and taking them
        // back would round-trip through whatever the last push happened to leave there.
        preset.curveWidth = manager.guideLineWidth;
        preset.curveHoverLength = manager.guideHoverLength;
        preset.curveResolution = manager.guideResolution;

        AssetDatabase.CreateAsset(preset, path);
        AssetDatabase.SaveAssets();

        Undo.RecordObject(manager, "Assign cut preset");
        manager.minigamePreset = preset;
        EditorUtility.SetDirty(manager);
    }

    /// <summary>One collapsible section: a foldout header, its contents indented, and the open/closed state kept for the session.</summary>
    /// <remarks>
    /// A scope rather than a pair of calls, because <c>BeginFoldoutHeaderGroup</c> must be closed on
    /// every path out -- including the early <c>return</c>s the section bodies are written with. Leaving
    /// one open corrupts the layout of everything drawn after it, in this inspector and the next.
    /// <para>State lives in <see cref="SessionState"/>, so it survives assembly reloads and entering
    /// play mode but does not follow the project to anyone else's machine.</para>
    /// <para>Header groups cannot be nested; sections are top-level only, and anything folded inside one
    /// uses a plain <c>Foldout</c>.</para>
    /// </remarks>
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
