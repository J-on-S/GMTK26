using System;
using UnityEngine.Events;

[Serializable]
public class ConversationNode
{
    public Conversation conversation;

    public UnityEvent onConversationFinished;
}