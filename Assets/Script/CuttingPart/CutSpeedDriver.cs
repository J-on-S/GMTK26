using UnityEngine;

/// <summary>Manages the cut travel speed: turns wheel/key input into <see cref="currentSpeed"/>, with coast and friction. Owns nothing else -- consumers (camera, tracer) read the speed.</summary>
/// <remarks>
/// Shared by every <see cref="CuttingManager"/> and not authored by hand: reach it through
/// <see cref="Shared"/>, which finds the scene's driver or makes one. It holds no tuning of its own
/// -- direction, speeds and the backward-input rules all come from the <see cref="CameraMovesPreset"/>
/// the entering cut assigns, which is what lets one driver serve cuts that travel different ways.
/// Its only state is the live speed, and that is wiped on both enter and quit.
/// </remarks>
public class CutSpeedDriver : MonoBehaviour, ISpeedSource {

    [Tooltip("Tuning for the cut currently running. Assigned by the CuttingManager on entry, not by hand.")]
    [HideInInspector] public CameraMovesPreset preset;

    /// <summary>Seconds since the last push; friction only applies past <c>coastTime</c>.</summary>
    private float idleTimer;

    [ReadOnly] public float currentSpeed;

    /// <summary>The scene's driver, found once and remembered.</summary>
    private static CutSpeedDriver shared;

    /// <summary>The one driver every cut runs on. Found in the scene, or created at play time if the scene has none.</summary>
    /// <remarks>
    /// Never creates anything in edit mode: this is reached from <c>PushParameters</c>, which runs
    /// on every <c>OnValidate</c>, and spawning a GameObject from there would litter the scene every
    /// time a field is touched. In edit mode it returns the scene's driver or null.
    /// </remarks>
    public static CutSpeedDriver Shared {
        get {
            if (shared == null) {
                shared = FindFirstObjectByType<CutSpeedDriver>(FindObjectsInactive.Include);
            }
            if (shared == null && Application.isPlaying) {
                GameObject go = new GameObject("~CutSpeedDriver");
                shared = go.AddComponent<CutSpeedDriver>();
                shared.enabled = false; // parked until a cut enables it
            }
            return shared;
        }
    }

    /// <summary>Which way the running cut travels around the ring, as a sign. 1 when no preset is assigned yet.</summary>
    private int Direction => preset != null ? preset.DirectionMainScroll : 1;

    /// <summary>Whether input against the travel direction is read at all, either to reverse or to brake.</summary>
    private bool AcceptsBackwardInput => preset != null && (preset.canGoBackwards || preset.canDecelerateManually);

    /// <summary>Speed signed by the main travel direction; what a follower orbits at. Consumers read this instead of being pushed to.</summary>
    void Update()
    {
        UpdateCameraSpeed();
    }

    /// <summary>Whether the player is pushing against the main cut direction this frame on an input the driver then ignores (wheel ridge or held key).</summary>
    /// <remarks>
    /// Consumed by <see cref="CuttingManager"/> to freeze the scalpel, which is only right when the
    /// push does nothing. With <c>canGoBackwards</c> the push reverses travel and with
    /// <c>canDecelerateManually</c> it brakes; in both cases the camera is still obeying the input,
    /// so freezing the scalpel would drift it out of its fixed lead.
    /// </remarks>
    public bool IsPushingBackward()
    {
        if (AcceptsBackwardInput) return false;

        float scroll = GameInputActions.Scroll != null ? GameInputActions.Scroll.ReadValue<Vector2>().y : 0f;
        if (Mathf.Abs(scroll) > 0.01f && Mathf.Sign(scroll) != Mathf.Sign(Direction)) return true;

        float keys = GameInputActions.Arrows != null ? GameInputActions.Arrows.ReadValue<Vector2>().y : 0f;
        return keys * Direction < 0f;
    }

    void UpdateCameraSpeed()
    {
        // enabled between EnterMinigame and quit, but the preset only lands when a cut claims the
        // driver; without it there is no speed cap or kick size to work from.
        if (preset == null) return;
        if (GameInputActions.Scroll == null || GameInputActions.Arrows == null) return;

        float scroll = GameInputActions.Scroll.ReadValue<Vector2>().y;
        float keys = GameInputActions.Arrows.ReadValue<Vector2>().y;

        int direction = Direction;
        bool acceptsBackward = AcceptsBackwardInput;

        bool pushed = false;

        bool sameDirection = Mathf.Sign(scroll) == Mathf.Sign(direction);
        // mouse-wheel ridge = one discrete kick (impulse), when it pushes along travel dir.
        // A backward ridge is read too when reversing or braking is allowed; it subtracts, and
        // the clamp at the bottom is what stops a brake from turning into a reverse.
        if (Mathf.Abs(scroll) > 0.01f &&( sameDirection || acceptsBackward))
        {
            currentSpeed += preset.wheelKick * Mathf.Sign(scroll) * Mathf.Sign(direction);
            pushed = true;
        }

        // arrow key held = continuous push
        float keyFwd = keys * direction;
        sameDirection = Mathf.Sign(keys) == Mathf.Sign(direction);
        if (Mathf.Abs(keys) > 0 &&( sameDirection || acceptsBackward) )
        {
            currentSpeed += preset.acceleration * keyFwd * Time.deltaTime;
            pushed = true;
        }

        // coast: hold speed for coastTime after the last push, then friction
        if (pushed)
        {
            idleTimer = 0f;
        }
        else
        {
            idleTimer += Time.deltaTime;
            // only decelerate if the currentSpeed is the same sign as where we are going
            if (idleTimer >= preset.coastTime &&  Mathf.Sign(currentSpeed) == Mathf.Sign(direction) )
            {
                currentSpeed += preset.deceleration * Time.deltaTime;
            }
        }
        float minSpeed = preset.canGoBackwards ? -preset.MaxSpeed : 0;
        currentSpeed = Mathf.Clamp(currentSpeed, minSpeed, preset.MaxSpeed);
    }

    /// <summary>Wipes every field that carries over between cuts: the live speed and the coast timer.</summary>
    /// <remarks>
    /// Called on both enter and quit. On quit so a stopped cut can't hand its momentum to the next
    /// one, and on enter so a cut that was quit some other way still starts from a standstill.
    /// Named ResetDrive, not Reset: Unity calls a method named <c>Reset</c> by itself when the
    /// component is added or reset in the inspector.
    /// </remarks>
    public void ResetDrive()
    {
        idleTimer = 0f;
        currentSpeed = 0f;
    }

    public float GetSignedSpeed()
    {
        return Direction * currentSpeed;
    }

    public void SetSignedSpeed(float value)
    {
        currentSpeed =  value;
    }

    public void Disable()
    {
        this.enabled = false;
    }

    public void Enable()
    {
        this.enabled = true;
    }

}
