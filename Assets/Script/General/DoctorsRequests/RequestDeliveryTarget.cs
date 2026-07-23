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

    public void ReceiveItem(string itemName, ToolRequestManager.ItemType itemType)
    {
        if (manager != null)
        {
            manager.PlayerSubmittedTool(itemName, itemType);
        }
    }
}
