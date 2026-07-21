using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
//[CreateAssetMenu(fileName = "Dialogue_line", menuName = "Scriptable Objects/Writing/Dialogue_line")]
public class Dialogue_line// : ScriptableObject
{
    [ContextMenuItem("Dialogue line to Word", nameof(DialogueLine_to_word))]
    [SerializeField]
    [TextArea(1, 5)]
    private string dialogue_line;
    
    [SerializeField] private List<Word> words = new List<Word>();
    [SerializeField] private List<Character> characters = new List<Character>();
    public string GetDialogue_text()
    {
        return dialogue_line;
    }
    public List<Character> GetDialogue_characters()
    {
        return characters;
    }
    public void DialogueLine_to_word()
    {
        words = dialogue_line
        .Split(' ')
        .Select(word => new Word(word))
        .ToList();
    }
    public void Words_to_Char()
    {
        characters = new List<Character>();
        for(int i=0; i<words.Count; i++)
        {
            Word word = words[i];
            characters.AddRange(word.Word_to_Char());

            if (i != words.Count - 1)
            {
                characters.Add(new Character());
            }
            
        }
        
    }

}
