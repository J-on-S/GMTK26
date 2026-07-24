using System.Collections.Generic;
using UnityEngine;

/// <summary>Orbits the camera around the cut loop, in the cutting plane.</summary>
/// <remarks>
/// Mental model:
/// - The "cut loop" is the ring of points where the cutting plane slices the mesh
///   (see <c>loopPoints</c>). It is the real cross-section shape, not a perfect circle.
/// - The "centre" is the middle of that ring.
/// - An "angle" is a direction around the ring, measured from the plane's right axis
///   like a compass: 0 deg points along right, 90 deg along forward, and so on.
///   A "direction" is that angle turned into a unit vector in the plane.
/// - <c>PointOnLoopInDirection</c> shoots a ray from the centre along a direction and
///   returns where it meets the ring: angle in, loop point out.
/// - The camera walks its <c>angle</c> around the ring each frame and sits over the
///   matching loop point (or a plain circle of radius <c>scale</c> in Circle mode).
/// </remarks>
[ExecuteAlways]
public class CameraFollow : MonoBehaviour {

    /// <summary>What the camera aims at while orbiting.</summary>
    public enum LookMode {
        /// <summary>Always face the centre of the cut.</summary>
        Center,
        /// <summary>Face the contour point the camera is currently over.</summary>
        Loop,
    }

    /// <summary>Path the camera travels while orbiting.</summary>
    public enum MoveMode {
        /// <summary>Perfect circle around the centre at radius <c>scale</c>.</summary>
        Circle,
        /// <summary>Follow the loop's own shape, pushed <c>scale</c> outward from the centre.</summary>
        ScaleLoop,
    }

    /// <summary>Which loop from the guide the camera orbits.</summary>
    public enum LoopSource {
        /// <summary>Raw flat cross-section.</summary>
        Flat,
        /// <summary>Curved, surface-snapped guide loop.</summary>
        Curved,
    }

    [Tooltip("Supplies the cut loop and cutting-plane axes the camera orbits around.")]
    public LoopGuideBuilder loopGuide;

    [Tooltip("Orbit the raw flat cut, or the curved (surface-snapped) guide loop.")]
    public LoopSource loopSource = LoopSource.Flat;

    [Tooltip("Orbit radius from the centre, in world units.")]
    public float scale = 1f;

    [Tooltip("Lift above the cutting plane along its normal, in world units. Raises the camera off the plane so it views the cut at an angle instead of edge-on -- stops the near plane clipping into the skin and makes the loop readable.")]
    public float height = 0.5f;

    [Tooltip("Also drive rotation (aim + roll). Off = orbit position only, leaving the object's rotation untouched -- lets any GameObject follow the loop without being turned into a look-at camera.")]
    public bool controlRotation = true;

    [Tooltip("Also drive POSITION (orbit). Off = leave transform.position for another script to set (e.g. a follower that snaps to the surface), while this still computes BasePosition and can drive rotation. Default on so ordinary cameras orbit as before.")]
    public bool controlPosition = true;

    [Header("Roll")]
    [Tooltip("Constant bank (roll) of the camera about its view axis, in degrees.")]
    public float rollDegrees = 0f;

    [Tooltip("Peak extra roll added on top of the constant bank, in degrees.")]
    public float rollAmplitude = 0f;

    [Tooltip("Roll oscillation speed, in radians per second. Keep low so the bank is readable.")]
    public float rollSpeed = 0.5f;

    [Header("Off-centre pivot")]
    [Tooltip("Route the pivot into the camera POSITION: the camera orbits the off-centre/wandering pivot, so the loop swings across the frame and its distance varies.")]
    public bool pivotAffectsPosition = true;

    [Tooltip("Route the pivot into the camera LOOK: the aim point shifts off the loop centre, so the loop drifts in the frame without the camera moving. Enable both to combine.")]
    public bool pivotAffectsLook = false;

