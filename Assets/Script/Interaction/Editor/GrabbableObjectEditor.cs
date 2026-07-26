using UnityEditor;
using UnityEngine;

/// <summary>Inspector for <see cref="GrabbableObject"/> that can park the object in the player's hand to tune its held pose.</summary>
/// <remarks>
/// The offsets are only readable as a picture: a centimetre of holdOffset means nothing until the object
/// is in the hand next to the camera. The preview puts it there in edit mode and follows every drag of
/// the offset fields, so the pose is authored where it will be seen.
/// <para>Invariant: the pose the preview shows is the pose the grab applies -- both call
/// <see cref="GrabbableObject.ApplyHeldPose"/>, so this cannot drift from the real thing.</para>
/// </remarks>
[CustomEditor(typeof(GrabbableObject), true)]
[CanEditMultipleObjects]
public class GrabbableObjectEditor : Editor
{
    /// <summary>Where the object was before the preview picked it up, so Stop can put it back exactly.</summary>
    private Transform parkedParent;
    private Vector3 parkedLocalPosition;
    private Quaternion parkedLocalRotation;
    private Vector3 parkedLocalScale;
    private bool previewing;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var grabbable = (GrabbableObject)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Held pose preview", EditorStyles.boldLabel);

        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Edit-mode only. In play, pick the object up.", MessageType.None);
            return;
        }

        Transform holdPoint = FindHoldPoint(out string problem);
        if (holdPoint == null)
        {
            EditorGUILayout.HelpBox(problem, MessageType.Info);
            return;
        }

        if (!previewing)
        {
            if (GUILayout.Button(new GUIContent(
                    "Preview held pose",
                    "Moves this object into the player's hand so the offsets can be tuned against what the player will see. Stop puts it back.")))
            {
                StartPreview(grabbable, holdPoint);
            }
            return;
        }

        // re-applied every inspector repaint, so dragging an offset field moves the object live
        grabbable.ApplyHeldPose(holdPoint);
        SceneView.RepaintAll();

        EditorGUILayout.HelpBox($"Held by {holdPoint.name}. Drag the offsets above to line it up.", MessageType.None);

        if (GUILayout.Button("Stop preview"))
        {
            StopPreview(grabbable);
        }
    }

    /// <summary>Snapshots where the object lives, then hands it to the hand.</summary>
    private void StartPreview(GrabbableObject grabbable, Transform holdPoint)
    {
        Undo.RegisterCompleteObjectUndo(grabbable.transform, "Preview held pose");

        parkedParent = grabbable.transform.parent;
        parkedLocalPosition = grabbable.transform.localPosition;
        parkedLocalRotation = grabbable.transform.localRotation;
        parkedLocalScale = grabbable.transform.localScale;
        previewing = true;

        grabbable.ApplyHeldPose(holdPoint);
    }

    /// <summary>Puts the object back exactly where the preview found it.</summary>
    private void StopPreview(GrabbableObject grabbable)
    {
        if (!previewing) return;

        Undo.RegisterCompleteObjectUndo(grabbable.transform, "Stop held pose preview");

        grabbable.transform.SetParent(parkedParent, false);
        grabbable.transform.localPosition = parkedLocalPosition;
        grabbable.transform.localRotation = parkedLocalRotation;
        // the hand's scale is in the chain while previewing, so ApplyHeldPose rewrote localScale;
        // the parked value is what the object had in the scene and is what must come back.
        grabbable.transform.localScale = parkedLocalScale;

        previewing = false;
        parkedParent = null;
    }

    /// <summary>The scene's hand, or null with the reason why there isn't one.</summary>
    private static Transform FindHoldPoint(out string problem)
    {
        Interactor interactor = Object.FindFirstObjectByType<Interactor>(FindObjectsInactive.Include);

        if (interactor == null)
        {
            problem = "No Interactor in the scene, so there is no hand to preview against.";
            return null;
        }

        if (interactor.holdPoint == null)
        {
            problem = $"{interactor.name} has no holdPoint assigned, so there is no hand to preview against.";
            return null;
        }

        problem = null;
        return interactor.holdPoint;
    }

    /// <summary>Leaving the inspector must not strand the object in the hand.</summary>
    /// <remarks>Selecting something else destroys this editor, and a preview left running would save the
    /// object parented to the player -- a change nobody asked for and nobody would notice until play.</remarks>
    private void OnDisable()
    {
        if (!previewing) return;

        if (target is GrabbableObject grabbable && grabbable != null)
        {
            StopPreview(grabbable);
        }
    }
}
