using System;
using UnityEngine;

public enum CameraViewState
{
    MainGame,
    BlackMarket
}

public class CameraSwitch : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Camera otherCamera;

    [Header("Black-market state")]
    [SerializeField] private bool pauseGameInBlackMarket = true;
    [ReadOnly, SerializeField]
    private CameraViewState currentState =
        CameraViewState.MainGame;

    private float timeScaleBeforeBlackMarket = 1f;
    private bool ownsBlackMarketPause;

    public CameraViewState CurrentState => currentState;
    public bool IsBlackMarketOpen =>
        currentState == CameraViewState.BlackMarket;

    public event Action<CameraViewState> ViewStateChanged;

    private void Start()
    {
        SetCameraEnabled(mainCamera, true);
        SetCameraEnabled(otherCamera, false);
        currentState = CameraViewState.MainGame;
    }

    private void Update()
    {
        // ToolRequestManager and the other gameplay timers use
        // Time.deltaTime. Keeping the global time scale at zero freezes those
        // timers, doctor movement, decay, and other scaled gameplay systems.
        if (IsBlackMarketOpen &&
            pauseGameInBlackMarket &&
            !Mathf.Approximately(Time.timeScale, 0f))
        {
            Time.timeScale = 0f;
        }
    }

    private void OnDisable()
    {
        RestoreTimeScale();
    }

    public void SwitchToOtherCamera()
    {
        SetCameraEnabled(mainCamera, false);
        SetCameraEnabled(otherCamera, true);

        if (IsBlackMarketOpen)
            return;

        if (pauseGameInBlackMarket)
        {
            timeScaleBeforeBlackMarket = Time.timeScale;
            ownsBlackMarketPause = true;
            Time.timeScale = 0f;
        }

        currentState = CameraViewState.BlackMarket;
        ViewStateChanged?.Invoke(currentState);
        Debug.Log(
            "Entered BlackMarket state. Scaled gameplay and doctor " +
            "request timers are paused.",
            this);
    }

    public void SwitchToMainCamera()
    {
        SetCameraEnabled(otherCamera, false);
        SetCameraEnabled(mainCamera, true);

        if (!IsBlackMarketOpen)
            return;

        RestoreTimeScale();
        currentState = CameraViewState.MainGame;
        ViewStateChanged?.Invoke(currentState);
        Debug.Log(
            "Returned to MainGame state. Scaled gameplay and doctor " +
            "request timers resumed.",
            this);
    }

    private void RestoreTimeScale()
    {
        if (!ownsBlackMarketPause)
            return;

        Time.timeScale = timeScaleBeforeBlackMarket;
        ownsBlackMarketPause = false;
    }

    private static void SetCameraEnabled(
        Camera targetCamera,
        bool enabled)
    {
        if (targetCamera != null)
            targetCamera.enabled = enabled;
    }
}
