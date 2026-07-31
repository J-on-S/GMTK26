using System.Collections.Generic;
using UnityEngine;

public class ConversationFlow : MonoBehaviour
{
    public static ConversationFlow Instance {get; private set;}
    [SerializeField]
    private List<ConversationNode> conversations;

    private int currentIndex;
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
    public void FinishCurrentConversation()
    {
        conversations[currentIndex].onConversationFinished?.Invoke();
        currentIndex++;
    }


    public void NextConversation()
    {
        if (currentIndex >= conversations.Count)
        {
            Debug.LogError("Not enough Conversation");
        }
        ConversationSystem.Instance.StartConversation(conversations[currentIndex].conversation);
    }
}