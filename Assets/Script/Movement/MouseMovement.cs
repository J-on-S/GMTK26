using UnityEngine;

/// <summary>Free-look: turns the camera with the mouse, and stands aside while a cut owns it.</summary>
/// <remarks>Invariant: the look is off from the moment a cut is entered until its camera has flown all the way back out.</remarks>
public class MouseMovement : MonoBehaviour
{
  [Tooltip("Degrees turned per hundred pixels of mouse movement.")]
  public float mouseSensitivity = 100f;

  float xRotation = 0f;
  float YRotation = 0f;

  /// <summary>Whether the mouse drives the camera; <c>false</c> for as long as a cut has it.</summary>
  public bool active = true;

  void Start(){
    //Locking the cursor to the middle of the screen and making it invisible
    Cursor.lockState = CursorLockMode.Locked;
    AdjustCamera();
  }

  void Update(){
    // owned by GameInputActions, built at startup, so it is live even in a scene with no cut
    if(GameInputActions.MouseDelta == null){
            Debug.Log("mouseDelta is null");
        }
    if( PauseMenu.IsPaused()){
            Debug.Log("Paused");
        }
    if(!active || GameInputActions.MouseDelta == null  || PauseMenu.IsPaused()) return;
    Vector2 move = GameInputActions.MouseDelta.ReadValue<Vector2>();
    
    
    float mouseX = move.x * mouseSensitivity * 0.01f;
    float mouseY = move.y * mouseSensitivity * 0.01f;


    xRotation -= mouseY;

  
    // prevent to rotate more than physically possible
    xRotation = Mathf.Clamp(xRotation, -90f, 90f);

    
    YRotation += mouseX;

    //applying both rotations
    transform.localRotation = Quaternion.Euler(xRotation, YRotation, 0f);
  }

    void OnEnable()
    {
        CuttingManager.OnMinigameEntered += Suspend;
        CuttingManager.OnMinigameExited += Resume;
    }

    void OnDisable()
    {
        CuttingManager.OnMinigameEntered -= Suspend;
        CuttingManager.OnMinigameExited -= Resume;
    }

    
    void Suspend(CuttingManager cm) => active = false;

    void Resume(CuttingManager cm)
    {
        Debug.Log("resuming for some reason");
      //AdjustCamera();
      // reapply our own stored rotation instead of reading it back from the transform
      transform.localRotation = Quaternion.Euler(xRotation, YRotation, 0f);
      active = true;
    }

    void AdjustCamera()
    {
        Vector3 euler = transform.localRotation.eulerAngles;

        xRotation = Mathf.Clamp(euler.x > 180f ? euler.x - 360f : euler.x, -90f, 90f);
        YRotation = euler.y;
    }
    // for making freeze during clients menu
    public void Pause(){ active = false; Debug.Log("MouseMovement Pause called, active = " + active); }
    
    public void Unpause() {
      transform.localRotation = Quaternion.Euler(xRotation, YRotation, 0f);
      active = true;
    } 
}
