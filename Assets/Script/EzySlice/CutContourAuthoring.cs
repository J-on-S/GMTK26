using System.Collections.Generic;
using UnityEngine;


namespace EzySlice {

    /// <summary>Editor helper that extracts a mesh's cut contour against a plane, then stores and draws it for the minigame.</summary>
    /// <remarks>
    /// Invariant: extracts the contour only; it never slices the mesh.
    /// Invariant: stored points are in mesh-local space; convert with <c>transform.TransformPoint</c>.
    /// </remarks>
    [ExecuteInEditMode]
    public class CutContourAuthoring : MonoBehaviour {

        /// <summary>One serialized contour.</summary>
        [System.Serializable]
        public class SavedLoop {
            /// <summary>Ordered contour points, in mesh-local space.</summary>
            public List<Vector3> points = new List<Vector3>();

            /// <summary><c>false</c> for an open chain left by clipping; do not trace an edge from the last point back to the first.</summary>
            public bool closed = true;
        }

        [Tooltip("Transform whose position + up axis define the cutting plane.")]
        public Transform planeTransform;

        [Tooltip("Weld distance for merging cut points (mesh-local units).")]
        public float weld = 1e-4f;

        [Tooltip("Limit the cut to a finite rectangle on the plane (planeTransform right/forward, sized below).")]
        public bool useBounds = false;

        [Tooltip("Rectangle size on the plane, in planeTransform local units (X = right, Y = forward).")]
        public Vector2 boundsSize = Vector2.one;

        [Tooltip("Also cut two parallel planes offset +/- half-width along the normal, giving a band that stays ON the surface.")]
        public bool showBand = false;

        [Tooltip("Total band width (world units) between the two offset planes, measured along the plane normal.")]
        public float bandWidth = 0.05f;

        [Tooltip("Outward offset of the orange preview loop from its centre.")]
        public float cameraScale = 0.05f;

        [Tooltip("Auto-recompute every editor frame. Turn off once the loops are baked.")]
        public bool liveUpdate = true;

        [Tooltip("Extracted loops in MESH-LOCAL space. Convert with transform.TransformPoint at runtime.")]
        public List<SavedLoop> savedLoops = new List<SavedLoop>();

        [Tooltip("Band edge on the -normal side (twin-plane cut, on the surface). Filled when showBand is on.")]
        public List<SavedLoop> innerLoops = new List<SavedLoop>();

        [Tooltip("Band edge on the +normal side (twin-plane cut, on the surface). Filled when showBand is on.")]
        public List<SavedLoop> outerLoops = new List<SavedLoop>();

        /// <summary>Builds the finite-window bounds from the plane and this object.</summary>
        /// <returns><c>null</c> when <c>useBounds</c> is off or no plane is assigned.</returns>
        private CutContour.PlaneBounds? BuildBounds() {
            if (!useBounds || planeTransform == null) return null;
            return CutContour.BuildBounds(planeTransform, boundsSize, gameObject);
        }

        /// <summary>Recomputes and stores every cut loop from the current plane.</summary>
        /// <returns>The number of loops in <c>savedLoops</c>.</returns>
        public int Recompute() {
            innerLoops.Clear();
            outerLoops.Clear();

            if (planeTransform == null || !TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null) {
                savedLoops.Clear();
                return 0;
            }

            // main cut: window sized by the plane's own scale
            savedLoops = GetLoops(gameObject, planeTransform, weld);

            Mesh mesh = filter.sharedMesh;
            Vector3 center = planeTransform.position;
            Vector3 normalWorld = planeTransform.up.normalized;

            // twin-plane band: two parallel cuts offset +/- half-width along the normal,
            // both real surface intersections, so the band hugs the mesh surface.
            if (showBand) {
                float half = bandWidth * 0.5f;
                ExtractAt(mesh, center - normalWorld * half, normalWorld, innerLoops);
                ExtractAt(mesh, center + normalWorld * half, normalWorld, outerLoops);
            }

            return savedLoops.Count;
        }

        /// <summary>Cuts the mesh with a world-space plane and appends the loops to <paramref name="dst"/>.</summary>
        /// <param name="worldPos">Point on the cutting plane, in world space.</param>
        /// <param name="worldNormal">Plane normal, in world space.</param>
        private void ExtractAt(Mesh mesh, Vector3 worldPos, Vector3 worldNormal, List<SavedLoop> dst) {
            Plane plane = new Plane(
                transform.InverseTransformPoint(worldPos),
                transform.InverseTransformDirection(worldNormal).normalized);

            List<CutContour.Loop> loops = CutContour.ExtractLoops(mesh, plane, weld, BuildBounds());
            ToSavedLoops(loops, dst);
        }

        /// <summary>Extracts every cut loop of an object against the finite quad the plane transform defines.</summary>
        /// <param name="meshObj">Object being cut; supplies the mesh and the mesh-local frame of the result.</param>
        /// <param name="plane">Cutting plane; its position + up give the cut and its scale gives the finite window.</param>
        /// <returns>Mesh-local loops of <paramref name="meshObj"/>; empty when it has no <c>MeshFilter</c> with a shared mesh.</returns>
        public static List<SavedLoop> GetLoops(GameObject meshObj, Transform plane, float weld = 1e-4f) {
            var result = new List<SavedLoop>();

            if (meshObj == null || plane == null ||
                !meshObj.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null) {
                return result;
            }

            Transform mt = meshObj.transform;

            Plane pl = new Plane(
                mt.InverseTransformPoint(plane.position),
                mt.InverseTransformDirection(plane.up).normalized);

            // A unit rectangle maps to the plane's actual scaled quad: the plane's scale is
            // already baked into the matrix BuildBounds builds, so passing it again would double it.
            CutContour.PlaneBounds? bounds = CutContour.BuildBounds(plane, Vector2.one, meshObj);

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

            if (showBand) {
                DrawLoops(transform, innerLoops, Color.yellow, false);
                DrawLoops(transform, outerLoops, Color.cyan, false);
            }
            if (useBounds && planeTransform != null) {
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
}
