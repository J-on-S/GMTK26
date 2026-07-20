using System;

[Flags]
public enum SpecialWordEffectType
{
    Default = 0,
    Big = 1<<0,
    Wave = 1<<1,
    Shaking = 1<<2,

    //2^x ->binary
}