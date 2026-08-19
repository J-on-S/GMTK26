using System;
using UnityEngine.Events;

[Serializable]
public class ConversationNode
{
    public Conversation conversation;
    [ReadOnly] public bool hasFinished = false;

    public UnityEvent onConversationFinished;
}