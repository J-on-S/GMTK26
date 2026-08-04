using System.Collections.Generic;
using UnityEngine;
using EzySlice;

/// <summary>Builds and draws the curved target loop the player must trace.</summary>
/// <remarks>
/// Self-contained: it extracts the flat cut loop from <c>meshFollow</c> against its <c>plane</c>
/// (re-extracting only when either moves), reshapes it into a wavy, surface-snapped guide and renders
/// it into <c>loopLine</c> every frame, including edit mode. It never moves the camera.
/// </remarks>
[ExecuteAlways]
public class LoopGuideBuilder : MonoBehaviour {

    [Tooltip("Object being cut (its MeshFilter supplies the mesh, its Collider snaps the guide onto the surface).")]
    [HideInInspector] public CuttableObject meshFollow;

    [Tooltip("Cut this guide draws. Supplies both the plane and its window, so the guide previews exactly the loop the slice will use.")]
    public CutPlane plane;

    /// <summary>The plane's transform, or null when no plane is assigned.</summary>
    private Transform PlaneTransform => plane != null ? plane.transform : null;

    [HideInInspector] public CurvePreset preset;
    
    [HideInInspector] public float curveWidth = 0.005f;
    [HideInInspector] public float curveHoverLength = 0.01f;

    /// <summary>Smallest number of points the loop is warped and drawn with; 0 leaves the extraction as it came.</summary>
    /// <remarks>
    /// The cross-section has exactly one point per triangle edge the plane crosses, so a low-poly body
    /// gives a ring of eight or ten. That is enough for a flat loop, and not nearly enough once each
    /// point is pushed up or down by the curve: neighbouring points land far apart and the ring reads as
    /// a zigzag rather than a wave. Subdividing first gives the warp something to shape.
    /// </remarks>
    [HideInInspector] public int curveResolution = 0;


    [Header("Loop guide")]
    [Tooltip("Draw the curved target loop into loopLine. Off by default: cuts show only the straight flat loop in play. CuttingManager forces this off every push.")]
    public bool showCurvedLoop = false;

    [Tooltip("Draw the raw flat (straight) cut loop into flatLine. On by default: this is the line shown in play. CuttingManager forces this on every push.")]
    public bool showFlatLoop = true;

    [Tooltip("Optional LineRenderer that draws the curved target loop each frame so the player can see where to cut.")]
    public LineRenderer loopLine;

    [Tooltip("Optional LineRenderer for the flat cut loop (raw cross-section).")]
    public LineRenderer flatLine;

    [Tooltip("Draw the lines in play mode too. Off, they are an authoring aid and only appear in edit mode. The curved target loop always draws either way. Pushed down by the CuttingManager that owns this guide.")]
    public bool showInPlayMode = true;

    [Tooltip("Rub the curved guide out behind the scalpel as it passes, so the drawn line is what is left to cut. Off, the whole ring stays drawn for the run.")]
    public bool eraseTraced = true;

    /// <summary>How much of the ring has been traced, 0..1. Negative means nothing is driving the cut and the whole ring draws.</summary>
    private float tracedFraction = -1f;

    /// <summary>Ring angle the trace started at, in degrees -- the cut's startAngle. Where the erasing begins from.</summary>
    private float traceStartAngle;

    /// <summary>Cached middle cut loop, in mesh-local space; re-extracted only when the plane or mesh moves.</summary>
    private List<Vector3> cachedLocal;

    /// <summary>Cached flat cut loop in world space; rebuilt alongside <c>cachedLocal</c>.</summary>
    private List<Vector3> cachedWorld;

    /// <summary>Cached world-space centre of the flat loop; rebuilt alongside <c>cachedLocal</c>.</summary>
    private Vector3 cachedCenter;

    /// <summary>Cached arc length of the flat loop, world units; rebuilt alongside <c>cachedLocal</c>.</summary>
    private float flatLength;

    /// <summary>Plane transform pose at the last extraction.</summary>
    private Matrix4x4 lastPlane;

    /// <summary>Mesh transform pose at the last extraction.</summary>
    private Matrix4x4 lastMesh;

    /// <summary>Mesh instance at the last extraction; a slice swaps the sharedMesh without moving the transform, so pose checks alone would serve a stale loop.</summary>
    private Mesh lastSharedMesh;

    /// <summary>Cut window at the last extraction. Part of the signature because resizing the window changes which loops survive it without moving a single transform -- and dragging a window box is exactly how the window gets tuned.</summary>
    private Vector2 lastWindowSize;

    /// <summary>Cut window centre at the last extraction, for the same reason as <c>lastWindowSize</c>.</summary>
    private Vector2 lastWindowCenter;

    /// <summary>Whether <c>cachedLocal</c> holds a result from a completed extraction.</summary>
    private bool cacheValid;

    /// <summary>Version counter bumped every time the flat loop is re-extracted; invalidates the guide cache.</summary>
    private int extractVersion;

    /// <summary>Cached curved + surface-snapped guide loop, on the raw surface (hover-free). This is the scoring target. Rebuilt only when the extraction or a curve param changes.</summary>
    private List<Vector3> curvedGuide;

