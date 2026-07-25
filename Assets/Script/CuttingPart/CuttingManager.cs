using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Runs one cutting minigame: owns its tuning, drives the camera + scalpel around the target loop, and reports progress.</summary>
/// <remarks>
/// Authoring shape: a cut needs a <see cref="CuttableObject"/> to cut, a <see cref="LoopGuideBuilder"/>
/// for the target loop, and the camera/scalpel/speed hardware it drives.
/// <para>
/// Tuning can come from a <see cref="CutMinigamePreset"/>; each number falls back to this
/// manager's own inline field when no preset is assigned, so a scene wired before the preset
/// existed behaves exactly as it did.
/// </para>
/// </remarks>
// runs before CameraFollow so the scalpel angle it sets is consumed the same frame.
// ExecuteAlways so the scalpel start-angle lead previews in edit mode.
[ExecuteAlways]
[DefaultExecutionOrder(-10)]
public class CuttingManager : MonoBehaviour
{

public enum CuttingState
    {
        NOT_STARTED,

        PROGRESSING,
        COMPLETED
    }

    [Tooltip("All of this cut's tuning in one asset. Assign it and every inline number below is ignored.")]
    public CutMinigamePreset minigamePreset;

    [Tooltip("Angle around the ring this cut opens at, in degrees. Progress is measured from here. Per-cut geometry, never taken from a preset.")]
    public float startAngle;

    [Tooltip("Angle this cut completes at, in degrees. 360 = one full turn from startAngle. Per-cut geometry, never taken from a preset.")]
    public float endAngle= 360;

    [ReadOnly] public float currentAngle;

    [ReadOnly] public float currentProgress => scalpelFollow == null
        ? 0f
        : (scalpelFollow.Angle - StartAngle) / (EndAngle - StartAngle); // normalized 0-1

    [ReadOnly] bool isPlaying = false;

    public CuttableObject GameObjectBeingCut;

    [Header("Identity")]
    [Tooltip("Which body part this cut takes off, e.g. \"Left Arm\". Names the piece for the rest of the game; not used by the cut itself.")]
    public string bodyPartName;

    [Tooltip("ToolPickup.itemName the player must be holding to start this cut. Leave empty for a cut that needs no particular tool.")]
    public string requiredToolName;

    // Snapshot of the free-look camera pose, by value: holding the Transform itself would just
    // alias the live camera, so restoring would assign every field to itself.
    private Vector3 initialCameraPos;
    private Quaternion initialCameraRot;
    private float initialcameraFOV;

    [Tooltip("Camera field of view while cutting, in degrees. The free-look FOV is snapshotted on enter and put back on quit.")]
    public float cameraFOV = 40f;

    /// <summary>Camera the minigame takes over, resolved from the scene rather than assigned.</summary>
    /// <remarks>
    /// Not a serialized slot: a cut is authored per body part and the camera belongs to the scene,
    /// so a drag-and-drop reference is one more thing to get wrong (and goes stale the moment the
    /// cut is used in another scene). Resolved lazily and cached, since the lookup only has to
    /// happen on enter/quit, not per frame.
    /// </remarks>
    public Camera SceneCamera
    {
        get
        {
            if (cachedCamera == null)
            {
                // MainCamera tag first: that is the one the player is looking through. Any camera
                // is a better fallback than none, so a scene with an untagged camera still runs.
                cachedCamera = Camera.main;
                if (cachedCamera == null)
                {
                    cachedCamera = FindFirstObjectByType<Camera>(FindObjectsInactive.Include);
                }
            }
            return cachedCamera;
        }
    }

    /// <summary>Resolved scene camera; cleared by a domain reload or a scene change, then found again on next use.</summary>
    private Camera cachedCamera;

    CuttingState state= CuttingState.NOT_STARTED;

    public CameraMovesPreset cameraPreset;

    public FollowLoopPresets ScalpelFollowLoopPreset;

    public CurvePreset curvePreset;

    [Tooltip("Draws this cut's target loop; one per CuttingManager, wired to this manager's object + plane + curvePreset.")]
    public LoopGuideBuilder loopGuide;

    public static InputAction move;

    /// <summary>Arrow-key drive, built in code so it needs no entry in the input asset. Same effect as the wheel.</summary>
    public static InputAction arrows;

    /// <summary>Per-frame mouse motion in pixels, both axes: x = horizontal, y = vertical. Shared by the scalpel's along-limb slide and <see cref="MoveCamera"/>'s look, so both read the same delta.</summary>
    public static InputAction mouseDelta;

