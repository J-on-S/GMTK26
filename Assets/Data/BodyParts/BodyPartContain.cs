using System;
using UnityEngine;
[Serializable]
public class BodyPartContain
{
    [SerializeField] private BodyPart bodyPart;
    [ReadOnly] [SerializeField] private bool hasBodyPart = false;
    [ReadOnly] [SerializeField] private GameObject spawnedBodyPartObj;
    public GameObject obj2d;
    [ReadOnly][SerializeField] private Material originalMat;
    public BodyPart BodyPart => bodyPart;
    public bool HasBodyPart
    {
        get;
        set;
    }
    public GameObject SpawnedBodyPartObj => spawnedBodyPartObj;
    public Material OriginalMat => originalMat;

    public BodyPartContain(BodyPart bodyPart, GameObject spawnedBodyPartObj, Material originalMat)
    {
        this.bodyPart = bodyPart;
        this.spawnedBodyPartObj = spawnedBodyPartObj;
        this.originalMat = originalMat;
    }

}