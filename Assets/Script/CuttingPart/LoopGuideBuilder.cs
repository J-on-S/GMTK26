using System.Collections.Generic;
using UnityEngine;
using EzySlice;

/// <summary>Builds and draws the curved target loop the player must trace.</summary>
/// <remarks>
/// Self-contained: it extracts the flat cut loop from <c>meshFollow</c> against <c>planeTransform</c>
/// (re-extracting only when either moves), reshapes it into a wavy, surface-snapped guide and renders
/// it into <c>loopLine</c> every frame, including edit mode. It never moves the camera.
/// </remarks>
[ExecuteAlways]
public class LoopGuideBuilder : MonoBehaviour {

    [Tooltip("Object being cut (its MeshFilter supplies the mesh, its Collider snaps the guide onto the surface).")]
    public GameObject meshFollow;

    [Tooltip("Transform whose position + up define the cutting plane.")]
    public Transform planeTransform;

    [Header("Curved plane")]
    [Tooltip("Warp the flat cut into a wavy ring: each loop point is pushed up/down the body axis by a sine of its angle around the ring. 0 = flat. Raise it and the drawn guide loop rides up and down the surface, so the cursor must track a moving target.")]
    public float curveAmplitude = 0f;

    [Tooltip("Number of full up/down waves around the ring. 1 = a single tilt (one high side, one low). Higher = more, tighter humps.")]
    public float curveWaves = 2f;

    public float curvePhase = 0;

    public float curveWidth = 0.005f;
    public float curveHoverLength = 0.01f;

    [Tooltip("Break the clean sine: each half-cycle around the ring gets a random height and width, so the curve is bumpy and irregular instead of a pure wave. Stable per seed, so it stays learnable.")]
    public bool curveRandom = false;

    [Tooltip("Seed for the random curve. Change it to reshuffle the bumps into a new fixed shape.")]
    public int curveSeed = 0;

    [Header("Loop guide")]
    [Tooltip("Draw the curved target loop into loopLine.")]
    public bool showCurvedLoop = true;

    [Tooltip("Draw the raw flat cut loop into flatLine.")]
    public bool showFlatLoop = false;

    [Tooltip("Optional LineRenderer that draws the curved target loop each frame so the player can see where to cut.")]
    public LineRenderer loopLine;

    [Tooltip("Optional LineRenderer for the flat cut loop (raw cross-section).")]
    public LineRenderer flatLine;

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

    /// <summary>Whether <c>cachedLocal</c> holds a result from a completed extraction.</summary>
    private bool cacheValid;

    /// <summary>Version counter bumped every time the flat loop is re-extracted; invalidates the guide cache.</summary>
    private int extractVersion;

    /// <summary>Cached curved + surface-snapped guide loop. Rebuilt only when the extraction or a curve param changes.</summary>
    private List<Vector3> curvedGuide;

    /// <summary>Cached arc length of the curved loop, world units; rebuilt alongside <c>curvedGuide</c>.</summary>
    private float curvedLength;

    // curve signature the cached guide was built for
    private int guideVersion = -1;
    private float gAmp = float.NaN, gWaves, gPhase;
    private int gSeed;
    private bool gRandom;
    private float gHoverLength;

    private void Update() {
        bool drawCurved = showCurvedLoop && loopLine != null;
        bool drawFlat = showFlatLoop && flatLine != null;
        if (!drawCurved && !drawFlat) {
            return;
        }

        // draw in edit mode too, so the loops are visible while authoring.
        if (!TryGetLoop(out Vector3 center, out List<Vector3> loopPoints)) {
            return;
        }

        if (drawCurved) {
            MaybeRebuildGuide(center, loopPoints);
        }
        DrawLoopGuide(drawFlat, drawCurved, loopPoints);
    }

    /// <summary>Cutting-plane normal (world space). <c>Vector3.up</c> when no plane is assigned.</summary>
    public Vector3 PlaneNormal => planeTransform != null ? planeTransform.up : Vector3.up;

    /// <summary>Cutting-plane right axis (world space). <c>Vector3.right</c> when no plane is assigned.</summary>
    public Vector3 PlaneRight => planeTransform != null ? planeTransform.right : Vector3.right;

    /// <summary>Cutting-plane forward axis (world space). <c>Vector3.forward</c> when no plane is assigned.</summary>
    public Vector3 PlaneForward => planeTransform != null ? planeTransform.forward : Vector3.forward;

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

        if (meshFollow == null || planeTransform == null) {
            return false;
        }

        Transform mt = meshFollow.transform;
        Matrix4x4 planePose = planeTransform.localToWorldMatrix;
        Matrix4x4 meshPose = mt.localToWorldMatrix;