    public MoveCamera moveCamera;

    [Header("Scalpel sync")]

    /// <summary>The camera's orbit -- the live angle that IS the cut progress, and that the scalpel slaves to.</summary>
    /// <remarks>
    /// Read off the camera, not assigned. A CameraFollow moves the transform it sits on, and this
    /// cut sets the FOV of <see cref="SceneCamera"/> and restores that camera's pose on quit, so an
    /// orbit anywhere else would leave the camera still. A drag-and-drop slot could only ever be
    /// filled with this component or with something broken -- most dangerously the scalpel's own
    /// follow, which looks identical in the inspector.
    /// <para>Cached, and re-fetched if the resolved camera changes.</para>
    /// </remarks>
    public CameraFollow cameraFollow
    {
        get
        {
            Camera cam = SceneCamera;
            if (cam == null)
            {
                return null;
            }
            if (cachedCameraFollow == null || cachedCameraFollow.gameObject != cam.gameObject)
            {
                cam.TryGetComponent(out cachedCameraFollow);
            }
            return cachedCameraFollow;
        }
    }

    /// <summary>Resolved camera orbit; dropped and found again when the camera changes.</summary>
    private CameraFollow cachedCameraFollow;

    [Tooltip("The scalpel's CameraFollow -- its orbit angle is driven here, not self-advanced. Assigned, unlike the camera's: the scalpel is a separate object this cut has no way to find.")]
    public CameraFollow scalpelFollow;

    [Tooltip("Fixed angular gap (deg) the scalpel keeps ahead of the camera.")]
    public float scalpelAngleLead;

    [Header("Sound")]
    [Tooltip("Channel and clips this cut plays. Shared across cuts, so one asset normally serves them all.")]
    public CutSoundPreset soundPreset;

    /// <summary>The looping cut sound while it is playing, so exactly that instance can be stopped again.</summary>
    private AudioMaster.PlayingClip cutLoop;

    /// <summary>Whether the cut loop is currently meant to be sounding. Edge-triggers the play/stop so a frame where the channel returns nothing doesn't retry forever.</summary>
    private bool cutSoundOn;

    /// <summary>The shared speed driver, provisioned on demand. Not a serialized slot: one driver serves every cut, and its per-cut tuning arrives with the CameraMovesPreset.</summary>
    public CutSpeedDriver speedDriver => CutSpeedDriver.Shared;


    public static event Action<CuttingManager> OnMinigameEntered;
    /// <summary>Fired when a cut finishes. The GameObject is the severed piece, or null when the slice produced none.</summary>
    public static event Action<CuttingManager, GameObject> OnMinigameCompleted;

    public static event Action<CuttingManager> OnMinigameQuit;

    // ---- resolved tuning: the preset when one is assigned, the inline field otherwise ----

    // Note the two below are NOT preset-backed: a preset is a reusable feel, and where a cut opens
    // around its ring is fixed by its own cutting plane. Sharing one preset must not move them.

    /// <summary>Angle the cut opens at, in degrees. Progress is measured from here.</summary>
    public float StartAngle => startAngle;

    /// <summary>Angle the cut completes at, in degrees.</summary>
    public float EndAngle => endAngle;

    /// <summary>Field of view held while cutting, in degrees.</summary>
    public float CameraFOV => minigamePreset != null ? minigamePreset.cameraFOV : cameraFOV;

    /// <summary>Fixed angular gap the scalpel keeps ahead of the camera, in degrees.</summary>
    public float ScalpelAngleLead => minigamePreset != null ? minigamePreset.scalpelAngleLead : scalpelAngleLead;

    /// <summary>Travel-speed tuning handed to the speed driver.</summary>
    public CameraMovesPreset SpeedPreset => minigamePreset != null && minigamePreset.cameraPreset != null
        ? minigamePreset.cameraPreset
        : cameraPreset;

    /// <summary>Target-loop shape handed to the loop guide.</summary>
    public CurvePreset Curve => minigamePreset != null && minigamePreset.curvePreset != null
        ? minigamePreset.curvePreset
        : curvePreset;

    /// <summary>Along-limb tuning handed to the scalpel's follower.</summary>
    public FollowLoopPresets ScalpelPreset => minigamePreset != null && minigamePreset.scalpelFollowPreset != null
        ? minigamePreset.scalpelFollowPreset
        : ScalpelFollowLoopPreset;

