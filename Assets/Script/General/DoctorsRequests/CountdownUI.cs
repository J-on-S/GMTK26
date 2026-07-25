using UnityEngine;
using TMPro;
using UnityEngine.UI;


// gotta connect this to the other timer stuff maybe call this from elsewhere
public class CountdownUI : MonoBehaviour
{

    public TextMeshProUGUI timerText;

    // will inevitably need to be changed with stefas script
    public float timeRemaining;
    private ToolRequestManager manager;

    // flashing lights section
    public Image flashLight;
    public float flashThreshold = 10f;          // this is how much remaining time will be when the screen starts flashing
    [Range(01, 1f)] public float maxOpacity = 0.4f;     // max opacity of the light


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
                SetFlashOpacity(maxOpacity);
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
    void SetFlashOpacity(float opacity)
    {
        if (flashLight != null)
        {
            Color c = flashLight.color;
            c.a = opacity;
            flashLight.color = c;
        }
    }
}
