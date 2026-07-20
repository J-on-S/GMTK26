using System;
using UnityEngine;

[Serializable]
public class Character
{
    [ReadOnly] [SerializeField] private char character;
    [ReadOnly] [SerializeField] private Color charColor = Color.white;
    [ReadOnly] [SerializeField] private SpecialWordEffectType specialWordEffects = SpecialWordEffectType.Default;
    public Character(char newCharacter, Color newColor, SpecialWordEffectType newSpecialWordEffects)
    {
        this.character = newCharacter;
        this.charColor = newColor;
        this.specialWordEffects = newSpecialWordEffects;
    }
    public Character()
    {
        character = ' ';
    }
    public SpecialWordEffectType GetCharSpecialEffectType()
    {
        return specialWordEffects;
    }
    public Color GetCharColor()
    {
        return charColor;
    }
    public char GetCharacter()
    {
        return character;
    }
}