    [Tooltip("Static pivot offset from the loop centre, in plane units (X = plane right, Y = plane forward). Small values keep the loop in view; off-centre makes the tracked target sweep in and out as the camera orbits.")]
    public Vector2 pivotOffset = Vector2.zero;

    [Tooltip("Slowly wander the pivot on a readable Lissajous path so the target motion is learnable, not jittery.")]
    public bool pivotMoves = false;

    [Tooltip("How far the wandering pivot strays from its base offset, in plane units.")]
    public float pivotMoveRadius = 0.2f;

    [Tooltip("Wander speed, in radians per second. Keep low so the path stays readable.")]
    public float pivotMoveSpeed = 0.5f;

    [Tooltip("What the camera aims at while orbiting.")]
    public LookMode lookMode = LookMode.Center;

    [Tooltip("Orbit path: a perfect circle, or the loop's own shape offset outward.")]
    public MoveMode moveMode = MoveMode.Circle;

    [Tooltip("Orbit rotation speed, in degrees per second.")]
    public float rotationSpeed = 30f;

    [Tooltip("How fast the camera eases toward the target rotation (higher = snappier).")]
    public float lookSpeed = 5f;

    [Tooltip("Roll the camera so the loop's travel direction points to the top of the screen.")]
    public bool loopTowardTop = false;

    [Tooltip("Fixed head start around the ring, in degrees. Shifts where the orbit sits (and its Progress) without changing the speed.")]
    public float angleOffset = 0f;

    [Tooltip("Where the orbit begins around the ring, in degrees. Applied on enable and previewed live in edit mode so you can place the follower before pressing play.")]
    public float startAngle = 0f;

    [Tooltip("Fixed extra position offset in world space, added on top of the orbit.")]
    public Vector3 positionOffset = Vector3.zero;

    /// <summary>Current angle around the ring, in degrees (advances every frame at <c>rotationSpeed</c>).</summary>
    private float angle;

    /// <summary>Live orbit angle around the ring, in degrees. Readable/settable so followers can slave to it.</summary>
    public float Angle { get => angle; set => angle = value; }

    private void OnEnable() {
        angle = startAngle;
    }

    /// <summary>How far around the ring the orbit currently sits, 0..1 (one full turn = 1). Includes <c>angleOffset</c>.</summary>
    public float Progress => Mathf.Repeat(angle + angleOffset, 360f) / 360f;

    /// <summary>The orbit target for this frame BEFORE <c>positionOffset</c> is applied (raw on-loop position, height included).</summary>
    public Vector3 BasePosition { get; private set; }

