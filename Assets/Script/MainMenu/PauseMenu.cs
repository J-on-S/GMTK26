using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.Audio;
using System;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class PauseMenu : MonoBehaviour
{
    [SerializeField] private GameObject pauseMenuUI = null;
    
    [Header ("Background Music references")]
    [SerializeField] private AudioEventChannel audioEventChannel;
    [SerializeField] private Audio backgroundMusic;
    
    [Serializable]
    public class GamePausedEvent : UnityEvent {}
    [SerializeField] private GamePausedEvent onGamePause = new GamePausedEvent();
    
    [Serializable]
    public class GameResumedEvent : UnityEvent {}
    [SerializeField] private GameResumedEvent onGameResume = new GameResumedEvent();

    public static PauseMenu instance {get; private set; }
    public static bool isPaused = false;
    public string mainMenuScene;

    private float savedSFX, savedSubmarine, savedPrintNoise, savedMusic, savedRadio, savedDayTransition; 

    private Boolean isSubMenuOpen = false;
    
    private AudioMaster.PlayingClip _playingBackgroundMusic;

    void Awake()
    {
        if (instance != null)
        {
            Destroy(instance.gameObject); // Delete duplicates if we return to the start scene
        }
        instance = this;
        DontDestroyOnLoad(gameObject);

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
            if (isPaused && !isSubMenuOpen) // ignore escape if submenu is open
                Resume();
            else
                Pause();
        }
    }

    public void Resume()
    {
        pauseMenuUI.SetActive(false);
        Time.timeScale = 1f;
        isPaused = false;
        audioEventChannel.Stop(_playingBackgroundMusic);
        onGameResume.Invoke();
    }

    public void Pause()
    {
        pauseMenuUI.SetActive(true);
        Time.timeScale = 0f;
        isPaused = true;
        _playingBackgroundMusic = audioEventChannel.Play(backgroundMusic);
        onGamePause.Invoke();
        audioEventChannel.Stop(_playingBackgroundMusic);
    }

    public void LoadMenu()
    {
        Time.timeScale = 1f;
        onGameResume.Invoke();
        audioEventChannel.Stop(_playingBackgroundMusic);
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