    /// <summary>Render-only copy of <c>curvedGuide</c> lifted off the surface by <c>curveHoverLength</c> so the drawn line doesn't z-fight. Never used for scoring.</summary>
    private List<Vector3> curvedDraw;

    /// <summary>Cached arc length of the curved loop, world units; rebuilt alongside <c>curvedGuide</c>.</summary>
    private float curvedLength;

    // curve signature the cached guide was built for
    private int guideVersion = -1;
    private float gAmp = float.NaN, gWaves, gPhase;
    private int gSeed;
    private bool gRandom;
    private float gHoverLength;
    private int gResolution = -1;

    private void Update() {
        // edit mode draws both loops whatever the toggles say: they are the authoring view of this cut,
        // and an author who cannot see the loop cannot place the plane. The toggles govern play only.
        bool editMode = !Application.isPlaying;

        // curved is exempt from showInPlayMode: it is the loop the player cuts along.
        bool drawCurved = loopLine != null && (editMode || showCurvedLoop);
        bool drawFlat = flatLine != null && (editMode || (showFlatLoop && showInPlayMode));
        if (!drawCurved && !drawFlat) {
            // turning a loop off mid-run has to take its line with it, or the last frame drawn stays up
            HideLines();
            return;
        }

        // draw in edit mode too, so the loops are visible while authoring.
        if (!TryGetLoop(out Vector3 center, out List<Vector3> loopPoints)) {
            // no closed loop right now: clear the lines instead of freezing the last drawn one
            HideLines();
            return;
        }

        if (drawCurved) {
            MaybeRebuildGuide(center, loopPoints);
        }
        DrawLoopGuide(drawFlat, drawCurved, loopPoints);
    }

    /// <summary>Cutting-plane normal (world space). <c>Vector3.up</c> when no plane is assigned.</summary>
    public Vector3 PlaneNormal => plane != null ? plane.Normal : Vector3.up;

    /// <summary>Cutting-plane right axis (world space). <c>Vector3.right</c> when no plane is assigned.</summary>
    public Vector3 PlaneRight => plane != null ? plane.transform.right : Vector3.right;

    /// <summary>Cutting-plane forward axis (world space). <c>Vector3.forward</c> when no plane is assigned.</summary>
    public Vector3 PlaneForward => plane != null ? plane.transform.forward : Vector3.forward;

#if UNITY_EDITOR
    // guards against stacking one deferred apply per OnValidate call; not serialized, purely edit-time.
    [System.NonSerialized] private bool widthApplyQueued;
#endif

    /// <remarks>Deferred for the same reason as <c>CuttingManager.OnValidate</c>: the width lands on the
    /// two LineRenderers, which are other objects, and a validate that fires during a prefab apply must
    /// not fight it -- an override applied to the prefab would revert on the spot.</remarks>
    void OnValidate() {
#if UNITY_EDITOR
        if (widthApplyQueued) return;
        widthApplyQueued = true;
        UnityEditor.EditorApplication.delayCall += RunDeferredWidthApply;
#endif
    }

#if UNITY_EDITOR
    private void RunDeferredWidthApply() {
        widthApplyQueued = false;

        if (this == null) return; // destroyed between the validate and this callback
        ApplyLineWidth();
    }
#endif

    /// <summary>Writes <see cref="curveWidth"/> onto both line renderers.</summary>
    /// <remarks>Public so the <see cref="CuttingManager"/> that owns the width can land it the moment it
    /// pushes: this component's own OnValidate does not fire when another script writes its fields, so
    /// without this the new width waits for the next frame that happens to draw.</remarks>
    public void ApplyLineWidth() {
        WriteWidth(loopLine);
        WriteWidth(flatLine);
    }

    /// <summary>Sets one line's width, undoably in edit mode, and only when it is not already there.</summary>
    /// <remarks>The skip matters: this is reached from every push and every deferred validate, and an
    /// unconditional write registers an undo step and dirties the renderer -- on a prefab instance, an
    /// override -- for a value that did not move.</remarks>
    private void WriteWidth(LineRenderer line) {
        if (line == null) return;

        if (Mathf.Approximately(line.widthMultiplier, 1f)
            && line.widthCurve.length == 1
            && Mathf.Approximately(line.widthCurve[0].value, curveWidth)) {
            return;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying) {
            UnityEditor.Undo.RecordObject(line, "Set guide line width");
        }
#endif

        line.widthCurve = AnimationCurve.Constant(0, 1, curveWidth);
        line.widthMultiplier = 1f;

#if UNITY_EDITOR
        if (!Application.isPlaying) {
            UnityEditor.EditorUtility.SetDirty(line);
        }
#endif
    }

