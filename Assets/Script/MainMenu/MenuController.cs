using UnityEngine;
using UnityEngine.UI;
using System;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

public class MenuController : MonoBehaviour
{
    [Header ("General Setting")]
    [SerializeField] private bool useSavedValues = false; // should we load prefs or not
    [SerializeField] private string mainScene = "MainScene";

    [Header ("Volume settings")]
    [SerializeField] private TMP_Text volumeValueText = null;
    [SerializeField] private Slider volumeSlider = null;
    [SerializeField] private float defaultVolume = 1.0f;
    [SerializeField] private AudioEventChannel audioEventChannel;
    
    /*
    [Header ("GamePlay Settings")]
    [SerializeField] private TMP_Text SensTextValue = null;
    [SerializeField] private Slider SensSlide = null;
    [SerializeField] private int defaultSens = 4;
    public int mainControllerSens = 4;

    [Header("Toggle Settings")]
    [SerializeField] private Toggle invertYToggle = null;
    */
    // [Space(10)]

    // [Header("Quality Settings")]
    // [SerializeField] private Slider brightnessSlider = null;
    // [SerializeField] private TMP_Text brightTextValue = null;
    // [SerializeField] private float defaultBrightness = 1;
    // [SerializeField] private TMP_Dropdown qualityDropdown;
    // [Header("FullScreen Settings")]
    // [SerializeField] private Toggle fullScreenToggle;
    // [Space(10)]
    // private int qualityLevel;
    // private bool isFullScreen;
    //private float brightnessLevel;
    /*
    [Header("Confirmation")]
    [SerializeField] private GameObject confirmPrompt = null;
    */

    // [Header ("Resolution Dropdown")]
    // public TMP_Dropdown resolutionDropdown;
    // private Resolution[] resolutions;

    void Awake()
    {
        EnsureEventSystem();

        if (useSavedValues)
        {
            if (PlayerPrefs.HasKey("masterVolume"))
            {
                float localVolume = PlayerPrefs.GetFloat("masterVolume");
                volumeSlider.value = localVolume;

                SetVolume();

                volumeValueText.text = localVolume.ToString("0.0");
            }
            else
            {
                ResetButton("Audio");
            }

            // if (PlayerPrefs.HasKey("masterQuality"))
            // {
            //     int localQuality = PlayerPrefs.GetInt("masterQuality");
            //
            //     qualityDropdown.value = localQuality;
            //     QualitySettings.SetQualityLevel(localQuality);
            // }
            // if (PlayerPrefs.HasKey("masterFullScreen"))
            // {
            //     int localFullScreen = PlayerPrefs.GetInt("masterFullScreen");
            //     if (localFullScreen == 1)
            //     {
            //         Screen.fullScreen = true;
            //         fullScreenToggle.isOn = true;
            //     } else
            //     {
            //         Screen.fullScreen = false;
            //         fullScreenToggle.isOn = false;
            //     }
            // }
            /*
            if (PlayerPrefs.HasKey("masterBrightness"))
            {
                float localBrightness = PlayerPrefs.GetFloat("masterBrightness");

                brightnessSlider.value = localBrightness; 
                // change brightness
            }
            */

        }
    }

    // Some menu scenes (Tutorial, Credits, ...) were authored without an EventSystem, so their Canvas
    // raycaster had nothing to dispatch clicks through and every button was dead. This makes one when the
    // scene has none, matching StartScreen's InputSystemUIInputModule. Guarded on EventSystem.current so a
    // scene that already has one (or a persistent one from another scene) is left alone.
    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;

