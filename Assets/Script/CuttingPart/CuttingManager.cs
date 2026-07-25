using System;
using UnityEngine;
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
    public float startAngle;

    public float endAngle= 360; 

    [ReadOnly] public float currentAngle;

    [ReadOnly] public float currentProgress =>(scalpelFollow.Angle  - startAngle) / (endAngle - startAngle); // normalized 0-1

    [ReadOnly] bool isPlaying = false;

    public CuttableObject GameObjectBeingCut;

    // Snapshot of the free-look camera pose, by value: holding the Transform itself would just
    // alias the live camera, so restoring would assign every field to itself.
    private Vector3 initialCameraPos;
    private Quaternion initialCameraRot;
    private float initialcameraFOV;

    [Tooltip("Camera field of view while cutting, in degrees. The free-look FOV is snapshotted on enter and put back on quit.")]
    public float cameraFOV = 40f;

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
        // that never started and fire OnMinigameQuit at every listener on load.
        RestoreRig();
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


        if (isPlaying)
        {
            DriveScalpelStartAngle();
            SyncScalpel();

            // the camera's orbit angle IS the cut progress; mirror it so currentProgress reads it.
            if (cameraFollow != null) currentAngle = cameraFollow.Angle;

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
        OnMinigameEntered?.Invoke();
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

        RestoreRig();

        OnMinigameQuit?.Invoke();
    }

    /// <summary>Switches the scene over to cutting: stores what <see cref="RestoreRig"/> puts back, hands the camera to the loop, and wakes the scalpel + speed driver. Mirror of <see cref="RestoreRig"/>; keep the two in step.</summary>
    void SetupRig()
    {
        // remember the free-look camera state so quitting can put it back.
        CaptureCameraState();

        // camera: free-look off, orbit on, cutting FOV.
        moveCamera.enabled = false;
        cameraFollow.enabled = true;
        sceneCamera.fieldOfView = cameraFOV;

        // CameraFollow.OnEnable re-seeds its angle from startAngle, and the enable above fires it,
        // so re-entering would rewind the cut. currentAngle is the kept progress: put it back.
        cameraFollow.Angle = currentAngle;
        if (scalpelFollow != null) scalpelFollow.Angle = currentAngle + scalpelAngleLead;

        // scalpel angle is driven by SyncScalpel; stop its CameraFollow from self-advancing.
        if (scalpelFollow != null) scalpelFollow.rotationSpeed = 0f;

        speedDriver.Enable();

        SetScalpelTrace(true);

       
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

    /// <summary>Undoes <see cref="SetupRig"/>: camera back where it was found, free-look on, speed driver parked. Mirror of <see cref="SetupRig"/>; keep the two in step.</summary>
    void RestoreRig()
    {
        // put the camera where it was found
        sceneCamera.transform.SetPositionAndRotation(initialCameraPos, initialCameraRot);
        sceneCamera.fieldOfView = initialcameraFOV;

        moveCamera.enabled = true; // reEnable playerMovement
        cameraFollow.enabled = false;


        speedDriver.SetSignedSpeed(0);
        speedDriver.Disable();

        SetScalpelTrace(false);
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
        GameObjectBeingCut.SpliceWindowed();
        QuitMinigame();
        // instantiate the BodyPart
        OnMinigameCompleted?.Invoke();
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

    public bool canEnterMinigame()
    {
        return state != CuttingState.COMPLETED && !isPlaying;
    }
}
