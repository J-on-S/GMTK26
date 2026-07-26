using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class StoredBodyPartStatus
{
    [SerializeField] private int slotNumber;
    [SerializeField] private bool occupied;
    [SerializeField] private DetachedBodyPart bodyPart;
    [SerializeField] private string bodyPartType = "Empty";
    [SerializeField] private float currentHealth;
    [SerializeField] private float maximumHealth;

    public int SlotNumber => slotNumber;
    public bool Occupied => occupied;
    public DetachedBodyPart BodyPart => bodyPart;
    public string BodyPartType => bodyPartType;
    public float CurrentHealth => currentHealth;
    public float MaximumHealth => maximumHealth;

    public void Refresh(int index, DetachedBodyPart storedBodyPart)
    {
        slotNumber = index + 1;
        bodyPart = storedBodyPart;
        occupied = bodyPart != null;

        if (!occupied)
        {
            bodyPartType = "Empty";
            currentHealth = 0f;
            maximumHealth = 0f;
            return;
        }

        bodyPartType = bodyPart.bodyPart != null
            ? bodyPart.bodyPart.BodyPartType.ToString()
            : "Unknown";
        currentHealth = bodyPart.health;
        maximumHealth = bodyPart.maxHealth;
    }
}

public class Fridge : MonoBehaviour, IInteractable
{
    [SerializeField] private GlobalFridgeState globalFridgeState;
    [SerializeField] private Transform[] slots;

    [Header("Runtime storage information")]
    [ReadOnly, SerializeField] private int storedItemCount;
    [ReadOnly, SerializeField]
    private List<StoredBodyPartStatus> storedItemStatuses = new();

    private DetachedBodyPart[] _itemsBySlotIndex;
    public DetachedBodyPart[] StoredBodyParts => _itemsBySlotIndex;
    public int StoredItemCount => storedItemCount;
    public int Capacity =>
        _itemsBySlotIndex != null
            ? _itemsBySlotIndex.Length
            : slots?.Length ?? 0;
    public IReadOnlyList<StoredBodyPartStatus> StoredItemStatuses =>
        storedItemStatuses;

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

        RefreshStoredItemInformation();

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

    private void LateUpdate()
    {
        // Stored parts continue decaying, so update their displayed health
        // after their own Update methods have run.
        RefreshStoredItemInformation();
    }

    private bool TryGetNextFreeSlot(out int slotIndex)
    {
        if (_itemsBySlotIndex == null)
        {
            slotIndex = -1;
            return false;
        }

        for (var i = 0; i < _itemsBySlotIndex.Length; i++)
        {
            if (_itemsBySlotIndex[i] != null) continue;
            if (slots == null ||
                i >= slots.Length ||
                slots[i] == null)
            {
                continue;
            }

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

        if (item.bodyPart != null)
        {
            BodyPartRunSummary.Instance
                .RecordFridgeBodyPartAdded(
                    item.bodyPart.BodyPartType);
        }

        RefreshStoredItemInformation();
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

            if (item.bodyPart != null)
            {
                BodyPartRunSummary.Instance
                    .RecordFridgeBodyPartRemoved(
                        item.bodyPart.BodyPartType);
            }

            item.fridge = null;
            RefreshStoredItemInformation();
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

    /// <summary>
    /// Refreshes every storage slot's occupant, type, and live health for
    /// both the Inspector and other gameplay scripts.
    /// </summary>
    public void RefreshStoredItemInformation()
    {
        int capacity = Capacity;

        while (storedItemStatuses.Count < capacity)
            storedItemStatuses.Add(new StoredBodyPartStatus());

        if (storedItemStatuses.Count > capacity)
        {
            storedItemStatuses.RemoveRange(
                capacity,
                storedItemStatuses.Count - capacity);
        }

        storedItemCount = 0;

        for (int i = 0; i < capacity; i++)
        {
            DetachedBodyPart item =
                _itemsBySlotIndex != null
                    ? _itemsBySlotIndex[i]
                    : null;

            if (item != null)
                storedItemCount++;

            storedItemStatuses[i].Refresh(i, item);
        }
    }

    [ContextMenu("Debug/Print Stored Body Parts")]
    private void DebugPrintStoredBodyParts()
    {
        RefreshStoredItemInformation();
        Debug.Log(
            $"[Storage Debug] {name} contains {storedItemCount}/" +
            $"{Capacity} body parts.",
            this);

        foreach (StoredBodyPartStatus status in storedItemStatuses)
        {
            if (!status.Occupied)
                continue;

            Debug.Log(
                $"[Storage Debug] Slot {status.SlotNumber}: " +
                $"{status.BodyPartType}, health " +
                $"{status.CurrentHealth:0.##}/" +
                $"{status.MaximumHealth:0.##}.",
                status.BodyPart);
        }
    }
}
