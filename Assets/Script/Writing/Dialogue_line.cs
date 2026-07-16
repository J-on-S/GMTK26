using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
[CreateAssetMenu(fileName = "Dialogue_line", menuName = "Scriptable Objects/Writing/Dialogue_line")]
public class Dialogue_line : ScriptableObject
{
    [ContextMenuItem("Dialogue line to Word", nameof(DialogueLine_to_word))]
    [SerializeField]
    [TextArea(1, 5)]
    private string dialogue_line;
    
    [SerializeField] private List<Word> words = new List<Word>();
    public void DialogueLine_to_word()
    {
        words = dialogue_line
        .Split(' ')
        .Select(word => new Word(word))
        .ToList();
    }

}
