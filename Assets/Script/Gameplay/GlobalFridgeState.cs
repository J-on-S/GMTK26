using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GlobalFridgeState", menuName = "Scriptable Objects/GlobalFridgeState")]
public class GlobalFridgeState : ScriptableObject
{
    public HashSet<Fridge> Fridges = new HashSet<Fridge>();

    public HashSet<DetachedBodyPart> GetStoredBodyParts()
    {
        var storedBodyParts = new HashSet<DetachedBodyPart>();
        foreach (var fridge in Fridges)
        {
            if (fridge == null) continue;

            var fridgeContents = fridge.StoredBodyParts;
            if (fridgeContents == null) continue;

            foreach (var bodyPart in fridgeContents)
            {
                if (bodyPart == null) continue;
                storedBodyParts.Add(bodyPart);
            }
        }

        return storedBodyParts;
    }
}
