using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Word
{
    [ReadOnly] [SerializeField] private string word;
    [SerializeField] private List<SpecialWordEffectType> specialWordEffects = new List<SpecialWordEffectType>();
    public Word(string newWord)
    {
        this.word = newWord;
    }
}
