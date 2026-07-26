using UnityEngine;

public class ReturnMain : MonoBehaviour
{
    [SerializeField] private CameraSwitch cameraSwitch;
    private void Update()
    {
        // Left mouse click
        if (Input.GetMouseButtonDown(0))
        {
            cameraSwitch.SwitchToMainCamera();
            this.enabled = false;
        }
        
    }
}