    /// <summary>Gets the centre and world-space contour points of the middle cut loop.</summary>
    /// <returns><c>false</c> when no plane or mesh is set, or the plane misses the mesh.</returns>
    /// <remarks>Invariant: the loop is re-extracted only when the plane or mesh transform moves; other frames reuse the cache.</remarks>
    public bool TryGetLoop(out Vector3 center, out List<Vector3> loopPoints) {
        center = Vector3.zero;
        loopPoints = null;

        if (meshFollow == null || plane == null) {
            return false;
        }

        Transform mt = meshFollow.transform;
        Matrix4x4 planePose = plane.transform.localToWorldMatrix;
        Matrix4x4 meshPose = mt.localToWorldMatrix;
        Mesh sharedMesh = meshFollow.TryGetComponent<MeshFilter>(out var mf) ? mf.sharedMesh : null;

        // re-extract only when the plane, the mesh transform or the mesh itself changed since
        // the last extraction (a slice swaps the sharedMesh in place); the world loop, centre
        // and arc length are all cached in the same block, so every frame in between just
        // returns them.
        // match the slice window and weld when the target is a CuttableObject, so the
        // guide shows exactly the loop the cut will use
        Vector2 window = plane.WindowSize;
        Vector2 windowCenter = plane.WindowCenter;

        if (!cacheValid || planePose != lastPlane || meshPose != lastMesh || sharedMesh != lastSharedMesh
            || window != lastWindowSize || windowCenter != lastWindowCenter) {
            float weld = meshFollow != null ? meshFollow.weld : 1e-4f;

            // the guide must be a full ring the player can trace: take the largest CLOSED
            // loop and ignore open chains the window clipped (they come first in the list)
            var loops = CuttableObject.GetLoops(meshFollow.gameObject, plane.transform, weld, window, windowCenter);
            cachedLocal = null;
            for (int i = 0; i < loops.Count; i++) {
                if (loops[i].closed && (cachedLocal == null || loops[i].points.Count > cachedLocal.Count)) {
                    cachedLocal = loops[i].points;
                }
            }
            lastPlane = planePose;
            lastMesh = meshPose;
            lastSharedMesh = sharedMesh;
            lastWindowSize = window;
            lastWindowCenter = windowCenter;
            cacheValid = true;
            extractVersion++; // invalidate the curved-guide cache

            if (cachedLocal != null && cachedLocal.Count > 0) {
                cachedCenter = mt.TransformPoint(CutContour.GetCenter(cachedLocal));
                cachedWorld = new List<Vector3>(cachedLocal.Count);
                for (int i = 0; i < cachedLocal.Count; i++) {
                    cachedWorld.Add(mt.TransformPoint(cachedLocal[i]));
                }
                flatLength = LoopScorer.SampledLength(cachedWorld);
            } else {
                cachedWorld = null;
                flatLength = 0f;
            }
        }

        if (cachedWorld == null || cachedWorld.Count == 0) {
            return false;
        }

        center = cachedCenter;
        loopPoints = cachedWorld;
        return true;
    }

    /// <summary>World-space centre of the cut loop. <c>Vector3.zero</c> when the plane misses the mesh. Cached with the extraction.</summary>
    public Vector3 LoopCenter => TryGetLoop(out _, out _) ? cachedCenter : Vector3.zero;

    /// <summary>Arc length of the flat cut loop, in world units. <c>0</c> when the plane misses the mesh. Cached with the extraction.</summary>
    public float FlatLoopLength => TryGetLoop(out _, out _) ? flatLength : 0f;

    /// <summary>Arc length of the curved (surface-snapped) loop, in world units. <c>0</c> when the plane misses the mesh. Cached with the curve rebuild.</summary>
    public float CurvedLoopLength => TryGetCurvedLoop(out _, out _) ? curvedLength : 0f;

    /// <summary>How big this cut's ring is, in world units: the radius of a circle of the same arc length. <c>0</c> when the plane misses the mesh.</summary>
    /// <remarks>
    /// The one number that says how large a cut is, so framing authored as a multiple of it reads the
    /// same on a wrist and on a thigh, and follows a body scaled up or down. Taken from the arc length
    /// rather than from a max vertex distance: the length is already cached with the extraction, and it
    /// is not thrown off by a single spike on the cross-section.
    /// </remarks>
    public float FlatLoopRadius => FlatLoopLength / (2f * Mathf.PI);

    /// <summary>Flat cut loop, world space. <c>false</c> when no plane/mesh is set or the plane misses the mesh.</summary>
    /// <remarks>This is the raw cross-section, before any curve warp.</remarks>
    public bool TryGetFlatLoop(out Vector3 center, out List<Vector3> loopPoints) {
        return TryGetLoop(out center, out loopPoints);
    }

    /// <summary>Curved, surface-snapped target loop, world space. <c>false</c> when no plane/mesh is set or the plane misses the mesh.</summary>
    /// <remarks>Built on demand and cached, independent of <c>loopLine</c>; with <c>curveAmplitude</c> == 0 it equals the flat loop.</remarks>
    public bool TryGetCurvedLoop(out Vector3 center, out List<Vector3> loopPoints) {
        if (!TryGetLoop(out center, out List<Vector3> flat)) {
            loopPoints = null;
            return false;
        }
        MaybeRebuildGuide(center, flat);
        loopPoints = curvedGuide;
        return loopPoints != null;
    }

