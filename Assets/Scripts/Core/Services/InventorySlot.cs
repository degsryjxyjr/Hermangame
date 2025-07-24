using UnityEngine;
using System;

// Make it serializable so it works with ScriptableObject and Unity's inspector
[Serializable] 
public class InventorySlot
{
    // Store the reference to the ScriptableObject definition
    public ItemDefinition ItemDef; 

    // Instance-specific data
    public string itemId;       // Cache the ID for easier lookup
    public int quantity;
    public bool isEquipped;

    // Constructor that takes an ItemDefinition
    public InventorySlot(ItemDefinition itemDef)
    {
        if (itemDef == null)
        {
            Debug.LogError("Cannot create InventorySlot with null ItemDefinition.");
            return;
        }

        this.ItemDef = itemDef;
        this.itemId = itemDef.itemId; // Use the ID from the definition
        this.quantity = 1;
        this.isEquipped = false;
    }

    // Optional: A parameterless constructor might be needed for Unity serialization in some cases
    // public InventorySlot() { }
}
