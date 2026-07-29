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
    public bool ReceiveItem(Item item)
    {
        if (manager != null)
        {
            // only respawn once the item is given to the doctor

            // check its correct item
            bool isCorrectItem = manager.PlayerSubmittedTool(item);

            
            if (isCorrectItem)
            {
                if (item is Tool tool)
                {
                    // trigger respawn for that item
                    TriggerWorldItemRespawn(tool);
                }
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
    private void TriggerWorldItemRespawn(Tool targetTool)
    {
        ToolPickup[] allPickups = FindObjectsByType<ToolPickup>(FindObjectsSortMode.None);

        foreach (ToolPickup pickup in allPickups)
        {
            if (pickup.tool.toolType == targetTool.toolType)
            {
                pickup.StartRespawnTimer();
                return;
            }
        }
    }
}
