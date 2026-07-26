using UnityEngine;

public class TestRandomCustomer : MonoBehaviour
{
    [SerializeField] private CustomersAsset customersAsset;
    [ContextMenu("Generate Random Customer")]
    public void GenerateRandomCustomer()
    {
        Instantiate(customersAsset.GetRandomCustomerAsset(), this.transform.position, Quaternion.identity);
    }
}
