using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Shifts a body's authored cuts so they land on the same anatomy after its mesh has been swapped for one whose vertices sit somewhere else.</summary>
/// <remarks>
/// A cut's placement is entirely its transform relative to the body (see <see cref="CutCopier"/>), and
/// nothing in it is derived from the mesh. Swapping the <c>MeshFilter</c> for a model exported with a
/// different origin therefore moves the geometry out from under every cut while the cuts stay exactly
/// where they were authored -- an arm cut left floating beside the arm.
/// <para>
/// The fix is one translation applied to every cut of that body, in the body's own space, which is the
/// same space the mesh moved in (the <c>MeshFilter</c> lives on the body's GameObject, so mesh-local
/// and body-local are the same axes). The offset is read off the two meshes' bounds centres, because
/// that is the one thing a re-export moves that can be measured without the modelling package -- or
/// typed in by hand when the two meshes do not have comparable bounds.
/// </para>
/// <para>Only translation. A mesh that also came back rotated or at a different scale needs the cuts
/// re-authored; the window says so rather than pretending an offset fixed it.</para>
/// </remarks>
public class CutMeshRetargetWindow : EditorWindow
{
    /// <summary>Body whose cuts are moved. Its current <c>MeshFilter</c> mesh is the NEW mesh.</summary>
    private CuttableObject body;

    /// <summary>The mesh the cuts were authored against, before the swap.</summary>
    private Mesh previousMesh;

    /// <summary>Type the offset instead of deriving it from the two meshes' bounds.</summary>
    private bool manualOffset;

    /// <summary>Offset in the body's local space, used when <see cref="manualOffset"/> is on.</summary>
    private Vector3 offset;

    [MenuItem("Tools/Cutting/Retarget Cuts To New Mesh...", false, 21)]
    private static void Open()
    {
        GetWindow<CutMeshRetargetWindow>(true, "Retarget Cuts").minSize = new Vector2(360f, 260f);
    }

    private void OnEnable()
    {
        PickBodyFromSelection();
    }

    private void OnSelectionChange()
    {
        // only fills an empty slot: a body chosen deliberately should not be replaced by whatever the
        // hierarchy click landed on while reading the numbers below.
        if (body == null)
        {
            PickBodyFromSelection();
            Repaint();
        }
    }

    private void PickBodyFromSelection()
    {
        GameObject picked = Selection.activeGameObject;
        if (picked != null) body = picked.GetComponentInParent<CuttableObject>();
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Swap the mesh on the body first, then run this. Every cut of that body is moved by the " +
            "difference between the old and the new mesh, so the cuts land on the same anatomy again.",
            MessageType.None);

        body = (CuttableObject)EditorGUILayout.ObjectField("Body", body, typeof(CuttableObject), true);

