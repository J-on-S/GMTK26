using UnityEditor;
using UnityEngine;

/// <summary>Inspector for <see cref="CutFinisher"/>, giving the shot scene-view handles and the beat an edit-mode preview.</summary>
/// <remarks>Invariant: inline tuning is drawn only while no preset is assigned, so the fields on screen are always the ones in effect.</remarks>
[CustomEditor(typeof(CutFinisher))]
public class CutFinisherEditor : Editor
{
    private const string InlineKey = "CutFinisherEditor.showInline";

    /// <summary>Everything a preset can supply, in the order it reads best.</summary>
    private static readonly string[] InlineTuningFields =
    {
        "cameraFOV", "easeIn", "easeInCurve",
        "toolPrefab", "toolEuler",
        "sweepAngle", "approachTilt",
        "bobAmp", "bobHz", "autoSlashAfter",
        "hoverHeight", "sweepDist", "slashTime", "slashEase", "holdAfter", "kick",
    };

    /// <summary>Stops this finisher's preview when the inspector is torn down, so deselecting it puts the camera and tool back.</summary>
    /// <remarks>
    /// The preview is a static system on <c>EditorApplication.update</c>, not owned by this editor:
    /// without this, selecting another object leaves it running with the game camera claimed at the
    /// shot and the tool posed mid-swing, and nothing ever puts them back until a domain reload. Only
    /// this finisher's own preview is stopped -- another finisher's is left alone.
    /// </remarks>
    private void OnDisable()
    {
        if (FinisherPreview.IsRunning && FinisherPreview.Active == target as CutFinisher)
        {
            FinisherPreview.Stop();
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var finisher = (CutFinisher)target;

        DrawStatus(finisher);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("enableFinisher"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("What this chop is", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("manager"));

        EditorGUILayout.Space();
        DrawShotAuthoring(finisher);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("How it plays", EditorStyles.boldLabel);
        SerializedProperty preset = serializedObject.FindProperty("preset");
        EditorGUILayout.PropertyField(preset);

        if (preset.objectReferenceValue == null)
        {
            DrawInlineTuning(finisher);
        }
        else
        {
            EditorGUILayout.HelpBox("Tuning comes from the preset. Edit the asset to change it, here or in the Project window.", MessageType.None);
            DrawEmbeddedPreset(preset);
        }

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        DrawPreview(finisher);
    }

    /// <summary>What this finisher still needs before it can run, and what the cut does meanwhile.</summary>
    private void DrawStatus(CutFinisher finisher)
    {
        if (!finisher.enableFinisher)
        {
            EditorGUILayout.HelpBox("Finisher off. The cut splices and quits the instant progress hits 1.", MessageType.None);
            return;
        }

        if (finisher.Manager == null)
        {
            EditorGUILayout.HelpBox("No CuttingManager up the hierarchy. Put this on the cut, or set Manager by hand.", MessageType.Warning);
            return;
        }

        if (finisher.Plane == null)
        {
            EditorGUILayout.HelpBox("The cut has no CutPlane, so there is no frame to swing in. It will splice directly instead of finishing.", MessageType.Warning);
            return;
        }

        if (!finisher.hasShot)
        {
            EditorGUILayout.HelpBox("No shot framed. The close-up watches from wherever the orbit left the camera.", MessageType.Info);
            return;
        }

        if (finisher.ToolPrefab == null)
        {
            EditorGUILayout.HelpBox("No tool prefab. The swing runs, but nothing visible does the chopping.", MessageType.Info);
            return;
        }

        EditorGUILayout.HelpBox("Wired and ready.", MessageType.Info);
    }

    /// <summary>Inline tuning, shown only while no preset is assigned, plus the button that turns it into one.</summary>
    private void DrawInlineTuning(CutFinisher finisher)
    {
        bool show = SessionState.GetBool(InlineKey, true);
        show = EditorGUILayout.Foldout(show, "Inline tuning (no preset assigned)", true);
        SessionState.SetBool(InlineKey, show);

        if (!show)
        {
            return;
        }

        EditorGUI.indentLevel++;
        for (int i = 0; i < InlineTuningFields.Length; i++)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(InlineTuningFields[i]));
        }

        if (GUILayout.Button("Save these as a preset asset"))
        {
            // applied first: the button can be clicked in the same frame a field was edited
            serializedObject.ApplyModifiedProperties();
            CreatePresetFrom(finisher);
        }
        EditorGUI.indentLevel--;
    }

