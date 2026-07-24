using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Orbits the camera around the centre of the cut loop, in the cutting plane.</summary>

public class CuttingSkin : MonoBehaviour {
    public InputActionAsset cameraControl; 
    private InputAction move;

    /// <summary>Arrow-key drive, built in code so it needs no entry in the input asset. Same effect as the wheel.</summary>
    private InputAction arrows;

    [Tooltip("Time for the camera to complete one full loop (360 deg) at top speed, in seconds. The speed cap derives from this.")]
    public float secondsPerLoop = 12f;

    public CameraMovesPreset preset;

    /// <summary>Seconds since the last push; friction only applies past <c>coastTime</c>.</summary>
    private float idleTimer;

    [ReadOnly] public float currentSpeed;

    public bool allowBothDirection = false;

    public int DirectionMainScroll  =1;

    public CameraFollow follow;
    

    [Tooltip("Lays the player's cut line; ticked only while the camera is moving.")]
    public CutTracer tracer;

    

    void Start()
    {
        move = cameraControl.FindAction("MouseScroll");
        move.Enable();


        
        // arrow keys as a 2D vector, same role as the wheel; built here so the input asset needs no change
        arrows = new InputAction("Arrows", InputActionType.Value, expectedControlType: "Vector2");
        arrows.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/downArrow")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/rightArrow");
        arrows.Enable();
    }

    void OnDestroy()
    {
        arrows?.Dispose();
    }

    void Update()
    {
        UpdateCameraSpeed();
        // draw while the camera is actually moving, not only on push frames, so coasting
        // between wheel ridges still lays a continuous cut
        if (currentSpeed > 0f) tracer.Trace();
    }

    void UpdateCameraSpeed()
    {
        float scroll = move.ReadValue<Vector2>().y;
        float keys = arrows.ReadValue<Vector2>().y;
        

        bool pushed = false;

        bool sameDirection = Mathf.Sign(scroll) == Mathf.Sign(DirectionMainScroll);
        // mouse-wheel ridge = one discrete kick (impulse), when it pushes along travel dir
        if (Mathf.Abs(scroll) > 0.01f &&( sameDirection || allowBothDirection))
        {
            Debug.Log(Mathf.Sign(scroll) + " " +Mathf.Sign(DirectionMainScroll) );
            currentSpeed += preset.wheelKick * Mathf.Sign(scroll) * Mathf.Sign(DirectionMainScroll);
            pushed = true;
        }

        // arrow key held = continuous push
        float keyFwd = keys * DirectionMainScroll;
        if (keyFwd > 0f)
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
            if (idleTimer >= preset.coastTime &&  Mathf.Sign(currentSpeed) == Mathf.Sign(DirectionMainScroll) )
            {
                currentSpeed += preset.deceleration * Time.deltaTime;
            }
        }

        currentSpeed = Mathf.Clamp(currentSpeed, -preset.MaxSpeed, preset.MaxSpeed);

        follow.rotationSpeed = DirectionMainScroll * currentSpeed;
    }

  
}
