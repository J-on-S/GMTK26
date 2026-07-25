using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

// runs before CameraFollow so the scalpel angle it sets is consumed the same frame.
// ExecuteAlways so the scalpel start-angle lead previews in edit mode.
[ExecuteAlways]
[DefaultExecutionOrder(-10)]
public class CuttingManager : MonoBehaviour
{

// this script will manage one cutting of a body part. 
// need: 
/*

// line renderer controlled.

need to track the progress.
*/
public enum CuttingState
    {
        NOT_STARTED,

        PROGRESSING,
        COMPLETED
    }

    /// <summary>Where the camera rig is, which is a separate axis from <see cref="CuttingState"/> (how far the cut got). Entering and Exiting are the travel phases: input is dead and the camera is being flown by this manager, not by <see cref="CameraFollow"/> or <see cref="MoveCamera"/>.</summary>
    public enum RigPhase
    {
        /// <summary>Free-look. The only phase a cut can be entered from.</summary>
        Free,
        /// <summary>Flying from free-look to the orbit position.</summary>
        Entering,
        /// <summary>Orbiting; the player is cutting.</summary>
        Cutting,
        /// <summary>Flying back to the pose free-look was left in.</summary>
        Exiting,
    }

    public bool useUpperHull = false;

    [Tooltip("the name of the tool you need to operate on this")]
    public string toolNeeded= "";
    public string bodyPartName= "";
    public float startAngle;

    public float endAngle= 360; 

    [ReadOnly] public float currentAngle;

    [ReadOnly] public float currentProgress =>(scalpelFollow.Angle  - startAngle) / (endAngle - startAngle); // normalized 0-1

    [ReadOnly] public RigPhase phase = RigPhase.Free;

    /// <summary>True only while the player actually has the cut; the travel phases don't count.</summary>
    bool isPlaying => phase == RigPhase.Cutting;

    public CuttableObject GameObjectBeingCut;

    // Snapshot of the free-look camera pose, by value: holding the Transform itself would just
    // alias the live camera, so restoring would assign every field to itself.
    private Vector3 initialCameraPos;
    private Quaternion initialCameraRot;
    private float initialcameraFOV;

    [Tooltip("Camera field of view while cutting, in degrees. The free-look FOV is snapshotted on enter and put back on quit.")]
    public float cameraFOV = 40f;

    [Header("Camera travel")]
    [Tooltip("Seconds the camera takes to fly from free-look to the orbit position. 0 = snap.")]
    public float enterTravelTime = 0.6f;

    [Tooltip("Seconds the camera takes to fly back to the free-look pose. 0 = snap.")]
    public float exitTravelTime = 0.45f;

    [Tooltip("Shapes the travel over its duration. Position, aim and FOV all ride this one curve, so they arrive together.")]
    public AnimationCurve travelEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    // Pose the current travel started from, captured on the frame it began. Lerping from a
    // stored start (rather than from the live transform) keeps the arrival exact.
    private Vector3 travelFromPos;
    private Quaternion travelFromRot;
    private float travelFromFOV;

    // Pose the fly-in is heading to. Resolved once (see TryResolveEnterTarget) rather than
    // recomputed per frame: a moving destination makes the ease meaningless, and reading the
    // aim back off the camera being written would freeze the rotation at its start value.
    private Vector3 travelToPos;
    private Quaternion travelToRot;
    private bool travelToResolved;

    /// <summary>Travel progress, 0..1, raw (before <see cref="travelEase"/>).</summary>
    private float travelT;

    public Camera sceneCamera;

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
    [Tooltip("The main camera's CameraFollow -- the live orbit angle the scalpel slaves to.")]
    public CameraFollow cameraFollow;

    [Tooltip("The scalpel's CameraFollow -- its orbit angle is driven here, not self-advanced.")]
    public CameraFollow scalpelFollow;

    [Tooltip("Speed driver, used only to freeze the scalpel on frames the player pushes against the cut direction.")]
    public CutSpeedDriver speedDriver;

