using UnityEngine;

/// <summary>Every tuning knob of one cutting minigame, in a single asset.</summary>
/// <remarks>
/// A <see cref="CuttingManager"/> with a preset assigned reads all of its tuning from here, so
/// authoring a new cut is "drop the prefab, pick a preset, place the plane" instead of filling
/// fourteen inspector fields. Managers with no preset keep using their own inline values, so
/// scenes authored before this asset existed are unaffected.
/// </remarks>
[CreateAssetMenu(fileName = "CutMinigamePreset", menuName = "Cutting/Cut Minigame Preset")]
public class CutMinigamePreset : ScriptableObject
{
 


    [Header("Camera")]
    [Tooltip("Field of view while cutting, in degrees. The free-look FOV is snapshotted on enter and put back on quit.")]
    public float cameraFOV = 60f;

    [Header("Scalpel")]
    [Tooltip("Fixed angular gap (deg) the scalpel keeps ahead of the camera.")]
    public float scalpelAngleLead = -2.58f;

    [Header("Sub-presets")]
    [Tooltip("Travel speed, acceleration, coast and friction. Read by the CutSpeedDriver.")]
    public CameraMovesPreset cameraPreset;

    [Tooltip("Shape of the wavy target loop. Read by the LoopGuideBuilder.")]
    public CurvePreset curvePreset;

    [Tooltip("Along-limb input, speeds and smoothing. Read by the scalpel's ScalpelSurfaceDriver.")]
    public ScalpelSurfacePreset scalpelFollowPreset;

    [Tooltip("How the camera frames this cut: orbit radius, height, aim, roll, pivot, drift. Pushed onto the shared camera CameraFollow on entry, so cuts can frame differently without each owning a camera. To share one across cuts of different sizes, set its Framing Units to Loop Radius; in World units its distances only suit the cut it was tuned on.")]
    public CameraFollowPreset cameraOrbitPreset;

    [Tooltip("The same, for the scalpel's CameraFollow. Normally has controlPosition off, since the ScalpelSurfaceDriver owns the scalpel's position.")]
    public CameraFollowPreset scalpelOrbitPreset;

    // Sound is deliberately absent: the clips are wired on the CuttingManager itself.

    [Header("Guide line")]
    [Tooltip("Drawn width of the target loop, in world units.")]
    public float curveWidth = 0.005f;

    [Tooltip("How far the drawn loop is lifted off the surface so it doesn't z-fight, in world units. Drawing only; scoring uses the unlifted loop.")]
    public float curveHoverLength = 0.01f;

    [Tooltip("Smallest number of points the loop is warped and drawn with. The cross-section of a low-poly body is only a handful of points, and curving those few makes a zigzag instead of a wave. 0 keeps the raw extraction.")]
    public int curveResolution = 64;
}
