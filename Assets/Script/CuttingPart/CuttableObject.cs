using UnityEngine;
using System.Collections.Generic;
using EzySlice;
using Unity.VisualScripting;

[RequireComponent(typeof(MeshCollider))]
[RequireComponent(typeof(MeshRenderer))]
public class CuttableObject : MonoBehaviour , IInteractable
{


        [Tooltip("Weld distance for merging cut points (mesh-local units). A property of this mesh, so it is shared by every cut on it.")]
        public float weld = 1e-4f;

        [Tooltip("Material for the exposed cut face. Must differ from the skin materials so the cap lands in its own submesh and can be culled outside the bounds window.")]
        public Material crossSectionMaterial;

        [Tooltip("Master switch for every CutPlane's scene-view loop on this body. Off stops the per-frame re-extraction, which is the expensive part of authoring; turn it off once the planes are placed.")]
        public bool drawCutLoops = true;

    // Where the plane went: a body can be cut in several places, so the plane and its window are a
    // CutPlane component on its own object, not a single slot here. This object holds only what is
    // true of the MESH -- weld distance and the cross-section material -- and takes the plane as an
    // argument, so it never carries "which cut is happening" state that could be stale or raced.

    /// <summary>Result of the last <see cref="Splice"/>, held for <see cref="Weld"/> to consume.</summary>
    private SlicedHull pendingHull;

    /// <summary>Slices and welds in one step: this object becomes the reattached body and each removed chunk is spawned as its own GameObject. Editor calls are a single undoable step.</summary>
    /// <param name="plane">Cut to make. Its transform gives the plane and it carries its own window.</param>
    /// <returns>The spawned lower hulls, in spawn order. Empty when the cut failed or produced nothing; never null.</returns>
    public List<GameObject> SpliceWindowed(CutPlane plane)
    {
        if (plane == null) {
            Debug.LogError("CuttableObject: no cut plane given.", this);
            return new List<GameObject>();
        }

#if UNITY_EDITOR
        UnityEditor.Undo.IncrementCurrentGroup();
        UnityEditor.Undo.SetCurrentGroupName("Slice Windowed");
        int undoGroup = UnityEditor.Undo.GetCurrentGroup();
#endif

        Splice(plane);
        List<GameObject> lowerHulls = Weld(plane);

#if UNITY_EDITOR
        UnityEditor.Undo.CollapseUndoOperations(undoGroup);
#endif

        return lowerHulls;
    }

    /// <summary>Slices the mesh with the plane and holds the result, ready for <see cref="Weld"/>. Errors and does nothing when the cut isn't complete.</summary>
    private void Splice(CutPlane plane)
    {
        pendingHull = null;

        if (!TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null) {
            Debug.LogError("CuttableObject: no MeshFilter with a shared mesh.", this);
            return;
        }

        if (!CutIsComplete(filter.sharedMesh, plane, out string error)) {
            Debug.LogError($"CuttableObject: cut incomplete, not slicing — {error}", this);
            return;
        }

        pendingHull = gameObject.Slice(plane.Origin, plane.Normal, crossSectionMaterial);
        if (pendingHull == null) {
            Debug.LogError("CuttableObject: slice produced no geometry.", this);
        }
    }

    /// <summary>Splits the pending slice: this object becomes the body and each removed chunk is spawned. Records the mutated components and the created objects for editor undo.</summary>
    /// <returns>The spawned lower hulls, in spawn order. Empty on any failure; never null.</returns>
    private List<GameObject> Weld(CutPlane plane)
    {
        var spawned = new List<GameObject>();

        if (pendingHull == null) {
            Debug.LogError("CuttableObject: no pending slice — call Splice first.", this);
            return spawned;
        }
        if (!TryGetComponent<MeshFilter>(out var filter)) {
            Debug.LogError("CuttableObject: no MeshFilter.", this);
            return spawned;
        }

        var pieces = new List<Mesh>();
        pendingHull.SliceWindowedSplit(gameObject, MeshLocalPlane(plane.transform), BuildBounds(plane), weld, out Mesh body, pieces);
        if (body == null) {
            Debug.LogError("CuttableObject: weld produced no body geometry.", this);
            return spawned;
        }

        MeshRenderer mr = GetComponent<MeshRenderer>();
        TryGetComponent<MeshCollider>(out var col);
        Material[] skinMats = mr.sharedMaterials;

#if UNITY_EDITOR
        if (!Application.isPlaying) {
            UnityEditor.Undo.RecordObject(filter, "Slice Windowed");
            UnityEditor.Undo.RecordObject(mr, "Slice Windowed");
            if (col != null) {
                UnityEditor.Undo.RecordObject(col, "Slice Windowed");
            }
        }
#endif

        // this object becomes the reattached body
        AssignMesh(filter, col, body);
        ApplyMaterials(mr, body, skinMats);

        // each removed chunk becomes its own GameObject next to this one
        List<GameObject> gameObjects = new List<GameObject>();
        for (int i = 0; i < pieces.Count; i++) {
            string pieceName = pieces.Count == 1 ? "Lower_Hull" : $"Lower_Hull_{i}";
            spawned.Add(SpawnPiece(pieceName, pieces[i], skinMats));
        }

        pendingHull = null;
        return spawned;
    }

