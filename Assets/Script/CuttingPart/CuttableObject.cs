using UnityEngine;
using System.Collections.Generic;
using EzySlice;
using Unity.VisualScripting;

[RequireComponent(typeof(MeshCollider))]
[RequireComponent(typeof(MeshRenderer))]
public class CuttableObject : MonoBehaviour , IInteractable, IHoverable
{


        [Tooltip("Weld distance for merging cut points (mesh-local units). A property of this mesh, so it is shared by every cut on it.")]
        public float weld = 1e-4f;

        [Tooltip("Material for the exposed cut face. May be the same as a skin material: the slice is made with a null cross material so the cap always lands in its own submesh, and this is applied to it afterwards.")]
        public Material crossSectionMaterial;

        [Tooltip("Master switch for every CutPlane's scene-view loop on this body. Off stops the per-frame re-extraction, which is the expensive part of authoring; turn it off once the planes are placed.")]
        public bool drawCutLoops = true;

        // ---- cut defaults: SEEDS the setup menu copies into each new cut on this body, not a runtime
        //      fallback. A cut still shows exactly what it uses; the body only decides what a fresh cut
        //      starts with. Only things that are the same WHEREVER you cut this body go here -- the
        //      camera move and framing differ per part, so they stay on the CuttingManager, never here.
        [Header("Cut defaults (seeded into new cuts on this body)")]

        [Tooltip("Cutting sounds a new cut on this body starts with. Same wherever you cut this body, so it sits here rather than being re-picked per cut. Left empty, the menu falls back to the project's shared preset.")]
        public CutSoundPreset defaultSoundPreset;

        [Tooltip("Grab/drop sounds a new cut's severed piece starts with. Per-body, since a body's parts sound alike in the hand. Left empty, the menu falls back to the project's shared preset.")]
        public AudioGrappablePreset defaultSeveredPieceAudio;

        [Tooltip("Freshness (seconds) a new cut's severed piece starts and caps at. Per-body -- one client's parts spoil no faster than another's part of the same client. 0 leaves the SeveredPiece component's own default.")]
        public float defaultSeveredPieceHealth = 0f;

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

        // sliced with a null cross material, never crossSectionMaterial: the slicer uses the
        // material only to pick the cap's submesh, and a material it finds among the renderer's
        // own merges the cap INTO that skin submesh. The windowed split tells cap from skin by
        // submesh index alone, so a merged cap reads as skin, bridges every lower chunk through
        // the slicer's plane-wide convex cap, and the whole lower half comes off as if the plane
        // were infinite. Null forces the cap into its own trailing submesh; the real material is
        // applied afterwards by ApplyMaterials, so the cap still renders with crossSectionMaterial
        // even when that equals the skin's.
        pendingHull = gameObject.Slice(plane.Origin, plane.Normal, null);
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

        CenterPivot(go, mesh);

