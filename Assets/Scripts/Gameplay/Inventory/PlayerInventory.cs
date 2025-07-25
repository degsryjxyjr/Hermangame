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
    /// Attempts to equip or unequip an item by its ID.
    /// Handles moving the item between the bag and equipped slots.
    /// </summary>
    /// <param name="itemId">The ID of the item to toggle.</param>
    /// <returns>True if the action was successfully initiated, false otherwise.</returns>
    public bool EquipItem(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
        {
            Debug.LogWarning("Cannot toggle equipment, itemId is null or empty.");
            return false;
        }

        // --- 1. FIND the InventorySlot instance associated with the itemId ---
        InventorySlot itemSlotToToggle = null;
        ItemDefinition.EquipmentSlot targetSlotType = ItemDefinition.EquipmentSlot.None;

        // a. Search in the Bag
        itemSlotToToggle = this.BagItems.FirstOrDefault(slot => slot != null && slot.itemId == itemId);
        if (itemSlotToToggle != null)
        {
            Debug.Log($"Found item '{itemId}' in BagItems.");
            targetSlotType = itemSlotToToggle.ItemDef?.equipSlot ?? ItemDefinition.EquipmentSlot.None;
        }
        else
        {
            // b. If not in bag, search in Equipped Items
            // We need to iterate the dictionary to find by itemId
            foreach (var kvp in this.EquippedItems)
            {
                if (kvp.Value != null && kvp.Value.itemId == itemId)
                {
                    itemSlotToToggle = kvp.Value;
                    targetSlotType = kvp.Key; // The dictionary key IS the equipment slot type
                    Debug.Log($"Found item '{itemId}' in EquippedItems under slot '{targetSlotType}'.");
                    break; // Found it, stop searching
                }
            }
        }

        // --- 2. VALIDATE the found item ---
        if (itemSlotToToggle == null)
        {
            Debug.LogWarning($"Cannot toggle equipment for item ID: '{itemId}'. Item not found in Bag or Equipped slots.");
            return false;
        }
        if (itemSlotToToggle.ItemDef == null)
        {
            Debug.LogError($"Found InventorySlot for item ID '{itemId}', but ItemDef is null!");
            return false;
        }
        if (targetSlotType == ItemDefinition.EquipmentSlot.None)
        {
            Debug.LogWarning($"Cannot toggle equipment for item ID: '{itemId}'. ItemDef marked as equipment but EquipSlot is None.");
            return false;
        }

        // --- 3. PERFORM the Equip/Unequip Action ---
        if (!itemSlotToToggle.isEquipped)
        {
            // --- ACTION: EQUIP the item ---
            Debug.Log($"Attempting to EQUIP item '{itemSlotToToggle.ItemDef.displayName}' (ID: {itemId}) to slot '{targetSlotType}'.");

            // a. Check for and handle an item already occupying the target slot
            if (this.EquippedItems.TryGetValue(targetSlotType, out InventorySlot previouslyEquippedSlot) && previouslyEquippedSlot != null)
            {
                Debug.Log($"Slot '{targetSlotType}' is occupied by '{previouslyEquippedSlot.ItemDef?.displayName ?? "Unknown Item"}'. Moving it to bag.");
                // i. Mark the old item as unequipped
                previouslyEquippedSlot.isEquipped = false;
                // ii. Clear the equipped slot
                this.EquippedItems[targetSlotType] = null;
                // iii. Ensure the old item is back in the bag list (defensive)
                if (!this.BagItems.Contains(previouslyEquippedSlot))
                {
                    this.BagItems.Add(previouslyEquippedSlot);
                    Debug.Log($"Moved previously equipped item '{previouslyEquippedSlot.itemId}' back to BagItems list.");
                }
                else
                {
                    Debug.Log($"Previously equipped item '{previouslyEquippedSlot.itemId}' was already in BagItems list.");
                }
            }

            // b. Equip the new item
            // i. Update its state flag
            itemSlotToToggle.isEquipped = true;
            // ii. Update the EquippedItems dictionary to point to this item
            this.EquippedItems[targetSlotType] = itemSlotToToggle;
            Debug.Log($"EquippedItems dict for slot '{targetSlotType}' now points to item '{itemId}'.");
            // iii. Remove the newly equipped item from the Bag list
            if (this.BagItems.Remove(itemSlotToToggle))
            {
                Debug.Log($"Removed item '{itemId}' from BagItems list.");
            }
            else
            {
                Debug.LogWarning($"Item '{itemId}' was not found in BagItems list during equip removal (might already be removed).");
            }

            Debug.Log($"Successfully EQUIPPED item '{itemSlotToToggle.ItemDef.displayName}' (ID: {itemId}).");
            return true;
        }
        else
        {
            // --- ACTION: UNEQUIP the item ---
            Debug.Log($"Attempting to UNEQUIP item '{itemSlotToToggle.ItemDef.displayName}' (ID: {itemId}) from slot '{targetSlotType}'.");

            // a. Validate it's actually in the expected equipped slot
            // This check helps catch inconsistencies
            if (!this.EquippedItems.TryGetValue(targetSlotType, out InventorySlot equippedSlotInDict) || equippedSlotInDict != itemSlotToToggle)
            {
                Debug.LogError($"Inconsistency: Item '{itemId}' is marked equipped, but EquippedItems dict for slot '{targetSlotType}' " +
                            $"does not point to this item instance. Dict points to '{equippedSlotInDict?.itemId ?? "null"}'. Attempting to resolve...");
                // Force fix the dictionary
                this.EquippedItems[targetSlotType] = itemSlotToToggle;
                // This might indicate a deeper problem, but let's continue with the unequip logic on the itemSlotToToggle instance.
            }

            // b. Unequip the item
            // i. Update its state flag
            itemSlotToToggle.isEquipped = false;
            // ii. Clear the corresponding slot in the EquippedItems dictionary
            this.EquippedItems[targetSlotType] = null;
            Debug.Log($"Cleared EquippedItems dict for slot '{targetSlotType}'.");
            // iii. Add the newly unequipped item back to the Bag list (defensive)
            if (!this.BagItems.Contains(itemSlotToToggle))
            {
                this.BagItems.Add(itemSlotToToggle);
                Debug.Log($"Added item '{itemId}' back to BagItems list.");
            }
            else
            {
                Debug.Log($"Item '{itemId}' was already present in BagItems list.");
            }

            Debug.Log($"Successfully UNEQUIPPED item '{itemSlotToToggle.ItemDef.displayName}' (ID: {itemId}).");
            return true;
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