    /// <summary>Framing pushed onto the shared camera orbit on entry. Preset-only: there is no inline fallback, since the follow keeps its own hand-tuned values when none is given.</summary>
    public CameraFollowPreset CameraOrbitPreset => minigamePreset != null ? minigamePreset.cameraOrbitPreset : null;

    /// <summary>Framing pushed onto the scalpel's orbit on entry.</summary>
    public CameraFollowPreset ScalpelOrbitPreset => minigamePreset != null ? minigamePreset.scalpelOrbitPreset : null;

    // ---- sound, all of it off the one sound preset ----

    /// <summary>Channel the cut sounds play on.</summary>
    public AudioEventChannel Channel => soundPreset != null ? soundPreset.channel : null;

    /// <summary>Looping sound held while the cut is travelling.</summary>
    public Audio CutSound => soundPreset != null ? soundPreset.cutSound : null;

    /// <summary>One-shot fired when the cut completes.</summary>
    public Audio TearSound => soundPreset != null ? soundPreset.tearSound : null;

    /// <summary>Travel speed above which the cut counts as cutting, in deg/sec.</summary>
    public float CutSoundSpeedThreshold => soundPreset != null ? soundPreset.cutSoundSpeedThreshold : 0.5f;

  

    /// <summary>What is still missing before this cut can run, in inspector-readable words. Empty when it is ready.</summary>
    public List<string> MissingWiring()
    {
        var missing = new List<string>();
        if (GameObjectBeingCut == null) missing.Add("Object being cut");
        if (loopGuide == null) missing.Add("Loop guide");
        else if (loopGuide.plane == null) missing.Add("A CutPlane for the loop guide");
        if (SceneCamera == null) missing.Add("Scene camera");
        if (moveCamera == null) missing.Add("Free-look MoveCamera");
        if (cameraFollow == null) missing.Add("A CameraFollow on the scene camera");
        if (scalpelFollow == null) missing.Add("Scalpel CameraFollow");
        // no speed-driver entry: it is provisioned on demand, so it can never be "missing".
        if (SpeedPreset == null) missing.Add("Camera moves preset");
        if (Curve == null) missing.Add("Curve preset");
        return missing;
    }

    /// <summary>Fills the per-cut references by looking around this object. Never overwrites a slot that is already set, so a deliberate hand-wiring survives.</summary>
    [ContextMenu("Auto-wire")]
    public void AutoWire()
    {
        if (loopGuide == null)
        {
            loopGuide = GetComponentInChildren<LoopGuideBuilder>(true);
        }

        // the target is normally this manager's parent (the menu tool parents cuts under it),
        // else whatever the guide is already pointed at.
        if (GameObjectBeingCut == null)
        {
            GameObjectBeingCut = GetComponentInParent<CuttableObject>();
        }
        if (GameObjectBeingCut == null && loopGuide != null && loopGuide.meshFollow != null)
        {
            loopGuide.meshFollow.TryGetComponent(out GameObjectBeingCut);
        }

        if (loopGuide != null && loopGuide.plane == null)
        {
            // this cut's own plane, which the setup tool parents under it
            loopGuide.plane = GetComponentInChildren<CutPlane>(true);
        }

        PushParameters();
    }


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

        // seed the snapshot: the RestoreRig below reads it before any SetupRig has run.
        CaptureCameraState();

        // seed the kept progress, so the first entry opens at startAngle and not at 0.
        currentAngle = StartAngle;
        PushParameters();

        // ExecuteAlways also runs this in edit mode; input + camera setup is play-only.
        if (!Application.isPlaying) return;

        InstantiateActions();
        
           

