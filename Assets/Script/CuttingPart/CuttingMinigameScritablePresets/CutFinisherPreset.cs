using UnityEngine;

/// <summary>Every tuning knob of the finisher chop, in a single asset several cuts can share.</summary>
/// <remarks>Invariant: a finisher with no preset keeps using its own inline values.</remarks>
[CreateAssetMenu(fileName = "CutFinisherPreset", menuName = "Cutting/Cut Finisher Preset")]
public class CutFinisherPreset : ScriptableObject
{
    // The shot stays on the component: it is stored in one body's local space, so sharing it would
    // point the camera at the wrong limb.

    [Header("Shot")]
    [Tooltip("Field of view for the close-up, in degrees. Usually tighter than the cut's own.")]
    public float cameraFOV = 40f;

    [Tooltip("Seconds the camera takes to reach the shot. 0 = snap.")]
    public float easeIn = 0.5f;

    [Tooltip("Shapes the camera's move into the shot.")]
    public AnimationCurve easeInCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Tool")]
    [Tooltip("Prefab that does the chopping. Leave empty to override it inline on the cut.")]
    public GameObject toolPrefab;

    [Tooltip("Extra rotation on the tool, in degrees, for prefabs whose blade doesn't point down their own +Z.")]
    public Vector3 toolEuler;

    [Header("Approach")]
    [Tooltip("Direction the blade travels across the cut, as an angle around the cut normal in degrees. 0 = the plane's right, 90 = its forward.")]
    [Range(-180f, 180f)]
    public float sweepAngle = 0f;

    [Tooltip("Tilts the approach out of the cutting plane, in degrees. 0 = the blade stays in the plane and chops across the limb, 90 = straight down the cut normal, negative = from the opposite side.")]
    [Range(-90f, 90f)]
    public float approachTilt = 90f;

    [Header("Wait")]
    // The tool waits at the swing's own start; anywhere else and the blade jumps to there on the
    // click.

    [Tooltip("Bob distance while waiting, in world units, along the approach axis. A tenth of Hover Height reads as nothing.")]
    public float bobAmp = 0.06f;

    [Tooltip("Bob rate while waiting, in cycles per second.")]
    public float bobHz = 1.5f;

    [Tooltip("Seconds before the swing fires on its own, for a player who never clicks. 0 = wait forever.")]
    public float autoSlashAfter = 0f;

    [Header("Slash")]
    [Tooltip("How far out along the approach axis the swing starts, in world units. Also where the tool waits.")]
    public float hoverHeight = 0.25f;

    [Tooltip("Half the blade's travel across the cut. The swing runs from +this to -this along the sweep axis.")]
    public float sweepDist = 0.6f;

    [Tooltip("Seconds the swing takes.")]
    public float slashTime = 0.18f;

    [Tooltip("Shapes the swing. Front-loaded reads as a chop; linear reads as a slice.")]
    public AnimationCurve slashEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Seconds held after the swing lands, before the camera flies back out.")]
    public float holdAfter = 0.25f;

    [Tooltip("Impulse pushing the severed piece away along the approach axis. 0 = leave it where it fell.")]
    public float kick = 3f;
}
