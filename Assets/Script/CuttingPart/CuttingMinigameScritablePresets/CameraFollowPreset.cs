using UnityEngine;

/// <summary>Every tuning knob of a <see cref="CameraFollow"/>, in one asset.</summary>
/// <remarks>
/// This is what lets a single <see cref="CameraFollow"/> on the camera serve every
/// <see cref="CuttingManager"/>: the framing is no longer authored on the component, so each cut
/// pushes its own preset in on entry and the shared follow reframes itself. Without it, two cuts
/// that want different orbit radii would each need their own camera.
/// <para>
/// Applied by copy, not read through: <see cref="ApplyTo"/> writes the values onto the component
/// and the component's own fields stay the storage. That keeps the per-frame path untouched, and
/// means a scene authored before presets existed keeps its values until a preset is assigned.
/// </para>
/// <para>
/// What belongs here is framing that reads the same on any cut. A distance in world units does
/// not: the same 1.0 orbit radius is a wide shot of a wrist and a camera inside a thigh. Set
/// <see cref="framingUnits"/> to Loop Radius and the distances become multiples of the cut's own
/// ring, which is what makes one asset genuinely shareable; the mode stays World by default so
/// presets authored against a single cut keep their exact framing.
/// </para>
/// <para>
/// Where a cut opens around its ring is NOT here: that is <c>CuttingManager.orbitAngleOffset</c>,
/// alongside <c>startAngle</c>, because it is fixed by the cut's own cutting plane.
/// </para>
/// </remarks>
[CreateAssetMenu(fileName = "CameraFollowPreset", menuName = "Cutting/Camera Follow Preset")]
public class CameraFollowPreset : ScriptableObject
{
    [Header("Path")]
    [Tooltip("Orbit path: a perfect circle, or the loop's own shape offset outward.")]
    public CameraFollow.MoveMode moveMode = CameraFollow.MoveMode.Circle;

    [Tooltip("What every distance in this asset (orbit radius, height, pivot, drift, position offset) is measured in. World = fixed world units, so this preset only suits cuts of one size. Loop Radius = multiples of the cut's own ring, which is what lets one preset frame a wrist, a thigh and a body scaled up or down.")]
    public CameraFollow.FramingUnits framingUnits = CameraFollow.FramingUnits.World;

    [Tooltip("Orbit radius from the centre, in the units above.")]
    public float scale = 1f;

    [Tooltip("Lift above the cutting plane along its normal, in the units above. Raises the camera off the plane so it views the cut at an angle instead of edge-on.")]
    public float height = 0.5f;

    // angleOffset is deliberately absent, for the same reason startAngle is: it is measured from the
    // plane's right axis, so it says where THIS cut opens on its own ring. Sharing one asset between
    // two cuts must not drag them to the same opening angle. It lives on CuttingManager.orbitAngleOffset,
    // which pushes it onto both follows every time a cut claims them.

    [Tooltip("Space the offset below is read in. Plane follows the cutting plane (X = plane right, Y = along its normal, Z = plane forward), so it survives a client who is moved or turned around; World is a fixed direction in the room.")]
    public CameraFollow.OffsetSpace offsetSpace = CameraFollow.OffsetSpace.World;

    [Tooltip("Fixed extra position offset added on top of the orbit, in the space and units above.")]
    public Vector3 positionOffset = Vector3.zero;

    [Tooltip("How fast the camera eases toward the target POSITION (higher = snappier). Separate from lookSpeed, which only eases the aim.")]
    public float moveSpeed = 5f;

    [Header("Aim")]
    [Tooltip("What the camera aims at while orbiting.")]
    public CameraFollow.LookMode lookMode = CameraFollow.LookMode.Center;

    [Tooltip("How fast the camera eases toward the target rotation (higher = snappier).")]
    public float lookSpeed = 5f;

    [Tooltip("Roll the camera so the loop's travel direction points to the top of the screen.")]
    public bool loopTowardTop = false;

    [Tooltip("What ends up at the top of the screen. The cutting plane's normal has whatever sign the plane was authored with, so Plane Normal alone can hand back an upside-down view (floor at the top) with the aim still perfectly on the loop. Ignored while Loop Toward Top is on, which owns the up vector itself.")]
    public CameraFollow.UpMode upMode = CameraFollow.UpMode.PlaneNormalUpright;

    [Tooltip("Also drive rotation (aim + roll). Off = orbit position only, leaving the object's rotation untouched.")]
    public bool controlRotation = true;

    [Tooltip("Also drive POSITION (orbit). Off = leave transform.position for another script to set, while this still computes BasePosition and can drive rotation.")]
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

    [Tooltip("Static pivot offset from the loop centre, in plane units (X = plane right, Y = plane forward).")]
    public Vector2 pivotOffset = Vector2.zero;

    [Tooltip("Slowly wander the pivot on a readable Lissajous path so the target motion is learnable, not jittery.")]
    public bool pivotMoves = false;

    [Tooltip("How far the wandering pivot strays from its base offset, in plane units.")]
    public float pivotMoveRadius = 0.2f;

