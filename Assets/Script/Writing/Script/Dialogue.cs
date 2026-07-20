using System;
using UnityEngine;

[Serializable]
//[CreateAssetMenu(fileName = "Dialogue", menuName = "Scriptable Objects/Writing/Dialogue")]
public class Dialogue// : ScriptableObject
{
    [SerializeField] NPC npc;
    [SerializeField] Dialogue_line dialogue_line;
    public Dialogue_line GetDialogue_line()
    {
        return dialogue_line;
    }
    public NPC GetNPC()
    {
        return npc;
    }

    // ------------- INSPECTOR ----------
    public void SetNPC(NPC newNPC)
    {
        npc = newNPC;
    }
}
