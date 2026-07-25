using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Manages the cut travel speed: turns wheel/key input into <see cref="currentSpeed"/>, with coast and friction. Owns nothing else -- consumers (camera, tracer) read the speed.</summary>

public class CutSpeedDriver : MonoBehaviour, ISpeedSource {
    public CameraMovesPreset preset;

    /// <summary>Seconds since the last push; friction only applies past <c>coastTime</c>.</summary>
    private float idleTimer;

    [ReadOnly] public float currentSpeed;

    public bool canGoBackwards = false;

    public bool canDecelerateManually = false;

    public int DirectionMainScroll  =1;

    /// <summary>Speed signed by the main travel direction; what a follower orbits at. Consumers read this instead of being pushed to.</summary>
    void Update()
    {
        UpdateCameraSpeed();
    }

    /// <summary>Whether the player is pushing against the main cut direction this frame, on either input (wheel ridge or held key). Always <c>false</c> when backward travel is allowed.</summary>
    public bool IsPushingBackward()
    {
        if (canGoBackwards) return false;

        float scroll = CuttingManager.move != null ? CuttingManager.move.ReadValue<Vector2>().y : 0f;
        if (Mathf.Abs(scroll) > 0.01f && Mathf.Sign(scroll) != Mathf.Sign(DirectionMainScroll)) return true;

        float keys = CuttingManager.arrows != null ? CuttingManager.arrows.ReadValue<Vector2>().y : 0f;
        return keys * DirectionMainScroll < 0f;
    }

    void UpdateCameraSpeed()
    {
        float scroll = CuttingManager.move.ReadValue<Vector2>().y;
        float keys = CuttingManager.arrows.ReadValue<Vector2>().y;


        bool pushed = false;

        bool sameDirection = Mathf.Sign(scroll) == Mathf.Sign(DirectionMainScroll);
        // mouse-wheel ridge = one discrete kick (impulse), when it pushes along travel dir
        if (Mathf.Abs(scroll) > 0.01f &&( sameDirection || canGoBackwards))
        {
            currentSpeed += preset.wheelKick * Mathf.Sign(scroll) * Mathf.Sign(DirectionMainScroll);
            pushed = true;
        }

        // arrow key held = continuous push
        float keyFwd = keys * DirectionMainScroll;
        sameDirection = Mathf.Sign(keys) == Mathf.Sign(DirectionMainScroll);
        if (Mathf.Abs(keys) > 0 &&( sameDirection || canGoBackwards) )
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
        float minSpeed = canGoBackwards? -preset.MaxSpeed : 0;
        currentSpeed = Mathf.Clamp(currentSpeed, minSpeed, preset.MaxSpeed);
    }

    public void Reset()
    {
        idleTimer  =0;
        currentSpeed = 0;
    }

    public float GetSignedSpeed()
    {
        return DirectionMainScroll * currentSpeed;
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