    [Tooltip("Wander speed, in radians per second. Keep low so the path stays readable.")]
    public float pivotMoveSpeed = 0.5f;

    [Header("Drift")]
    [Tooltip("Largest sideways drift offset from the orbit position, in world units.")]
    public float maxHorizontalDerive = 1f;

    [Tooltip("How many times per second a new random drift target is rolled.")]
    public float DerivePerSecondEvaluate = 4f;

    [Tooltip("How fast the drift eases toward its current target (higher = snappier).")]
    public float DeriveSpeed = 0.1f;

    [Tooltip("Bias of the random drift toward the centre. 1 = uniform; higher = more likely to stay near the middle.")]
    public float deriveCenterBias = 3f;

    /// <summary>Writes every value onto a follow. Touches tuning only -- never its loop guide, speed source, startAngle or live angle.</summary>
    public void ApplyTo(CameraFollow follow)
    {
        if (follow == null) return;

        follow.moveMode = moveMode;
        follow.framingUnits = framingUnits;
        follow.scale = scale;
        follow.height = height;
        follow.offsetSpace = offsetSpace;
        follow.positionOffset = positionOffset;
        follow.moveSpeed = moveSpeed;

        follow.lookMode = lookMode;
        follow.lookSpeed = lookSpeed;
        follow.loopTowardTop = loopTowardTop;
        follow.upMode = upMode;
        follow.controlRotation = controlRotation;
        follow.controlPosition = controlPosition;

        follow.rollDegrees = rollDegrees;
        follow.rollAmplitude = rollAmplitude;
        follow.rollSpeed = rollSpeed;

        follow.pivotAffectsPosition = pivotAffectsPosition;
        follow.pivotAffectsLook = pivotAffectsLook;
        follow.pivotOffset = pivotOffset;
        follow.pivotMoves = pivotMoves;
        follow.pivotMoveRadius = pivotMoveRadius;
        follow.pivotMoveSpeed = pivotMoveSpeed;

        follow.maxHorizontalDerive = maxHorizontalDerive;
        follow.DerivePerSecondEvaluate = DerivePerSecondEvaluate;
        follow.DeriveSpeed = DeriveSpeed;
        follow.deriveCenterBias = deriveCenterBias;
    }

    /// <summary>Reads every value off a follow, for building a preset out of an already hand-tuned component.</summary>
    public void CopyFrom(CameraFollow follow)
    {
        if (follow == null) return;

        moveMode = follow.moveMode;
        framingUnits = follow.framingUnits;
        scale = follow.scale;
        height = follow.height;
        offsetSpace = follow.offsetSpace;
        positionOffset = follow.positionOffset;
        moveSpeed = follow.moveSpeed;

        lookMode = follow.lookMode;
        lookSpeed = follow.lookSpeed;
        loopTowardTop = follow.loopTowardTop;
        upMode = follow.upMode;
        controlRotation = follow.controlRotation;
        controlPosition = follow.controlPosition;

        rollDegrees = follow.rollDegrees;
        rollAmplitude = follow.rollAmplitude;
        rollSpeed = follow.rollSpeed;

        pivotAffectsPosition = follow.pivotAffectsPosition;
        pivotAffectsLook = follow.pivotAffectsLook;
        pivotOffset = follow.pivotOffset;
        pivotMoves = follow.pivotMoves;
        pivotMoveRadius = follow.pivotMoveRadius;
        pivotMoveSpeed = follow.pivotMoveSpeed;

        maxHorizontalDerive = follow.maxHorizontalDerive;
        DerivePerSecondEvaluate = follow.DerivePerSecondEvaluate;
        DeriveSpeed = follow.DeriveSpeed;
        deriveCenterBias = follow.deriveCenterBias;
    }
}

/// <summary>The tuning categories, shared by the CameraFollow and CameraFollowPreset inspectors so both group their fields the same way.</summary>
public static class CameraFollowCategories
{
    /// <summary>One collapsible group: a heading and the serialized field names under it.</summary>
    public readonly struct Group
    {
        public Group(string title, string[] fields)
        {
            Title = title;
            Fields = fields;
        }

        public readonly string Title;
        public readonly string[] Fields;
    }

    public static readonly Group[] All =
    {
        new("Path", new[] { "moveMode", "framingUnits", "scale", "height", "offsetSpace", "positionOffset", "moveSpeed" }),
        new("Aim", new[] { "lookMode", "lookSpeed", "loopTowardTop", "upMode", "controlRotation", "controlPosition" }),
        new("Roll", new[] { "rollDegrees", "rollAmplitude", "rollSpeed" }),
        new("Off-centre pivot", new[] { "pivotAffectsPosition", "pivotAffectsLook", "pivotOffset", "pivotMoves", "pivotMoveRadius", "pivotMoveSpeed" }),
        new("Drift", new[] { "maxHorizontalDerive", "DerivePerSecondEvaluate", "DeriveSpeed", "deriveCenterBias" }),
    };
}
