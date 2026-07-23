using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class DemoPlayerController : MonoBehaviour
{
    private Transform _camera;
    private Vector3 _offsetPosition;
    private InputAction _moveAction;
    
    private void Start()
    {
        _offsetPosition = _camera.position - transform.position;
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void OnEnable()
    {
        var camera = Camera.main;
        if (camera != null)
        {
            _camera = camera.transform;
        }
        else
        {
            Debug.LogError("Main camera missing");
        }
        var map = new InputActionMap("Demo Player Controller");
        _moveAction = map.AddAction("move", binding: "<Gamepad>/leftStick");
        _moveAction.AddCompositeBinding("Dpad")
            .With("Up", "<Keyboard>/w")
            .With("Up", "<Keyboard>/upArrow")
            .With("Down", "<Keyboard>/s")
            .With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/a")
            .With("Left", "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/d")
            .With("Right", "<Keyboard>/rightArrow");
        _moveAction.Enable();
    }

    private void FixedUpdate()
    {
        var moveDelta = _moveAction.ReadValue<Vector2>() * (5.0f * Time.deltaTime);
        transform.position += moveDelta.x * _camera.right + moveDelta.y * _camera.forward;
        
        var mouseDelta = Mouse.current.delta.ReadValue();
        var rotationDelta = mouseDelta.x * Vector3.up - mouseDelta.y * Vector3.right;
        _camera.localRotation = Quaternion.Euler(_camera.localEulerAngles + rotationDelta);
    }

    private void LateUpdate()
    {
        _camera.position = transform.position + _offsetPosition;
    }
}
