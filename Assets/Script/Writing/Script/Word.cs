using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Word
{
    [ReadOnly] [SerializeField] private string word;
    [SerializeField] private Color wordColor = Color.white;
    [SerializeField] private SpecialWordEffectType specialWordEffects = SpecialWordEffectType.Default;
    public Word(string newWord)
    {
        this.word = newWord;
    }
    public SpecialWordEffectType GetSpecialWordEffectType()
    {
        return specialWordEffects;
    }
    public List<Character> Word_to_Char()
    {
        List<Character> word_to_char = new List<Character>();
        foreach(char c in word)
        {
            word_to_char.Add(new Character(c, wordColor, specialWordEffects));
        }
        return word_to_char;
    }
}
