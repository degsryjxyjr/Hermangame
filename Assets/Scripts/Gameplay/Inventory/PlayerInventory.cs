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
    /// Attempts to toggle the equipped state of an item.
    /// If the item is in the bag, it equips it (potentially unequipping the item in its slot).
    /// If the item is equipped, it unequips it (moving it back to the bag).
    /// </summary>
    /// <returns>True if the item state was successfully toggled, false otherwise.</returns>
    public bool EquipItem(string itemId)
    {
        // 1. First, try to find the item in the bag
        var targetSlot = GetBagSlot(itemId);

        // 2. If not in the bag, check if it's already equipped
        if (targetSlot == null)
        {
            // Search equipped items for the ID
            // Use .Values.FirstOrDefault to find the slot instance by its itemId property
            targetSlot = EquippedItems.Values.FirstOrDefault(slot => slot != null && slot.itemId == itemId);
        }

        // 3. Validate the slot and item definition
        if (targetSlot == null || targetSlot.ItemDef == null || targetSlot.ItemDef.equipSlot == ItemDefinition.EquipmentSlot.None)
        {
            Debug.Log($"Cannot toggle equip state for item ID: {itemId}. Item not found in bag or equipped slots, ItemDef null, or not equippable.");
            return false;
        }

        ItemDefinition.EquipmentSlot targetSlotType = targetSlot.ItemDef.equipSlot;

        // 4. Determine action based on current state
        if (targetSlot.isEquipped)
        {
            // --- Unequip Logic ---
            targetSlot.isEquipped = false;
            EquippedItems[targetSlotType] = null; // Clear the equipped slot

            // Add the item definition back to the bag
            // AddItemToBag handles creating a new stack or merging with an existing one
            bool addedToBag = AddItemToBag(targetSlot.ItemDef);
            if (!addedToBag)
            {
                // Revert changes if bag is full
                targetSlot.isEquipped = true;
                EquippedItems[targetSlotType] = targetSlot; // Restore equipped state
                Debug.LogWarning($"Failed to move unequipped item {targetSlot.ItemDef.displayName} back to bag. Bag might be full.");
                return false;
            }
            // If AddItemToBag merged with an existing bag slot, the old equipped `targetSlot` instance becomes orphaned.
            // This is generally fine as the item is now correctly represented in the bag.
            // If you need the exact same instance back in the bag, the logic becomes more complex.

            Debug.Log($"Unequipped item {targetSlot.ItemDef.displayName} from {targetSlotType}.");
            return true;
        }
        else
        {
            // --- Equip Logic ---
            // Find any item currently equipped in the target slot
            InventorySlot currentlyEquippedInTargetSlot = GetEquippedItem(targetSlotType);

            // If there's an item equipped, move it back to the bag
            if (currentlyEquippedInTargetSlot != null && currentlyEquippedInTargetSlot.isEquipped)
            {
                // Mark it unequipped
                currentlyEquippedInTargetSlot.isEquipped = false;
                // Add its definition back to the bag
                bool addedToBag = AddItemToBag(currentlyEquippedInTargetSlot.ItemDef);
                if (addedToBag)
                {
                    // Clear the equipped slot reference
                    EquippedItems[targetSlotType] = null;
                }
                else
                {
                    // Revert changes if bag is full
                    currentlyEquippedInTargetSlot.isEquipped = true; // Restore its equipped state
                    Debug.LogWarning($"Failed to move previously equipped item {currentlyEquippedInTargetSlot.ItemDef.displayName} back to bag when equipping {targetSlot.ItemDef.displayName}. Bag might be full.");
                    return false; // Abort equipping
                }
            }

            // Now, equip the target item
            // Remove the item's slot from the bag (since we confirmed it's not equipped)
            if (BagItems.Remove(targetSlot))
            {
                // Mark the slot as equipped
                targetSlot.isEquipped = true;
                // Place the slot instance into the equipped dictionary
                EquippedItems[targetSlotType] = targetSlot;
                Debug.Log($"Equipped item {targetSlot.ItemDef.displayName} to {targetSlotType}.");
                return true;
            }
            else
            {
                // This shouldn't happen if GetBagSlot found it and it wasn't equipped
                Debug.LogError($"Failed to remove item slot {itemId} from BagItems during equip.");
                // Potentially re-set isEquipped = false here if it was somehow set true elsewhere
                return false;
            }
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