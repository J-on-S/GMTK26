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


    [Header("Loop guide")]
    [Tooltip("Draw the curved target loop into loopLine.")]
    public bool showCurvedLoop = true;

    [Tooltip("Draw the raw flat cut loop into flatLine.")]
    public bool showFlatLoop = false;

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

    private void Update() {
        // curved is exempt: it is the loop the player cuts along, so it draws in play whatever this says.
        bool hiddenInPlay = Application.isPlaying && !showInPlayMode;

        bool drawCurved = showCurvedLoop && loopLine != null;
        bool drawFlat = showFlatLoop && flatLine != null && !hiddenInPlay;
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

    void OnValidate() {
        if (loopLine != null) {
            loopLine.widthCurve = AnimationCurve.Constant(0, 1, curveWidth);
        }
        if (flatLine != null) {
            flatLine.widthCurve = AnimationCurve.Constant(0, 1, curveWidth);
        }
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
            || gSeed != preset.curveSeed || gRandom != preset.curveRandom || gHoverLength != curveHoverLength;
        if (!dirty) {
            return;
        }

        curvedGuide = BuildCurvedGuide(center, flatWorld);
        curvedDraw = BuildHoverLift(center, curvedGuide);
        curvedLength = LoopScorer.SampledLength(curvedGuide);

        guideVersion = extractVersion;
        gAmp = preset.curveAmplitude;
        gWaves = preset.curveWaves;
        gPhase = preset.curvePhase;
        gSeed = preset.curveSeed;
        gRandom = preset.curveRandom;
        gHoverLength = curveHoverLength;
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
                    p = TryProjectOntoSurface(expected, rdir, band, out Vector3 snapped, out _)
                        ? snapped        // snapped to the local surface
                        : expected;      // no local surface in band: keep the naive lift
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
        if (curveHoverLength == 0f || pts == null) {
            return pts;
        }
        Vector3 up = PlaneTransform.up;
        var lifted = new List<Vector3>(pts.Count);
        for (int i = 0; i < pts.Count; i++) {
            Vector3 flat = pts[i] - center;
            Vector3 radial = flat - up * Vector3.Dot(flat, up);
            Vector3 dir = radial.sqrMagnitude > 1e-8f ? radial.normalized : Vector3.zero;
            lifted.Add(pts[i] + dir * curveHoverLength);
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

    /// <summary>Pushes a loop of points into a LineRenderer at the guide width.</summary>
    private void DrawInto(LineRenderer lr, List<Vector3> points, bool closed) {
        lr.loop = closed;
        lr.widthCurve = AnimationCurve.Constant(0f, 1f, curveWidth);
        lr.positionCount = points.Count;
        lr.SetPositions(points.ToArray());
    }

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