        return go;
    }

    /// <summary>Moves a piece's origin to the middle of its own mesh, without moving the piece.</summary>
    /// <remarks>
    /// A sliced piece keeps the whole body's local space, so its vertices sit wherever that part of the
    /// body was and the object's origin stays at the body's. The piece then looks detached from its own
    /// handle: the gizmo is off in the chest while the mesh is an arm, rotation swings it around a point
    /// it does not contain, and physics is given a centre of mass nowhere near the geometry.
    /// <para>Invariant: nothing moves on screen. The vertices go back by the same offset the transform
    /// goes forward, so the world pose of every vertex is unchanged.</para>
    /// </remarks>
    public static void CenterPivot(GameObject piece, Mesh mesh)
    {
        if (piece == null || mesh == null) {
            return;
        }

        Vector3 center = mesh.bounds.center;
        if (center == Vector3.zero) {
            return;
        }

        Vector3[] vertices = mesh.vertices;
        for (int i = 0; i < vertices.Length; i++) {
            vertices[i] -= center;
        }
        mesh.vertices = vertices;
        mesh.RecalculateBounds();

        // TransformVector, not TransformPoint: this is a displacement, and it has to carry the piece's
        // own rotation and scale so the mesh lands back exactly where it was.
        piece.transform.position += piece.transform.TransformVector(center);

        // a MeshCollider caches the mesh it cooked; re-assigning is what makes it read the new vertices
        if (piece.TryGetComponent(out MeshCollider collider)) {
            Recook(collider, mesh);
        }
    }

    /// <summary>Makes a MeshCollider throw away its cooked shape and build it again from the mesh as it is now.</summary>
    /// <remarks>
    /// The cook is cached against the collider, not the mesh, so editing the vertices under it leaves the
    /// old shape in place -- the collider sits where the mesh used to be, and only ticking Convex off and
    /// on in the inspector puts it right. This is that toggle, done in code.
    /// <para>Both steps are needed: re-assigning the mesh re-reads the vertices, and re-setting Convex is
    /// what rebuilds the hull, which is the shape a dynamic piece actually collides with.</para>
    /// </remarks>
    public static void Recook(MeshCollider collider, Mesh mesh)
    {
        if (collider == null) {
            return;
        }

        collider.sharedMesh = null;
        collider.sharedMesh = mesh;

        if (collider.convex) {
            collider.convex = false;
            collider.convex = true;
        }
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

        // null cross material for the same reason Splice passes it: the cap must land in its own
        // trailing submesh for the windowed split to recognise it, whatever crossSectionMaterial is.
        SlicedHull hull = gameObject.Slice(plane.Origin, plane.Normal, null);
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
    // ---- Aim highlight (was the standalone MoveCamera; now driven by Interactor's hover ray) ----
    [Header("Aim highlight")]
    [Tooltip("How many times a second the aim is re-resolved while the player keeps looking at this body. Resolving runs the real slicer over the whole body mesh, so this is the biggest cost here.")]
    public float resolvesPerSecond = 12f;

    [Tooltip("The cut can be started right now.")]
    public Color canCutColor = new(0f, 1f, 0f, 0.35f);

    [Tooltip("The cut is otherwise fine, but the player is holding the wrong tool for it.")]
    public Color wrongToolColor = new(1f, 0.92f, 0f, 0.35f);

    /// <summary>Highlighter currently lit on this body, so it can be cleared when the aim moves off it.</summary>
    private CutRegionHighlighter litHighlighter;

    /// <summary>What is on screen now, so an unchanged tint is not rewritten every frame.</summary>
    private Mesh litMesh;
    private Color litColor;

    /// <summary>Time the next throttled resolve is due.</summary>
    private float nextResolve;

    /// <summary>Set the frame the interactor's aim is on this body, cleared in LateUpdate: a frame with no HoverOver call means the aim left.</summary>
    private bool hovering;

    /// <summary>Lights the piece under the crosshair while the player aims at this body, in the colour that says whether its cut can be started.</summary>
    /// <remarks>
    /// Aiming resolves in three steps, because a cut does not live on the object it cuts. The interactor's
    /// ray found this body, <see cref="CutRegistry"/> maps it back to its cuts, and the hit point picks
    /// which of those would take the piece being pointed at. Regions nest -- the hand sits inside both the
    /// wrist cut's piece and the shoulder cut's -- so the registry hands back the innermost.
    /// <para>Called every frame the aim is on this body; resolving runs the real slicer over the whole
    /// mesh, so it is throttled to <see cref="resolvesPerSecond"/>.</para>
    /// </remarks>
    public void HoverOver(Interactor player)
    {
        hovering = true;

        // no highlight while a cut is on screen: that camera is flown onto a body, and a tint under the
        // crosshair there would fight the cut. Replaces the old disable of the scene-wide MoveCamera.
        if (CuttingManager.currentGame != null)
        {
            ClearHover();
            return;
        }

        // still lit from the last resolve -- resolving is the expensive part, so leave the tint be
        if (Time.time < nextResolve) return;
        nextResolve = Time.time + (resolvesPerSecond > 0f ? 1f / resolvesPerSecond : 0f);

        Camera cam = Camera.main;
        if (cam == null)
        {
            ClearHover();
            return;
        }

        // screen centre: the cursor is locked, so a mouse position carries no information. The interactor
        // already knows the aim is on this body; this ray only recovers the hit point it did not hand over.
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!Physics.Raycast(ray, out RaycastHit hit) || hit.collider.gameObject != gameObject)
        {
            ClearHover();
            return;
        }

        // which cut would take the piece under the crosshair. Null means the upper hull -- the part that
        // stays attached -- and that is never tinted.
        CuttingManager aimed = CutRegistry.CutAt(this, hit.point);
        if (aimed == null)
        {
            ClearHover();
            return;
        }

        Item heldItem = player != null && player.heldObject != null ? player.heldObject.item : null;
        bool hasTool = aimed.HasRequiredTool(heldItem);

        Color color = hasTool ? canCutColor : wrongToolColor;

        // the actual severed mesh, so the tint is the piece that would come away and nothing more
        Highlight(aimed.SeveredPreviewMesh, color);

        // and name the tool this cut wants on the scene HUD, e.g. "required: Scalpel"
        BodyPartDescriptionHUD hud = BodyPartDescriptionHUD.LastActiveInstance;
        if (hud != null) hud.ShowText(this, $"required: {aimed.requiredTool}");
    }

    /// <summary>Clears both this body's tint and its requirement line on the HUD, for the frames the aim is on the body but not on a cut.</summary>
    private void ClearHover()
    {
        Highlight(null, default);
        BodyPartDescriptionHUD hud = BodyPartDescriptionHUD.LastActiveInstance;
        if (hud != null) hud.HideText(this);
    }

    /// <summary>Lights this body's severed piece, clearing whichever tint was on before. A <c>null</c> mesh clears.</summary>
    private void Highlight(Mesh severedMesh, Color color)
    {
        CutRegionHighlighter target = severedMesh != null ? CutRegionHighlighter.For(this) : null;

        // already showing exactly this: rewriting it would churn a property block for no change
        if (target == litHighlighter && severedMesh == litMesh && color == litColor) return;

        // clear the old one first, so sweeping between two bodies never leaves both lit
        if (litHighlighter != null && litHighlighter != target) litHighlighter.Hide();

        litHighlighter = target;
        litMesh = severedMesh;
        litColor = color;

        if (target != null) target.Show(severedMesh, color);
    }

    /// <summary>Clears the tint and requirement line once the aim leaves: a frame with no HoverOver call means the player looked away.</summary>
    private void LateUpdate()
    {
        if (!hovering) ClearHover();
        hovering = false;
    }

    /// <summary>A tint or line left on would sit frozen on screen; clear both when this body is disabled.</summary>
    private void OnDisable()
    {
        ClearHover();
    }

    public void Interact(Interactor player)
    {
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (!Physics.Raycast(ray, out RaycastHit hit)) Debug.LogError("Should not happen: raycast on interact hit. Check layers");

        CuttingManager aimed = CutRegistry.CutAt(this, hit.point);
        if(aimed == null) return;

        // read the held item the same way the hover highlight does, so what the tint promised is what
        // the click does. Empty hands give null, which HasRequiredTool refuses -- no separate guard for
        // it, and no dereference of a heldObject that isn't there.
        Item heldItem = player != null && player.heldObject != null ? player.heldObject.item : null;

        if(aimed.canEnterMinigame() && aimed.HasRequiredTool(heldItem)){
            // read before entering: EnterMinigame is what empties the hand, via the respawn below.
            GrabbableObject usedTool = player.heldObject;

            aimed.EnterMinigame();

            // the cut draws its own tool prop, so the held one is not needed once the minigame owns the
            // screen: send it home on the same timer a delivered tool uses. Guarded on currentGame rather
            // than on canEnterMinigame alone -- EnterMinigame still refuses a cut with missing wiring, and
            // that must leave the tool in hand instead of quietly consuming it.
            if (usedTool != null && CuttingManager.currentGame == aimed) usedTool.StartRespawnTimer();
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

            // through the plane's WindowSize/WindowCenter, never its raw boundsSize: those are what
            // resolve the window box, and the slice has to use the same rectangle the guide and the
            // gizmo do or the cut disagrees with what was authored.
            return CutContour.BuildBounds(plane.transform, plane.WindowSize, gameObject, plane.WindowCenter);
        }

        /// <summary>Extracts every cut loop of an object against the finite quad the plane transform defines.</summary>
        /// <param name="meshObj">Object being cut; supplies the mesh and the mesh-local frame of the result.</param>
        /// <param name="plane">Cutting plane; its position + up give the cut and its scale gives the finite window.</param>
        /// <param name="windowSize">Window rectangle in the plane's local units (the plane's own scale multiplies it); defaults to a unit rectangle. Pass <c>CutPlane.WindowSize</c> to match the slice window.</param>
        /// <param name="windowCenter">Window centre offset in the plane's local X/Z. Pass <c>CutPlane.WindowCenter</c> alongside the size; a size taken from a box with an offset centre is only half of that window.</param>
        /// <returns>Mesh-local loops of <paramref name="meshObj"/>; empty when it has no <c>MeshFilter</c> with a shared mesh.</returns>
        public static List<SavedLoop> GetLoops(GameObject meshObj, Transform plane, float weld = 1e-4f, Vector2? windowSize = null, Vector2 windowCenter = default) {
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

            CutContour.PlaneBounds? bounds = CutContour.BuildBounds(plane, windowSize ?? Vector2.one, meshObj, windowCenter);

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

