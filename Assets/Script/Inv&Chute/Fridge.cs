using UnityEngine;

public class Fridge : MonoBehaviour, IInteractable
{
    [SerializeField] private GlobalFridgeState globalFridgeState;
    [SerializeField] private Transform[] slots;
    
    private DetachedBodyPart[] _itemsBySlotIndex;
    public DetachedBodyPart[] StoredBodyParts => _itemsBySlotIndex;

    private void OnEnable()
    {
        if (slots == null || slots.Length == 0)
        {
            Debug.LogError($"{name}: fridge has no slots assigned; nothing can be stored in it.", this);
            _itemsBySlotIndex = new DetachedBodyPart[0];
        }
        else
        {
            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null)
                {
                    Debug.LogError($"{name}: fridge slot {i} is empty; storing into it would drop the part at the world origin.", this);
                }
            }

            _itemsBySlotIndex = new DetachedBodyPart[slots.Length];
        }

        if (globalFridgeState == null)
        {
            Debug.LogError($"{name}: no GlobalFridgeState assigned; this fridge's contents are invisible to the rest of the game.", this);
            return;
        }

        globalFridgeState.Fridges.Add(this);
    }

    private void OnDisable()
    {
        if (globalFridgeState == null) return;
        globalFridgeState.Fridges.Remove(this);
    }

    private bool TryGetNextFreeSlot(out int slotIndex)
    {
        for (var i = 0; i < slots.Length; i++)
        {
            if (_itemsBySlotIndex[i] != null) continue;
            if (slots[i] == null) continue;
            slotIndex = i;
            return true;
        }

        slotIndex = -1;
        return false;
    }

    public bool TryAddItemToFreeSlot(DetachedBodyPart item)
    {
        if (!TryGetNextFreeSlot(out var index)) return false;
        _itemsBySlotIndex[index] = item;
        // false, then the pose is written: keeping the world pose would also rewrite localScale, and a
        // shelved part would come out of the fridge a different size than it went in.
        item.transform.SetParent(slots[index].transform, false);
        item.transform.localPosition = Vector3.zero;
        item.RestoreWorldScale();
        if (item.TryGetComponent<Rigidbody>(out var itemRigidbody))
        {
            itemRigidbody.constraints = RigidbodyConstraints.FreezeAll;
        }

        item.SetCollidersEnabled(true);
        item.fridge = this;
        return true;
    }

    public bool TryEvictItemFromFridge(DetachedBodyPart item)
    {
        for (var i = 0; i < slots.Length; i++)
        {
            if (_itemsBySlotIndex[i] != item) continue;
            _itemsBySlotIndex[i] = null;
            item.DetachToWorld();
            if (item.TryGetComponent<Rigidbody>(out var itemRigidbody))
            {
                itemRigidbody.constraints = RigidbodyConstraints.None;
            }

            item.fridge = null;
            return true;
        }

        return false;
    }

    public void Interact(Interactor player)
    {
        if (player.heldObject == null) return;

        var bodyPart = player.heldObject as DetachedBodyPart;
        if (bodyPart == null) return;

        if (!TryAddItemToFreeSlot(bodyPart)) return;

        bodyPart.ReleaseFromHolder();
    }
}