    private void Update() {
        if (loopGuide == null) {
            return;
        }

        bool got = loopSource == LoopSource.Curved
            ? loopGuide.TryGetCurvedLoop(out Vector3 center, out List<Vector3> loopPoints)
            : loopGuide.TryGetFlatLoop(out center, out loopPoints);
        if (!got) {
            return;
        }

        bool playing = Application.isPlaying;

        // route the pivot independently into position and/or look; each falls back to the
        // raw loop centre when its toggle is off.
        Vector3 pivot = GetPivot(center);
        Vector3 movePivot = pivotAffectsPosition ? pivot : center;
        Vector3 lookPivot = pivotAffectsLook ? pivot : center;

        // walk the angle around the ring, then turn it into a direction vector in the plane.
        // angleOffset shifts the whole orbit (and Progress) by a fixed head start.
        // Edit mode holds the angle at startAngle so the follower previews where it will begin.
        if (playing) {
            angle += rotationSpeed * Time.deltaTime;
        } else {
            angle = startAngle;
        }
        float rad = (angle + angleOffset) * Mathf.Deg2Rad;
        Vector3 orbitDir = loopGuide.PlaneRight * Mathf.Cos(rad) + loopGuide.PlaneForward * Mathf.Sin(rad);

        // POSITION: orbit the move pivot. Circle: fixed radius around it. ScaleLoop: follow
        // the loop's own shape, pushed 'scale' outward from it.
        Vector3 moveLoopPoint = PointOnLoopInDirection(movePivot, orbitDir, loopPoints);
        Vector3 targetPos = moveMode == MoveMode.Circle
            ? movePivot + orbitDir * scale
            : moveLoopPoint + (moveLoopPoint - movePivot).normalized * scale;

        // lift off the cutting plane along its normal so the camera views the cut at an
        // angle, not edge-on: stops the near plane clipping the skin and gives the loop depth.
        targetPos += loopGuide.PlaneNormal * height;

        // orbit target before the world offset, exposed for anything that needs the raw
        // on-loop position (not the shifted camera position).
        BasePosition = targetPos;

        // fixed extra offset in world space.
        targetPos += positionOffset;

        // ease toward the target while playing; snap straight to it in edit mode so the
        // preview tracks startAngle without a running deltaTime. Skipped when controlPosition
        // is off: BasePosition is still published above, but transform.position is left for
        // another script (e.g. a surface-snapping follower) to own.
        if (controlPosition) {
            transform.position = playing
                ? Vector3.Lerp(transform.position, targetPos, lookSpeed * Time.deltaTime)
                : targetPos;
        }

        // ROTATION is optional: when off, the object just orbits (position only), keeping
        // whatever rotation it already has. Useful for followers that aren't cameras.
        if (controlRotation) {
            // LOOK: aim at the look pivot's centre, or the loop point in the orbit direction.
            Vector3 lookTarget = lookMode == LookMode.Center
                ? lookPivot
                : PointOnLoopInDirection(lookPivot, orbitDir, loopPoints);

            // default up is the plane normal; loopTowardTop uses the orbit tangent (travel
            // direction) so the loop appears to move toward the top of the screen.
            Vector3 up = loopTowardTop
                ? -loopGuide.PlaneRight * Mathf.Sin(rad) + loopGuide.PlaneForward * Mathf.Cos(rad)
                : loopGuide.PlaneNormal;

            Vector3 toTarget = lookTarget - transform.position;
            if (toTarget.sqrMagnitude > 1e-8f) {
                // bank the up vector about the view axis so the horizon rolls; constant
                // rollDegrees plus a slow readable oscillation.
                float roll = rollDegrees + rollAmplitude * Mathf.Sin(Time.time * rollSpeed);
                if (roll != 0f) {
                    up = Quaternion.AngleAxis(roll, toTarget.normalized) * up;
                }

                Quaternion targetRot = Quaternion.LookRotation(toTarget, up);
                transform.rotation = playing
                    ? Quaternion.Slerp(transform.rotation, targetRot, lookSpeed * Time.deltaTime)
                    : targetRot;
            }
        }

        // random drift is play-only jitter; keep the edit-mode preview steady. Skipped when
        // this object doesn't own its position.
        if (playing && controlPosition) {
            ApplyRandomPerpendicularPosition();
        }
    }

    /// <summary>The point the camera orbits: loop centre plus the (optionally wandering) pivot offset, laid out in the cutting plane.</summary>
    /// <remarks>Invariant: the wander is a slow two-frequency Lissajous, so the target motion is learnable rather than jittery.</remarks>
    private Vector3 GetPivot(Vector3 center) {
        float ox = pivotOffset.x;
        float oz = pivotOffset.y;

        if (pivotMoves) {
            float t = Time.time * pivotMoveSpeed;
            // different frequencies on each axis trace a readable figure, not a circle
            ox += Mathf.Sin(t) * pivotMoveRadius;
            oz += Mathf.Cos(t * 0.7f) * pivotMoveRadius;
        }

        return center + loopGuide.PlaneRight * ox + loopGuide.PlaneForward * oz;
    }

    [Tooltip("Largest sideways drift offset from the orbit position, in world units.")]
    public float maxHorizontalDerive = 1f;