        Mesh currentMesh = CurrentMesh(body);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField("New mesh (on body)", currentMesh, typeof(Mesh), false);
        }

        previousMesh = (Mesh)EditorGUILayout.ObjectField("Previous mesh", previousMesh, typeof(Mesh), false);

        manualOffset = EditorGUILayout.Toggle("Type the offset", manualOffset);

        bool haveMeshes = currentMesh != null && previousMesh != null;
        Vector3 measured = haveMeshes ? currentMesh.bounds.center - previousMesh.bounds.center : Vector3.zero;

        if (manualOffset)
        {
            offset = EditorGUILayout.Vector3Field("Offset (body space)", offset);
        }
        else
        {
            offset = measured;
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Vector3Field("Offset (body space)", offset);
            }
        }

        List<CuttingManager> cuts = body != null ? CutCopier.CutsOn(body) : new List<CuttingManager>();
        EditorGUILayout.LabelField("Cuts on this body", cuts.Count.ToString());

        if (body == null)
        {
            EditorGUILayout.HelpBox("Pick the body that carries the mesh and the cuts.", MessageType.Info);
        }
        else if (currentMesh == null)
        {
            EditorGUILayout.HelpBox($"{body.name} has no MeshFilter mesh.", MessageType.Warning);
        }
        else if (!manualOffset && previousMesh == null)
        {
            EditorGUILayout.HelpBox(
                "Assign the mesh the cuts were authored against, or switch on \"Type the offset\" and " +
                "enter the shift yourself (drag one cut into place, read its position change, type it here).",
                MessageType.Info);
        }
        else if (cuts.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "No cut names this body. A cut only counts when its Game Object Being Cut points at it; " +
                "use Tools > Cutting > Copy Cuts to rebind ones that do not.",
                MessageType.Warning);
        }

        WarnIfMoreThanAnOffset(currentMesh, previousMesh);

        using (new EditorGUI.DisabledScope(cuts.Count == 0 || (!manualOffset && !haveMeshes)))
        {
            if (GUILayout.Button("Move cuts by this offset"))
            {
                int moved = Retarget(body, offset);
                Debug.Log($"Retarget Cuts: moved {moved} cut{(moved == 1 ? "" : "s")} on {body.name} by {offset:F4} (body space).", body);
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Check the green guide loops afterwards. The loop is re-extracted from the new mesh's own " +
            "triangles, so a cut that lands on a differently shaped part can come out with a different " +
            "loop, or none at all.",
            MessageType.None);
    }

    /// <summary>Says when the two meshes differ by more than a translation, which an offset cannot repair.</summary>
    /// <remarks>Compared on bounds size: a re-export at another scale or turned onto another axis changes
    /// the extents, and moving the cuts would then line them up at one end of the part and nowhere else.</remarks>
    private static void WarnIfMoreThanAnOffset(Mesh current, Mesh previous)
    {
        if (current == null || previous == null) return;

        Vector3 a = current.bounds.size;
        Vector3 b = previous.bounds.size;
        float tolerance = 0.02f * Mathf.Max(b.x, b.y, b.z);

        if (Mathf.Abs(a.x - b.x) > tolerance || Mathf.Abs(a.y - b.y) > tolerance || Mathf.Abs(a.z - b.z) > tolerance)
        {
            EditorGUILayout.HelpBox(
                $"The two meshes are not the same size ({b:F3} → {a:F3}). This tool only translates, so a " +
                "rescaled or reoriented mesh will still need its cuts placed by hand -- or set the new " +
                "model's import scale so the sizes match, then run this.",
                MessageType.Warning);
        }
    }

    /// <summary>The mesh currently on the body, from a <c>MeshFilter</c> or a <c>SkinnedMeshRenderer</c>.</summary>
    private static Mesh CurrentMesh(CuttableObject body)
    {
        if (body == null) return null;
        if (body.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null) return filter.sharedMesh;
        if (body.TryGetComponent(out SkinnedMeshRenderer skinned)) return skinned.sharedMesh;
        return null;
    }

    /// <summary>Moves every cut of a body by an offset expressed in the body's local space. Returns how many moved.</summary>
    /// <remarks>
    /// Applied as a world translation rather than by adding to <c>localPosition</c>, so a cut kept outside
    /// the body -- or under a scaled parent -- moves the same distance as one parented straight to it.
    /// <para>Anything a cut owns but keeps elsewhere (an external plane, finisher or scalpel) is moved
    /// too, and the parts already inside a moved cut are skipped: they travel with their root, and moving
    /// them again would shift them twice.</para>
    /// <para>The finisher's close-up is stored in the body's space, not in the world, so it does NOT
    /// travel with the transform -- its stored position is offset separately, or the framed shot keeps
    /// pointing at where the part used to be.</para>
    /// </remarks>
    public static int Retarget(CuttableObject body, Vector3 bodyLocalOffset)
    {
        if (body == null) return 0;

        List<CuttingManager> cuts = CutCopier.CutsOn(body);
        if (cuts.Count == 0) return 0;

        var roots = new List<Transform>();
        for (int i = 0; i < cuts.Count; i++)
        {
            if (cuts[i] != null) roots.Add(cuts[i].transform);
        }

        var toMove = new List<Transform>(roots);
        for (int i = 0; i < cuts.Count; i++)
        {
            CuttingManager cut = cuts[i];
            if (cut == null) continue;

            if (cut.loopGuide != null && cut.loopGuide.plane != null) AddIfOutside(toMove, roots, cut.loopGuide.plane.transform);
            if (cut.finisher != null) AddIfOutside(toMove, roots, cut.finisher.transform);
            if (cut.scalpelFollow != null) AddIfOutside(toMove, roots, cut.scalpelFollow.transform);
        }

        Vector3 worldOffset = body.transform.TransformVector(bodyLocalOffset);

        Undo.SetCurrentGroupName("Retarget Cuts To New Mesh");
        int group = Undo.GetCurrentGroup();

        for (int i = 0; i < toMove.Count; i++)
        {
            Transform t = toMove[i];
            Undo.RecordObject(t, "Retarget Cuts");
            t.position += worldOffset;
            EditorUtility.SetDirty(t);
        }

        // the framed shot lives in the body's space, so no transform carried it
        for (int i = 0; i < cuts.Count; i++)
        {
            CutFinisher finisher = cuts[i] != null ? cuts[i].finisher : null;
            if (finisher == null || finisher.ShotSpace != body.transform) continue;

            Undo.RecordObject(finisher, "Retarget Cuts");
            finisher.shotLocalPosition += bodyLocalOffset;
            EditorUtility.SetDirty(finisher);
        }

        Undo.CollapseUndoOperations(group);
        CutRegistry.Invalidate();

        return roots.Count;
    }

    /// <summary>Queues a transform for the move unless it already travels with one of the cut roots.</summary>
    private static void AddIfOutside(List<Transform> toMove, List<Transform> roots, Transform candidate)
    {
        if (candidate == null || toMove.Contains(candidate)) return;

        for (int i = 0; i < roots.Count; i++)
        {
            if (roots[i] != null && (candidate == roots[i] || candidate.IsChildOf(roots[i]))) return;
        }

        toMove.Add(candidate);
    }
}
