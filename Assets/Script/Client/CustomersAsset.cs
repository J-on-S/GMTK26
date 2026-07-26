using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
[CreateAssetMenu(fileName = "CustomersAsset", menuName = "Scriptable Objects/CustomersAsset")]
public class CustomersAsset : ScriptableObject
{
    [SerializeField] private List<GameObject> customersPrefab = new List<GameObject>();
    [SerializeField] private List<Material> materials = new List<Material>();

    public GameObject GetRandomCustomerAsset()
    {
        int randomIndexCustomerAsset = UnityEngine.Random.Range(0, customersPrefab.Count);
        GameObject randomCustomerPrefab = customersPrefab[randomIndexCustomerAsset];
        int randomIndexMat = UnityEngine.Random.Range(0, materials.Count);
        Material randomMat = materials[randomIndexMat];
        foreach (Transform child in randomCustomerPrefab.transform)
        {
            Renderer childRenderer = child.gameObject.GetComponent<Renderer>();
            childRenderer.material = randomMat;
        }
        return randomCustomerPrefab;
    }
}
