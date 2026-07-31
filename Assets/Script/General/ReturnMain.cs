using UnityEngine;

public class ReturnMain : MonoBehaviour
{
    [SerializeField] private CameraSwitch cameraSwitch;
    private void Update()
    {
        // Left mouse click
        if (Input.GetMouseButtonDown(0))
        {
            CameraSwitch.Instance.SwitchCamera();
            this.enabled = false;
        }
        
    }
}