        // re-extract only when the plane or mesh has moved since the last extraction; the
        // world loop, centre and arc length are all cached in the same block, so every
        // frame in between just returns them.
        if (!cacheValid || planePose != lastPlane || meshPose != lastMesh) {
            var loops = CutContourAuthoring.GetLoops(meshFollow, planeTransform);
            cachedLocal = loops.Count > 0 ? loops[0].points : null;
            lastPlane = planePose;
            lastMesh = meshPose;
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
        bool dirty = curvedGuide == null
            || guideVersion != extractVersion
            || gAmp != curveAmplitude || gWaves != curveWaves || gPhase != curvePhase
            || gSeed != curveSeed || gRandom != curveRandom || gHoverLength != curveHoverLength;
        if (!dirty) {
            return;
        }

        curvedGuide = BuildCurvedGuide(center, flatWorld);
        curvedLength = LoopScorer.SampledLength(curvedGuide);

        guideVersion = extractVersion;
        gAmp = curveAmplitude;
        gWaves = curveWaves;
        gPhase = curvePhase;
        gSeed = curveSeed;
        gRandom = curveRandom;
        gHoverLength = curveHoverLength;
    }

    /// <summary>Warps the flat loop into a wavy ring that rides the mesh surface: each point's cross-section is slid up/down the body axis by CurveHeight, then raycast back onto the collider.</summary>
    private List<Vector3> BuildCurvedGuide(Vector3 center, List<Vector3> flatWorld) {
        var result = new List<Vector3>(flatWorld.Count);

        Vector3 up = planeTransform.up;
        Vector3 right = planeTransform.right;
        Vector3 forward = planeTransform.forward;
        bool hasCollider = TargetCollider != null;

        for (int i = 0; i < flatWorld.Count; i++) {
            Vector3 p = flatWorld[i];

            if (curveAmplitude != 0f) {
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
                cachedColliderOwner = meshFollow;
            }
            return cachedCollider;
        }
    }

    /// <summary>Projects <paramref name="near"/> onto the cut mesh by raycasting inward along <c>-rdir</c> within a short band, then lifting the hit off the surface by <c>curveHoverLength</c>.</summary>
    /// <param name="near">Point to snap; the ray starts one <paramref name="band"/> outside it along <paramref name="rdir"/>.</param>
    /// <param name="rdir">Outward direction (unit); the ray shoots the opposite way, into the surface.</param>
    /// <param name="band">Half the ray length. Keep it below the body radius so the ray can't reach a far surface.</param>
    /// <param name="surfacePoint">Snapped, hover-offset point on hit; <paramref name="near"/> otherwise.</param>
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
            surfacePoint = rh.point + rdir * curveHoverLength;
            surfaceNormal = rh.normal;
            return true;
        }
        return false;
    }

    /// <summary>Renders the selected loops into their line renderers.</summary>
    private void DrawLoopGuide(bool drawFlat, bool drawCurved, List<Vector3> flat) {
        if (drawFlat && flat != null) {
            flatLine.enabled = true;
            DrawInto(flatLine, flat);
        }
        else if(flat != null)
        {
            flatLine.enabled = false;
        }
        if (drawCurved && curvedGuide != null) {
            loopLine.enabled = true;
            DrawInto(loopLine, curvedGuide);
        }
        else if(curvedGuide != null)
        {
            loopLine.enabled = false;
        }
        if (drawCurved && curvedGuide != null) {
            DrawInto(loopLine, curvedGuide);
        }
    }

    /// <summary>Pushes a closed loop of points into a LineRenderer at the guide width.</summary>
    private void DrawInto(LineRenderer lr, List<Vector3> points) {
        lr.loop = true;
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
        if (curveAmplitude == 0f) {
            return 0f;
        }

        if (!curveRandom) {
            return curveAmplitude * Mathf.Sin(curveWaves * angleRad + curvePhase * Mathf.Deg2Rad);
        }

        BuildRandomCurve();

        // wrap into one ring turn, plus the phase shift, then find its segment
        float b = Mathf.Repeat(angleRad + curvePhase * Mathf.Deg2Rad, 2f * Mathf.PI);
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
        if (segEnd != null && builtSeed == curveSeed && builtWaves == curveWaves && builtAmp == curveAmplitude) {
            return;
        }

        // curveWaves full waves = twice as many half-cycle humps
        int humps = Mathf.Max(1, Mathf.RoundToInt(curveWaves * 2f));
        segEnd = new float[humps];
        segAmp = new float[humps];

        Random.State prev = Random.state;
        Random.InitState(curveSeed);

        float total = 0f;
        for (int i = 0; i < humps; i++) {
            segEnd[i] = Random.Range(0.5f, 1.5f); // width for now; cumulated below
            total += segEnd[i];
            // alternate sign so the ring rises and falls; random fraction of the max height
            float sign = (i % 2 == 0) ? 1f : -1f;
            segAmp[i] = sign * Random.Range(0.3f, 1f) * curveAmplitude;
        }

        // normalise widths to span exactly one turn, store cumulative ends
        float cum = 0f;
        float scale = 2f * Mathf.PI / total;
        for (int i = 0; i < humps; i++) {
            cum += segEnd[i] * scale;
            segEnd[i] = cum;
        }

        Random.state = prev;
        builtSeed = curveSeed;
        builtWaves = curveWaves;
        builtAmp = curveAmplitude;
    }
}