    [Tooltip("Fixed angular gap (deg) the scalpel keeps ahead of the camera.")]
    public float scalpelAngleLead;


    public event Action OnMinigameEntered;
    public event Action OnMinigameCompleted;

    public event Action OnMinigameQuit;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

        // seed the snapshot: the RestoreRig below reads it before any SetupRig has run.
        CaptureCameraState();

        // seed the kept progress, so the first entry opens at startAngle and not at 0.
        currentAngle = startAngle;
        PushParameters();

        // ExecuteAlways also runs this in edit mode; input + camera setup is play-only.
        if (!Application.isPlaying) return;

        InstantiateActions();
        if(sceneCamera != null)
            {
                moveCamera.c = sceneCamera;
            }

        // park the rig in free-look; not QuitMinigame, which would report quitting a game
        // that never started and fire OnMinigameQuit at every listener on load. Instant, not
        // a travel: there is no pose to fly back from on load.
        ParkRigInstant();
    }
    /// <summary>Holds the scalpel's orbit start-angle a fixed lead ahead of the camera's, live in edit mode so the follower previews before play.</summary>
    void DriveScalpelStartAngle()
    {
        if (scalpelFollow == null || cameraFollow == null) return;
        scalpelFollow.startAngle = cameraFollow.startAngle + scalpelAngleLead;
    }

    // Update is called once per frame
    void Update()
    {

        // edit-mode + live preview of the scalpel's start-angle lead.


        if (!Application.isPlaying) return;

        switch (phase)
        {
            case RigPhase.Entering:
                TickEnter();
                break;

            case RigPhase.Exiting:
                TickExit();
                break;

            case RigPhase.Cutting:
                DriveScalpelStartAngle();
                SyncScalpel();

                // the camera's orbit angle IS the cut progress; mirror it so currentProgress reads it.
                if (cameraFollow != null) currentAngle = cameraFollow.Angle;

                if (currentProgress >= 1) HandleCompletion();
                // edge, not held: a held Q would quit again the instant the player re-entered.
                else if (Keyboard.current.qKey.wasPressedThisFrame)
                {
                    QuitMinigame();
                }
                break;
        }
    }

    /// <summary>Flies the camera from the free-look pose to the orbit position, easing position, aim and FOV together. The orbit is parked at the kept progress while this runs, so the destination doesn't drift out from under the lerp.</summary>
    void TickEnter()
    {
        // hold the orbit at the kept progress so it hands over at exactly the angle the
        // travel aimed for. It owns neither position nor rotation yet, and the speed driver
        // is still off, so nothing else moves.
        if (cameraFollow != null) cameraFollow.Angle = currentAngle;

        // normally already resolved in SetupRig; this retries on the rare frame the guide had
        // no loop to give (the plane momentarily missing the mesh).
        if (!travelToResolved && !TryResolveEnterTarget())
        {
            if (cameraFollow == null || cameraFollow.loopGuide == null)
            {
                // nothing to orbit: the cut itself can't work either, so land immediately
                // rather than stranding the player in a travel that will never arrive.
                Debug.LogError("CuttingManager: no cameraFollow/loopGuide, skipping the fly-in.", this);
                CompleteEnter();
            }

            // hold: lerping toward an unresolved target would fly the camera at the origin.
            return;
        }

        float e = Advance(enterTravelTime);
        ApplyTravel(travelToPos, travelToRot, cameraFOV, e);

        if (travelT >= 1f) CompleteEnter();
    }

    /// <summary>Flies the camera back to the pose free-look was left in, easing position, aim and FOV together.</summary>
    void TickExit()
    {
        float e = Advance(exitTravelTime);

        ApplyTravel(initialCameraPos, initialCameraRot, initialcameraFOV, e);

        if (travelT >= 1f) CompleteExit();
    }

    /// <summary>Steps <see cref="travelT"/> by one frame over <paramref name="duration"/> and returns the eased value. A duration of 0 arrives immediately.</summary>
    float Advance(float duration)
    {
        travelT = duration > 0f
            ? Mathf.Min(1f, travelT + Time.deltaTime / duration)
            : 1f;

        return travelEase != null && travelEase.length > 0
            ? travelEase.Evaluate(travelT)
            : travelT;
    }

    /// <summary>Writes the camera one step along the current travel: position, aim and FOV all off the same eased <paramref name="e"/>, all measured from the pose the travel started at.</summary>
    void ApplyTravel(Vector3 toPos, Quaternion toRot, float toFOV, float e)
    {
        sceneCamera.transform.SetPositionAndRotation(
            Vector3.Lerp(travelFromPos, toPos, e),
            Quaternion.Slerp(travelFromRot, toRot, e));

        sceneCamera.fieldOfView = Mathf.Lerp(travelFromFOV, toFOV, e);
    }

    /// <summary>Locks in where the fly-in is heading: the orbit position <see cref="CameraFollow"/> would hold at the kept progress, aimed at the cut's centre and rolled level with the cutting plane. Resolved once and reused, so the destination can't drift mid-travel and the aim can't be read back off the camera it is writing to.</summary>
    /// <returns><c>false</c> while the guide has no loop yet; nothing is written and the caller should hold.</returns>
    bool TryResolveEnterTarget()
    {
        if (cameraFollow == null) return false;

        // ask the orbit itself rather than reading its published BasePosition: that is only
        // written by its Update, which has not run since SetupRig enabled it (it sits at
        // execution order 0, this manager at -10), so it would still hold a stale pose. This
        // also hands back the orbit's real aim -- roll, loopTowardTop, lookMode and pivot
        // included -- so the handover at the end of the travel is seamless.
        if (!cameraFollow.TryGetPose(currentAngle, out travelToPos, out travelToRot)) return false;

        travelToResolved = true;
        return true;
    }

   
    /// <summary>Slaves the scalpel's orbit angle to the camera's, a fixed lead ahead. Frozen on frames the player pushes against the cut direction.</summary>


    [ContextMenu("StartMinigame")]
    public void EnterMinigame()
    {
        // Free, not just "not Cutting": entering mid-travel would fight the lerp.
        if( state == CuttingState.COMPLETED || phase != RigPhase.Free) return;
        OnMinigameEntered?.Invoke();
        Debug.LogWarning("entering minigame");

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

        RestoreRig();

        // fired when the player loses the cut, not when the camera lands: the run is already over.
        OnMinigameQuit?.Invoke();
    }

    /// <summary>Starts the flight into the cut: stores what <see cref="RestoreRig"/> puts back, takes the camera off free-look, and lets <see cref="CameraFollow"/> compute the destination without yet driving anything. Control is handed over in <see cref="CompleteEnter"/>. Mirror of <see cref="RestoreRig"/>; keep the two in step.</summary>
    void SetupRig()
    {
        // remember the free-look camera state so quitting can put it back.
        CaptureCameraState();

        // free-look off for the whole travel; its look would fight the lerp.
        moveCamera.enabled = false;

        // orbit on, but driving nothing: the travel owns the transform until it lands. The
        // destination is asked for directly (TryGetPose), so this does not need to have run.
        cameraFollow.enabled = true;
        cameraFollow.controlPosition = false;
        cameraFollow.controlRotation = false;

        // CameraFollow.OnEnable re-seeds its angle from startAngle, and the enable above fires it,
        // so re-entering would rewind the cut. currentAngle is the kept progress: put it back.
        cameraFollow.Angle = currentAngle;
        if (scalpelFollow != null) scalpelFollow.Angle = currentAngle + scalpelAngleLead;

        // scalpel angle is driven by SyncScalpel; stop its CameraFollow from self-advancing.
        if (scalpelFollow != null) scalpelFollow.rotationSpeed = 0f;

        // the speed driver and the trail stay parked until the camera lands, so the player
        // can't scroll the cut forward (or paint a trail) during the fly-in.
        BeginTravel(RigPhase.Entering);

        // lock the destination now: TryGetPose computes it outright, so there is nothing to
        // wait for and the first travelled frame already moves.
        TryResolveEnterTarget();
    }

    /// <summary>Hands the rig over at the end of the fly-in: <see cref="CameraFollow"/> takes the camera back and the player gets the cut.</summary>
    void CompleteEnter()
    {
        cameraFollow.controlPosition = true;
        cameraFollow.controlRotation = true;
        cameraFollow.Angle = currentAngle;

        // zero it first: the driver keeps its speed across a disable, so a quit mid-cut would
        // otherwise hand the next entry the speed it was carrying.
        speedDriver.SetSignedSpeed(0);
        speedDriver.Enable();

        SetScalpelTrace(true);

        phase = RigPhase.Cutting;
    }


    [ContextMenu("Reset the cut")]
    /// <summary>Rewinds the cut to <c>startAngle</c>: orbit angles, progress, travel speed and the scalpel's trail. Called on every entry, so quitting always costs the run.</summary>
    void ResetCut()
    {
        currentAngle = startAngle;

        // set the live angles, not just startAngle: CameraFollow only re-seeds itself in
        // OnEnable, which doesn't fire when the rig is already enabled.
        if (cameraFollow != null) cameraFollow.Angle = startAngle;
        if (scalpelFollow != null) scalpelFollow.Angle = startAngle + scalpelAngleLead;

        if (speedDriver != null) speedDriver.SetSignedSpeed(0);

        if (scalpelFollow != null
            && scalpelFollow.TryGetComponent<LoopFollowingObject>(out var scalpelLoop))
        {
            scalpelLoop.ResetTrace();
        }
    }

    /// <summary>Starts the flight back out: takes the camera off the orbit and parks the cut inputs. Free-look is not handed back until the camera lands, in <see cref="CompleteExit"/>. Mirror of <see cref="SetupRig"/>; keep the two in step.</summary>
    void RestoreRig()
    {
        // the travel owns the camera from here; the orbit must not keep writing to it.
        cameraFollow.enabled = false;

        speedDriver.SetSignedSpeed(0);
        speedDriver.Disable();

        SetScalpelTrace(false);

        // moveCamera stays off for the whole travel -- see CompleteExit.
        BeginTravel(RigPhase.Exiting);
    }

    /// <summary>Hands the camera back to free-look at the end of the fly-out, snapping to the captured pose so rounding in the lerp can't leave it a hair off.</summary>
    void CompleteExit()
    {
        sceneCamera.transform.SetPositionAndRotation(initialCameraPos, initialCameraRot);
        sceneCamera.fieldOfView = initialcameraFOV;

        // only now: MoveCamera writes the camera's rotation every frame from its own kept
        // yaw/pitch, so enabling it any earlier would snap the aim and cancel the travel.
        moveCamera.enabled = true;

        phase = RigPhase.Free;
    }

    /// <summary>Opens a travel: captures the pose it starts from and rewinds the timer.</summary>
    void BeginTravel(RigPhase travelPhase)
    {
        travelFromPos = sceneCamera.transform.position;
        travelFromRot = sceneCamera.transform.rotation;
        travelFromFOV = sceneCamera.fieldOfView;
        travelT = 0f;

        // the fly-in resolves its own destination on a later frame; the fly-out already knows
        // its one (the captured free-look pose), so this only matters to Entering.
        travelToResolved = false;

        phase = travelPhase;
    }

    /// <summary>Puts the rig in free-look with no travel, for load time -- there is no pose to fly back from before the first cut.</summary>
    void ParkRigInstant()
    {
        cameraFollow.enabled = false;
        speedDriver.SetSignedSpeed(0);
        speedDriver.Disable();
        SetScalpelTrace(false);

        CompleteExit();
    }

    /// <summary>Snapshots the camera's current pose and FOV, the state <see cref="RestoreRig"/> returns it to.</summary>
    void CaptureCameraState()
    {
        initialCameraPos = sceneCamera.transform.position;
        initialCameraRot = sceneCamera.transform.rotation;
        initialcameraFOV = sceneCamera.fieldOfView;
    }

    /// <summary>Turns the scalpel's surface trail on or off, if it has a follower.</summary>
    void SetScalpelTrace(bool on)
    {
        if (scalpelFollow == null) return;
        if (scalpelFollow.TryGetComponent<LoopFollowingObject>(out var scalpelLoop))
        {
            scalpelLoop.enabled = on;
            scalpelLoop.drawTrace = on;
        }
    }

    void SyncScalpel()
    {
        if (scalpelFollow == null || cameraFollow == null) return;

        // freeze: don't advance the scalpel when scrolling against the main cut direction.
        if (speedDriver != null && speedDriver.IsPushingBackward()) return;

        // set before CameraFollow.Update (which runs after this, at order 0) so BasePosition uses it this frame.
        scalpelFollow.Angle = cameraFollow.Angle + scalpelAngleLead;
    }

    [ContextMenu("HandleCompletion")]
    void HandleCompletion()
    {
        Debug.LogError("minigameCompleted");
        state = CuttingState.COMPLETED;
        // null, not empty, is what SpliceWindowed returns when the slice or the weld fails.
        List<GameObject> cutted = GameObjectBeingCut.SpliceWindowed();
        int pieceCount = cutted != null ? cutted.Count : 0;

        if (pieceCount != 1) Debug.LogError("invalid num cutObjects: " + pieceCount);

        if (useUpperHull)
        {
            // the upper hull stays on the cuttable itself -- this manager's own GameObject is
            // the rig, not the mesh.
            InstantiateBodyPart(GameObjectBeingCut.gameObject);
        }
        else
        {
            // still quit the rig on a failed cut; returning here would strand the player in it.
            if (pieceCount > 0) InstantiateBodyPart(cutted[0]);
        }


        QuitMinigame();
        // instantiate the BodyPart
        OnMinigameCompleted?.Invoke();
    }


    void InstantiateBodyPart(GameObject bodyPart)
    {
        // should call a method that someone will provide me
    }

