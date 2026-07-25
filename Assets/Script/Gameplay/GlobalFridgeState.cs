using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GlobalFridgeState", menuName = "Scriptable Objects/GlobalFridgeState")]
public class GlobalFridgeState : ScriptableObject
{
    public SortedSet<Fridge> Fridges = new SortedSet<Fridge>();

    public SortedSet<DetachedBodyPart> GetStoredBodyParts()
    {
        var storedBodyParts = new SortedSet<DetachedBodyPart>();
        foreach (var fridge in Fridges)
        {
            foreach (var bodyPart in fridge.StoredBodyParts)
            {
                storedBodyParts.Add(bodyPart);
            }
        }

        return storedBodyParts;
    }
}
