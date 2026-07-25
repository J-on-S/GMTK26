using UnityEngine;
using Random = UnityEngine.Random;

public class Fridge : MonoBehaviour
{
    [SerializeField] private Transform[] slots;
    
    private DetachedBodyPart[] _itemsBySlotIndex;
    public DetachedBodyPart[] StoredBodyParts => _itemsBySlotIndex;

    private void OnEnable()
    {
        _itemsBySlotIndex = new DetachedBodyPart[slots.Length];
    }

    private bool TryGetNextFreeSlot(out int slotIndex)
    {
        for (var i = 0; i < slots.Length; i++)
        {
            if (_itemsBySlotIndex[i] != null) continue;
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
        item.transform.SetParent(slots[index].transform);
        item.transform.localPosition = Vector3.zero;
        if (item.TryGetComponent<Rigidbody>(out var itemRigidbody))
        {
            itemRigidbody.constraints = RigidbodyConstraints.FreezeAll;
        }
        return true;
    }

    public bool TryEvictItemFromFridge(DetachedBodyPart item)
    {
        for (var i = 0; i < slots.Length; i++)
        {
            if (_itemsBySlotIndex[i] != item) continue;
            _itemsBySlotIndex[i] = null;
            item.transform.SetParent(null);
            if (item.TryGetComponent<Rigidbody>(out var itemRigidbody))
            {
                itemRigidbody.constraints = RigidbodyConstraints.None;
            }
            return true;
        }

        return false;
    }
}
