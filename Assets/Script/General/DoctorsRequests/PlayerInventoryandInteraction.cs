using UnityEngine;

public class PlayerInventoryandInteraction : MonoBehaviour
{
    // inventory stuff
    public bool isHoldingItem = false;
    public string heldItemName;
    public ToolRequestManager.ItemType heldItemType;

    public LayerMask interactionLayers;
    public float interactionDistance = 100f;

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            ExecuteInteractionRaycast();
        }
        
    }


    // interact!
    public void ExecuteInteractionRaycast()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, interactionDistance, interactionLayers))
        {
            // click on an item
            ToolPickup pickup = hit.collider.GetComponent<ToolPickup>();
            if (pickup != null)
            {
                TryPickupItem(pickup);
                return;
            }

            // click on the doctor
            RequestDeliveryTarget target = hit.collider.GetComponent<RequestDeliveryTarget>();
            if (target != null)
            {
                TryDeliverItem(target);
                return;
            }
        }
    }

    // maybe just if you pickup another item you automatically take that instead of needing to drop it
    private void TryPickupItem(ToolPickup pickup)
    {
        // removed this part cause i think we should be able to grab stuff and not need to drop it
        //if (isHoldingItem)
        //{
        //    Debug.Log("You're already holding an item!");
        //    return;
        //}

        // get the item data goodness
        heldItemName = pickup.itemName;
        heldItemType = pickup.itemType;
        isHoldingItem = true;

        Debug.Log($"Picked up {heldItemType}: {heldItemName}");


        // starts respawn thing
        pickup.OnItemCollected();
    }

    private void TryDeliverItem(RequestDeliveryTarget target)
    {
        if (!isHoldingItem)
        {
            Debug.Log("You're not holding anything.");
            return;
        }
        // give item 
        target.ReceiveItem(heldItemName, heldItemType);

        // remove from inventory
        isHoldingItem = false;
        heldItemName = "";
    }
}
