using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class InventoryService : MonoBehaviour
{
    public static InventoryService Instance { get; private set; }
    
    // Key: Player Session ID, Value: Their inventory
    private Dictionary<string, PlayerInventory> _playerInventories = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"Duplicate InventoryService instance detected! Destroying {gameObject.name}");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Debug.Log("InventoryService initialized and set as singleton");
    }

    /// <summary>
    /// Initializes a player's inventory with starting items.
    /// </summary>
    public void InitializeInventory(string playerId, List<ItemDefinition> startingItems)
    {
        var inventory = new PlayerInventory();
        foreach (var item in startingItems)
        {
            if (item != null)
            {
                // AddItem method in PlayerInventory now handles ItemDefinition correctly
                inventory.AddItem(item); 
            }
            else
            {
                Debug.LogWarning($"Null item found in starting items for player {playerId}");
            }
        }
        _playerInventories[playerId] = inventory;
        Debug.Log($"Initialized inventory for player {playerId} with {startingItems.Count} starting items.");
    }

    /// <summary>
    /// Adds an item to a player's inventory.
    /// </summary>
    public bool AddItem(string playerId, ItemDefinition item, int quantity = 1)
    {
        if (!_playerInventories.TryGetValue(playerId, out var inventory))
        {
            Debug.LogWarning($"Cannot add item, inventory not found for player: {playerId}");
            return false;
        }

        bool success = true;
        for (int i = 0; i < quantity; i++)
        {
             // PlayerInventory.AddItem now takes ItemDefinition
            if (!inventory.AddItem(item)) 
            {
                success = false; // Might fail if inventory is full or item is null
                // Depending on design, you might want to stop adding if one fails,
                // or continue trying to add others. Here we continue but track success.
            }
        }
        return success;
    }

    /// <summary>
    /// Uses an item from a player's inventory (e.g., consumes a potion or equips gear).
    /// </summary>
    public bool UseItem(string playerId, string itemId)
    {
        if (!_playerInventories.TryGetValue(playerId, out var inventory))
        {
            Debug.LogWarning($"Cannot use item, inventory not found for player: {playerId}");
            return false;
        }

        var slot = inventory.GetSlot(itemId);
        if (slot == null || slot.ItemDef == null)
        {
            Debug.Log($"Cannot use item, slot or ItemDef not found for ID: {itemId}");
            return false;
        }

        var player = PlayerManager.Instance.GetPlayer(playerId);
        if (player == null)
        {
            Debug.LogWarning($"Cannot use item, player connection not found for ID: {playerId}");
            return false;
        }

        bool itemUsed = false;
        switch (slot.ItemDef.itemType)
        {
            case ItemDefinition.ItemType.Consumable:
                Debug.Log($"Using consumable: {slot.ItemDef.displayName} (ID: {slot.itemId}) for player {player.LobbyData.Name}");
                
                // Apply stats modifiers
                // Note: You might want more robust stat handling in PlayerGameData
                player.GameData.stats.currentHealth = Mathf.Clamp(
                    player.GameData.stats.currentHealth + slot.ItemDef.healthModifier,
                    0,
                    player.GameData.stats.maxHealth
                );
                player.GameData.stats.attack += slot.ItemDef.attackModifier;
                player.GameData.stats.defense += slot.ItemDef.defenseModifier;
                player.GameData.stats.magic += slot.ItemDef.magicModifier;

                // Consume the item (reduce quantity/remove slot)
                itemUsed = inventory.RemoveItem(itemId, 1);
                if (itemUsed)
                {
                    Debug.Log($"Consumed item {slot.ItemDef.displayName}. New quantity: {slot.quantity - 1}");
                }
                else
                {
                     Debug.LogWarning($"Failed to remove consumed item {slot.ItemDef.displayName} from inventory.");
                }
                break;

            case ItemDefinition.ItemType.Equipment:
                Debug.Log($"Toggling equipment: {slot.ItemDef.displayName} (ID: {slot.itemId}) for player {player.LobbyData.Name}");
                // Equip/Unequip item
                itemUsed = inventory.EquipItem(itemId);
                if (itemUsed)
                {
                    Debug.Log($"{(slot.isEquipped ? "Equipped" : "Unequipped")} item {slot.ItemDef.displayName}.");
                }
                else
                {
                    Debug.LogWarning($"Failed to toggle equipment for item {slot.ItemDef.displayName}.");
                }
                break;

            default:
                Debug.Log($"No use behavior defined for item type: {slot.ItemDef.itemType} (Item: {slot.ItemDef.displayName})");
                itemUsed = false; // Indicate the "use" action wasn't processed meaningfully
                break;
        }

        return itemUsed; // Return whether the item was successfully used/processed
    }

    /// <summary>
    /// Gets a copy of a player's inventory items.
    /// </summary>
    public List<InventorySlot> GetInventory(string playerId)
    {
        if (_playerInventories.TryGetValue(playerId, out var inventory))
        {
            // Return a copy to prevent external modification of the internal list
            return new List<InventorySlot>(inventory.GetAllItems()); 
        }
        Debug.LogWarning($"Cannot get inventory, not found for player: {playerId}");
        return new List<InventorySlot>(); // Return empty list if not found
    }

    // --- New Method for PlayerManager to Route Messages ---
    /// <summary>
    /// Handles incoming inventory-related messages from a player client.
    /// This method should be called by PlayerManager when an "inventory" message type is received.
    /// </summary>
    public void HandleMessage(string sessionId, Dictionary<string, object> msg)
    {
        if (!_playerInventories.ContainsKey(sessionId))
        {
            Debug.LogWarning($"Inventory message received for unknown player/session: {sessionId}");
            // Optionally send an error message back to the client
            GameServer.Instance.SendToPlayer(sessionId, new { type = "error", message = "Player inventory not found." });
            return;
        }

        if (msg.TryGetValue("action", out var actionObj))
        {
            string action = actionObj.ToString();
            switch (action)
            {
                case "get":
                    Debug.Log($"Player {sessionId} requested inventory data.");
                    SendInventoryUpdate(sessionId);
                    break;

                case "use":
                    if (msg.TryGetValue("itemId", out var itemIdObj))
                    {
                        string itemId = itemIdObj.ToString();
                        Debug.Log($"Player {sessionId} attempting to use item ID: {itemId}");
                        bool success = UseItem(sessionId, itemId);
                        if (success)
                        {
                            Debug.Log($"Item {itemId} used successfully by player {sessionId}. Sending inventory update.");
                            SendInventoryUpdate(sessionId);
                            // TODO: Potentially send a message about the item's specific effect to the client/UI
                        }
                        else
                        {
                            Debug.Log($"Failed to use item {itemId} for player {sessionId}");
                            // Optionally send an error message back to the client
                            GameServer.Instance.SendToPlayer(sessionId, new { type = "error", message = $"Failed to use item {itemId}." });
                        }
                    }
                    else
                    {
                        Debug.LogWarning("Inventory 'use' action missing 'itemId'");
                        GameServer.Instance.SendToPlayer(sessionId, new { type = "error", message = "Missing itemId for use action." });
                    }
                    break;

                // Add cases for other actions like "equip", "discard", "drop" etc. if needed
                // case "equip": ...
                // case "discard": ...
                default:
                    Debug.LogWarning($"Unknown inventory action: {action}");
                    GameServer.Instance.SendToPlayer(sessionId, new { type = "error", message = $"Unknown inventory action: {action}" });
                    break;
            }
        }
        else
        {
            Debug.LogWarning("Inventory message missing 'action' field");
            GameServer.Instance.SendToPlayer(sessionId, new { type = "error", message = "Inventory message missing 'action' field." });
        }
    }

    // --- Helper Method to Send Data to Client ---
    /// <summary>
    /// Sends the current state of a player's inventory to their client.
    /// </summary>
    private void SendInventoryUpdate(string playerId)
    {
        if (!_playerInventories.TryGetValue(playerId, out var inventory))
        {
            Debug.LogWarning($"Cannot send inventory update, inventory not found for player: {playerId}");
            return;
        }

        var itemsToSend = new List<object>();
        foreach (var slot in inventory.GetAllItems())
        {
            // Ensure the ItemDef exists before trying to access its properties
            if (slot.ItemDef != null) 
            {
                
                // Create an anonymous object or a specific DTO for the item data needed by the client
                itemsToSend.Add(new
                {
                    id = slot.itemId, // Use the cached ID from the slot
                    name = slot.ItemDef.displayName, // Get name from ItemDefinition
                    quantity = slot.quantity,
                    // Use the calculated icon path
                    icon = slot.ItemDef.icon != null ?
                        $"images/icons/{slot.ItemDef.itemId}" : // Send base path WITHOUT extension
                        "images/icons/default-item.jpg",
                    // Add other properties the client needs
                    isEquipped = slot.isEquipped,
                    itemType = slot.ItemDef.itemType.ToString() // Send type for client-side logic if needed
                });
            }
            else
            {
                Debug.LogError($"Found InventorySlot with null ItemDef for player {playerId}. Slot ID: {slot.itemId}");
                // Optionally add a placeholder or skip this slot
            }
        }

        // Send the message via GameServer
        GameServer.Instance.SendToPlayer(playerId, new
        {
            type = "inventory_update", // Match the client's expected message type
            items = itemsToSend
        });
        Debug.Log($"Sent inventory update to player {playerId} ({itemsToSend.Count} items).");
    }


    // --- Optional: Method to remove items by ID (e.g., for selling/discarding) ---
    public bool RemoveItem(string playerId, string itemId, int quantity = 1)
    {
         if (_playerInventories.TryGetValue(playerId, out var inventory))
         {
             return inventory.RemoveItem(itemId, quantity);
         }
         return false;
    }
}