    /// <summary>The curved loop as it is drawn: lifted off the surface by <c>curveHoverLength</c>.</summary>
    /// <remarks>
    /// Not the same list as <see cref="TryGetCurvedLoop"/>, which hands back the loop sitting exactly on
    /// the mesh -- right for scoring, wrong for drawing. A line rendered on the surface z-fights it and
    /// dips through it triangle by triangle, which looks like a tangle rather than a ring. Anything that
    /// draws the loop wants this one.
    /// </remarks>
    public bool TryGetDrawnCurvedLoop(out List<Vector3> loopPoints) {
        if (!TryGetCurvedLoop(out _, out List<Vector3> curved)) {
            loopPoints = null;
            return false;
        }

        loopPoints = curvedDraw ?? curved;
        return loopPoints != null;
    }

    /// <summary>Rebuilds <c>curvedGuide</c> only when the extraction or any curve param changed since the last build.</summary>
    private void MaybeRebuildGuide(Vector3 center, List<Vector3> flatWorld) {
        // no curve preset yet (a freshly created cut): leave curvedGuide null so the flat loop
        // still draws, instead of throwing out of Update every frame in the editor.
        if (preset == null) {
            return;
        }

        bool dirty = curvedGuide == null
            || guideVersion != extractVersion
            || gAmp != preset.curveAmplitude || gWaves != preset.curveWaves || gPhase != preset.curvePhase
            || gSeed != preset.curveSeed || gRandom != preset.curveRandom || gHoverLength != curveHoverLength
            || gResolution != curveResolution;
        if (!dirty) {
            return;
        }

        curvedGuide = BuildCurvedGuide(center, Densify(flatWorld, curveResolution));
        curvedDraw = BuildHoverLift(center, curvedGuide);
        curvedLength = LoopScorer.SampledLength(curvedGuide);

        guideVersion = extractVersion;
        gAmp = preset.curveAmplitude;
        gWaves = preset.curveWaves;
        gPhase = preset.curvePhase;
        gSeed = preset.curveSeed;
        gRandom = preset.curveRandom;
        gHoverLength = curveHoverLength;
        gResolution = curveResolution;
    }

    /// <summary>Subdivides a closed loop until it has at least <paramref name="minPoints"/> points, keeping every original point.</summary>
    /// <remarks>
    /// Splits each segment evenly rather than resampling by arc length: the extracted points sit exactly
    /// on the cut, and keeping them means the denser loop still passes through the real cross-section
    /// instead of cutting its corners. The inserted points are pulled onto the surface by the raycast in
    /// <see cref="BuildCurvedGuide"/>, so they follow the body rather than chording across it.
    /// </remarks>
    private static List<Vector3> Densify(List<Vector3> loop, int minPoints) {
        if (loop == null || loop.Count < 2 || minPoints <= loop.Count) {
            return loop;
        }

        // segments of a closed loop: every point joins the next, and the last joins the first
        int cuts = Mathf.CeilToInt((float)minPoints / loop.Count);
        var dense = new List<Vector3>(loop.Count * cuts);

        for (int i = 0; i < loop.Count; i++) {
            Vector3 a = loop[i];
            Vector3 b = loop[(i + 1) % loop.Count];

            dense.Add(a);
            for (int k = 1; k < cuts; k++) {
                dense.Add(Vector3.Lerp(a, b, (float)k / cuts));
            }
        }

        return dense;
    }

    /// <summary>Warps the flat loop into a wavy ring that rides the mesh surface: each point's cross-section is slid up/down the body axis by CurveHeight, then raycast back onto the collider.</summary>
    private List<Vector3> BuildCurvedGuide(Vector3 center, List<Vector3> flatWorld) {
        var result = new List<Vector3>(flatWorld.Count);

        Vector3 up = PlaneTransform.up;
        Vector3 right = PlaneTransform.right;
        Vector3 forward = PlaneTransform.forward;
        bool hasCollider = TargetCollider != null;

        for (int i = 0; i < flatWorld.Count; i++) {
            Vector3 p = flatWorld[i];

            if (preset.curveAmplitude != 0f) {
                Vector3 flat = p - center;
                float alongUp = Vector3.Dot(flat, up);
                Vector3 radial = flat - up * alongUp; // point's direction out from the body axis
                float angleRad = Mathf.Atan2(Vector3.Dot(flat, forward), Vector3.Dot(flat, right));
                float h = CurveHeight(angleRad);

                float r0 = radial.magnitude;
                if (r0 > 1e-4f && hasCollider) {
                    Vector3 rdir = radial / r0;
                    // where the point wants to sit after sliding h up the body axis
                    Vector3 expected = p + up * h;

                    // scan only a SHORT band around the point, not the whole mesh. band < arm
                    // radius, so the ray can never reach the torso / other arm.
                    float band = Mathf.Min(Mathf.Abs(h) + r0 * 0.25f, r0 * 0.9f);

                    // Invariant: a point that finds no surface falls back to the FLAT loop, not to the
                    // lift that missed. The lift is a guess at where the body would be h along its axis;
                    // when the ray cannot confirm it -- an amplitude larger than the limb, a slide off
                    // the end of it, a taper the band cannot cross -- that guess is a point hanging in
                    // the air, and a ring of them reads as green spaghetti beside the patient. The flat
                    // point is always on the cut, so the guide degrades to a plain ring instead.
                    p = TryProjectOntoSurface(expected, rdir, band, out Vector3 snapped, out _)
                        ? snapped        // snapped to the local surface
                        : p;             // no local surface in band: stay on the flat loop
                } else {
                    p += up * h;
                }
            }

            result.Add(p);
        }

        return result;
    }