    /// <summary>Draws the assigned preset's own fields inline, so tuning costs no trip to the Project window.</summary>
    private void DrawEmbeddedPreset(SerializedProperty presetProperty)
    {
        var asset = (CutFinisherPreset)presetProperty.objectReferenceValue;
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

    /// <summary>Composes the close-up: take it from the Scene view, or throw it away.</summary>
    private void DrawShotAuthoring(CutFinisher finisher)
    {
        EditorGUILayout.LabelField("Shot", EditorStyles.boldLabel);

        Transform space = finisher.ShotSpace;
        EditorGUILayout.LabelField("Framed relative to", space != null ? space.name : "(nothing)");

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(SceneView.lastActiveSceneView == null))
            {
                if (GUILayout.Button(new GUIContent(
                    finisher.hasShot ? "Re-grab from Scene view" : "Grab from Scene view",
                    "Stores the Scene view camera's current pose, in the body's local space. Compose with normal scene navigation, then click once.")))
                {
                    // applied first: the write below goes straight to the object, bypassing
                    // serializedObject, so a pending field edit would otherwise be lost
                    serializedObject.ApplyModifiedProperties();
                    GrabFromSceneView(finisher);
                    serializedObject.Update();
                }
            }

            using (new EditorGUI.DisabledScope(!finisher.hasShot))
            {
                if (GUILayout.Button(new GUIContent("Clear", "Forgets the framing. The close-up falls back to wherever the orbit left the camera.")))
                {
                    serializedObject.FindProperty("hasShot").boolValue = false;
                }
            }
        }

