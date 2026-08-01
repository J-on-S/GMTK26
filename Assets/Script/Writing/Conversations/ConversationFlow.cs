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
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        currentIndex = 0;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void FinishCurrentConversation()
    {
        if (currentIndex >= conversations.Count) return;

        conversations[currentIndex].onConversationFinished?.Invoke();
        currentIndex++;
    }


    public void NextConversation()
    {
        if (currentIndex >= conversations.Count)
        {
            Debug.LogError($"{name}: no conversation at index {currentIndex}; the list holds {conversations.Count}.", this);
            return;
        }

        ConversationSystem.Instance.StartConversation(conversations[currentIndex].conversation);
    }
}