        // park the rig in free-look; not QuitMinigame, which would report quitting a game
        // that never started and fire OnMinigameQuit at every listener on load.
        RestoreRig();
    }
    /// <summary>Holds the scalpel's orbit start-angle a fixed lead ahead of the camera's, live in edit mode so the follower previews before play.</summary>
    void DriveScalpelStartAngle()
    {
        if (scalpelFollow == null || cameraFollow == null) return;
        scalpelFollow.startAngle = cameraFollow.startAngle + ScalpelAngleLead;
    }

    // Update is called once per frame
    void Update()
    {

        // edit-mode + live preview of the scalpel's start-angle lead.


        if (isPlaying)
        {
            DriveScalpelStartAngle();
            SyncScalpel();

            // the camera's orbit angle IS the cut progress; mirror it so currentProgress reads it.
            if (cameraFollow != null) currentAngle = cameraFollow.Angle;

            UpdateCutSound();

            if(currentProgress >=1 ) HandleCompletion();
            // edge, not held: a held Q would quit again the instant the player re-entered.
            else if (Keyboard.current.qKey.wasPressedThisFrame){
                QuitMinigame();
            }
        }
    }


    /// <summary>Slaves the scalpel's orbit angle to the camera's, a fixed lead ahead. Frozen on frames the player pushes against the cut direction.</summary>


    [ContextMenu("StartMinigame")]
    public void EnterMinigame()
    {
        if( state == CuttingState.COMPLETED || isPlaying) return;

        // fail loud here instead of half-entering and NREing inside SetupRig: a missing
        // piece is a scene setup mistake worth naming.
        List<string> missing = MissingWiring();
        if (missing.Count > 0)
        {
            Debug.LogError($"{name}: can't enter the cut, still missing {string.Join(", ", missing)}.", this);
            return;
        }

        OnMinigameEntered?.Invoke(this);
        Debug.LogWarning("entering minigame");

        isPlaying = true;
        state = CuttingState.PROGRESSING;

        SetupRig();
    }
    [ContextMenu("quit Minigame")]
    void QuitMinigame()
    {
        if(!isPlaying)
        {
            Debug.LogError("trying to Quit minigame but not in it");
            return;
        }
        isPlaying = false;

        // before RestoreRig: quitting mid-stroke must not leave the loop sounding.
        StopCutSound();

        RestoreRig();

        OnMinigameQuit?.Invoke(this);
    }

    /// <summary>Switches the scene over to cutting: stores what <see cref="RestoreRig"/> puts back, hands the camera to the loop, and wakes the scalpel + speed driver. Mirror of <see cref="RestoreRig"/>; keep the two in step.</summary>
    void SetupRig()
    {
        ClaimCamera();

        // CameraFollow.OnEnable re-seeds its angle from startAngle, and the enable in ClaimCamera
        // fires it, so re-entering would rewind the cut. currentAngle is the kept progress: put it back.
        cameraFollow.Angle = currentAngle;
        if (scalpelFollow != null) scalpelFollow.Angle = currentAngle + ScalpelAngleLead;

        // the driver is shared, so start from a standstill rather than inheriting whatever the
        // last cut was carrying. PushParameters in ClaimCamera already gave it this cut's preset.
        speedDriver.ResetDrive();
        speedDriver.Enable();
    }

    /// <summary>Hands the scene camera to this cut: free-look off, orbit on, cutting FOV, scalpel awake.</summary>
    /// <remarks>
    /// Shared by the real cut and the editor preview, so the preview cannot drift out of step with
    /// what actually happens on entry. Everything input-driven -- the speed driver, the kept
    /// progress -- stays in <see cref="SetupRig"/>, since a preview drives the angle itself.
    /// Mirror of <see cref="ReleaseCamera"/>; keep the two in step.
    /// </remarks>
    internal void ClaimCamera()
    {
        // remember the free-look camera state so quitting can put it back.
        CaptureCameraState();

        // the hardware is shared between cuts, so the last one left its own guide and presets
        // on it. Claim it before it runs.
        PushParameters();

        // camera: free-look off, orbit on, cutting FOV.
        if (moveCamera != null) moveCamera.enabled = false;
        if (cameraFollow != null) cameraFollow.enabled = true;
        RefreshLiveTuning();

        // scalpel angle is driven by SyncScalpel; stop its CameraFollow from self-advancing.
        if (scalpelFollow != null) scalpelFollow.rotationSpeed = 0f;

        SetScalpelTrace(true);
    }

    /// <summary>Gives the camera back: pose and FOV restored, free-look on, orbit off, trace off. Mirror of <see cref="ClaimCamera"/>.</summary>
    internal void ReleaseCamera()
    {
        // put the camera where it was found
        if (SceneCamera != null)
        {
            SceneCamera.transform.SetPositionAndRotation(initialCameraPos, initialCameraRot);
            SceneCamera.fieldOfView = initialcameraFOV;
        }

        if (moveCamera != null) moveCamera.enabled = true; // reEnable playerMovement
        if (cameraFollow != null) cameraFollow.enabled = false;

        SetScalpelTrace(false);
    }

    /// <summary>Re-reads the tuning that can change while a cut is already on screen: field of view, and which framing preset each follow uses.</summary>
    /// <remarks>
    /// Called on entry and on every editor-preview tick. Without the per-tick call these are read
    /// once and never again, so editing cameraFOV on the CutMinigamePreset appears to do nothing
    /// until the preview is restarted.
    /// <para>
    /// Deliberately narrow: it does not re-run <see cref="PushParameters"/>, which writes serialized
    /// fields across several components and would churn the scene on every editor frame.
    /// </para>
    /// </remarks>
    public void RefreshLiveTuning()
    {
        if (SceneCamera != null) SceneCamera.fieldOfView = CameraFOV;

        if (cameraFollow != null && CameraOrbitPreset != null) cameraFollow.preset = CameraOrbitPreset;
        if (scalpelFollow != null && ScalpelOrbitPreset != null) scalpelFollow.preset = ScalpelOrbitPreset;
    }

    /// <summary>Sets the scene up exactly as entering the cut would, for the editor preview. No input, no speed driver -- the preview owns the angle.</summary>
    public void EnterPreview()
    {
        ClaimCamera();
    }

    /// <summary>Undoes <see cref="EnterPreview"/>.</summary>
    public void ExitPreview()
    {
        ReleaseCamera();
    }


    [ContextMenu("Reset the cut")]
    /// <summary>Rewinds the cut to <c>startAngle</c>: orbit angles, progress, travel speed and the scalpel's trail. Called on every entry, so quitting always costs the run.</summary>
    void ResetCut()
    {
        currentAngle = StartAngle;

        // set the live angles, not just startAngle: CameraFollow only re-seeds itself in
        // OnEnable, which doesn't fire when the rig is already enabled.
        if (cameraFollow != null) cameraFollow.Angle = StartAngle;
        if (scalpelFollow != null) scalpelFollow.Angle = StartAngle + ScalpelAngleLead;

        if (speedDriver != null) speedDriver.ResetDrive();

        if (scalpelFollow != null
            && scalpelFollow.TryGetComponent<LoopFollowingObject>(out var scalpelLoop))
        {
            scalpelLoop.ResetTrace();
        }
    }

    /// <summary>Undoes <see cref="SetupRig"/>: camera back where it was found, free-look on, speed driver parked. Mirror of <see cref="SetupRig"/>; keep the two in step.</summary>
    void RestoreRig()
    {
        ReleaseCamera();

        if (speedDriver != null)
        {
            // full reset, not just the speed: the coast timer carries over too, and the next cut
            // to claim this driver must not inherit it.
            speedDriver.ResetDrive();
            speedDriver.Disable();
        }

        SetScalpelTrace(false);
    }

    /// <summary>Snapshots the camera's current pose and FOV, the state <see cref="RestoreRig"/> returns it to.</summary>
    void CaptureCameraState()
    {
        Camera cam = SceneCamera;
        if (cam == null) return;
        initialCameraPos = cam.transform.position;
        initialCameraRot = cam.transform.rotation;
        initialcameraFOV = cam.fieldOfView;
    }

    /// <summary>Turns the scalpel's surface trail on or off, if it has a follower.</summary>
    void SetScalpelTrace(bool on)
    {
        CameraFollow scalpel = scalpelFollow;
        if (scalpel == null) return;
        if (scalpel.TryGetComponent<LoopFollowingObject>(out var scalpelLoop))
        {
            scalpelLoop.enabled = on;
            scalpelLoop.drawTrace = on;
        }
    }

    /// <summary>Holds the cut loop sounding exactly while the cut is travelling, and silences it the moment it stalls.</summary>
    /// <remarks>
    /// Edge-triggered on the want/don't-want flip rather than driven off <c>cutLoop</c> being null:
    /// <see cref="AudioEventChannel.Play"/> returns null when no AudioMaster is listening, which
    /// would otherwise make every frame retry the play.
    /// </remarks>
    void UpdateCutSound()
    {
        bool wants = isPlaying
            && CutSound != null
            && Channel != null
            && speedDriver != null
            && Mathf.Abs(speedDriver.GetSignedSpeed()) > CutSoundSpeedThreshold;

        if (wants == cutSoundOn) return;

        if (wants)
        {
            cutSoundOn = true;
            cutLoop = Channel.Play(CutSound);
        }
        else
        {
            StopCutSound();
        }
    }

    /// <summary>Silences the cut loop, if it is sounding. Safe to call when it isn't.</summary>
    void StopCutSound()
    {
        cutSoundOn = false;
        if (cutLoop != null && Channel != null)
        {
            Channel.Stop(cutLoop);
        }
        cutLoop = null;
    }

    void SyncScalpel()
    {
        if (scalpelFollow == null || cameraFollow == null) return;

        // freeze: don't advance the scalpel when scrolling against the main cut direction.
        if (speedDriver != null && speedDriver.IsPushingBackward()) return;

        // set before CameraFollow.Update (which runs after this, at order 0) so BasePosition uses it this frame.
        scalpelFollow.Angle = cameraFollow.Angle + ScalpelAngleLead;


    }

    [ContextMenu("HandleCompletion")]
    void HandleCompletion()
    {
        Debug.LogError("minigameCompleted");
        state = CuttingState.COMPLETED;

        // the loop stops and the tear lands together, on the frame the part comes away.
        StopCutSound();
        if (Channel != null && TearSound != null)
        {
            Channel.Play(TearSound);
        }

        GameObject severed = SliceOffPart();

        QuitMinigame();
        // instantiate the BodyPart
        OnMinigameCompleted?.Invoke(this, severed);
    }

    /// <summary>Runs the slice and picks out the piece that came away.</summary>
    /// <remarks>
    /// One cut should sever exactly one piece. Anything else means the plane and the bounds window
    /// disagree with what this cut thinks it is removing -- a window wide enough to catch a second
    /// limb, or a plane that never fully crosses -- so both are worth an error rather than a silent
    /// wrong answer. More than one still hands back the first, so a mis-authored cut degrades
    /// instead of stalling the game.
    /// </remarks>
    /// <returns>The severed piece, or null when the slice produced none.</returns>
    GameObject SliceOffPart()
    {
        if (GameObjectBeingCut == null)
        {
            Debug.LogError($"{name}: cut completed with nothing to cut.", this);
            return null;
        }

        // the plane travels as an argument, so the body never holds "which cut is happening"
        // state and two cuts on one body cannot tread on each other.
        List<GameObject> lowerHulls = GameObjectBeingCut.SpliceWindowed(CutPlane);

        if (lowerHulls == null || lowerHulls.Count == 0)
        {
            Debug.LogError($"{name}: the slice severed no piece. Check the cutting plane crosses the mesh and the bounds window contains a closed loop.", this);
            return null;
        }

        if (lowerHulls.Count > 1)
        {
            Debug.LogError($"{name}: the slice severed {lowerHulls.Count} pieces, expected 1. Narrow the bounds window so it only catches this cut's loop. Using the first.", this);
        }

        return lowerHulls[0];
    }

