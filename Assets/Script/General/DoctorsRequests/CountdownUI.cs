using UnityEngine;
using TMPro;


// gotta connect this to the other timer stuff maybe call this from elsewhere
public class CountdownUI : MonoBehaviour
{

    // audio stuff for the alarm
    public AudioEventChannel channel;
    public Audio alarm;

    public TextMeshProUGUI timerText;

    // will inevitably need to be changed with stefas script
    public float timeRemaining;
    private ToolRequestManager manager;

    // flashing lights section
    public float flashThreshold = 10f;          // this is how much remaining time will be when the screen starts flashing
    public float maxIntensity = 15f;     // max intensity of the light


    // change to light instead of UI
    public Light flashLight;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        manager = FindFirstObjectByType<ToolRequestManager>();
        if (manager == null)
        {
            Debug.LogError("Can't find a ToolRequestManager");
        }
    }

    // Update is called once per frame
    void Update()
    {
        // without a manager there is no countdown to show; Start already said so once, and this would
        // otherwise throw on every frame for the rest of the session.
        if (manager == null) return;

        // Displays either the active request deadline or the rewarded
        // cooldown/storage window owned by ToolRequestManager.
        timeRemaining = manager.timeRemaining();
        if (timeRemaining > 0)
        {
            // fixes the negative number messy display
            float safety = Mathf.Max(0, timeRemaining);
            UpdateTimerDisplay(safety);

            if (manager.IsRequestActive)
                FlashScreen(safety);
            else
            {
                SetFlashOpacity(0f);
                alarmSoundedThisFlash = false;
            }
        }
        else
        {
            timeRemaining = 0;
            UpdateTimerDisplay(timeRemaining);
            SetFlashOpacity(0f);
            alarmSoundedThisFlash = false;
            // heart loss could also happen here idk
            // 
        }
    }

    void UpdateTimerDisplay(float timeToDisplay)
    {
        if (timerText == null) return;

        // show up digital clock style
        float minutes = Mathf.FloorToInt(timeToDisplay / 60);
        float seconds = Mathf.FloorToInt(timeToDisplay % 60);

        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    /// <summary>Whether the alarm has already sounded for the flash currently on screen.</summary>
    /// <remarks>The flash is a per-frame test, so without this the alarm was fired on every frame it was
    /// lit -- around thirty overlapping copies per half-second flash, each one a fresh AudioSource.</remarks>
    private bool alarmSoundedThisFlash;

    // checks if within flashing lights threshold, calls function to set the opacity of the light image
    void FlashScreen(float timeToDisplay)
    { // only flash light if within the threshold
        if (timeToDisplay <= flashThreshold)
        {
            if (timeToDisplay % 1f > 0.5f)
            {
                SetFlashOpacity(maxIntensity);

                // once per flash, on the frame it lights up
                if (!alarmSoundedThisFlash)
                {
                    alarmSoundedThisFlash = true;
                    if (channel != null && alarm != null)
                    {
                        channel.Play(alarm);   // sound when flashing light
                    }
                }
            }
            else
            {
                SetFlashOpacity(0f);
                alarmSoundedThisFlash = false;
            }
        }
        else
        {
            {
                SetFlashOpacity(0f);
                alarmSoundedThisFlash = false;
            }
        }
    }

    // sets the opacity of the red image
    void SetFlashOpacity(float intensity)
    {

        if (flashLight != null)
        {
            flashLight.intensity = intensity;
        }
    }
}
