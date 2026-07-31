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
/// - Distances (<c>scale</c>, <c>height</c>, the pivot, the drift, <c>positionOffset</c>) are
///   read in whatever <c>framingUnits</c> says: world units, or multiples of the ring's own
///   radius. The second is what lets one framing serve cuts of very different sizes, since a
///   wrist and a thigh want the same picture, not the same number of metres.
/// </remarks>

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

    /// <summary>Where the camera's up vector comes from, i.e. what ends up at the top of the screen.</summary>
    public enum UpMode {
        /// <summary>The cutting plane's normal, sign and all. What the camera did before this setting existed; an author who rotated the plane the other way gets an upside-down view.</summary>
        PlaneNormal,
        /// <summary>The plane normal, flipped when it would put the world's floor above the horizon. Same framing as <see cref="PlaneNormal"/> for a plane authored the right way up.</summary>
        PlaneNormalUpright,
        /// <summary>World up, so the horizon stays level however the plane is rotated. Falls back toward the upright plane normal as the view turns vertical and world up stops being usable.</summary>
        WorldUp,
    }

    /// <summary>What the framing's distances are measured in.</summary>
    public enum FramingUnits {
        /// <summary>World units, exactly as authored. What the follow did before this setting existed.</summary>
        World,
        /// <summary>Multiples of this cut's own loop radius, so one framing frames a wrist and a thigh alike and follows a body scaled up or down.</summary>
        LoopRadius,
    }

    /// <summary>Space the fixed position offset is read in.</summary>
    public enum OffsetSpace {
        /// <summary>A fixed direction in the room, whichever way the body is turned. What the follow did before this setting existed.</summary>
        World,
        /// <summary>The cutting plane's own axes (X = plane right, Y = along its normal, Z = plane forward), so the offset follows a client who is moved or turned around.</summary>
        Plane,
    }

    /// <summary>Which loop from the guide the camera orbits.</summary>
    public enum LoopSource {
        /// <summary>Raw flat cross-section.</summary>
        Flat,
        /// <summary>Curved, surface-snapped guide loop.</summary>
        Curved,
    }

    [Tooltip("All the framing below in one asset. Assigned, it overwrites those fields on enable and on validate -- and a CuttingManager swaps it in on entry, which is what lets one CameraFollow serve every cut.")]
    [HideInInspector] public CameraFollowPreset preset;

    [Tooltip("Supplies the cut loop and cutting-plane axes the camera orbits around.")]
    [HideInInspector] public LoopGuideBuilder loopGuide;

    [Tooltip("Orbit the raw flat cut, or the curved (surface-snapped) guide loop.")]
    public LoopSource loopSource = LoopSource.Flat;

    [Tooltip("What every distance below (orbit radius, height, pivot, drift, position offset) is measured in. World = exactly as authored. Loop Radius = multiples of THIS cut's ring, so one framing fits a wrist and a thigh and follows a body scaled up or down.")]
    public FramingUnits framingUnits = FramingUnits.World;

    [Tooltip("Orbit radius from the centre, in the units above (world units, or multiples of the loop radius).")]
    public float scale = 1f;

    [Tooltip("Lift above the cutting plane along its normal, in the units above. Raises the camera off the plane so it views the cut at an angle instead of edge-on -- stops the near plane clipping into the skin and makes the loop readable.")]
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

    [Tooltip("Static pivot offset from the loop centre, along the plane's axes (X = plane right, Y = plane forward), in the framing units above. Small values keep the loop in view; off-centre makes the tracked target sweep in and out as the camera orbits.")]
    public Vector2 pivotOffset = Vector2.zero;

    [Tooltip("Slowly wander the pivot on a readable Lissajous path so the target motion is learnable, not jittery.")]
    public bool pivotMoves = false;

    [Tooltip("How far the wandering pivot strays from its base offset, in the framing units above.")]
    public float pivotMoveRadius = 0.2f;

    [Tooltip("Wander speed, in radians per second. Keep low so the path stays readable.")]
    public float pivotMoveSpeed = 0.5f;

    [Tooltip("What the camera aims at while orbiting.")]
    public LookMode lookMode = LookMode.Center;

    [Tooltip("Orbit path: a perfect circle, or the loop's own shape offset outward.")]
    public MoveMode moveMode = MoveMode.Circle;

    [Tooltip("Orbit rotation speed, in degrees per second.")]
    public float rotationSpeed = 30f;

    [Tooltip("Optional. When set, rotationSpeed is pulled from this each frame (its SignedSpeed) instead of being a fixed value -- lets the speed source own the travel speed while this only orbits. Must implement ISpeedSource. Leave null for a fixed-speed orbit (e.g. the scalpel).")]
    // Serialized as MonoBehaviour, not ISpeedSource: Unity does not serialize bare interface
    // fields, so an ISpeedSource slot silently stays null at runtime however it's dragged.
    [HideInInspector] [SerializeField] private MonoBehaviour speedSourceBehaviour;

    /// <summary>The wired speed source, or null when the slot is empty. Resolved off <c>speedSourceBehaviour</c>.</summary>
    public ISpeedSource speedSource => speedSourceBehaviour as ISpeedSource;

    /// <summary>Wires the speed source in code, for owners like <see cref="CuttingManager"/> that already hold the driver. Pass null for a fixed-speed orbit.</summary>
    public void SetSpeedSource(ISpeedSource source) {
        if (source == null) {
            speedSourceBehaviour = null;
            return;
        }

        if (source is not MonoBehaviour behaviour) {
            Debug.LogError($"Speed source {source.GetType().Name} is not a MonoBehaviour, so it can't be stored on {name}.", this);
            return;
        }

        speedSourceBehaviour = behaviour;
    }



    [Tooltip("How fast the camera eases toward the target rotation (higher = snappier).")]
    public float lookSpeed = 5f;

    [Tooltip("How fast the camera eases toward the target POSITION (higher = snappier). Separate from lookSpeed so aim snappiness and travel smoothing can be tuned apart.")]
    public float moveSpeed = 5f;

    [Tooltip("Roll the camera so the loop's travel direction points to the top of the screen.")]
    public bool loopTowardTop = false;

    [Tooltip("What ends up at the top of the screen. The cutting plane's normal has whatever sign the plane was authored with, so Plane Normal alone can hand back an upside-down view (floor at the top) with the aim still perfectly on the loop. Ignored while Loop Toward Top is on, which owns the up vector itself.")]
    public UpMode upMode = UpMode.PlaneNormalUpright;

    [Tooltip("Fixed head start around the ring, in degrees. Shifts where the orbit sits (and its Progress) without changing the speed.")]
    public float angleOffset = 0f;

    [Tooltip("Where the orbit begins around the ring, in degrees. Applied on enable and previewed live in edit mode so you can place the follower before pressing play.")]
    [HideInInspector] public float startAngle = 0f;

    [Tooltip("Space the offset below is read in. Plane follows the cutting plane (X = plane right, Y = along its normal, Z = plane forward), so it survives a body that is moved or turned around; World is a fixed direction in the room.")]
    public OffsetSpace offsetSpace = OffsetSpace.World;

    [Tooltip("Fixed extra position offset added on top of the orbit, in the space and units above.")]
    public Vector3 positionOffset = Vector3.zero;

    /// <summary>Current angle around the ring, in degrees (advances every frame at <c>rotationSpeed</c>).</summary>
    private float angle;

    /// <summary>What every authored distance is multiplied by before it is used: 1 in world units, this cut's loop radius in Loop Radius units.</summary>
    /// <remarks>
    /// Read live rather than baked into the fields on entry, and deliberately so: the follow is shared
    /// between cuts and re-applies its preset on enable and on validate, so anything written onto the
    /// fields would be overwritten by the next <see cref="ApplyPreset"/> -- and applying it twice would
    /// square it. Kept as a factor here, it cannot compound and it tracks a body that is rescaled while
    /// the cut is on screen.
    /// <para>Falls back to 1 when there is no ring yet (no guide, or the plane misses the mesh): the
    /// authored numbers are a usable framing, a factor of zero is a camera inside the body.</para>
    /// </remarks>
    public float FramingScale {
        get {
            if (framingUnits == FramingUnits.World || loopGuide == null) {
                return 1f;
            }

            float radius = loopGuide.FlatLoopRadius;
            return radius > 1e-5f ? radius : 1f;
        }
    }

    /// <summary>The fixed offset as a world-space vector: as authored, or laid out in the cutting plane's own axes, times <see cref="FramingScale"/>.</summary>
    private Vector3 ResolvedPositionOffset(float framingScale) {
        Vector3 offset = positionOffset * framingScale;

        if (offsetSpace == OffsetSpace.World || loopGuide == null) {
            return offset;
        }

        return loopGuide.PlaneRight * offset.x
             + loopGuide.PlaneNormal * offset.y
             + loopGuide.PlaneForward * offset.z;
    }

    /// <summary>Live orbit angle around the ring, in degrees.</summary>
    public float Angle { get => angle; set => angle = value; }

    private void OnEnable() {
        angle = startAngle;
        ApplyPreset();
    }

    /// <summary>Copies the assigned preset's framing onto this component. No-op when no preset is set, so a hand-tuned follow is left alone.</summary>
    /// <remarks>Called on enable, on validate, and by <see cref="CuttingManager"/> when a cut claims the shared follow.</remarks>
    public void ApplyPreset() {
        if (preset != null) {
            preset.ApplyTo(this);
        }
    }

    private void OnValidate() {
        ApplyPreset();

        // reject a behaviour that doesn't implement ISpeedSource, so a wrong drag fails loud
        // instead of silently leaving the orbit on its fixed speed. The field takes any
        // MonoBehaviour (that's what makes it serialize), so this is the only gate.
        if (speedSourceBehaviour != null && speedSourceBehaviour is not ISpeedSource) {
            Debug.LogError($"{speedSourceBehaviour.GetType().Name} does not implement ISpeedSource; cleared the speed source on {name}.", this);
            speedSourceBehaviour = null;
        }
    }

    /// <summary>How far around the ring the orbit currently sits, 0..1 (one full turn = 1). Includes <c>angleOffset</c>.</summary>
    public float Progress => Mathf.Repeat(angle + angleOffset, 360f) / 360f;

    /// <summary>The orbit target for this frame BEFORE <c>positionOffset</c> is applied (raw on-loop position, height included).</summary>
    public Vector3 BasePosition { get; private set; }

    private void Update() {
        Advance(Time.deltaTime);
        ApplyPose(Time.time, Application.isPlaying);
    }

    /// <summary>Walks the orbit angle forward by one step, at the speed source's speed when one is wired.</summary>
    public void Advance(float deltaTime) {
        // pull the live travel speed from the speed source when wired; otherwise keep the fixed value.
        if (speedSource != null) {
            rotationSpeed = speedSource.GetSignedSpeed();
        }
        angle += rotationSpeed * deltaTime;
    }

    /// <summary>Places the follower at <paramref name="degrees"/> without advancing it, for an editor-driven preview.</summary>
    /// <param name="degrees">Orbit angle to sit at.</param>
    /// <param name="clock">Seconds, for the roll oscillation and the pivot wander. Edit mode has no running <c>Time.time</c>, so the driver supplies its own.</param>
    /// <remarks>Never eased: the preview lands exactly on the target pose, so what you see is the framing rather than the chase toward it.</remarks>
    public void PreviewAt(float degrees, float clock) {
        angle = degrees;
        ApplyPose(clock, animated: false);
    }

    /// <summary>The orbit pose at a given angle: where this follower would sit, and how it would aim from there.</summary>
    /// <remarks>Pure -- no side effects, no dependence on when <see cref="Update"/> last ran -- so anything that wants to fly to the orbit (the camera travel in <see cref="CuttingManager"/>) can ask for the destination up front and land on it exactly.</remarks>
    /// <param name="atAngle">Angle around the ring, in degrees, before <c>angleOffset</c>.</param>
    /// <param name="position">The orbit position, <c>positionOffset</c> already applied.</param>
    /// <param name="rotation">The aim, measured from <paramref name="viewFrom"/>.</param>
    /// <param name="viewFrom">Where the aim is measured from. Defaults to <paramref name="position"/> itself; the live orbit passes its own current position so the look stays correct while it eases in.</param>
    /// <param name="clock">Seconds driving the roll oscillation and the pivot wander. Defaults to <c>Time.time</c>; the editor preview passes its own, since edit mode has no running clock.</param>
    /// <returns><c>false</c> when there is no guide or the plane misses the mesh; the outputs are then left at the current transform.</returns>
    public bool TryGetPose(float atAngle, out Vector3 position, out Quaternion rotation, Vector3? viewFrom = null, float? clock = null) {
        position = transform.position;
        rotation = transform.rotation;

        if (loopGuide == null) {
            return false;
        }

        bool got = loopSource == LoopSource.Curved
            ? loopGuide.TryGetCurvedLoop(out Vector3 center, out List<Vector3> loopPoints)
            : loopGuide.TryGetFlatLoop(out center, out loopPoints);
        if (!got) {
            return false;
        }

        float t = clock ?? Time.time;

        // every authored distance goes through this one factor: 1 in world units, the ring's own
        // radius when the framing is authored as multiples of it.
        float k = FramingScale;

        // route the pivot independently into position and/or look; each falls back to the
        // raw loop centre when its toggle is off.
        Vector3 pivot = GetPivot(center, t, k);
        Vector3 movePivot = pivotAffectsPosition ? pivot : center;
        Vector3 lookPivot = pivotAffectsLook ? pivot : center;

        float rad = (atAngle + angleOffset) * Mathf.Deg2Rad;
        Vector3 orbitDir = loopGuide.PlaneRight * Mathf.Cos(rad) + loopGuide.PlaneForward * Mathf.Sin(rad);

        // POSITION: orbit the move pivot. Circle: fixed radius around it. ScaleLoop: follow
        // the loop's own shape, pushed 'scale' outward from it.
        Vector3 moveLoopPoint = PointOnLoopInDirection(movePivot, orbitDir, loopPoints);
        position = moveMode == MoveMode.Circle
            ? movePivot + orbitDir * (scale * k)
            : moveLoopPoint + (moveLoopPoint - movePivot).normalized * (scale * k);

        // lift off the cutting plane along its normal so the camera views the cut at an
        // angle, not edge-on: stops the near plane clipping the skin and gives the loop depth.
        position += loopGuide.PlaneNormal * (height * k);

        // fixed extra offset, in the room's axes or the cutting plane's own.
        position += ResolvedPositionOffset(k);

        // LOOK: aim at the look pivot's centre, or the loop point in the orbit direction.
        Vector3 lookTarget = lookMode == LookMode.Center
            ? lookPivot
            : PointOnLoopInDirection(lookPivot, orbitDir, loopPoints);

        // default up is the plane normal; loopTowardTop uses the orbit tangent (travel
        // direction) so the loop appears to move toward the top of the screen.
        Vector3 up = loopTowardTop
            ? -loopGuide.PlaneRight * Mathf.Sin(rad) + loopGuide.PlaneForward * Mathf.Cos(rad)
            : loopGuide.PlaneNormal;

        Vector3 toTarget = lookTarget - (viewFrom ?? position);
        if (toTarget.sqrMagnitude > 1e-8f) {
            Vector3 view = toTarget.normalized;

            // the plane normal's sign is authoring, not geometry, so on its own it can put the
            // floor at the top of the screen with the aim still correct. loopTowardTop is exempt:
            // its up IS the travel direction, and it is meant to turn the whole way round.
            if (!loopTowardTop) {
                up = ResolveUp(up, view);
            }

            // bank the up vector about the view axis so the horizon rolls; constant
            // rollDegrees plus a slow readable oscillation. After the up is resolved, so a
            // deliberate bank is measured from a level horizon rather than fighting a flip.
            float roll = rollDegrees + rollAmplitude * Mathf.Sin(t * rollSpeed);
            if (roll != 0f) {
                up = Quaternion.AngleAxis(roll, view) * up;
            }

            rotation = Quaternion.LookRotation(toTarget, up);
        }

        return true;
    }

    /// <summary>Turns the raw plane normal into the up vector the aim actually uses, per <see cref="upMode"/>.</summary>
    /// <param name="planeNormal">Up as the geometry gives it, before any world alignment.</param>
    /// <param name="view">Unit view direction the rotation will be built around.</param>
    /// <remarks>Pure and a function of this frame's pose alone -- no remembered sign -- so <see cref="TryGetPose"/> stays something the travel in <see cref="CuttingManager"/> can ask for a destination pose up front.</remarks>
    private Vector3 ResolveUp(Vector3 planeNormal, Vector3 view) {
        switch (upMode) {
            case UpMode.PlaneNormalUpright:
                return Upright(planeNormal, view);

            case UpMode.WorldUp: {
                // world up is useless once the view is vertical (it collapses onto the view axis),
                // which is exactly where a big height puts the camera. Fade to the upright plane
                // normal before that happens, rather than snapping at the degenerate pose.
                float verticality = Mathf.Abs(Vector3.Dot(view, Vector3.up));
                float blend = Mathf.InverseLerp(0.97f, 0.999f, verticality);
                return blend <= 0f
                    ? Vector3.up
                    : Vector3.Slerp(Vector3.up, Upright(planeNormal, view), blend);
            }

            default:
                return planeNormal;
        }
    }

    /// <summary>Flips an up vector when it would put the world's floor above the horizon.</summary>
    /// <remarks>Tests the vector <c>LookRotation</c> will actually use -- the part of <paramref name="up"/> across the view axis -- because a normal can point below the horizon and still come out upright on screen once the view direction is taken out of it.</remarks>
    private static Vector3 Upright(Vector3 up, Vector3 view) {
        Vector3 onScreen = up - view * Vector3.Dot(up, view);
        return Vector3.Dot(onScreen, Vector3.up) < 0f ? -up : up;
    }

    /// <summary>Puts the follower where its current <c>angle</c> says it should be.</summary>
    /// <param name="clock">Seconds driving the roll oscillation and the pivot wander.</param>
    /// <param name="animated">Ease toward the pose and apply the random drift, rather than snapping exactly onto it.</param>
    public void ApplyPose(float clock, bool animated) {
        // aim measured from where this actually is, not from the orbit target, so the look
        // stays correct while the position is still easing toward it.
        if (!TryGetPose(angle, out Vector3 targetPos, out Quaternion targetRot, transform.position, clock)) {
            return;
        }

        // orbit target before the fixed offset, exposed for anything that needs the raw
        // on-loop position (not the shifted camera position). Subtracts the RESOLVED offset,
        // the same vector TryGetPose added: the authored one is in plane axes and scaled units
        // when the framing says so, so taking it away raw would leave a residue.
        BasePosition = targetPos - ResolvedPositionOffset(FramingScale);

        // random sideways drift, play-only jitter. Folded into the TARGET, not added to the
        // eased result: adding it afterwards made each frame's drift the starting point for the
        // next ease, so it accumulated into a runaway offset of currentDerive / (moveSpeed * dt)
        // -- about 12x at 60fps and 29x at 144fps, i.e. framerate-dependent too.
        if (animated && controlPosition) {
            // transform.right is last frame's, since rotation is set below. One frame of lag on
            // a drift that already eases over seconds is not visible.
            targetPos += transform.right * UpdateDerive();
        }

        // ease toward the target while animated; snap straight onto it otherwise, so the editor
        // preview shows the framing rather than the chase toward it. Skipped when controlPosition
        // is off: BasePosition is still published above, but transform.position is left for
        // another script (e.g. a surface-snapping follower) to own.
        if (controlPosition) {
            transform.position = animated
                ? Vector3.Lerp(transform.position, targetPos, moveSpeed * Time.deltaTime)
                : targetPos;
        }

        // ROTATION is optional: when off, the object just orbits (position only), keeping
        // whatever rotation it already has. Useful for followers that aren't cameras.
        if (controlRotation) {
            transform.rotation = animated
                ? Quaternion.Slerp(transform.rotation, targetRot, lookSpeed * Time.deltaTime)
                : targetRot;
        }
    }

    /// <summary>The point the camera orbits: loop centre plus the (optionally wandering) pivot offset, laid out in the cutting plane.</summary>
    /// <remarks>Invariant: the wander is a slow two-frequency Lissajous, so the target motion is learnable rather than jittery.</remarks>
    /// <param name="framingScale">What the authored offsets are multiplied by, so a pivot stays the same fraction of the ring on a cut of any size.</param>
    private Vector3 GetPivot(Vector3 center, float clock, float framingScale) {
        float ox = pivotOffset.x * framingScale;
        float oz = pivotOffset.y * framingScale;

        if (pivotMoves) {
            float t = clock * pivotMoveSpeed;
            // different frequencies on each axis trace a readable figure, not a circle
            ox += Mathf.Sin(t) * pivotMoveRadius * framingScale;
            oz += Mathf.Cos(t * 0.7f) * pivotMoveRadius * framingScale;
        }

        return center + loopGuide.PlaneRight * ox + loopGuide.PlaneForward * oz;
    }

    [Tooltip("Largest sideways drift offset from the orbit position, in the framing units above.")]
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

    /// <summary>Advances the random sideways drift and returns how far off the orbit it currently sits, in world units.</summary>
    /// <remarks>
    /// Returns the offset rather than writing it: the caller folds it into the orbit target so it
    /// stays an offset. Applied to <c>transform.position</c> after the ease instead, it would feed
    /// back into the next frame's ease and grow without bound.
    /// </remarks>
    private float UpdateDerive() {
        deriveTimer += Time.deltaTime;

        float interval = DerivePerSecondEvaluate > 0f ? 1f / DerivePerSecondEvaluate : float.MaxValue;
        if (deriveTimer >= interval) {
            deriveTimer -= interval;

            // uniform sample pushed toward 0 by a power curve, keeping its sign, so
            // small offsets are far more likely than offsets near the edge
            float u = Random.Range(-1f, 1f);
            float biased = Mathf.Sign(u) * Mathf.Pow(Mathf.Abs(u), deriveCenterBias);
            targetDerive = biased * maxHorizontalDerive * FramingScale;
        }

        currentDerive = Mathf.Lerp(currentDerive, targetDerive, DeriveSpeed * Time.deltaTime);
        return currentDerive;
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