/// <summary>This manager owns the tuning; it pushes its presets + wiring down into the loop guide, both CameraFollows and the cutting speed driver so they can't drift apart. Live in edit mode too.</summary>
    void PushParameters()
    {
        // loop guide: target + curve shape.
        if (loopGuide != null)
        {
            if (GameObjectBeingCut != null) loopGuide.meshFollow = GameObjectBeingCut.gameObject;
            if (curvePreset != null) loopGuide.preset = curvePreset;
        }

        // cutting speed driver reads the camera-moves preset.
        if (speedDriver != null && cameraPreset != null) speedDriver.preset = cameraPreset;

        // main camera: orbit this guide, travelling at the speed driver's speed, opening the cut
        // at this manager's startAngle -- the same angle currentProgress is measured from.
        if (cameraFollow != null)
        {
            if (loopGuide != null) cameraFollow.loopGuide = loopGuide;
            cameraFollow.SetSpeedSource(speedDriver);
            cameraFollow.startAngle = startAngle;
        }

        // scalpel: same guide, and its along-limb follow tuning. Its speed source stays null --
        // its angle is slaved by SyncScalpel, a fixed lead ahead of the camera.
        if (scalpelFollow != null)
        {
            if (loopGuide != null) scalpelFollow.loopGuide = loopGuide;
            scalpelFollow.SetSpeedSource(null);

            if (ScalpelFollowLoopPreset != null
                && scalpelFollow.TryGetComponent<LoopFollowingObject>(out var scalpelLoop))
            {
                scalpelLoop.preset = ScalpelFollowLoopPreset;
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
    void OnValidate()
    {
        DriveScalpelStartAngle();
        PushParameters();
    }
    void OnDestroy()
    {
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

    public bool canEnterMinigame(string toolName)
    {
        // Free, not !isPlaying: during a travel the camera is mid-lerp and can't be handed over.
        return state != CuttingState.COMPLETED && phase == RigPhase.Free && toolNeeded.Equals(toolName);
    }
}
