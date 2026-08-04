using UnityEngine;

/// <summary>
/// Sweeps the camera's field of view from <see cref="startFov"/> to whatever it was authored at, over
/// <see cref="duration"/> seconds. The authored value is captured on Awake, so the scene view still shows
/// the FOV the shot is framed for.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraFovIntro : MonoBehaviour
{
    [Tooltip("Field of view the sweep starts from. Above the camera's own FOV zooms in, below zooms out.")]
    [SerializeField] private float startFov = 90f;

    [Tooltip("Length of the sweep, in seconds.")]
    [SerializeField] private float duration = 2f;

    [Tooltip("Shape of the sweep. Left to right is start to end.")]
    [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Run on unscaled time, so the sweep still plays while the game is paused or in slow motion.")]
    [SerializeField] private bool unscaledTime = true;

    [Tooltip("Largest slice of the sweep any single frame may consume, in seconds. Keeps a hitch or a focus loss from skipping the whole sweep.")]
    [SerializeField] private float maxStep = 0.1f;

    private Camera cam;

    /// <summary>The FOV the camera was authored with, which is where the sweep ends.</summary>
    private float targetFov;

    /// <summary>Seconds into the sweep, or negative when no sweep is running.</summary>
    private float elapsed = -1f;

    /// <summary>The authored FOV, for anything that needs the end value while the sweep is still mid-flight.</summary>
    public float TargetFov => targetFov;

    /// <summary>True while the camera is showing a transient sweep value rather than its authored FOV.</summary>
    public bool IsSweeping => elapsed >= 0f;

    private void Awake()
    {
        // Captured in Awake, not Start: other components snapshot this camera's FOV in their own Start
        // and treat it as the authored value -- CuttingManager does, and restores it after every cut.
        // Start order between components is undefined and differs between the Editor and a build, so
        // reading the authored value before any Start has run is the only order-proof point.
        cam = GetComponent<Camera>();
        targetFov = cam.fieldOfView;
    }

    private void Start()
    {
        Play();
    }

    /// <summary>Restarts the sweep from <see cref="startFov"/>.</summary>
    public void Play()
    {
        if (duration <= 0f)
        {
            cam.fieldOfView = targetFov;
            elapsed = -1f;
            return;
        }

        elapsed = 0f;

        // startFov is deliberately NOT written here: Play runs in the Start phase, where a component
        // whose Start has not run yet would read it as this camera's authored FOV. The first write
        // lands in LateUpdate, by which point every Start has run.
    }

    private void LateUpdate()
    {
        if (elapsed < 0f) return;

        float step = unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        // Clamped, because the player has runInBackground off: an unfocused build stops rendering
        // entirely, and the frame that regains focus reports the whole gap at once (unscaledDeltaTime
        // is not capped by maximumDeltaTime the way deltaTime is). Unclamped, that single frame
        // finishes the sweep, so the player sees the frozen startFov shot snap straight to the end
        // with no zoom. Clamping costs the sweep nothing on a healthy frame and makes it play in
        // full whenever the game is actually on screen. Same guard covers a load hitch.
        if (maxStep > 0f) step = Mathf.Min(step, maxStep);

        elapsed += step;

        float k = Mathf.Clamp01(elapsed / duration);
        cam.fieldOfView = Mathf.LerpUnclamped(startFov, targetFov, ease.Evaluate(k));

        if (k >= 1f) elapsed = -1f;
    }

    private void OnDisable()
    {
        // A disable, deactivate or destroy mid-sweep must not strand the camera at startFov: nothing
        // restarts the sweep afterwards, so the FOV would stay wrong for the rest of the run.
        if (elapsed < 0f) return;
        elapsed = -1f;
        cam.fieldOfView = targetFov;
    }
}