    /// <summary>Copies a loop and lifts each point off the surface by <c>curveHoverLength</c> along its outward radial (plane-normal component removed), for drawing only.</summary>
    private List<Vector3> BuildHoverLift(Vector3 center, List<Vector3> pts) {
        return BuildHoverLift(center, pts, curveHoverLength);
    }

    /// <summary>The same lift at a caller's own distance, for anything that draws this loop with its own hover.</summary>
    /// <remarks>Public so a second line over the same ring -- the scalpel's trace -- can sit at its own
    /// height instead of inheriting the guide's, which is preset-owned and not the trace's to set.</remarks>
    public List<Vector3> BuildHoverLift(Vector3 center, List<Vector3> pts, float distance) {
        if (distance == 0f || pts == null || PlaneTransform == null) {
            return pts;
        }
        Vector3 up = PlaneTransform.up;
        var lifted = new List<Vector3>(pts.Count);
        for (int i = 0; i < pts.Count; i++) {
            Vector3 flat = pts[i] - center;
            Vector3 radial = flat - up * Vector3.Dot(flat, up);
            Vector3 dir = radial.sqrMagnitude > 1e-8f ? radial.normalized : Vector3.zero;
            lifted.Add(pts[i] + dir * distance);
        }
        return lifted;
    }

    /// <summary>Cached collider of <c>meshFollow</c>; re-fetched only when <c>meshFollow</c> changes.</summary>
    private Collider cachedCollider;
    private GameObject cachedColliderOwner;

    /// <summary>Collider used for surface projection: <c>meshFollow</c>'s, cached.</summary>
    public Collider TargetCollider {
        get {
            if (meshFollow == null) {
                return null;
            }
            if (cachedCollider == null || cachedColliderOwner != meshFollow) {
                cachedCollider = meshFollow.GetComponent<Collider>();
                cachedColliderOwner = meshFollow.gameObject;
            }
            return cachedCollider;
        }
    }

    /// <summary>Projects <paramref name="near"/> onto the cut mesh by raycasting inward along <c>-rdir</c> within a short band. Returns the raw surface hit (no hover); callers add any render lift themselves.</summary>
    /// <param name="near">Point to snap; the ray starts one <paramref name="band"/> outside it along <paramref name="rdir"/>.</param>
    /// <param name="rdir">Outward direction (unit); the ray shoots the opposite way, into the surface.</param>
    /// <param name="band">Half the ray length. Keep it below the body radius so the ray can't reach a far surface.</param>
    /// <param name="surfacePoint">Raw surface hit; <paramref name="near"/> otherwise.</param>
    /// <param name="surfaceNormal">World-space surface normal at the hit; <paramref name="rdir"/> otherwise.</param>
    /// <returns><c>true</c> when the ray hit the mesh within the band.</returns>
    public bool TryProjectOntoSurface(Vector3 near, Vector3 rdir, float band, out Vector3 surfacePoint, out Vector3 surfaceNormal) {
        surfacePoint = near;
        surfaceNormal = rdir;
        Collider col = TargetCollider;
        if (col == null) {
            return false;
        }
        Vector3 start = near + rdir * band;
        if (col.Raycast(new Ray(start, -rdir), out RaycastHit rh, band * 2f)) {
            surfacePoint = rh.point;
            surfaceNormal = rh.normal;
            return true;
        }
        return false;
    }

    /// <summary>Renders the selected loops into their line renderers.</summary>
    private void DrawLoopGuide(bool drawFlat, bool drawCurved, List<Vector3> flat) {
        if (drawFlat && flat != null) {
            flatLine.enabled = true;
            DrawInto(flatLine, flat, closed: true);
        }
        else if(flat != null)
        {
            flatLine.enabled = false;
        }
        if (drawCurved && curvedGuide != null) {
            // the flat line is left whole: it is the raw cross-section, an authoring aid rather than
            // the line the player traces
            DrawGuideLine(curvedDraw ?? curvedGuide);
        }
        else if(curvedGuide != null)
        {
            loopLine.enabled = false;
        }
    }

    /// <summary>Draws the target loop, minus whatever the scalpel has already gone over.</summary>
    private void DrawGuideLine(List<Vector3> points) {
        if (!eraseTraced || tracedFraction < 0f) {
            loopLine.enabled = true;
            DrawInto(loopLine, points, closed: true);
            return;
        }

        if (tracedFraction >= 1f) {
            // the whole ring is behind the scalpel
            loopLine.enabled = false;
            return;
        }

        List<Vector3> remaining = Remaining(points, tracedFraction);
        if (remaining == null || remaining.Count < 2) {
            loopLine.enabled = false;
            return;
        }

        loopLine.enabled = true;

        // open: closing it would draw a chord across the body between the scalpel and the start
        DrawInto(loopLine, remaining, closed: false);
    }

