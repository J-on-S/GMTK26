using System;
using System.Collections.Generic;
using UnityEngine;
[Serializable]
[CreateAssetMenu(fileName = "BodyParts", menuName = "Scriptable Objects/BodyParts")]
public class BodyParts : ScriptableObject
{
    [SerializeField] List<BodyPart> bodyParts = new List<BodyPart>();
    public BodyPart SearchBodyPart(BodyPartType bodyPartType)
    {
        foreach(BodyPart bodyPart in bodyParts)
        {
            if(bodyPart.BodyPartType == bodyPartType)
            {
                return bodyPart;
            }
        }
        Debug.LogError("We didn't found BodyPart, need to assign in the scriptable: "+bodyPartType);
        return null;
    }
}
