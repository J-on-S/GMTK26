using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using System;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class PauseMenu : MonoBehaviour
{
    [SerializeField] private GameObject pauseMenuUI = null;
    
    [Header("Background Music references")]
    [SerializeField] private AudioEventChannel audioEventChannel;
    [SerializeField] private Audio backgroundMusic;
    
    [Serializable] public class GamePausedEvent : UnityEvent {}
    [Serializable] public class GameResumedEvent : UnityEvent {}
    [Header("Events")]
    [SerializeField] private GamePausedEvent onGamePause = new GamePausedEvent();
    [SerializeField] private GameResumedEvent onGameResume = new GameResumedEvent();

    public static PauseMenu instance {get; private set; }
    public static bool isPaused = false;
    public string mainMenuScene;

    private float savedSFX, savedSubmarine, savedPrintNoise, savedMusic, savedRadio, savedDayTransition; 

    private bool isSubMenuOpen = false;
    private CursorLockMode previousCursorLockMode;
    
    private AudioMaster.PlayingClip _playingBackgroundMusic;

    void Awake()
    {
        if (instance != null)
        {
            Destroy(instance.gameObject); // Delete duplicates if we return to the start scene
        }
        instance = this;
        DontDestroyOnLoad(gameObject);

        if (EventSystem.current == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        SceneManager.activeSceneChanged += OnChangeScene;
    }

    void OnChangeScene(Scene previous, Scene next)
    {
        if (next.name == mainMenuScene)
        {
            SceneManager.activeSceneChanged -= OnChangeScene;
            Destroy(gameObject);
        }
    }

    // Update is called once per frame
    void Update()
    {
        // No to menu scene
        if (SceneManager.GetActiveScene().name == mainMenuScene) return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (!isPaused)
            {
                Pause();
            }
            else if (isSubMenuOpen)
            {
                for (var index = 0; index < pauseMenuUI.transform.parent.childCount; index++)
                {
                    var submenu = pauseMenuUI.transform.parent.GetChild(index).gameObject;
                    if (submenu != pauseMenuUI) submenu.SetActive(false);
                }
                isSubMenuOpen = false;
                pauseMenuUI.SetActive(true);
            }
            else
            {
                Resume();
            }
        }
    }

    public void Resume()
    {
        Cursor.lockState = previousCursorLockMode;
        pauseMenuUI.SetActive(false);
        Time.timeScale = 1f;
        isPaused = false;
        audioEventChannel.FadePause(_playingBackgroundMusic, 2.0f);
        onGameResume.Invoke();
    }

    public void Pause()
    {
        previousCursorLockMode = Cursor.lockState;
        Cursor.lockState = CursorLockMode.None;
        pauseMenuUI.SetActive(true);
        Time.timeScale = 0f;
        isPaused = true;
        if (_playingBackgroundMusic != null)
        {
            audioEventChannel.FadeResume(_playingBackgroundMusic);
        }
        else
        {
            _playingBackgroundMusic = audioEventChannel.FadeStart(backgroundMusic);
        }
        onGamePause.Invoke();
    }

    public void LoadMenu()
    {
        Time.timeScale = 1f;
        onGameResume.Invoke();
        audioEventChannel.FadeStop(_playingBackgroundMusic);
        SceneManager.LoadScene(mainMenuScene);
    }

    public void SubMenuOpen()
    {
        isSubMenuOpen = true;
    }

    public void SubMenuClose()
    {
        isSubMenuOpen = false;
    }

    void OnDestroy()
    {
        SceneManager.activeSceneChanged -= OnChangeScene;
    }
}