    /// <summary>The stretch of the loop still ahead of the scalpel, in draw order.</summary>
    /// <returns><c>null</c> when fewer than two points are left, which is too few to draw.</returns>
    private List<Vector3> Remaining(List<Vector3> points, float fraction) {
        int n = points.Count;
        if (n < 2) {
            return null;
        }

        int startIndex = IndexAtAngle(points, traceStartAngle);
        int step = IndexStep(points);

        int consumed = Mathf.Clamp(Mathf.RoundToInt(fraction * n), 0, n);
        int keep = n - consumed;
        if (keep < 2) {
            return null;
        }

        var result = new List<Vector3>(keep);
        for (int i = 0; i < keep; i++) {
            // Repeat, not %, so a negative step still wraps into range
            int index = (int)Mathf.Repeat(startIndex + step * (consumed + i), n);
            result.Add(points[index]);
        }
        return result;
    }

    /// <summary>Index of the loop point lying closest to a given ring angle.</summary>
    /// <param name="degrees">Measured in the cutting plane's own basis, the one the orbit angle is measured in.</param>
    private int IndexAtAngle(List<Vector3> points, float degrees) {
        float rad = degrees * Mathf.Deg2Rad;
        Vector3 dir = PlaneRight * Mathf.Cos(rad) + PlaneForward * Mathf.Sin(rad);
        Vector3 center = cachedCenter;

        int best = 0;
        float bestDot = float.NegativeInfinity;
        for (int i = 0; i < points.Count; i++) {
            Vector3 radial = points[i] - center;
            radial -= PlaneNormal * Vector3.Dot(radial, PlaneNormal);
            if (radial.sqrMagnitude < 1e-10f) {
                continue;
            }

            float dot = Vector3.Dot(radial.normalized, dir);
            if (dot > bestDot) {
                bestDot = dot;
                best = i;
            }
        }
        return best;
    }

    /// <summary>Whether walking the point list forwards goes the same way round the ring as the cut does.</summary>
    /// <returns><c>+1</c> when it does, <c>-1</c> when it runs against it.</returns>
    private int IndexStep(List<Vector3> points) {
        // signed area over the whole loop, not two neighbouring points, which sit close enough
        // together for one noisy vertex to flip the sign
        Vector3 center = cachedCenter;
        float area = 0f;
        for (int i = 0; i < points.Count; i++) {
            Vector3 a = points[i] - center;
            Vector3 b = points[(i + 1) % points.Count] - center;

            float ax = Vector3.Dot(a, PlaneRight), ay = Vector3.Dot(a, PlaneForward);
            float bx = Vector3.Dot(b, PlaneRight), by = Vector3.Dot(b, PlaneForward);
            area += ax * by - ay * bx;
        }

        int winding = area >= 0f ? 1 : -1;

        // the cut itself can run either way round
        int sweep = tracedSweepSign >= 0 ? 1 : -1;
        return winding * sweep;
    }

    /// <summary>Which way round the ring the cut travels: <c>+1</c> for increasing angle, <c>-1</c> for decreasing.</summary>
    private int tracedSweepSign = 1;

    /// <summary>Tells the guide how far the scalpel has got, so the traced stretch stops being drawn.</summary>
    /// <param name="endDegrees">Only its side of <paramref name="startDegrees"/> is read, to tell which way round the cut runs.</param>
    /// <param name="fraction">How much of the ring is behind the scalpel, <c>0</c>..<c>1</c>.</param>
    public void SetTraceProgress(float startDegrees, float endDegrees, float fraction) {
        traceStartAngle = startDegrees;
        tracedSweepSign = endDegrees >= startDegrees ? 1 : -1;
        tracedFraction = Mathf.Clamp01(fraction);
    }

    /// <summary>Puts the whole ring back, for when no cut is running.</summary>
    public void ClearTrace() {
        tracedFraction = -1f;
    }

    /// <summary>Takes both lines off screen, leaving their points alone.</summary>
    private void HideLines() {
        if (loopLine != null) loopLine.enabled = false;
        if (flatLine != null) flatLine.enabled = false;
    }

    /// <summary>Pushes a loop of points into a LineRenderer at the guide width, skipping a redraw that would change nothing.</summary>
    /// <remarks>
    /// The points arrive in world space and are written in the line's own space, with
    /// <c>useWorldSpace</c> off. World-space points are a property of where the body happens to stand,
    /// so the same ring serialises to different numbers in the prefab stage (root at the origin) than on
    /// an instance placed in a scene -- and a LineRenderer's points ARE serialised, so alternating
    /// between the two rewrote every point of every guide line on this body each time. In the line's own
    /// space the numbers depend only on the mesh, the plane and the preset, so they are written once and
    /// then match forever. It also fixes a latent bug: a body that moves during play now takes its
    /// drawn ring with it instead of leaving it behind until the next re-extraction.
    /// </remarks>
    private void DrawInto(LineRenderer lr, List<Vector3> points, bool closed) {
        Matrix4x4 toLocal = lr.transform.worldToLocalMatrix;

        if (!Application.isPlaying) {
            LineCache cache = lr == flatLine ? flatCache : curvedCache;

            // cheap check first: the same world list, drawn from the same line pose
            if (cache.WouldDrawTheSame(lr, points, curveWidth, closed, toLocal)) {
                return;
            }

            // and the one that matters: a fresh list whose points equal what the line already holds.
            // This is the common case on a scene or prefab open -- the cache is empty after a domain
            // reload, so the first frame would otherwise rewrite the array with the values already in
            // it and leave a whole-array override on the prefab that nobody authored.
            if (AlreadyHolds(lr, points, toLocal, closed)) {
                return;
            }
        }

        FillWriteScratch(points, toLocal);

        WriteSpace(lr);
        if (lr.loop != closed) lr.loop = closed;
        WriteWidth(lr); // same path as a push, so a redraw cannot leave a width nobody could undo
        lr.positionCount = points.Count;
        lr.SetPositions(writeScratch);
    }

