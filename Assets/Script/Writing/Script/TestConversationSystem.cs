using UnityEngine;

public class TestConversationSystem : MonoBehaviour
{
    [SerializeField] private Conversation conversationTest;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ConversationFlow.Instance.NextConversation();
        Debug.LogError("Should have started");
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