[System.Serializable] // Make serializable if you ever want to inspect it directly on a MonoBehaviour
public class PlayerInventory 
{
    public const int MAX_SLOTS = 20;
    
    // Changed to hold InventorySlot objects which now contain ItemDefinition references
    public List<InventorySlot> items = new(); 

    /// <summary>
    /// Finds the slot containing an item with the given ID.
    /// </summary>
    public InventorySlot GetSlot(string itemId)
    {
        // Use the cached itemId in the slot for lookup
        return items.FirstOrDefault(s => s.itemId == itemId); 
    }

    /// <summary>
    /// Adds an item to the inventory, handling stacking.
    /// </summary>
    public bool AddItem(ItemDefinition itemDef)
    {
        if (itemDef == null)
        {
            Debug.LogWarning("Attempted to add null ItemDefinition to inventory.");
            return false;
        }

        // Try to stack if possible
        if (itemDef.isStackable && itemDef.maxStack > 1)
        {
            // Find an existing slot for this item type that isn't full
            var existingSlot = items.FirstOrDefault(slot =>
                slot.ItemDef == itemDef && // Reference comparison is fine for ScriptableObjects
                slot.quantity < itemDef.maxStack);

            if (existingSlot != null)
            {
                existingSlot.quantity++;
                Debug.Log($"Stacked item {itemDef.displayName}. New quantity: {existingSlot.quantity}");
                return true;
            }
        }

        // Add new slot if there's space
        if (items.Count < MAX_SLOTS)
        {
            items.Add(new InventorySlot(itemDef));
            Debug.Log($"Added new item {itemDef.displayName} to inventory.");
            return true;
        }

        Debug.Log($"Failed to add item {itemDef.displayName}. Inventory is full ({MAX_SLOTS}/{MAX_SLOTS}).");
        return false; // Inventory full or couldn't stack
    }

