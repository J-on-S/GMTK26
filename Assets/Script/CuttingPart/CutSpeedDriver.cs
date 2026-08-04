using UnityEngine;

/// <summary>Manages the cut travel speed: turns wheel/key input into <see cref="currentSpeed"/></summary>
public class CutSpeedDriver : MonoBehaviour, ISpeedSource {

    [Tooltip("Tuning for the cut currently running. Assigned by the CuttingManager on entry, not by hand. When null, the inline fields below are used instead.")]
    [HideInInspector] public CameraMovesPreset preset;

    [Header("Fallback tuning (used when no preset is assigned)")]

    [Tooltip("Time for the camera to complete one full loop (360 deg) at top speed, in seconds. The speed cap derives from this.")]
    public float secondsPerLoop = 12f;

    [Tooltip("Continuous push rate while an arrow key is held (units/sec added to speed).")]
    public float acceleration = 4f;

    [Tooltip("Speed added per mouse-wheel ridge (one kick, like a skateboard foot push).")]
    public float wheelKick = 3f;

    [Tooltip("Friction rate once coasting ends. Negative = slows down.")]
    public float deceleration = -0.1f;

    [Tooltip("Glide time after the last push before friction starts, in seconds.")]
    public float coastTime = 0.3f;

    [Tooltip("Which way the cut travels around the ring: 1 or -1. Scroll and keys are read relative to it.")]
    public int directionMainScroll = 1;

    [Tooltip("Let the player travel backwards along the cut. Off, the speed floor is 0.")]
    public bool canGoBackwards = false;

    [Tooltip("Let input against the travel direction brake down to a stop, but never reverse. Ignored when Can Go Backwards is on.")]
    public bool canDecelerateManually = false;

    /// <summary>Seconds since the last push; friction only applies past <c>coastTime</c>.</summary>
    private float idleTimer;

    [ReadOnly] public float currentSpeed;

    // Every number below reads from the assigned preset when there is one, and from the inline field
    // otherwise -- the same fallback pattern as the other cutting components.
    private float MaxSpeed => preset != null ? preset.MaxSpeed : (secondsPerLoop > 0f ? 360f / secondsPerLoop : float.MaxValue);
    private float Acceleration => preset != null ? preset.acceleration : acceleration;
    private float WheelKick => preset != null ? preset.wheelKick : wheelKick;
    private float Deceleration => preset != null ? preset.deceleration : deceleration;
    private float CoastTime => preset != null ? preset.coastTime : coastTime;
    private bool CanGoBackwards => preset != null ? preset.canGoBackwards : canGoBackwards;
    private bool CanDecelerateManually => preset != null ? preset.canDecelerateManually : canDecelerateManually;

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

    /// <summary>Which way the running cut travels around the ring, as a sign, from the preset or the inline field.</summary>
    private int Direction => preset != null ? preset.DirectionMainScroll : directionMainScroll;

    /// <summary>Whether input against the travel direction is read at all, either to reverse or to brake.</summary>
    private bool AcceptsBackwardInput => CanGoBackwards || CanDecelerateManually;

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
        // enabled between EnterMinigame and quit. Tuning comes from the assigned preset, or the inline
        // fields when a cut runs with none, so there is always a speed cap and kick size to work from.
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
            currentSpeed += WheelKick * Mathf.Sign(scroll) * Mathf.Sign(direction);
            pushed = true;
        }

        // arrow key held = continuous push
        float keyFwd = keys * direction;
        sameDirection = Mathf.Sign(keys) == Mathf.Sign(direction);
        if (Mathf.Abs(keys) > 0 &&( sameDirection || acceptsBackward) )
        {
            currentSpeed += Acceleration * keyFwd * Time.deltaTime;
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
            if (idleTimer >= CoastTime &&  Mathf.Sign(currentSpeed) == Mathf.Sign(direction) )
            {
                currentSpeed += Deceleration * Time.deltaTime;
            }
        }
        float minSpeed = CanGoBackwards ? -MaxSpeed : 0;
        currentSpeed = Mathf.Clamp(currentSpeed, minSpeed, MaxSpeed);
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
