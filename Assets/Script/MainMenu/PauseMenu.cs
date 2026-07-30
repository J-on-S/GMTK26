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
    
    public enum PauseState { Paused, InSubMenu, Resumed }
    public static PauseState CurrentState = PauseState.Resumed;
    
    public string mainMenuScene = "StartScreen";
    private AudioMaster.PlayingClip _playingBackgroundMusic;

    void Awake()
    {
        // An event system is critical for input to register.
        if (EventSystem.current == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.pKey.wasPressedThisFrame)
        {
            // Pause/resume button hit, figure out what was meant.
            switch (CurrentState)
            {
                case PauseState.Resumed:
                    // We were in game, go to the pause menu.
                    Pause();
                    break;
                case PauseState.InSubMenu:
                    // We were paused inside a menu, exit and go to the main pause menu.
                    for (var index = 0; index < pauseMenuUI.transform.parent.childCount; index++)
                    {
                        var submenu = pauseMenuUI.transform.parent.GetChild(index).gameObject;
                        if (submenu != pauseMenuUI) submenu.SetActive(false);
                    }
                    // Exited dialog, but still in the pause menu.
                    CurrentState = PauseState.Paused;
                    pauseMenuUI.SetActive(true);
                    break;
                case PauseState.Paused:
                    // We were paused, go back to the game.
                    Resume();
                    break;
            }
        }
    }

    public void Resume()
    {
        Cursor.lockState = CursorLockMode.Locked;
        pauseMenuUI.SetActive(false);
        Time.timeScale = 1f;
        CurrentState = PauseState.Resumed;
        audioEventChannel.FadePause(_playingBackgroundMusic, 2.0f);
        onGameResume.Invoke();
    }

    public void Pause()
    {
        Cursor.lockState = CursorLockMode.None;
        pauseMenuUI.SetActive(true);
        Time.timeScale = 0f;
        CurrentState = PauseState.Paused;
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
        CurrentState = PauseState.Resumed;
        SceneManager.LoadScene(mainMenuScene);
    }

    public void SubMenuOpen()
    {
        CurrentState = PauseState.InSubMenu;
    }

    public void SubMenuClose()
    {
        CurrentState = PauseState.Resumed;
    }

    public void ExitGame()
    {
        Application.Quit();
    }

    public static bool IsPaused()
    {
        return CurrentState == PauseState.Paused || CurrentState == PauseState.InSubMenu;
    }
}
