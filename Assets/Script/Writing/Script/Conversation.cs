using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
[CreateAssetMenu(fileName = "Conversation", menuName = "Scriptable Objects/Writing/Conversation")]
public class Conversation : ScriptableObject
{
    [ContextMenuItem("Assign NPC To Dialogue", nameof(AssignNPCToDialogue))]
    [SerializeField] private string conversationName;
    [SerializeField] private List<NPC> npcsTalking = new List<NPC>();
    [SerializeField] private List<Dialogue> dialogues = new List<Dialogue>();
    [SerializeField] private Color defaultColor;

    private int currentIndex = 0;
    public void Reset()
    {
        currentIndex = 0;
    }
    public List<Dialogue> GetDialogues()
    {
        return dialogues;
    }
    public bool NextDialogueLine()
    {
        currentIndex++;
        if (currentIndex >= dialogues.Count)
        {
            Debug.Log("currentIndex: "+currentIndex+">= "+dialogues.Count);
            return false;
        }
        return true;
    }
    public bool isConversationFinished()
    {
        if (currentIndex >= dialogues.Count)
        {
            return true;
        }
        return false;
    }
    public Dialogue_line GetCurrentDialogueLine()
    {
        return dialogues[currentIndex].GetDialogue_line();
    }
    public Dialogue GetCurrentDialogue()
    {
        return dialogues[currentIndex];
    }
    public NPC GetCurrentNPC()
    {
        return dialogues[currentIndex].GetNPC();
    }
    
    // ----------- INSPECTOR ---------;
    public void AssignNPCToDialogue()
    {
        int nbNPCTalking = npcsTalking.Count;
        if (nbNPCTalking == 0)
        {
            Debug.LogError("Need to Assign the NPC for this conversation: "+conversationName);
            return;
        }

        int npcIndex = 0;
        foreach(Dialogue dialogue in dialogues)
        {
            dialogue.SetNPC(npcsTalking[npcIndex]);
            npcIndex = (npcIndex+1)%nbNPCTalking;
        }
    }
    [ContextMenu("ALL Dialogue line to Word")]
    public void DialogueLine_to_word()
    {
        foreach(Dialogue dialogue in dialogues)
        {
            dialogue.GetDialogue_line().DialogueLine_to_word();
        }
    }
    [ContextMenu("ALL Dialogues line Words to Character")]
    public void Words_to_Char()
    {
        foreach(Dialogue dialogue in dialogues)
        {
            dialogue.GetDialogue_line().Words_to_Char();
        }
    }

}