    /// <summary>
    /// Removes a specified quantity of an item from the inventory.
    /// </summary>
    public bool RemoveItem(string itemId, int quantity = 1)
    {
        var slot = GetSlot(itemId);
        if (slot == null)
        {
            Debug.Log($"Cannot remove item, slot not found for ID: {itemId}");
            return false;
        }

        slot.quantity -= quantity;

        if (slot.quantity <= 0)
        {
            items.Remove(slot);
            Debug.Log($"Removed item slot for ID: {itemId} (quantity reached 0)");
        }
        else
        {
             Debug.Log($"Reduced quantity for item ID: {itemId}. New quantity: {slot.quantity}");
        }
        return true;
    }

    /// <summary>
    /// Equips or unequips an item.
    /// </summary>
    public bool EquipItem(string itemId)
    {
        var slotToToggle = GetSlot(itemId);
        if (slotToToggle == null || 
            slotToToggle.ItemDef == null || 
            slotToToggle.ItemDef.equipSlot == ItemDefinition.EquipmentSlot.None)
        {
            Debug.Log($"Cannot equip item ID: {itemId}. Slot not found, ItemDef null, or not equippable.");
            return false;
        }

        // If it's already equipped, just unequip it
        if (slotToToggle.isEquipped)
        {
            slotToToggle.isEquipped = false;
            Debug.Log($"Unequipped item {slotToToggle.ItemDef.displayName}.");
            return true;
        }

        // If equipping, first unequip any item in the same slot
        ItemDefinition.EquipmentSlot targetSlot = slotToToggle.ItemDef.equipSlot;
        var currentlyEquippedInSlot = items.FirstOrDefault(s =>
            s.isEquipped && 
            s.ItemDef != null && 
            s.ItemDef.equipSlot == targetSlot);

        if (currentlyEquippedInSlot != null)
        {
            currentlyEquippedInSlot.isEquipped = false;
            Debug.Log($"Unequipped previous item {currentlyEquippedInSlot.ItemDef.displayName} from slot {targetSlot}.");
        }

        slotToToggle.isEquipped = true;
        Debug.Log($"Equipped item {slotToToggle.ItemDef.displayName} in slot {targetSlot}.");
        return true;
    }

    /// <summary>
    /// Gets all items in the inventory.
    /// </summary>
    public List<InventorySlot> GetAllItems()
    {
        return items;
    }
}