    /// <summary>How far two guide points may sit apart and still count as the same point.</summary>
    /// <remarks>
    /// The stored points came from this same computation on an earlier open, and every step of it --
    /// the contour extraction, the warp, the collider raycasts -- is deterministic for an unchanged
    /// mesh, plane and preset, so a match here is normally exact. The tolerance only absorbs the last
    /// bit or two; it is orders of magnitude below any movement an author could make by hand, so a real
    /// edit is never mistaken for a redraw of the same ring.
    /// </remarks>
    private const float SamePointEpsilon = 1e-6f;

    /// <summary>Scratch buffers, sized to the loop. Reused rather than allocated per call: these run every edit-mode frame, and the comparison usually ends in "no change".</summary>
    private static Vector3[] positionScratch = System.Array.Empty<Vector3>();
    private static Vector3[] writeScratch = System.Array.Empty<Vector3>();

    /// <summary>Fills <see cref="writeScratch"/> with <paramref name="points"/> taken into the line's own space.</summary>
    private static void FillWriteScratch(List<Vector3> points, Matrix4x4 toLocal) {
        if (writeScratch.Length != points.Count) {
            writeScratch = new Vector3[points.Count];
        }
        for (int i = 0; i < points.Count; i++) {
            writeScratch[i] = toLocal.MultiplyPoint3x4(points[i]);
        }
    }

    /// <summary>Takes a line off world space, undoably in edit mode, and only when it is not already off.</summary>
    /// <remarks>Flipped in code rather than left to authoring so a guide line built before this -- every one currently in the project -- converts itself the first time it draws.</remarks>
    private static void WriteSpace(LineRenderer line) {
        if (!line.useWorldSpace) return;

#if UNITY_EDITOR
        if (!Application.isPlaying) {
            UnityEditor.Undo.RecordObject(line, "Set guide line space");
        }
#endif

        line.useWorldSpace = false;

#if UNITY_EDITOR
        if (!Application.isPlaying) {
            UnityEditor.EditorUtility.SetDirty(line);
        }
#endif
    }

    /// <summary>Whether <paramref name="lr"/> is already drawing exactly these world points, compared in the line's own space.</summary>
    private static bool AlreadyHolds(LineRenderer lr, List<Vector3> points, Matrix4x4 toLocal, bool closed) {
        if (lr.useWorldSpace || lr.loop != closed || !ReadInto(lr, points.Count)) {
            return false;
        }

        for (int i = 0; i < points.Count; i++) {
            if (Apart(positionScratch[i], toLocal.MultiplyPoint3x4(points[i]))) {
                return false;
            }
        }

        return true;
    }

    /// <summary>Whether a line already holds exactly these points, given in whatever space it draws in.</summary>
    /// <remarks>
    /// Public because every component that previews into a serialized LineRenderer in edit mode needs
    /// this same guard, and they were each carrying their own reference cache -- which a domain reload
    /// empties, so the first frame after rewrote the whole positions array with the values already in
    /// it and left an override nobody authored. Comparing what is stored survives the reload.
    /// </remarks>
    public static bool HoldsPoints(LineRenderer lr, List<Vector3> pointsInLineSpace) {
        if (lr == null || pointsInLineSpace == null || !ReadInto(lr, pointsInLineSpace.Count)) {
            return false;
        }

        for (int i = 0; i < pointsInLineSpace.Count; i++) {
            if (Apart(positionScratch[i], pointsInLineSpace[i])) {
                return false;
            }
        }

        return true;
    }

    /// <summary>Reads a line's current points into <see cref="positionScratch"/>. <c>false</c> when it does not hold <paramref name="count"/> of them, which already settles the comparison.</summary>
    private static bool ReadInto(LineRenderer lr, int count) {
        if (lr.positionCount != count) {
            return false;
        }
        if (positionScratch.Length != count) {
            positionScratch = new Vector3[count];
        }
        lr.GetPositions(positionScratch);
        return true;
    }

    private static bool Apart(Vector3 a, Vector3 b) =>
        (a - b).sqrMagnitude > SamePointEpsilon * SamePointEpsilon;

