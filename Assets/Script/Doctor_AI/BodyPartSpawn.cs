using System;
using System.Collections.Generic;
using UnityEngine;
[Serializable]
public class BodyPartSpawn
{
    public List<BodyPartPos> bodiesPos = new List<BodyPartPos>();
    public GameObject bodyPartPrefab;
    public void Initialize()
    {
        foreach(BodyPartPos bodyPartPos in bodiesPos)
        {
            bodyPartPos.hasBodyPart = false;
        }
    }
}