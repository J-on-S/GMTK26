using System;
using UnityEngine;
[Serializable]
[CreateAssetMenu(fileName = "BodyPart", menuName = "Scriptable Objects/BodyPart")]
public class BodyPart : ScriptableObject
{
    [SerializeField] private BodyPartType bodyPartType;
    [SerializeField] private GameObject bodyPartPrefab;
    //maybe mat
    public BodyPartType BodyPartType => bodyPartType;
    public GameObject BodyPartPrefab => bodyPartPrefab;
}