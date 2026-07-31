using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public enum CameraType
{
    MainGame,
    BlackMarket,
    Doctor
}
[Serializable]
public class CameraViewState
{
    public CameraType cameraType;
    public Camera camera;
}

public class CameraSwitch : MonoBehaviour
{
    public static CameraSwitch Instance { get; private set; }
    [SerializeField] private List<CameraViewState> cameras;

    [Header("Black-market state")]
    [SerializeField] private bool pauseGameInBlackMarket = true;
    [ReadOnly, SerializeField]
    private CameraType currentStateType = CameraType.MainGame;

    private float timeScaleBeforeBlackMarket = 1f;
    private bool ownsBlackMarketPause;

    public CameraType CurrentState => currentStateType;
    public bool IsBlackMarketOpen =>
        currentStateType == CameraType.BlackMarket;

    public event Action<CameraType> ViewStateChanged;
    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }
    private void Start()
    {
        SwitchCamera();
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
            Debug.Log(
            "Stop ");
            Time.timeScale = 0f;
        }
    }

    private void OnDisable()
    {
        RestoreTimeScale();
    }
    // public void SwitchCamera(CameraType newCameraType = CameraType.MainGame)
    // {
    
    //     currentStateType = newCameraType;
    //     foreach(CameraViewState cameraState in cameras)
    //     {
    //         if(newCameraType == cameraState.cameraType)
    //         {
    //             cameraState.camera.enabled = true;
    //         }
    //         else
    //         {
    //             cameraState.camera.enabled = false;
    //         }
    //     }
    //     ViewStateChanged?.Invoke(currentStateType);

    //     if (currentStateType == CameraType.BlackMarket)
    //     {
            
    //         if (IsBlackMarketOpen) return;

    //         if (pauseGameInBlackMarket)
    //         {
    //             timeScaleBeforeBlackMarket = Time.timeScale;
    //             ownsBlackMarketPause = true;
    //             Time.timeScale = 0f;
    //         }
    //         Debug.Log(
    //         "Entered BlackMarket state. Scaled gameplay and doctor " +
    //         "request timers are paused.",
    //         this);
            
    //     }
    //     else
    //     {

    //         RestoreTimeScale();
    //         Debug.Log(
    //         "Returned to MainGame state. Scaled gameplay and doctor:  " + timeScaleBeforeBlackMarket +
    //         "request timers resumed.",
    //         this);
    //     }
        
    // }
    public void SwitchCamera(CameraType newType = CameraType.MainGame)
    {
        currentStateType = newType;

        foreach (var cameraState in cameras)
            cameraState.camera.enabled = cameraState.cameraType == newType;

        ViewStateChanged?.Invoke(newType);

        if (newType == CameraType.BlackMarket)
        {
            timeScaleBeforeBlackMarket = Time.timeScale;
            Time.timeScale = 0f;
        }
        else
        {
            Time.timeScale = timeScaleBeforeBlackMarket;
        }
    }

    private void RestoreTimeScale()
    {
        if (!ownsBlackMarketPause)
            return;
        Time.timeScale = timeScaleBeforeBlackMarket;
        ownsBlackMarketPause = false;
    }
}
