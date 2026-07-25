using System;
using UnityEngine;

[Serializable]
public class BodyPartPos
{
    public Transform bodyPartPos;
    [ReadOnly] public bool hasBodyPart = false;
}