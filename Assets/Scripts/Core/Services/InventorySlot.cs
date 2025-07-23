using UnityEngine;
using System;

[Serializable]
public class InventorySlot
{
    public string itemId;
    public int quantity;
    public bool isEquipped;
    
    // Reference to the actual item definition
    [NonSerialized] private ItemDefinition _itemDef;
    public ItemDefinition ItemDef => _itemDef ??= Resources.Load<ItemDefinition>($"Items/{itemId}");

    public InventorySlot(ItemDefinition item, int qty = 1)
    {
        itemId = item.itemId;
        quantity = qty;
        _itemDef = item;
    }

    public bool CanStackWith(ItemDefinition item) => 
        itemId == item.itemId && ItemDef.isStackable;
}