    [Tooltip("How many times per second a new random drift target is rolled.")]
    public float DerivePerSecondEvaluate = 4;

    [Tooltip("How fast the drift eases toward its current target (higher = snappier).")]
    public float DeriveSpeed = 0.1f;

    [Tooltip("Bias of the random drift toward the centre. 1 = uniform; higher = more likely to stay near the middle.")]
    public float deriveCenterBias = 3f;

    /// <summary>Current eased drift offset along the camera's horizontal axis.</summary>
    private float currentDerive;

    /// <summary>Drift offset the camera is easing toward until the next roll.</summary>
    private float targetDerive;

    /// <summary>Seconds since the last drift target was rolled.</summary>
    private float deriveTimer;

    /// <summary>Nudges the camera sideways toward a periodically re-rolled random offset.</summary>
    private void ApplyRandomPerpendicularPosition() {
        deriveTimer += Time.deltaTime;

        float interval = DerivePerSecondEvaluate > 0f ? 1f / DerivePerSecondEvaluate : float.MaxValue;
        if (deriveTimer >= interval) {
            deriveTimer -= interval;

            // uniform sample pushed toward 0 by a power curve, keeping its sign, so
            // small offsets are far more likely than offsets near the edge
            float u = Random.Range(-1f, 1f);
            float biased = Mathf.Sign(u) * Mathf.Pow(Mathf.Abs(u), deriveCenterBias);
            targetDerive = biased * maxHorizontalDerive;
        }

        currentDerive = Mathf.Lerp(currentDerive, targetDerive, DeriveSpeed * Time.deltaTime);
        transform.position += transform.right * currentDerive;
    }

    /// <summary>Shoots a ray from <paramref name="center"/> along <paramref name="direction"/> and returns where it meets the loop, interpolated along the crossed edge. Angle in (as a direction), loop point out.</summary>
    /// <returns>The crossing point; <paramref name="center"/> when the ray hits no edge.</returns>
    /// <remarks>Invariant: moves continuously as <paramref name="direction"/> rotates, so no vertex-to-vertex jumps.</remarks>
    private Vector3 PointOnLoopInDirection(Vector3 center, Vector3 direction, List<Vector3> loopPoints) {
        Vector3 right = loopGuide.PlaneRight;
        Vector3 forward = loopGuide.PlaneForward;

        // the ray in the plane's 2D (right, forward) basis, origin at the centre
        Vector2 ray = new(Vector3.Dot(direction, right), Vector3.Dot(direction, forward));
        if (ray.sqrMagnitude < 1e-8f) {
            return center;
        }
        ray.Normalize();

        int n = loopPoints.Count;
        Vector3 hit = center;
        float bestS = -1f;

        for (int i = 0; i < n; i++) {
            Vector3 pa = loopPoints[i];
            Vector3 pb = loopPoints[(i + 1) % n];

            Vector2 a = To2D(pa - center, right, forward);
            Vector2 e = To2D(pb - pa, right, forward);

            // solve s*ray = a + t*e for ray param s >= 0 and edge param t in [0,1]
            float denom = -ray.x * e.y + e.x * ray.y;
            if (Mathf.Abs(denom) < 1e-8f) {
                continue;
            }

            float s = (-a.x * e.y + e.x * a.y) / denom;
            float t = (ray.x * a.y - a.x * ray.y) / denom;

            // keep the farthest crossing so concave loops give the outer boundary
            if (s > 0f && t >= 0f && t <= 1f && s > bestS) {
                bestS = s;
                hit = Vector3.Lerp(pa, pb, t);
            }
        }

        return bestS > 0f ? hit : center;
    }

    /// <summary>Projects a plane-lying vector into the 2D <c>(right, forward)</c> basis.</summary>
    private static Vector2 To2D(Vector3 v, Vector3 right, Vector3 forward) {
        return new(Vector3.Dot(v, right), Vector3.Dot(v, forward));
    }
}
