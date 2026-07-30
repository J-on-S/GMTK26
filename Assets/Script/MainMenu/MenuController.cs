using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class MenuController : MonoBehaviour
{
    [Header ("General Setting")]
    [SerializeField] private string mainScene = "MainScene";

    [Header ("Volume settings")]
    [SerializeField] private TMP_Text volumeValueText;
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private float defaultVolume = 1.0f;
    [SerializeField] private AudioEventChannel audioEventChannel;

    void Awake()
    {
        // Check whether volume settings were save from the last play-through.
        if (PlayerPrefs.HasKey("masterVolume"))
        {
            // Load volume from disk.
            float localVolume = PlayerPrefs.GetFloat("masterVolume");
            // Update saved volume in UI
            volumeSlider.value = localVolume;
            // Flush slider volume to channel and text hint
            SyncChannelVolumeAndTextWithSlider();
        }
        else
        {
            // Simulate an audio setting reset button press
            ResetButton("Audio");
        }
    }

    void Start()
    {
        SyncChannelVolumeAndTextWithSlider();
    }

    private void Update()
    {
        SyncChannelVolumeAndTextWithSlider();
        PersistSettingsToDisk();
    }

    // play button -- forces the player to see credits first
    public void PlayButton()
    {
        SceneManager.LoadScene("Credits");
    }

    // exits the game
    public void ExitButton()
    {
        Application.Quit();
    }

    // takes player back to menu
    public void BackToMenu()
    {
        SceneManager.LoadScene("StartScreen");
    }

    // takes player to the tutorial scene (comes after credits)
    public void GoToTutorial()
    {
        SceneManager.LoadScene("Tutorial");
    }

    // starts game -- only called after tutorial
    public void StartGameProper()
    {
        SceneManager.LoadScene("Game");
    }
    
    private void SyncChannelVolumeAndTextWithSlider()
    {
        var volume = volumeSlider.value;
        audioEventChannel.SetLevel(Mathf.Log10(Mathf.Clamp(volume, 0.001f, 1.0f)) * 20);
        volumeValueText.text = volume.ToString("0.0");
    }

    private void PersistSettingsToDisk()
    {
        PlayerPrefs.SetFloat("masterVolume", volumeSlider.value);
    }

    public void ResetButton(string menuType)
    {
        if (menuType == "Audio")
        {
            volumeSlider.value = defaultVolume;
            volumeValueText.text = defaultVolume.ToString("0.0");
            SyncChannelVolumeAndTextWithSlider();
            PersistSettingsToDisk(); //save value
        }
    }
}