    /// <summary>What was last pushed into one line, so an edit-mode frame that would redraw the same thing writes nothing.</summary>
    /// <remarks>
    /// A LineRenderer's points are serialized. Rewriting them every frame while this component previews
    /// in edit mode keeps the scene dirty for as long as a cut is on screen and, on a prefab instance,
    /// holds open an override on the whole positions array that nobody authored.
    /// <para>Play is exempt and goes straight through: nothing there is saved, and the erased ring hands
    /// over a freshly built list every frame anyway, so the check could never hit.</para>
    /// <para>The cached loops are compared by reference, which is exactly right here: the builder hands
    /// back the same list until it re-extracts or re-warps, and every reason it would (the plane moved,
    /// the mesh changed, a curve or hover value was edited) replaces the list.</para>
    /// </remarks>
    private sealed class LineCache {
        private List<Vector3> points;
        private float width = float.NaN;
        private bool closed;
        private int count = -1;

        /// <summary>Line pose the points were last taken into. Part of the signature because the stored points are in the line's own space, so moving the line changes them while the world list stays the same object.</summary>
        private Matrix4x4 toLocal = Matrix4x4.zero;

        /// <summary>True when the line already holds this exact drawing; otherwise remembers it and returns false.</summary>
        public bool WouldDrawTheSame(LineRenderer lr, List<Vector3> pts, float lineWidth, bool loop, Matrix4x4 lineToLocal) {
            if (ReferenceEquals(pts, points)
                && closed == loop
                && Mathf.Approximately(width, lineWidth)
                && toLocal == lineToLocal
                && !lr.useWorldSpace
                && lr.positionCount == count) {
                return true;
            }

            points = pts;
            width = lineWidth;
            closed = loop;
            count = pts.Count;
            toLocal = lineToLocal;
            return false;
        }
    }

    private readonly LineCache flatCache = new LineCache();
    private readonly LineCache curvedCache = new LineCache();

    /// <summary>Cumulative end angle (0..2pi) of each random half-cycle segment.</summary>
    private float[] segEnd;

    /// <summary>Signed peak height of each random half-cycle segment.</summary>
    private float[] segAmp;

    /// <summary>Params the segment table was built for, so it rebuilds only when they change.</summary>
    private int builtSeed = int.MinValue;
    private float builtWaves = -1f;
    private float builtAmp = float.NaN;

    /// <summary>How far to push a loop point up/down the body axis for a given angle around the ring, giving the flat cut its wavy "curved plane" profile.</summary>
    /// <param name="angleRad">Angle around the ring, in radians.</param>
    /// <remarks>Clean sine by default; when <c>curveRandom</c> is on it is a chain of random-height, random-width humps, stable per seed.</remarks>
    private float CurveHeight(float angleRad) {
        if (preset.curveAmplitude == 0f) {
            return 0f;
        }

        if (!preset.curveRandom) {
            return preset.curveAmplitude * Mathf.Sin(preset.curveWaves * angleRad + preset.curvePhase * Mathf.Deg2Rad);
        }

        BuildRandomCurve();

        // wrap into one ring turn, plus the phase shift, then find its segment
        float b = Mathf.Repeat(angleRad + preset.curvePhase * Mathf.Deg2Rad, 2f * Mathf.PI);
        int seg = 0;
        while (seg < segEnd.Length - 1 && b >= segEnd[seg]) {
            seg++;
        }
        float start = seg == 0 ? 0f : segEnd[seg - 1];
        float frac = (b - start) / Mathf.Max(segEnd[seg] - start, 1e-6f);

        // half-sine hump: 0 at both ends, so neighbouring segments join with no jump
        return segAmp[seg] * Mathf.Sin(Mathf.PI * frac);
    }

    /// <summary>Builds the random half-cycle table: each hump gets a random width and height, normalised to close the ring. Deterministic per seed.</summary>
    private void BuildRandomCurve() {
        if (segEnd != null && builtSeed == preset.curveSeed && builtWaves == preset.curveWaves && builtAmp == preset.curveAmplitude) {
            return;
        }

        // curveWaves full waves = twice as many half-cycle humps
        int humps = Mathf.Max(1, Mathf.RoundToInt(preset.curveWaves * 2f));
        segEnd = new float[humps];
        segAmp = new float[humps];

        Random.State prev = Random.state;
        Random.InitState(preset.curveSeed);

        float total = 0f;
        for (int i = 0; i < humps; i++) {
            segEnd[i] = Random.Range(0.5f, 1.5f); // width for now; cumulated below
            total += segEnd[i];
            // alternate sign so the ring rises and falls; random fraction of the max height
            float sign = (i % 2 == 0) ? 1f : -1f;
            segAmp[i] = sign * Random.Range(0.3f, 1f) * preset.curveAmplitude;
        }

        // normalise widths to span exactly one turn, store cumulative ends
        float cum = 0f;
        float scale = 2f * Mathf.PI / total;
        for (int i = 0; i < humps; i++) {
            cum += segEnd[i] * scale;
            segEnd[i] = cum;
        }

        Random.state = prev;
        builtSeed = preset.curveSeed;
        builtWaves = preset.curveWaves;
        builtAmp = preset.curveAmplitude;
    }
}


// for lineRenderer material: 
/*
use unlit material : SurfaceType transparent
TextureMode: Tile
// 
all texture must have Alpha is transparency



*/
