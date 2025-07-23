using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class InventoryService : MonoBehaviour
{

    public static InventoryService Instance { get; private set; }
    private Dictionary<string, PlayerInventory> _playerInventories = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(gameObject);
        else
            Instance = this;
    }

    public void InitializeInventory(string playerId, List<ItemDefinition> startingItems)
    {
        var inventory = new PlayerInventory();
        foreach (var item in startingItems)
            inventory.AddItem(item);
        _playerInventories[playerId] = inventory;
    }

    public bool AddItem(string playerId, ItemDefinition item, int quantity = 1)
    {
        if (!_playerInventories.TryGetValue(playerId, out var inventory))
            return false;

        for (int i = 0; i < quantity; i++)
            inventory.AddItem(item);
        
        return true;
    }

    public bool UseItem(string playerId, string itemId)
    {
        if (!_playerInventories.TryGetValue(playerId, out var inventory))
            return false;

        var slot = inventory.GetSlot(itemId);
        if (slot == null) return false;

        var player = PlayerManager.Instance.GetPlayer(playerId);
        if (player == null) return false;

        switch (slot.ItemDef.itemType)
        {
            case ItemDefinition.ItemType.Consumable:
                // Apply stats
                player.GameData.stats.currentHealth = Mathf.Clamp(
                    player.GameData.stats.currentHealth + slot.ItemDef.healthModifier,
                    0,
                    player.GameData.stats.maxHealth
                );

                // Consume item
                return inventory.RemoveItem(itemId);

            case ItemDefinition.ItemType.Equipment:
                return inventory.EquipItem(itemId);

            default:
                Debug.Log($"No use behavior for item type: {slot.ItemDef.itemType}");
                return false;
        }
    }

    public List<InventorySlot> GetInventory(string playerId) => 
        _playerInventories.TryGetValue(playerId, out var inventory) ? inventory.GetAllItems() : new List<InventorySlot>();
}

[System.Serializable]
public class PlayerInventory
    {
        public const int MAX_SLOTS = 20;
        public List<InventorySlot> items = new();

        public InventorySlot GetSlot(string itemId) => items.FirstOrDefault(s => s.itemId == itemId);

        public bool AddItem(ItemDefinition item)
        {
            // Try to stack if possible
            if (item.isStackable)
            {
                var existing = items.FirstOrDefault(slot => 
                    slot.itemId == item.itemId && slot.quantity < item.maxStack);
                
                if (existing != null)
                {
                    existing.quantity++;
                    return true;
                }
            }

            // Add new slot if there's space
            if (items.Count < MAX_SLOTS)
            {
                items.Add(new InventorySlot(item));
                return true;
            }

            return false;
        }

        public bool RemoveItem(string itemId, int quantity = 1)
        {
            var slot = GetSlot(itemId);
            if (slot == null) return false;

            slot.quantity -= quantity;
            if (slot.quantity <= 0)
                items.Remove(slot);
            
            return true;
        }

        public bool EquipItem(string itemId)
        {
            var slot = GetSlot(itemId);
            if (slot == null || slot.ItemDef.equipSlot == ItemDefinition.EquipmentSlot.None)
                return false;

            // Unequip any item in same slot first
            var equippedInSlot = items.FirstOrDefault(s => 
                s.isEquipped && s.ItemDef.equipSlot == slot.ItemDef.equipSlot);
            
            if (equippedInSlot != null)
                equippedInSlot.isEquipped = false;

            slot.isEquipped = !slot.isEquipped;
            return true;
        }

        public List<InventorySlot> GetAllItems() => items;
    }