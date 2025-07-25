// File: Scripts/Core/Services/PlayerInventory.cs
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq; // For Find/FirstOrDefault if needed

/// <summary>
/// Manages the complete inventory state for a single player, including bag items and equipped items in specific slots.
/// </summary>
[Serializable] // Making it serializable allows it to potentially be saved directly or inspected if attached to a MonoBehaviour
public class PlayerInventory
{
    // --- Constants ---
    public const int MAX_BAG_SLOTS = 20; // Define maximum number of bag/inventory slots

    // --- Bag Inventory ---
    /// <summary>
    /// List of items currently in the player's bag/inventory (not equipped).
    /// </summary>
    public List<InventorySlot> BagItems = new List<InventorySlot>();

    // --- Equipment Slots ---
    /// <summary>
    /// Dictionary mapping specific equipment slot types to the item currently occupying that slot.
    /// Key: Equipment Slot Type, Value: The InventorySlot representing the equipped item.
    /// </summary>
    public Dictionary<ItemDefinition.EquipmentSlot, InventorySlot> EquippedItems = new Dictionary<ItemDefinition.EquipmentSlot, InventorySlot>();

    // --- Constructor ---
    public PlayerInventory()
    {
        // Initialize the equipped items dictionary with all possible equipment slots
        // This ensures keys exist even if no item is equipped there.
        foreach (ItemDefinition.EquipmentSlot slotType in Enum.GetValues(typeof(ItemDefinition.EquipmentSlot)))
        {
            if (slotType != ItemDefinition.EquipmentSlot.None) // Don't track 'None'
            {
                EquippedItems[slotType] = null; // Start with no item equipped in any slot
            }
        }
    }

    // --- Bag Management Methods ---

    /// <summary>
    /// Finds a bag slot containing an item with the given ID.
    /// </summary>
    public InventorySlot GetBagSlot(string itemId)
    {
        return BagItems.FirstOrDefault(s => s.itemId == itemId);
    }

    /// <summary>
    /// Adds an item to the bag inventory, handling stacking.
    /// </summary>
    public bool AddItemToBag(ItemDefinition itemDef)
    {
        if (itemDef == null)
        {
            Debug.LogWarning("Attempted to add null ItemDefinition to bag.");
            return false;
        }

        // Try to stack if possible
        if (itemDef.isStackable && itemDef.maxStack > 1)
        {
            // Find an existing slot for this item type that isn't full
            var existingSlot = BagItems.FirstOrDefault(slot =>
                slot.ItemDef == itemDef && slot.quantity < itemDef.maxStack);

            if (existingSlot != null)
            {
                existingSlot.quantity++;
                Debug.Log($"Stacked item {itemDef.displayName}. New quantity: {existingSlot.quantity}");
                return true;
            }
        }

        // Add new slot if there's space in the bag
        if (BagItems.Count < MAX_BAG_SLOTS)
        {
            BagItems.Add(new InventorySlot(itemDef));
            Debug.Log($"Added new item {itemDef.displayName} to bag.");
            return true;
        }

        Debug.Log($"Failed to add item {itemDef.displayName}. Bag is full ({BagItems.Count}/{MAX_BAG_SLOTS}).");
        return false; // Bag full or couldn't stack
    }

    /// <summary>
    /// Removes a specified quantity of an item from the bag.
    /// </summary>
    public bool RemoveItemFromBag(string itemId, int quantity = 1)
    {
        var slot = GetBagSlot(itemId);
        if (slot == null)
        {
            Debug.Log($"Cannot remove item from bag, slot not found for ID: {itemId}");
            return false;
        }

        slot.quantity -= quantity;
        if (slot.quantity <= 0)
        {
            BagItems.Remove(slot);
            Debug.Log($"Removed item slot for ID: {itemId} from bag (quantity reached 0)");
        }
        else
        {
            Debug.Log($"Reduced quantity for item ID: {itemId} in bag. New quantity: {slot.quantity}");
        }
        return true;
    }

    // --- Equipment Management Methods ---

