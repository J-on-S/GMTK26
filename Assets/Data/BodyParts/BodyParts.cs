using System;
using System.Collections.Generic;
using UnityEngine;
[Serializable]
[CreateAssetMenu(fileName = "BodyParts", menuName = "Scriptable Objects/BodyParts")]
public class BodyParts : ScriptableObject
{
    private static BodyParts instance;
    public static BodyParts Instance
    {
        get
        {
            if (instance == null)
                instance = Resources.Load<BodyParts>("BodyParts");

            return instance;
        }
    }
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
