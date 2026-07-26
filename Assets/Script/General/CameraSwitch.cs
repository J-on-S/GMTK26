using UnityEngine;

public class CameraSwitch : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Camera otherCamera;

    private void Start()
    {
        mainCamera.enabled = true;
        otherCamera.enabled = false;
    }

    public void SwitchToOtherCamera()
    {
        mainCamera.enabled = false;
        otherCamera.enabled = true;
    }

    public void SwitchToMainCamera()
    {
        otherCamera.enabled = false;
        mainCamera.enabled = true;
    }
}
