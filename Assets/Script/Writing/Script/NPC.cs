using System;
using UnityEngine;

[Serializable]
[CreateAssetMenu(fileName = "NPC", menuName = "Scriptable Objects/Writing/NPC")]
public class NPC : ScriptableObject
{
    [SerializeField] float typingSpeed = 0.05f;
    public float GetTypingSpeed()
    {
        return typingSpeed;
    }
}