    /// <summary>Assigns a mesh to the filter (and collider), using sharedMesh in the editor so the change is undoable and no mesh instance leaks.</summary>
    private void AssignMesh(MeshFilter filter, MeshCollider col, Mesh mesh)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) {
            filter.sharedMesh = mesh;
            if (col != null) {
                col.sharedMesh = mesh;
            }
            return;
        }
#endif
        filter.mesh = mesh;
        if (col != null) {
            col.sharedMesh = mesh;
        }
    }

    /// <summary>Creates a sibling GameObject carrying the cut piece: same transform, mesh, materials and a mesh collider. Registered for editor undo.</summary>
    /// <returns>The spawned piece, so the caller can hand it on to whoever wants the severed part.</returns>
    private GameObject SpawnPiece(string pieceName, Mesh mesh, Material[] skinMats)
    {
        GameObject go = new GameObject(pieceName);
#if UNITY_EDITOR
        if (!Application.isPlaying) {
            UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Slice Windowed");
        }
#endif
        go.transform.SetParent(transform.parent, false);
        go.transform.SetLocalPositionAndRotation(transform.localPosition, transform.localRotation);
        go.transform.localScale = transform.localScale;

        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        ApplyMaterials(go.AddComponent<MeshRenderer>(), mesh, skinMats);
        go.AddComponent<MeshCollider>().sharedMesh = mesh;

        return go;
    }

    /// <summary>Assigns the skin materials in order, appending the cross-section material last when the mesh carries a cap submesh.</summary>
    private void ApplyMaterials(MeshRenderer renderer, Mesh mesh, Material[] skinMats)
    {
        var mats = new List<Material>(skinMats);
        if (mesh.subMeshCount > mats.Count) {
            mats.Add(crossSectionMaterial);
        }
        renderer.sharedMaterials = mats.ToArray();
    }

    /// <summary>A cutting plane in mesh-local space, built the same way the slicer builds it (inverse-transpose normal), so contour extraction and the slice agree exactly.</summary>
    private EzySlice.Plane MeshLocalPlane(Transform plane)
    {
        Matrix4x4 inv = transform.worldToLocalMatrix.transpose.inverse;
        return new EzySlice.Plane(
            transform.InverseTransformPoint(plane.position),
            inv.MultiplyVector(plane.up).normalized);
    }

    /// <summary>Runs the real slice against an arbitrary plane and hands back the pieces it would sever, without touching this object.</summary>
    /// <remarks>
    /// The same call the actual cut makes, minus the assignment: the window and the connectivity
    /// rules are applied identically, so what this returns is exactly what <see cref="SpliceWindowed"/>
    /// would spawn. That matters for previewing -- a half-space test can't express either the
    /// finite window or "connected to a closed in-window loop", so it disagrees with the real cut
    /// wherever the plane passes through more than one limb.
    /// <para>Meshes are freshly allocated; the caller owns them and must Destroy them.</para>
    /// </remarks>
    /// <param name="plane">Cut to preview. Supplies both the plane and its window, so this cannot disagree with the real slice.</param>
    public List<Mesh> PreviewLowerHulls(CutPlane plane)
    {
        var pieces = new List<Mesh>();

        if (plane == null
            || !TryGetComponent<MeshFilter>(out var filter)
            || filter.sharedMesh == null) {
            return pieces;
        }

        SlicedHull hull = gameObject.Slice(plane.Origin, plane.Normal, crossSectionMaterial);
        if (hull == null) {
            return pieces;
        }

        hull.SliceWindowedSplit(
            gameObject,
            MeshLocalPlane(plane.transform),
            BuildBounds(plane),
            weld,
            out Mesh previewBody,
            pieces);

        // everything except the pieces is scratch. None of it is an asset, so nothing else will
        // collect it, and a preview that re-slices whenever the plane moves would leak a body and
        // two hulls per rebuild.
        DestroyMesh(previewBody);
        DestroyMesh(hull.upperHull);
        DestroyMesh(hull.lowerHull);

        return pieces;
    }

    /// <summary>Destroys a runtime-generated mesh, using the call that works in the current mode.</summary>
    private static void DestroyMesh(Mesh mesh)
    {
        if (mesh == null) return;
        if (Application.isPlaying) Destroy(mesh);
        else DestroyImmediate(mesh);
    }
    public void Interact(Interactor player)
    {
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if(player.heldObject == null) return;

        if (!Physics.Raycast(ray, out RaycastHit hit)) Debug.LogError("Should not happen: raycast on interact hit. Check layers");
        Debug.Log("successfully interacted");
        

        CuttingManager aimed = CutRegistry.CutAt(this, hit.point);
        if(aimed == null) return;

        bool hasTool = aimed.HasRequiredTool(player.heldObject.itemName);
        if(aimed.canEnterMinigame() && hasTool){
            aimed.EnterMinigame();
        }
        

    }

    /// <summary>Checks at least one cut contour inside the bounds window is a closed loop, i.e. some cut fully crosses the mesh. Multiple closed loops are allowed — each becomes its own removed piece by connectivity. Open (clipped) loops are allowed too; the splice discards them and welds their cut shut.</summary>
    /// <returns><c>false</c> with a reason when the plane misses or every contour is left open (clipped) by the bounds.</returns>
    private bool CutIsComplete(Mesh mesh, CutPlane plane, out string error)
    {
        List<CutContour.Loop> loops = CutContour.ExtractLoops(mesh, MeshLocalPlane(plane.transform), weld, BuildBounds(plane));

        if (loops.Count == 0) { error = "plane misses the mesh or nothing lies inside the bounds"; return false; }

        bool anyClosed = false;
        for (int i = 0; i < loops.Count; i++) {
            anyClosed |= loops[i].closed;
        }
        if (!anyClosed) { error = "every contour is open — clipped by the bounds, no cut fully crosses"; return false; }

        error = null;
        return true;
    }

    /// <summary>One serialized contour.</summary>
        [System.Serializable]
        public class SavedLoop {
            /// <summary>Ordered contour points, in mesh-local space.</summary>
            public List<Vector3> points = new List<Vector3>();

            /// <summary><c>false</c> for an open chain left by clipping; do not trace an edge from the last point back to the first.</summary>
            public bool closed = true;
        }

  



        /// <summary>Builds the finite-window bounds from a cut plane and this object.</summary>
        /// <returns><c>null</c> when no plane is given.</returns>
        private CutContour.PlaneBounds? BuildBounds(CutPlane plane) {
            if (plane == null) return null;

            return CutContour.BuildBounds(plane.transform, plane.boundsSize, gameObject);
        }

        /// <summary>Extracts every cut loop of an object against the finite quad the plane transform defines.</summary>
        /// <param name="meshObj">Object being cut; supplies the mesh and the mesh-local frame of the result.</param>
        /// <param name="plane">Cutting plane; its position + up give the cut and its scale gives the finite window.</param>
        /// <param name="windowSize">Window rectangle in the plane's local units (the plane's own scale multiplies it); defaults to a unit rectangle. Pass the cuttable's <c>boundsSize</c> to match the slice window.</param>
        /// <returns>Mesh-local loops of <paramref name="meshObj"/>; empty when it has no <c>MeshFilter</c> with a shared mesh.</returns>
        public static List<SavedLoop> GetLoops(GameObject meshObj, Transform plane, float weld = 1e-4f, Vector2? windowSize = null) {
            var result = new List<SavedLoop>();

            if (meshObj == null || plane == null ||
                !meshObj.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null) {
                return result;
            }

            Transform mt = meshObj.transform;

            // inverse-transpose normal transform, the same the slicer uses, so these loops
            // sit exactly where a slice with this plane would cut
            Matrix4x4 inv = mt.worldToLocalMatrix.transpose.inverse;
            EzySlice.Plane pl = new EzySlice.Plane(
                mt.InverseTransformPoint(plane.position),
                inv.MultiplyVector(plane.up).normalized);

            CutContour.PlaneBounds? bounds = CutContour.BuildBounds(plane, windowSize ?? Vector2.one, meshObj);

            ToSavedLoops(CutContour.ExtractLoops(filter.sharedMesh, pl, weld, bounds), result);
            return result;
        }

        /// <summary>Copies framework loops into serialized <c>SavedLoop</c>s, appending to <paramref name="dst"/>.</summary>
        private static void ToSavedLoops(List<CutContour.Loop> loops, List<SavedLoop> dst) {
            for (int i = 0; i < loops.Count; i++) {
                dst.Add(new SavedLoop {
                    points = new List<Vector3>(loops[i].points),
                    closed = loops[i].closed,
                });
            }
        }

#if UNITY_EDITOR
        /// <summary>Draws every loop in a set.</summary>
        /// <param name="withDots">Whether to mark each vertex with a sphere.</param>
        public static void DrawLoops(Transform tf, List<SavedLoop> set, Color color, bool withDots) {
            Gizmos.color = color;

            for (int l = 0; l < set.Count; l++) {
                GizmoUtils.DrawLoop(tf, set[l], color, withDots);
            }
        }

    
    

    /// <summary>Draws the finite cut window as a wire rectangle on the plane.</summary>

#endif
}

