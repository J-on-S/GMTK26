using UnityEngine;
using UnityEngine.SceneManagement;

public class Tutorial : MonoBehaviour
{
    public static Tutorial Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Start()
    {
        StartTutorial();
    }

    private void StartTutorial()
    {
        if (ConversationFlow.Instance == null)
        {
            Debug.LogError($"{name}: no ConversationFlow in the scene, so the tutorial cannot start.", this);
            return;
        }

        ConversationFlow.Instance.NextConversation();
    }

    public void StartGame()
    {
        SceneManager.LoadScene("Scenes/Game");
    }

    public void FinishTutorial()
    {

    }
}
