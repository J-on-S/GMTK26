using System;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

public class Shelving : MonoBehaviour
{
    [SerializeField] private Transform[] slots;
    
    private GameObject[] _itemsBySlotIndex;

    private void OnEnable()
    {
        _itemsBySlotIndex = new GameObject[slots.Length];
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

    public bool TryAddItemToFreeSlot(GameObject item)
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

    public bool TryEvictItemFromSlot(GameObject item)
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

    private void OnMouseDown()
    {
        var item = _itemsBySlotIndex[Random.Range(0, _itemsBySlotIndex.Length)];
        TryEvictItemFromSlot(item);
    }
}
