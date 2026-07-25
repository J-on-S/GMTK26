using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Moves the follower left/right along the limb, snaps it onto the mesh surface, scores it, and draws its gizmos. The orbit angle around the loop is driven externally (by <see cref="CuttingManager"/>); this script does not sync it.</summary>
/// <remarks>Invariant: runs before its own <c>CameraFollow</c>, so a snap uses the same frame's orbit angle.</remarks>
[ExecuteAlways]
[RequireComponent(typeof(CameraFollow))]
[DefaultExecutionOrder(-10)]
public class LoopFollowingObject : MonoBehaviour
{
    /// <summary>Supplies the cut loop, its centre, and surface projection.</summary>
    public LoopGuideBuilder builder;

    /// <summary>Which way mouse-x slides the object across the loop.</summary>
    public enum SlideAxis {
        /// <summary>Along the plane normal (up/down the limb axis).</summary>
        PlaneNormal,
        /// <summary>Sweep the sample along the limb over a sphere of the loop radius, instead of a straight axial slide.</summary>
        RadialPerpendicular,
    }

    [Tooltip("RadialPerpendicular only: largest sweep angle from the loop, in degrees. Kept under 90 so the sample can't cross the pole (top of the limb) and wrap onto the far side.")]
    public float radialMaxAngle = 85f;

    /// <summary>What drives left/right travel along the limb.</summary>
    public enum MoveInput {
        /// <summary>Horizontal mouse motion; speed scaled per-pixel by <c>Xspeed</c>.</summary>
        MouseDelta,
        /// <summary>Hold left click = left, right click = right; speed scaled per-second by <c>Xspeed</c>.</summary>
        MouseButtons,
        /// <summary>Left/right arrow keys; speed scaled per-second by <c>Xspeed</c>.</summary>
        ArrowKeys,
    }

    /// <summary>Serialized tuning bundle: input mode, slide axis, speeds, and smoothing.</summary>
    public FollowLoopPresets preset;

    /// <summary>Left/right arrow-and-A/D axis driving along-limb travel.</summary>

    /// <summary>This object's own <c>CameraFollow</c>, supplying <c>BasePosition</c> and aim while this script owns the surface-snapped position. Its orbit angle is driven externally by <see cref="CuttingManager"/>.</summary>
    public CameraFollow owned;

    /// <summary>Along-limb travel, in world units for <c>PlaneNormal</c> mode or degrees for <c>RadialPerpendicular</c>.</summary>
    private float offset;

    /// <summary>Distance the object floats above the surface along the smoothed normal, in world units.</summary>
    public float ObjectHover = 0.01f;

    [Header("Trace")]
    [Tooltip("Draw the path the scalpel walks over the surface into traceRenderer.")]
    public bool drawTrace = false;

    public LineRenderer traceRenderer;

    [Tooltip("Line width, in world units.")]
    public float traceWidth = 0.005f;

    [Tooltip("Min world distance between stored points; skips near-duplicates so the list stays small.")]
    public float traceMinStep = 0.005f;

    private readonly List<Vector3> tracePoints = new List<Vector3>();

    /// <summary>Most recent surface hit with hover applied; held through frames the ray misses.</summary>
    private Vector3 lastSurfacePos;

    /// <summary>Whether <c>lastSurfacePos</c> holds a real hit yet.</summary>
    private bool hasSurface;

    /// <summary>Low-passed surface normal for the hover lift, so it doesn't step between triangles.</summary>
    private Vector3 smoothedNormal;

    /// <summary>Whether <c>smoothedNormal</c> has been seeded.</summary>
    private bool hasNormal;

    /// <summary>Latest raw surface hit before hover, shared with the precision gizmo.</summary>
    private Vector3 onMeshPos;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;

        owned = GetComponent<CameraFollow>();