    /// <summary>
    /// Gets the item equipped in a specific slot.
    /// </summary>
    public InventorySlot GetEquippedItem(ItemDefinition.EquipmentSlot slotType)
    {
        if (slotType == ItemDefinition.EquipmentSlot.None) return null;

        if (EquippedItems.TryGetValue(slotType, out InventorySlot equippedSlot))
        {
            return equippedSlot; // Can be null if nothing is equipped
        }
        // This case shouldn't happen if constructor initializes correctly
        Debug.LogWarning($"Unexpected equipment slot type requested: {slotType}");
        return null;
    }

    /// <summary>
    /// Attempts to equip an item from the bag to its designated slot.
    /// Handles unequipping any currently equipped item in that slot.
    /// </summary>
    /// <returns>True if the item was successfully equipped or unequipped, false otherwise.</returns>
    public bool EquipItem(string itemId)
    {
        // 1. Find the item in the bag
        var bagSlot = GetBagSlot(itemId);
        if (bagSlot == null || bagSlot.ItemDef == null || bagSlot.ItemDef.equipSlot == ItemDefinition.EquipmentSlot.None)
        {
            Debug.Log($"Cannot equip item ID: {itemId}. Not found in bag, ItemDef null, or not equippable.");
            return false;
        }

        ItemDefinition.EquipmentSlot targetSlotType = bagSlot.ItemDef.equipSlot;

        // 2. Check if it's already equipped (this action then means Unequip)
        if (bagSlot.isEquipped)
        {
            // 3. Unequip Logic
            bagSlot.isEquipped = false;
            EquippedItems[targetSlotType] = null; // Clear the equipped slot
            Debug.Log($"Unequipped item {bagSlot.ItemDef.displayName} from {targetSlotType}.");
            return true;
        }

        // 4. Equip Logic
        // Find any item currently equipped in the target slot
        InventorySlot currentlyEquippedInTargetSlot = GetEquippedItem(targetSlotType);

        // If there's an item equipped, move it back to the bag
        if (currentlyEquippedInTargetSlot != null)
        {
             // Important: Find the slot *instance* in the BagItems list that matches the equipped item
             // We cannot just use currentlyEquippedInTargetSlot directly as it might be a reference
             // that was removed from BagItems when it was equipped.
             // The safest way is to mark it as unequipped and ensure it's in BagItems.
             // Actually, let's rethink this. When an item is equipped, it should be *moved* from BagItems
             // to the EquippedItems dictionary. When unequipped, it should be moved back.
             // This simplifies tracking and prevents duplication.

             // Let's revise the approach:
             // AddItemToBag should *not* create a new slot if one already exists in EquippedItems.
             // We need a way to move items between bag and equipped state.

             // Revised Equip Logic:
             // 1. Find item in bag (done)
             // 2. Check if target slot is free or occupied
             // 3. If occupied, move the equipped item *back* to the bag (creating a new/finding existing bag slot)
             // 4. Move the item from the bag slot to the equipped slot
             // 5. Update isEquipped flags

             // However, the current AddItemToBag/RemoveItemFromBag works on ID/quantity.
             // Moving an *instance* (the InventorySlot object itself) is trickier.

             // Simpler approach for now, consistent with current structure:
             // - When equipping, remove the item from BagItems.
             // - When unequipping, add the item back to BagItems (using AddItemToBag logic).
             // - EquippedItems holds the *reference* to the InventorySlot instance.
             // - isEquipped flag is on the InventorySlot instance.

             // So, unequipping the currently equipped item:
             if (currentlyEquippedInTargetSlot.isEquipped)
             {
                 // Mark it unequipped
                 currentlyEquippedInTargetSlot.isEquipped = false;
                 // Add it back to the bag (this might create a new stack or merge)
                 // We need the ItemDef to add it back
                 bool addedToBag = AddItemToBag(currentlyEquippedInTargetSlot.ItemDef);
                 if (addedToBag)
                 {
                     // Remove the old equipped slot instance from tracking if it was a separate instance
                     // But in this model, it's the same instance. We just moved its location conceptually.
                     // The issue is BagItems held the slot, now EquippedItems holds it, now BagItems holds it again.
                     // This is confusing with the current Add/Remove methods.

                     // Let's stick closer to the original logic but be clear:
                     // Find the *exact slot instance* in BagItems that corresponds to the equipped item
                     // and remove it from BagItems when equipping. This requires storing the instance.
                     // Or, simpler: Don't store the instance in BagItems if it's equipped.
                     // But then GetBagSlot won't find it.

                     // Cleanest way: InventorySlot represents the item. Its location (bag/equipped) is determined
                     // by whether it's in the BagItems list or referenced in EquippedItems dict.
                     // isEquipped flag confirms it.

                     // So, when unequipping:
                     // 1. Set isEquipped = false on the slot instance.
                     // 2. Add the item *definition* back to the bag (AddItemToBag handles new stack or merging).
                     // 3. Remove the reference from EquippedItems dictionary.
                     // 4. The original slot instance might become orphaned unless AddItemToBag merges it correctly.
                     // This is problematic.

                     // Better model:
                     // 1. BagItems list holds InventorySlot instances for bag items.
                     // 2. EquippedItems dict holds references to InventorySlot instances that are equipped.
                     // 3. An InventorySlot instance can only be in ONE place: either BagItems OR EquippedItems.
                     // 4. Equipping means moving the instance from BagItems to EquippedItems.
                     // 5. Unequipping means moving the instance from EquippedItems back to BagItems (AddItemToBag logic).

                     // This requires modifying AddItemToBag to potentially accept an *existing* InventorySlot instance
                     // to merge/stack with, rather than always creating a new one.

                     // For now, let's implement a move operation.

                     // Move currently equipped item back to bag
                     // Remove from equipped dict
                     EquippedItems[targetSlotType] = null;
                     // Add its definition back to bag (might merge or create new)
                     AddItemToBag(currentlyEquippedInTargetSlot.ItemDef);
                     // Note: The 'currentlyEquippedInTargetSlot' instance might now be orphaned
                     // if it was a separate stack. The Bag will have the item, potentially in a different slot instance.
                     // This is a limitation of the current Add/Remove design which works on definitions/IDs.
                     // For prototype, this might be acceptable. For production, consider refactoring Add/Remove
                     // to work with slot instances or have a dedicated Move method.
                }
                else
                {
                    Debug.LogWarning($"Failed to move previously equipped item back to bag when equipping {bagSlot.ItemDef.displayName}. Bag might be full.");
                    // Abort equipping?
                    return false;
                }
             }
        }

        // Now, equip the new item
        // Remove the item's slot from the bag
        if (BagItems.Remove(bagSlot))
        {
            // Mark the slot as equipped
            bagSlot.isEquipped = true;
            // Place the slot instance into the equipped dictionary
            EquippedItems[targetSlotType] = bagSlot;
            Debug.Log($"Equipped item {bagSlot.ItemDef.displayName} to {targetSlotType}.");
            return true;
        }
        else
        {
            // This shouldn't happen if GetBagSlot found it
            Debug.LogError($"Failed to remove item slot {itemId} from BagItems during equip.");
            return false;
        }
    }


    /// <summary>
    /// Gets all items currently in the bag.
    /// </summary>
    public List<InventorySlot> GetAllBagItems()
    {
        return new List<InventorySlot>(BagItems); // Return a copy to prevent direct modification
    }

    /// <summary>
    /// Gets all currently equipped items.
    /// </summary>
    public List<InventorySlot> GetAllEquippedItems()
    {
        return EquippedItems.Values.Where(slot => slot != null).ToList(); // Return a copy/list of non-null equipped slots
    }

    // --- Initialization Method ---
    /// <summary>
    /// Initializes the inventory with a list of starting items.
    /// </summary>
    public void InitializeWithItems(List<ItemDefinition> startingItems)
    {
        // Clear existing state (if any)
        BagItems.Clear();
        // Re-initialize equipped items dictionary (already done in constructor, but safe to reset)
        foreach (var key in EquippedItems.Keys.ToList()) // ToList to avoid modification during iteration
        {
            EquippedItems[key] = null;
        }

        foreach (var item in startingItems)
        {
            if (item != null)
            {
                AddItemToBag(item);
            }
            else
            {
                Debug.LogWarning("Null item found in starting items list.");
            }
        }
        Debug.Log($"PlayerInventory initialized with {startingItems.Count} starting items.");
    }
}