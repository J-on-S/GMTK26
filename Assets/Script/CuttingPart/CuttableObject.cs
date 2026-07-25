using UnityEngine;
using System.Collections.Generic;
using EzySlice;

[RequireComponent(typeof(MeshCollider))]
[RequireComponent(typeof(MeshRenderer))]
public class CuttableObject : MonoBehaviour
{


        [Tooltip("Transform whose position + up axis define the cutting plane.")]
        public Transform planeTransform;

        [Tooltip("Weld distance for merging cut points (mesh-local units).")]
        public float weld = 1e-4f;


        [Tooltip("Rectangle size on the plane, in planeTransform local units (X = right, Y = forward). The rectangle rotates with the plane; contours it clips are discarded from the cut, so keep it large enough to cover every loop you want cut (use a huge value for an effectively infinite window).")]
        public Vector2 boundsSize = Vector2.one;

        [Tooltip("Material for the exposed cut face. Must differ from the skin materials so the cap lands in its own submesh and can be culled outside the bounds window.")]
        public Material crossSectionMaterial;

        [Tooltip("Outward offset of the orange preview loop from its centre.")]
        public float cameraScale = 0.05f;

        [Tooltip("Auto-recompute every editor frame. Turn off once the loops are baked.")]
        public bool liveUpdate = true;

        [Tooltip("Extracted loops in MESH-LOCAL space. Convert with transform.TransformPoint at runtime.")]
        public List<SavedLoop> savedLoops = new List<SavedLoop>();

    
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    /// <summary>Result of the last <see cref="Splice"/>, held for <see cref="Weld"/> to consume.</summary>
    private SlicedHull pendingHull;

    /// <summary>Slices and welds in one step: this object becomes the reattached body and the removed chunk is spawned as its own GameObject. Editor calls are a single undoable step.</summary>
    [ContextMenu("Slice Windowed")]
    public List<GameObject> SpliceWindowed()
    {
#if UNITY_EDITOR
        UnityEditor.Undo.IncrementCurrentGroup();
        UnityEditor.Undo.SetCurrentGroupName("Slice Windowed");
        int undoGroup = UnityEditor.Undo.GetCurrentGroup();
#endif

        Splice();
        List<GameObject> created = Weld();


#if UNITY_EDITOR
        UnityEditor.Undo.CollapseUndoOperations(undoGroup);
#endif
    return created;
    }

    /// <summary>Slices the mesh with the plane and holds the result, ready for <see cref="Weld"/>. Errors and does nothing when the cut isn't complete.</summary>
    private void Splice()
    {
        pendingHull = null;

        if (planeTransform == null) {
            Debug.LogError("CuttableObject: no planeTransform assigned.", this);
            return;
        }
        if (!TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null) {
            Debug.LogError("CuttableObject: no MeshFilter with a shared mesh.", this);
            return;
        }

        if (!CutIsComplete(filter.sharedMesh, out string error)) {
            Debug.LogError($"CuttableObject: cut incomplete, not slicing — {error}", this);
            return;
        }

        pendingHull = gameObject.Slice(planeTransform.position, planeTransform.up, crossSectionMaterial);
        if (pendingHull == null) {
            Debug.LogError("CuttableObject: slice produced no geometry.", this);
        }
    }

    /// <summary>Splits the pending slice: this object becomes the body and the removed chunk is spawned. Records the mutated components and the created object for editor undo.</summary>
    private  List<GameObject> Weld()
    {
        if (pendingHull == null) {
            Debug.LogError("CuttableObject: no pending slice — call Splice first.", this);
            return null;
        }
        if (!TryGetComponent<MeshFilter>(out var filter)) {
            Debug.LogError("CuttableObject: no MeshFilter.", this);
            return null;
        }

        var pieces = new List<Mesh>();
        pendingHull.SliceWindowedSplit(gameObject, MeshLocalPlane(), BuildBounds(), weld, out Mesh body, pieces);
        if (body == null) {
            Debug.LogError("CuttableObject: weld produced no body geometry.", this);
            return null;
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
            gameObjects.Add(SpawnPiece(pieceName, pieces[i], skinMats));
        }

        pendingHull = null;
        return gameObjects;
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

    /// <summary>The cutting plane in mesh-local space, built the same way the slicer builds it (inverse-transpose normal), so contour extraction and the slice agree exactly.</summary>
    private EzySlice.Plane MeshLocalPlane()
    {
        Matrix4x4 inv = transform.worldToLocalMatrix.transpose.inverse;
        return new EzySlice.Plane(
            transform.InverseTransformPoint(planeTransform.position),
            inv.MultiplyVector(planeTransform.up).normalized);
    }

    /// <summary>Checks at least one cut contour inside the bounds window is a closed loop, i.e. some cut fully crosses the mesh. Multiple closed loops are allowed — each becomes its own removed piece by connectivity. Open (clipped) loops are allowed too; the splice discards them and welds their cut shut.</summary>
    /// <returns><c>false</c> with a reason when the plane misses or every contour is left open (clipped) by the bounds.</returns>
    private bool CutIsComplete(Mesh mesh, out string error)
    {
        List<CutContour.Loop> loops = CutContour.ExtractLoops(mesh, MeshLocalPlane(), weld, BuildBounds());

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

  



        /// <summary>Builds the finite-window bounds from the plane and this object.</summary>
        /// <returns><c>null</c> when no plane is assigned.</returns>
        private CutContour.PlaneBounds? BuildBounds() {
            if (planeTransform == null) return null;

            return CutContour.BuildBounds(planeTransform, boundsSize, gameObject);
        }

        /// <summary>Recomputes and stores every cut loop from the current plane, using the exact same plane and bounds window as the actual slice — what the preview shows is what <see cref="SpliceWindowed"/> cuts.</summary>
        /// <returns>The number of loops in <c>savedLoops</c>.</returns>
        public int Recompute() {
            savedLoops.Clear();

            if (planeTransform == null || !TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null) {
                return 0;
            }

            ToSavedLoops(CutContour.ExtractLoops(filter.sharedMesh, MeshLocalPlane(), weld, BuildBounds()), savedLoops);
            return savedLoops.Count;
        }

        /// <summary>Cuts the mesh with a world-space plane and appends the loops to <paramref name="dst"/>.</summary>
        /// <param name="worldPos">Point on the cutting plane, in world space.</param>
        /// <param name="worldNormal">EzySlice.Plane normal, in world space.</param>
        private void ExtractAt(Mesh mesh, Vector3 worldPos, Vector3 worldNormal, List<SavedLoop> dst) {
            EzySlice.Plane plane = new EzySlice.Plane(
                transform.InverseTransformPoint(worldPos),
                transform.InverseTransformDirection(worldNormal).normalized);

            List<CutContour.Loop> loops = CutContour.ExtractLoops(mesh, plane, weld, BuildBounds());
            ToSavedLoops(loops, dst);
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
        private void OnDrawGizmos() {
            if (liveUpdate) {
                Recompute();
            }

            foreach (var loop in savedLoops) {
                SavedLoop preview = new SavedLoop {
                    closed = loop.closed,
                    points = CutContour.ScaleLoop(loop.points, cameraScale),
                };
                GizmoUtils.DrawLoop(transform, preview, Color.orange, false);
            }

            DrawLoops(transform, savedLoops, Color.green, true);


            if (planeTransform != null) {
            GizmoUtils.DrawBoundsGizmo(planeTransform , boundsSize);
            }

        }

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

