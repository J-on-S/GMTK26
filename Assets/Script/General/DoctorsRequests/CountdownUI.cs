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
    private float maxIntensity = 15f;     // max intensity of the light


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
        timeRemaining = manager.timeRemaining();    // gets the time remaining from the toolrequestmanager script
        if (timeRemaining > 0)
        {
            timeRemaining -= Time.deltaTime;

            // fixes the negative number messy display
            float safety = Mathf.Max(0, timeRemaining);
            UpdateTimerDisplay(safety);
            FlashScreen(safety);
        }
        else
        {
            timeRemaining = 0;
            UpdateTimerDisplay(timeRemaining);
            // heart loss could also happen here idk
        }
    }

    void UpdateTimerDisplay(float timeToDisplay)
    {
        // show up digital clock style
        float minutes = Mathf.FloorToInt(timeToDisplay / 60);
        float seconds = Mathf.FloorToInt(timeToDisplay % 60);

        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    // checks if within flashing lights threshold, calls function to set the opacity of the light image
    void FlashScreen(float timeToDisplay)
    { // only flash light if within the threshold
        if (timeToDisplay <= flashThreshold)
        {
            if (timeToDisplay % 1f > 0.5f)
            {
                SetFlashOpacity(maxIntensity);
                //channel.Stop(alarm);
                channel.Play(alarm);
            }
            else
            {
                SetFlashOpacity(0f);
            }
        }
        else
        {
            {
                SetFlashOpacity(0f);
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
