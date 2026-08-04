using UnityEngine;
using UnityEngine.UI;
using System;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

public class MenuController : MonoBehaviour
{

    [Header ("Volume settings")]
    [SerializeField] private TMP_Text volumeValueText = null;
    [SerializeField] private Slider volumeSlider = null;
    [SerializeField] private float defaultVolume = 1.0f;
    [SerializeField] private AudioEventChannel audioEventChannel;
    

    void Awake()
    {
        EnsureEventSystem();

        if(volumeSlider==null)
	{
            Debug.LogWarning("volumeSlider: please use the dropdown in Inspector to assign me :sob:")
	    return;
	}

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
        
    }

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
    

    // takes player to the story scene (comes after credits)
    public void GoToTutorial()
    {
        SceneManager.LoadScene("Scenes/Story");
    }


    // starts game -- only called after tutorial
    public void StartGameProper()
    {
        SceneManager.LoadScene("Scenes/Game");
    }

    // Propagate volume slider value to channel and text
    public void SetVolume()
    {
        if(volumeSlider==null)
	{
            Debug.LogWarning("volumeSlider: please use the dropdown in Inspector to assign me :sob:")
	    return;
	}
        float volume = volumeSlider.value;
        AudioEventChannel.Instance.SetLevel(Mathf.Log10(Mathf.Clamp(volume, 0.001f, 1.0f)) * 20);
        volumeValueText.text = volume.ToString("0.0");
    }

    // propagate volume slider to playerpref for persistence
    public void VolumeApply()
    {
        // Save value of Volume in variable masterVolume
        if(volumeSlider==null)
	{
            Debug.LogWarning("volumeSlider: please use the dropdown in Inspector to assign me :sob:")
	    return;
	}
        PlayerPrefs.SetFloat("masterVolume", volumeSlider.value);
    }

    public void ResetButton(String MenuType)
    {
        if (MenuType == "Audio")
        {
            volumeSlider.value = defaultVolume;
            volumeValueText.text = defaultVolume.ToString("0.0");
            SetVolume();
            VolumeApply(); //save value
        }
    }
}