/// <summary>This manager owns the tuning; it pushes its presets + wiring down into the loop guide, both CameraFollows and the cutting speed driver so they can't drift apart. Live in edit mode too.</summary>
    void PushParameters()
    {
        // loop guide: target, curve shape and drawn width.
        if (loopGuide != null)
        {
            if (GameObjectBeingCut != null) loopGuide.meshFollow = GameObjectBeingCut;
            if (Curve != null) loopGuide.preset = Curve;
            if (minigamePreset != null)
            {
                loopGuide.curveWidth = minigamePreset.curveWidth;
                loopGuide.curveHoverLength = minigamePreset.curveHoverLength;
            }
        }

        // cutting speed driver reads the camera-moves preset.
        CutSpeedDriver driver = speedDriver;
        if (driver != null && SpeedPreset != null) driver.preset = SpeedPreset;

        // main camera: orbit this guide, travelling at the speed driver's speed, opening the cut
        // at this manager's startAngle -- the same angle currentProgress is measured from.
        CameraFollow orbit = cameraFollow;
        if (orbit != null)
        {
            if (loopGuide != null) orbit.loopGuide = loopGuide;
            // only when there is one to give: in edit mode the driver is not provisioned yet, and
            // clearing the slot on every OnValidate would just churn the scene.
            if (driver != null) orbit.SetSpeedSource(driver);
            orbit.startAngle = StartAngle;

            // framing: the follow is shared, so this cut reclaims it with its own preset.
            if (CameraOrbitPreset != null)
            {
                orbit.preset = CameraOrbitPreset;
                orbit.ApplyPreset();
            }
        }

        // scalpel: same guide, and its along-limb follow tuning. Its speed source stays null --
        // its angle is slaved by SyncScalpel, a fixed lead ahead of the camera.
        CameraFollow scalpel = scalpelFollow;
        if (scalpel != null)
        {
            if (loopGuide != null) scalpel.loopGuide = loopGuide;
            scalpel.SetSpeedSource(null);

            if (ScalpelOrbitPreset != null)
            {
                scalpel.preset = ScalpelOrbitPreset;
                scalpel.ApplyPreset();
            }

            if (ScalpelPreset != null
                && scalpel.TryGetComponent<LoopFollowingObject>(out var scalpelLoop))
            {
                scalpelLoop.preset = ScalpelPreset;
                if (loopGuide != null) scalpelLoop.builder = loopGuide;
            }
        }
    }
    void InstantiateActions()
    {
         if(move == null){
            move = new InputAction(
            name: "MouseScroll",
            type: InputActionType.Value,
            binding: "<Mouse>/scroll"
        );
        move.Enable();
        }
        if(arrows == null){
            // arrow keys as a 2D vector, same role as the wheel; built here so the input asset needs no change
            arrows = new InputAction("Arrows", InputActionType.Value, expectedControlType: "Vector2");
            arrows.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/downArrow")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d")
                .With("Right", "<Keyboard>/rightArrow");
            arrows.Enable();
        }
        if(mouseDelta == null){
            // raw pointer motion, both axes; readers pick the component they need
            mouseDelta = new InputAction(
                name: "MouseDelta",
                type: InputActionType.Value,
                binding: "<Mouse>/delta",
                expectedControlType: "Vector2"
            );
            mouseDelta.Enable();
        }
    }
    void Reset()
    {
        AutoWire();
    }
    void OnValidate()
    {
        DriveScalpelStartAngle();
        PushParameters();
    }
    void OnEnable()
    {
        // the registry maps bodies back to their cuts by one scene sweep; tell it the set moved.
        CutRegistry.Invalidate();
    }
    void OnDisable()
    {
        CutRegistry.Invalidate();

        // a manager torn down or disabled mid-cut would otherwise leave the loop sounding
        // with nothing left holding a reference to stop it.
        StopCutSound();
    }
    void OnDestroy()
    {
        // the preview mesh is generated, not an asset: nothing else will collect it.
        ReleaseSeveredPreview();

        // null them out too: statics survive a scene reload, and the `== null` guards in
        // InstantiateActions would otherwise keep a disposed action.
        arrows?.Dispose();
        arrows = null;
        move?.Dispose();
        move = null;
        mouseDelta?.Dispose();
        mouseDelta = null;
    }

    public CuttingState getState()
    {
        return state;
    }

    public bool canEnterMinigame()
    {
        return state != CuttingState.COMPLETED && !isPlaying;
    }

    // ---- the region this cut removes ----
    //
    // Before the cut runs there is no Lower_Hull object to point at: CuttableObject only spawns
    // those in Weld(), at completion. So the piece is previewed by running the real slice against
    // this cut's plane and keeping the mesh, without assigning anything. A plane half-space test
    // would NOT do: the real piece is bounded by the finite window and by mesh connectivity, so an
    // infinite plane claims every limb it happens to pass through.

    /// <summary>The cutting plane this cut runs on, or null when the loop guide has none.</summary>
    public CutPlane CutPlane => loopGuide != null ? loopGuide.plane : null;

    /// <summary>Mesh of the piece this cut would sever, in the body's local space. Null when the cut severs nothing.</summary>
    private Mesh severedPreview;

    // signature the cached preview was built for; any change re-slices
    private Matrix4x4 previewPlanePose;
    private Matrix4x4 previewBodyPose;
    private Mesh previewSourceMesh;
    private Vector2 previewWindow;
    private bool previewBuilt;

    /// <summary>The piece this cut would take off, as a mesh in the body's local space. Rebuilt only when the plane, the body or the window moves.</summary>
    /// <remarks>
    /// Slicing a whole body is not cheap, so the first read after a change costs a hitch. Every
    /// read in between is a matrix comparison. Callers that only need containment should prefer
    /// <see cref="RegionContains"/>, which is bounds-only once this is built.
    /// </remarks>
    public Mesh SeveredPreviewMesh
    {
        get
        {
            RefreshSeveredPreview();
            return severedPreview;
        }
    }

    /// <summary>Re-slices the preview when its inputs have moved, and does nothing otherwise.</summary>
    void RefreshSeveredPreview()
    {
        CutPlane plane = CutPlane;
        if (GameObjectBeingCut == null || plane == null)
        {
            ReleaseSeveredPreview();
            return;
        }

        Mesh sourceMesh = GameObjectBeingCut.TryGetComponent<MeshFilter>(out var filter) ? filter.sharedMesh : null;
        if (sourceMesh == null)
        {
            ReleaseSeveredPreview();
            return;
        }

        Matrix4x4 planePose = plane.transform.localToWorldMatrix;
        Matrix4x4 bodyPose = GameObjectBeingCut.transform.localToWorldMatrix;
        Vector2 window = plane.boundsSize;

        // a slice swaps sharedMesh in place without moving anything, so the mesh identity has to
        // be part of the signature, not just the two poses.
        if (previewBuilt
            && planePose == previewPlanePose
            && bodyPose == previewBodyPose
            && sourceMesh == previewSourceMesh
            && window == previewWindow)
        {
            return;
        }

        ReleaseSeveredPreview();

        List<Mesh> pieces = GameObjectBeingCut.PreviewLowerHulls(plane);
        if (pieces.Count > 0)
        {
            severedPreview = pieces[0];

            // the preview allocates a mesh per piece; keep the one we show and drop the rest,
            // or a mis-authored window leaks a mesh on every rebuild.
            for (int i = 1; i < pieces.Count; i++)
            {
                DestroyMesh(pieces[i]);
            }
        }

        previewPlanePose = planePose;
        previewBodyPose = bodyPose;
        previewSourceMesh = sourceMesh;
        previewWindow = window;
        previewBuilt = true;
    }

    /// <summary>Frees the cached preview mesh and marks the cache empty.</summary>
    void ReleaseSeveredPreview()
    {
        if (severedPreview != null)
        {
            DestroyMesh(severedPreview);
            severedPreview = null;
        }
        previewBuilt = false;
    }

    /// <summary>Destroys a runtime-generated mesh, using the call that works in the current mode.</summary>
    static void DestroyMesh(Mesh mesh)
    {
        if (mesh == null) return;
        if (Application.isPlaying) Destroy(mesh);
        else DestroyImmediate(mesh);
    }

    /// <summary>Signed distance from a world point to the cutting plane. Negative = inside the piece this cut removes.</summary>
    /// <returns><c>float.PositiveInfinity</c> when there is no plane, so a plane-less cut never wins a containment test.</returns>
    public float SignedDistanceToPlane(Vector3 worldPoint)
    {
        CutPlane plane = CutPlane;
        return plane == null ? float.PositiveInfinity : plane.SignedDistance(worldPoint);
    }

    /// <summary>Whether a world point lies in the piece this cut removes.</summary>
    /// <remarks>
    /// Two tests, both cheap once the preview is built: the point must be on the lower-hull side of
    /// the plane, and inside the severed piece's own bounds. The bounds are what make this agree
    /// with the real cut where a bare half-space would not -- a shoulder plane extended across the
    /// body passes through the far arm too, but the severed piece's bounds do not reach it.
    /// <para>
    /// Still an approximation: the bounds are an axis-aligned box, so a diagonal limb claims a
    /// little space around itself. Exact would mean a point-in-mesh test per candidate per frame.
    /// </para>
    /// </remarks>
    public bool RegionContains(Vector3 worldPoint)
    {
        if (SignedDistanceToPlane(worldPoint) >= 0f)
        {
            return false;
        }

        Mesh piece = SeveredPreviewMesh;
        if (piece == null)
        {
            // nothing would be severed here, so nothing to point at
            return false;
        }

        // the piece mesh is in the body's local space, the same space SpawnPiece hands it to
        Vector3 localPoint = GameObjectBeingCut.transform.InverseTransformPoint(worldPoint);
        return piece.bounds.Contains(localPoint);
    }

    /// <summary>Whether this cut's region wholly contains another's, judged by where the other's plane sits.</summary>
    /// <remarks>
    /// A wrist plane sits inside the arm the shoulder cut would remove, so the shoulder region
    /// contains the wrist region. That is what lets a hit on the hand pick the wrist cut over the
    /// shoulder cut: both contain the point, only one of them contains the other.
    /// </remarks>
    public bool RegionContainsCutOf(CuttingManager other)
    {
        if (other == null || other == this)
        {
            return false;
        }
        CutPlane otherPlane = other.CutPlane;
        return otherPlane != null && RegionContains(otherPlane.Origin);
    }

    /// <summary>Whether the player is holding the tool this cut needs. Always true when no tool is named.</summary>
    public bool HasRequiredTool(PlayerInventoryandInteraction inventory)
    {
        if (string.IsNullOrWhiteSpace(requiredToolName))
        {
            return true;
        }
        return inventory != null
            && inventory.isHoldingItem
            && string.Equals(inventory.heldItemName, requiredToolName.Trim(), System.StringComparison.OrdinalIgnoreCase);
    }


}
