using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [Header ("Background Music references")]
    [SerializeField] private AudioEventChannel audioEventChannel;
    [SerializeField] private Audio backgroundMusic;

    private AudioMaster.PlayingClip _backgroundMusicInstance;
    
    private void OnEnable()
    {
        _backgroundMusicInstance = audioEventChannel.Play(backgroundMusic);
    }

    private void OnDisable()
    {
        audioEventChannel.Stop(_backgroundMusicInstance);
    }
}