        ApplyTraceWidth();
    }

    void OnValidate()
    {
        ApplyTraceWidth();
    }

    void Update()
    {
        // Input runs only in play mode.
        if (!Application.isPlaying) return;

        // Accumulate left/right travel: MouseDelta is per-pixel, the held modes per-second.
        updateOffset(preset.moveInput);

        // In radial mode offset is degrees; clamp so it can't cross the pole and wrap round the back.
        if (preset.slideAxis == SlideAxis.RadialPerpendicular) {
            offset = Mathf.Clamp(offset, -radialMaxAngle, radialMaxAngle);
        }
    }

    /// <summary>Score against the flat cut loop; off = the curved guide.</summary>
    public bool useFlatCurve = true;

    /// <summary>Closest point on the target loop to <c>onMeshPos</c>, drawn by the precision gizmo.</summary>
    private Vector3 expected;

    /// <summary>Logs how far the snapped object sits from the target loop and records the nearest loop point for the gizmo.</summary>
    void calculatePrecision()
    {
        bool result;
        List<Vector3> points;
        if (useFlatCurve) {
            result = builder.TryGetFlatLoop(out Vector3 center, out points);
        } else {
            result = builder.TryGetCurvedLoop(out Vector3 center, out points);
        }
        if (!result) return;

        expected = LoopScorer.ClosestPointOnPolyline(points, onMeshPos, out float t, out float dst);
        //Debug.Log(dst.ToString("0.000"));
    }

    void OnDrawGizmos()
    {
        if (owned == null) return;

        Color c = Gizmos.color;
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(expected, 0.01f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(owned.BasePosition, 0.01f);
        Gizmos.color = c;
    }

    /// <summary>Snaps the object onto the mesh surface each frame, after its orbit position is known.</summary>
    void LateUpdate()
    {
        if (!Application.isPlaying || builder == null || owned == null) return;

        // Both modes travel along the limb; they differ in how the surface point is found.
        Vector3 rawPos;   // free-space sample the ray starts from
        Vector3 projDir;  // outward direction to project along (-projDir shoots into the surface)

        if (preset.slideAxis == SlideAxis.PlaneNormal) {
            // Slide along the limb axis, then project radially inward.
            rawPos = owned.BasePosition + builder.PlaneNormal * offset;
            projDir = (rawPos - builder.LoopCenter).normalized;
        } else {
            // In-plane radial: drop any lift along the plane normal so the sweep starts on the loop.
            Vector3 fromCenter = owned.BasePosition - builder.LoopCenter;
            Vector3 radialIn = fromCenter - builder.PlaneNormal * Vector3.Dot(fromCenter, builder.PlaneNormal);

            // Rotate the in-plane radial about the tangent by offset degrees.
            Vector3 axis = Vector3.Cross(builder.PlaneNormal, radialIn).normalized;
            projDir = (Quaternion.AngleAxis(-offset, axis) * radialIn).normalized;
            rawPos = builder.LoopCenter + projDir * radialIn.magnitude;
        }

        // Ray search length from rawPos's own distance to the centre, so a large offset still
        // reaches the surface.
        float reach = (rawPos - builder.LoopCenter).magnitude;

        if (builder.TryProjectOntoSurface(rawPos, projDir, 1.3f * reach, out onMeshPos, out Vector3 onMeshNormal)) {
            // Low-pass the flat per-triangle collider normal so the hover lift doesn't step.
            smoothedNormal = hasNormal
                ? Vector3.Slerp(smoothedNormal, onMeshNormal, 1f - Mathf.Exp(-preset.normalSmooth * Time.deltaTime))
                : onMeshNormal;
            hasNormal = true;

            lastSurfacePos = onMeshPos + smoothedNormal * ObjectHover;
            hasSurface = true;
        }
        // Miss: keep lastSurfacePos instead of snapping out to free space.

        if (!hasSurface) return;

        // Ease onto the target; the exp factor keeps it framerate-independent.
        owned.transform.position = preset.followSmooth > 0f
            ? Vector3.Lerp(owned.transform.position, lastSurfacePos, 1f - Mathf.Exp(-preset.followSmooth * Time.deltaTime))
            : lastSurfacePos;

        // Trail the surface point the object sits on.
        if (drawTrace && traceRenderer != null) AddTracePoint(owned.transform.position);
        calculatePrecision();
    }

    /// <summary>Appends a surface point to the trail, skipping near-duplicates.</summary>
    void AddTracePoint(Vector3 p)
    {
        int n = tracePoints.Count;
        if (n > 0 && (p - tracePoints[n - 1]).sqrMagnitude < traceMinStep * traceMinStep) return;

        tracePoints.Add(p);
        traceRenderer.positionCount = tracePoints.Count;
        traceRenderer.SetPositions(tracePoints.ToArray());
    }

    void ApplyTraceWidth()
    {
        if (traceRenderer != null) traceRenderer.widthCurve = AnimationCurve.Constant(0, 1, traceWidth);
    }

    /// <summary>Drops the trail and the along-limb offset, so a fresh run starts from a clean surface and a centred scalpel.</summary>
    [ContextMenu("reset trace points")]
    public void ResetTrace()
    {
        tracePoints.Clear();
        if (traceRenderer != null) traceRenderer.positionCount = 0;

        offset = 0f;
        hasSurface = false;
        hasNormal = false;
    }

    /// <summary>Accumulates along-limb travel into <c>offset</c> from the active input.</summary>
    private void updateOffset(MoveInput moveInput)
    {
        bool hasMouse = Mouse.current != null;
        switch (moveInput) {
            case MoveInput.MouseDelta:
                if (CuttingManager.mouseDelta != null)
                    offset -= CuttingManager.mouseDelta.ReadValue<Vector2>().x * preset.Xspeed;
                break;
            case MoveInput.MouseButtons: {
                if (!hasMouse) break;
                float dir = (Mouse.current.rightButton.isPressed ? 1f : 0f)
                          - (Mouse.current.leftButton.isPressed ? 1f : 0f);
                offset -= dir * preset.Xspeed * Time.deltaTime;
                break;
            }
            case MoveInput.ArrowKeys:
                offset -= CuttingManager.arrows.ReadValue<Vector2>().x * preset.Xspeed * Time.deltaTime;
                break;
        }
    }
}
