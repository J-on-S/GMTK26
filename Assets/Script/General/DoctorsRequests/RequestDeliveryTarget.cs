using UnityEngine;

public class RequestDeliveryTarget : MonoBehaviour
{
    private ToolRequestManager manager;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        manager = FindFirstObjectByType<ToolRequestManager>();
        if (manager == null)
        {
            Debug.LogError("Can't find a ToolRequestManager");
        }
    }

    // returns boolean indicating if the item was correctly received
    public bool ReceiveItem(string itemName, ItemType itemType)
    {
        if (manager != null)
        {
            // only respawn once the item is given to the doctor

            // check its correct item
            bool isCorrectItem = manager.PlayerSubmittedTool(itemName, itemType);

            if (isCorrectItem)
            {
                // trigger respawn for that item
                TriggerWorldItemRespawn(itemName, itemType);
                return true;
            }
            else
            {
                // otherwise need to give correct item
                Debug.Log("Incorrect item. Item will not respawn.");
                return false;
            }
        }
        return false;
    }

    // find object and trigger its respawn
    private void TriggerWorldItemRespawn(string targetName, ItemType targetType)
    {
        ToolPickup[] allPickups = FindObjectsByType<ToolPickup>(FindObjectsSortMode.None);

        foreach (ToolPickup pickup in allPickups)
        {
            if (pickup.itemName == targetName && pickup.itemType ==targetType)
            {
                pickup.StartRespawnTimer();
                return;
            }
        }
    }
}
