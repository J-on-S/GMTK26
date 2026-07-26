using UnityEngine;

public class TestRandomCustomer : MonoBehaviour
{
    [SerializeField] private CustomersAsset customersAsset;
    [ContextMenu("Generate Random Customer")]
    public void GenerateRandomCustomer()
    {
        if (customersAsset == null)
        {
            Debug.LogError("Assign a CustomersAsset.", this);
            return;
        }

        GameObject customerPrefab =
            customersAsset.GetRandomCustomerAsset();
        if (customerPrefab == null)
            return;

        GameObject customer = Instantiate(
            customerPrefab,
            transform.position,
            Quaternion.identity);
        customersAsset.ApplyRandomMaterial(customer);
    }
}