        if (finisher.hasShot)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("shotLocalPosition"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("shotLocalEuler"));
            EditorGUILayout.HelpBox("Local to the body, so the shot rides it wherever it is carried. Drag the Scene view handles to adjust.", MessageType.None);
        }
    }

    /// <summary>Draws move and rotate handles on the shot, so it needs no proxy GameObject to select.</summary>
    private void OnSceneGUI()
    {
        var finisher = (CutFinisher)target;
        if (!finisher.hasShot || !finisher.TryGetCameraPose(out Vector3 position, out Quaternion rotation, out _))
        {
            return;
        }

        EditorGUI.BeginChangeCheck();

        Vector3 movedTo = Handles.PositionHandle(position, rotation);
        Quaternion aimedTo = Handles.RotationHandle(rotation, movedTo);

        if (!EditorGUI.EndChangeCheck())
        {
            return;
        }

        Undo.RecordObject(finisher, "Move finisher shot");

        // back through the body's transform, the space it is stored in
        finisher.SetShotFromWorld(movedTo, aimedTo);
        EditorUtility.SetDirty(finisher);
    }

    /// <summary>Draws the edit-mode preview controls for the framing and the swing.</summary>
    private void DrawPreview(CutFinisher finisher)
    {
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Preview is edit-mode only. The game is driving the camera.", MessageType.None);
            return;
        }

        bool running = FinisherPreview.IsRunning && FinisherPreview.Active == finisher;

        if (!running)
        {
            // anything else previewing is writing the same camera transform
            if (EditorCameraClaim.IsClaimed)
            {
                EditorGUILayout.HelpBox($"'{EditorCameraClaim.HolderName()}' has the camera. Starting this stops it.", MessageType.None);
            }

            using (new EditorGUI.DisabledScope(finisher.Manager == null))
            {
                if (GUILayout.Button("Preview the finisher"))
                {
                    FinisherPreview.Start(finisher);
                }
            }
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(FinisherPreview.Playing ? "Pause" : "Play"))
            {
                FinisherPreview.SetPlaying(!FinisherPreview.Playing);
            }
            if (GUILayout.Button("Stop"))
            {
                FinisherPreview.Stop();
                return;
            }
        }

        FinisherPreview.TimeScale = EditorGUILayout.Slider(
            new GUIContent("Playback rate", "1 is real time. Drop it to study a swing too short to read at speed."),
            FinisherPreview.TimeScale, 0.05f, 2f);

        FinisherPreview.Loop = EditorGUILayout.Toggle(
            new GUIContent("Loop", "Replays from the top instead of stopping on the hold."),
            FinisherPreview.Loop);

        if (!FinisherPreview.WaitIsAuthored)
        {
            FinisherPreview.PreviewWaitSeconds = EditorGUILayout.FloatField(
                new GUIContent("Preview wait", "Seconds to sit on the wait here. Auto Slash After is 0, which waits for the click indefinitely."),
                FinisherPreview.PreviewWaitSeconds);
        }

        DrawTimeline();

        EditorGUILayout.HelpBox(
            "Runs on this finisher's own Ease In, wait, Slash Time and Hold After, in seconds. Nothing is sliced: the highlight is the real lower hull, drawn as an overlay. Camera and tool are put back on Stop.",
            MessageType.None);
    }

    /// <summary>Draws the scrub bar in real seconds, with the beat broken down beside it.</summary>
    private void DrawTimeline()
    {
        float total = FinisherPreview.TotalDuration;

        // scrubbing pauses, so a frame can be held still and judged
        float scrubbed = EditorGUILayout.Slider(
            new GUIContent("Time (s)", "Seconds into the beat. Scrubbing pauses playback."),
            FinisherPreview.Elapsed, 0f, Mathf.Max(total, 0.0001f));
        if (!Mathf.Approximately(scrubbed, FinisherPreview.Elapsed))
        {
            FinisherPreview.ScrubTo(scrubbed);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(
                $"{FinisherPreview.Phase}   {FinisherPreview.Elapsed:0.00}s / {total:0.00}s",
                EditorStyles.miniLabel);

            if (GUILayout.Button(new GUIContent("Go to impact", "Jumps to the frame the blade reaches the cut, where the splice fires."), EditorStyles.miniButton, GUILayout.Width(90f)))
            {
                FinisherPreview.ScrubToImpact();
            }
        }

        // the durations making up the total, so a beat that reads wrong points at the field
        // responsible
        string wait = FinisherPreview.WaitIsAuthored
            ? $"{FinisherPreview.WaitDuration:0.00}"
            : $"{FinisherPreview.WaitDuration:0.00}*";
        EditorGUILayout.LabelField(
            $"ease {FinisherPreview.EaseInDuration:0.00}  +  wait {wait}  +  slash {FinisherPreview.SlashDuration:0.00}  +  hold {FinisherPreview.HoldDuration:0.00}   (impact at {FinisherPreview.ImpactTime:0.00}s)",
            EditorStyles.miniLabel);

        if (FinisherPreview.SlashDuration <= 0f)
        {
            EditorGUILayout.HelpBox("Slash Time is 0. The swing is instant, one frame from start to follow-through.", MessageType.Warning);
        }
    }

    /// <summary>Writes this finisher's inline values into a new preset asset and assigns it, doing nothing when the save is cancelled.</summary>
    private static void CreatePresetFrom(CutFinisher finisher)
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Save finisher preset",
            $"{finisher.name} Finisher",
            "asset",
            "Where should this chop's tuning live?");
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        var preset = ScriptableObject.CreateInstance<CutFinisherPreset>();
        // the shot is left out: it lives in one body's local space, so carrying it to another cut
        // would point the camera at the wrong limb
        preset.cameraFOV = finisher.cameraFOV;
        preset.easeIn = finisher.easeIn;
        preset.easeInCurve = finisher.easeInCurve;
        preset.toolPrefab = finisher.toolPrefab;
        preset.toolEuler = finisher.toolEuler;
        preset.sweepAngle = finisher.sweepAngle;
        preset.approachTilt = finisher.approachTilt;
        preset.bobAmp = finisher.bobAmp;
        preset.bobHz = finisher.bobHz;
        preset.autoSlashAfter = finisher.autoSlashAfter;
        preset.hoverHeight = finisher.hoverHeight;
        preset.sweepDist = finisher.sweepDist;
        preset.slashTime = finisher.slashTime;
        preset.slashEase = finisher.slashEase;
        preset.holdAfter = finisher.holdAfter;
        preset.kick = finisher.kick;

        AssetDatabase.CreateAsset(preset, path);
        AssetDatabase.SaveAssets();

        Undo.RecordObject(finisher, "Assign finisher preset");
        finisher.preset = preset;
        EditorUtility.SetDirty(finisher);
    }

    /// <summary>Stores the Scene view camera's pose as this chop's shot, in the body's local space.</summary>
    private static void GrabFromSceneView(CutFinisher finisher)
    {
        SceneView view = SceneView.lastActiveSceneView;
        if (view == null || view.camera == null)
        {
            return;
        }

        Undo.RecordObject(finisher, "Grab finisher shot");
        finisher.SetShotFromWorld(view.camera.transform.position, view.camera.transform.rotation);
        EditorUtility.SetDirty(finisher);
    }
}
