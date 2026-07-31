using UnityEngine;
using UnityEngine.SceneManagement;

public class Tutorial : MonoBehaviour
{
    public static Tutorial Instance { get; private set; }
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    public void Start()
    {
        StartTutorial();
    }
    private void StartTutorial()
    {
        ConversationFlow.Instance.NextConversation();
        Debug.LogError("Should have started");
    }
    public void StartGame()
    {
        SceneManager.LoadScene("Scenes/Game");
    }
    public void FinishTutorial()
    {
        
    }
    
}
