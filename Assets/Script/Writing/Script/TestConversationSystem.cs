using UnityEngine;

public class TestConversationSystem : MonoBehaviour
{
    [SerializeField] private Conversation conversationTest;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ConversationSystem.Instance.StartConversation(conversationTest);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
