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
        if (customersPrefab == null || customersPrefab.Count == 0)
        {
            Debug.LogError(
                "CustomersAsset needs at least one customer prefab.",
                this);
            return null;
        }

        int randomIndex =
            UnityEngine.Random.Range(0, customersPrefab.Count);
        return customersPrefab[randomIndex];
    }

    /// <summary>
    /// Applies one random configured material to a spawned customer instance.
    /// The prefab asset itself is never modified.
    /// </summary>
    public void ApplyRandomMaterial(GameObject customerInstance)
    {
        if (customerInstance == null ||
            materials == null ||
            materials.Count == 0)
        {
            return;
        }

        Material randomMaterial =
            materials[UnityEngine.Random.Range(0, materials.Count)];
        Renderer[] renderers =
            customerInstance.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer customerRenderer in renderers)
            customerRenderer.material = randomMaterial;
    }
}