        GameObject go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }

    void Start()
    {
        SetVolume();
        
        // resolutions = Screen.resolutions;
        // resolutionDropdown.ClearOptions();
        //
        // List<string> options = new List<string>();
        // int currentResolutionIndex = 0;
        //
        // for (int i = 0; i < resolutions.Length; i++)
        // {
        //     string option = resolutions[i].width + " x " + resolutions[i].height;
        //     options.Add(option);
        //     if (resolutions[i].width == Screen.width && resolutions[i].height == Screen.height)
        //     {
        //         currentResolutionIndex = i;
        //     }
        // }
        // resolutionDropdown.AddOptions(options);
        // resolutionDropdown.value = currentResolutionIndex;
        // resolutionDropdown.RefreshShownValue();
    }
    // public void SetResolution(int ResolutionIndex)
    // {
    //     Resolution resolution = resolutions[ResolutionIndex];
    //     Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreen);
    // }

    private void Update()
    {
        SetVolume();
        VolumeApply();
    }


    // buttons that actually matter

    // play button -- forces the player to see credits first
    public void PlayButton()
    {
        SceneManager.LoadScene("Scenes/Game");
    }

    // exits the game
    public void ExitButton()
    {
        Application.Quit();
    }

    // takes player back to menu
    public void BackToMenu()
    {
        SceneManager.LoadScene("Scenes/StartScreen");
    }
    public void GoToCredit()
    {
        SceneManager.LoadScene("Scenes/Credits");
    }
    

    // takes player to the tutorial scene (comes after credits)
    public void GoToTutorial()
    {
        SceneManager.LoadScene("Scenes/Story");
        //SceneManager.LoadScene("Tutorial");
    }


    // starts game -- only called after tutorial
    public void StartGameProper()
    {
        SceneManager.LoadScene("Scenes/Game");
    }
    public void SetVolume()
    {
        float volume = volumeSlider.value;
        audioEventChannel.SetLevel(Mathf.Log10(Mathf.Clamp(volume, 0.001f, 1.0f)) * 20);
        volumeValueText.text = volume.ToString("0.0");
    }

    public void VolumeApply()
    {
        // Save value of Volume in variable masterVolume
        PlayerPrefs.SetFloat("masterVolume", volumeSlider.value);
        // StartCoroutine(ConfirmationBox());
    }

    /*
    public void SetControllerSens(float sensitivity) // we get float
    {
        mainControllerSens = Mathf.RoundToInt(sensitivity); // but we need whole int
        SensTextValue.text = sensitivity.ToString("0"); 
    }

    public void GameplayApply()
    {
        if (invertYToggle.isOn)
        {
            // value is 1 true or 0 false
            PlayerPrefs.SetInt("masterInvertyY",1);
        } else
        {
            PlayerPrefs.SetInt("masterInvertY",0);
        } 
        PlayerPrefs.SetFloat("masterSens", mainControllerSens);
        StartCoroutine(ConfirmationBox());
    }

    public void SetBrightness(float brightness)
    {
        brightnessLevel = brightness;
        brightTextValue.text = brightness.ToString("0.0");
    }
    */
    // public void SetFullScreen(bool isFullScreen)
    // {
    //     this.isFullScreen = isFullScreen;
    // }
    // public void SetQuality(int qualityIndex)
    // {
    //     qualityLevel = qualityIndex;
    // }
    // public void GraphicsApply()
    // {
    //     //PlayerPrefs.SetFloat("masterBrightness",brightnessLevel);
    //     // change your brightness with ur post processing or whatever it is
    //
    //     PlayerPrefs.SetInt("masterQuality", qualityLevel);
    //     QualitySettings.SetQualityLevel(qualityLevel);
    //
    //     PlayerPrefs.SetInt("masterFullScreen", (isFullScreen ? 1 : 0));
    //     Screen.fullScreen = isFullScreen;
    //
    //     // StartCoroutine(ConfirmationBox());
    // }

    public void ResetButton(String MenuType)
    {
        if (MenuType == "Audio")
        {
            volumeSlider.value = defaultVolume;
            volumeValueText.text = defaultVolume.ToString("0.0");
            SetVolume();
            VolumeApply(); //save value
        }
        /*
        if (MenuType == "Gameplay")
        {
            SensTextValue.text = defaultSens.ToString("0");
            SensSlide.value = defaultSens;
            mainControllerSens = defaultSens;
            invertYToggle.isOn = false;
            GameplayApply();
        }
        */
        // if (MenuType == "Graphics")
        // {
        //     // Reset brightness value
        //     // brightnessSlider.value = defaultBrightness;
        //     // brightTextValue.text = defaultBrightness.ToString("0.0");
        //
        //     qualityDropdown.value = 1;
        //     QualitySettings.SetQualityLevel(1);
        //
        //     fullScreenToggle.isOn = false;
        //     Screen.fullScreen = false;
        //
        //     Resolution currentResolution = Screen.currentResolution;
        //     Screen.SetResolution(currentResolution.width, currentResolution.height,Screen.fullScreen);
        //     resolutionDropdown.value = resolutions.Length; // last is max, like the screen
        //     GraphicsApply();
        // }
    }

    /*

    public IEnumerator ConfirmationBox()
    {
        confirmPrompt.SetActive(true);
        // Pauses execution for a specified amount of time. The coroutine resumes after the specified number of seconds has elapsed.
        yield return new WaitForSeconds(2);
        confirmPrompt.SetActive(false);
    }
    */
}
